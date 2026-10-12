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

## Twitch: OAuth y chat real

1. Creá una aplicación en la [consola de desarrolladores de Twitch](https://dev.twitch.tv/console/apps).
2. Registrá exactamente esta URL de redirección: `http://localhost:17563/callback/`.
3. Copiá el **Client ID** de la aplicación en **Twitch y chat**.
4. Escribí el nombre de la cuenta/canal que corresponde al usuario autorizado y elegí **Autorizar con Twitch**. Se abre el navegador; aceptá los permisos `chat:read`, `chat:edit`, `moderator:read:followers`, `moderator:manage:banned_users`, `moderator:manage:chat_messages`, `channel:read:subscriptions` y `channel:read:redemptions`. Para timeout/ban/borrar mensajes, la cuenta autorizada debe ser el broadcaster o un moderador del canal. Si ya habías autorizado antes de agregar `moderator:manage:chat_messages`, repetí OAuth para aceptar el nuevo permiso.
5. Volvé a la app y elegí **Conectar**. El access token, refresh token, scopes y expiración se guardan cifrados con DPAPI para el usuario actual. Mientras Chat o EventSub estén activos, VTuberBot valida la sesión cada cinco minutos y renueva el access token antes de expirar cuando Twitch proporciona un refresh token; luego restaura las conexiones activas. Si Twitch revoca tanto el token como el refresh token, tendrás que volver a autorizar. El estado conectado solo se informa tras la confirmación de Twitch. Si el socket se corta por una caída temporal, el cliente reintenta automáticamente con espera creciente (hasta 30 segundos); si Twitch rechaza el token, detiene el reintento y pide volver a autorizar.
6. Probá enviar un mensaje o usar comandos como `!hola`, `!puntos`, `!dado` y `!entrar`.

Los mensajes del chat comparten el registro local de comandos; la actividad otorga puntos con un intervalo anti-farming y `!entrar` registra participantes únicos en el sorteo abierto. Los errores de autenticación no se tratan como conexión exitosa. Para alertas oficiales, elegí **Conectar EventSub (alertas)** después de autorizar con los scopes ampliados. La app intenta registrar follows, suscripciones, regalos de subs, redenciones de puntos de canal y raids hacia tu canal. Las notificaciones aparecen en el registro del chat y en Actividad/diagnóstico; si hay una imagen base importada, también se muestra una tarjeta temporal en el overlay de OBS. Activá **Leer alertas de Twitch con la voz local** para encolarlas con prioridad alta; la voz en curso no se interrumpe.

## Comandos, comunidad y sorteos

- **Comandos:** creá respuestas con variables `{user}`, `{points}`, `{roll}`, `{args}` y `{message}`. Podés añadir hasta 20 alias separados por coma; cada alias se guarda y ejecuta como el mismo comando, y los conflictos con nombres/alias existentes se rechazan. También podés restringir su uso a Todos, Suscriptores, VIP, Moderadores o solo Broadcaster; los roles se identifican con etiquetas oficiales de Twitch y los comandos rechazados no consumen cooldown. El cooldown puede ser global (un intervalo para todo el chat) o por usuario; ambas modalidades y sus marcas se guardan en SQLite. La previsualización usa el texto aún no guardado y no consume cooldown ni envía mensajes.
- **Comunidad y puntos:** los perfiles, el ranking, los ajustes manuales y el ledger de puntos se conservan en la base local. La actividad del chat puede otorgar puntos automáticamente.
- **Sorteos:** creá un sorteo, registrá participantes manualmente o mediante `!entrar` en el chat y cerralo con selección uniforme usando el generador criptográfico del sistema. Las entradas duplicadas se descartan y un sorteo cerrado no puede volver a sortearse.
- **Minijuegos:** Duelo de moneda (Cara/Cruz), Ruleta europea (Rojo/Negro/Verde, con 0 verde) y Dado (Bajo 1–3/Alto 4–6) tienen reglas distintas y elecciones visibles. Las apuestas son de 0 a 10.000 puntos: 0 juega en práctica sin modificar el saldo; apostar requiere tener saldo suficiente. Una victoria de moneda/dado o rojo/negro en ruleta paga 2× la apuesta total; el verde 0 paga 36×. Si perdés, se descuenta la apuesta y no hay premio. Débito, premio, ledger e historial se guardan en una transacción SQLite para que un fallo no deje el saldo a medias. No requieren Twitch conectado; la publicación es opcional cuando ya está conectado.
- **Moderación:** las reglas regex se prueban localmente. La moderación automática queda desactivada por defecto y solo se habilita tras confirmación. Cada regla puede ser `flag` (registro), `warn` (aviso en chat), `delete` (elimina el mensaje exacto), `timeout` (10 minutos) o `ban` (permanente). Si se habilita la opción global, se limita a una acción no informativa por mensaje y una acción por usuario/regla cada cinco minutos.
- Las acciones manuales permiten timeout de duración elegida, ban permanente y retirar un ban/timeout, con confirmación visible antes de afectar el chat real. Si agregaste scopes después de autorizar, volvé a completar OAuth. Los errores de permiso se muestran como fallos; nunca se simula el éxito.
- La eliminación automática de mensajes usa el endpoint oficial [Delete Chat Messages](https://dev.twitch.tv/docs/api/reference#delete-chat-messages), necesita `moderator:manage:chat_messages` y solo envía el ID de mensaje exacto recibido desde las etiquetas IRC; nunca hace una solicitud vacía que pudiera borrar todo el chat.
- **Automatizaciones:** los mensajes programados respetan un intervalo mínimo de cinco minutos y solo se envían cuando Twitch está conectado. Si está desconectado, la omisión queda registrada.

## Voz y audio

- Seleccioná el micrófono disponible e iniciá la captura real.
- Ajustá el umbral acústico y el tiempo de retención para que el estado Talking no parpadee por pequeñas pausas.
- El detector local suaviza el nivel de señal y activa el cue Talking del overlay; no reconoce palabras ni equivale a lip-sync por fonemas.
- La cola TTS procesa frases una por una en un hilo STA de fondo para no congelar el panel. Las prioridades Alta/Normal/Baja afectan las frases que todavía esperan; limpiar pendientes no interrumpe la frase en curso.
- **Actualizar lista de voces** muestra los motores SAPI instalados en Windows. Podés elegir una voz, ajustar velocidad (-10 a 10) y volumen (0–100); las alertas habladas respetan las preferencias guardadas. No descarga ni incluye un modelo de voz anime por sí sola.
- **Encolar voz** usa las voces locales instaladas mediante Windows SAPI; no necesita una API de IA remota.
- El lip-sync facial por fonemas/visemas y el enrutamiento avanzado de audio todavía están pendientes.

## OBS WebSocket

1. En OBS, habilitá el servidor WebSocket (normalmente puerto `4455`) y configurá una contraseña.
2. En **OBS**, ingresá host, puerto y contraseña, y elegí **Conectar y autenticar**.
3. Al completar el handshake WebSocket v5, podés consultar escenas y cambiar la escena del programa.

Si OBS está cerrado o la contraseña no coincide, la app sigue funcionando y muestra el error sin fingir conexión.

## Telegram Web

La página intenta cargar Telegram Web mediante WebView2. Si el Runtime no está instalado o falla la inicialización, usá **Abrir en navegador**. Las cookies permanecen en el perfil WebView2/navegador, no en el repositorio ni en la base del bot.

## Diagnóstico

La página **Actividad y diagnóstico** muestra el registro local y permite exportar un informe de texto. El informe excluye tokens, contraseñas y cookies. Revisá siempre el estado visible antes de asumir que un servicio externo está conectado.

## Limitaciones pendientes conocidas

- **Twitch/EventSub:** el cliente reconecta ante pérdidas y usa alertas visuales/TTS; sigue pendiente recuperar automáticamente suscripciones individuales revocadas, diseñar triggers avanzados y probar el ciclo completo con una cuenta real del broadcaster/moderador.
- **Avatar:** la composición de sprites, las capas transformables, los sprites por estado y el parpadeo ligero ya existen. Sigue pendiente el rig 2.5D con pivotes/parentado, física por huesos, deformación, seguimiento facial y lip-sync por fonemas/visemas.
- **Audio/voz:** el medidor de señal y detector acústico con umbral/retención y la cola TTS con prioridad están implementados. Sigue pendiente reconocimiento de habla (STT), cancelación/enrutamiento avanzado y lip-sync real; la voz disponible depende de los motores SAPI instalados en Windows.
- **Moderación:** timeout/ban/borrado manual con confirmación y acciones automáticas opt-in por regla existen. Sigue pendiente un simulador/prueba segura de reglas con vista previa sobre mensajes de ejemplo, auditoría más detallada y validación end-to-end de scopes/permisos en un canal real.
- **Persistencia/entrega:** ya existe backup/restauración guiados de SQLite con integridad comprobada y copia de emergencia pre-restauración. La aceptación final aún requiere validación manual global de una build actual con Twitch/OBS/Telegram reales; el backup de SQLite no incluye assets, cookies ni credenciales DPAPI.
- **Integraciones externas:** CI prueba compilación, pruebas y publicación del paquete Windows x64, pero no autentica Twitch, OBS ni Telegram del usuario ni demuestra el comportamiento de una sesión de streaming real.

Estas limitaciones se mantienen explícitas: no se marca el producto como definitivo solo por compilar o por tener una página de interfaz.
