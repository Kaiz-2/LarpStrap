# ADR 0008: Instalador propio en WPF, por usuario y sin elevación

## Estado

**Aceptado** (2026-10-14, aprobado por el usuario). A partir de 0.3.0 se distribuyen el instalador y el zip portable; desde 0.4.0, solo el instalador (decisión del propietario, 2026-10-16: el usuario no debe tener que descomprimir nada); publicar una versión sigue requiriendo aprobación explícita.

## Contexto

Hasta 0.2.1, LarpStrap se distribuía como un zip portable (ADR 0007): el usuario lo extraía a mano, sin accesos directos ni entrada en «Configuración › Aplicaciones», y desinstalar era borrar la carpeta. La fase 6 pide un asistente de instalación con la identidad de LarpStrap, que de verdad instale, actualice, repare y desinstale.

Condiciones que ya estaban decididas:
- **Sin firma de código** (ADR 0006).
- **Dependiente del framework** (ADR 0007): el .NET 10 Desktop Runtime se instala aparte, porque la build autocontenida cargaba DLL plantadas.
- **Sin dependencias nuevas sin preguntar,** y **sin ejecutar CLIs externas** sin confirmación (CLAUDE.md).
- **No ejecutar LarpStrap elevado** (H-2 de la revisión de seguridad).

## Opciones evaluadas

| Opción | A favor | En contra | Veredicto |
|---|---|---|---|
| **MSIX** (empaquetado de Windows) | Instalación y desinstalación limpias que gestiona Windows; actualizaciones nativas | **Exige un certificado de confianza para instalarse.** Sin firma (ADR 0006), el usuario tendría que importar un certificado o activar el modo desarrollador | Descartada |
| **ClickOnce** (nativa de .NET) | Viene con el SDK; por usuario; sin elevación | Carpeta fija en la caché de ClickOnce, sin elegir destino; el asistente es el de Windows, sin marca; está pensado para su propio actualizador automático, que ADR 0006 descarta; sin firma muestra «Editor desconocido» | Descartada |
| **WiX (MSI) + Burn** | Estándar empresarial; reparación con `msiexec`; despliegue por directivas de grupo | Otra cadena de herramientas (CLI externa y paquetes nuevos). Un asistente con la marca exige un *bootstrapper application* propio en WPF: tanto trabajo como un instalador propio, más el de MSI. MSI por usuario tiene casos raros (ICE, carpetas por usuario) | Descartada por coste |
| **Inno Setup** | Maduro, pequeño, muy usado | Herramienta externa fuera del repositorio y de la CI; scripts en Pascal; la marca se limita a mapas de bits, sin reutilizar el sistema de diseño WPF | Descartada |
| **Velopack / Squirrel** | Instalación por usuario y actualizaciones | Dependencia nueva, orientada a la actualización automática (ADR 0006) | Descartada |
| **Instalador propio en WPF** | Reutiliza Core, los temas y los controles de la app (un solo sistema de diseño); por usuario sin elevación; sin dependencias ni herramientas nuevas; todo el código es nuestro y tiene tests | Somos responsables de que las operaciones de archivos sean correctas; sin las funciones de MSI (directivas de grupo, reparación con `msiexec`) | **Elegida** |

## Decisión

**`LarpStrap-Setup-<versión>-win-x64.exe`**, un proyecto WPF propio (`src/LarpStrap.Setup`):

- **Un solo archivo** dependiente del framework (`PublishSingleFile`, sin autocontenido), con la app publicada **embebida como zip** (`eng/Publish-Release.ps1`).
- **Por usuario y sin elevación:**
  - el manifiesto pide `asInvoker`;
  - la carpeta por defecto es `%LOCALAPPDATA%\Programs\LarpStrap`;
  - el registro va en `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\LarpStrap`;
  - los accesos directos van en el menú Inicio y el escritorio del usuario.
  - Si se abre como administrador, **se niega a instalar** (código 4), porque instalaría en el perfil equivocado y abriría LarpStrap elevado (H-2).
- **El motor está en Core** (`LarpStrap.Core.Setup.Installer`) y es el mismo para el asistente y para el modo silencioso (`--silent`, `--uninstall`). Funciona así:
  1. extrae a una carpeta de preparación, calculando el SHA-256 de cada archivo y rechazando rutas que se salgan de la carpeta (*zip slip*);
  2. escribe un diario;
  3. sustituye cada archivo dejando el anterior como `*.larpstrap-old`;
  4. si algo falla, restaura la versión anterior; si el proceso muere a mitad, la siguiente ejecución la restaura a partir del diario;
  5. crea los accesos directos, registra la desinstalación y escribe un manifiesto con lo instalado;
  6. **comprueba** hashes, versión del exe, accesos directos y registro antes de declarar el éxito.
- **El desinstalador borra solo lo que instaló:**
  - los archivos del manifiesto (solo los que quedan dentro de la carpeta), los accesos directos que apuntan a esa instalación y su clave de registro;
  - nunca `%LOCALAPPDATA%\LarpStrap` (configuración y logs), Roblox ni los archivos ajenos.
- **Nunca cierra procesos:** si LarpStrap está abierto, pide cerrarlo. Los clientes de Roblox no se tocan.
- **El asistente reutiliza el sistema de diseño de la app enlazando sus archivos**, sin copiarlos: `Themes/*.xaml`, `Glyph`, `SettingCard`, los conversores y `ThemeService`.
- ~~**El zip portable se sigue generando**~~ *Retirado en 0.4.0:* solo se publica el instalador.

## Consecuencias

- **Seguridad:**
  - El instalador se ejecuta desde Descargas, así que lleva el mismo endurecimiento contra DLL plantadas que la app (`NativeLibraryHardening`).
  - El ensayo del 2026-10-14 descubrió que el *apphost* de .NET cargaba un `hostfxr.dll` plantado junto al exe, antes de ejecutar nuestro código, en la app y en el instalador. Se corrige con `AppHostDotNetSearch=Global` (S-5 en `docs/quality/security-review.md`).
- **Sin firma:**
  - SmartScreen avisará igual que con el zip.
  - El origen se comprueba con el SHA-256 publicado, que el asistente recuerda en su panel lateral.
  - El paquete **no es reproducible bit a bit**: dos builds de la misma versión dan hashes distintos, por las fechas del zip. Se publica el hash del artefacto concreto.
- **Desinstalar deja una copia del desinstalador en `%TEMP%\LarpStrap-Uninstall-<id>\`**, porque un exe no puede borrarse a sí mismo. Pesa lo mismo que el instalador (< 1 MB). Borrarla con un `cmd /c` diferido añadiría ejecutar un intérprete de comandos; se acepta el resto en `%TEMP%`, que Windows limpia.
- **`--uninstall --silent` vuelve antes de terminar:** la copia temporal hace el trabajo. Los scripts que lo usen deben esperar a que desaparezca la clave de registro (`tests/manual/Test-Installer.ps1` lo hace).
- **Sin actualización automática** (ADR 0006): para actualizar, se descarga el instalador nuevo y se ejecuta. Detecta la versión instalada y actualiza en su carpeta.
- **Hay más código propio que mantener:** el motor tiene 18 tests y las pruebas manuales de extremo a extremo (`Test-Installer.ps1`, `Test-InstallerWizard.ps1`).
