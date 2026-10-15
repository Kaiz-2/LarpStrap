# Creates LarpStrap's update-signing key (ECDSA P-256, docs/adr/0009) OUTSIDE the repository, readable only by this
# Windows user, and prints the public key for src/LarpStrap.Core/Updates/UpdateTrust.cs. Run it once; with an existing
# key it only prints the public key. eng/Publish-Release.ps1 signs update.json with it.
# BACK UP the key file (offline: USB, password manager). Lost = installed copies stop updating; leaked = anyone can
# sign updates. Never commit it.
param([string]$KeyFile = $(if ($env:LARPSTRAP_SIGNING_KEY) { $env:LARPSTRAP_SIGNING_KEY } else { Join-Path $env:LOCALAPPDATA 'LarpStrap-Release\update-signing-key.p8' }))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Core
$root = (Resolve-Path (Split-Path $PSScriptRoot -Parent)).Path
$full = [IO.Path]::GetFullPath($KeyFile)
if ($full.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'La clave no puede estar dentro del repositorio.' }

if (Test-Path $full) {
    $key = [Security.Cryptography.CngKey]::Import([IO.File]::ReadAllBytes($full), [Security.Cryptography.CngKeyBlobFormat]::Pkcs8PrivateBlob)
} else {
    $params = New-Object Security.Cryptography.CngKeyCreationParameters
    $params.ExportPolicy = [Security.Cryptography.CngExportPolicies]::AllowPlaintextExport
    $key = [Security.Cryptography.CngKey]::Create([Security.Cryptography.CngAlgorithm]::ECDsaP256, $null, $params)
    New-Item -ItemType Directory (Split-Path $full) -Force | Out-Null
    [IO.File]::WriteAllBytes($full, $key.Export([Security.Cryptography.CngKeyBlobFormat]::Pkcs8PrivateBlob))
    # Only this user: no inherited entries (Administrators, SYSTEM).
    $acl = New-Object Security.AccessControl.FileSecurity
    $acl.SetAccessRuleProtection($true, $false)
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule([Security.Principal.WindowsIdentity]::GetCurrent().User, 'FullControl', 'Allow')))
    Set-Acl $full $acl
    "Clave creada: $full"
    'HAZ UNA COPIA DE SEGURIDAD de ese archivo fuera de este PC y no lo subas nunca al repositorio.'
}

# SubjectPublicKeyInfo for P-256: fixed DER prefix + 0x04 + X + Y (the CNG public blob is an 8-byte header + X + Y).
$blob = $key.Export([Security.Cryptography.CngKeyBlobFormat]::EccPublicBlob)
$prefix = [byte[]](0x30,0x59,0x30,0x13,0x06,0x07,0x2A,0x86,0x48,0xCE,0x3D,0x02,0x01,0x06,0x08,0x2A,0x86,0x48,0xCE,0x3D,0x03,0x01,0x07,0x03,0x42,0x00,0x04)
"Clave pública (UpdateTrust.PublicKeyBase64): $([Convert]::ToBase64String($prefix + $blob[8..71]))"
$key.Dispose()
