# Informe 1 — Comparativa de bootstrappers de referencia

Fecha de la investigación: 2026-10-13 · Proyecto: LarpStrap (workspace vacío, sin Git)

## Cómo leer este informe

Cada afirmación lleva una etiqueta de evidencia:

| Etiqueta | Significado |
|---|---|
| **[API]** | Dato obtenido de la API de GitHub (`gh api`) el 2026-10-13 |
| **[Código]** | Comprobado leyendo el código fuente clonado (`git clone --depth 1`, commit indicado) |
| **[Local]** | Observado en este PC (lectura pasiva: registro, ficheros, procesos) |
| **[Doc oficial]** | Documentación primaria del fabricante (Microsoft Learn, soporte de Microsoft), citada con URL |
| **[Web]** | Otra fuente web citada; fiabilidad indicada |
| **[No verificado]** | Afirmación de terceros que no he podido comprobar |

**Lo que NO se ha hecho:** no se ha compilado ni ejecutado ninguno de los cuatro proyectos. No se ha ejecutado ninguna prueba, ni simulada ni con clientes reales de Roblox. El análisis es estático (código y metadatos) más observación pasiva del sistema local.

## 1. Estado del workspace y del entorno

| Elemento | Estado | Evidencia |
|---|---|---|
| `%USERPROFILE%\LarpStrap` | Vacío antes de este informe; sin `CLAUDE.md`, sin README | [Local] |
| Git | No es un repositorio (git 2.55 instalado) | [Local] |
| .NET SDK | **No instalado** (`dotnet --list-sdks` vacío). Bloqueante para la fase 0 | [Local] |
| GitHub CLI | 2.102.0, sesión iniciada (cuenta Kaiz-2) | [Local] |
| Node 24 / Python 3.12 | Instalados (no son necesarios para el stack propuesto) | [Local] |
| SO | Windows 11 **Home** 10.0.26200 | [Local] |
| Bloxstrap | 2.11.4 instalado; es el gestor actual del protocolo `roblox-player` | [Local] |
| Fishstrap | 3.1.2 instalado | [Local] |
| Cliente Roblox | 0.742.0.7421053 (en `Bloxstrap\Versions\version-cec3ad5889b447cf`) | [Local] |
| Autoarranque de Roblox | Clave `HKCU\...\Run\RobloxPlayerBeta` con `--launch-to-tray`; apunta a una carpeta de Fishstrap que ya no existe | [Local] |

Skills usadas: `context-engineering` (inventario de contexto), `documentation-and-adrs` (formato de las ADR en `docs/decisions/`). Las skills de UI/diseño no aplican todavía, porque esta fase no tiene interfaz.

## 2. Estado de mantenimiento

| | Bloxstrap | Fishstrap | Froststrap | ExploitStrap |
|---|---|---|---|---|
| Repositorio | github.com/bloxstraplabs/bloxstrap | github.com/fishstrap/fishstrap | github.com/Froststrap/Froststrap | sirmemegithub.com/RealSlimShady2000/ExploitStrap (**Forgejo propio, no GitHub**) |
| Origen | Original (2022-08-02) | Fork de Bloxstrap (2024-10-17) | «Derivado de Fishstrap» según su README; no es fork en GitHub (2026-01-18) | Fork de Bloxstrap según su README |
| Licencia | MIT | MIT (+ `LICENSE.Bloxstrap`) | **MPL-2.0** (REUSE; `LICENSES/` contiene también MIT, AGPL-3.0-only y Unlicense) | MIT (heredada) |
| Estrellas / forks | 3174 / 1094 | 620 / 147 | 292 / 46 | 26 / 0 (según la página) |
| Issues abiertas | 1420 | 56 | 3 | — |
| Última release | v2.11.4 (2026-08-05) | v3.1.2 (2026-08-21) | v2.0.6-beta.4 (2026-10-13, pre-release) | v420.53.2 según un commit del 2026-10-02 |
| Último commit en la rama principal | 2026-08-05 | 2026-09-27 | 2026-10-13 | 2026-10-02 |
| Valoración | Mantenido, ritmo bajo, issues sin atender | Activo | Muy activo, varias betas por semana | Activo; distribución y autoría no verificables |

