# Guía de VTuberBot para principiantes

Esta guía está escrita para quien empieza a usar un programa VTuber. También está disponible dentro de la aplicación, en **Guía fácil**, mediante pestañas para cada tema. No hace falta saber programar.

## Cómo repartir el trabajo

- **VTuberBot**: controla el avatar PNG, la voz local, el comportamiento compatible del bot, las conexiones y las operaciones disponibles de OBS.
- **OBS Studio**: captura y mezcla las fuentes de vídeo/audio y es el motor que graba o transmite.
- **Twitch y Kick**: son los servicios de chat; cada uno conserva su propia cuenta, autorización y funciones exclusivas.
- **Streamer.bot**: es un puente opcional, no un requisito para usar el flujo básico. Evitá duplicar en él y en VTuberBot la misma respuesta.

## Orden recomendado

1. Abrí **PNGTuber 2D** e importá un PNG propio. Comprobá la posición, la transparencia y las imágenes de reposo/hablando.
2. Abrí **Voz y audio**, elegí micrófono y salida y probá la voz. VOICEVOX Engine y sus voicebanks se instalan por separado y sus licencias deben respetarse.
3. Abrí **OBS** y habilitá WebSocket con contraseña. El puerto por defecto suele ser 4455. Confirmá la conexión antes de ejecutar acciones reales.
4. Abrí **Bots y plataformas** y conectá solo una plataforma para empezar. Probá en un canal controlado por vos antes de habilitar automatizaciones.
5. Si algo falla, revisá **Actividad y diagnóstico** y exportá un diagnóstico sin secretos.

## Qué hace cada sección

### PNGTuber 2D
Usa tu ilustración PNG y permite asignar sprites de boca y movimiento global simple. Los estados **Idle** y **Talking** no son lip-sync por fonemas. Si el PNG de base ya tiene una boca, la boca dibujada seguirá debajo del sticker; el editor no incluye una herramienta para borrarla, así que prepará una copia transparente sin boca con un editor de imagen.

### Voz y audio
Selecciona micrófono, salida y opciones de síntesis. VOICEVOX se ejecuta en el equipo y no viene dentro del paquete. Las voces estándar están orientadas principalmente al japonés; el español natural no está garantizado.

### OBS y transmisiones
VTuberBot envía acciones compatibles a OBS mediante OBS WebSocket. OBS sigue siendo responsable de componer y transmitir el directo. Nunca compartas la contraseña WebSocket o la clave de emisión. Hacé una grabación local antes de salir en vivo.

### Bots y plataformas
- **Twitch**: hay chat IRC autenticado y EventSub. En **Moderación** podés aplicar timeout, ban permanente, quitar sanción y eliminar un mensaje por su ID. La moderación automática empieza desactivada; si la activás, las reglas habilitadas pueden ejecutar acciones reales. Probá primero en un canal controlado y revisá cuidadosamente cada patrón.
- **Kick — acciones manuales**: **Moderación** contiene controles para timeout, ban permanente, retirar sanción y borrar un mensaje por su ID de API. Requiere los scopes `moderation:ban` y `moderation:chat_message:manage` y una cuenta con permisos en el canal. Volvé a autorizar Kick desde **Bots y plataformas** después de añadir esos scopes. Para sanciones se pide el ID numérico del usuario; para borrar, el ID real del mensaje.
- **Kick — eventos entrantes**: en **Bots y plataformas → Kick → Chat y eventos entrantes** guardá tu URL HTTPS pública terminada en `/kick-webhook`, iniciá el receptor local y suscribí los eventos. Antes, creá un túnel HTTPS (por ejemplo, Cloudflare Tunnel o ngrok) que reenvíe esa ruta a `http://127.0.0.1:17565/kick-webhook`; registrá esa misma URL en Kick Developer y activá **Enable Webhooks**. VTuberBot no crea el túnel ni abre puertos públicos; la app escucha únicamente en loopback.
- **Cómo probar el webhook**: mantené el túnel activo, iniciá el receptor local y usá **Suscribir chat y eventos**. La aplicación verifica la firma de Kick, la antigüedad y los IDs duplicados antes de procesar mensajes. El estado indica si Kick aceptó suscripciones, pero solo ver un mensaje entrante en VTuberBot confirma que llegó un evento real. Si cerrás VTuberBot, el receptor local se detiene aunque las suscripciones remotas puedan seguir activas; podés cancelarlas con **Cancelar suscripciones registradas**.
- **Reglas automáticas de Kick**: permanecen desactivadas por defecto. Activá el interruptor independiente en **Moderación** solo después de probar recepción real con reglas `flag` (solo registro). Si las activás, reglas `warn`, `delete`, `timeout` o `ban` pueden afectar el chat real. Broadcaster y moderadores se protegen de sanciones automáticas y hay un cooldown por usuario/regla; revisá con cuidado los patrones antes de activar acciones.
- **Reglas y voz**: las reglas guardadas se consumen desde el chat de Twitch y, tras configurar correctamente los webhooks, desde mensajes Kick con firma válida. El guardado de una regla por sí solo no demuestra que un evento llegue.
- **Streamer.bot**: integración opcional. Elegí un solo lugar como autoridad para cada regla para evitar mensajes o acciones duplicadas.

