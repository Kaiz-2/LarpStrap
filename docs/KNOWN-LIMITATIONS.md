# Limitaciones conocidas: LarpStrap 0.3.0

Lo que LarpStrap **no hace**, **no garantiza** o **hace mal** a día de hoy. Los identificadores remiten a [docs/quality/final-audit.md](quality/final-audit.md) (F-, O-), [security-review.md](quality/security-review.md) (S-, R-) y a la revisión `/ship` del 2026-10-14 (A–C, I-, H-).

## Por diseño (no se va a «arreglar»)

- **«Dos cuentas» no está garantizada ni probada.** Abre Roblox - Windows y Roblox web, cada uno con su sesión. Roblox puede cerrar una al abrir la otra, y no hay ninguna prueba con cliente real registrada. «Unirme por Microsoft» usa el programa registrado para los enlaces `roblox:`, que puede ser otro launcher (Bloxstrap, Fishstrap) en lugar de Roblox - Windows.
- **Sin almacenamiento de cuentas.** LarpStrap no guarda cookies ni contraseñas (ADR 0004); las sesiones las mantienen Roblox - Windows y el navegador.
- **No toca el proceso de bandeja de Roblox.** Si ese proceso ocupa la plaza, el lanzamiento se bloquea y hay que cerrarlo desde la bandeja de Windows.
- **Sin firma de código ni actualizador automático** (ADR 0006). SmartScreen avisa; las versiones se comprueban con el SHA-256 publicado.
- **Requiere el .NET 10 Desktop Runtime** instalado aparte (ADR 0007).
- **Solo Windows 10/11 x64 e interfaz en español.**

## Funciones que todavía no existen

- **Registrar LarpStrap como gestor de `roblox-player:`** (P4-T03). Sin registro, el botón «Jugar» de la web sigue abriendo el gestor actual (Bloxstrap). LarpStrap ya abre un enlace si se le pasa (`LarpStrap.exe roblox-player:…`, desde 0.3.0). «Lanzar» abre la página de inicio de la app de Roblox (`--app`).
- **Perfiles de cuenta, cerrar a la bandeja y comprobación de actualizaciones.** Las que aparecen en la interfaz están desactivadas y marcadas «no disponible».
- **Discord Rich Presence** necesita que crees una aplicación de Discord y pegues su ID: LarpStrap no tiene una aplicación propia registrada.
- **FastFlags:** si abres Roblox con otro programa (Bloxstrap, Fishstrap), este puede sustituir el archivo de flags. LarpStrap los vuelve a escribir en cada lanzamiento suyo. Si no se pueden escribir (archivo bloqueado), Roblox se abre con los anteriores y la barra de estado lo dice.

## Defectos conocidos

| Id | Qué pasa | Cómo sortearlo |
|---|---|---|
| O-01 / I-2 | **Corregido en 0.2.1** (afecta a 0.2.0). Un cliente lanzado que nunca muestra ventana se quedaba en «Iniciando» sin botones y bloqueaba el siguiente lanzamiento. Ahora su fila ofrece «Forzar cierre…» y «Lanzar» explica el bloqueo | Actualizar a 0.2.1 |
| A | **Corregido en 0.2.1** (afecta a 0.2.0). Dos refrescos solapados podían hacer reaparecer como «Externo» un cliente que LarpStrap acababa de cerrar. Ahora los refrescos se aplican de uno en uno | — |
| B | **Corregido en 0.2.1** (afecta a 0.2.0). «Cerrar y lanzar» no registraba el cierre del cliente. Ahora los cierres y las salidas se registran en un único punto | — |
| C | **Corregido en 0.2.1** (afecta a 0.2.0). Un lanzamiento interrumpido por un cambio de modo se etiquetaba con el modo nuevo | Actualizar a 0.2.1 |
| — | **Corregido en 0.3.0** (afecta a 0.2.1). Tras un lanzamiento **simulado**, la barra de estado, que lee el lector de pantalla, decía «Roblox está abierto.» sin indicar «simulado» | — |
| S-1 (código) | Si «Cerrar» falla y después el cliente se cierra por otra causa, el log dice `result=Closed` | — |
| S-7 (código) | Cancelar «Cerrar y lanzar» no deshace el cierre ya pedido, y el mensaje no lo dice | — |
| S-6 (código) | Un `<nombre>.zip.tmp` que quede de una exportación interrumpida impide exportar con ese mismo nombre | Elegir otro nombre o borrar el `.tmp` |
| O-04 | Si Windows no tiene navegador o programa asociado, «Abrir en el navegador» o «Abrir carpeta» cierran la app (queda registrado en el log). No reproducido | — |
| O-05 | Un guardado fallido puede dejar `Settings.json.tmp`. No tiene efecto | — |

