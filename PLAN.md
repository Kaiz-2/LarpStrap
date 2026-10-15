# PLAN

Estado de las fases de LarpStrap. Las decisiones están en `docs/adr/`. Lo que queda abierto está en `docs/testing/` y `docs/KNOWN-LIMITATIONS.md`.

| Fase | Estado |
|---|---|
| 1. Investigación de fuentes | Hecha (`docs/research/reference-comparison.md`, `research_notes/`) |
| 2. Especificación y arquitectura | Parcial: `SPEC-app-shell.md` en borrador; ADR 0002–0006 aceptadas |
| 3. MVP técnico del Panel de instancias | **Hecha, con límites** |
| 4. Producto principal | **En curso** |
| 5. Interfaz de escritorio de producción | **Hecha, con pendientes** (ver abajo) |

## Fase 3: MVP técnico

| Id | Tarea | Estado |
|---|---|---|
| P3-T01 | `IProcessProvider` + `WindowsProcessProvider` (sin retener handles; no lee líneas de comandos) | Hecha |
| P3-T02 | `InstallationLocator` (carpetas de versiones de Roblox, Bloxstrap y Fishstrap; solo lectura) | Hecha |
| P3-T03 | `InstanceTracker`: estados independientes, clientes externos, cierre y cierre forzado individuales | Hecha |
| P3-T04 | `LaunchOrchestrator`: secuencial, tiempo máximo y retardo configurables, cancelación, fallos parciales, diagnóstico y límite de concurrencia | Hecha |
| P3-T05 | Interfaz mínima (WPF) | Hecha (provisional; la sustituye P4-T03) |
| P3-T06 | Tests: 22 simulados + 2 reales de solo lectura (opcionales) | Hecha |
| P3-T07 | Lanzamiento real de 1 cliente (AT-18a) y controles reales (AT-19) | Hecha (PASS, 2026-10-13) |
| P3-T08 | Arranque < 2 s (AT-23) | Se traslada a P4-T03 |
| P3-T09 | Lanzar el cliente sin que herede los handles de LarpStrap (`UseShellExecute`) | **Hecha** (2026-10-13): 2 tests; verificación real: el comando con salida capturada termina a los 8,4 s con el cliente abierto |

## Fase 4: producto principal

En orden de prioridad del usuario. Complejidad: S (1–2 archivos), M (3–5), L (dividir antes de empezar).
**Límite transversal:** LarpStrap no fuerza varias copias del mismo cliente (ADR 0003). La página «Dos cuentas» abre Roblox - Windows y Roblox web con sesiones separadas, sin garantizar que Roblox permita mantener ambas abiertas.

