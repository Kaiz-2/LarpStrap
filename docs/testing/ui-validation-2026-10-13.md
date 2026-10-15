# Validación de la interfaz de escritorio (fase 5), 2026-10-13

**Método:** la app compilada en Release se ejecutó en Windows 11 Home. Se recorrió con un script de UI Automation (seleccionar, pulsar, escribir) y se capturó con `PrintWindow`; las capturas se revisaron una a una. No se usó un lector de pantalla real.

**Datos:**
- **Reales (de este PC):** el cliente de Roblox que el usuario tenía abierto (PID 28336), en solo lectura; **no se actuó sobre él**.
- **Simulados:** los estados, lanzamientos y fallos del panel se probaron en **modo demostración**.

Estados: PASS · FAIL · NOT VERIFIED.

## Flujos ejecutados

| Flujo | Tipo | Resultado |
|---|---|---|
| Arranque y ventana | Real (app) | PASS: la ventana aparece en 1,60–2,82 s según la ejecución (**objetivo < 2 s no siempre cumplido**) |
| Inicio y panel con el cliente real del usuario | Real, lectura | PASS: lo muestra como «Externo · En ejecución», con memoria y hora de inicio; la acción principal pasa a «Cerrar y lanzar…» |
| Activar el modo demostración desde Ajustes | Simulado | PASS: banner, aviso en la navegación y SIMULADO en cada fila |
| Lanzar ×3 (correcto, correcto, falla al arrancar) | Simulado | PASS: el error aparece en el banner de la tarjeta Lanzar, en Actividad y en la barra de estado |
| Lanzar con el límite alcanzado | Simulado | PASS **tras corregir un fallo** (ver abajo) |
| Cerrar un cliente concreto sin afectar a los demás | Simulado | PASS |
| Proceso en segundo plano en solo lectura, sin acciones | Simulado | PASS |
| Cambio de tema (oscuro y claro), acento y tamaño del texto (130 %) | Real (app) | PASS **tras corregir un fallo** (ver abajo); persiste en `Settings.json` |
| Acceso directo: un dominio ajeno se rechaza y un enlace de roblox.com se acepta | Real (app) | PASS |
| Cuentas, Personalización, Diagnóstico y Acerca de: lo no disponible aparece desactivado y explicado | Real (app) | PASS |
| Diagnóstico: lista y contenido de logs | Real (app) | PASS (tras corregir la alineación del texto) |
| Exportar logs (diálogo de guardar) | — | **NOT VERIFIED en la interfaz** (cubierto por `LogFilesTests`) |
| Lanzar un cliente real desde la nueva interfaz | — | **NOT VERIFIED**: el usuario tenía Roblox abierto. El lanzamiento real quedó verificado en la fase 3 (AT-18a) con el mismo servicio |

## Fallos encontrados y corregidos durante la validación

1. **Mensaje equivocado al alcanzar el límite:** decía «proceso en segundo plano» cuando el bloqueo era por número de clientes. **Causa:** la interfaz decidía el mensaje buscando texto dentro del diagnóstico. **Corrección:** resultados estructurados (`BlockedByBackground`, `CloseTimedOut`), con 2 tests nuevos.
2. **Las opciones (tema, acento, tamaño, tiempos) se marcaban pero no se aplicaban** al seleccionarlas por UI Automation, es decir, con un lector de pantalla. **Causa:** dependían de `Command`, que solo se ejecuta con un clic. **Corrección:** `IsChecked` enlazado TwoWay al ViewModel.
3. **A 130 % de texto, la fila del cliente cortaba el PID y la memoria.** Ahora la fila usa `WrapPanel`.
4. **Un fallo de lanzamiento quedaba por debajo del pliegue.** Ahora el último resultado se muestra como banner en la tarjeta Lanzar.
5. **El texto del log aparecía centrado en vertical.** Corregido en la plantilla de `TextBox`.
6. **«Roblox version-cec3…» era confuso.** Ahora dice «Roblox detectado», con la versión como dato secundario.
7. **Tecla de acceso duplicada** (Alt+D en «Accesos directos» y «Acerca de»): ahora «Acer_ca de» usa Alt+C.
8. **Plural «1 cliente(s)»:** ahora «1 cliente» / «N clientes».

## Accesibilidad

| Comprobación | Resultado |
|---|---|
| Contraste de texto y controles en ambos temas | PASS, medido (ver `docs/design/design-system.md`) |
| Todos los controles interactivos tienen nombre accesible | PASS: 0 sin nombre en el árbol de UIA |
| Acciones por cliente identificables («Cerrar cliente 41000») | PASS |
| Idioma de la ventana `es-ES` | Configurado en XAML; **NOT VERIFIED** con un lector de pantalla |
| Anuncios por región «live» | Implementado (LiveSetting + LiveRegionChanged); **NOT VERIFIED** con Narrador o NVDA |
| Lista actualizada sin perder el foco | Implementado (actualización por PID); **NOT VERIFIED** con teclado real |
| Orden de tabulación | El orden del documento es lógico (navegación → Lanzar → acciones del cliente); **NOT VERIFIED con pulsaciones reales** (ver incidente) |
| Escala de texto 100–150 % sin cortes | PASS a 130 % tras la corrección 3; 150 % **no capturado** después de la corrección |

## Incidente durante la prueba de teclado
El primer intento de recorrer la app con Tab no llegó a activar LarpStrap: Windows mantuvo en primer plano el cliente de Roblox del usuario (PID 28336). El script le envió **22 pulsaciones de Tab y un Alt+U**, que se movieron entre los botones de la interfaz del juego. No se envió Enter ni Espacio. Se informó al usuario y se le pidió revisar la partida.

**Medida tomada:** el script de teclado comprueba antes de cada tecla que LarpStrap está en primer plano y aborta si no lo está. Con esa protección, el segundo intento abortó sin enviar nada, porque Windows no permitió traer LarpStrap al frente. Por eso la prueba con teclado real sigue pendiente: requiere que el usuario ponga la ventana en primer plano.
