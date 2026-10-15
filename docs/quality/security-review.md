# Revisión de seguridad (fase 6)

- **Fecha:** 2026-10-13
- **Alcance:** todo `src/`, la CI y la cadena de dependencias, sobre los cambios de la auditoría (`final-audit.md`).
- **Método:** `/security-review` no pudo ejecutarse porque el repositorio no tiene remoto `origin`. La revisión se hizo a mano con la skill *security-and-hardening*: modelo de amenazas, STRIDE por frontera y casos de abuso. Cada corrección se verificó con un test o un ensayo real.

## 1. Modelo de amenazas

**Activos:**
- la sesión de Roblox del usuario: el ticket de `roblox-player:` y `-t`, que todavía no se manejan (llegarán en P4-T02);
- las cookies de cuenta, que LarpStrap nunca toca (ADR 0004);
- los archivos del usuario: destino de la exportación y carpeta de logs;
- el control de procesos: cerrar o forzar cierre;
- la integridad del ejecutable que se lanza.

**Atacante considerado:** contenido que llega a carpetas escribibles por el usuario sin que él lo elija, por ejemplo una descarga automática en `Descargas`, y archivos de configuración editados. Un proceso malicioso que ya se ejecuta como el mismo usuario **queda fuera**: puede hacer todo lo que hace LarpStrap.

| Frontera (quién escribe el dato) | STRIDE relevante | Control | Estado |
|---|---|---|---|
| `Settings.json` (usuario o edición a mano) | Manipulación, DoS | `Normalize()`: clamp, favoritos válidos y enums definidos (F-03), copia `.bad` y escritura atómica | OK (tests) |
| Texto escrito en Accesos directos | Manipulación | Solo place id o `roblox.com/games/<id>`; la URL se construye con un `long` | OK (tests) |
| Lista de procesos `RobloxPlayerBeta` (cualquier proceso del mismo usuario con ese nombre) | Suplantación | Se muestra y se controla. Matar requiere confirmación y se comprueba el nombre antes de actuar | Aceptado (mismo usuario) |
| `…\Versions\*\RobloxPlayerBeta.exe` (escribible por el usuario, Bloxstrap o Fishstrap) | Suplantación, elevación | Se lanza el más reciente, **sin comprobar la firma Authenticode** | Riesgo residual R-2 |
| Carpeta del exe (búsqueda de DLL y EXE) | Elevación (planting) | **S-2 y S-3 corregidos** | Verificado **solo con `dwmapi.dll` y los P/Invoke de App**; Core y otras DLL sin verificar (H-1 de `/ship`) |
| Ruta de exportación elegida en el diálogo | Manipulación, pérdida de datos | **S-1 corregido** | Verificado |
| Contenido de los logs | Divulgación | Lista blanca de claves, excepciones solo por tipo, tope de 5 MB, exportación solo de logs propios | OK (tests) |
| Shell (`OpenUri` y `OpenFolder`) | Manipulación | URIs construidas por la app; `explorer.exe` con ruta completa (S-3) | OK |
| Cadena de suministro: NuGet y GitHub Actions | Manipulación | **S-4:** archivos de bloqueo, `--locked-mode` y `permissions: contents: read` | Verificado en local |
| Futuro: URI `roblox-player:` y named pipe (P4-T01/02) | Suplantación, divulgación | Diseño ya fijado: pipe `CurrentUserOnly`, campos en lista blanca y nunca registrar el ticket | Pendiente de implementar |

**Secretos:**
- `git log -p --all` no contiene credenciales, cookies, claves ni tickets reales. `.ROBLOSECURITY` solo aparece en la documentación de investigación.
- `.gitignore` excluye `.env`, `*.pfx` y los logs.

## 2. Hallazgos y correcciones

### S-1 · Media · La exportación de logs podía borrar un archivo del usuario
- **Causa:** la corrección #9 de la revisión anterior añadió `File.Delete(tmp)` en el `catch` de `LogFiles.ExportZip`. Si ya existía `<destino>.zip.tmp`, `ZipFile.Open(Create)` fallaba y el `catch` **borraba ese archivo ajeno**. Es una operación destructiva sobre una ruta derivada sin prueba de propiedad.
- **Reproducción:** `Failed_export_never_deletes_a_file_the_user_already_had`. Antes de la corrección dio `FileNotFoundException`: el archivo del usuario había desaparecido.
- **Corrección:** el archivo se abre antes del `try`. El modo Create falla si ya existe, así que solo se borra un temporal que haya creado esta llamada.
- **Verificación:** los 5 tests de `LogFilesTests` pasan.

