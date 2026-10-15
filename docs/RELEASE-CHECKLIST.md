# Checklist de release

Un procedimiento reutilizable más el estado de la candidata actual. **Preparar una release no autoriza a publicarla:** subir el instalador o el zip a GitHub, o a cualquier otro sitio, requiere la aprobación explícita del propietario.

## Procedimiento

1. **Árbol limpio en una rama de release:** `git status` vacío; `git switch -c release/<versión>`.
2. **Versión:** `<Version>` en `Directory.Build.props` (desde 0.3.0, la misma para la app y el instalador) y una entrada en `CHANGELOG.md`.
3. **Barreras de la CI en local, en el mismo orden que `.github/workflows/ci.yml`:**
   ```bash
   dotnet restore LarpStrap.slnx --locked-mode
   dotnet format LarpStrap.slnx --verify-no-changes --no-restore
   dotnet build LarpStrap.slnx -c Release --no-restore
   dotnet test --solution LarpStrap.slnx -c Release --no-build
   ```
   Después, el paso «Fail on vulnerable packages» de la CI.
4. **Commit** y luego `.\eng\Publish-Release.ps1`.
   - Se niega a empaquetar si el árbol tiene cambios o si cualquier archivo del paquete, incluido el instalador, contiene la ruta de tu perfil o tu correo de git.
   - Genera, cada uno con su `.sha256`:
     - `artifacts/LarpStrap-Setup-<versión>-win-x64.exe`: un solo archivo, con la app embebida;
     - `artifacts/LarpStrap-<versión>-win-x64.zip`: portable, con los mismos archivos.
   - Comprueba que dice `Firma: ninguna` y que `Get-AuthenticodeSignature` da `NotSigned`. **Nunca se anuncia como firmado.**
   - **Que el paquete se genere no prueba nada de la instalación:** eso lo hacen los pasos 5 y 5b.
5. **Verificar el paquete extraído:**
   - SHA-256;
   - instalar y arrancar;
   - actualizar desde la versión anterior;
   - reiniciar;
   - cerrar;
   - un cliente real abierto queda intacto;
   - desinstalar;
   - logs sin datos sensibles;
   - `tests/manual/Test-DllPlanting.ps1 -Dir <carpeta extraída>`.
5b. **Verificar el instalador** (REAL, con el usuario de Windows de la máquina de release; los scripts se niegan a ejecutarse si LarpStrap ya está instalado):
   - Generar una versión anterior para la prueba de actualización. Solo es para la prueba y no se publica:
     ```bash
     .\eng\Publish-Release.ps1 -AllowDirty -Version <versión-anterior> -Output <carpeta-temporal>\old
     ```
   - `tests/manual/Test-Installer.ps1 -Setup <instalador> -OldSetup <instalador anterior> -WorkDir <carpeta vacía>`: 16 comprobaciones, que cubren:
     - instalación limpia en la carpeta por defecto y en una personalizada;
     - accesos directos;
     - abrir tras instalar;
     - actualizar desde la versión anterior, conservando la configuración;
     - rutas no válidas y sin permiso;
     - recuperación de una instalación interrumpida;
     - desinstalación;
     - metadatos.
   - `tests/manual/Test-InstallerWizard.ps1 -Setup <instalador> -WorkDir <carpeta vacía>`: el asistente por UI Automation, con capturas de su propia ventana (`PrintWindow`).
   - `tests/manual/Test-DllPlanting.ps1 -Dir <carpeta con solo el instalador> -Source <una DLL .NET> -StartedEvent Setup.Started`.
6. **Validación con cliente real** (registrar en `docs/testing/acceptance.md`): lanzar, enfocar y cerrar 1 cliente desde la interfaz del paquete.
7. **Decisión GO / CONDITIONAL GO / NO-GO** con la evidencia, y **aprobación del propietario**.
8. **Solo con aprobación:**
   - `git tag v<versión>` y push;
   - release en GitHub con el zip, el `.sha256` y las notas (`CHANGELOG.md`, `docs/KNOWN-LIMITATIONS.md`);
   - descargar lo publicado y comprobar el hash.

