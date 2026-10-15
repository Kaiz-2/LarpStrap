# Comparativa de proyectos de referencia

**Fase 1, investigación de fuentes.** Fecha: 2026-10-13. Sustituye a `01-comparativa-bootstrappers.md`, que se conserva como histórico.

**Método:**
- Repositorio oficial verificado desde la web oficial de cada proyecto (navegador o WebFetch, 2026-10-13).
- Metadatos con la API de GitHub.
- Código leído en clones superficiales en estos commits: Bloxstrap `02db760` (2026-08-05), Fishstrap `988db40` (2026-09-27), Froststrap `e8e165d` (2026-10-13), ExploitStrap `22aaed2` (2026-10-02).
- Notas sobre el protocolo de despliegue y lanzamiento en `research_notes/protocolo-despliegue-lanzamiento.md`.

**No se ha compilado ni ejecutado ningún proyecto, y no se ha hecho ninguna prueba, ni simulada ni con cliente real.**

Etiquetas: **[Web oficial]** web del proyecto · **[API]** API de GitHub · **[Código]** leído en el código fuente · **[Oficial-Roblox]** · **[Terceros]** · **[No verificado]**.

## 1. Repositorios verificados

| Proyecto | Web oficial | Repositorio enlazado desde la web | Verificación |
|---|---|---|---|
| Bloxstrap | https://bloxstraplabs.com/ | https://github.com/bloxstraplabs/bloxstrap | Enlace «GitHub» de la web [Web oficial]. `pizzaboxer/bloxstrap` redirige al mismo repositorio [API] |
| Fishstrap | https://www.fishstrap.app/ | https://github.com/fishstrap/fishstrap y la réplica Forgejo https://git.fishstrap.app/fishstrap/fishstrap | Enlaces de la web; la web avisa de que fishstrap.com **no** es oficial [Web oficial] |
| Froststrap | https://froststrap.xyz/ | https://github.com/Froststrap/Froststrap | Enlaces de la web renderizada en navegador; la descarga apunta a las releases de ese repositorio [Web oficial] |
| ExploitStrap | https://exploitstrap.com/ | https://sirmemegithub.com/RealSlimShady2000/ExploitStrap (Forgejo propio, **no GitHub**) | La web enlaza ahí el código, la licencia y las descargas. La cuenta no existe en GitHub (404) [Web oficial, API] |

## 2. Tabla comparativa

| | Bloxstrap | Fishstrap | Froststrap | ExploitStrap |
|---|---|---|---|---|
| Origen | Original (2022) | Fork de Bloxstrap (2024) | «Fishstrap fork» según su web (2026) | Fork de Bloxstrap |
| Licencia | MIT | MIT | MPL-2.0 (REUSE; incluye textos MIT, AGPL-3.0 y Unlicense) | MIT (según su web) |
| Última release | v2.11.4 (2026-08-05) | v3.1.2 (2026-08-21) | v2.0.5 estable en la web; v2.0.6-beta.4 (2026-10-13) | v420.53.2 (2026-10-02) |
| Actividad | Baja; 1420 issues abiertas | Activa | Muy activa | Activa |
| Lenguaje / UI | C# · .NET 6 · WPF + WPF-UI | C# · .NET 6 · WPF + WPF-UI | C# · .NET 10 · **Avalonia 12** + backend en Rust | C# · .NET 6 · WPF + WebView2 |
| Plataformas | Windows | Windows | Windows, macOS, Linux (vía Sober) | Windows |
| Cuentas | No | No | **Account Manager**: guarda `.ROBLOSECURITY` y lo escribe en el almacén de cookies de Roblox | Gestor multicuenta con cookies (DPAPI) y generador de cuentas alternativas |
| Firma de código | Sí (SignPath Foundation) | No | No verificado | No (lo dice su web) |
| winget | `pizzaboxer.Bloxstrap` | `Fishstrap.Fishstrap` | No | No |
| Tests automatizados | No encontrados | No encontrados | No encontrados | No encontrados |

Fuentes: [API] para licencias, fechas y releases; [Web oficial] para plataformas, funciones anunciadas y firma; [Código] para las cuentas; la investigación a fondo para winget y la firma.

## 3. Análisis por proyecto

