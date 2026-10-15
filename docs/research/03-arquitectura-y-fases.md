# Informe 3 — Arquitectura y fases de desarrollo

Fecha: 2026-10-13 · **Versión 2**, revisada tras una revisión adversarial con dos modelos. Decisiones en `docs/decisions/` (ADR-001 a ADR-005). Base: informe [01](01-comparativa-bootstrappers.md) y `research_notes/protocolo-despliegue-lanzamiento.md`.

## 1. Principios

1. **Nada de botones de mentira:** cada control visible tiene detrás un comportamiento verificado. Si Roblox no permite algo, la interfaz lo dice.
2. **No eludir ni manipular Roblox:** no se anula la instancia única, no se tocan handles ajenos, no se inyecta, no se parchea, y no se termina ningún proceso sin confirmación del usuario (ADR-002).
3. **Sin credenciales y sin registrar tickets:** nunca se registran la URI ni la línea de comandos completas (ADR-003).
4. **Núcleo testeable:** la lógica no depende de WPF ni de Win32. Las interfaces solo existen donde un test las necesita.
5. **Pruebas etiquetadas:** se distingue entre *simulada* y *cliente real*, y de cada prueba con cliente real se registra la evidencia (§6).

## 2. Stack (ADR-001)

| Pieza | Elección |
|---|---|
| Runtime | .NET 10 (LTS, soporte hasta el 2028-11-14) |
| UI | WPF + CommunityToolkit.Mvvm 8.4.x, con estilos propios (el tema Fluent de WPF sigue «in progress») |
| Win32 | CsWin32 0.3.x |
| Tests | xUnit |
| CI | GitHub Actions `windows-latest`: build + test |
| Licencia y distribución | MIT; releases sin firmar con SHA-256; sin autoactualizador (ADR-005) |

## 3. Estructura

```
LarpStrap.sln
src/LarpStrap.Core/    net10.0-windows, sin UI
  Deployment/          versión, manifiesto, descarga, MD5, staging, activación de versión
  Launch/              parseo de la URI (lista blanca de campos), argumentos, protocolo
  Instances/           descubrimiento por PID, máquina de estados, parser de FLog
  Platform/            Win32: procesos (handles de vida corta), ventanas, registro, pipe
src/LarpStrap.App/     WPF: shell, Panel de instancias, bandeja
tests/LarpStrap.Core.Tests/  simuladas, con fixtures de logs y manifiestos reales
docs/testing/          registro de evidencias de pruebas con cliente real
LICENSE  README.md  THIRD_PARTY_NOTICES.md (si hace falta)
```

## 4. Componentes

### 4.1 Instancia única de LarpStrap
Cada clic en «Jugar» abre un proceso `LarpStrap.exe "roblox-player:…"`. El primer proceso crea un mutex propio (`Local\LarpStrap-<usuario>`) y un *named pipe*. Los procesos siguientes le envían la URI por el pipe y terminan. Un único proceso decide cada lanzamiento, así que no hay carreras.

### 4.2 Despliegue
1. Versión desde `client-version` (solo canal de producción).
2. Se descarga el manifiesto y los paquetes desde una **lista blanca** de hosts del CDN, con concurrencia limitada, reintentos con *backoff* y comprobación previa de espacio en disco.
3. MD5 de cada paquete. Garantiza integridad, no autenticidad; es la limitación del protocolo de Roblox.
4. Extracción en `staging/<version>` y escritura de `AppSettings.xml` **dentro del staging**.
5. **Activación solo si no hay clientes abiertos.** Si los hay, la versión nueva queda preparada y se activa en el siguiente lanzamiento sin clientes.
6. Se conservan la versión activa y la anterior; las demás se borran.

Si algo falla, la versión anterior sigue lanzable y la interfaz muestra el paso que falló.

### 4.3 Protocolo
- Se registra `roblox-player:` y `roblox:` en `HKCU\Software\Classes`, con confirmación del usuario. Antes se guarda el gestor anterior.
- Al arrancar, LarpStrap **detecta si otro launcher ha vuelto a reclamar el protocolo** y lo avisa, sin recuperarlo por su cuenta.
- La opción «Restaurar gestor anterior» existe **desde la fase 1**. Antes de restaurar, comprueba que el ejecutable guardado sigue existiendo; si no, deja que Roblox gestione el protocolo.

### 4.4 Panel de instancias (ADR-002)

**Descubrimiento.** Cada 1–2 s se buscan los procesos `RobloxPlayerBeta.exe` por PID. Por cada uno se abre un handle de vida corta (`PROCESS_QUERY_LIMITED_INFORMATION`), se lee y se cierra al momento. **Nunca se retiene un handle**, ni con `Process.Exited` ni con `Process.Start` sin liberar. Se muestran los clientes lanzados por LarpStrap y los ajenos (estos marcados como «externo»). El proceso de bandeja se identifica y se muestra aparte, sin actuar sobre él.

**Estado de cada instancia:**

