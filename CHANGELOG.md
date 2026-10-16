# Changelog

Todos los cambios relevantes de LarpStrap. Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/); versiones según [SemVer](https://semver.org/lang/es/).

## 0.4.5 - 2026-10-16 (versión preliminar)

### Corregido
- Evita el cierre al iniciar: las plantillas WPF ahora resuelven correctamente los recursos del tema compartido.

## 0.4.4 - 2026-10-16 (versión preliminar)

### Cambiado
- Ventana con barra de título propia y botones para minimizar, maximizar/restaurar y cerrar.
- Nuevo tema oscuro obsidiana con tarjetas, bordes y radios unificados; el instalador comparte los mismos recursos.
- FastFlags pasa a la navegación principal; la sección Mods se muestra deshabilitada porque aún no hay soporte.
- Pie de ventana con estado de guardado automático y acceso a Lanzar Roblox; diálogo de lanzamiento con barra de progreso.

## 0.4.3 - 2026-10-16 (versión preliminar)

### Corregido
- Evita un cierre al iniciar causado por asignar un estilo de ToggleButton a botones normales en la portada y el aviso.

## 0.4.2 - 2026-10-16 (versión preliminar)

### Cambiado
- Portada renovada con estado del cliente, acceso directo para jugar, biblioteca y acciones rápidas.
- Los avisos de actualización y conexión aparecen en un banner visible; la actualización indica su versión antes de reiniciar.

## 0.4.1 - 2026-10-16 (versión preliminar)

### Cambiado
- Rediseño completo de la interfaz principal, navegación, tarjetas, temas y asistente de instalación.

## 0.4.0 - 2026-10-16 (versión preliminar)

### Añadido
- **Actualización automática** (ADR 0009). Al iniciarse, la versión instalada busca una nueva en GitHub, verifica su firma con la clave propia de LarpStrap y su SHA-256, y se actualiza sola sin permisos de administrador.
  - Nunca con Roblox abierto ni durante un lanzamiento.
  - Se desactiva en Acerca de, donde también está «Buscar ahora».
  - Quien tenga 0.3.0 debe instalar esta versión a mano una vez.
- Instalador: `--update <pid>` (lo usa el actualizador).

### Cambiado (distribución)
- **Solo se publica el instalador:** un único `.exe` que lleva LarpStrap dentro y lo instala. Ya no hay zip portable.

### Añadido (Codex, revisado)
- **Dos cuentas:** abre Roblox - Windows (Microsoft Store) y Roblox web, cada uno con su sesión. No está garantizada ni probada con cliente real (ver limitaciones).
- **Accesos directos:** nombre e icono públicos del juego desde Roblox, y «Unirme» por web o por Microsoft.
- **Personalización:** 7 temas de interfaz, 23 acentos más uno personalizado y 3 logotipos.
- Pasos del instalador rediseñados.

### Corregido (revisión de los cambios de Codex)
- Los colores de los temas y acentos vuelven a `Themes/*.xaml` (`Presets.xaml`, `AccentSeed*`), y los tests de contraste cubren todos los temas, modos y acentos. El rojo de error oscuro pasa a `#DB8080`: no llegaba a 4,5:1 sobre los menús en tres temas.
- Roblox - Windows se busca sin abrir PowerShell.
- Los mensajes de «Unirme» ya no dan por hecho que Roblox se abrió.
- La consulta a Roblox espera a que dejes de escribir: una petición por juego, no una por tecla.
- El nombre que escribes ya no se sustituye por el título de Roblox.
- La documentación dice ahora a qué se conecta LarpStrap.

### Retirado
- **Todo lo relativo a varios clientes de Roblox a la vez,** porque Roblox no lo permite:
  - el selector «Clientes a la vez» y el lanzamiento de varios clientes seguidos;
  - el ajuste «Pausa entre lanzamientos» (los `Settings.json` que lo tengan se siguen leyendo; el valor se ignora);
  - la lista ya no está pensada para 4 clientes: muestra el cliente abierto y el proceso de Roblox en segundo plano.
  - El modo demostración simula lo mismo que un PC real: un cliente y el proceso en segundo plano.
  - Se borraron la investigación y los documentos sobre la viabilidad de la multiinstancia; la ADR 0003 se reescribe como «un cliente cada vez», y las pruebas de aceptación pasan a `docs/testing/acceptance.md`.

### Cambiado
- Nueva paleta más sobria (superficies gris azuladas, acentos y degradado menos saturados) y etiqueta SIMULADO rectangular sin contorno, hechas por Codex.

## 0.3.0 - 2026-10-14 (versión preliminar)

Primera versión con **instalador** (`LarpStrap-Setup-0.3.0-win-x64.exe`); el zip portable sigue disponible con los mismos archivos. Versión única para la app y el instalador en `Directory.Build.props`.

**Sin probar con un cliente real de Roblox:** abrir un juego desde un enlace del navegador, que Roblox aplique los FastFlags escritos por LarpStrap, que un crash real deje un informe y Discord Rich Presence. Todo eso está cubierto solo con tests *simulados* (ver `docs/KNOWN-LIMITATIONS.md`).

### Cambiado (rediseño, iteración 2: modo simple y avanzado)
- **Modo simple (por defecto) y modo avanzado,** con un interruptor en el pie de la navegación y en Ajustes › Interfaz:
  - **simple:** la navegación muestra JUGAR (Inicio, Clientes abiertos, Accesos directos) y el pie (Personalización, Ajustes, Acerca de);
  - **avanzado:** añade el grupo AVANZADO (FastFlags, Diagnóstico, Cuentas), abre «Detalles» y «Actividad» en Clientes abiertos y muestra en Ajustes la pausa entre lanzamientos y la integración con Discord;
  - si pasas a simple estando en una pantalla avanzada, vuelves a Inicio;
  - lo que se oculta sigue funcionando con su valor guardado.
- **«Panel de instancias» pasa a llamarse «Clientes abiertos»** (Alt+L).
- **Se recuerdan** el modo y la navegación contraída (`AdvancedMode`, `CompactNavigation` en `Settings.json`; los archivos anteriores se abren en modo simple).

### Cambiado (rediseño, iteración 1: solo XAML, estilos y tokens)
- **Inicio:** cabecera compacta con el estado real y «Lanzar»; tus juegos son el contenido principal, en mosaico, y cada tarjeta entera abre el juego en roblox.com. Las tres tarjetas de estado se sustituyen por una línea con el último resultado.
- **Panel de instancias:**
  - lista compacta, una fila por cliente, con estado, Enfocar, Cerrar y «⋯»;
  - PID, memoria y origen en «Detalles», plegado;
  - **Forzar cierre** en «⋯ Más acciones» y en el menú contextual (clic derecho o Mayús+F10), con la misma confirmación de siempre;
  - la actividad está plegada.
- **Accesos directos:** lista compacta de juegos; «Quitar» pasa a un botón de icono con nombre accesible.
- **Navegación contraíble** con ☰ (de 240 a 64 px; solo durante la sesión).
- **Menos ruido:**
  - tarjetas sin contorno;
  - radios de Fluent (6/8/12);
  - el aviso de demostración aparece una vez, en el panel, además del chip de la barra de título y el chip SIMULADO de cada fila. Se quitan los de la barra lateral y de Inicio.
- Sin cambios de lógica: los mismos comandos, textos de estado y confirmaciones. «Panel de instancias» mantiene su nombre hasta la iteración 2, porque está en C#.

### Añadido (instalador, fase 6)
- **`LarpStrap-Setup-<versión>-win-x64.exe`:** un asistente con la marca de LarpStrap (Bienvenida, Opciones, Instalación, Listo) que instala de verdad, por usuario y sin permisos de administrador (ADR 0008):
  - elección de carpeta, validada antes de tocar nada;
  - accesos directos opcionales en el menú Inicio y el escritorio;
  - entrada en Configuración › Aplicaciones;
  - actualización y reparación en la misma carpeta, conservando la configuración;
  - recuperación de instalaciones interrumpidas;
  - desinstalación que quita solo lo que instaló.
  - El éxito solo se anuncia después de comprobar el SHA-256 de cada archivo, la versión, los accesos directos y el registro.
  - Modo silencioso: `--silent`, `--dir`, `--no-*-shortcut`, `--launch`, `--uninstall`.
- `tests/manual/Test-Installer.ps1` (16 comprobaciones de extremo a extremo) y `Test-InstallerWizard.ps1` (el asistente, por UI Automation).

### Corregido (CI)
- La CI de `main` falló al restaurar los paquetes: `setup-dotnet` instalaba el SDK más reciente de la serie (10.0.402), que pide `Microsoft.NET.ILLink.Tasks` 10.0.13 para el instalador, y el lock file fija la 10.0.12. `global.json` fija ahora el SDK exacto (`rollForward: disable`).

### Seguridad
- **S-5:** el lanzador nativo de .NET cargaba un `hostfxr.dll` plantado junto al exe, antes de cualquier código de LarpStrap. Afecta a 0.2.0 y 0.2.1. Corregido con `AppHostDotNetSearch=Global`; `Test-DllPlanting.ps1` incluye ahora `hostfxr.dll` y `hostpolicy.dll`.

### Añadido (funciones de la fase 4)
- **Abrir juegos desde el navegador** (P4-T01/T02): `LarpStrap.exe roblox-player:…` abre ese juego. Si LarpStrap ya está abierto, el segundo arranque le pasa el enlace por un *named pipe* que solo puede abrir tu usuario y se cierra. El enlace se valida y llega al cliente como un único argumento; del enlace solo se registran el ID del juego y el modo, nunca el ticket. Para que el botón «Jugar» de la web use LarpStrap falta registrar el protocolo (P4-T03), que cambia el gestor actual (Bloxstrap) y requiere tu confirmación.
- **Crash frente a cierre** (P4-T04): si Roblox se cierra solo y deja un informe de error (`*.dmp`), el panel lo muestra como «Se cerró por un error» en vez de «Se cerró fuera de LarpStrap».
- **«No responde» con margen:** solo tras 5 s colgado, porque una pantalla de carga suele bloquearse un momento. El cambio se anota en la actividad.
- **Editor de FastFlags** (P4-T08), nueva pantalla: solo los 18 flags que Roblox acepta desde el 29-09-2025, con su tipo y rango. Se escriben en la versión de Roblox que lanza LarpStrap justo antes de abrirla, sin tocar las claves de otros programas. En el modo demostración no se escribe nada.
- **Discord Rich Presence** (P4-T10), opcional y desactivado por defecto: muestra «En Roblox» y desde cuándo; nunca el juego ni la cuenta. Sin dependencias nuevas. Necesita el ID de una aplicación de Discord tuya (Ajustes → Integraciones).

### Cambiado (rediseño «Nebulosa»)
- **Interfaz nueva de arriba abajo**, inspirada en los bootstrappers de la comunidad (Bloxstrap, Fishstrap, Froststrap; solo ideas, sin código):
  - **ventana estilo Windows 11:** material Mica o Acrílico a elegir (sólido en Windows 10), barra de título propia con el logotipo y el contenido en una capa redondeada;
  - **Inicio:** cabecera de marca con un titular que refleja el estado real, botón grande «Lanzar», tarjetas de estado y tus juegos en mosaico;
  - **diálogo de arranque** con el logotipo y un indicador mientras Roblox abre, con «Cancelar lanzamiento» enfocado;
  - **panel:** clientes como tarjetas con avatar de estado y *chips*, y la actividad como línea de tiempo;
  - **Ajustes, Personalización y Acerca de:** tarjetas de ajuste con icono y descripción; temas con vista previa; acentos como muestras de color.
- **Marca «Nebulosa»:**
  - el violeta pasa a ser el acento por defecto (los acentos ya guardados se respetan), con turquesa, coral, lima y ámbar como alternativas;
  - Bahnschrift en los títulos;
  - **logotipo e icono propios** (`eng/Build-Icon.ps1`).
- **Guía de marca** en `docs/brand-guidelines.md`, y **tokens CSS/JSON** exportados desde los temas (`eng/Export-DesignTokens.ps1`, en `assets/`).

### Añadido
- **Tests de diseño:**
  - `ThemeContrastTests` comprueba con la fórmula WCAG el contraste de todos los pares de color de ambos temas y de los 5 acentos;
  - `DesignTokensTests` impide que los tokens exportados se desincronicen del XAML.
- **Iconos decorativos fuera del lector de pantalla** (`views:Glyph`).

### Corregido (accesibilidad)
- Los campos «Juego» y «Nombre» de Accesos directos no tenían nombre accesible: un lector de pantalla los anunciaba solo como «edición». Ahora toman el de su etiqueta.
- El bloque de información del sistema de Diagnóstico era una lista sin nombre; ahora se llama «Información del sistema».
- Los avisos de lanzamiento de la barra de estado, que lee el lector de pantalla, indican «(simulado)» en el modo demostración. Antes un lanzamiento simulado sonaba igual que uno real.
- Encontrado al probar el rediseño en modo demostración (pruebas *simuladas*):
  - el diálogo de arranque dejaba el foco en la ventana en vez de en «Cancelar lanzamiento»;
  - la lista de secciones era una parada de tabulación vacía;
  - con la ventana a 900 px y el texto al 130 %: «Acerca de» quedaba oculto tras el aviso de demostración (la navegación ahora se desplaza), el título del panel se cortaba, el chip de la instalación se montaba sobre «Lanzar» y la etiqueta del campo «Juego» se cortaba.

### Añadido
- `tests/manual/Test-Accessibility.ps1`: recorre las 8 pantallas con UI Automation y falla si algún control interactivo visible no tiene nombre accesible o no recibe el foco del teclado.

## 0.2.1 - 2026-10-14 (versión preliminar)

Versión de corrección sobre 0.2.0. Sin funciones nuevas.

### Corregido
- Un cliente lanzado que nunca muestra ventana ya no deja el panel bloqueado: su fila ofrece «Forzar cierre…» (con confirmación) y «Lanzar» explica por qué está bloqueado (O-01).
- Dos refrescos del panel solapados ya no hacen reaparecer un cliente recién cerrado ni lo registran como «se cerró fuera de LarpStrap» (A).
- «Cerrar y lanzar» registra el cierre y la salida del cliente que cierra; todos los cierres y salidas se registran en un único punto (B).
- El resultado de un lanzamiento conserva la etiqueta real o SIMULADO del modo en que se lanzó, aunque se cambie de modo a mitad (C).

## 0.2.0 - 2026-10-14 (versión preliminar)

Primera versión, publicada como *pre-release* en GitHub (`v0.2.0`, paquete construido desde `7f6e10d`, SHA-256 `445b3df6fe77a7481f514a5c17262bdd12b77df754f201a4eec541ec129b02ce`). Las limitaciones están en [docs/KNOWN-LIMITATIONS.md](docs/KNOWN-LIMITATIONS.md).

### Añadido
- **Panel de instancias:**
  - detecta los clientes de Roblox abiertos y muestra su estado (Iniciando, En ejecución, No responde, En segundo plano, Cerrado, Se cerró fuera de LarpStrap), la memoria y desde cuándo están abiertos;
  - acciones: enfocar, cerrar, y forzar el cierre con confirmación;
  - lanzar la app de Roblox, y «Cerrar y lanzar» cuando ya hay un cliente;
  - cancelar un lanzamiento sin cerrar el cliente que ya arrancó.
- **Modo demostración:** clientes **simulados**, siempre etiquetados como tales, para ver todos los estados sin abrir Roblox.
- **Interfaz WPF:** ocho pantallas, tema claro y oscuro, cuatro colores de acento, tamaño de texto de 100–150 %, nombres accesibles, teclas de acceso y anuncios para lectores de pantalla.
- **Accesos directos** a juegos, por place id o por enlace de roblox.com: se abren en el navegador.
- **Diagnóstico:**
  - visor de los logs de LarpStrap;
  - exportación en zip, solo de los logs propios.
- **Log local** con lista blanca de campos: rotación (10 × 5 MB), hora con desfase UTC y eventos de lanzamiento, cierre, salida de clientes, fallos del panel y errores no previstos (`docs/quality/observability.md`).
- **Configuración** con versión de esquema, corrección de valores no válidos, copia `.bad` si está dañada y escritura atómica.
- **Distribución:** paquete portable dependiente del framework con SHA-256 (`eng/Publish-Release.ps1`, ADR 0007), `docs/INSTALLATION.md` y `docs/KNOWN-LIMITATIONS.md`.

### Seguridad
- No guarda credenciales, no lee la línea de comandos de Roblox y no registra URIs ni tickets (ADR 0004).
- No elude la instancia única de Roblox ni actúa sobre su proceso de bandeja (ADR 0003).
- No retiene handles a procesos de Roblox, y los clientes se lanzan sin heredar los de LarpStrap.
- Defensa contra DLL plantadas junto al exe. Comprobada con 7 DLL en el paquete dependiente del framework.
- `explorer.exe` se lanza por ruta completa.
- La exportación de logs nunca borra un archivo que ya existía.
- Dependencias fijadas con `packages.lock.json`, restore bloqueado y barrera de vulnerabilidades en la CI.
- El paquete se rechaza si contiene la ruta del perfil o el correo de quien lo genera.

### Corregido (durante el desarrollo; ninguno llegó a publicarse)
- Cancelar «Cerrar y lanzar» cerraba la aplicación.
- Un `Settings.json` con un número de tema o color inexistente impedía arrancar.
- Un cliente recién lanzado podía figurar como cerrado por una carrera entre refrescos.
- Al cambiar el modo demostración podían verse clientes reales etiquetados como simulados, o al revés.
- Una carpeta de versiones de Roblox ilegible cerraba la aplicación al arrancar.

