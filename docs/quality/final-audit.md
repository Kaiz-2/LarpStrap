# Auditoría final de ingeniería (fase 6)

- **Fecha:** 2026-10-13
- **Alcance:** todo el código de `src/` y `tests/` en `main` (04fdb0f) más los cambios sin commit de la revisión de código anterior y de esta auditoría.
- **Entorno:** Windows 11 Home 10.0.26200, .NET SDK 10.0.401, cliente de Roblox 0.742.0.7421053 (abierto por el usuario, PID 28336).

**Cómo leer las pruebas:**
- **SIMULADA:** proveedores de procesos en memoria. Nunca equivale a verificación con cliente real.
- **REAL (lectura):** inspecciona procesos reales sin actuar sobre ellos.
- **REAL (app):** ejecuta LarpStrap de verdad.
- **REAL (lanzamiento):** crea procesos reales de Roblox.

> **Revisión de seguridad posterior:** `docs/quality/security-review.md` (S-1–S-4). La suite final tiene **88 tests: 86 correctos y 2 omitidos (cliente real)**. Observabilidad del log en `docs/quality/observability.md`.

## 1. Resumen

| Severidad | Encontrados | Corregidos y verificados | Pendientes |
|---|---|---|---|
| Alta | 2 | 2 | 0 |
| Media | 4 | 3 | 1 (O-01) |
| Baja / info | 7 | 1 (O-02) | 6 |

La revisión de código previa sobre el mismo diff dejó 10 hallazgos, ya corregidos y resumidos en el §4.

**Veredicto:** **no está lista para preparar la release** de la v1 especificada (ver §9). El panel de instancias es estable para **1 cliente real**. Las 2–4 instancias reales siguen **bloqueadas** y no se afirman.

## 2. Comandos ejecutados

| Comando | Resultado |
|---|---|
| `dotnet format LarpStrap.slnx --verify-no-changes` (inicio) | OK |
| `dotnet build LarpStrap.slnx -c Release` (inicio) | 0 warnings, 0 errores |
| `dotnet test --solution LarpStrap.slnx -c Release` (inicio) | 74: 72 correctos, 2 omitidos (cliente real, opt-in) |
| Tests de regresión de la revisión previa contra el código **anterior** (worktree de `HEAD` + tests nuevos) | 3/3 **fallan** con el código anterior, como debía ser |
| Tests nuevos de esta auditoría antes de corregir | 4/4 **fallan** (RED) |
| `dotnet list LarpStrap.slnx package --vulnerable --include-transitive` | Sin paquetes vulnerables en los 4 proyectos |
| `dotnet list LarpStrap.slnx package --include-transitive` | Ver §7 |
| `dotnet publish src/LarpStrap.App -c Release -r win-x64 --self-contained false` | OK, 656 KB, 8 archivos |
| `measure.ps1` (scratchpad): arranque, CPU y memoria, 2 ejecuciones REAL (app) | Ver §6 |
| `LARPSTRAP_REAL_CLIENT=1 dotnet test … --filter-class "*ReadOnlyRealClientTests"` | 2/2 correctos. REAL (lectura): PID 28336 External/Running; instalación `version-cec3ad5889b447cf` |
| **Final:** `dotnet format --verify-no-changes` | OK. La primera pasada **falló** por 2 líneas de `App.xaml.cs`; corregido con `dotnet format` |
| **Final:** `dotnet build -c Release` | 0 warnings, 0 errores |
| **Final:** `dotnet test --solution LarpStrap.slnx -c Release` | **80: 78 correctos, 0 fallos, 2 omitidos** (los de cliente real; ejecutados aparte arriba) |

## 3. Hallazgos de esta auditoría

### F-01 · Alta · Cancelar «Cerrar y lanzar» cerraba la aplicación
- **Causa:** `InstancePanelService.CloseAndLaunchAsync` esperaba el cierre con `Task.Delay(…, ct)` sin capturar la cancelación. `TaskCanceledException` salía de `InstancesViewModel.LaunchAsync`, que no tiene `catch`. `AsyncRelayCommand` la relanza en el dispatcher y, sin un manejador global (F-02), la app termina.
- **Cuándo pasa:**
  - el usuario pulsa «Cancelar» mientras se espera el cierre (hasta 15 s si el cliente no se cierra);
  - o cambia el modo demostración o los tiempos de lanzamiento en ese intervalo (`UseService` cancela el lanzamiento).
