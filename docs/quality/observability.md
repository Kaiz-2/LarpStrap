# Observabilidad de LarpStrap

LarpStrap es una app de escritorio y **no envía telemetría**: no hay métricas remotas, trazas ni alertas, y es una decisión de privacidad (ADR 0004). La única señal es el **log local**:
- se escribe en `%LOCALAPPDATA%\LarpStrap\Logs` y, si esa carpeta no es escribible, en `%TEMP%\LarpStrap\Logs`;
- se rota: como máximo 10 archivos de 5 MB;
- el usuario lo ve y lo exporta desde **Diagnóstico**.

**Formato:** una línea por evento, con nombre estable y campos `clave=valor` de una lista blanca (`AppLog.AllowedKeys`). No es JSON porque lo lee una persona en la app; el formato es igual de fácil de filtrar con `findstr` o `Select-String`.

**Correlación:** cada ejecución escribe su propio archivo, `LarpStrap_<fecha>_<pid>.log`, así que el archivo es el identificador de la ejecución. Hoy todo se inicia desde la interfaz. Cuando llegue la URI del navegador (P4-T01/02) habrá que añadir un campo de punto de entrada (`entryPoint`: UI o URI).

## Preguntas que el log debe responder

| # | Pregunta (soporte o el propio usuario) | Eventos |
|---|---|---|
| 1 | ¿Funcionó el lanzamiento y, si no, por qué? | `Launch.Result` (`result`, `pid`, `durationMs`, `count`) |
| 2 | ¿Cerró o mató LarpStrap mi juego, y lo pedí yo? | `Instance.CloseRequested`, `Instance.ForceKill` (`pid`, `result`). Forzar el cierre siempre requiere confirmación |
| 3 | ¿Mi cliente se cerró solo o lo cerró LarpStrap? | `Instance.Exited` (`pid`, `result` = `Closed` si lo pidió LarpStrap, `ExitedUnexpectedly` si no, y `origin`) |
| 4 | ¿Por qué el panel no se actualizaba y cuánto duró? | `Panel.RefreshFailed` (solo el primer fallo, con el tipo de excepción) y `Panel.RefreshRecovered` (`count` = refrescos fallidos, uno cada 2 s) |
| 5 | ¿Con qué versión, en qué modo, y se perdió la configuración? | `App.Started`, `App.Configured` (`demoMode`, `theme`, `robloxVersion`), `Panel.ModeChanged`, `Settings.RecoveredFromCorruptFile`, `Settings.SaveFailed` |
| 6 | ¿Por qué se cerró LarpStrap? | `App.UnhandledException`, `App.UnobservedTaskException` (tipo de excepción) |

## Catálogo de eventos

| Evento | Nivel | Campos |
|---|---|---|
| `App.Started` | Info | `appVersion` |
| `App.Configured` | Info | `demoMode`, `theme`, `robloxVersion` (carpeta de versión) |
| `App.UnhandledException` | Error | `exception` |
| `App.UnobservedTaskException` | Warning | `exception` |
| `Settings.RecoveredFromCorruptFile` | Warning | — |
| `Settings.SaveFailed` | Warning | `exception` |
| `Panel.ModeChanged` | Info | `demoMode` |
| `Panel.RefreshFailed` | Warning | `exception` |
| `Panel.RefreshRecovered` | Info | `count` |
| `Launch.Result` | Info (Started) o Warning | `result`, `pid`, `durationMs`, `count` |
| `Instance.CloseRequested` | Info, o Warning si falla | `pid`, `result` |
| `Instance.ForceKill` | Info, o Warning si falla | `pid`, `result` |
| `Instance.Exited` | Info | `pid`, `result`, `origin` |

Desde la corrección del defecto B, `InstanceTracker` escribe `Instance.CloseRequested`, `Instance.ForceKill` e `Instance.Exited`. Es el único punto por el que pasan el panel, «Cerrar y lanzar» y el sondeo del lanzamiento. Antes los escribía la interfaz, y se perdían cuando un lanzamiento refrescaba primero.
| `Log.SizeLimitReached` | Warning | — (el archivo deja de crecer) |
| `Update.Checked` | Info, o Warning si se rechaza | `result` (`UpToDate`, `Ready`, `NoRelease`, `Rejected`, `Failed`), `version` (la disponible), `reason` (por qué se rechazó o falló), `origin` (`start` o `manual`) |
| `Update.Started` | Info | `version`: el instalador se abrió y LarpStrap se cierra |
| `Update.StartFailed` / `Update.ChangedOnDisk` | Warning | `exception` / — |

**Nunca se registran:**
- URIs, líneas de comandos, tickets ni cookies;
- mensajes de excepción, que pueden incluir rutas: solo se guarda el tipo;
- texto libre: `AppLog` sustituye cualquier cadena que no sea un identificador corto por `[omitted]`.

Las horas son locales y llevan desfase UTC (`2026-10-13T23:37:43.680+02:00`), para que un log exportado no sea ambiguo en un cambio de horario.

## Verificación (2026-10-13)

- **Tests (SIMULADA):**
  - `PanelLogTests`: fallo y recuperación del refresco, forzar cierre, cerrar, salida inesperada;
  - `AppLogTests`: desfase horario y campos `origin` y `demoMode`;
  - todos fallaron antes de implementarlos.
- **REAL (app):** se ejecutó la app y se leyó el log que escribió:
  - configuración corrupta: `App.Configured demoMode=False theme=System robloxVersion=version-cec3ad5889b447cf` y `Settings.RecoveredFromCorruptFile`;
  - modo demostración, por UI Automation sin teclas y sobre clientes **simulados**: `Instance.ForceKill pid=41000 result=True` y luego `Instance.Exited … result=Closed`; `Instance.CloseRequested pid=41001 result=True` y luego `Instance.Exited … result=Closed`.
- **Sin ejecutar en la app real:** `Panel.ModeChanged`, `Settings.SaveFailed` y `Panel.RefreshFailed`/`Recovered`; los dos últimos están cubiertos por tests. `ModeChanged` y `SaveFailed` **no tienen test**: son una línea dentro de rutas ya existentes.
