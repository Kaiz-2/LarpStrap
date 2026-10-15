# Mapa de capacidades: LarpStrap v1

**Estado:** aprobado el 2026-10-13. El usuario decidió que `app-shell` vaya primero y que FastFlags, Discord RPC y Mods entren en la v1. Este mapa es el índice de los specs: cada módulo tiene su `SPEC-<id>.md` en la raíz.

| Id del módulo | Responsabilidad | Depende de | Spec |
|---|---|---|---|
| `app-shell` | Preparación del proyecto (fase 0), ventana WPF, navegación, ajustes persistentes, icono de bandeja, tema, log con lista blanca e **instancia única de LarpStrap con canal interno (named pipe)** | — | [SPEC-app-shell.md](SPEC-app-shell.md) |
| `deployment` | Versión de Roblox, descarga y verificación MD5, preparación en staging, activación sin clientes abiertos, limpieza de versiones | `app-shell` | pendiente |
| `launch` | Lectura de la URI `roblox-player:` (solo campos permitidos), registro y restauración del protocolo, entrega de la URI por el canal interno y lanzamiento del cliente | `deployment` | pendiente |
| `instance-panel` | Panel de instancias: detección por PID con handles efímeros, estados, logs FLog y controles de ventana | `launch` | pendiente |
| `fastflags` | Editor de FastFlags limitado a la allowlist de Roblox; escribe `ClientAppSettings.json` en la versión activa | `deployment` | pendiente |
| `mods` | Sustituir assets locales (solo contenido, nunca ejecutables ni DLL) y volver a aplicarlos en cada versión | `deployment` | pendiente |
| `discord-rpc` | Discord Rich Presence con el juego de la instancia en primer plano | `instance-panel` | pendiente |

**Orden de construcción:** `app-shell` → `deployment` → `launch` → `instance-panel` → `fastflags`, `mods` → `discord-rpc`

**Cambio respecto a la propuesta:** la instancia única de LarpStrap pasa de `launch` a `app-shell`. Con la ventana construida primero, ya hace falta evitar que se abra dos veces. `launch` solo amplía el canal interno para que transporte la URI.
