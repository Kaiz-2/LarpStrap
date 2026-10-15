# Instalar, actualizar y desinstalar LarpStrap

Desde 0.4.0 se distribuye **un solo archivo**, `LarpStrap-Setup-<versión>-win-x64.exe`. Lleva LarpStrap dentro: al abrirlo, el asistente lo extrae e instala, sin zip que descomprimir a mano.

| | |
|---|---|
| Dónde | `%LOCALAPPDATA%\Programs\LarpStrap` o la carpeta que elijas |
| Accesos directos | Menú Inicio y escritorio, opcionales |
| Actualizaciones | Automáticas al iniciar (ADR 0009) |
| Desinstalar | Configuración › Aplicaciones |
| Permisos de administrador | No los pide, y se niega a ejecutarse elevado |

No está firmado (ADR 0006). La decisión sobre el instalador está en [ADR 0008](adr/0008-instalador-propio.md).

## Requisitos

- Windows 10 u 11 de **64 bits**.
- **.NET 10 Desktop Runtime (x64)**, de Microsoft: <https://dotnet.microsoft.com/download/dotnet/10.0>. Busca la fila «.NET Desktop Runtime» para Windows x64.
  - Ni el instalador ni la app lo incluyen, para que WPF no pueda cargar DLL ajenas desde su carpeta (ADR 0007).
  - Si falta, Windows lo dirá al abrir el instalador y ofrecerá un enlace para descargarlo. Ese aviso es el estándar de .NET y **no se ha comprobado** en un equipo sin el runtime.
- **Roblox Player** instalado. LarpStrap usa la versión más reciente que encuentre en:
  - `%LOCALAPPDATA%\Roblox\Versions`
  - `%LOCALAPPDATA%\Bloxstrap\Versions`
  - `%LOCALAPPDATA%\Fishstrap\Versions`

## Instalar con el instalador

1. **Descarga** `LarpStrap-Setup-<versión>-win-x64.exe` y su `.sha256` desde la página de la versión.
2. **Comprueba la descarga** antes de abrirla. En PowerShell:
   ```bash
   Get-FileHash .\LarpStrap-Setup-0.4.0-win-x64.exe -Algorithm SHA256
   ```
   El resultado debe coincidir con el `.sha256`. Si no coincide, bórralo y no lo abras.
3. **Ábrelo normalmente,** con doble clic y **sin** «Ejecutar como administrador». SmartScreen avisará porque no está firmado: si el SHA-256 coincide, pulsa «Más información» y «Ejecutar de todas formas». Si lo abres como administrador, el instalador se niega a seguir y explica por qué.
4. **Bienvenida:** muestra la versión, si es una instalación nueva, una actualización o una reparación, y los requisitos. «Ver licencia» muestra la licencia MIT.
5. **Opciones:**
   - **Carpeta:** la propuesta es `%LOCALAPPDATA%\Programs\LarpStrap`. Puedes elegir otra con «Examinar…». Se rechazan con una explicación, antes de tocar nada:
     - rutas relativas, de red o con caracteres no válidos;
     - la raíz de una unidad;
     - carpetas del sistema (`Windows`, `Archivos de programa`, `ProgramData`);
     - carpetas sin permiso de escritura o sin espacio;
     - carpetas que no están vacías y no son una instalación de LarpStrap.
   - **Accesos directos:** menú Inicio y escritorio, los dos marcados por defecto.
6. **Instalación:** muestra cada etapa y avanza según el trabajo real (bytes copiados, archivos sustituidos).
   - Solo se puede cancelar mientras copia, antes de tocar la instalación.
   - Si algo falla al sustituir los archivos, se restaura la versión anterior y se ofrece «Reintentar».
7. **Listo:** solo aparece después de comprobar:
   - el SHA-256 de cada archivo;
   - la versión de `LarpStrap.exe`;
   - los accesos directos;
   - la entrada de desinstalación.

   «Abrir LarpStrap ahora» lo abre con tu usuario, sin elevar.

