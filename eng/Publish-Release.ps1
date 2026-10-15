# Builds the Windows release packages from a clean commit:
#   artifacts/LarpStrap-Setup-<version>-win-x64.exe (+ .sha256)  installer with the wizard (docs/adr/0008)
#   artifacts/LarpStrap-<version>-win-x64.zip (+ .sha256)        portable copy, same files
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
$zip = Join-Path $Output "$name.zip"
$setupName = "LarpStrap-Setup-$version-win-x64.exe"
$setup = Join-Path $Output $setupName
$payload = Join-Path $Output 'payload.zip'
$setupStage = Join-Path $Output 'setup-publish'
Remove-Item $stage, $zip, "$zip.sha256", $setup, "$setup.sha256", $payload, $setupStage -Recurse -Force -ErrorAction SilentlyContinue

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

Compress-Archive -Path $stage -DestinationPath $zip  # keeps the LarpStrap-<version>-win-x64\ root folder

# Installer: the same files as the zip, without its root folder, embedded in a single-file, framework-dependent setup.
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
$hashes = foreach ($file in @($setup, $zip)) {
    $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
    $leaf = Split-Path $file -Leaf
    [IO.File]::WriteAllText((Join-Path (Resolve-Path $Output) "$leaf.sha256"), "$hash  $leaf`n")
    [pscustomobject]@{ File = (Resolve-Path $file).Path; Bytes = (Get-Item $file).Length; Sha256 = $hash }
}

$hashes | ForEach-Object { "Paquete:  $($_.File)"; "Tamaño:   $($_.Bytes) bytes"; "SHA-256:  $($_.Sha256)" }
"Versión:  $version (commit $commit$(if ($AllowDirty -and (git status --porcelain)) { ', árbol con cambios: SOLO PRUEBA' }))"
"Archivos: $((Get-ChildItem $stage -Recurse -File).Count)"
"Firma:    ninguna (ADR 0006). Compruébalo con Get-AuthenticodeSignature: debe decir NotSigned."
