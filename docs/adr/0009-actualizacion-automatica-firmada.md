# ADR 0009: Actualización automática verificada con una clave propia

## Estado

**Aceptado** (2026-10-16, a petición del propietario). Sustituye la parte de la ADR 0006 que decía «Sin autoactualizador». La distribución sigue sin firma de código Authenticode.

## Contexto

Tener que descargar cada versión a mano es demasiado molesto para el usuario. El propietario pidió que LarpStrap reciba las versiones nuevas y se actualice solo al iniciarse.

La ADR 0006 descartaba un actualizador porque, sin firma de código, no hay forma de comprobar quién publicó lo que se descarga. Un actualizador que ejecuta lo que encuentra en Internet es el mejor objetivo posible para repartir malware a todos los usuarios. Ya preveía que hacerlo exigiría una firma o una clave propia de verificación.

El repositorio pasó a ser público el 2026-10-16 (`Kaiz-2/LarpStrap`), así que sus releases se pueden descargar sin iniciar sesión.

## Decisión

1. **Clave propia de firma (ECDSA P-256).**
   - La clave privada solo existe en el equipo del propietario, fuera del repositorio, y la usa `eng/Publish-Release.ps1`. La genera `eng/New-UpdateSigningKey.ps1` en `%LOCALAPPDATA%\LarpStrap-Release`, legible solo por su usuario. `Publish-Release.ps1` se niega a empaquetar si la clave no corresponde a la pública de `UpdateTrust`.
   - La clave pública va dentro de LarpStrap (`UpdateTrust`).
   - Se usa la criptografía de .NET, sin dependencias nuevas.
2. **Manifiesto firmado en cada release:**
   - `update.json`: versión, nombre del instalador, tamaño y SHA-256;
   - `update.json.sig`: la firma de esos bytes exactos.
3. **Al iniciar, LarpStrap, en segundo plano:**
   1. consulta las releases de `Kaiz-2/LarpStrap` en la API de GitHub;
   2. descarga el manifiesto y su firma;
   3. **descarta la actualización** y lo registra en el log si:
      - la firma no es válida;
      - la versión no es más nueva que la instalada;
      - algún dominio no está en la lista permitida (`api.github.com`, `github.com` y los de descarga de GitHub);
      - el instalador no coincide en tamaño o SHA-256.
4. **Instalación:**
   1. el instalador verificado se guarda en `%LOCALAPPDATA%\LarpStrap\Updates` y se vuelve a comprobar su SHA-256 justo antes de ejecutarlo; las descargas de versiones ya instaladas se borran;
   2. LarpStrap avisa en la barra de estado, se cierra y ejecuta el instalador con `--update <pid>` (silencioso, conserva los accesos directos y reabre LarpStrap);
   3. el instalador espera hasta un minuto a que ese proceso termine, actualiza con su mecanismo transaccional (ADR 0008) y lo vuelve a abrir.
   4. Sin permisos de administrador.
5. **Nunca interrumpe una partida:**
   - si hay un cliente de Roblox abierto o arrancando, o un lanzamiento en curso, la actualización espera al siguiente inicio (el proceso de Roblox en segundo plano no cuenta);
   - no cierra Roblox.
6. **Solo para instalaciones hechas con el instalador,** que desde 0.4.0 es la única forma de distribución. Una copia portable antigua no se actualiza sola.
7. **Se puede desactivar** en Acerca de («Actualizar automáticamente»), donde también se puede buscar una actualización en cualquier momento.

## Alternativas consideradas

- **Solo avisar y enlazar la descarga:** es más seguro y más simple, pero el propietario lo descartó por ser molesto para el usuario.
- **Firma de código Authenticode:** daría además confianza ante SmartScreen, pero sigue descartada por coste y proceso (ADR 0006). Si llega, se puede sumar a la verificación.
- **Comprobar solo el SHA-256 publicado en la release:** no protege nada si alguien se hace con la cuenta de GitHub, porque podría cambiar el instalador y el hash a la vez. La firma con una clave que no está en GitHub sí.

## Consecuencias

- **La clave privada es crítica:**
  - si se pierde, las versiones instaladas no aceptarán más actualizaciones, y habrá que publicar una con una clave nueva que los usuarios instalen a mano;
  - si se filtra, cualquiera podría firmar actualizaciones.
  - Hay que guardar una copia en un lugar seguro y nunca subirla al repositorio.
- **Los usuarios de 0.3.0 o anteriores** tienen que instalar a mano, una vez, la primera versión con actualizador.
- **LarpStrap hace peticiones a GitHub al iniciar** (versión y manifiesto). No envía datos del usuario; GitHub ve la IP, como en cualquier descarga.
- **Las releases deben incluir `update.json` y `update.json.sig`;** sin ellos, LarpStrap no actualiza.
