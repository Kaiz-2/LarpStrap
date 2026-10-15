# ADR 0006: Distribución sin firma de código y licencia MIT (antes ADR-005)

## Estado
Aceptado (2026-10-13)

## Fecha
2026-10-13

## Contexto
LarpStrap es un proyecto personal de un desarrollador individual residente en España. Microsoft Artifact Signing solo admite particulares de EE. UU. y Canadá. SignPath Foundation es gratuita para software de código abierto con licencia OSI, pero exige builds verificables en CI, aprobación manual de cada release y MFA, y emite el certificado a nombre de «SignPath Foundation». Además excluye el software que elude medidas de seguridad; LarpStrap no elude ninguna (ADR 0003), así que sería elegible.

## Decisión
- Licencia **MIT** (`LICENSE`).
- Releases **sin firmar** en GitHub Releases, acompañadas de su hash SHA-256 publicado.
- **Sin autoactualizador:** el usuario actualiza a mano desde Releases. Así no hay que resolver una raíz de confianza sin firma.
- El README avisa de que SmartScreen y los antivirus pueden advertir sobre un ejecutable sin firmar.

## Alternativas consideradas
- **SignPath Foundation:** gratuita y posible con MIT. Se pospone por el proceso que exige. Se puede revisar más adelante sin cambiar el código.
- **Certificado OV/EV de pago:** coste injustificado para un proyecto personal.

## Consecuencias
- Avisos de SmartScreen en cada versión nueva hasta que gane reputación, si llega a ganarla.
- Si más adelante se añade un autoactualizador, necesitará firma o una clave propia de verificación. Eso requerirá otra ADR.
