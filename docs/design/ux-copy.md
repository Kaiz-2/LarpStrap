# Textos de la interfaz: Panel de instancias

Fecha: 2026-10-13. Idioma: español de España, tuteo. Estado: **propuesta**. Todavía no se ha aplicado al código; los textos actuales están en `LaunchOrchestrator.cs` y `MainWindow.xaml(.cs)`.

## Glosario (el mismo término en todas partes)

| Concepto | Término | No usar |
|---|---|---|
| Un proceso de Roblox con ventana | **cliente** | instancia (solo en el título de la sección), juego, proceso |
| Cliente iniciado por esta sesión de LarpStrap | **Lanzado por LarpStrap** | Lanzado, propio |
| Cliente que ya estaba abierto o lo abrió otro programa | **Externo** | ajeno |
| Proceso de Roblox sin ventana en la bandeja | **En la bandeja** | tray, segundo plano |
| Estados | **Iniciando · En ejecución · No responde · Cerrado · Se cerró inesperadamente** | Running, Starting, crash |
| Cierre normal | **Cerrar** | Matar, Terminar |
| Cierre sin esperar al cliente | **Forzar cierre** | Kill, Matar |

El número de proceso se muestra como texto secundario («PID 14284»), nunca como título.

## Botones y acciones

| Elemento | Texto |
|---|---|
| Acción principal sin clientes abiertos | **Lanzar** |
| Acción principal con un cliente abierto | **Cerrar y lanzar…** (con «…» porque abre una confirmación) |
| Cancelar un lanzamiento en curso | **Cancelar lanzamiento** |
| Acciones de una fila | **Enfocar** · **Cerrar** · **Forzar cierre…** |

## Diálogos de confirmación

**Cerrar y lanzar** (recomendado):
> **¿Cerrar el cliente abierto y lanzar uno nuevo?**
> Se cerrará el cliente que está abierto. Si estás en una partida, saldrás de ella.
> [**Cerrar y lanzar**] [**Mantener el actual**]

| Opción | Título | Tono | Mejor para |
|---|---|---|---|
| A (recomendada) | ¿Cerrar el cliente abierto y lanzar uno nuevo? | Directo | Uso normal |
| B | Ya tienes Roblox abierto | Informativo | Si se quiere ofrecer más opciones (p. ej. «Enfocar el actual») |
| C | Solo se permite un cliente de Roblox | Explicativo | La primera vez que el usuario lo ve |

**Forzar cierre:**
> **¿Forzar el cierre de este cliente?**
> Se cerrará sin guardar y saldrás de la partida en curso. Úsalo solo si «Cerrar» no funciona.
> [**Forzar cierre**] [**Cancelar**]

Los botones llevan el nombre de la acción, no «Sí» y «No». Por eso hace falta sustituir el `MessageBox` actual por un diálogo propio.

## Estados de la lista

| Estado | Texto |
|---|---|
| Vacío | **No hay ningún cliente de Roblox abierto.** Pulsa Lanzar para abrir Roblox, o entra a un juego desde el navegador. |
| Actualizada | Actualizado hace 2 s |
| Desactualizada | **Sin actualizar desde hace 30 s.** No se puede leer el estado de los procesos. Reintentando… |
| No responde | **No responde** · El cliente lleva 10 s sin responder. Espera o ciérralo. |
| Cerrado inesperadamente | **Se cerró inesperadamente** · Revisa el diagnóstico para ver los detalles. |

## Resultados de lanzamiento (qué pasó, por qué y qué hacer)

| Resultado (`LaunchOutcome`) | Texto actual | Texto recomendado |
|---|---|---|
| Started | Ventana del cliente detectada. | **Roblox está abierto.** |
| InstallationNotFound | No se encontró RobloxPlayerBeta.exe en: … | **No se encontró Roblox.** Instala Roblox y vuelve a intentarlo. *(Detalle: carpetas revisadas)* |
| AlreadyOpen | Ya hay un cliente de Roblox abierto o iniciándose. | **Ya hay un cliente abierto.** Ciérralo o usa «Cerrar y lanzar». |
| StartFailed | Win32Exception: … | **No se pudo iniciar Roblox.** Windows rechazó abrir el programa. Comprueba que el antivirus no lo bloquea y vuelve a intentarlo. *(Detalle: tipo de error)* |
| ExitedDuringLaunch | El proceso X terminó a los N ms sin mostrar ventana. | **Roblox se cerró al arrancar.** Vuelve a intentarlo; si se repite, abre el diagnóstico. |
| TimedOut | Sin ventana tras 60 s; el proceso X sigue abierto. | **Roblox tarda en abrirse.** Sigue arrancando en segundo plano; puedes esperar o cerrarlo desde la lista. |
| Cancelled (antes de iniciar) | Cancelado antes de iniciarse. | **Lanzamiento cancelado.** |
| Cancelled (ya iniciado) | Cancelado; el proceso X ya iniciado sigue abierto. | **Lanzamiento cancelado.** El cliente que ya se había abierto sigue en ejecución. |

Los textos recomendados son para la interfaz. Los detalles técnicos actuales (PID, milisecondos, tipo de excepción) pasan a la línea de **diagnóstico**, que es donde se necesitan.

## Notas de localización
- **Solo español en la v1.** Aun así, conviene sacar los textos a recursos (`.resx`) para no tener cadenas sueltas por el código.
- **Plurales:** nada de «abierto(s)». Cada caso tiene su propia frase.
- **Números y tiempos:** formato de España, con coma decimal («5,9 s»).
- **Longitud:** en la v1 no hay traducción, pero se deja margen de un 30 % en botones y etiquetas por si llega una versión inglesa.