### Sin asistente (scripts)

```bash
.\LarpStrap-Setup-0.4.0-win-x64.exe --silent --dir "D:\Juegos\LarpStrap" --no-desktop-shortcut --launch
```

| Opción | Efecto |
|---|---|
| `--silent` | Instala sin ventanas |
| `--dir <carpeta>` | Carpeta de destino. Si ya está instalado, solo se acepta la carpeta actual |
| `--no-start-menu-shortcut`, `--no-desktop-shortcut` | Sin ese acceso directo |
| `--launch` | Abre LarpStrap al terminar |
| `--uninstall [--silent]` | Desinstala |

**Códigos de salida:** 0 correcto, 1 error (el motivo queda en el log como `Setup.Failed`), 2 argumentos no válidos, 4 ejecutado como administrador, 5 instalador sin LarpStrap dentro (build de desarrollo).

## Dónde guarda cada cosa

| Qué | Dónde | Lo toca el instalador |
|---|---|---|
| Programa | La carpeta de instalación | Sí: instala, sustituye y desinstala solo los archivos que puso él (`LarpStrap.install.json`) |
| Copia del instalador para desinstalar | `<carpeta>\LarpStrap-Setup.exe` | Sí |
| Accesos directos | Menú Inicio y escritorio del usuario | Sí, solo los suyos (comprueba a qué apuntan) |
| Entrada de desinstalación | `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\LarpStrap` | Sí |
| Configuración | `%LOCALAPPDATA%\LarpStrap\Settings.json` | **No, nunca** |
| Logs de LarpStrap y del instalador | `%LOCALAPPDATA%\LarpStrap\Logs` (los del instalador: eventos `Setup.*`) | Escribe los suyos; nunca borra |

LarpStrap no guarda cookies, contraseñas ni enlaces de juego, y no envía telemetría. Solo se conecta a las API públicas de Roblox (`apis`, `games` y `thumbnails.roblox.com`) para el nombre y el icono de los juegos que guardas, enviando el place ID y nada de tu cuenta. Para actualizarse, consulta las releases de `Kaiz-2/LarpStrap` en GitHub (`api.github.com` y las descargas de GitHub) al iniciarse; no envía datos tuyos. Ni el instalador ni la app cambian qué programa abre los enlaces de Roblox.

## Actualizar

- **Automática (versión instalada, ADR 0009):** al iniciarse, LarpStrap busca una versión nueva en GitHub.
  - Solo la acepta si su manifiesto está firmado con la clave de LarpStrap y el instalador coincide en tamaño y SHA-256; si no, la descarta y no instala nada.
  - La descarga va a `%LOCALAPPDATA%\LarpStrap\Updates`. Después LarpStrap avisa, se cierra, el instalador actualiza en silencio conservando tus accesos directos y lo vuelve a abrir. Sin permisos de administrador.
  - **Nunca con Roblox abierto** ni durante un lanzamiento: entonces espera al siguiente inicio. Nunca cierra Roblox.
  - Se desactiva en **Acerca de › Actualizar automáticamente**; «Buscar ahora» comprueba en cualquier momento.
  - Quien tenga 0.3.0 o anterior debe instalar a mano, una vez, la primera versión con actualizador.
- **Con el instalador, a mano:** cierra LarpStrap y abre el instalador de la versión nueva.
  - Detecta la instalación y la actualiza **en su carpeta**; para cambiar de carpeta, desinstala antes.
  - Mantiene los accesos directos que tenías.
  - Si LarpStrap está abierto, se detiene sin cambiar nada y te pide cerrarlo. **Nunca cierra LarpStrap ni Roblox por su cuenta.**
  - Los archivos que ya no existen en la versión nueva se quitan. Los que añadiste tú se quedan.
  - Con la misma versión, «Reparar» vuelve a copiar y comprobar los archivos.
  - Con una versión anterior, explica que sustituirá una más reciente antes de hacerlo.