## Seguridad: riesgos aceptados

- **S-5, afecta a 0.2.0 y 0.2.1; corregido en 0.3.0:** si en la carpeta donde extraes el zip ya hay un `hostfxr.dll` ajeno, el lanzador de .NET lo carga antes que LarpStrap. Actualiza a 0.3.0; con 0.2.x, extrae el zip en una **carpeta nueva y vacía**, nunca directamente en `Descargas`.
- **Instalador:** al desinstalar queda una copia del desinstalador (< 1 MB) en `%TEMP%\LarpStrap-Uninstall-<id>\`. `--uninstall --silent` termina antes de que acabe el trabajo. No es reproducible bit a bit: se publica el hash del artefacto concreto (ADR 0008).

- **H-2:** no hay protección si se ejecuta **como administrador**. En ese caso lanzaría con permisos elevados el `RobloxPlayerBeta.exe` más reciente de carpetas que tu usuario puede escribir. No lo ejecutes elevado.
- **H-3:** «Forzar cierre» termina todo el árbol de procesos del cliente. Si el cliente abrió otro programa (por ejemplo, el navegador) como proceso hijo, también se cerraría. Es una hipótesis sin comprobar.
- **H-4:** se listan clientes de **otras sesiones de Windows** (cambio rápido de usuario) y pueden bloquear el lanzamiento.
- **H-5:** entre la confirmación de «Forzar cierre» y la acción, el PID podría reutilizarlo otro cliente de Roblox. Solo se comprueba el nombre del proceso.
- **R-2:** no se verifica la firma Authenticode del `RobloxPlayerBeta.exe` que se lanza.

## Accesibilidad

- **Alto contraste de Windows:** LarpStrap no lo respeta. Usa siempre sus propios colores (tema claro u oscuro), aunque Windows tenga activado el modo de alto contraste. Detectado el 2026-10-14 con la guía de WPF de la skill de UI/UX; pendiente de implementar y de probar con el modo activado.
- **Afecta a 0.2.1 y corregido en 0.3.0:** dos campos de Accesos directos y la información del sistema de Diagnóstico no tenían nombre accesible.

## Sin verificar

- **«Unirme por web»** usa la ruta `roblox.com/games/start`, que Roblox no documenta y puede cambiar; sin sesión iniciada en el navegador solo muestra la página del juego.

- Prueba en un **equipo limpio**: este equipo tiene Windows 11 Home, sin Windows Sandbox. El instalador se probó con el usuario real de este equipo, en carpetas de prueba.
- **Instalador:** la instalación en otra cuenta de Windows, con OneDrive redirigiendo el escritorio, con un antivirus de terceros y sin el .NET Runtime.
- El aviso de Windows cuando **falta el .NET Runtime**.
- **Llegar a una partida real** con un cliente lanzado (AT-18b), ahora también abriendo un enlace del navegador.
- Que Roblox **aplique los FastFlags** escritos por LarpStrap, que **un crash real** deje siempre un `.dmp` y que **Discord** muestre la actividad: todo lo nuevo de la fase 4 está probado solo con tests *simulados*.
- **Teclado y lector de pantalla reales** (P5-T05).
- **Arranque en frío por debajo de 2 s** (P5-T06). Se han medido 4,41 s en la primera ejecución de una build nueva y alrededor de 1 s en las siguientes.
