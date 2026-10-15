# Pruebas de aceptación: Clientes abiertos

Fecha de ejecución: 2026-10-13. Cliente de referencia: Roblox Player 0.742.0.7421053. Equipo: Windows 11 Home.

**Tipos de prueba:**
- **SIMULADA:** usa `FakeProcessProvider`. Verifica la lógica de LarpStrap. **Nunca cuenta como verificación con cliente real.**
- **REAL (lectura):** observa la máquina real sin lanzar, cerrar ni matar ningún cliente.
- **REAL (lanzamiento):** lanza el cliente oficial. Requiere la autorización del usuario.

**Estados:** PASS · FAIL · BLOCKED · NOT VERIFIED

## Resultados

| Id | Criterio | Tipo | Estado | Evidencia |
|---|---|---|---|---|
| AT-01 | Detecta la instalación de Roblox | REAL (lectura) | **PASS** | `Real_installation_detection_reports_what_it_finds`: encontrado `version-cec3ad5889b447cf` |
| AT-02 | Instalación ausente: no lanza nada y lo diagnostica | SIMULADA | **PASS** | `Missing_installation_fails_every_request_without_starting_anything` + `InstallationLocatorTests` (3) |
| AT-03 | Descubre los clientes ya abiertos como «externo» | REAL (lectura) | **PASS** | `Real_discovery_lists_running_clients_as_external`: PID 10280, External, Running |
| AT-04 | Al reiniciar la app, vuelve a descubrir los clientes abiertos | SIMULADA | **PASS** | `A_restarted_app_rediscovers_running_clients` |
| AT-05 | Lanzamiento correcto del cliente | SIMULADA | **PASS** | `A_launch_starts_the_client_and_waits_for_its_window` |
| AT-07 | Tiempo máximo agotado cuando no aparece la ventana | SIMULADA | **PASS** | `Window_that_never_appears_times_out` |
| AT-08 | El proceso termina durante el lanzamiento y se diagnostica | SIMULADA | **PASS** | `Process_that_exits_during_launch_is_reported_with_diagnostics` |
| AT-09 | Fallo al iniciar el proceso, con el tipo de excepción | SIMULADA | **PASS** | `Start_failure_is_reported_with_exception_type` |
| AT-12 | Cancelar no mata un cliente ya iniciado | SIMULADA | **PASS** | `Cancelling_does_not_kill_a_client_that_already_started`, `Cancelled_before_starting_launches_nothing` |
| AT-13 | Una salida inesperada se distingue de un cierre pedido | SIMULADA | **PASS** | `Unexpected_exit_is_reported_as_exited_not_closed` |
| AT-14 | Cerrar o forzar el cierre actúa solo sobre el cliente elegido | SIMULADA | **PASS** | `Closing_one_instance_leaves_the_others_running`, `Force_kill_targets_only_the_given_instance` |
| AT-15 | Con un cliente abierto no se lanza otro; con solo el proceso de bandeja, se explica | SIMULADA | **PASS** | `An_open_client_means_nothing_is_launched`, `Only_the_background_process_blocks_with_its_own_outcome` |
| AT-17 | La interfaz arranca, muestra la ventana y cierra limpiamente | REAL (app, sin Roblox) | **PASS** | Prueba de humo: ventana a los 4,21 s, estable 3 s, código de salida 0 |
| AT-18a | Lanzar 1 cliente real con `--app` y obtener una ventana estable | REAL (lanzamiento) | **PASS** | Intento 2: `Started`, PID 14284, ventana a los 5,9 s, vivo y `Running` en el panel durante más de 60 s |
| AT-18b | Ese cliente llega a una partida utilizable | REAL | **NOT VERIFIED** | `--app` abre la app de Roblox (inicio), no una partida. Desde 2026-10-14 LarpStrap abre enlaces `roblox-player:` (P4-T02): la lógica está probada con tests *simulados* (`A_browser_link_reaches_the_client_as_one_argument_…`), falta probarla con un cliente real |
| AT-24 | Un segundo arranque entrega su enlace al LarpStrap abierto | SIMULADA | **PASS** | `SingleInstanceTests` (4): pipe `CurrentUserOnly`, mensajes sobredimensionados descartados |
| AT-25 | Una salida con informe de error se muestra como crash | SIMULADA | **PASS** | `An_exit_with_a_new_crash_report_is_reported_as_a_crash`, `CrashReportsTests`. Falta un crash real |
| AT-19 | Enfocar y cerrar un cliente real desde el panel | REAL (lanzamiento) | **PASS** | `Focus` = True y la ventana en primer plano pertenece al PID 14284. `Close` (WM_CLOSE) aceptado; el cliente y su CrashHandler terminaron en unos 2 s |
| AT-23 | La app arranca en < 2 s (criterio del spec de app-shell) | REAL (app) | **FAIL** | 4,21 s en la primera medición (build Release, sin optimizar). Se revisará en el módulo app-shell |

