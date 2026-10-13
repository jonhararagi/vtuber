# Guía del candidato integral de VTuberBot

Esta guía corresponde a la rama `product/complete-vtuber-experience`. Las funciones externas no se consideran conectadas hasta completar la autenticación real.

## Primer inicio y datos

- Ejecutá `VtuberBot.App.exe` en Windows.
- La base SQLite, los logs, preferencias, perfiles y assets importados se guardan bajo `%LocalAppData%\VtuberBot\`.
- El token de Twitch y la contraseña de OBS se cifran con DPAPI para el usuario de Windows actual. No copies archivos de secretos entre usuarios ni los subas a GitHub.
- El paquete portable no debe incluir `config.toml`, base de datos, logs, tokens ni cookies.

## Dashboard, Avatar Studio y OBS overlay
El Dashboard muestra CPU y RAM reales del proceso cada dos segundos mientras está abierto; no presenta valores GPU/FPS inventados.

1. Abrí **Avatar Studio**. Podés crear un perfil vacío, duplicar el actual o cambiar entre perfiles desde la parte superior; cada uno conserva su propia imagen base, capas/transformaciones y sprites de expresión. Eliminar un perfil no borra los archivos de imagen importados, y siempre debe quedar al menos un perfil. **Exportar perfil (.vtuberavatar)** guarda base, capas, transformaciones y expresiones juntos; al importarlo se crea otro perfil sin sobrescribir los existentes.
2. Importá la imagen base del perfil activo.
3. Importá capas PNG/JPG adicionales transparentes. Podés ocultar o eliminar una capa sin borrar el archivo original, cambiar su orden visual y ajustar X/Y, escala y rotación.
4. En **Sprites por expresión**, asigná imágenes con el mismo tamaño para Idle, Talking, Blink, Happy o Surprised. La imagen base se usa como fallback para estados normales sin sprite; para Blink necesitás una imagen explícita de ojos cerrados.
5. En la previsualización, seleccioná una capa visible a la derecha, arrastrala para ajustar X/Y y pulsá **Aplicar y guardar transformación**.
6. Abrí **Mostrar overlay transparente**.
7. En OBS agregá una fuente **Captura de ventana** y seleccioná `VTuberBot Avatar Overlay`.
8. Arrastrá la ventana del overlay para ubicarla y redimensionala con el borde. Doble clic alterna la prioridad Topmost.

El overlay compone la imagen base con capas transformables, reproduce el sprite asignado al estado activo y aplica movimiento respiratorio sutil. El parpadeo automático requiere un sprite Blink de ojos cerrados asignado: sin ese archivo, no se atenúa la imagen completa ni se simula un parpadeo falso. “Reducir movimiento” suspende el parpadeo y la respiración. El micrófono activa Talking con un detector acústico configurable. Esto no equivale a un rig con huesos/física ni a lip-sync por fonemas; usá sprites de expresión con el mismo lienzo para evitar desalineaciones.

## Avatar externo con PNGTuber Plus

Usá **Avatar externo** para configurar una copia de PNGTuber Plus que ya esté instalada. Esta opción es independiente de los perfiles y capas de **Avatar Studio** nativo; no importa ni convierte archivos .vtuberavatar.

1. Seleccioná el ejecutable dentro de la carpeta extraída. Según el paquete distribuido, el .exe, el .pck y las bibliotecas .dll deben permanecer juntos. No ejecutes desde dentro del ZIP.
2. Abrí la aplicación y completá el asistente paso a paso. La ruta y las confirmaciones se guardan en SQLite y se recuperan al reiniciar VTuberBot.
3. Importá PNG transparentes dentro de PNGTuber Plus, configurá el micrófono allí, prepara sprites/expresiones y guarda el avatar con las funciones propias de PNGTuber Plus.
4. En OBS, prueba una captura de ventana con transparencia. Si el método de captura no conserva el canal alfa, usa un fondo verde sólido y aplica el filtro Chroma Key a esa fuente.
5. Para los atajos, crea el registro en VTuberBot y asigna la misma tecla dentro de PNGTuber Plus. “Enviado, no verificado” significa que Windows envió la tecla; no confirma un cambio de expresión.

El portal pngtuber.cc/es se presenta en su página “About” como un sitio independiente de fans. Consulta su guía de distribución y el [código fuente público de PNGTuber Plus](https://github.com/kaiakairos/PNGTuber-Plus). La versión que anuncia el portal no demuestra qué versión tienes instalada. VTuberBot no descarga ni instala binarios de terceros y no declara una API/IPC/WebSocket de PNGTuber Plus que no haya podido verificar.

Descarga del creador: [PNGTuber Plus en itch.io](https://kaiakairos.itch.io/pngtuber-plus). El portal [pngtuber.cc/es](https://pngtuber.cc/es/) ofrece guía en español, pero se identifica como un sitio independiente.

Guía ampliada: [Avatar externo: PNGTuber Plus](PNGTUBER-PLUS-AVATAR-SETUP.md).

## Twitch: OAuth y chat real

1. Creá una aplicación en la [consola de desarrolladores de Twitch](https://dev.twitch.tv/console/apps).
2. Registrá exactamente esta URL de redirección: `http://localhost:17563/callback/`.
3. Copiá el **Client ID** de la aplicación en **Twitch y chat**.
4. Escribí el nombre de la cuenta/canal que corresponde al usuario autorizado y elegí **Autorizar con Twitch**. Se abre el navegador; aceptá los permisos `chat:read`, `chat:edit`, `moderator:read:followers`, `moderator:manage:banned_users`, `moderator:manage:chat_messages`, `channel:read:subscriptions` y `channel:read:redemptions`. Para timeout/ban/borrar mensajes, la cuenta autorizada debe ser el broadcaster o un moderador del canal. Si ya habías autorizado antes de agregar `moderator:manage:chat_messages`, repetí OAuth para aceptar el nuevo permiso.
5. Volvé a la app y elegí **Conectar**. El access token, refresh token, scopes y expiración se guardan cifrados con DPAPI para el usuario actual. Mientras Chat o EventSub estén activos, VTuberBot valida la sesión cada cinco minutos y renueva el access token antes de expirar cuando Twitch proporciona un refresh token; luego restaura las conexiones activas. Si Twitch revoca tanto el token como el refresh token, tendrás que volver a autorizar. El estado conectado solo se informa tras la confirmación de Twitch. Si el socket se corta por una caída temporal, el cliente reintenta automáticamente con espera creciente (hasta 30 segundos); si Twitch rechaza el token, detiene el reintento y pide volver a autorizar.
6. Probá enviar un mensaje o usar los comandos integrados `!puntos`, `!top`, `!jugar dado alto 0` y `!entrar`. `!hola` y otros nombres solo existen si los creás como comandos personalizados.

