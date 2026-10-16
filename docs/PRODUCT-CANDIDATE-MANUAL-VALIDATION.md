# Validación global del candidato VTuberBot (una sola pasada en Windows)

**Estado inicial:** PENDIENTE. Candidato CI más reciente verificado el 2026-10-15: `main` SHA `4b9a90282b0b19914ec8a1cc18fdf19ba7a6d818`, run [#896](https://github.com/jonhararagi/vtuber/actions/runs/38789912151), artefacto `VTuberBot-Product-Candidate-win-x64-4b9a90282b0b19914ec8a1cc18fdf19ba7a6d818` (ID `12000555288`), digest del artefacto `sha256:d9a8210332462939e922b813b89c6fa18d1fa3b248dfae3d1609b8aaa74db9fb`. Restore, Build Release, pruebas automatizadas, publicación del paquete autocontenido Windows x64 y subida del artefacto terminaron en éxito para este SHA exacto. El paquete tiene aproximadamente 84,7 MB y expira el 2026-10-29. Descargalo desde la sección **Artifacts** del run #896; extraelo en una carpeta nueva y verificá `BUILD-INFO.txt` antes de ejecutarlo. La validación manual sigue pendiente. Esta lista no certifica el producto por sí sola; registrá el resultado real de cada paso.

## 1. Antes de abrirlo: procedencia del paquete

1. Abrí la ejecución más reciente de [Windows CI](https://github.com/jonhararagi/vtuber/actions/workflows/windows-ci.yml) de `main`.
2. Confirmá que todos los pasos `Restore solution`, `Build Release`, `Run automated tests`, `Publish self-contained Windows x64 validation package` y `Upload temporary validation artifact` terminaron en éxito.
3. Compará el **Head SHA** de esa ejecución con el HEAD actual de `main`. Deben coincidir exactamente.
4. Descargá el artefacto de esa misma ejecución y extraelo en una carpeta nueva. Abrí `BUILD-INFO.txt` y confirmá que su SHA coincide. No uses artefactos antiguos.
5. Registrá run ID, SHA, nombre/ID del artefacto y digest:

| Dato | Valor de esta sesión |
|---|---|
| Fecha/hora | |
| Windows | |
| HEAD de la rama | |
| Run ID / enlace CI | |
| Head SHA del run | |
| Nombre e ID del artefacto | |
| Digest | |
| ¿Coinciden todos los SHA? | |

Si no coincide la procedencia, **detenete** y marcá la sesión BLOQUEADA.

## 2. Inicio, navegación y modo local

- Abrí `VtuberBot.App.exe` sin iniciar Twitch ni OBS. La aplicación debe abrir, mostrar el estado real y seguir siendo utilizable con servicios desconectados.
- Navegá por Inicio, Avatar, Twitch, comandos/comunidad, sorteos/minijuegos, moderación/automatización, Audio, OBS, Telegram, diagnóstico y Ajustes. No debe cerrarse ni congelarse.
- Confirmá que el estado de Twitch indica desconectado/`LOCAL ACTIVO`, que la navegación permanece funcional y que CPU/RAM reales se actualizan en el Dashboard. No ejecutes `help`, `ping` o `status` en esta pasada: la aplicación de escritorio no expone una consola visible para el `CommandRouter` del núcleo; los comandos que sí tienen superficie de usuario se prueban en las secciones de Comandos y Comunidad.
- **Resultado:** APROBADO / FALLIDO — notas:

## 3. Avatar Studio y overlay

- Importá una imagen PNG propia; verificá preview y rechazo comprensible de un archivo inválido.
- Creá/duplicá un perfil, añadí una capa, alterná visibilidad, cambiá su posición/orden, guardá y navegá fuera/volvé a entrar. Comprobá que se conserva.
- Si disponés de sprites Idle/Talking/Happy/Surprised, asigná al menos dos y comprobá el cambio de estado. Sin sprites, la app debe permanecer estable y no fingir que la expresión existe.
- Mostrá el overlay, arrastralo, comprobá el fondo transparente y capturalo como fuente de **Captura de ventana** en OBS. Probá parallax y «Reducir movimiento».
- **Resultado:** APROBADO / FALLIDO — notas:

## 4. Micrófono, VAD, SAPI y sonidos

- Elegí el micrófono real e iniciá captura; hablá y callá para comprobar medidor y transición Talking/Idle. Detené la captura.
- Actualizá la lista de voces SAPI y salidas, elegí una voz instalada, guardá, navegá fuera y regresá. La selección debe persistir.
- Encolá una frase, comprobá que se oye, probá «Detener voz actual» y «Vaciar frases pendientes». Importá y reproducí/detené un efecto local si tenés WAV/MP3.
- Si no hay micrófono o voz SAPI usable, anotá BLOQUEADO; no marques una reproducción inexistente como aprobada.
- **Resultado:** APROBADO / FALLIDO / BLOQUEADO — notas:

## 5. Twitch, OAuth, chat y EventSub

- Con una cuenta de prueba, configurá Client ID y completá OAuth con los permisos solicitados. Confirmá que la interfaz solo indica conectado después de autenticar.
- Conectá IRC al canal, verificá recepción y envío real de un mensaje. Probá un comando válido, permiso denegado y cooldown; una identidad IRC inválida no debe crear un usuario ni recibir puntos.
- Si el canal y los permisos lo permiten, generá un follow/redención/suscripción de prueba y comprobá la alerta/estado visual. Si no hay cuenta/evento de prueba, marcá la parte EventSub BLOQUEADA.
- Desconectá Twitch y verificá que el estado se actualice y las funciones locales sigan disponibles.
- **Resultado:** APROBADO / FALLIDO / BLOQUEADO — notas:

## 6. Comandos, puntos, sorteos y minijuegos

- Creá y guardá un comando; usá la vista previa y confirmá que no publica, no altera puntos y no consume cooldown.
- Ejecutá una operación local de puntos y verificá balance, ledger e historial después de navegar fuera/volver.
- Creá un sorteo, probá entrada duplicada, cerralo con ganador y verificá el historial; probá cancelar otro sin ganador.
- Jugá una partida de práctica con apuesta cero y confirmá que no cambia el saldo. Si hacés una apuesta real local, comprobá que débito/recompensa/historial se actualicen juntos.
- **Resultado:** APROBADO / FALLIDO — notas:

## 7. Moderación y automatizaciones

- Analizá un mensaje de prueba con una regla regex y verificá que la vista previa solo informa coincidencias; no debe borrar ni sancionar.
- Confirmá que la moderación automática está desactivada por defecto. No habilites sanciones reales salvo que estés en un canal de prueba y hayas confirmado conscientemente la acción.
- Revisá mensajes automáticos/temporizadores: no deben publicar al iniciar, y deben informar claramente cuando Twitch está desconectado.
- **Resultado:** APROBADO / FALLIDO — notas:

## 8. OBS Studio

- En OBS, habilitá WebSocket v5 y configurá contraseña. Conectá desde VTuberBot y verificá que el estado se marque autenticado solo tras completar Hello/Identify.
- Cargá escenas y cambiá la escena de programa. Comprobá estados de stream/grabación reales.
- Si estás en un entorno de prueba, iniciá y detené stream/grabación; la detención debe pedir confirmación. No hagas pruebas de transmisión en un canal público sin querer hacerlo.
- Desconectá OBS y verificá que el estado vuelva a desconectado y no se afirmen acciones exitosas.
- **Resultado:** APROBADO / FALLIDO / BLOQUEADO — notas:

## 9. Telegram Web, persistencia y cierre

- Entrá en Telegram: WebView2 debe iniciar la página automáticamente. Si no está disponible, probá el navegador externo y confirmá que el resto de la app sigue funcionando.
- Creá una copia de seguridad SQLite en una ruta temporal. Solo si disponés de datos de prueba, verificá restauración y la copia de emergencia; no sobrescribas datos personales que no quieras reemplazar.
- Cerrá la aplicación normalmente y comprobá que el proceso termina. Reabrila y confirma que las preferencias/datos locales probados persisten.
- Revisá el informe/log local sin publicar tokens, contraseñas, cookies ni datos personales.
- **Resultado:** APROBADO / FALLIDO / BLOQUEADO — notas:

## Cierre de la sesión

| Área | Resultado (APROBADO / FALLIDO / BLOQUEADO / NO EJECUTADO) | Incidencias |
|---|---|---|
| Procedencia y arranque | | |
| Avatar y overlay | | |
| Micrófono/SAPI/sonidos | | |
| Twitch/OAuth/IRC/EventSub | | |
| Comunidad/juegos/sorteos | | |
| Moderación/automatización | | |
| OBS | | |
| Telegram | | |
| Persistencia/cierre | | |

**Criterio:** no declares validación global aprobada si queda un fallo reproducible o si un área obligatoria está sin probar. Las dependencias externas sin cuenta/dispositivo se registran como BLOQUEADAS y no como aprobadas. La lista no exige STT, seguimiento facial ni lip-sync por fonemas: esas capacidades no se deben atribuir a la versión PNG/2.5D actual.