- **Reproducción:** `Cancelling_close_and_launch_while_waiting_returns_cancelled_instead_of_throwing` (SIMULADA). Antes de la corrección falló con `TaskCanceledException`.
- **Corrección:** la cancelación devuelve `LaunchOutcome.Cancelled`, igual que `LaunchAsync`. No se lanza nada nuevo.
- **Verificación:** el test pasa. **No se reprodujo con la app real:** el modo demostración nunca usa «Cerrar y lanzar» (permite 4) y hacerlo con Roblox real exige cerrar el cliente del usuario.

### F-02 · Alta · Sin manejador global de excepciones
- **Causa:** `App` no se suscribía a `DispatcherUnhandledException`, `AppDomain.UnhandledException` ni `TaskScheduler.UnobservedTaskException`. Cualquier excepción imprevista cerraba la app **sin dejar rastro en el log**. Afecta a F-01, O-02 y O-04.
- **Corrección:** `App.WatchUnhandledExceptions` registra `App.UnhandledException` o `App.UnobservedTaskException`.
  - Solo registra el tipo, como exige la lista blanca de `AppLog`, porque los mensajes pueden llevar rutas.
  - Muestra un aviso y deja que la app se cierre. No se marca `Handled`, para no seguir en un estado desconocido.
- **Verificación:** **REAL (app)**, durante la revisión de seguridad (`security-review.md`, S-2). Una DLL plantada provocó `EntryPointNotFoundException`; quedó registrada como `App.UnhandledException exception=EntryPointNotFoundException` y apareció el aviso.

### F-03 · Media · Un `Settings.json` editado con números de enum no definidos impedía arrancar
- **Causa:** el conversor de enums de System.Text.Json acepta números. `Normalize()` no validaba `Theme` ni `Accent`. `ThemeService.Apply` hace `(Color)resources["Accent99Fill"]`, la clave no existe, y desempaquetar `null` provoca `NullReferenceException` en `OnStartup`. Esto último se **dedujo leyendo el código**: no se ejecutó la app anterior a la corrección.
- **Reproducción:** `Undefined_theme_and_accent_numbers_fall_back_to_defaults` (SIMULADA). Antes de la corrección, `Accent` quedaba en 99.
- **Corrección:** `Normalize()` cambia los valores no definidos por los predeterminados.
- **Verificación:**
  - el test pasa;
  - **REAL (app):** arranque con `{ "Accent": 99, "Theme": 7, "DemoMode": true }`: ventana a los 0,98 s, viva 13 s y cierre con código 0.
  - El `Settings.json` temporal se borró: el usuario no tenía ninguno.

### F-04 · Media · Un cliente recién lanzado podía aparecer como «se cerró fuera de LarpStrap»
- **Causa:** el refresco del panel (temporizador) y el lanzamiento corren en hilos distintos. Si `InstanceTracker.Refresh` toma la instantánea de procesos **antes** de `Start` y la aplica **después** de `MarkLaunched`, el PID nuevo no aparece y se marca `ExitedUnexpectedly`.
- **Consecuencias:**
  - una actividad falsa;
  - el PID se olvida y se redescubre como «Externo»;
  - si tarda más de 30 s en mostrar ventana, se clasifica como **proceso de segundo plano**, el botón de lanzar se desactiva y la fila pasa a solo lectura.
- **Reproducción:** `A_refresh_whose_snapshot_predates_a_launch_does_not_report_the_new_client_as_exited` (SIMULADA, con un proveedor que lanza entre la instantánea y su aplicación). Falló antes de la corrección. No se ha observado con un cliente real.
- **Corrección:** contador de lanzamientos. Un `Refresh` no da por terminado un cliente marcado después de tomar su instantánea.
- **Verificación:** el test pasa.

