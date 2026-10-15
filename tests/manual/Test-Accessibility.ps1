param([string]$Exe)
# Manual accessibility check: walks the 9 screens in demo mode (SIMULATED clients) with UI Automation and lists every
# visible interactive control without an accessible name (Name or LabeledBy) or that cannot take keyboard focus.
# PASS = no problems on any screen. Reads only: no keystrokes, no actions on any client. Restores Settings.json.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [Windows.Automation.AutomationElement]; $T = [Windows.Automation.TreeScope]; $CT = [Windows.Automation.ControlType]
function Cond($p, $v) { New-Object Windows.Automation.PropertyCondition($p, $v) }
function AndC($a, $b) { New-Object Windows.Automation.AndCondition($a, $b) }
$settings = Join-Path $env:LOCALAPPDATA 'LarpStrap\Settings.json'
$had = Test-Path $settings
if ($had) { Copy-Item $settings "$settings.a11ybak" -Force }
# Advanced mode, so every page is in the navigation pane (simple mode hides FastFlags and Diagnóstico).
Set-Content -Encoding utf8 $settings '{ "DemoMode": true, "AdvancedMode": true }'
$interactive = @($CT::Button, $CT::RadioButton, $CT::CheckBox, $CT::Edit, $CT::ComboBox, $CT::ListItem, $CT::Hyperlink, $CT::Slider, $CT::List, $CT::MenuItem)
try {
  $p = Start-Process $Exe -PassThru
  do { Start-Sleep -Milliseconds 100; $p.Refresh() } until ($p.MainWindowHandle -ne 0)
  $root = $null; for ($i = 0; $i -lt 50 -and -not $root; $i++) { $root = $A::RootElement.FindFirst($T::Children, (AndC (Cond $A::ProcessIdProperty $p.Id) (Cond $A::NameProperty 'LarpStrap'))); if (-not $root) { Start-Sleep -Milliseconds 200 } }
  $total = 0
  $pages = 'Inicio', 'Clientes abiertos', 'Accesos directos', 'Dos cuentas', 'Personalización', 'FastFlags', 'Ajustes', 'Diagnóstico', 'Acerca de'
  foreach ($page in $pages) {
    $root.FindFirst($T::Descendants, (AndC (Cond $A::NameProperty $page) (Cond $A::ControlTypeProperty $CT::RadioButton))).GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep 3  # a page that has not finished loading reports missing names and controls
    $all = $root.FindAll($T::Descendants, [Windows.Automation.Condition]::TrueCondition)
    $count = 0; $issues = @()
    foreach ($e in $all) {
      $c = $e.Current
      if ($interactive -notcontains $c.ControlType -or $c.IsOffscreen) { continue }
      $count++
      $label = "$($c.ControlType.ProgrammaticName.Replace('ControlType.',''))#$($c.AutomationId)"
      if ([string]::IsNullOrWhiteSpace($c.Name) -and -not $c.LabeledBy) { $issues += "SIN NOMBRE: $label" }
      if ($c.IsEnabled -and -not $c.IsKeyboardFocusable -and $c.ControlType -ne $CT::List) { $issues += "NO ENFOCABLE: $label '$($c.Name)'" }
    }
    "{0,-20} controles interactivos visibles: {1,2} · problemas: {2}" -f $page, $count, $issues.Count
    $issues | Select-Object -Unique | ForEach-Object { "    $_" }
    $total += $issues.Count
  }
  if ($total) { 'FAIL' } else { 'PASS' }
  $null = $p.CloseMainWindow(); $null = $p.WaitForExit(8000)
} finally {
  Remove-Item $settings, "$settings.tmp" -ErrorAction SilentlyContinue
  if ($had) { Move-Item "$settings.a11ybak" $settings -Force }
  "Settings.json restaurado: $(if ($had) { 'el del usuario' } else { 'no existía, borrado' })"
}
