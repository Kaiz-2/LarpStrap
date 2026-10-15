# ADR 0005: Código original; las referencias solo se consultan (antes ADR-004)

## Estado
Aceptado (2026-10-13)

## Fecha
2026-10-13

## Contexto
Quieres arquitectura, código e identidad originales. Licencias de las referencias: Bloxstrap, Fishstrap y ExploitStrap son MIT; Froststrap es MPL-2.0 (con el texto AGPL-3.0 incluido en `LICENSES/`, sin ficheros etiquetados encontrados); el Roblox Account Manager original es GPL-3.0. ExploitStrap se orienta a exploits y a evadir baneos (informe 01).

## Decisión
- El código se escribe desde cero. Las referencias sirven para entender protocolos, no se copian.
- Si se reutiliza literalmente un fragmento MIT, se registra en `THIRD_PARTY_NOTICES.md` con su copyright.
- No se copia código MPL, GPL ni AGPL.
- No se reutiliza nada de ExploitStrap.
- Ni el nombre ni la identidad visual usan marcas de Roblox de forma que sugiera afiliación.

- LarpStrap se publica bajo **MIT** (ver ADR 0006).

## Consecuencias
- Más trabajo inicial en el despliegue, a cambio de una base limpia y testeable y sin obligaciones de copyleft.