### Comandos locales
La pantalla Comandos sirve para acciones locales. No uses que un comando se ejecutó en esa página como prueba de que el mismo nombre funciona desde el chat de Twitch. Las reglas vinculadas al chat están en el panel Bots y plataformas.

### Ajustes, privacidad y datos
Las credenciales admitidas por la app se protegen con DPAPI para el usuario actual de Windows. No pegues tokens, Client Secret, contraseña WebSocket ni clave de emisión en GitHub, capturas o logs. En Kick, dejar vacío el campo Client Secret al guardar otra configuración conserva el secreto ya guardado; para eliminarlo, usá el botón específico de eliminación y confirmá la acción. Esa eliminación local no borra los tokens OAuth ni revoca el permiso remoto. Para revocar el acceso, entrá en el sitio de la plataforma y retiralo allí.

## Solución de problemas sin reinstalar

### Twitch no conecta
1. Revisá Internet y el nombre real del usuario que inicia sesión.
2. Comprobá por separado el login del bot y el canal destino.
3. Revisá que la autorización conserve los permisos necesarios; si se revocaron, volvé a autorizar desde el panel.
4. Mirá Actividad y diagnóstico. No compartas el token al pedir ayuda.

### Kick no autoriza: error de redirect_uri o HTTP 400 vacío
La Redirect URL registrada en Kick Developer debe coincidir exactamente con la que muestra el panel: http://localhost:17564/callback/. El reporte público [KickDevDocs #211](https://github.com/KickEngineering/KickDevDocs/issues/211) documentó que el flujo podía cambiar 127.0.0.1 a localhost; la documentación oficial recomienda usar localhost o aplicar su workaround si es imprescindible la IP. El panel de VTuberBot usa localhost.