AT-06, AT-10, AT-11, AT-16 y AT-20 a AT-22 se retiraron el 2026-10-14.

## Comandos

```
dotnet build LarpStrap.slnx -c Release
dotnet test --solution LarpStrap.slnx -c Release --no-build              # simuladas; las reales se omiten
$env:LARPSTRAP_REAL_CLIENT='1'; dotnet test --project tests/LarpStrap.Core.Tests -c Release --no-build --filter-namespace "*RealClient*"
```

## Registro de pruebas con cliente real

| Fecha | Cliente | Prueba | Duración | Resultado | Notas |
|---|---|---|---|---|---|
| 2026-10-13 | 0.742.0.7421053 | AT-01, AT-03 (solo lectura) | < 1 s | PASS | 1 cliente del usuario detectado como externo; no se actuó sobre él |
| 2026-10-13 19:44 | 0.742.0.7421053 | AT-18 intento 1: `LaunchOrchestrator` + `WindowsProcessProvider`, `--app`, opciones por defecto, sin otros clientes | 32 s | **Parcial** | `Started`, PID 4756, ventana detectada a los 14,3 s; la app de Roblox llegó a cargar su interfaz (stage LuaApp). A los 32 s el cliente se cerró de forma ordenada (`shutDown: (stage:LuaApp)`): **lo cerró el usuario a mano**. No se llegó a enfocar ni cerrar desde el panel |
| 2026-10-13 19:46 | 0.742.0.7421053 | AT-18a + AT-19, intento 2, sin intervención del usuario | 112 s | **PASS** | Lanzamiento → ventana a los 5,9 s → `Running` a +60 s → `Focus` correcto → `Close` → cerrado en unos 2 s, sin procesos residuales |
| 2026-10-13 (auditoría F6) | 0.742.0.7421053 | AT-01, AT-03 (solo lectura) + 2 arranques de la app (demo y real) | < 1 s y 13–33 s | PASS | Cliente del usuario PID 28336 detectado como External/Running; no se actuó sobre él. Ver `docs/quality/final-audit.md` |
| 2026-10-14 12:11 (`/run`) | version-7a0c6d10fcb34a2c (vía Bloxstrap) | Panel de la UI en modo real, solo lectura (AT-03 desde la interfaz) | 1 s | PASS | PID 18680 detectado como Externo y En ejecución, 1,9 GB; primario «Cerrar y lanzar…». No se actuó sobre él: las acciones se hicieron en modo demostración, sobre clientes simulados |
| 2026-10-14 (fase 7) | version-7a0c6d10fcb34a2c (vía Bloxstrap) | Paquete 0.2.0 (7f6e10d) con un cliente ya abierto, solo lectura, 3 arranques | ~2 min | PASS | PID 18804 (iniciado a las 12:23:11) visible como Externo; mismo PID y hora de inicio antes y después. No se lanzó ni se cerró ningún cliente real |
| 2026-10-14 12:45 (fase 7, P5-T08) | version-7a0c6d10fcb34a2c (vía Bloxstrap) | **Paquete 0.2.0** (7f6e10d): Lanzar → Enfocar → Cerrar desde la interfaz, con autorización del usuario y Roblox cerrado antes. Solo UI Automation, sin teclas | 50 s | **PASS** | `Started`, PID 29212, ventana a los 5,4 s (fila «Lanzado por LarpStrap», «En ejecución», 869 MB); estable 30 s; Enfocar: la ventana en primer plano es del PID 29212; Cerrar (WM_CLOSE): terminó en 0,8 s junto con su RobloxCrashHandler, sin procesos residuales. Log: `Launch.Result`, `Instance.CloseRequested`, `Instance.Exited result=Closed origin=Launched` |