### F-05 · Media · Al cambiar el modo demostración, clientes reales podían salir como SIMULADO (o al revés)
- **Causa:** `UseService` vaciaba la lista mientras seguía en curso un refresco del proveedor anterior. Al terminar, `Apply` pintaba las filas antiguas con la etiqueta del modo nuevo, hasta 2 s. Incumple la regla «lo simulado nunca se presenta como real».
- **Reproducción:** `Switching_to_demo_mode_during_a_refresh_never_shows_real_clients_as_simulated` (SIMULADA, proyecto nuevo `tests/LarpStrap.App.Tests`). Falló antes de la corrección.
- **Corrección:** `RefreshAsync` descarta la instantánea si el servicio cambió mientras se tomaba.
- **Verificación:** el test pasa.

### Tests de caracterización añadidos (pasan; cubren lógica del ViewModel que no tenía tests)
- `One_open_client_turns_the_primary_action_into_close_and_launch`
- `Only_the_background_process_disables_launch`

## 4. Revisión de código previa (mismo diff, ya corregida)

| # | Hallazgo | Verificación |
|---|---|---|
| 1 | Un cliente lanzado y aún sin ventana pasaba a «segundo plano» a los 30 s | Test que **falla con el código anterior** (comprobado en worktree) y pasa ahora |
| 2 | Una salida no pedida se mostraba como error | Revisión (texto y tipo neutros en `Presentation`) |
| 3 | «Cerrar y lanzar» se decidía por el texto del botón | Ahora es un flag. Cubierto por `One_open_client_turns_the_primary_action_into_close_and_launch` |
| 4 | `RefreshAsync` solo capturaba algunas excepciones desde un temporizador | Revisión |
| 5 | `Process.Responding` bloqueaba hasta 5 s por ventana colgada | `SendMessageTimeout` de 500 ms. El descubrimiento REAL (lectura) funciona. **Ventana colgada real no reproducida** |
| 6 | «Actualizado hace…» sin ningún dato todavía | Revisión |
| 7 | Sin log si `%LOCALAPPDATA%` no es escribible | Respaldo en `%TEMP%`. **No ejecutado** |
| 8 | Log sin tope de tamaño | Test que falla con el código anterior y pasa ahora |
| 9 | La exportación fallida dejaba un `.zip.tmp` | Test que falla con el código anterior y pasa ahora |
| 10 | Simplificaciones (`CanLaunch`, `NavItem.SetSelected`) | Build y tests |

## 5. Defectos pendientes

| Id | Sev. | Defecto | Por qué no se corrigió |
|---|---|---|---|
| ~~O-01~~ | Media | **Corregido en 0.2.1** (2026-10-14). La fila de un cliente lanzado sin ventana ofrece «Forzar cierre…» y «Lanzar» se bloquea con una explicación. Tests: `A_launched_client_that_never_shows_a_window_can_be_force_closed_and_blocks_another_launch` y `An_external_window_less_process_is_never_offered_force_close` | — |
| ~~O-02~~ | Baja | **Corregido después** (fase de depuración). `InstallationLocator.Find` lanzaba `UnauthorizedAccessException` con una carpeta `Versions` ilegible, y además ocultaba las demás raíces. Reproducido con `An_unreadable_versions_folder_is_skipped_instead_of_throwing` (ACL de denegación en una carpeta temporal): falla antes y pasa después. Ahora se salta la raíz ilegible, que es el único punto por el que pasan el arranque, el panel y el lanzamiento | — |
| O-03 | Baja | `AppSettings.CloseToTray` se guarda pero no hace nada. La UI lo muestra desactivado como «no disponible todavía», así que no engaña | Es la función pendiente de bandeja (P4-T13) |
| O-04 | Baja | `DialogService.OpenUri` y `OpenFolder` no capturan `Win32Exception` (por ejemplo, sin navegador asociado). Ahora quedaría registrado, pero la app se cerraría | No reproducido |
| O-05 | Baja | `SettingsStore.Save` deja `Settings.json.tmp` si falla el `Move` (el mismo patrón corregido en `ExportZip`) | Sin efecto funcional: el siguiente guardado lo sobrescribe |
| O-06 | Info | El ejecutable se llama `LarpStrap.App.exe`, no tiene `.ico` (P5-T07) y la publicación incluye `.pdb` | Preparación de release |
| O-07 | Info | Las dependencias de test incluyen la telemetría de Microsoft.Testing.Platform (`Microsoft.Testing.Extensions.Telemetry` y `ApplicationInsights`). No se distribuyen. La CI define `DOTNET_CLI_TELEMETRY_OPTOUT=1`; que MTP la respete según su documentación **no se ha comprobado aquí** | Solo afecta a los tests |

