# ADR 0003: Clientes abiertos y lanzamientos oficiales

## Estado
Aceptado (2026-10-13). Revisado el 2026-10-15: no forzar varias instancias del mismo cliente; permitir abrir los clientes oficiales de Microsoft Store y web por separado.

## Contexto
Roblox limita la concurrencia de clientes y su comportamiento puede cambiar por versión. El propietario decidió el 2026-10-16 mantener «Dos cuentas», que abre la app oficial Roblox - Windows (Microsoft Store) y la versión web, cada una con su sesión. **No hay ninguna prueba con cliente real registrada** en `docs/testing/acceptance.md`, y Roblox puede cerrar una sesión al abrir la otra.

## Decisión
1. La función se llama **Clientes abiertos** (antes «Panel de instancias»).
2. Supervisa los procesos de Roblox del PC, los haya lanzado LarpStrap o no: el cliente abierto y, si existe, el proceso en segundo plano (bandeja) de Roblox.
3. Controles sobre el cliente: lanzar, cerrar y lanzar de nuevo con confirmación, enfocar, cerrar y forzar el cierre con confirmación. El proceso de bandeja es de solo lectura.
4. **LarpStrap no elude ni manipula la instancia única del mismo cliente**: nada de retener u ocupar sus objetos, cerrar handles ajenos, terminar el proceso de bandeja, inyectar ni parchear.
5. **Dos cuentas** puede abrir la app oficial Roblox - Windows y Roblox web en el navegador. No guarda credenciales, no lanza dos copias del mismo cliente y no promete que ambas sesiones sigan abiertas.

## Consecuencias
- En el flujo principal, si hay un cliente abierto, «Lanzar» pasa a «Cerrar y lanzar…». «Dos cuentas» es una entrada independiente que abre los dos canales oficiales para iniciar sesión por separado.
- Si solo está el proceso de bandeja, LarpStrap no lanza y explica cómo cerrarlo.
- El modo demostración simula un cliente y el proceso de bandeja, igual que en un PC real.
