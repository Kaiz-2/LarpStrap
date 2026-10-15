# Manual security check: run against a `dotnet publish` folder (framework-dependent and self-contained) or an
# extracted release. Plants copies of a managed DLL under the names of common non-KnownDLL system libraries next to
# the exe, starts LarpStrap, and lists every module loaded from that folder that is not part of the release.
# PASS = the app is running and logged App.Started, no planted DLL is loaded and the log has no App.UnhandledException. Only the LarpStrap process started here
# is touched; no keys are sent.
param(
    [Parameter(Mandatory)][string]$Dir,
    [string[]]$Canaries = @('dwmapi.dll', 'uxtheme.dll', 'version.dll', 'wtsapi32.dll', 'dwrite.dll', 'cryptbase.dll', 'user32.dll', 'hostfxr.dll', 'hostpolicy.dll'),
    [string]$Source = (Join-Path $Dir 'LarpStrap.Core.dll'),  # any managed DLL; the single-file installer has none next to it
    [string]$StartedEvent = 'App.Started'                     # the installer logs Setup.Started
)
$exe = Get-ChildItem $Dir -Filter 'LarpStrap*.exe' | Select-Object -First 1
$shipped = @(Get-ChildItem $Dir -File | ForEach-Object Name)
$planted = @($Canaries | Where-Object { $shipped -notcontains $_ })
foreach ($name in $planted) { Copy-Item $Source (Join-Path $Dir $name) -Force }

$logs = Join-Path $env:LOCALAPPDATA 'LarpStrap\Logs'
$before = Get-Date
$p = Start-Process $exe.FullName -PassThru
Start-Sleep 8; $p.Refresh()
$alive = -not $p.HasExited
$exitCode = if ($alive) { $null } else { $p.ExitCode }
$fromDir = @()
try { $fromDir = @((Get-Process -Id $p.Id).Modules | Where-Object { $_.FileName -like "$Dir\*" } | ForEach-Object { Split-Path $_.FileName -Leaf }) } catch { }
$log = Get-ChildItem $logs -Filter 'LarpStrap_*.log' | Where-Object LastWriteTime -ge $before | Sort-Object LastWriteTime | Select-Object -Last 1
$crash = $log -and (Select-String -Path $log.FullName -Pattern 'App.UnhandledException' -Quiet)
$started = $log -and (Select-String -Path $log.FullName -Pattern $StartedEvent -SimpleMatch -Quiet)
if (-not $p.HasExited) { $null = $p.CloseMainWindow(); if (-not $p.WaitForExit(8000)) { Stop-Process -Id $p.Id -Force; $p.WaitForExit(5000) | Out-Null } }
foreach ($name in $planted) { Remove-Item (Join-Path $Dir $name) -Force }

$loadedPlanted = @($fromDir | Where-Object { $planted -contains $_ })
"plantadas: $($planted -join ', ')"
"cargadas desde la carpeta y plantadas: $(if ($loadedPlanted) { $loadedPlanted -join ', ' } else { 'ninguna' })"
"proceso vivo a los 8 s: $alive$(if (-not $alive) { " (código $exitCode)" }) · $StartedEvent en el log: $started"
"App.UnhandledException en el log: $crash"
# A process that never started proves nothing: that counts as a failure too.
if ($loadedPlanted -or $crash -or -not $alive -or -not $started) { 'FAIL'; exit 1 } else { 'PASS' }