### 3.1 Bloxstrap
- **Mantenimiento:** v2.11.4 (2026-08-05). El último push fue el 2026-08-17 [API]. La v2.10.0 (2025-10-01) eliminó opciones que Roblox había roto [API, notas de la versión].
- **Arquitectura:** un único proyecto WPF. `Bootstrapper.cs` es una clase dios de más de 1500 líneas. Los watchers son procesos aparte (`Watcher.cs`, `Integrations/ActivityWatcher.cs`) [Código].
- **Detección e instalación:**
  - No detecta la instalación oficial de Roblox. Gestiona su propia carpeta `Versions\` y su estado en `State.json` [Código].
  - Comprueba el runtime de WebView2 (GUID de EdgeUpdate en el registro) [Código].
  - Registra `roblox-player:` y `roblox:` en HKCU (`Utility/WindowsRegistry.cs`) [Código].
- **Lanzamiento:** pasa la URI al cliente. Hay un comentario en el código que dice que retener el handle del proceso más de un minuto dispara Byfron, así que libera el handle nada más obtener el PID [Código; afirmación de terceros sobre Hyperion, **no verificada**].
- **Procesos y ventanas:** `CloseMainWindow`, `ShowWindow`, `SetForegroundWindow`. Sigue el estado de la partida leyendo los logs de FLog [Código].
- **Personalización:** mods de assets locales, Discord RPC, BloxstrapRPC. Los FastFlags quedaron limitados por la allowlist de Roblox del 2025-09-29 [Web oficial, Oficial-Roblox].
- **Configuración y actualizaciones:**
  - Configuración en `Settings.json` y `State.json` [Código].
  - Se autoactualiza con `api.github.com/.../releases/latest` y ejecuta el binario descargado **sin verificar hash ni firma** en el código revisado [Código; también lo señala la investigación a fondo].
- **Ideas útiles:**
  - Verificación MD5 de los paquetes de Roblox.
  - Carrera entre espejos del CDN.
  - Separar los watchers en otro proceso.
  - Firma gratuita con SignPath.

### 3.2 Fishstrap
- **Mantenimiento:** v3.1.2 (2026-08-21). El último commit en `main` es del 2026-09-27 (PR #1499, distribución VNG para Vietnam) [API].
- **Arquitectura:** misma base que Bloxstrap, con perfiles (`Profiles.json`) y `appStorage.json` [Código].
- **Lanzamiento:** canales no productivos con token por usuario (`Roblox-Channel-Token`) [investigación a fondo, Código].
- **Ventanas:** `Integrations/WindowManipulation.cs` redimensiona la ventana del cliente con `SetWindowPos` [Código].
- **Actualizaciones:** mismo patrón que Bloxstrap, sin firma [Código, investigación a fondo].
- **Ideas útiles:** servidor y región de la partida (API de RoValra), perfiles de configuración.

### 3.3 Froststrap
- **Mantenimiento:** varias betas por semana. Se creó en 2026-01 [API].
- **Arquitectura:**
  - Multiplataforma con Avalonia 12 y .NET 10.
  - Backend en Rust compilado por plataforma.
  - Nix flake y licencias REUSE.
  - Unas 58.000 líneas de C# [Código].
- **Cuentas:**
  - El Account Manager guarda la cookie `.ROBLOSECURITY` (`ktsu.CredentialCache`, con DPAPI como método antiguo) [Código].
  - **La escribe en el almacén de cookies del propio cliente de Roblox** (`AccountCookieWriter.cs`) [Código].
  - `CookiesManager.cs` **lee y descifra** la cookie que guarda Roblox [Código].
  - Es el patrón que ADR-003 excluye en LarpStrap.
- **Procesos:** usa `Process.Kill` en varios watchers (`Watcher.cs`, `ActivityWatcher.cs`) [Código].
- **Actualizaciones:** se autoactualiza vía GitHub Releases (`App.axaml.cs`) y también descarga herramientas auxiliares (mod-generator, FroststrapStudioRPC, vinegarhq/kombucha) [Código].
- **Ideas útiles:**
  - Selector de región.
  - Accesos directos a juegos.
  - Arquitectura multiplataforma.
  - Notificación cuando un flag no está en la allowlist.
- **Licencia:** MPL-2.0 con copyleft por fichero. Copiar un fichero obliga a mantenerlo MPL.

### 3.4 ExploitStrap
- **Distribución:** web exploitstrap.com, código y binarios en un Forgejo propio. Releases sin firmar de 166 MB, con SHA-256 publicado [Web oficial].
- **Funciones anunciadas** [Web oficial, Código, solo nombres]:
  - Bloqueo del canal LIVE y gestión de versiones por *executor*.
  - **Generador de cuentas alternativas** (BloxGen).
  - **Herramientas de evasión de baneos** (BanAsync: suplantación de MAC, aleatorización de MachineGuid, borrado de cookies).
  - Navegador de scripts, ofuscador y desofuscador.
- **Valoración:** está orientado a exploits y a evadir baneos. Solo sirve como evidencia de qué técnicas circulan; no es una referencia de ingeniería para LarpStrap (ADR-004).

## 4. Licencias

| Si se reutiliza código de… | Obligación |
|---|---|
| Bloxstrap / Fishstrap (MIT) | Conservar el copyright y la licencia en `THIRD_PARTY_NOTICES.md` |
| Froststrap (MPL-2.0) | El fichero sigue siendo MPL y su fuente debe estar disponible; no se reutiliza (ADR-004) |
| ExploitStrap (MIT) | No se reutiliza (ADR-004) |
| LarpStrap | MIT (`LICENSE`, ADR-005) |

## 5. Ideas útiles y lecciones

| Para copiar la idea (no el código) | Para evitar |
|---|---|
| Verificación MD5 de paquetes, espejos del CDN, activación de versión | La clase dios de `Bootstrapper.cs` y la ausencia total de tests |
| Liberar el handle de proceso al momento | Autoactualizar sin verificar hash ni firma |
| Firma gratuita vía SignPath (si se decide firmar) | Guardar cookies `.ROBLOSECURITY` o escribirlas en el almacén de Roblox |
| Avisar de flags fuera de la allowlist | Funciones que dependen de comportamiento no documentado sin plan de retirada |

## 6. Lo no verificado

- Que el comentario sobre «handle > 1 min dispara Byfron» siga siendo cierto: no hay fuente de Roblox.
- La firma de los binarios de Froststrap: no se ha inspeccionado el MSI.
- Que las funciones de ExploitStrap hagan lo que anuncian: no se ha ejecutado nada.
- Que las releases publicadas se correspondan con el código leído: no hay builds reproducibles verificados.
