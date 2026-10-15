# Sistema de diseño de LarpStrap (v1, fase 5)

> **Sustituido** el 2026-10-14 por la marca «Nebulosa»: ver [docs/brand-guidelines.md](../brand-guidelines.md). Este documento queda como histórico de la dirección «Consola» (v1).

Implementado en `src/LarpStrap.App/Themes/`. Dirección **A · Consola** del prototipo: el usuario no eligió entre A, B y C, así que el asistente aplicó su recomendación. Incluye tema claro y corrige el ámbar duplicado que señaló la crítica de diseño.

## Principios
1. **Honestidad:** un control solo aparece activo si hace algo real. Lo que no existe se muestra desactivado con la etiqueta «No disponible todavía» y su motivo.
2. **Real o simulado, siempre explícito:** el modo demostración muestra un banner en el panel, un aviso en la navegación, la etiqueta SIMULADO en cada fila y «· simulado» en cada resultado.
3. **El estado nunca depende solo del color:** cada indicador combina un punto o barra de color con texto («En ejecución», «No responde»…).
4. **Teclado y lector de pantalla primero:** anillo de foco propio, teclas de acceso en la navegación, nombres de automatización por cliente y una región «live» para anunciar resultados.

## Tokens

| Archivo | Contenido | Uso |
|---|---|---|
| `Colors.Dark.xaml` / `Colors.Light.xaml` | Fondo, superficies, bordes, texto (normal, secundario, deshabilitado), estados (Ok, Warn, Error, Info y sus versiones suaves), FocusRing | `DynamicResource`, porque el tema cambia en ejecución |
| `Accents.xaml` | 4 acentos (ámbar, turquesa, coral, lima): relleno + versión de texto para fondo claro + `OnAccent` | Los aplica `ThemeService` |
| `Tokens.xaml` | Tipografía, tamaños (12/14/16/20/28), radios (6 control / 8 contenedor), altura de control 36 | `StaticResource` |

- **Tipografía:**
  - Segoe UI Variable Text/Display, con Segoe UI como respaldo;
  - Cascadia Mono / Consolas para PID, versiones y logs;
  - iconos Segoe Fluent Icons / Segoe MDL2.
  - Son fuentes del sistema, así que no se incluyen archivos de fuentes.
- **Espaciado:** múltiplos de 4.
- **Colores escritos a mano fuera de `Themes/`:** **0**, comprobado con grep (criterio 10 del spec de app-shell).

## Contraste (fórmula WCAG 2.x, medido)

| Par | Oscuro | Claro | Mínimo |
|---|---|---|---|
| Texto / superficie | 14,3:1 | ≥15:1 | 4,5 |
| Texto secundario / superficie | 7,5:1 | 7,6:1 | 4,5 |
| Estados (Ok, Warn, Error, Info) / superficies | ≥4,6:1 | ≥5,2:1 | 4,5 |
| Texto / fondos suaves de banner | ≥11,9:1 | ≥15,3:1 | 4,5 |
| Texto secundario / fondos suaves | ≥6,2:1 | ≥6,4:1 | 4,5 |
| Borde fuerte (controles) / superficie | 3,51:1 | ≥3:1 | 3 (no texto) |
| Texto `OnAccent` / acentos | ≥6,1:1 | igual | 4,5 |
| Acento como texto (claro) / superficie | — | ≥4,5:1 | 4,5 |

Durante el diseño se corrigieron dos pares que fallaban: el borde fuerte oscuro (2,84 → 3,51) y el verde «Ok» claro sobre superficie alternativa (4,32 → 5,28).

## Componentes (`Controls.xaml`)

| Componente | Variantes y estados |
|---|---|
| Botón | Base, `PrimaryButton`, `DangerButton`, `GhostButton`. Hover y pulsado con capa de opacidad; deshabilitado; foco con anillo de 2 px. Altura mínima de 36 |
| Navegación (`NavItem`) | Icono + texto. Seleccionado = barra de acento + superficie elevada (sin relleno de acento) |
| Opción segmentada (`OptionButton`) | Marcada = relleno de acento; deshabilitada con motivo en el nombre accesible y el tooltip |
| Interruptor (`ToggleCheck`) | Activo, inactivo, deshabilitado |
| Campo de texto | Borde de 2 px en `FocusRing` con el foco; alineación vertical configurable |
| Tarjeta (`Card`), `Banner` (Info / Warn / Error / Ok) | — |
| Píldora de estado | Punto + texto, sobre fondo suave según el tipo |
| Lista, barras de desplazamiento, tooltip | Adaptados al tema |

## Movimiento
- Al cambiar de página, fundido de 150 ms. Se omite si Windows tiene las animaciones desactivadas (`SystemParameters.ClientAreaAnimation`).
- Hover en 2 niveles de opacidad, sin animaciones decorativas.

## Pendiente
- **Logotipo:** el actual (cuadro + escuadra en acento) es un marcador; falta exportar un `.ico` propio para la ventana y el exe.
- **Barra de título:** se usa la estándar de Windows, oscurecida en tema oscuro con `DWMWA_USE_IMMERSIVE_DARK_MODE`.
