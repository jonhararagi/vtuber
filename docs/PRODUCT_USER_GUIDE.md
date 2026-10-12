# Guía del candidato integral de VTuberBot

Esta guía corresponde a la rama `product/complete-vtuber-experience`. Las funciones externas no se consideran conectadas hasta completar la autenticación real.

## Primer inicio y datos

- Ejecutá `VtuberBot.App.exe` en Windows.
- La base SQLite, los logs, preferencias, perfiles y assets importados se guardan bajo `%LocalAppData%\VtuberBot\`.
- El token de Twitch y la contraseña de OBS se cifran con DPAPI para el usuario de Windows actual. No copies archivos de secretos entre usuarios ni los subas a GitHub.
- El paquete portable no debe incluir `config.toml`, base de datos, logs, tokens ni cookies.

## Avatar Studio y OBS overlay

1. Abrí **Avatar Studio** e importá tu imagen base.
2. Importá capas PNG/JPG adicionales transparentes. Podés ocultar o eliminar una capa sin borrar el archivo original, cambiar su orden visual y ajustar X/Y, escala y rotación.
3. En **Sprites por expresión**, asigná imágenes con el mismo tamaño para Idle, Talking, Blink, Happy o Surprised. La imagen base se usa como fallback cuando un estado no tiene sprite propio.
3. Abrí **Mostrar overlay transparente**.
4. En OBS agregá una fuente **Captura de ventana** y seleccioná `VTuberBot Avatar Overlay`.
5. Arrastrá la ventana para ubicarla y redimensionala con el borde. Doble clic alterna la prioridad Topmost.

El overlay compone la imagen base con capas transformables, reproduce el sprite asignado al estado activo y aplica movimiento respiratorio sutil. El micrófono activa Talking con un detector acústico configurable. Esto no equivale a un rig con huesos/física ni a lip-sync por fonemas; usá sprites de expresión con el mismo lienzo para evitar desalineaciones.

## Twitch: OAuth y chat real

1. Creá una aplicación en la [consola de desarrolladores de Twitch](https://dev.twitch.tv/console/apps).
2. Registrá exactamente esta URL de redirección: `http://localhost:17563/callback/`.
3. Copiá el **Client ID** de la aplicación en **Twitch y chat**.
4. Escribí el nombre de la cuenta/canal que corresponde al usuario autorizado y elegí **Autorizar con Twitch**. Se abre el navegador; aceptá los permisos `chat:read`, `chat:edit`, `moderator:read:followers`, `moderator:manage:banned_users`, `channel:read:subscriptions` y `channel:read:redemptions`. Para timeout/ban, la cuenta autorizada debe ser el broadcaster o un moderador del canal.
5. Volvé a la app y elegí **Conectar**. El access token, refresh token, scopes y expiración se guardan cifrados con DPAPI para el usuario actual. Mientras Chat o EventSub estén activos, VTuberBot valida la sesión cada cinco minutos y renueva el access token antes de expirar cuando Twitch proporciona un refresh token; luego restaura las conexiones activas. Si Twitch revoca tanto el token como el refresh token, tendrás que volver a autorizar. El estado conectado solo se informa tras la confirmación de Twitch. Si el socket se corta por una caída temporal, el cliente reintenta automáticamente con espera creciente (hasta 30 segundos); si Twitch rechaza el token, detiene el reintento y pide volver a autorizar.
6. Probá enviar un mensaje o usar comandos como `!hola`, `!puntos`, `!dado` y `!entrar`.

Los mensajes del chat comparten el registro local de comandos; la actividad otorga puntos con un intervalo anti-farming y `!entrar` registra participantes únicos en el sorteo abierto. Los errores de autenticación no se tratan como conexión exitosa. Para alertas oficiales, elegí **Conectar EventSub (alertas)** después de autorizar con los scopes ampliados. La app intenta registrar follows, suscripciones, regalos de subs, redenciones de puntos de canal y raids hacia tu canal. Las notificaciones aparecen en el registro del chat y en Actividad/diagnóstico; si hay una imagen base importada, también se muestra una tarjeta temporal en el overlay de OBS. Activá **Leer alertas de Twitch con la voz local** para encolarlas con prioridad alta; la voz en curso no se interrumpe.

## Comandos, comunidad y sorteos

- **Comandos:** creá respuestas con variables `{user}`, `{points}` y `{roll}`. Los cooldowns se guardan en SQLite y se aplican al ejecutar el comando.
- **Comunidad y puntos:** los perfiles, el ranking, los ajustes manuales y el ledger de puntos se conservan en la base local. La actividad del chat puede otorgar puntos automáticamente.
- **Sorteos:** creá un sorteo, registrá participantes manualmente o mediante `!entrar` en el chat y cerralo con una selección aleatoria trazable. Las entradas duplicadas se descartan.
- **Minijuegos:** las rondas locales guardan resultados y recompensas. No requieren Twitch conectado.
- **Moderación:** las reglas regex se prueban localmente y se evalúan sobre el chat en modo observación. No se ejecutan sanciones remotas automáticas.
- Las acciones manuales permiten timeout de duración elegida, ban permanente y retirar un ban/timeout, con confirmación visible antes de afectar el chat real. Si agregaste el scope `moderator:manage:banned_users` después de autorizar, volvé a completar OAuth. Los errores de permiso se muestran como fallos; nunca se simula el éxito.
- **Automatizaciones:** los mensajes programados respetan un intervalo mínimo de cinco minutos y solo se envían cuando Twitch está conectado. Si está desconectado, la omisión queda registrada.

## Voz y audio

- Seleccioná el micrófono disponible e iniciá la captura real.
- Ajustá el umbral acústico y el tiempo de retención para que el estado Talking no parpadee por pequeñas pausas.
- El detector local suaviza el nivel de señal y activa el cue Talking del overlay; no reconoce palabras ni equivale a lip-sync por fonemas.
- La cola TTS procesa frases una por una en un hilo STA de fondo para no congelar el panel. Las prioridades Alta/Normal/Baja afectan las frases que todavía esperan; limpiar pendientes no interrumpe la frase en curso.
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

- Reconexión automática de EventSub, recuperación de suscripciones revocadas, overlay visual personalizado para alertas y triggers avanzados.
- Rig 2.5D avanzado (pivotes, parentado, física), estados faciales con assets por expresión y lip-sync real.
- Enrutamiento avanzado de audio/cola priorizada y VAD de habla separado del medidor de amplitud.
- Acciones remotas de moderación con permisos explícitos, panel de triggers más avanzado y configuración de timers con edición/pausa individual.
- Copias de seguridad/restauración guiadas de SQLite y pruebas manuales end-to-end en el Windows del usuario.

Estas limitaciones se registran deliberadamente; no se consideran funciones terminadas solo por tener una página de interfaz.
