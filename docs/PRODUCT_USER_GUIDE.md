# Guía del candidato integral de VTuberBot

Esta guía corresponde a la rama `product/complete-vtuber-experience`. Las funciones externas no se consideran conectadas hasta completar la autenticación real.

## Primer inicio y datos

- Ejecutá `VtuberBot.App.exe` en Windows.
- La base SQLite, los logs, preferencias, perfiles y assets importados se guardan bajo `%LocalAppData%\VtuberBot\`.
- El token de Twitch y la contraseña de OBS se cifran con DPAPI para el usuario de Windows actual. No copies archivos de secretos entre usuarios ni los subas a GitHub.
- El paquete portable no debe incluir `config.toml`, base de datos, logs, tokens ni cookies.

## Avatar Studio y OBS overlay

1. Abrí **Avatar Studio** e importá tu imagen base.
2. Importá capas PNG/JPG adicionales transparentes. El orden de la lista determina la composición; podés ocultar o eliminar una capa sin borrar el archivo del disco.
3. Abrí **Mostrar overlay transparente**.
4. En OBS agregá una fuente **Captura de ventana** y seleccioná `VTuberBot Avatar Overlay`.
5. Arrastrá la ventana para ubicarla y redimensionala con el borde. Doble clic alterna la prioridad Topmost.

El render actual compone sprites alineados y añade un movimiento respiratorio sutil. Todavía no es un rig facial avanzado: las capas no tienen pivotes/parentado por huesos, y no se declara lip-sync real solo por tener micrófono.

## Twitch: OAuth y chat real

1. Creá una aplicación en la [consola de desarrolladores de Twitch](https://dev.twitch.tv/console/apps).
2. Registrá exactamente esta URL de redirección: `http://localhost:17563/callback/`.
3. Copiá el **Client ID** de la aplicación en **Twitch y chat**.
4. Escribí el nombre de la cuenta/canal que corresponde al usuario autorizado y elegí **Autorizar con Twitch**. Se abre el navegador; aceptá los permisos `chat:read`, `chat:edit`, `moderator:read:followers`, `channel:read:subscriptions` y `channel:read:redemptions`.
5. Volvé a la app y elegí **Conectar**. El token queda cifrado con DPAPI. El estado conectado solo se informa después del registro IRC por TLS.
6. Probá enviar un mensaje o usar comandos como `!hola`, `!puntos`, `!dado` y `!entrar`.

Los mensajes del chat comparten el registro local de comandos; la actividad otorga puntos con un intervalo anti-farming y `!entrar` registra participantes únicos en el sorteo abierto. Los errores de autenticación no se tratan como conexión exitosa. Para alertas oficiales, elegí **Conectar EventSub (alertas)** después de autorizar con los scopes ampliados. La app intenta registrar follows, suscripciones, regalos de subs, redenciones de puntos de canal y raids hacia tu canal. Las notificaciones aparecen en el registro del chat y en Actividad/diagnóstico; si hay una imagen base importada, también se muestra una tarjeta de alerta temporal en el overlay de OBS.

## Comandos, comunidad y sorteos

- **Comandos:** creá respuestas con variables `{user}`, `{points}` y `{roll}`. Los cooldowns se guardan en SQLite y se aplican al ejecutar el comando.
- **Comunidad y puntos:** los perfiles, el ranking, los ajustes manuales y el ledger de puntos se conservan en la base local. La actividad del chat puede otorgar puntos automáticamente.
- **Sorteos:** creá un sorteo, registrá participantes manualmente o mediante `!entrar` en el chat y cerralo con una selección aleatoria trazable. Las entradas duplicadas se descartan.
- **Minijuegos:** las rondas locales guardan resultados y recompensas. No requieren Twitch conectado.
- **Moderación:** las reglas regex se prueban localmente y se evalúan sobre el chat en modo observación. No se ejecutan borrados ni sanciones remotas automáticas.
- **Automatizaciones:** los mensajes programados respetan un intervalo mínimo de cinco minutos y solo se envían cuando Twitch está conectado. Si está desconectado, la omisión queda registrada.

## Voz y audio

- Seleccioná el micrófono disponible e iniciá la captura real.
- Ajustá el umbral acústico y el tiempo de retención para que el estado Talking no parpadee por pequeñas pausas.
- El detector local suaviza el nivel de señal y activa el cue Talking del overlay; no reconoce palabras ni equivale a lip-sync por fonemas.
- **Hablar** usa las voces locales instaladas mediante Windows SAPI. No necesita una API de IA remota.
- La cola avanzada de voz, prioridades de reproducción y lip-sync facial por fonemas todavía no forman parte de esta integración.

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
