# Guía de marca de LarpStrap: «Nebulosa»

> **Versión:** 1.2, 2026-10-16: acentos como color base y 7 temas de interfaz, todo en `Themes/*.xaml` y comprobado por tests. 1.1, 2026-10-15: paleta sobria de Codex (superficies gris azuladas, acentos y degradado menos saturados).
> **Estado:** vigente. La 1.0 (paleta saturada «azul noche») acompañó a la 0.3.0.
> **Fuente de la verdad:** los colores, fuentes y medidas viven en `src/LarpStrap.App/Themes/*.xaml`. Este documento los describe, y `assets/design-tokens.json`/`.css` son exportaciones de esos archivos (`eng/Export-DesignTokens.ps1`). Si algo no coincide, manda el XAML.

## Referencia rápida

| Element | Value |
|---------|-------|
| Primary Color | #765CC2 |
| Secondary Color | #285A78 |
| Accent Color | #BBAEEA |
| Primary Font | Bahnschrift (títulos) · Segoe UI Variable (texto) |
| Voice | Honesta, directa, cercana |

## 1. Concepto

LarpStrap controla los clientes de Roblox de tu PC. La marca **«Nebulosa»** combina:
- un **fondo gris azulado** neutro, tranquilo para sesiones largas;
- un **violeta apagado** que marca la acción sin estridencias;
- el **degradado violeta → azul petróleo** como firma, en pocos sitios.

Se inspira en los bootstrappers de la comunidad (Bloxstrap, Fishstrap, Froststrap): estilo Windows 11 (Mica), ajustes en tarjetas y un diálogo de arranque con el logotipo. **No copia nada de ellos:** solo toma ideas de interfaz (ADR 0005).

**Principios, por orden de prioridad:**
1. **Honestidad.** Nada aparece activo si no hace algo real, y el titular de Inicio refleja el estado verdadero.
2. **Lo simulado siempre a la vista.** El modo demostración se marca en la barra de título, en cada fila y en cada resultado, y en el aviso para lectores de pantalla.
3. **Accesible por defecto.** Contraste WCAG comprobado por tests, foco de teclado visible, nombres accesibles y el estado nunca indicado solo por color.

## 2. Logotipo

Una **«L» que sostiene el símbolo de reproducir** sobre una baldosa redondeada con el degradado de marca. Significa lanzar desde LarpStrap.

![Logotipo de LarpStrap](design/logo-256.png)

- **Geometría:** `LogoTile` y `LogoMark` en `Tokens.xaml`, en una caja de 64 × 64. El icono `.ico` se genera de ahí con `eng/Build-Icon.ps1`, para todos los tamaños de 16 a 256 px.
- **Variantes de la aplicación:** Clásico (L + reproducir), Órbita y Monograma se pueden elegir en Personalización. La ventana y Acerca de se actualizan al momento; el instalador y el icono de Windows conservan el clásico.
- **Sobre el degradado de marca:** la baldosa va en blanco translúcido (`OnBrandFaintBrush`) y el símbolo en blanco.
- **Margen mínimo:** ¼ del lado alrededor del logotipo.
- **Tamaño mínimo:** 16 px.
- **No:**
  - deformarlo ni girarlo;
  - cambiar los colores del degradado;
  - ponerlo sobre fotografías sin la baldosa;
  - añadirle sombras o contornos.

## 3. Color

Todos los pares de color están medidos con la fórmula WCAG 2.x. `ThemeContrastTests` hace fallar el build si alguno baja de su mínimo.

### Primary Colors

| Name | Hex | Uso |
|------|-----|-----|
| Violeta marca | #765CC2 | Relleno de botones y controles activos, tema oscuro (texto blanco, 5,1:1) |
| Violeta profundo | #5B4A9E | Relleno de acento, tema claro (texto blanco, 7,2:1) |
| Violeta luz | #BBAEEA | Acento como texto sobre fondos oscuros (8,5:1 sobre Surface) |

### Secondary Colors

| Name | Hex | Uso |
|------|-----|-----|
| Nebulosa inicio | #4B3A83 | Inicio del degradado de marca (texto blanco, 9,4:1) |
| Nebulosa fin | #285A78 | Final del degradado de marca (texto blanco, 7,4:1) |