**Vuelta atrás:** retirar la release, o marcarla «no usar», y volver a enlazar la anterior con su hash.
- **Zip portable:** no hay que revertir nada en los equipos.
- **Instalador:** el usuario puede ejecutar el instalador de la versión anterior, que avisa de que sustituye una más reciente y conserva la configuración, o desinstalar desde Configuración › Aplicaciones. El instalador solo escribe su clave de `HKCU\…\Uninstall\LarpStrap` y sus accesos directos. Nunca cambia el gestor de `roblox-player:`.

## Estado de 0.3.0 (2026-10-14)

Primera versión con instalador (ADR 0008, aceptada), más el rediseño de la interfaz (iteraciones 1 y 2), las funciones de la fase 4 y S-5. Rama `release/0.3.0`; paquete construido con `eng/Publish-Release.ps1` desde el commit limpio `d1fa06c`.

| Artefacto | Tamaño | SHA-256 |
|---|---|---|
| `LarpStrap-Setup-0.3.0-win-x64.exe` | 799 361 bytes | `33daf91e2d6ec7168a48beba5454fdeda30e10f93de06322d7e476cf223417e7` |
| `LarpStrap-0.3.0-win-x64.zip` | 339 534 bytes, 12 archivos | `6c003813b027430bdc6d922d65e4d5980a1bd4c626402f8870c8acfcb1d682c7` |

Versión de los exe: FileVersion `0.3.0.0`; ProductVersion `0.3.0+d1fa06c7f9681ea282346af64fee58766532ae99`; producto LarpStrap, empresa Leir.

| Comprobación | Resultado | Evidencia |
|---|---|---|
| Restore bloqueado, formato, build, vulnerabilidades (local) | **PASS** | 0 warnings; ningún paquete vulnerable en los 5 proyectos |
| Tests automáticos | **PASS** | 192: 190 correctos, 2 omitidos (cliente real, opt-in) |
| CI en GitHub sobre `d1fa06c` | **PASS** | Ejecución 38637568179 (con el SDK fijado a 10.0.401) |
| Integridad y privacidad | **PASS** | `sha256sum -c` correcto en los dos `.sha256` (LF); barrera de privacidad del script en la app y en el instalador; zip con LICENSE y avisos de terceros |
| Firma | **NotSigned** | Sin firma de código (ADR 0006). No se anuncia como firmado |
| Instalador sobre **este** build: `Test-Installer.ps1`, `Test-InstallerWizard.ps1`, DLL planting | **NO VERIFICADO** | El propietario decidió publicar sin ellas: había un cliente de Roblox suyo abierto y estas pruebas abren ventanas. Pasaron (16/16, 8/8, PASS) sobre el build de `4b995a3`; desde entonces cambiaron la interfaz de la app (iteraciones 1 y 2), `global.json` y un test, no el motor ni el asistente del instalador |
| Cliente real: lanzar, enlace del navegador, FastFlags aplicados, crash real, Discord | **NO VERIFICADO** | Solo tests *simulados* |
| Equipo limpio, sin .NET | **NO VERIFICADO** | Sin Windows Sandbox en este equipo |

**Decisión: CONDITIONAL GO** como versión preliminar, con las pruebas anteriores registradas como no verificadas. El propietario pidió publicarla el 2026-10-14.

