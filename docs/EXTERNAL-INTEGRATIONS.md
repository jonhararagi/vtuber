> **Alcance vigente (2026-10-22):** VTuberBot incluye código activo para OBS WebSocket v5, avatar PNGTuber 2D propio, síntesis local VOICEVOX, chat/comandos/moderación de Twitch y una integración OAuth/API/webhook de Kick. Streamer.bot y PNGTuber Plus son integraciones externas opcionales, no dependencias para el funcionamiento básico. El ejecutable no incluye los programas/bancos de voz de terceros y no crea túneles HTTPS para Kick. Los comandos/reacciones públicas de Kick requieren activación explícita. La CI y los servidores simulados prueban el código, pero no sustituyen una comprobación con tus cuentas, dispositivos y programas Windows reales; la lista de validación manual aparece en `docs/CONTINUIDAD_PROYECTO.md`.
>
> Fuente de verdad: [`docs/ESPECIFICACION_DEFINITIVA.md`](ESPECIFICACION_DEFINITIVA.md).

---

# Centro de integraciones externas

La página **Integraciones** coordina aplicaciones de escritorio existentes. No descarga, instala ni incluye ejecutables de terceros.

## Configurar

1. Abrí **Integraciones** desde el menú lateral.
2. En **PNGTuber Plus** y/o **Streamer.bot**, pulsá **Seleccionar ejecutable…** y elegí el `.exe` que ya tengas instalado.
3. La ruta se guarda localmente en las preferencias SQLite de VTuberBot. Podés corregirla cuando cambie la ubicación.

## Estados

- **Sin configurar:** no se guardó una ruta.
- **No encontrado:** la ruta guardada no apunta a un archivo existente.
- **Configurado:** el archivo existe, pero no se detecta su proceso.
- **En ejecución:** el proceso del ejecutable elegido está presente.
- **Inicio solicitado:** Windows aceptó la solicitud para abrirlo.
- **Error:** Windows o el sistema no pudo comprobar o iniciar la aplicación; la interfaz muestra el mensaje devuelto.

La presencia de un proceso **no significa que exista una conexión** entre esa aplicación y VTuberBot. En esta primera versión el centro configura, abre e inspecciona procesos; no afirma integración por WebSocket/IPC ni controla internamente las funciones de PNGTuber Plus o Streamer.bot. El estado se puede volver a consultar con **Actualizar estado**.

VTuberBot no detiene procesos externos al cerrarse. Las aplicaciones deben cerrarse desde sus propias ventanas.

## Pruebas

ExternalApplicationManager depende de IExternalApplicationRuntime; las pruebas sustituyen ese límite para simular archivo ausente, proceso activo y fallo de inicio sin instalar ni lanzar aplicaciones reales.


## Streamer.bot: conexión WebSocket