```
Lanzando ─► Cargando ─► EnPartida(placeId, jobId) ─┐
   │           ▲                                   │ teleport
   │           └───────────────────────────────────┘
   ├─► Desconectada ─► Cargando / Cerrada
   └─► Cerrada (también si muere antes de cargar)
```

- La **confianza** de cada señal (proceso, ventana, log) se muestra aparte del estado.
- «No responde» es una marca temporal, con un periodo de gracia mientras carga.
- No se distingue «crash» de «cierre» por el código de salida, porque no es fiable. Solo cuenta como crash si el log del CrashHandler registra un volcado.

**Fuentes de las señales:**
- **Ventana:** clase `WINDOWSCLIENT` del PID, para saber si es visible y si responde (`SendMessageTimeout` + `SMTO_ABORTIFHUNG`).
- **Log:** solo ficheros `*_Player_*_last.log`, abiertos con `FileShare.ReadWrite`, con tail incremental y parser de FLog. Para asociar log y PID se prueba Restart Manager. Si falla, se usa la hora de creación del log frente a la hora de inicio del proceso, y si es ambiguo el estado se marca como «sin asociar». **Hay que medirlo con clientes reales.**

**Controles:**
- Foco: puede fallar por el bloqueo de primer plano de Windows, y entonces se avisa.
- Minimizar y organizar en cuadrícula, teniendo en cuenta el DPI.
- Cerrar con `WM_CLOSE`. Si no responde, se ofrece «Forzar cierre», que mata el proceso y su CrashHandler **solo con confirmación**.
- **No hay relanzar** (ADR-003).

**Lanzamiento con un cliente ya abierto:** se avisa de que Roblox cerrará el anterior y se pide confirmación.

## 5. Fases

| # | Fase | Criterio de salida | Pruebas |
|---|---|---|---|
| 0 | **Fundaciones:** SDK de .NET 10, `git init`, solución, CI, `CONSTRAINTS.md`, README | Build y test en verde, en local y en CI | Automáticas |
| 1 | **Bootstrapper:** despliegue (§4.2), protocolo con restauración (§4.3), instancia única con pipe (§4.1), lanzamiento | Tests de parsers, del manifiesto y del pipe. **Real:** entrar en un juego desde el navegador a través de LarpStrap y restaurar después el gestor anterior | Simuladas + cliente real |
| 2 | **Panel de instancias:** descubrimiento, estados, logs, controles | Tests de la máquina de estados y del parser con fixtures reales. **Real:** el estado coincide con la pantalla al unirse, hacer teleport, desconectarse y cerrar; un cliente externo aparece como «externo» | Simuladas + cliente real |
| 3 | **Identidad visual y UX** | Revisión WCAG AA y crítica de diseño | Revisión |
| 4 | **Release:** README con aviso de SmartScreen, SHA-256, `THIRD_PARTY_NOTICES` si hace falta, checklist | Todos los criterios de `CONSTRAINTS.md` en un Windows limpio | Todas |

## 6. Evidencia de las pruebas con cliente real

Cada prueba manual se registra en `docs/testing/real-client-log.md` con estos campos: fecha, versión del cliente, versión de LarpStrap, pasos, duración, resultado y observaciones. Una prueba sin registro no cuenta como verificada.

## 7. Riesgos

| Riesgo | Mitigación |
|---|---|
| Roblox cambia el despliegue o el formato de los logs | Parsers tolerantes, fixtures versionados, fallo visible |
| La asociación log↔PID no es fiable | Estado «sin asociar» visible; nunca se inventa |
| Ejecutable sin firmar: SmartScreen y antivirus | Aviso en el README y SHA-256 publicado (ADR-005) |
| Otro launcher reclama el protocolo | Detección y aviso (§4.3) |
| Roblox restringe la consulta de procesos | La consulta usa el acceso mínimo y handles efímeros; si falla, queda la confianza solo por ventana y log |

## 8. Verificación contra documentación oficial

| Afirmación | Fuente |
|---|---|
| .NET 10 LTS hasta el 2028-11-14; .NET 6 sin soporte desde el 2024-11-12 | [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) |
| WPF en .NET 10; tema Fluent en curso | [What's new in WPF for .NET 10](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100) |
| WPF no admite trimming | [Known trimming incompatibilities](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities#wpf) |
| Objetos con nombre en el espacio de la sesión por defecto; `Local\` y `Global\` | [Kernel object namespaces](https://learn.microsoft.com/en-us/windows/win32/termserv/kernel-object-namespaces) |
| `RmGetList` requiere `RmStartSession` + `RmRegisterResources` | [RmGetList](https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmgetlist) |
| Detección de cuelgues con `SendMessageTimeout` + `SMTO_ABORTIFHUNG`; `IsHungAppWindow` no es de uso general | [SendMessageTimeoutW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagetimeoutw), [IsHungAppWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-ishungappwindow) |
| Protocolo de despliegue y lanzamiento de Roblox | **Sin documentación oficial**: solo hay código de terceros y se trata como contrato inestable |

**Ninguna afirmación de este documento se ha probado todavía en código ni con clientes reales.**
