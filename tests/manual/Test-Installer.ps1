param(
    [Parameter(Mandatory)][string]$Setup,      # artifacts\LarpStrap-Setup-<version>-win-x64.exe
    [Parameter(Mandatory)][string]$OldSetup,   # the same build with a lower version (see docs/RELEASE-CHECKLIST.md)
    [Parameter(Mandatory)][string]$WorkDir     # empty scratch folder for custom install folders
)
# End-to-end test of the real installer on this Windows user (silent mode, no keystrokes, no screenshots).
# Touches only what LarpStrap's installer owns: its install folders under $WorkDir and the default one, its two
# shortcuts, HKCU\...\Uninstall\LarpStrap and %LOCALAPPDATA%\LarpStrap\Settings.json (backed up and restored).
# Refuses to run if LarpStrap is already installed, so a real installation is never touched. Never touches Roblox.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\LarpStrap'
$settings = Join-Path $env:LOCALAPPDATA 'LarpStrap\Settings.json'
$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'LarpStrap.lnk'
$desktop = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'LarpStrap.lnk'
$default = Join-Path $env:LOCALAPPDATA 'Programs\LarpStrap'
$version = [version]((Get-Item $Setup).VersionInfo.FileVersion)
$version = "$($version.Major).$($version.Minor).$($version.Build)"
$results = [ordered]@{}

if (Test-Path $key) { throw 'LarpStrap ya está instalado para este usuario: esta prueba no toca una instalación real. Desinstálalo antes.' }
if (Get-Process LarpStrap -ErrorAction SilentlyContinue) { throw 'Cierra LarpStrap antes de la prueba.' }
New-Item -ItemType Directory -Force $WorkDir | Out-Null
$hadSettings = Test-Path $settings
if ($hadSettings) { Copy-Item $settings "$settings.installer-test-bak" -Force }

function Run([string]$exe, [string[]]$arguments) {
    # Not Start-Process -Wait: in Windows PowerShell it also waits for child processes, such as the LarpStrap that --launch opens.
    $p = Start-Process $exe -ArgumentList $arguments -PassThru
    $null = $p.Handle  # keeps the handle so ExitCode is readable after exit
    $p.WaitForExit()
    return $p.ExitCode
}
function Target([string]$lnk) { if (Test-Path $lnk) { (New-Object -ComObject WScript.Shell).CreateShortcut($lnk).TargetPath } }
function WaitUninstalled { for ($i = 0; $i -lt 60 -and (Test-Path $key); $i++) { Start-Sleep -Milliseconds 500 }; Start-Sleep 1 }
function Check([string]$name, [bool]$ok, [string]$detail = '') { $results[$name] = if ($ok) { 'PASS' } else { "FAIL $detail" } }
function Uninstall([string]$dir) {
    # What Windows runs: the registered QuietUninstallString (the installed copy relaunches itself from %TEMP%).
    $quiet = (Get-ItemProperty $key).QuietUninstallString
    $exe = $quiet.Substring(1, $quiet.IndexOf('"', 1) - 1)
    Run $exe @('--uninstall', '--silent') | Out-Null
    WaitUninstalled
}