## 6. Rendimiento y respuesta (REAL, app)

| Ejecución | Ventana | CPU media | Working set | Privada | Handles |
|---|---|---|---|---|---|
| Modo demostración + enums inválidos, 10 s | 0,98 s | 0,03 % | 116 MB (pico 131) | 73 MB | 622 |
| Modo real (lectura del cliente del usuario), 30 s | 0,87 s | 0,08 % | 112 MB (pico 114) | 75 MB | 625 |

- **Cómo se midió:** la CPU es el porcentaje del total de núcleos; el arranque es el tiempo hasta que hay `MainWindowHandle`.
- **Arranque:** son ejecuciones en caliente. Las mediciones anteriores dieron entre 1,34 y 2,82 s, así que **P5-T06 («< 2 s de forma consistente») sigue abierta**: falta medir un arranque en frío.
- **Memoria:** por debajo del objetivo de < 150 MB en reposo.
- **Bloqueos:** el refresco corre fuera del hilo de UI (`Task.Run`) y la detección de «no responde» tiene un tope de 500 ms por ventana.

## 7. Auditoría por áreas

| # | Área | Estado | Evidencia |
|---|---|---|---|
| 1 | Corrección funcional | Corregidos F-01, F-03, F-04, F-05. Pendiente O-01 | §3, §5 |
| 2 | Arquitectura | Correcta: Core sin UI, todo proceso pasa por `IProcessProvider` y el ViewModel delega en `InstancePanelService`. Nuevo `LarpStrap.App.Tests` | Revisión |
| 3 | Fiabilidad del lanzamiento | Secuencial, con resultado por petición, límite respetado y estados estructurados. 2–4 reales: bloqueado (§8) | Tests `LaunchOrchestratorTests` |
| 4 | Ciclo de vida y cancelación | Cancelar no mata clientes ya iniciados (AT-12). Corregida la cancelación en «Cerrar y lanzar» (F-01) | Tests |
| 5 | Estado de ventanas e instancias | Corregida la carrera (F-04). Sin handles retenidos (`using` en cada `Process`). Sin lectura de líneas de comandos | Revisión y tests |
| 6 | Datos de cuenta y privacidad | Sin credenciales ni cookies: `git grep` sin coincidencias de password, cookie, ROBLOSECURITY, token ni secret fuera del `CancellationToken`. El log usa lista blanca, excepciones solo por tipo, tope de 5 MB. La exportación solo incluye logs propios | Revisión, `AppLogTests` y `LogFilesTests` |
| 7 | Validación de entrada y configuración | Juegos: solo place id o enlace roblox.com/games. Configuración: clamp, favoritos inválidos descartados, copia `.bad` y escritura atómica. Corregida la validación de enums (F-03) | Tests de Settings |
| 8 | CPU, memoria y respuesta | Ver §6 | Medido |
| 9 | Errores y recuperación | Manejador global añadido y verificado (F-02). O-02 corregido. Pendiente O-04 | §3, §5 |
| 10 | Dependencias y licencias | Distribuido: solo `CommunityToolkit.Mvvm` 8.4.2 (MIT) y el runtime .NET (MIT). Tests: xUnit v3 (Apache-2.0) y Microsoft.Testing.Platform (MIT). Sin vulnerabilidades conocidas | `dotnet list package` |
| 11 | Actualizaciones e instalador | **No hay nada que auditar.** No existe autoactualizador (ADR 0006) ni instalador. P4-T11 y P4-T12 están pendientes. Sin firma de código: SmartScreen avisará | Revisión |
| 12 | Accesibilidad y consistencia | Las funciones no implementadas se muestran desactivadas y con «(no disponible)». Nombres accesibles por cliente. **No se repitió la prueba de teclado y lector de pantalla reales (P5-T05)** | `ui-validation-2026-10-13.md` |