**Publicada el 2026-10-14:** *pre-release* [v0.3.0](https://github.com/Kaiz-2/LarpStrap/releases/tag/v0.3.0); la etiqueta anotada apunta a `d1fa06c`, el commit del que salieron los artefactos. Los cuatro archivos descargados de GitHub pasan `sha256sum -c` con los `.sha256` publicados.

## Candidata previa de 0.3.0 (4b995a3): verificación del instalador

Construida con `eng/Publish-Release.ps1` desde el commit limpio `4b995a3` (rama `feature/installer`, sin fusionar ni subir).

| Artefacto | Tamaño | SHA-256 |
|---|---|---|
| `artifacts/LarpStrap-Setup-0.3.0-win-x64.exe` | 789 121 bytes | `e5c9c7fb794d1a8e12598058a910379826aa7ae4cf72121511bf98911940475a` |
| `artifacts/LarpStrap-0.3.0-win-x64.zip` | 334 979 bytes, 12 archivos | `050236026674a1d560996f020c50b14ff637df82d1a0da58686ed02d504e7d9c` |

| Comprobación | Resultado | Evidencia |
|---|---|---|
| Build del paquete | **PASS** | Desde cero, restore bloqueado, barrera de privacidad en la app y en el instalador. Esto solo prueba que se genera |
| Tests automáticos | **PASS** | 187: 185 correctos, 2 omitidos (cliente real). Incluye 18 del motor de instalación (en carpetas temporales y una clave `HKCU\Software\LarpStrap.Tests`) y 6 de argumentos |
| `Test-Installer.ps1` sobre el artefacto | **PASS, 16/16** (REAL, este usuario de Windows) | Instalación limpia (por defecto y personalizada, con espacios), accesos directos, abrir tras instalar, actualización desde 0.2.99 de prueba, configuración conservada, app abierta que bloquea sin cambiar nada, rutas no válidas y sin permiso, recuperación de una instalación interrumpida, desinstalación por el comando registrado, metadatos e icono |
| `Test-InstallerWizard.ps1` sobre el artefacto | **PASS, 8/8** | Asistente por UI Automation: error de carpeta del sistema, instalación, resultado comprobado, Finalizar abre LarpStrap, desinstalación con confirmación y sin restos |
| DLL planting (instalador en carpeta tipo Descargas y app) | **PASS** | 9 canarias, incluidas `hostfxr.dll` y `hostpolicy.dll` (S-5) |
| Firma | **NotSigned** | Sin firma de código (ADR 0006). No se anuncia como firmado |
| Equipo limpio, otra cuenta, escritorio en OneDrive, antivirus de terceros, sin .NET Runtime | **NO VERIFICADO** | Este equipo no tiene Windows Sandbox. Prueba manual: en una máquina virtual con Windows 11 limpio, sin .NET, abrir el instalador, comprobar el aviso de .NET, instalar el runtime y repetir `Test-Installer.ps1` |
| Cliente real de Roblox con la 0.3.0 instalada | **NO VERIFICADO** | Pendiente de que el propietario no esté jugando |

Sustituida por el build de `d1fa06c` (sección anterior).

## Estado de 0.2.1 (2026-10-14)

Versión de corrección (O-01, A, B y C; commit `f0144ac`).

**Candidata:**
- Rama `release/0.2.1`; paquete construido desde `cd4821f`.
- **Artefacto:** `LarpStrap-0.2.1-win-x64.zip`, 251 324 bytes, 12 archivos, dependiente del framework.
- **SHA-256:** `ad5288beec4f63e953ce2b49c2628155b28deaf49cc67abdda990c9712aab868`. El `.sha256` ya usa LF.
- **Versión del exe:** FileVersion `0.2.1.0`; ProductVersion `0.2.1+cd4821f549278c7bae72d8dbe0e6f674ed3ff81c`.

| Comprobación | Resultado | Evidencia |
|---|---|---|
| Build, formato, restore bloqueado y vulnerabilidades (local) | **PASS** | 0 warnings |
| Tests automáticos | **PASS** | 93 tests: 91 correctos, 2 omitidos (cliente real, opt-in) |
| CI en GitHub | **PASS** | Ejecución 38591033514 sobre `cd4821f` |
| Integridad del zip y barrera de privacidad | **PASS** | SHA-256 recalculado igual; sin ruta del perfil ni correo |
| Actualización desde la **0.2.0 publicada** | **PASS** | Tema Claro, acento Turquesa y un acceso directo hechos con el zip de 0.2.0 se conservan en 0.2.1 |
| Persistencia tras reinicio | **PASS** | El tema Oscuro elegido en 0.2.1 persiste |
| Arranque y cierre | **PASS** | Ventana a 1,42 s y 1,25 s; cierre con código 0; sin procesos residuales |
| Cliente real ya abierto | **PASS (REAL, solo lectura)** | PID 9112 (iniciado a las 15:00:20) sin cambios tras 3 arranques de 0.2.1 |
| Desinstalación y logs | **PASS** | Sin claves de registro ni accesos; datos conservados; 10 logs sin datos sensibles |
| DLL planting | **PASS** | 7 canario, ninguna cargada |
| Lanzar, enfocar y cerrar un cliente real desde el paquete 0.2.1 | **NO VERIFICADO** | El propietario decidió publicar sin esta prueba: había un cliente suyo abierto. El código de lanzamiento no cambió desde 0.2.0, donde la prueba dio PASS; sí cambiaron el seguimiento y el registro del panel (cubiertos por tests con procesos simulados) |
| Equipo limpio | **NO VERIFICADO** | Igual que en 0.2.0 |

**Decisión: CONDITIONAL GO** como versión preliminar, con la prueba de cliente real pendiente y registrada como no verificada. El propietario aprobó publicarla el 2026-10-14.

**Publicada el 2026-10-14:** *pre-release* [v0.2.1](https://github.com/Kaiz-2/LarpStrap/releases/tag/v0.2.1); la etiqueta anotada apunta a `cd4821f`. El zip descargado de GitHub pasa `sha256sum -c` con el `.sha256` publicado.

## Estado de 0.2.0 (2026-10-14)

**Candidata:**
- Rama `release/0.2.0`; commits `77f1389` (fase 6) y `7f6e10d` (empaquetado). Sin etiqueta y sin push. El repositorio no tiene remoto.
- **Artefacto:** `artifacts/LarpStrap-0.2.0-win-x64.zip`, 250 437 bytes, 12 archivos, dependiente del framework (ADR 0007, aceptado).
- **SHA-256:** `445b3df6fe77a7481f514a5c17262bdd12b77df754f201a4eec541ec129b02ce`.
- **Versión del exe:** FileVersion `0.2.0.0`; ProductVersion `0.2.0+7f6e10dbeee91fe53d3a23ece62e2cc7f12648d2`.

| Comprobación | Resultado | Evidencia |
|---|---|---|
| Build Release | **PASS** | 0 warnings y 0 errores. El paquete se compiló desde cero (`bin` y `obj` borrados) desde el commit limpio `7f6e10d` |
| Tests automáticos | **PASS** | 88 tests: 86 correctos, 2 omitidos (cliente real, opt-in) |
| Restore bloqueado, formato, vulnerabilidades | **PASS** | Barrera de vulnerabilidades comprobada: falla (exit 1) con un paquete vulnerable y pasa con LarpStrap |
| CI en GitHub | **PASS** | Ejecución 38567853342 sobre `b2e906c` (rama `release/0.2.0`, repositorio privado `Kaiz-2/LarpStrap`), runner `windows-latest`, 2 min 14 s. Pasos en verde: restore bloqueado, formato, build, test (88 tests: 0 fallos, 2 omitidos de cliente real) y barrera de vulnerabilidades |
| Integridad del zip | **PASS** | El SHA-256 recalculado coincide con el `.sha256` |
| Datos sensibles en el paquete | **PASS** | La barrera de privacidad no encontró la ruta del perfil ni el correo. En su primera ejecución **detectó y bloqueó** una ruta de PDB que había dejado una compilación incremental |
| Instalación desde el zip | **PASS (mismo equipo)** | Extraído en una carpeta nueva: carpeta raíz correcta, arranca y se cierra con código 0 |
| Instalación en entorno limpio | **NO VERIFICADA** | Windows 11 Home no tiene Windows Sandbox y no se creó otra cuenta. Tampoco se probó el aviso de runtime ausente |
| Actualización desde la versión anterior | **PASS** | Con la build de la fase 5 (04fdb0f) se pusieron tema Claro, acento Turquesa y un acceso directo; el paquete nuevo los conservó. Lectura de esquema v1: test `Version_1_file_loads_with_defaults…` |
| Persistencia tras reinicio | **PASS** | El tema Oscuro elegido en el paquete persistió en el siguiente arranque |
| Arranque y cierre | **PASS** | Ventana a 1,67 s (primera ejecución del paquete) y 1,35 s (segunda); cierre con código 0; ningún proceso residual. El arranque en frío de una build recién compilada llegó a 4,41 s (`/run`), así que **P5-T06 sigue abierto** |
| Clientes ya abiertos | **PASS (REAL, solo lectura)** | El cliente del usuario (PID 18804, iniciado a las 12:23:11) se vio como «Externo» y siguió con el mismo PID y la misma hora de inicio tras 3 arranques de LarpStrap |
| Lanzar, enfocar y cerrar un cliente real desde el paquete | **PASS (REAL)** | 12:45, con la autorización del usuario y Roblox cerrado antes: `Started`, PID 29212, ventana a los 5,4 s; estable 30 s; Enfocar puso en primer plano la ventana del PID 29212; Cerrar terminó el cliente y su CrashHandler en 0,8 s. Registrado en `docs/testing/acceptance.md` |
| Actualizador (origen e integridad) | **NO APLICA** | No existe actualizador (ADR 0006). La integridad la comprueba el usuario con el SHA-256 (`INSTALLATION.md`). No se valida el origen: no hay firma |
| DLL planting | **PASS** | 7 DLL plantadas junto al exe del paquete extraído: ninguna se carga; la app arranca y escribe `App.Started` |
| Desinstalación y retención | **PASS** | Tras borrar la carpeta: ninguna clave en `HKCU\Software` con «LarpStrap», nada en `%APPDATA%` ni en el menú Inicio. `%LOCALAPPDATA%\LarpStrap` (Logs y Settings) se conserva, como documenta `INSTALLATION.md` |
| Logs sin datos sensibles | **PASS** | 10 logs revisados: sin ruta del perfil, correo, `roblox-player:`, `gameinfo`, `ROBLOSECURITY` ni URLs |
| Dependencias y licencias | **PASS** | Solo se distribuye `CommunityToolkit.Mvvm` 8.4.2 (MIT), con su `License.md` y `ThirdPartyNotices.txt` en `licenses/`. LarpStrap es MIT (`LICENSE`). El runtime de .NET lo instala el usuario desde Microsoft |
| Documentación | **PASS** | `README.md`, `CHANGELOG.md`, `docs/INSTALLATION.md` (incluye fallos y recuperación), `docs/KNOWN-LIMITATIONS.md` y este documento |

**Riesgos que quedan:** ver `docs/KNOWN-LIMITATIONS.md`. Los principales son:
- O-01 y los defectos A–C;
- H-2 a H-5;
- sin firma de código;
- entorno limpio sin verificar;
- (ADR 0007 ya aceptado.)

**Publicada el 2026-10-14** con la aprobación del propietario:
- *Pre-release* [v0.2.0](https://github.com/Kaiz-2/LarpStrap/releases/tag/v0.2.0) en el repositorio privado `Kaiz-2/LarpStrap`.
- La etiqueta anotada `v0.2.0` apunta a `7f6e10d`, el commit del que salen los binarios; los commits posteriores solo cambian documentación.
- Activos: el zip (250 437 bytes) y el `.sha256`.
- **Comprobado tras publicar:** el zip descargado de GitHub tiene el SHA-256 `445b3df6…02ce`, idéntico al verificado.
- Nota: el `.sha256` de 0.2.0 usa CRLF, así que `sha256sum -c` lo rechaza; `Get-FileHash` (el método documentado) funciona. El script ya lo escribe con LF para las próximas versiones.

**Decisión (actualizada el 2026-10-14): CONDITIONAL GO** como **versión preliminar** 0.2.0, con las limitaciones de `docs/KNOWN-LIMITATIONS.md`. Se cumplen las tres condiciones que se fijaron:
- lanzamiento real desde el paquete: PASS;
- ADR 0007: aceptado;
- CI en GitHub: PASS.

No es la v1 del spec, porque falta unirse a partidas desde el navegador (P4-T01–T03). Sigue sin verificar la instalación en un equipo limpio. **Publicar** (etiqueta `v0.2.0` y release en GitHub con el zip y el `.sha256`) sigue requiriendo la aprobación expresa del propietario.

Historial: la decisión anterior era NO-GO, con estas condiciones:
1. ~~Lanzar, enfocar y cerrar 1 cliente real desde la interfaz del paquete~~: **hecho** (PASS, 12:45).
2. ~~Aprobar el ADR 0007~~: **aceptado** por el usuario (2026-10-14).
3. ~~Que la CI pase una vez en GitHub~~: **PASS** (ejecución 38567853342). Correo de los commits (H-6) decidido: desde 2026-10-14 se usa la dirección *noreply* de GitHub; los commits anteriores conservan el correo personal; hoy solo los ve quien tenga acceso al repositorio, que es privado, y serían públicos si se hiciera público.

No sería la v1 del spec: falta unirse a partidas desde el navegador (P4-T01–T03).
