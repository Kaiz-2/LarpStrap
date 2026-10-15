# ADR 0004: LarpStrap no almacena credenciales ni registra tickets (antes ADR-003)

## Estado
Aceptado, revisado (2026-10-13)

## Fecha
2026-10-13

## Contexto
Los gestores de cuentas guardan cookies `.ROBLOSECURITY`; una cookie robada equivale a la cuenta. Los Términos prohíben compartir credenciales. Cada clic en «Jugar» genera una URI `roblox-player:1+…` con el ticket de autenticación dentro (`gameinfo:`). El cliente lo recibe en la línea de comandos (`-t`). El ticket es de corta vida y de un solo uso. La revisión adversarial detectó que redactar solo `-t` no basta y que «relanzar en la misma ranura» exige credenciales.

## Decisión
- LarpStrap no pide, guarda ni transmite contraseñas, cookies ni tickets.
- **Nunca se registran la URI ni la línea de comandos completas**: ni en logs, ni en mensajes de excepción, ni en el estado del panel. Solo se registran campos permitidos (placeId, modo de lanzamiento) mediante una lista blanca.
- **No hay botón de relanzar.** Para volver a entrar, la interfaz indica que se pulse «Jugar» en el navegador.

## Alternativas consideradas
- **Gestor de cuentas con cookies cifradas (DPAPI):** convierte LarpStrap en objetivo de robo de sesiones. Rechazada.
- **Redactar el ticket con expresiones regulares:** frágil ante cambios de formato. Se prefiere la lista blanca.

## Consecuencias
- Para cambiar de cuenta, el usuario lo hace en el navegador.
- Un test simulado verifica que ningún log contiene una URI de prueba ni un ticket ficticio.