## 8. Aceptación del Panel de instancias por número de clientes

| Clientes | Tests automáticos (SIMULADA) | Procesos simulados en la app (modo demostración) | Creación real de procesos | Partida real utilizable | Sesiones de cuenta independientes |
|---|---|---|---|---|---|
| 1 | **PASS** (`LaunchOrchestratorTests`, `InstancePanelServiceTests`, `InstancesViewModelTests`) | **PASS** en la fase 5 (lanzamientos individuales, incluido el fallo simulado; no repetido en esta auditoría) | **PASS**, AT-18a/AT-19 en la fase 3 con el mismo servicio. **Desde la UI nueva: NOT VERIFIED** (P5-T08) | **NOT VERIFIED** (AT-18b: `--app` abre la página de inicio, no una partida) | **N/A**: LarpStrap no gestiona cuentas (ADR 0004) |
| 2 | **PASS** (límite subido a propósito en los tests) | **NOT VERIFIED** por separado (ver nota) | **BLOCKED**: Roblox impone la instancia única y LarpStrap no la elude (ADR 0003). No se intentó | **BLOCKED** | **BLOCKED / no soportado** |
| 3 | **PASS** | **NOT VERIFIED** por separado | **BLOCKED** | **BLOCKED** | **BLOCKED / no soportado** |
| 4 | **PASS** | **Parcial**: en la fase 5 se vio el aviso de límite con 4 activos | **BLOCKED** | **BLOCKED** | **BLOCKED / no soportado** |

Las pruebas de lanzamiento de esta auditoría son *simuladas* salvo donde se indica cliente real.

**Nota sobre el modo demostración:**
- arranca con clientes simulados (hoy, uno con ventana y el proceso en segundo plano);
- por eso, un «Lanzar 2/3/4» desde la app inicia uno y marca el resto como límite alcanzado;
- la fase 5 probó lanzamientos individuales y el aviso de límite, **no cada número de clientes por separado**;
- para verlos haría falta cerrar antes los simulados, algo que la UI permite.

**Pruebas que no se pudieron ejecutar en esta auditoría:**
- lanzar o cerrar un cliente real: hay un cliente del usuario abierto y cerrarlo requiere su permiso;
- partida real;
- teclado y lector de pantalla reales: hace falta LarpStrap en primer plano (regla del incidente de la fase 5);
- arranque en frío;
- ~~forzar F-02 en ejecución~~: verificado después en la revisión de seguridad.

## 9. Riesgos residuales y recomendaciones

1. **Antes de la release (bloqueantes de la v1 especificada):**
   - P4-T01–P4-T03: instancia única de LarpStrap, URI `roblox-player:` y registro del protocolo. Sin ellas, LarpStrap no puede unirse a una partida desde el navegador, que es la función básica de un bootstrapper.
   - P4-T12: distribución, con publish autocontenido, SHA-256 y nombre del exe.
   - P5-T07: icono.
2. **Decidir O-01** (permitir «Forzar cierre» a un cliente lanzado sin ventana) y verificarlo con un cliente real.
3. **Con tu presencia:**
   - P5-T08: lanzar desde la UI nueva sin otros clientes abiertos;
   - P5-T05: teclado y Narrador o NVDA;
   - arranque en frío (P5-T06).
4. **Riesgos externos:**
   - Roblox puede cambiar el arranque, el proceso de bandeja o la política de instancia única;
   - SmartScreen y los antivirus, al no haber firma;
   - el protocolo de Roblox no está documentado.
5. **O-04:** corregirlo cuando se pueda reproducir. El manejador global ya registraría el tipo de excepción.

**Evaluación:** el núcleo (supervisión, lanzamiento de 1 cliente, cierre, diagnóstico y configuración) está probado. Tiene 78 tests automáticos y pruebas reales de lectura y de lanzamiento de la fase 3, sin fallos conocidos de severidad alta abiertos.

La aplicación **todavía no está lista para preparar la release**:
- faltan módulos que el spec exige para la v1 (URI y protocolo, distribución);
- hay un defecto medio abierto (O-01);
- quedan sin verificar el lanzamiento real desde la UI nueva, la accesibilidad con teclado y lector reales, y el arranque en frío.