Fuentes: [API] para las tres de GitHub; [Web] para ExploitStrap (página del repositorio en Forgejo y commit `22aaed2`). La cuenta `RealSlimShady2000` **no existe en GitHub** (404) [API].

## 3. Arquitectura comparada

Commits analizados: Bloxstrap `02db760`, Fishstrap `988db40`, Froststrap `e8e165d`, ExploitStrap `22aaed2`.

| | Bloxstrap | Fishstrap | Froststrap | ExploitStrap |
|---|---|---|---|---|
| Runtime | .NET 6 (`net6.0-windows`), **sin soporte desde el 2024-11-12** ([política de soporte de .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)) | .NET 6 | **.NET 10** (LTS hasta el 2028-11-14) | .NET 6 |
| UI | WPF + WPF-UI (submódulo `wpfui`) | WPF + WPF-UI | **Avalonia 12** (Windows, macOS y Linux) | WPF + WPF-UI + WebView2 |
| Otros componentes | CsWin32, CommunityToolkit.Mvvm, DiscordRPC, SharpZipLib | Igual, más SharpVectors | Backend en **Rust** (`backend.csproj` compila por target), NLog, BouncyCastle, Nix flake | SQLite, ImageSharp, DPAPI (`ProtectedData`) |
| Tamaño (C#) | 214 ficheros / ~20,5 k líneas | 267 / ~26,1 k | 369 / ~57,8 k | 336 / ~49,1 k |
| Proyectos de test | **Ninguno encontrado** | Ninguno | Ninguno | Ninguno |
| CI | debug, release, winget | debug, release, winget | build, gh-release, winget | debug, release, virustotal |

Todos los datos de esta tabla son [Código].

### Flujo común (heredado de Bloxstrap) [Código]

1. **Protocolo:** el instalador registra `roblox-player:` en `HKCU\Software\Classes` (`Utility/WindowsRegistry.cs`). El navegador abre el bootstrapper con la URI del botón «Jugar» (en este PC: `Bloxstrap.exe -player "%1"` [Local]).
2. **Versión:** consulta `clientsettingscdn.roblox.com/v2/client-version/...` (`RobloxInterfaces/Deployment.cs`).
3. **Paquetes:** descarga `{version}-rbxPkgManifest.txt` desde `setup.rbxcdn.com` (con mirrors `setup-aws`/`setup-ak`), descarga cada zip, **verifica el MD5** contra el manifiesto (`Bootstrapper.cs:1473`, `:1548`), extrae a `Versions\version-xxx` y escribe `AppSettings.xml`.
4. **Configuración:** escribe `ClientSettings\ClientAppSettings.json` (FastFlags). Desde el 2025-09-29 Roblox **ignora los flags que no están en su allowlist** ([DevForum, anuncio oficial](https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569)).
5. **Lanzamiento:** ejecuta `RobloxPlayerBeta.exe` con argumentos derivados de la URI (ticket `-t`, `-b`, etc.).
6. **Supervisión:** el *watcher* hace tail del log más reciente en `%LOCALAPPDATA%\Roblox\logs` y reconoce entradas `[FLog::...]` (unirse, teleport, desconexión). Con eso alimenta Discord RPC y el historial (`Integrations/ActivityWatcher.cs`).

### Puntos débiles observados (lecciones para LarpStrap)

- **Clase dios:** `Bootstrapper.cs` supera las 1500 líneas y mezcla red, disco, UI y procesos. Por eso es difícil de testear, y ninguno de los cuatro tiene tests.
- **Supervisión con un único log:** el watcher sigue «el log más reciente».
- **.NET 6 sin soporte** en tres de los cuatro proyectos.
- Las funciones dependen de comportamientos no documentados de Roblox, que pueden cambiar sin aviso. Ya ocurrió con la allowlist de FastFlags; los tres proyectos de GitHub retiraron opciones por esos cambios ([notas de Bloxstrap v2.10.0](https://github.com/bloxstraplabs/bloxstrap/releases/tag/v2.10.0)).

## 4. Valoración de cada referencia

- **Bloxstrap:** la referencia canónica del flujo de despliegue. Su código es MIT y legible. Útil para entender el protocolo y el manifiesto; mal ejemplo de modularidad.
- **Fishstrap:** Bloxstrap con más opciones. Aporta poco a la arquitectura; es útil como historial de cómo mantener un fork.
- **Froststrap:** el más moderno (.NET 10, multiplataforma, Rust). Es la referencia técnica si algún día se quisiera soporte multiplataforma. **MPL-2.0:** copiar sus ficheros obliga a mantenerlos bajo MPL y publicar su código fuente.
- **ExploitStrap: no recomendado como referencia de código.**
  - Está orientado explícitamente a usuarios de *executors*/exploits. Su README anuncia herramientas «BanAsync» con *MAC spoofing* y aleatorización de `MachineGuid` (evasión de baneos), y tiene clientes para servicios de exploits (`BypassToolsClient`, `LuaObfuscatorClient`, `ExecutorLogoCache`) [Código, solo nombres de ficheros].
  - Guarda cookies `.ROBLOSECURITY` cifradas con DPAPI para su gestor de cuentas [Código].
  - Distribuye binarios **sin firmar** desde un dominio no oficial [Web].

## 5. Licencias aplicables a LarpStrap

| Si se reutiliza… | Obligación |
|---|---|
| Código de Bloxstrap / Fishstrap / ExploitStrap (MIT) | Conservar el aviso de copyright y la licencia MIT (p. ej., en `THIRD_PARTY_NOTICES.md`). Se puede relicenciar el conjunto. |
| Ficheros de Froststrap (MPL-2.0) | Esos ficheros siguen siendo MPL y su código fuente debe ponerse a disposición; el resto del proyecto puede tener otra licencia. |
| Ficheros marcados con AGPL-3.0 en Froststrap | El texto AGPL está en `LICENSES/`, pero no he encontrado ficheros etiquetados con él (grep de `SPDX-License-Identifier: AGPL`). **Hay que verificarlo fichero a fichero antes de copiar nada.** |
| Roblox Account Manager original (GPL-3.0, archivado el 2024-10-01) | Copyleft fuerte; no copiar. |

**Recomendación:** código propio («arquitectura y código originales», como pediste). Las referencias se consultan para entender los protocolos, no para copiar. Si algún fragmento MIT se reutiliza literalmente, se registra en `THIRD_PARTY_NOTICES.md`.

**Marca y Términos de Roblox** ([Términos de Uso, en vigor el 2026-11-01](https://en.help.roblox.com/hc/en-us/articles/115004647846-Roblox-Terms-of-Use), leídos en el navegador [Web]):
- No usar «Roblox» ni su logotipo en el nombre o la identidad visual de forma que sugiera afiliación.
- El bootstrapper descarga el cliente desde el CDN de Roblox; **no debe redistribuir** sus binarios.
- *Restrictions of Use (c)* prohíbe eludir «cualquier medida tecnológica diseñada para proteger los Servicios». LarpStrap no elude ninguna (ADR 0003).
- Solo se puede acceder con la propia cuenta, y las credenciales no se comparten. Es relevante para cualquier gestor de cuentas.

## Fuentes

- Repositorios (API de GitHub y clones): [bloxstrap](https://github.com/bloxstraplabs/bloxstrap), [fishstrap](https://github.com/fishstrap/fishstrap), [Froststrap](https://github.com/Froststrap/Froststrap), [ExploitStrap (Forgejo)](https://sirmemegithub.com/RealSlimShady2000/ExploitStrap)
- [Bloxstrap v2.10.0, notas de la versión](https://github.com/bloxstraplabs/bloxstrap/releases/tag/v2.10.0)
- [Roblox DevForum: allowlist de FastFlags (2025-09-29)](https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569)
- [Roblox Terms of Use](https://en.help.roblox.com/hc/en-us/articles/115004647846-Roblox-Terms-of-Use)
- [Bloxstrap wiki: cómo funciona el bootstrapper de Roblox](https://github.com/pizzaboxer/bloxstrap/wiki/A-deep-dive-on-how-the-Roblox-bootstrapper-works) (no consultada en detalle)