### S-2 · Media · DLL planting: una `dwmapi.dll` junto al exe se cargaba en lugar de la del sistema
- **Causa:** `dwmapi.dll` no es una KnownDLL. Windows y .NET buscan primero en la carpeta de la aplicación.
- **Reproducción (REAL, app):** script `tests/manual/Test-DllPlanting.ps1`, que copia un ensamblado .NET como `dwmapi.dll` junto al exe.
  - Antes de la corrección, el proceso cargó `…\plant\DWMAPI.dll` (módulo comprobado) y falló con `EntryPointNotFoundException`. Con una DLL maliciosa, su código se habría ejecutado.
- **Intentos que no bastaron:**
  1. `[DefaultDllImportSearchPaths(System32)]`. El componente nativo de WPF ya había cargado la copia plantada por su nombre y Windows reutiliza el módulo ya cargado.
  2. Añadir `SetDefaultDllDirectories`. Corrigió la build dependiente del framework, pero **no la autocontenida**: .NET sigue buscando primero en la carpeta de la app los P/Invoke del propio ensamblado.
- **Corrección final, en el constructor estático de `App`, antes de que WPF cargue nada nativo:**
  - `SetDefaultDllDirectories(SYSTEM32 | USER_DIRS)`;
  - `NativeLibrary.SetDllImportResolver`, que resuelve los P/Invoke de la app desde `Environment.SystemDirectory`.
- **Verificación (REAL, app, DLL plantada):**
  - publicación dependiente del framework: solo `C:\WINDOWS\SYSTEM32\DWMAPI.dll`, sin errores en el log;
  - publicación autocontenida (`win-x64`): igual.
- **Alcance de la verificación (añadido el 2026-10-14 tras `/ship`):** solo se plantó `dwmapi.dll`. El resolver solo cubre el ensamblado App. Los `LibraryImport("user32.dll")` de Core y los P/Invoke del propio WPF hacia DLL que no son KnownDLL **no están verificados**. Pendiente H-1: registrar el resolver también en Core y ampliar el script a varias DLL.
- **Sin regresión:** el diálogo «Guardar como», que carga extensiones del shell, se abre en las dos builds igual que en la build de control. Se comprobó con UI Automation, sin teclas.
- **Efecto colateral útil:** este ensayo verificó en ejecución el manejador global de excepciones (F-02 de la auditoría). Registró `App.UnhandledException exception=EntryPointNotFoundException` y mostró el aviso.

### S-5 · Alta · El *apphost* de .NET cargaba un `hostfxr.dll` plantado junto al exe (2026-10-14, fase 6)
- **Causa:** por defecto, el ejecutable nativo que genera .NET (*apphost*) busca primero `hostfxr.dll` en su propia carpeta (búsqueda `AppLocal`), **antes de ejecutar ningún código de LarpStrap**. `NativeLibraryHardening` (S-2) no puede protegerlo.
- **Reproducción (REAL, app e instalador):** `tests/manual/Test-DllPlanting.ps1` con `hostfxr.dll` y `hostpolicy.dll` añadidas a las canarias:
  - la app del paquete murió con `0x80008084` (el host cargó el `hostfxr.dll` plantado y no encontró su punto de entrada);
  - el instalador, copiado a una carpeta como Descargas, murió con `0xC0000409`.
  - Con una DLL nativa maliciosa, su código se habría ejecutado.
- **Afecta a 0.2.0 y 0.2.1 publicadas:** quien extraiga el zip en una carpeta donde ya haya un `hostfxr.dll` ajeno.
- **Corrección:** `AppHostDotNetSearch=Global` en `Directory.Build.props` (.NET 9 o posterior; solo afecta a lo publicado). El *apphost* solo usa la instalación de .NET registrada en el sistema, que solo puede modificar un administrador. También ignora `DOTNET_ROOT`.
- **Verificación (REAL):** con las 9 canarias plantadas, la app del paquete y el instalador en una carpeta tipo Descargas arrancan, escriben `App.Started` y `Setup.Started` respectivamente, y no cargan ninguna DLL de su carpeta: **PASS**.

### S-3 · Baja · `OpenFolder` lanzaba `explorer.exe` por nombre
- **Causa:** con `UseShellExecute`, un nombre relativo puede resolverse primero en la carpeta actual. Al abrir la app desde el Explorador, esa es la carpeta del exe.
- **Corrección:** ruta completa `%WINDIR%\explorer.exe`.
- **Verificación:**
  - «Abrir carpeta de logs» lanza Explorer igual con la build corregida que con la de control: cada ejecución creó un proceso de Explorer.
  - **Sin verificar:** que la ventana muestre la carpeta. En este entorno de pruebas esas ventanas aparecieron sin ruta ni título, y también con la build de control; las cerré al terminar.
  - **Tampoco se demostró** un ataque real de `explorer.exe` plantado.