Otro informe, [KickDevDocs #428](https://github.com/KickEngineering/KickDevDocs/issues/428), describe un HTTP 400 sin cuerpo aunque el desarrollador ya había comprobado PKCE, scopes y redirect_uri. En ese caso, verificá que Client ID y Client Secret pertenezcan a la misma aplicación y que los scopes estén habilitados. Si persiste con un cuerpo vacío, registrá hora y estado HTTP: el reporte no ofrece una causa técnica pública confirmada, por lo que no hay que atribuirlo automáticamente a contraseña incorrecta.

### Kick envía como usuario pero no como bot
[KickDevDocs #343](https://github.com/KickEngineering/KickDevDocs/issues/343) documenta un 500 al usar type=bot mientras type=user funcionaba para el autor. Empezá con el modo Usuario, que el panel usa por defecto. El modo Bot puede requerir configuración del bot y autorización de la cuenta dueña del canal. No cambies de identidad silenciosamente ni repitas el mensaje muchas veces.

### Kick no recibe mensajes o eventos
Aunque la API puede confirmar que una suscripción fue aceptada, eso no garantiza entrega. Revisá que **Enable Webhooks** esté activado en Kick Developer, que la URL pública HTTPS coincida exactamente, que el túnel esté iniciado y que reenvíe la ruta `/kick-webhook` a `127.0.0.1:17565`. Luego iniciá el receptor local antes de suscribir eventos. Los webhooks con firma inválida, timestamp demasiado antiguo, payload inválido o ID repetido no ejecutan reglas. La [documentación oficial de Events](https://docs.kick.com/events/introduction) explica el flujo; [Webhook security](https://docs.kick.com/events/webhook-security) detalla la firma y la clave pública rotativa. El reporte [KickDevDocs #419](https://github.com/KickEngineering/KickDevDocs/issues/419) describe casos donde una suscripción fue aceptada pero no apareció en la lista; [#403](https://github.com/KickEngineering/KickDevDocs/issues/403) describe problemas de entrega relacionados con proxies/WAF. No desactives globalmente la seguridad ni aceptes una firma inválida.

### OBS WebSocket no conecta
La guía oficial confirma que obs-websocket está incorporado desde OBS Studio 28. Abrí las preferencias de WebSocket, habilitá autenticación, comprobá que puerto y contraseña coincidan y mantené la conexión en loopback salvo que entiendas la configuración de red. La [guía oficial de control remoto](https://obsproject.com/kb/remote-control-guide) recomienda proteger el control con contraseña.

### Cómo leer el progreso del asistente de OBS
El asistente separa ahora los pasos que confirmás vos de las comprobaciones que puede ejecutar VTuberBot. **M✓** significa «confirmado manualmente»: la aplicación guarda tu confirmación, pero no inspecciona automáticamente ese resultado. **✓** significa que la aplicación recibió una respuesta válida en una comprobación real anterior; no garantiza que la conexión siga activa para siempre.

Los pasos de conexión WebSocket y de aplicación del perfil 720p/30 FPS no se pueden completar marcando una casilla. Para el primero, usá **Comprobar conexión real**: la app intenta conectarse/autenticarse y leer la versión de OBS. Para el segundo, confirmá el cambio global y esperá que OBS acepte la operación. Si falla, el paso queda pendiente; corregí el puerto/contraseña o revisá el estado de OBS antes de volver a intentarlo. El perfil modifica ajustes reales de OBS y puede afectar todas las escenas.

El progreso se guarda en este equipo. Las marcas manuales y las verificaciones automáticas son tipos de evidencia distintos; ninguna de ellas demuestra que una grabación o un directo completo se vean y escuchen bien. Para eso, hacé una grabación local y reproducí el archivo.

### La voz no sale
Confirmá que el Engine esté instalado y en ejecución, que la URL sea local, que haya estilos disponibles y que Windows use el dispositivo de salida correcto. Ni Engine ni voicebanks forman parte del ZIP de VTuberBot.

### Un analizador de WPF falla aunque la compilación normal pasa
El reporte [dotnet/wpf #11775](https://github.com/dotnet/wpf/issues/11775) describe NETSDK1022 en un proyecto temporal wpftmp durante DesignTimeBuild con el SDK .NET 10; el reporte indica que una compilación normal podía funcionar. Primero distinguí el análisis de diseño del build de Release de verdad. No fijes un SDK viejo ni cambies las propiedades del proyecto a ciegas sin reproducir el caso.

## Fuentes oficiales y reportes públicos consultados

- [OBS Remote Control Guide](https://obsproject.com/kb/remote-control-guide)
- [OBS WebSocket](https://github.com/obsproject/obs-websocket)
- [Twitch: Chat and Chatbots](https://dev.twitch.tv/docs/chat)
- [Twitch OAuth scopes](https://dev.twitch.tv/docs/authentication/scopes/)
- [Kick OAuth 2.1](https://github.com/KickEngineering/KickDevDocs/blob/main/getting-started/generating-tokens-oauth2-flow.md)
- [Kick Chat API](https://github.com/KickEngineering/KickDevDocs/blob/main/apis/chat.md)
- [Kick moderation API](https://docs.kick.com/apis/moderation)
- [Kick API scopes](https://docs.kick.com/getting-started/scopes)
- [Kick Events API](https://docs.kick.com/events/introduction)
- [Kick webhook subscriptions](https://github.com/KickEngineering/KickDevDocs/blob/main/events/subscribe-to-events.md)
- [Kick webhook security](https://docs.kick.com/events/webhook-security)
- [KickDevDocs #211: redirect URI](https://github.com/KickEngineering/KickDevDocs/issues/211)
- [KickDevDocs #413: secretos OAuth en apps de escritorio](https://github.com/KickEngineering/KickDevDocs/issues/413)
- [KickDevDocs #428: error 400 sin cuerpo](https://github.com/KickEngineering/KickDevDocs/issues/428)
- [KickDevDocs #403: webhook y proxy/WAF](https://github.com/KickEngineering/KickDevDocs/issues/403)
- [KickDevDocs #343: mensajes como bot](https://github.com/KickEngineering/KickDevDocs/issues/343)
- [dotnet/wpf #11775: NETSDK1022 durante análisis de diseño](https://github.com/dotnet/wpf/issues/11775)

## Qué prueba cada tipo de validación

Una compilación y tests verdes prueban que el código puede compilar y que los escenarios automatizados pasan. No prueban que las cuentas personales estén autenticadas, que haya eventos entrantes de Kick, que VOICEVOX esté instalado, que el micrófono funcione en tu PC o que una transmisión real de OBS haya salido bien. Esas acciones necesitan una comprobación real en Windows.