El espacio **Streamer.bot** usa el servidor WebSocket de la instancia, separado del administrador de ejecutables. La fuente del protocolo es la [documentación oficial de WebSocket](https://docs.streamer.bot/api/websocket), en particular [configuración](https://docs.streamer.bot/api/websocket/guide/configuration), [autenticación](https://docs.streamer.bot/api/websocket/guide/authentication), [solicitudes](https://docs.streamer.bot/api/websocket/requests) y [eventos](https://docs.streamer.bot/api/websocket/events).

1. En Streamer.bot abrí **Servers/Clients → WebSocket Server**. Comprobá que el servidor esté habilitado y anotá Address, Port y Endpoint. Los valores por defecto de Streamer.bot son normalmente `127.0.0.1`, `8080` y `/`.
2. En VTuberBot abrí **Streamer.bot**. Configurá host, puerto y endpoint para que coincidan con la instancia. El valor inicial es local; si configurás un host remoto, verificá la confianza de la red. `ws://` no cifra el tráfico y VTuberBot no abre puertos ni cambia el firewall.
3. Si el servidor envía el objeto `authentication` en `Hello`, introducí la contraseña configurada. VTuberBot calcula el desafío SHA-256/Base64 documentado y nunca envía la contraseña como parte de esa solicitud. La contraseña guardada se protege con DPAPI para el usuario actual de Windows; si no puede descifrarse, introducila de nuevo.
4. Pulsá **Conectar / probar**. El estado conectado solo se establece después de recibir `Hello` válido y completar autenticación cuando el servidor la exige. **Desconectar** cierra la sesión actual; volver a conectar es manual, y las suscripciones deben solicitarse de nuevo para esa conexión.
5. Pulsá **Consultar / actualizar acciones** para usar `GetActions`. Elegí una acción del catálogo real y revisá su ID/estado. **Ejecutar acción seleccionada** pide confirmación y usa `DoAction` por GUID. Un `status=ok` confirma la aceptación de la solicitud, no el efecto final en OBS u otros programas.
6. Pulsá **Consultar eventos disponibles** (`GetEvents`), seleccioná eventos concretos y elegí **Suscribir eventos seleccionados** (`Subscribe`). No se usa un comodín de suscripción. La interfaz muestra el resultado confirmado por el servidor. Los eventos recibidos se registran, pero no ejecutan acciones ni atajos automáticamente.

### Alcance y verificación

- **Implementado en código:** cliente `ClientWebSocket`, handshake `Hello`, desafío de autenticación documentado, IDs únicos de solicitud, correlación de respuestas, límite de mensaje y lectura fragmentada, `GetActions`, `DoAction`, `GetEvents`, `Subscribe`, estado real, protección DPAPI y cierre al salir.
- **Pruebas automatizadas añadidas:** endpoint, algoritmo de autenticación y prueba de protocolo con servidor WebSocket simulado que envía `Hello` fragmentado, autentica, responde a solicitudes y publica un evento. El servidor simulado no sustituye una validación con Streamer.bot real.
- **Pendiente de validación en Windows real:** comprobar conexión con la versión instalada, contraseña correcta/incorrecta, acciones seleccionadas de bajo riesgo, eventos Twitch habilitados, comportamiento en OBS y PNGTuber Plus. No se afirma integración directa con expresiones de PNGTuber Plus.
- **Limitaciones actuales:** reconexión manual, sin reconexión automática; no hay un EventManager genérico compartido en el Core actual, por lo que el evento se expone mediante el contrato del cliente y se registra para preparar la coordinación de la Orden 4/5.
## Automatizaciones por eventos — implementación en la rama de producto

El espacio **Automatizaciones** coordina eventos de la conexión WebSocket real de Streamer.bot y notificaciones que ya procesa el puente de Twitch EventSub de VTuberBot. No vuelve a ejecutar comandos de chat ni republica cada acción como un evento entrante.

- Las reglas se guardan en las preferencias SQLite existentes bajo `automation.eventActions.rules.v1`; el interruptor global y la autorización de teclas también usan las preferencias existentes. No se crea otra base de datos.
- Las reglas nuevas están desactivadas y las automatizaciones arrancan pausadas si no existe una preferencia previa. El origen de cada regla es explícito.
- Para eventos de Streamer.bot se consulta `GetEvents`; las suscripciones manuales y las requeridas por reglas habilitadas se reconcilian en un solo conjunto. Al editar o borrar reglas se intenta retirar la selección anterior y aplicar la nueva; al reconectar se reconstruye desde SQLite.
- Antes de cada ejecución se comprueba la conexión y el catálogo actual de acciones habilitadas. La confirmación de una acción externa queda vinculada al identificador concreto de destino.
- PNGTuber Plus solo acepta atajos ya registrados y habilitados. El envío automático está desautorizado por defecto y su habilitación advierte que Windows puede cambiar el foco. Un resultado de envío de tecla no confirma un cambio de expresión.
- Las condiciones usan rutas simples de campos JSON y operadores exactos `eq`, `contains` y `present`. No se evalúa código, expresiones C# ni comandos de sistema recibidos en eventos.
- La cola tiene capacidad acotada, el procesamiento se ejecuta fuera del bucle WebSocket, hay cooldown por regla, límites por regla/globales, deduplicación con memoria acotada y límite de profundidad causal. Si falta un ID de proveedor o una marca de tiempo fiable, no se afirma que los eventos equivalentes de Streamer.bot y EventSub se deduplicaron.
- La actividad distingue recepción, saturación, deduplicación, pausa, cooldown/límite, solicitud aceptada, rechazo, resultado desconocido y tecla enviada. La aceptación del servidor no demuestra el efecto posterior en OBS.

**Validación automatizada previa a la auditoría:** Windows CI [run #427](https://github.com/jonhararagi/vtuber/actions/runs/38382027364) en SHA `dc55ca9bb68dfbb5ff35c024931e930c34730fe5` completó restore, compilación Release sin advertencias/errores, 423/423 pruebas y publicación del artefacto win-x64. La corrección posterior de configuración dañada requiere su propio CI sobre el SHA final. **Pendiente manual:** conexión con Streamer.bot instalado, eventos Twitch reales, OBS y PNGTuber Plus. CI no demuestra un efecto visual del avatar.
### Estado de CI de la capa de automatizaciones

Las ejecuciones #380, #390, #397 y #415 fallaron en SHA anteriores; sus causas y correcciones se registran en continuidad. La ejecución de auditoría [run #435](https://github.com/jonhararagi/vtuber/actions/runs/38384321638) validó el SHA `0609de270aa045912f487c5117c429bce8a25b30` con 424/424 pruebas y publicación del ZIP. Para una revisión posterior, verificá siempre el run y artefacto cuyo `head_sha` coincida con el HEAD exacto. La validación manual de aplicaciones externas sigue pendiente.
Windows CI run [#415](https://github.com/jonhararagi/vtuber/actions/runs/38329915472) compiled successfully but failed one of 422 tests due to a fixed delay in an asynchronous assertion. The test was changed to wait for the expected activity with a bounded timeout; this is now verified by run [#423](https://github.com/jonhararagi/vtuber/actions/runs/38330331610), where all 423 tests passed on SHA `2e3fedc7be40f4ac17bc59dc7c3cbbbd8258cf75`.


## Bot social: lectura TTS y conversación local (implementación en validación)

La sección **Bot social** reutiliza el cliente IRC/TLS de Twitch ya existente y `LocalSpeechQueue` / Windows SAPI. No crea una segunda conexión de Twitch ni una segunda cola de audio.

- La lectura de chat está desactivada por defecto. Si no se activa explícitamente «Leer TODOS los mensajes», se limita a menciones y activaciones permitidas; los filtros de comandos, enlaces, longitud y usuarios ignorados siguen aplicándose.
- La voz, velocidad, volumen y salida se eligen en **Voz y audio**. La nueva sección permite encolar una frase de prueba, detener la frase actual y vaciar la cola pendiente.
- Las respuestas automáticas, la publicación en Twitch y la voz de respuestas están desactivadas por defecto y se configuran por separado. Solo se responde a una mención `@Nombre` o al comando configurado (por defecto `!bot`).
- La inferencia utiliza un endpoint compatible con OpenAI Chat Completions servido localmente. Por privacidad, el proveedor rechaza hosts que no sean `localhost` o una IP loopback. No se envía chat a proveedores remotos.
- Para probarlo, instala por separado un servidor local compatible (por ejemplo, `llama.cpp` con su servidor HTTP), descarga y configura un modelo GGUF compatible, indica la URL local y el ID que el servidor publica en `/v1/models`, y usa **Comprobar servidor y modelo**.
- El botón de prueba genera texto local sin publicarlo. Activar respuestas automáticas muestra una confirmación. Configura el login real de la cuenta bot para excluir su propio eco; por defecto se usa el canal configurado como fallback.
- Las colas y los límites son acotados. Una respuesta generada no se considera publicada hasta que el cliente Twitch acepta el envío; esto tampoco confirma que los espectadores la hayan leído.

**Estado de verificación:** el código y las pruebas simuladas no sustituyen la prueba manual con Twitch autenticado, Windows SAPI y un modelo local instalado. No declarar la integración en tiempo real como validada hasta revisar Windows CI del SHA final y realizar esas pruebas manuales.


## Emisiones a Twitch y YouTube desde OBS

La página **OBS** puede configurar la salida RTMPS principal de OBS para Twitch o YouTube mediante `SetStreamServiceSettings`; no necesita un segundo cliente de emisión ni guarda las claves en SQLite. Las claves se protegen mediante DPAPI para el usuario actual de Windows. La aplicación debe estar conectada a OBS WebSocket y la transmisión debe estar detenida para cambiar el destino. Guía paso a paso: [Transmisión a Twitch y YouTube](STREAMING-YOUTUBE-TWITCH.md).

**Emisión simultánea:** la salida nativa de OBS es un destino. La doble salida necesita un complemento multi-RTMP de terceros instalado por el usuario; VTuberBot no lo instala ni controla directamente su panel. Hay que comprobar que el complemento sea compatible con la versión local de OBS. El CI de VTuberBot no prueba una transmisión real a Twitch/YouTube ni la compatibilidad de un plugin externo.

## Verificación física de OBS, voz y avatar

En la página **OBS**, el panel **Prueba guiada real · grabación local, voz y avatar** permite comprobar la cadena completa sin iniciar un directo. Está pensada para ejecutarse con OBS instalado en la misma PC, la conexión WebSocket autenticada y una voz local seleccionada.

1. En **OBS**, conectá la sesión WebSocket al host local guardado. El host escrito debe coincidir con el host guardado; la prueba no funciona contra OBS remoto ni si la sesión no está autenticada.
2. En **Voz y audio**, elegí una voz **VOICEVOX** local con el Engine en ejecución, o habilitá explícitamente Windows SAPI como alternativa. La prueba se niega a usar un proveedor de voz remoto.
3. Antes de continuar, asegurate de que OBS no esté transmitiendo ni grabando y de que la cola de voz esté vacía. Aceptá la confirmación de **Grabar prueba local + reproducir voz**.
4. La aplicación consulta el estado de OBS, inicia solo `StartRecord`, reproduce una frase con la cola local, detiene la grabación y espera a que aparezca un archivo nuevo, válido y con tamaño estable en la carpeta que OBS declara. El directorio se consulta con el request oficial `GetRecordDirectory`. Si existen más de 10.000 grabaciones compatibles en esa carpeta, la prueba se detiene y solicita reducir los candidatos en lugar de acumularlos en memoria.
5. El archivo se abre con el reproductor predeterminado. Comprobá manualmente que se vea el avatar, que la boca cambie, que no haya halo/fondo blanco y que se escuche la frase. Solo responde **Sí** si acabás de verificar imagen y sonido; cualquier otro resultado queda como pendiente. La existencia del archivo y el fin de la cola TTS no demuestran por sí mismos que los altavoces hayan sonado.

La prueba no llama a `StartStream`, no toca claves de transmisión ni modifica escenas/fuentes. Si el inicio de grabación agota el tiempo de espera sin una confirmación, la aplicación no intenta detener una salida cuya pertenencia sea ambigua; revisá el estado en OBS manualmente antes de volver a probar.

## Qué cuenta como evento real de Kick

En **Bots y plataformas → Chat y eventos entrantes de Kick**, la etiqueta **Recepción real pendiente** solo cambia al entrar un mensaje de chat real que haya pasado la verificación de firma y se haya procesado por el consumidor. Aceptar una suscripción, iniciar el listener o recibir la marca de salud HTTPS **no** cuenta como un evento real. La hora que se muestra indica que el mensaje llegó al consumidor; no confirma que una reacción, TTS o respuesta saliente se haya completado.

Para completar esa verificación necesitás una URL pública HTTPS con túnel hacia el receptor local, la URL exacta registrada en Kick Developer, suscripciones aceptadas y scopes OAuth válidos. El programa no crea un túnel ni puede usar tus cuentas reales desde CI. La marca de tiempo de evidencia se persiste como máximo una vez cada 15 segundos durante tráfico intenso, para evitar una escritura SQLite por cada mensaje; en la sesión abierta la interfaz muestra la última hora observada.

**Importante:** las pruebas automatizadas usan servidores locales, datos sintéticos o clientes simulados. No pueden reemplazar una grabación reproducida en tu Windows, una prueba audible con tu dispositivo de salida ni la recepción de un mensaje desde tu propio canal de Kick.