- **Se conserva tu configuración,** porque vive fuera de la carpeta del programa.
- **Si usabas el zip portable de 0.3.0 o anterior:** instala con el instalador y borra la carpeta del zip. La configuración se mantiene.

## Desinstalar

- **Instalador:** Configuración › Aplicaciones › LarpStrap › Desinstalar. Se abre el asistente y te dice qué quita y qué conserva.
  - **Se quita:** los archivos que instaló, sus accesos directos y su entrada en Aplicaciones. La carpeta se borra si queda vacía.
  - **Se conserva:** tu configuración y tus logs (`%LOCALAPPDATA%\LarpStrap`), los archivos que no instaló, y Roblox con sus clientes.
  - Como un programa no puede borrarse a sí mismo, el desinstalador se copia a `%TEMP%\LarpStrap-Uninstall-<id>\` y trabaja desde ahí. Esa copia (< 1 MB) se queda en `%TEMP%`.
- **Copia portable de 0.3.0 o anterior:** cierra LarpStrap y borra su carpeta.
- **Opcional:** para borrar también tu configuración y los logs, borra `%LOCALAPPDATA%\LarpStrap`, y `%TEMP%\LarpStrap` si existe.

## Si algo falla

| Síntoma | Qué pasa | Qué hacer |
|---|---|---|
| Aviso de SmartScreen o del antivirus | El programa no está firmado | Comprueba el SHA-256. Si coincide, permítelo; si no, bórralo |
| No abre y pide instalar .NET | Falta el .NET 10 Desktop Runtime x64 | Instálalo (ver Requisitos) |
| «Has abierto el instalador como administrador» | Instalaría en otro perfil y abriría LarpStrap elevado | Ciérralo y ábrelo con doble clic normal |
| «LarpStrap está abierto. Ciérralo y pulsa Reintentar» | No se puede sustituir un programa en uso | Cierra LarpStrap (los clientes de Roblox pueden seguir abiertos) y pulsa Reintentar |
| «No se pudieron copiar los archivos… Se ha dejado la instalación como estaba» | Un archivo bloqueado (antivirus, Explorador) | Cierra lo que use la carpeta y pulsa Reintentar |
| «Una instalación anterior se interrumpió» | Se cortó la luz o se cerró el proceso a mitad | Sigue: el instalador restaura la versión anterior y vuelve a instalar |
| «La comprobación final falló» | Algún archivo no coincide después de copiar | Ejecuta el instalador otra vez (Reparar). Si se repite, descárgalo de nuevo y comprueba su SHA-256 |
| «LarpStrap encontró un error inesperado y se cerrará» | Excepción no prevista; queda `App.UnhandledException` en el log | Vuelve a abrirlo. Si se repite, exporta los logs desde Diagnóstico |
| Aviso «No se pudo leer la configuración guardada» | `Settings.json` dañado | Se usan los valores por defecto y la copia dañada se guarda como `Settings.json.bad` |
| «Roblox no encontrado» | No hay `RobloxPlayerBeta.exe` en las carpetas de Requisitos | Instala Roblox, o ábrelo una vez desde Bloxstrap o Fishstrap |
| «Hay un proceso de Roblox en segundo plano» | El proceso de bandeja de Roblox ocupa su única plaza | Ciérralo desde el icono de Roblox en la bandeja. LarpStrap no lo cierra por diseño |
| «Ya hay un cliente abierto» | Roblox solo permite uno | Usa «Cerrar y lanzar…» o cierra el cliente |

Los logs del instalador están en `%LOCALAPPDATA%\LarpStrap\Logs`; la pantalla de error tiene un botón para abrir esa carpeta. Las limitaciones conocidas están en [KNOWN-LIMITATIONS.md](KNOWN-LIMITATIONS.md).