Los mensajes del chat comparten el registro local de comandos; la actividad otorga puntos con un intervalo anti-farming y `!entrar` registra participantes únicos en el sorteo abierto. Los errores de autenticación no se tratan como conexión exitosa. Para alertas oficiales, elegí **Conectar EventSub (alertas)** después de autorizar con los scopes ampliados. La app intenta registrar follows, suscripciones, regalos de subs, redenciones de puntos de canal y raids hacia tu canal. Las notificaciones aparecen en el registro del chat y en Actividad/diagnóstico; si hay una imagen base importada, también se muestra una tarjeta temporal en el overlay de OBS. Activá **Leer alertas de Twitch con la voz local** para encolarlas con prioridad alta; las alertas no interrumpen automáticamente la frase que ya está sonando.

## Comandos, comunidad y sorteos

- **Comandos:** creá respuestas con variables `{user}`, `{points}`, `{roll}`, `{args}` y `{message}`. Podés añadir hasta 20 alias separados por coma; cada alias se guarda y ejecuta como el mismo comando, y los conflictos con nombres/alias existentes se rechazan. También podés restringir su uso a Todos, Suscriptores, VIP, Moderadores o solo Broadcaster; los roles se identifican con etiquetas oficiales de Twitch y los comandos rechazados no consumen cooldown. El cooldown puede ser global (un intervalo para todo el chat) o por usuario; ambas modalidades y sus marcas se guardan en SQLite. La previsualización usa el texto aún no guardado y no consume cooldown ni envía mensajes.
- **Comunidad y puntos:** los perfiles, el ranking, los ajustes manuales y el ledger de puntos se conservan en la base local. La actividad del chat puede otorgar puntos automáticamente. Los comandos integrados `!puntos` y `!top` consultan el saldo y los cinco primeros del ranking desde Twitch o la prueba local de Comandos.
- **Sorteos:** creá un sorteo, registrá participantes manualmente o mediante `!entrar` en el chat y cerralo con selección uniforme usando el generador criptográfico del sistema. Las entradas duplicadas se descartan y un sorteo cerrado no puede volver a sortearse. Si Twitch está desconectado, crear/cerrar/cancelar modifica el historial local y el estado indica explícitamente que no se publicó un anuncio remoto.
- **Minijuegos:** Duelo de moneda (Cara/Cruz), Ruleta europea (Rojo/Negro/Verde, con 0 verde) y Dado (Bajo 1–3/Alto 4–6) tienen reglas distintas y elecciones visibles. En el chat también se puede usar `!jugar moneda cara 10`, `!jugar ruleta rojo 10` o `!jugar dado alto 10`; una apuesta `0` es práctica gratis. Las apuestas son de 0 a 10.000 puntos: 0 juega en práctica sin modificar el saldo; apostar requiere tener saldo suficiente. Una victoria de moneda/dado o rojo/negro en ruleta paga 2× la apuesta total; el verde 0 paga 36×. Si perdés, se descuenta la apuesta y no hay premio. Débito, premio, ledger e historial se guardan en una transacción SQLite para que un fallo no deje el saldo a medias. No requieren Twitch conectado; la publicación es opcional cuando ya está conectado.
- **Moderación:** las reglas regex se prueban localmente. La moderación automática queda desactivada por defecto y solo se habilita tras confirmación. Cada regla puede ser `flag` (registro), `warn` (aviso en chat), `delete` (elimina el mensaje exacto), `timeout` (10 minutos) o `ban` (permanente). Si se habilita la opción global, se limita a una acción no informativa por mensaje y una acción por usuario/regla cada cinco minutos.
- Las acciones manuales permiten timeout de duración elegida, ban permanente y retirar un ban/timeout, con confirmación visible antes de afectar el chat real. Si agregaste scopes después de autorizar, volvé a completar OAuth. Los errores de permiso se muestran como fallos; nunca se simula el éxito.
- La eliminación automática de mensajes usa el endpoint oficial [Delete Chat Messages](https://dev.twitch.tv/docs/api/reference#delete-chat-messages), necesita `moderator:manage:chat_messages` y solo envía el ID de mensaje exacto recibido desde las etiquetas IRC; nunca hace una solicitud vacía que pudiera borrar todo el chat.
- **Automatizaciones:** los mensajes programados respetan un intervalo mínimo de cinco minutos y solo se envían cuando Twitch está conectado. Si está desconectado, la omisión queda registrada.

## Voz y audio

- Seleccioná el micrófono disponible e iniciá la captura real.
- Ajustá el umbral acústico y el tiempo de retención para que el estado Talking no parpadee por pequeñas pausas.
- El detector local suaviza el nivel de señal y activa el cue Talking del overlay; no reconoce palabras ni equivale a lip-sync por fonemas.
- La cola TTS procesa frases una por una en un hilo STA de fondo para no congelar el panel. Las prioridades Alta/Normal/Baja afectan las frases que todavía esperan. **Detener voz actual** solicita cancelar la frase activa; **Vaciar frases pendientes** solo elimina las frases que esperan y no interrumpe la actual.
- **Actualizar lista de voces** muestra los motores SAPI instalados en Windows. Podés elegir una voz, ajustar velocidad (-10 a 10) y volumen (0–100); las alertas habladas respetan las preferencias guardadas. No descarga ni incluye un modelo de voz anime por sí sola.
- **Encolar voz** usa las voces locales instaladas mediante Windows SAPI; no necesita una API de IA remota.
- El lip-sync facial por fonemas/visemas y el enrutamiento avanzado de audio todavía están pendientes.

## OBS WebSocket

1. En OBS, habilitá el servidor WebSocket (normalmente puerto `4455`) y configurá una contraseña.
2. En **OBS**, ingresá host, puerto y contraseña, y elegí **Conectar y autenticar**.
3. Al completar el handshake WebSocket v5, podés consultar escenas y cambiar la escena del programa.

Si OBS está cerrado o la contraseña no coincide, la app sigue funcionando y muestra el error sin fingir conexión. Si una conexión ya autenticada se corta de forma transitoria, la app reintenta cada vez con una espera creciente limitada a 30 segundos; la desconexión manual detiene los reintentos. La reconexión real debe confirmarse en un OBS configurado.

## Telegram Web

La página intenta cargar Telegram Web mediante WebView2. Si el Runtime no está instalado o falla la inicialización, usá **Abrir en navegador**. Las cookies permanecen en el perfil WebView2/navegador, no en el repositorio ni en la base del bot.

## Diagnóstico

La página **Actividad y diagnóstico** muestra el registro local y permite exportar un informe de texto. El informe excluye tokens, contraseñas y cookies. Revisá siempre el estado visible antes de asumir que un servicio externo está conectado.

## Limitaciones pendientes conocidas

- **Twitch/EventSub:** el cliente reconecta ante pérdidas y usa alertas visuales/TTS. Ante causas transitorias conocidas de red/servidor intenta recuperar una suscripción individual, con límite de un intento por tipo cada diez minutos; revocaciones por autorización, rol de moderador, usuario inexistente o versión retirada requieren la acción indicada en el diagnóstico. Siguen pendientes triggers avanzados y probar el ciclo completo con una cuenta real del broadcaster/moderador.
- **Avatar:** la composición de sprites, las capas transformables, los sprites por estado y el parpadeo ligero ya existen. Sigue pendiente el rig 2.5D con pivotes/parentado, física por huesos, deformación, seguimiento facial y lip-sync por fonemas/visemas.
- **Audio/voz:** el medidor de señal y detector acústico con umbral/retención, la cola TTS con prioridad y la cancelación de la frase activa están implementados. Sigue pendiente reconocimiento de habla (STT), enrutamiento avanzado y lip-sync real; la voz disponible depende de los motores SAPI instalados en Windows.
- **Moderación:** timeout/ban/borrado manual con confirmación y acciones automáticas opt-in por regla existen. Sigue pendiente un simulador/prueba segura de reglas con vista previa sobre mensajes de ejemplo, auditoría más detallada y validación end-to-end de scopes/permisos en un canal real.
- **Persistencia/entrega:** ya existe backup/restauración guiados de SQLite con integridad comprobada y copia de emergencia pre-restauración. La aceptación final aún requiere validación manual global de una build actual con Twitch/OBS/Telegram reales; el backup de SQLite no incluye assets, cookies ni credenciales DPAPI.
- **Integraciones externas:** CI prueba compilación, pruebas y publicación del paquete Windows x64, pero no autentica Twitch, OBS ni Telegram del usuario ni demuestra el comportamiento de una sesión de streaming real.

Estas limitaciones se mantienen explícitas: no se marca el producto como definitivo solo por compilar o por tener una página de interfaz.


### Biblioteca de sonidos

En **Voz y audio**, importá archivos WAV o MP3 propios. VTuberBot los copia a la carpeta local `sounds`, guarda la biblioteca en SQLite y permite reproducir/detener un efecto o quitarlo de la lista sin borrar el archivo. Los sonidos usan la salida de audio predeterminada de Windows; la selección de salida SAPI es independiente y se aplica a la voz sintetizada. Las copias de seguridad SQLite no incluyen los archivos de audio.


### Descargar y ejecutar el paquete de Windows

1. Abrí [Windows CI](https://github.com/jonhararagi/vtuber/actions/workflows/windows-ci.yml?query=branch%3Aproduct%2Fcomplete-vtuber-experience).
2. Entrá en la ejecución de la rama `product/complete-vtuber-experience` cuyo **Head SHA sea idéntico al commit que vas a probar**.
3. En **Artifacts**, descargá `VTuberBot-Product-Candidate-win-x64-<SHA>` y extraé el ZIP; no lo ejecutes dentro del ZIP.
4. Abrí `VtuberBot.App.exe` y consultá `BUILD-INFO.txt` para confirmar SHA, runtime y fecha.
5. El paquete es self-contained para Windows x64, no una release oficial. El artefacto temporal tiene retención de 14 días; las integraciones de Streamer.bot, PNGTuber Plus, Twitch y OBS requieren su propia configuración y no vienen incluidas en el ZIP.

## Validación global de un candidato

Cuando se solicite la validación integrada, seguí una sola vez la lista [Validación global del candidato de producto](PRODUCT-CANDIDATE-MANUAL-VALIDATION.md). Descargá únicamente el artefacto de la ejecución Windows CI cuyo **Head SHA coincida exactamente** con el HEAD de la rama; no uses una descarga histórica como sustituto. Marcá los servicios que no puedas probar por falta de cuenta o dispositivo como **BLOQUEADOS**, no como aprobados. No hace falta ejecutar comandos Git ni compilar localmente.

## Aplicaciones externas

Configuración de rutas, apertura y estados de PNGTuber Plus y Streamer.bot: [Centro de integraciones externas](EXTERNAL-INTEGRATIONS.md).


## Streamer.bot por WebSocket

1. En Streamer.bot, abrí **Servers/Clients → WebSocket Server** y comprobá la dirección, el puerto, el endpoint y si se exige autenticación. La documentación oficial está en https://docs.streamer.bot/api/websocket/guide/configuration.
2. En VTuberBot, entrá a **Streamer.bot** en el menú lateral. Usá los valores por defecto `127.0.0.1:8080/` o copiá los que muestra tu instancia.
3. Marcá autenticación e introducí la contraseña si el servidor la exige. Al guardarla, VTuberBot usa DPAPI de Windows para protegerla para el usuario actual. No compartas el archivo cifrado entre usuarios.
4. Pulsá **Conectar / probar**. Si el estado es Conectado, el cliente completó el handshake WebSocket y autenticación solicitada. El proceso de Streamer.bot abierto por sí solo no es prueba de conexión.
5. **Consultar / actualizar acciones** consulta el catálogo real. Seleccioná una acción y verificá el ID antes de confirmar su ejecución. No pruebes acciones que modifiquen OBS, moderen usuarios o tengan efectos destructivos sin comprender su configuración.
6. **Consultar eventos disponibles** obtiene los eventos admitidos por la instancia. Seleccioná los que quieras y pulsá **Suscribir eventos seleccionados**. Tras reconectar, repetí la suscripción; los eventos recibidos no ejecutan acciones automáticamente.

El estado `status=ok` de `DoAction` confirma la respuesta de Streamer.bot, no que OBS o el avatar hayan realizado el efecto final. Para un servidor remoto, tené presente que `ws://` no cifra el tráfico. VTuberBot no cambia el firewall ni abre puertos automáticamente.

La prueba automatizada de protocolo utiliza un servidor simulado; antes de considerar validada la integración completa, hace falta probar la conexión y los eventos contra tu instancia real de Streamer.bot en Windows, junto con OBS y PNGTuber Plus.
## Automatizaciones por eventos (candidato de producto)

1. Abrí **Automatizaciones** y mantené activada la pausa global mientras configurás.
2. Elegí una fuente. Para Streamer.bot, conectate desde **Streamer.bot** y consultá el catálogo de eventos reales; seleccioná una categoría y un tipo que devuelva `GetEvents`. Para `VTuberBot.EventSub`, el tipo debe haberse observado en una notificación real procesada por VTuberBot.
3. Consultá los destinos. Para Streamer.bot se muestran acciones habilitadas del catálogo real; para PNGTuber Plus se muestran solo atajos existentes, registrados y habilitados.
4. Definí el nombre, las condiciones opcionales (una por línea: `campo|eq|valor`, `campo|contains|texto` o `campo|present|`), cooldown y límite por minuto.
5. Guardá la regla. Una regla nueva nace desactivada. Para habilitar una regla que solicita una acción externa, confirmá el destino identificado cuando la aplicación lo solicite. La confirmación se invalida si cambiás el identificador de la acción.
6. Las suscripciones necesarias de reglas habilitadas se combinan con la selección manual de **Streamer.bot**. La interfaz informa si la reconciliación o el servidor no confirmaron la operación.
7. Consultá la actividad para distinguir un evento recibido de una regla omitida, una solicitud aceptada/rechazada, un timeout/desconexión con resultado desconocido o una tecla enviada.

**Atención con PNGTuber Plus:** habilitar el envío automático de teclas requiere una autorización separada y explícita. Windows puede poner PNGTuber Plus en primer plano y quitar el foco a OBS, un juego u otra aplicación. No se envían teclas al guardar reglas, conectar Streamer.bot ni iniciar VTuberBot. El adaptador solo informa que Windows aceptó el envío; no puede confirmar el cambio de sprite/expresión.

**Seguridad y límites:** la pausa global impide automatizaciones disparadas por eventos, pero no sustituye las acciones manuales existentes. Se aplican límites por regla y globales y una cola acotada; en caso de saturación algunos eventos se descartan y se registra una advertencia. Cuando los proveedores no comparten un identificador común, no puede garantizarse deduplicación entre EventSub y Streamer.bot; evitá crear reglas equivalentes en ambas fuentes.
### Nota de validación de automatizaciones

La revisión de código de la auditoría final, SHA `0609de270aa045912f487c5117c429bce8a25b30`, está validada por [Windows CI #435](https://github.com/jonhararagi/vtuber/actions/runs/38384321638): restore correcto, build Release con 0 advertencias/errores, 424/424 pruebas y publicación del artefacto win-x64 del mismo SHA. La corrección hace que una configuración de reglas dañada fuerce la pausa global y añade pruebas para reglas inválidas y JSON corrupto. El CI valida código, tests y paquete, no una sesión real con servicios externos. Las pruebas manuales con Streamer.bot, Twitch, OBS y PNGTuber Plus siguen pendientes.


## Bot social: leer el chat y conversar con IA local

Abrí **Bot social** desde la navegación principal.

### Leer mensajes con voz local
1. En **Voz y audio**, actualizá la lista de voces instaladas y elegí voz, velocidad, volumen y salida; guardá los ajustes.
2. En **Bot social**, activá **Activar lectura de chat**.
3. Por defecto no se narra todo el chat: se priorizan menciones o activaciones explícitas. Configurá el máximo de caracteres, intervalo, capacidad de cola y usuarios ignorados.
4. Los filtros de enlaces y comandos vienen activos. La opción **Leer TODOS los mensajes** requiere una decisión explícita y muestra una advertencia de moderación/privacidad.
5. Usá **Encolar frase de prueba** para probar manualmente la cola. **Detener voz actual** y **Vaciar cola de voz** no desconectan Twitch.

### Configurar la personalidad y la IA local
1. Instalá por separado un runtime local compatible con la API de chat de OpenAI (por ejemplo, el servidor HTTP de `llama.cpp`) y un modelo compatible. VTuberBot no descarga pesos ni incluye modelos en el ZIP.
2. Dejá el servidor escuchando en `127.0.0.1`, por ejemplo `http://127.0.0.1:8080/v1`. Este proveedor rechaza destinos remotos por privacidad.
3. Configurá el ID del modelo que publica `/v1/models`, el idioma, tono, personalidad, timeout y longitud máxima.
4. Pulsá **Guardar configuración social** y después **Comprobar servidor y modelo**. Una comprobación correcta demuestra que el endpoint local responde, no que la personalidad sea perfecta.
5. Usá **Generar respuesta de prueba (privada)** para probar sin publicar en Twitch. **Cancelar generación activa** solicita cancelar la inferencia.

### Habilitar conversación en directo
- El modo conversacional empieza desactivado. Habilitá **Habilitar respuestas automáticas** solo después de configurar y probar el proveedor.
- El bot responde únicamente cuando lo mencionan por el nombre configurado o cuando se usa el comando de activación (por defecto `!bot`). No responde a todos los mensajes.
- Configurá cooldown global, cooldown por usuario y límite por minuto. El texto de chat es contenido no confiable: nunca se interpreta como código ni selecciona acciones externas.
- **Publicar las respuestas generadas en el chat de Twitch** y **Encolar también las respuestas ... en la voz local** son opciones independientes. Para publicar se requiere la conexión Twitch autenticada.
- Configurá el **login real de la cuenta bot** para que sus propios mensajes no creen un bucle de respuesta.

La configuración se guarda en SQLite. Los errores de proveedor, timeout, cola llena y desconexión se informan en el estado de la aplicación. Una respuesta generada localmente no significa que se haya publicado; el envío aceptado por Twitch tampoco confirma que el público haya oído o leído el mensaje.
