# ADR 0002: .NET 10 + WPF, solo Windows (antes ADR-001)

## Estado
Aceptado (2026-10-13)

## Fecha
2026-10-13

## Contexto
LarpStrap es un bootstrapper de Roblox solo para Windows, con identidad visual propia y foco en supervisar procesos y ventanas mediante Win32. Bloxstrap, Fishstrap y ExploitStrap usan .NET 6 + WPF; .NET 6 está fuera de soporte desde 2024-11. Froststrap usa .NET 10 + Avalonia + Rust para ser multiplataforma. Ninguno tiene tests (informe 01).

## Decisión
.NET 10 (LTS), WPF con CommunityToolkit.Mvvm, CsWin32 para P/Invoke y xUnit. La lógica vive en `LarpStrap.Core`, sin dependencias de UI.

## Alternativas consideradas
- **Avalonia:** multiplataforma, pero no hace falta; añade una capa de abstracción sobre Win32, que es justo lo que más usamos. Rechazada mientras el alcance sea Windows.
- **WinUI 3 / Windows App SDK:** aspecto moderno, pero el empaquetado y el despliegue son más frágiles para una app sin tienda. Rechazada.
- **Electron/Tauri:** runtime pesado o un segundo lenguaje para la interoperabilidad Win32. Rechazada.

## Consecuencias
- Hay que instalar el SDK de .NET 10 (ahora no hay ningún SDK en el PC).
- WPF no admite trimming: un ejecutable autocontenido rondará varias decenas de MB. Se decide en la fase 6.
