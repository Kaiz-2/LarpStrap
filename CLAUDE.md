# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Estado del proyecto (2026-10-14)

LarpStrap es un bootstrapper de Roblox para Windows. Su función principal es **Clientes abiertos** (antes «Panel de instancias»; en el código, `InstancesViewModel`): supervisa y controla los clientes oficiales de Roblox del PC y su proceso en segundo plano. La sección **Dos cuentas** puede abrir Roblox - Windows (Microsoft Store) y Roblox web en paralelo; cada una conserva su sesión propia y Roblox puede cerrar una al abrir la otra.

- **Código:** .NET 10 (SDK **exacto** 10.0.401 en `global.json`, sin avanzar: el lock file del instalador depende de la versión del SDK), WPF + CommunityToolkit.Mvvm, xUnit v3 sobre Microsoft Testing Platform.
  - `LarpStrap.Core`: lógica y servicios (`Instances/`, `Settings/`, `Logging/`), sin UI.
  - `LarpStrap.App`: MVVM, con `Themes/` (sistema de diseño, la única fuente de color), `ViewModels/`, `Views/` y `Services/`.
  - `LarpStrap.Setup`: el instalador (`LarpStrap-Setup.exe`). Enlaza los temas de App sin copiarlos; su motor está en `LarpStrap.Core/Setup`. Lo empaqueta `eng/Publish-Release.ps1`.
  - El modo demostración usa `SimulatedProcessProvider` y siempre se etiqueta como SIMULADO.
  - Tests: `tests/LarpStrap.Core.Tests` (lógica) y `tests/LarpStrap.App.Tests` (ViewModels, sin ventanas).
- **Estado y pendientes:** ver `PLAN.md`. Validación de la UI en `docs/testing/ui-validation-2026-10-13.md`. Auditoría, seguridad y log en `docs/quality/`.
- **Pruebas de UI automatizadas:** nunca enviar teclas sin comprobar antes que LarpStrap está en primer plano. Un intento sin esa comprobación tecleó en el cliente de Roblox del usuario.

## Comandos

```
dotnet build LarpStrap.slnx -c Release                       # 0 warnings: TreatWarningsAsErrors en Directory.Build.props
dotnet test --solution LarpStrap.slnx -c Release             # todos los tests (MTP: la ruta va tras --solution o --project)
dotnet test --project tests/LarpStrap.Core.Tests --filter-method "*SettingsStoreTests*"   # un test o una clase
dotnet format LarpStrap.slnx --verify-no-changes             # formato, igual que en CI
dotnet run --project src/LarpStrap.App
```

La CI (`.github/workflows/ci.yml`) ejecuta restore, formato, build, test y `dotnet list package --vulnerable`.
- **Licencia:** MIT (`LICENSE`).
- **Distribución:** sin firma de código y sin autoactualizador (ADR 0006).
- **Decisiones:** `docs/adr/0002…0008`. Todas aceptadas (0007: el paquete es dependiente del framework, no autocontenido; 0008: instalador propio, por usuario y sin elevación, junto al zip portable). Se numeran por el archivo, como las cita el código; los documentos anteriores usan `ADR-001…005` (cada ADR indica su número antiguo).

## Leer antes de cambiar nada

1. `docs/research/03-arquitectura-y-fases.md` (v2): arquitectura, fases y criterios de salida.
2. `docs/adr/0002…0008`.
3. `research_notes/protocolo-despliegue-lanzamiento.md`: cómo se despliega, se lanza y se observa el cliente de Roblox.

## Hechos que condicionan el diseño

- **Lanzamiento normal:** el flujo principal de LarpStrap lanza un cliente cada vez; un proceso `--launch-to-tray` puede ocupar esa plaza desde el arranque. El flujo separado Roblox - Windows + web solo abre los dos clientes oficiales y no fuerza la concurrencia; su compatibilidad depende de Roblox.
- **Equipo de desarrollo:** Windows 11 Home. Bloxstrap gestiona `roblox-player:`, y Fishstrap también está instalado.
- **Ticket de autenticación:** la URI `roblox-player:1+…` lo lleva dentro (`gameinfo:`), y la línea de comandos del cliente lo lleva en `-t`.
- **Supervisión:** no retener handles a procesos de Roblox. Hay un aviso en el código de Bloxstrap de que el anticheat salta si un handle sigue abierto más de un minuto.

## Límites del proyecto

- **Nunca eludir ni manipular la instancia única del mismo cliente:** nada de retener u ocupar sus objetos, cerrar handles ajenos, terminar o desactivar el proceso de bandeja, inyectar ni parchear (ADR 0003). Sí se permite abrir la app oficial de Microsoft Store junto a Roblox web; LarpStrap no garantiza que Roblox mantenga ambas sesiones abiertas.
- **Sin credenciales:** nunca guardar cookies ni contraseñas, y nunca registrar URIs ni líneas de comandos completas (ADR 0004).
- **Código propio:** no copiar código de las referencias; nada de ExploitStrap (ADR 0005).
- **Pedir confirmación al usuario** antes de:
  - matar procesos de Roblox;
  - cambiar el gestor de `roblox-player` o `roblox:`;
  - ejecutar CLIs externas.
- **Pruebas:** etiquetar cada una como *simulada* o *cliente real*. Las de cliente real se registran en la sección «Registro de pruebas con cliente real» de `docs/testing/acceptance.md`.
- Responder en español.