### S-4 · Baja · Cadena de suministro y CI
- **Antes:**
  - sin archivos de bloqueo de NuGet (versiones transitivas sin hash fijado);
  - el token de la CI tenía los permisos por defecto;
  - las acciones se fijaban por etiqueta.
- **Corrección:**
  - `RestorePackagesWithLockFile` y `RuntimeIdentifiers=win-x64` en `Directory.Build.props`, con 4 `packages.lock.json` nuevos;
  - CI con `dotnet restore --locked-mode` y `permissions: contents: read`.
- **Verificación:**
  - `dotnet restore --locked-mode` pasa;
  - publicar en modo autocontenido y dependiente del framework no modifica ningún lock (comprobado con md5);
  - `--vulnerable --include-transitive`: sin vulnerabilidades.
  - **La CI no se ha ejecutado**: el repositorio no tiene remoto.
- **Barrera de vulnerabilidades (añadido el 2026-10-14):**
  - El paso `dotnet list package --vulnerable` de la CI **no fallaba**: con Newtonsoft.Json 12.0.1, que tiene un aviso alto, devuelve exit 0 tanto en texto como en JSON (comprobado en un proyecto del scratchpad).
  - La barrera real era el restore: `TreatWarningsAsErrors` convierte NU1903 en error, también con `--locked-mode`.
  - Ahora `NuGetAuditMode=all` es explícito, y el paso de la CI analiza el JSON y falla si hay algo. Comprobado en local: exit 1 con el paquete vulnerable y exit 0 en LarpStrap.
  - Añadidos también `persist-credentials: false`, `concurrency`, `timeout-minutes`, caché de NuGet y `dependabot.yml`; nada de esto se ha ejecutado en GitHub.

## 3. Riesgos residuales

| Id | Riesgo | Recomendación |
|---|---|---|
| R-1 | Las acciones de GitHub se fijan por etiqueta (`@v7`, `@v6`), no por SHA | Fijarlas por SHA cuando haya remoto. Hace falta consultar GitHub: pedirte permiso antes |
| R-2 | Se lanza el `RobloxPlayerBeta.exe` más reciente de carpetas escribibles por el usuario sin verificar la firma Authenticode (Roblox Corporation) | Añadir `WinVerifyTrust` antes de `Start` en P4 (módulo deployment). El atacante tendría que escribir ya como el usuario |
| R-3 | Un proceso del mismo usuario llamado `RobloxPlayerBeta` aparece en el panel y se puede cerrar | Aceptado: no hay frontera de privilegios. Opcional: comprobar la ruta del ejecutable con un handle de consulta limitada y corta |
| R-4 | `docs/research/01-comparativa-bootstrappers.md` contiene la ruta con tu usuario de Windows | Revisarlo antes de publicar el repositorio |
| R-5 | P4-T01/02 manejará el ticket de autenticación: ese será el mayor riesgo de divulgación del proyecto | Hará falta otra revisión de seguridad específica antes de integrarlo |
| R-6 | Sin firma de código: SmartScreen y los antivirus avisarán, y el usuario no puede verificar el origen salvo con el SHA-256 publicado | Publicar el SHA-256 (P4-T12). Evaluar SignPath si cambia la decisión |

## 4. Comprobaciones ejecutadas

| Comando o ensayo | Resultado |
|---|---|
| `git log -p --all` filtrado por patrones de secretos | Solo menciones en la documentación |
| `Failed_export_never_deletes_a_file_the_user_already_had` (antes y después) | Falla y luego pasa |
| `tests/manual/Test-DllPlanting.ps1` en 4 builds (antes, solo atributo, solo `SetDefaultDllDirectories` en autocontenida, final ×2) | Cargó la DLL plantada; la cargó; la cargó (el P/Invoke); **System32 en las dos finales** |
| Diálogo «Guardar como» por UI Automation (control y 2 builds finales) | Se abre en las tres |
| `dotnet restore --locked-mode` y 2 publicaciones con md5 de los locks | Sin cambios |
| `dotnet format --verify-no-changes`, `dotnet build -c Release`, `dotnet test --solution` | OK; 0 warnings; **81: 79 correctos, 2 omitidos (cliente real)** en el momento de esta revisión. El recuento actual está en la cabecera de `final-audit.md` |