Sobre el degradado, el texto secundario usa `OnBrandMutedBrush` (#D8D6E5, ≥ 5,2:1) y el botón blanco de la cabecera lleva el texto en `OnHeroButtonBrush` (#30265C, 13,5:1).

### Accent Colors

El usuario elige el acento en Personalización: 23 con nombre o uno personalizado (#RRGGBB). Cada acento es **un color base** (`AccentSeed*` en `Accents.xaml`). `AccentPalette` lo convierte, para el modo y el tema activos, en tres colores:
- un **relleno** con al menos 3:1 sobre la superficie;
- el **texto sobre ese relleno**, blanco o negro según cuál contraste más (siempre ≥ 4,5:1);
- el **acento como texto**, con ≥ 4,5:1 sobre Surface y SurfaceAlt.

El color base se aclara (modo oscuro) o se oscurece (modo claro) solo lo necesario. `ThemeContrastTests` comprueba los 23 acentos en los 7 temas y los 2 modos, más colores personalizados extremos.

| Acento | Color base |
|--------|------------|
| Violeta (marca) | #9475F5 |
| Azul | #70A7F7 |
| Rosa | #F28DAD |
| Turquesa | #45C2B5 |
| Coral | #FF806F |
| Lima | #A5D65C |
| Ámbar | #F4B942 |
| Cian | #31C7E8 |
| Menta | #54D6A0 |
| Esmeralda | #32C784 |
| Bosque | #4D9A67 |
| Cielo | #6BC7FF |
| Océano | #347FC4 |
| Índigo | #6475ED |
| Lavanda | #B39DFF |
| Magenta | #D66BE8 |
| Rosa chicle | #FF76C8 |
| Carmesí | #E34B68 |
| Naranja | #F28A32 |
| Oro | #D7AD32 |
| Bronce | #BA7748 |
| Pizarra | #8B9AAF |
| Grafito | #64748B |

### Temas de interfaz

Cada tema (`Presets.xaml`) sustituye solo las superficies de su modo: Bg, Surface, SurfaceAlt, SurfaceHover y BorderSubtle. Texto, bordes de control y estados siguen en `Colors.*.xaml`. Hay 7 temas: Nebulosa (por defecto, los valores de la tabla siguiente), Medianoche, Océano, Bosque, Atardecer, Rosa y Pizarra. Los valores exactos están en `Presets.xaml` y en `assets/design-tokens.json` (`theme`).

### Neutros y estados

| Token | Oscuro | Claro | Uso |
|-------|--------|-------|-----|
| Bg | #111318 | #F3F4F6 | Fondo de la ventana (si no hay Mica) |
| Layer | #D9111318 | #D9F3F4F6 | Capa del contenido sobre Mica/Acrílico (85 % opaca) |
| Surface | #181B22 | #FFFFFF | Tarjetas |
| SurfaceAlt | #20242C | #EEF0F3 | Controles y elementos elevados |
| SurfaceHover | #292E38 | #E4E7EB | Fondo al pasar el ratón |
| BorderSubtle | #303743 | #D7DCE3 | Separadores |
| BorderStrong | #9AA8BA | #687386 | Bordes de controles (≥ 3:1 en todos los temas) |
| Text | #E7EAF0 | #1B2028 | Texto (14,3–16,4:1) |
| TextMuted | #A9B1BE | #525C69 | Texto secundario (≥ 5,9:1) |
| TextDisabled | #858F9D | #737C89 | Texto desactivado |
| Ok / Warn / Error / Info | #69B889 / #D8A64D / #DB8080 / #79AFC2 | #28704C / #8B5F18 / #A94E52 / #3A6E7F | Estados, siempre con texto o icono (≥ 4,5:1 sobre Surface y SurfaceAlt en todos los temas) |
| Fondos suaves de estado | #182A22 / #302719 / #301D20 / #1A2931 | #E7F2EB / #F5EEDF / #F7E9EA / #E7F0F3 | Avisos y chips de estado |
| FocusRing | #FFFFFF | #1B2028 | Anillo de foco del teclado |

**Etiqueta SIMULADO:** rectángulo sin contorno sobre el fondo suave de información (`SimulatedChip`, radio 4). Sigue presente en la barra de título, en cada fila y en cada resultado simulado; ya no destaca por el contorno, sino por el texto en mayúsculas y su posición.

## 4. Tipografía

| Uso | Fuente | Tamaño |
|-----|--------|--------|
| Marca y titulares (`FontBrand`, `FontDisplay`) | **Bahnschrift** SemiBold | 40 (hero), 30 (página), 20 (sección) |
| Texto (`FontText`) | Segoe UI Variable Text | 14 (cuerpo), 16 (subtítulo), 12 (nota) |
| Datos técnicos: PID, versiones, logs (`FontMono`) | Cascadia Mono | 13 |
| Iconos (`FontIcons`) | Segoe Fluent Icons | 16–20 |

Todas son **fuentes de Windows**: no se descarga ni se incluye ningún archivo de fuente. Bahnschrift da el aire técnico y «gaming»; Segoe UI mantiene la legibilidad del texto largo. Las fuentes de la recomendación inicial (Russo One y Chakra Petch) se descartaron porque había que descargarlas y por la legibilidad del cuerpo de texto.

## 5. Voz

- **En español, de tú, directa:** «Pulsa Lanzar para abrir Roblox».
- **Honesta con los límites:** «LarpStrap no cierra el proceso de Roblox en segundo plano: ciérralo desde la bandeja». Nunca prometas lo que no hace.
- **Cada error dice qué pasó y qué hacer:** «Roblox se cerró al arrancar. Vuelve a intentarlo; si se repite, revisa Diagnóstico».
- **Sin jerga interna** (PID solo donde aporta), **sin culpar al usuario** y sin exclamaciones.

## 6. Componentes y forma

- **Radios (iteración 1 del rediseño, 2026-10-14):** 6 para controles, 8 para tarjetas, 12 para la cabecera de marca, 4 para la etiqueta SIMULADO y 999 para los *chips*.
- **Menos bordes:** las tarjetas y las tarjetas de ajuste no llevan contorno; la jerarquía la marca el relleno (capa → superficie → superficie alternativa). Solo conservan borde los campos, los avisos y el foco.
- **El degradado de marca** solo aparece en la cabecera de Inicio, en el diálogo de arranque y en el panel lateral del instalador.
- **Navegación (iteración 2):** grupos JUGAR (Inicio, Clientes abiertos, Accesos directos), AVANZADO (FastFlags, Diagnóstico, Cuentas; solo en modo avanzado) y un pie con Personalización, Ajustes, Acerca de y el interruptor de modo. Se contrae de 240 a 64 px con ☰; en modo compacto, la etiqueta pasa al tooltip y sigue siendo el nombre accesible. El modo y la navegación contraída se guardan en `Settings.json`.
- **Modo simple y modo avanzado:** el simple es para jugar; el avanzado añade las herramientas técnicas, abre «Detalles» y «Actividad» y muestra las opciones técnicas de Ajustes. Lo oculto sigue funcionando con su valor guardado.
- **Listas compactas** (clientes, juegos): filas de 52 px; los datos técnicos (PID, memoria, origen) van en «Detalles», plegado, y las acciones destructivas en «⋯ Más acciones» y en el menú contextual.
- **Espaciado:** múltiplos de 4. Las páginas tienen un margen de 32, las tarjetas un relleno de 20.
- **Tarjeta de ajuste** (`views:SettingCard`): icono, título, descripción y el control a la derecha, que pasa debajo cuando la ventana es estrecha o el texto grande.
- **Material:** Mica o Acrílico en Windows 11 22H2 o posterior; fondo sólido en el resto o si el usuario lo elige. El contenido va en una capa redondeada sobre el material.
- **Iconos decorativos** (`views:Glyph`): el lector de pantalla no los lee. Un icono que informa por sí mismo lleva su nombre accesible.

## 7. Movimiento

- Fundido de 150 ms al cambiar de página y giro del indicador en el diálogo de arranque.
- **Ambos se desactivan** si Windows tiene las animaciones apagadas (`SystemParameters.ClientAreaAnimation`).

## 8. Comprobaciones

| Qué | Cómo |
|-----|------|
| Contraste de todos los pares | `ThemeContrastTests` (automático) |
| Tokens exportados iguales al XAML | `DesignTokensTests` (automático); regenerar con `eng/Export-DesignTokens.ps1` |
| Nombres accesibles y foco de teclado | `tests/manual/Test-Accessibility.ps1` |
| Ningún color hexadecimal fuera de `Themes/` | Revisión de código: los colores de vista previa y de la cabecera están en `Accents.xaml` |
