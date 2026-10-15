# Builds the Windows release packages from a clean commit:
#   artifacts/LarpStrap-Setup-<version>-win-x64.exe (+ .sha256)  the only package: the wizard with the app inside (docs/adr/0008)
#   artifacts/update.json + update.json.sig                       signed manifest for the in-app updater (docs/adr/0009)
# Framework-dependent on purpose: with a self-contained build, DLLs planted next to the exe (uxtheme, wtsapi32,
# dwrite, user32) were loaded by WPF; with the shared .NET runtime they were not (tests/manual/Test-DllPlanting.ps1,
# docs/adr/0007). Nothing is uploaded or published.
param(
    [string]$Output = 'artifacts',
    [switch]$AllowDirty,  # dry runs only: a real package must come from a commit
    [string]$Version      # test builds only (e.g. the older version for tests/manual/Test-Installer.ps1); releases use Directory.Build.props
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

$commit = (git rev-parse --short HEAD).Trim()
if ((git status --porcelain) -and -not $AllowDirty) { throw 'El árbol de trabajo tiene cambios sin commit: el paquete debe salir de un commit.' }

$version = if ($Version) { $Version } else { ([xml](Get-Content Directory.Build.props)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1 }
$versionArg = "-p:Version=$version"
$name = "LarpStrap-$version-win-x64"
$stage = Join-Path $Output $name
$setupName = "LarpStrap-Setup-$version-win-x64.exe"
$setup = Join-Path $Output $setupName
$payload = Join-Path $Output 'payload.zip'
$setupStage = Join-Path $Output 'setup-publish'
$manifest = Join-Path $Output 'update.json'
Remove-Item $stage, $setup, "$setup.sha256", $payload, $setupStage, $manifest, "$manifest.sig" -Recurse -Force -ErrorAction SilentlyContinue

# The update-signing key (eng/New-UpdateSigningKey.ps1) must match the public key built into LarpStrap, or installed
# copies would reject this release. Checked before building anything.
$keyFile = if ($env:LARPSTRAP_SIGNING_KEY) { $env:LARPSTRAP_SIGNING_KEY } else { Join-Path $env:LOCALAPPDATA 'LarpStrap-Release\update-signing-key.p8' }
$signingKey = $null
if (Test-Path $keyFile) {
    Add-Type -AssemblyName System.Core
    $signingKey = [Security.Cryptography.CngKey]::Import([IO.File]::ReadAllBytes($keyFile), [Security.Cryptography.CngKeyBlobFormat]::Pkcs8PrivateBlob)
    $blob = $signingKey.Export([Security.Cryptography.CngKeyBlobFormat]::EccPublicBlob)
    $prefix = [byte[]](0x30,0x59,0x30,0x13,0x06,0x07,0x2A,0x86,0x48,0xCE,0x3D,0x02,0x01,0x06,0x08,0x2A,0x86,0x48,0xCE,0x3D,0x03,0x01,0x07,0x03,0x42,0x00,0x04)
    $public = [Convert]::ToBase64String($prefix + $blob[8..71])
    if (-not (Select-String -Path src/LarpStrap.Core/Updates/UpdateTrust.cs -SimpleMatch -Quiet "`"$public`"")) {
        throw 'La clave de firma no corresponde a UpdateTrust.PublicKeyBase64: las copias instaladas rechazarían esta versión.'
    }
} elseif (-not $AllowDirty) {
    throw "Falta la clave de firma de actualizaciones ($keyFile). Restaura tu copia de seguridad o, solo si es la primera vez, ejecuta eng/New-UpdateSigningKey.ps1."
}

# Always a full rebuild: an incremental build reused a Core DLL compiled with symbols, whose debug record held the
# local obj path (user name). No symbols, mapped source paths, locked restore.
Remove-Item src/*/bin, src/*/obj -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish src/LarpStrap.App -c Release -r win-x64 --self-contained false $versionArg `
    -p:DebugType=none -p:DebugSymbols=false -p:ContinuousIntegrationBuild=true -p:RestoreLockedMode=true -o $stage
if ($LASTEXITCODE) { throw "dotnet publish falló ($LASTEXITCODE)" }

Copy-Item LICENSE, CHANGELOG.md, docs/INSTALLATION.md, docs/KNOWN-LIMITATIONS.md -Destination $stage
$packages = ((dotnet nuget locals global-packages --list) -replace '^global-packages:\s*', '').Trim()
$mvvm = Join-Path $packages 'communitytoolkit.mvvm/8.4.2'
$licenses = New-Item -ItemType Directory (Join-Path $stage 'licenses/CommunityToolkit.Mvvm') -Force
Copy-Item (Join-Path $mvvm 'License.md'), (Join-Path $mvvm 'ThirdPartyNotices.txt') -Destination $licenses

# Privacy gate: no file in the package may contain this machine's user profile path or the git e-mail.
$needles = @("Users\$env:USERNAME", "Users/$env:USERNAME", (git config user.email)) | Where-Object { $_ }
function Assert-NoPersonalData([IO.FileInfo[]]$files) {
    foreach ($file in $files) {
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        $text = [Text.Encoding]::ASCII.GetString($bytes) + [Text.Encoding]::Unicode.GetString($bytes)
        foreach ($needle in $needles) {
            if ($text.IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -ge 0) { throw "Dato personal en $($file.Name); no se empaqueta." }
        }
    }
}
Assert-NoPersonalData (Get-ChildItem $stage -Recurse -File)

# Installer: the published app, without a root folder, embedded in a single-file, framework-dependent setup.
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory((Resolve-Path $stage).Path, (Join-Path (Resolve-Path $Output) 'payload.zip'))
dotnet publish src/LarpStrap.Setup -c Release -r win-x64 $versionArg `
    "-p:PayloadZip=$((Resolve-Path $payload).Path)" `
    -p:DebugType=none -p:DebugSymbols=false -p:ContinuousIntegrationBuild=true -p:RestoreLockedMode=true -o $setupStage
if ($LASTEXITCODE) { throw "dotnet publish del instalador falló ($LASTEXITCODE)" }
$built = @(Get-ChildItem $setupStage -File)
if ($built.Count -ne 1 -or $built[0].Name -ne 'LarpStrap-Setup.exe') { throw "El instalador debía ser un único LarpStrap-Setup.exe; hay: $($built.Name -join ', ')" }
Assert-NoPersonalData $built
Move-Item $built[0].FullName $setup
Remove-Item $payload, $setupStage -Recurse -Force

# LF and no BOM, so `sha256sum -c` works too (Set-Content wrote CRLF in 0.2.0).
$hashes = foreach ($file in @($setup)) {
    $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
    $leaf = Split-Path $file -Leaf
    [IO.File]::WriteAllText((Join-Path (Resolve-Path $Output) "$leaf.sha256"), "$hash  $leaf`n")
    [pscustomobject]@{ File = (Resolve-Path $file).Path; Bytes = (Get-Item $file).Length; Sha256 = $hash }
}

# Signed manifest: compact UTF-8 without BOM; the signature covers these exact bytes (ECDSA P-256, SHA-256, r||s).
if ($signingKey) {
    $setupHash = $hashes | Where-Object { $_.File -like "*$setupName" }
    $json = [ordered]@{ version = $version; file = $setupName; size = $setupHash.Bytes; sha256 = $setupHash.Sha256 } | ConvertTo-Json -Compress
    $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes($json)
    $ecdsa = New-Object Security.Cryptography.ECDsaCng($signingKey)
    $ecdsa.HashAlgorithm = [Security.Cryptography.CngAlgorithm]::Sha256
    [IO.File]::WriteAllBytes((Join-Path (Resolve-Path $Output) 'update.json'), $bytes)
    [IO.File]::WriteAllBytes((Join-Path (Resolve-Path $Output) 'update.json.sig'), $ecdsa.SignData($bytes))
    $ecdsa.Dispose(); $signingKey.Dispose()
    "Manifiesto: $((Resolve-Path $manifest).Path) (+ .sig). Súbelos a la release junto al instalador."
} else {
    'Manifiesto: NO generado (sin clave de firma; solo vale para una prueba).'
}

$hashes | ForEach-Object { "Paquete:  $($_.File)"; "Tamaño:   $($_.Bytes) bytes"; "SHA-256:  $($_.Sha256)" }
"Versión:  $version (commit $commit$(if ($AllowDirty -and (git status --porcelain)) { ', árbol con cambios: SOLO PRUEBA' }))"
"Archivos: $((Get-ChildItem $stage -Recurse -File).Count)"
"Firma:    ninguna (ADR 0006). Compruébalo con Get-AuthenticodeSignature: debe decir NotSigned."
