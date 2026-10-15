param(
    [Parameter(Mandatory)][string]$Setup,
    [Parameter(Mandatory)][string]$WorkDir,   # install folder parent and screenshots
    [switch]$NoScreenshots
)
# Walks the real wizard with UI Automation patterns only (Invoke/Value/Toggle: no keystrokes, nothing typed into other
# windows): validation error, install into $WorkDir, launch from the last page, then uninstall through the registered
# command. Screenshots use PrintWindow, which copies only the installer's own window even if something covers it.
# Refuses to run if LarpStrap is already installed. Never touches Roblox.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
if (-not ('W.Wiz' -as [type])) {
Add-Type -Namespace W -Name Wiz -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PrintWindow(System.IntPtr h, System.IntPtr dc, uint flags);
[DllImport("user32.dll")] public static extern bool GetWindowRect(System.IntPtr h, out RECT r);
public struct RECT { public int Left, Top, Right, Bottom; }
'@
}
$A = [Windows.Automation.AutomationElement]; $T = [Windows.Automation.TreeScope]; $CT = [Windows.Automation.ControlType]
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\LarpStrap'
if (Test-Path $key) { throw 'LarpStrap ya está instalado: esta prueba no toca una instalación real.' }
New-Item -ItemType Directory -Force $WorkDir | Out-Null
$results = [ordered]@{}
function Check([string]$name, [bool]$ok, [string]$detail = '') { $results[$name] = if ($ok) { 'PASS' } else { "FAIL $detail" } }
function Cond($p, $v) { New-Object Windows.Automation.PropertyCondition($p, $v) }
function AndC($a, $b) { New-Object Windows.Automation.AndCondition($a, $b) }

function WindowOf([int]$processId) {
    for ($i = 0; $i -lt 60; $i++) {
        $w = $A::RootElement.FindFirst($T::Children, (AndC (Cond $A::ProcessIdProperty $processId) (Cond $A::NameProperty 'Instalador de LarpStrap')))
        if ($w) { Start-Sleep -Milliseconds 800; return $w }
        Start-Sleep -Milliseconds 250
    }
    throw "No apareció la ventana del instalador (PID $processId)."
}
function Find($w, [string]$name, $type) { $w.FindFirst($T::Descendants, (AndC (Cond $A::NameProperty $name) (Cond $A::ControlTypeProperty $type))) }
function Click($w, [string]$name) { (Find $w $name $CT::Button).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 700 }
function WaitText($w, [string]$text, [int]$seconds = 60) {
    for ($i = 0; $i -lt $seconds * 4; $i++) { if (Find $w $text $CT::Text) { return $true }; Start-Sleep -Milliseconds 250 }
    return $false
}
function Shot($w, [string]$name) {
    if ($NoScreenshots) { return }
    $h = [IntPtr]$w.Current.NativeWindowHandle
    $r = New-Object W.Wiz+RECT; [void][W.Wiz]::GetWindowRect($h, [ref]$r)
    $bmp = New-Object Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
    $g = [Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc()
    [void][W.Wiz]::PrintWindow($h, $dc, 2)  # PW_RENDERFULLCONTENT: the window's own pixels, never the screen
    $g.ReleaseHdc($dc); $g.Dispose(); $bmp.Save((Join-Path $WorkDir "$name.png")); $bmp.Dispose()
}

$target = Join-Path $WorkDir 'Asistente\LarpStrap'
try {
    $p = Start-Process $Setup -PassThru
    $w = WindowOf $p.Id
    Check 'bienvenida: título, versión y botones' ((WaitText $w 'Instalar LarpStrap' 5) -and (Find $w 'Continuar' $CT::Button) -and (Find $w 'Cancelar' $CT::Button))
    Shot $w '01-bienvenida'
    Click $w 'Continuar'

    $box = Find $w 'Carpeta de instalación' $CT::Edit
    $box.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue("$env:windir\LarpStrap")
    Start-Sleep -Milliseconds 500
    $install = Find $w 'Instalar' $CT::Button
    Check 'opciones: carpeta del sistema rechazada antes de instalar' (-not $install.Current.IsEnabled)
    Shot $w '02-opciones-error'
    $box.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($target)
    (Find $w 'Crear un acceso directo en el escritorio' $CT::CheckBox).GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 500
    Shot $w '03-opciones'
    Click $w 'Instalar'

    Check 'instalación: página de resultado tras la comprobación' (WaitText $w 'LarpStrap está instalado')
    Shot $w '04-listo'
    $reg = Get-ItemProperty $key -ErrorAction SilentlyContinue
    Check 'instalado en la carpeta elegida y sin acceso del escritorio' ($reg.InstallLocation -eq $target -and -not (Test-Path (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'LarpStrap.lnk')))
    Click $w 'Finalizar'  # «Abrir LarpStrap ahora» is ticked by default
    Start-Sleep 5
    $app = Get-Process LarpStrap -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq "$target\LarpStrap.exe" }
    Check 'Finalizar abre LarpStrap y cierra el asistente' ($app -and $p.HasExited)
    $app | ForEach-Object { $null = $_.CloseMainWindow(); $null = $_.WaitForExit(10000) }

    # Uninstall as Windows does it: the installed copy relaunches itself from %TEMP%, so look for the new process.
    $before = @(Get-Process LarpStrap-Setup -ErrorAction SilentlyContinue | ForEach-Object Id)  # the temp copy is named LarpStrap-Setup.exe
    Start-Process "$target\LarpStrap-Setup.exe" -ArgumentList '--uninstall' | Out-Null
    $u = $null
    for ($i = 0; $i -lt 40 -and -not $u; $i++) { Start-Sleep -Milliseconds 250; $u = Get-Process LarpStrap-Setup -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Id -and $_.Path -like "$env:TEMP\LarpStrap-Uninstall-*" } | Select-Object -First 1 }
    $uw = WindowOf $u.Id
    Check 'desinstalar: confirmación con lo que se quita y se conserva' ((WaitText $uw 'Se conserva' 5) -and (Find $uw 'Desinstalar' $CT::Button))
    Shot $uw '05-desinstalar'
    Click $uw 'Desinstalar'
    Check 'desinstalación terminada' (WaitText $uw 'LarpStrap se ha desinstalado')
    Shot $uw '06-desinstalado'
    Click $uw 'Finalizar'
    Check 'sin restos: carpeta, registro y menú Inicio' (-not (Test-Path $target) -and -not (Test-Path $key) -and -not (Test-Path (Join-Path ([Environment]::GetFolderPath('Programs')) 'LarpStrap.lnk')))
}
finally {
    Get-Process LarpStrap -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$WorkDir*" } | ForEach-Object { $null = $_.CloseMainWindow() }
    # The downloaded installer keeps its file name as process name (e.g. LarpStrap-Setup-0.3.0-win-x64).
    Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like 'LarpStrap-Setup*' } | ForEach-Object { $null = $_.CloseMainWindow() }
}
$results.GetEnumerator() | ForEach-Object { '{0,-62} {1}' -f $_.Key, $_.Value }
if ($results.Values | Where-Object { $_ -like 'FAIL*' }) { 'FAIL'; exit 1 } else { 'PASS' }
