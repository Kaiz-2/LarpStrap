# ADR 0007: Paquete dependiente del framework, no autocontenido

## Estado

**Aceptado** (2026-10-14, aprobado por el usuario). Sustituye la parte de P4-T12 que pedía «`dotnet publish` autocontenido».

## Contexto

LarpStrap se distribuye como un zip portable, sin instalador ni firma (ADR 0006). El usuario lo extrae en una carpeta escribible por él, a menudo dentro de `Descargas`. Cualquier contenido que llegue a esa carpeta sin que el usuario lo elija, como una descarga automática de un navegador, puede intentar suplantar una DLL del sistema (DLL planting).

Ensayo `tests/manual/Test-DllPlanting.ps1` del 2026-10-14 (REAL, app): copias de un ensamblado plantadas con el nombre de 7 DLL de Windows junto al exe, con el commit 77f1389.

| DLL plantada | Autocontenido (`--self-contained true`) | Dependiente del framework |
|---|---|---|
| `dwmapi.dll` | No se carga | No se carga |
| `uxtheme.dll` | **Se carga** (P/Invoke de WPF) | No se carga |
| `wtsapi32.dll` | **Se carga** (P/Invoke de WPF) | No se carga |
| `dwrite.dll` | **La app muere antes de arrancar** | No se carga |
| `user32.dll` | **La app muere antes de arrancar** | No se carga |
| `version.dll`, `cryptbase.dll` | No se cargan | No se cargan |

En la build autocontenida, WPF y sus DLL nativas (`wpfgfx_cor3.dll`, `DirectWriteForwarder.dll`…) están en la carpeta del exe. .NET busca primero en esa carpeta los P/Invoke de esos ensamblados, y Windows resuelve allí también las dependencias nativas. La defensa de LarpStrap (`SetDefaultDllDirectories` y un resolver para su propio ensamblado) no puede cubrir el código de WPF.

En la build dependiente del framework, WPF se carga desde `C:\Program Files\dotnet\shared\…`, que solo puede modificar un administrador.

**Control del ensayo:** la build de la fase 5 (sin la defensa S-2) da FAIL con `dwmapi.dll`. El ensayo exige además que la app esté viva y haya escrito `App.Started`; una primera versión del script daba PASS falsos cuando la app ni siquiera arrancaba.

## Decisión

Publicar `LarpStrap-<versión>-win-x64.zip` **dependiente del framework**, con `eng/Publish-Release.ps1`. El usuario instala una vez el **.NET 10 Desktop Runtime (x64)** de Microsoft.

## Consecuencias

- **A favor:**
  - las 7 DLL del ensayo no se cargan;
  - la carpeta publicada pasa de 141 MB a 656 KB (medido, sin comprimir);
  - el runtime recibe parches de seguridad por Windows Update o por el instalador de Microsoft, sin necesidad de publicar una versión nueva de LarpStrap.
- **En contra:**
  - un requisito previo más para el usuario;
  - si falta el runtime, el ejecutable de .NET muestra un aviso con el enlace de descarga. Es el comportamiento estándar de .NET y **no se ha verificado en este equipo**, porque el runtime ya está instalado.
- **Instrucciones:** se recomienda extraer el zip en su propia carpeta (el zip ya contiene la carpeta raíz `LarpStrap-<versión>-win-x64\`) y no ejecutarlo como administrador (`docs/INSTALLATION.md`).
- **Alternativa descartada:** autocontenido con un resolver de DLL para cada ensamblado de WPF. No cubre las dependencias nativas (`dwrite.dll`) y añade código frágil.