| Id | Prioridad | Tarea | Depende de | Compl. | Estado / notas |
|---|---|---|---|---|---|
| P4-T01 | 1 | Instancia única de LarpStrap + named pipe (`PipeOptions.CurrentUserOnly`) para entregar la URI al proceso abierto | — | M | **Hecha** (sin publicar): `SingleInstance` (mutex `Local\` + pipe `CurrentUserOnly`, mensaje ≤ 16 KB). Un segundo arranque trae la ventana al frente |
| P4-T02 | 1 | Lectura de la URI `roblox-player:` (lista blanca de campos, sin registrar el ticket) y lanzamiento de una partida con ella (AT-18b) | P4-T01 | M | **Hecha** (sin publicar, *simulada*): `RobloxUri` valida el enlace (esquema, longitud, ASCII imprimible sin espacios ni comillas) y lo pasa entre comillas como único argumento; solo se registran `placeId` y `launchMode`. **Falta AT-18b con cliente real** y, para que el navegador use LarpStrap, P4-T03 |
| P4-T03 | 1 | Registro del protocolo con copia del gestor anterior, restauración y confirmación del usuario | P4-T02 | M | Pendiente. **Requiere confirmación antes de tocar el registro real** (ahora el gestor es Bloxstrap) |
| P4-T04 | 2 | Ciclo de vida: crash frente a cierre (log del CrashHandler), «no responde» con periodo de gracia, minimizar y restaurar | — | M | **Hecha** (sin publicar, *simulada*): crash = salida no pedida con un `*.dmp` nuevo en `Roblox\logs\crashes\reports` (observado en este PC; falta confirmarlo con un crash real); «No responde» tras 5 s colgado. Minimizar y restaurar: no se hace (no se pidió) |
| P4-T05 | 5 | Configuración versionada (`SchemaVersion` + migración) sobre `SettingsStore` | — | S | Pendiente |
| P4-T06 | 3 | Perfiles de cuenta **sin credenciales** (nombre visible y notas; el cambio de cuenta se hace en el navegador) | P4-T05 | M | Pendiente. Limitado: no habrá sesiones independientes por perfil (ADR 0004) |
| P4-T07 | 4 | Accesos directos y favoritos de juegos (placeId) | P4-T05, P4-T02 | M | Pendiente. Falta verificar el mecanismo oficial para abrir un juego sin ticket (enlace profundo o web) |
| P4-T08 | 6 | FastFlags limitados a la allowlist de Roblox | P4-T05 | M | **Hecha** (sin publicar): lista oficial de 18 flags embebida (anuncio del 29-09-2025), tipo por prefijo, validación de rango; se escriben en `ClientSettings\ClientAppSettings.json` de la versión que se lanza conservando las claves ajenas. **Decisión tomada: la fuente es el anuncio oficial, actualizado con cada versión de LarpStrap.** Falta comprobar con cliente real que Roblox los aplica |
| P4-T09 | 7 | Diagnóstico: visor de logs de LarpStrap y exportación segura (zip solo de logs propios, que ya van con lista blanca) | — | S | **Hecha**: Diagnóstico ya tenía visor y exportación zip de los logs propios (fase 5) |
| P4-T10 | 8 | Discord Rich Presence (opcional) | P4-T04 | M | **Hecha** (sin publicar): cliente propio del pipe local de Discord, sin dependencias (evaluación resuelta: ninguna). Solo «En Roblox» y la hora de inicio; desactivado por defecto; necesita el ID de una aplicación de Discord del usuario. **Sin probar con Discord real** |
| P4-T11 | 9 | Actualizaciones: **solo comprobar** si hay versión nueva en GitHub Releases y enlazar la descarga | — | S | Pendiente. Sin autoactualizador (ADR 0006) |
| P4-T12 | 10 | Distribución: zip **dependiente del framework** (ADR 0007, aceptado: el autocontenido cargaba DLL plantadas), SHA-256, README e INSTALLATION | P4-T03 | S | **0.2.0 publicada** como *pre-release* el 2026-10-14 (`v0.2.0`, repositorio privado). Proceso en `docs/RELEASE-CHECKLIST.md`. **Instalador (fase 6, 2026-10-14):** `LarpStrap-Setup-<versión>-win-x64.exe` por usuario y sin elevación, junto al zip (ADR 0008, aceptada el 2026-10-14). Verificado en este equipo con `Test-Installer.ps1` y `Test-InstallerWizard.ps1`; equipo limpio sin verificar |
| P4-T13 | — | Shell definitivo de app-shell (MVVM, bandeja, tema, identidad visual aprobada, arranque < 2 s) | P4-T01 | L | Pendiente. Dividir; necesita antes la dirección de marca aprobada por el usuario |
| P4-T14 | 2 | Flujo «Dos cuentas»: abrir Roblox - Windows y Roblox web con sesiones independientes, sin guardar credenciales | — | S | **Hecha**: página dedicada, detección de la app Store y acceso a roblox.com; no fuerza la instancia única del mismo cliente |

## Fase 5: interfaz de escritorio

Detalle en `docs/design/design-system.md` y `docs/testing/ui-validation-2026-10-13.md`.

| Id | Tarea | Estado |
|---|---|---|
| P5-T01 | Core: configuración v2, `GameLinks`, recursos y estados de instancia, `InstancePanelService`, proveedor simulado y `LogFiles` | Hecha (commit b097f2d) |
| P5-T02 | Sistema de diseño (tokens oscuro y claro, acentos, componentes, foco) | Hecha |
| P5-T03 | Shell MVVM y 8 pantallas conectadas a servicios reales; lo no implementado, desactivado y explicado | Hecha |
| P5-T04 | Validación ejecutando la app, crítica y accesibilidad; 8 fallos corregidos | Hecha |
| P5-T05 | Prueba de tabulación con teclado real y lector de pantalla (Narrador o NVDA) | **Pendiente**: requiere que el usuario ponga LarpStrap en primer plano |
| P5-T06 | Arranque < 2 s de forma consistente (medido entre 1,60 y 2,82 s) | **Pendiente** |
| P5-T07 | Icono `.ico` propio para la ventana y el exe | **Hecha** (2026-10-14): logotipo «Nebulosa», `eng/Build-Icon.ps1` |
| P5-T08 | Lanzar un cliente real desde la nueva interfaz | **Hecha** (2026-10-14): Lanzar, Enfocar y Cerrar desde el paquete 0.2.0, PASS (cliente real) |

## Fase 6: auditoría final

Informe en `docs/quality/final-audit.md`. 5 hallazgos corregidos: F-01, F-03, F-04 y F-05 con test de regresión; F-02 (manejador global de excepciones) verificado en ejecución. Revisión de seguridad en `docs/quality/security-review.md`: S-1–S-4 corregidos (exportación que borraba un archivo del usuario, DLL planting, `explorer.exe` por ruta completa, locks de NuGet y permisos de la CI). O-02 corregido después (carpeta `Versions` ilegible). Pendientes: O-01 (cliente lanzado sin ventana sin acciones), O-03–O-07 y los hallazgos de `/ship` del 2026-10-14 (defectos A, B y C del panel, I-2 y H-1–H-6). **No está lista para preparar la release.**

## Correcciones posteriores a 0.2.0

Rama `fix/panel-defects` (2026-10-14): O-01/I-2, A, B y C corregidos, cada uno con un test que falla sin la corrección. Fusionado en `main` (CI en verde) y publicado en 0.2.1.