try {
    # 1-3, 10: install the OLD version into a custom folder with both shortcuts, then check files, shortcuts, metadata.
    $custom = Join-Path $WorkDir 'Custom Folder\LarpStrap'
    $code = Run $OldSetup @('--silent', '--dir', "`"$custom`"")
    $oldVersion = (Get-ItemProperty $key -ErrorAction SilentlyContinue).DisplayVersion
    Check '1 instalación limpia (versión anterior)' ($code -eq 0 -and (Test-Path "$custom\LarpStrap.exe") -and (Test-Path "$custom\LarpStrap.install.json") -and (Test-Path "$custom\LICENSE") -and (Test-Path "$custom\licenses")) "código $code"
    Check '2 carpeta personalizada (con espacios)' ((Get-ItemProperty $key).InstallLocation -eq $custom)
    Check '3 accesos directos' ((Target $startMenu) -eq "$custom\LarpStrap.exe" -and (Target $desktop) -eq "$custom\LarpStrap.exe")

    # 6 (before): a user setting that must survive the upgrade and the uninstall.
    New-Item -ItemType Directory -Force (Split-Path $settings) | Out-Null
    Set-Content -Encoding utf8 $settings '{ "DemoMode": true, "Accent": "Teal" }'
    $settingsHash = (Get-FileHash $settings).Hash

    # 7b: LarpStrap running from the folder blocks the upgrade; nothing changes and Roblox is not involved.
    $app = Start-Process "$custom\LarpStrap.exe" -PassThru
    Start-Sleep 4
    $before = (Get-FileHash "$custom\LarpStrap.dll").Hash
    $code = Run $Setup @('--silent')
    Check '7b la app abierta bloquea la actualización sin cambiar nada' ($code -eq 1 -and (Get-FileHash "$custom\LarpStrap.dll").Hash -eq $before -and (Get-ItemProperty $key).DisplayVersion -eq $oldVersion) "código $code"
    $null = $app.CloseMainWindow(); $null = $app.WaitForExit(10000)

    # 5-6: upgrade in place over the older version; settings untouched; desktop shortcut kept as chosen before.
    $code = Run $Setup @('--silent', '--no-desktop-shortcut')
    $installed = (Get-Item "$custom\LarpStrap.exe").VersionInfo
    Check '5 actualización sobre la versión anterior' ($code -eq 0 -and $oldVersion -ne $version -and (Get-ItemProperty $key).DisplayVersion -eq $version -and $installed.FileVersion -eq "$version.0") "código $code, antes $oldVersion, ahora $((Get-ItemProperty $key).DisplayVersion)"
    Check '6 configuración conservada en la actualización' ((Get-FileHash $settings).Hash -eq $settingsHash)
    Check '3b acceso directo desmarcado quitado' (-not (Test-Path $desktop) -and (Target $startMenu) -eq "$custom\LarpStrap.exe")

    # 8: interrupted installation. Recreate the on-disk state of a swap cut short (old file set aside, new one half
    # written, journal present); the next run must restore and then install cleanly.
    $good = (Get-FileHash "$custom\LarpStrap.exe").Hash
    Move-Item "$custom\LarpStrap.exe" "$custom\LarpStrap.exe.larpstrap-old"
    Set-Content "$custom\LarpStrap.exe" 'half-written'
    Set-Content -Encoding ascii "$custom\.larpstrap-journal.json" '{"Files":[{"Path":"LarpStrap.exe","Existed":true}],"CreatedDirectory":false,"PreviousVersion":null}'
    $code = Run $Setup @('--silent', '--no-desktop-shortcut')
    Check '8 recuperación de una instalación interrumpida' ($code -eq 0 -and (Get-FileHash "$custom\LarpStrap.exe").Hash -eq $good -and -not (Get-ChildItem $custom -Force -Filter '*.larpstrap*')) "código $code"

    # 4: launch after install (--launch starts it as this user, not elevated).
    $code = Run $Setup @('--silent', '--no-desktop-shortcut', '--launch')
    Start-Sleep 5
    $launched = Get-Process LarpStrap -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq "$custom\LarpStrap.exe" }
    Check '4 abrir tras instalar' ($code -eq 0 -and $launched) "código $code"
    $launched | ForEach-Object { $null = $_.CloseMainWindow(); $null = $_.WaitForExit(10000) }

    # 9: uninstall through Windows' registered command; a file the user put there keeps the folder.
    Set-Content "$custom\mis-notas.txt" 'mío'
    Uninstall $custom
    Check '9 desinstalación (registro, accesos directos, archivos)' (-not (Test-Path $key) -and -not (Test-Path $startMenu) -and -not (Test-Path "$custom\LarpStrap.exe") -and -not (Test-Path "$custom\LarpStrap-Setup.exe"))
    Check '9b conserva archivos ajenos' ((Get-ChildItem $custom -Force | ForEach-Object Name) -join ',' -eq 'mis-notas.txt')
    Check '6b configuración conservada en la desinstalación' ((Get-FileHash $settings).Hash -eq $settingsHash)

    # 1b: clean install into the default folder, then uninstall: the folder disappears.
    $code = Run $Setup @('--silent', '--no-desktop-shortcut')
    Check '1b instalación limpia en la carpeta por defecto' ($code -eq 0 -and (Get-ItemProperty $key).InstallLocation -eq $default -and (Test-Path "$default\LarpStrap.exe")) "código $code"
    Uninstall $default
    Check '9c desinstalación borra la carpeta propia' (-not (Test-Path $default) -and -not (Test-Path $key))

    # 7: invalid paths and missing permissions are refused before anything is written.
    $denied = Join-Path $WorkDir 'SinPermiso'
    New-Item -ItemType Directory -Force $denied | Out-Null
    $acl = Get-Acl $denied
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule([Security.Principal.WindowsIdentity]::GetCurrent().Name, 'CreateFiles,CreateDirectories,WriteData', 'Deny')))
    Set-Acl $denied $acl
    $codes = @(
        (Run $Setup @('--silent', '--dir', 'relativa\LarpStrap')),
        (Run $Setup @('--silent', '--dir', "`"$env:windir\LarpStrap`"")),
        (Run $Setup @('--silent', '--dir', "`"$WorkDir`"")),            # not empty, not an installation
        (Run $Setup @('--silent', '--dir', "`"$denied\LarpStrap`"")))
    Check '7 rutas no válidas y sin permiso rechazadas' (($codes | Where-Object { $_ -ne 1 }).Count -eq 0 -and -not (Test-Path $key) -and -not (Test-Path "$env:windir\LarpStrap") -and -not (Test-Path "$denied\LarpStrap")) "códigos $($codes -join ',')"
    Set-Acl $denied (Get-Acl $WorkDir)  # drops the deny rule so the folder can be cleaned up

    # 10: branding and metadata of the installer itself.
    $info = (Get-Item $Setup).VersionInfo
    $signature = (Get-AuthenticodeSignature $Setup).Status
    Check '10 metadatos e icono del instalador' ($info.ProductName -eq 'LarpStrap' -and $info.CompanyName -eq 'Leir' -and $info.FileVersion -eq "$version.0" -and [Drawing.Icon]::ExtractAssociatedIcon($Setup)) "producto $($info.ProductName), empresa $($info.CompanyName), versión $($info.FileVersion)"
    $results['10b firma'] = "INFO $signature (sin firma de código, ADR 0006)"
}
finally {
    Get-Process LarpStrap -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$WorkDir*" -or $_.Path -like "$default*" } | ForEach-Object { $null = $_.CloseMainWindow() }
    if (Test-Path $key) { Uninstall (Get-ItemProperty $key).InstallLocation }
    Remove-Item $settings -ErrorAction SilentlyContinue
    if ($hadSettings) { Move-Item "$settings.installer-test-bak" $settings -Force }
}
$results.GetEnumerator() | ForEach-Object { '{0,-62} {1}' -f $_.Key, $_.Value }
if ($results.Values | Where-Object { $_ -like 'FAIL*' }) { 'FAIL'; exit 1 } else { 'PASS' }
