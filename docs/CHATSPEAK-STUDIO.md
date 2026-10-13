# ChatSpeak Studio

## Objetivo

ChatSpeak Studio separa el trabajo de IA y voz del PC local. VTuberBot conserva Twitch IRC, el control de la cola de audio, los límites de seguridad y el avatar, mientras que los modelos y el audio se generan por servicios remotos cuando se configura y activa esa opción.

## Los dos modos

### Modo manual con ChatGPT web

1. Abre ChatSpeak Studio y pulsa **Abrir ChatGPT web**.
2. Pulsa **Copiar prompt de personaje** y pega el prompt en ChatGPT.
3. Pega la respuesta de ChatGPT en la caja manual.
4. Pulsa **Procesar respuesta pegada** para ejecutar solamente los comandos visuales permitidos y encolar la frase para la voz.

El prompt incluye una selección breve de los mensajes recientes visibles para VTuberBot. La opción de publicar texto en Twitch está desactivada por defecto. No se leen automáticamente las cookies, el DOM, la sesión ni las respuestas de la web de ChatGPT. Para cambiar de chat, abre una conversación nueva en el propio ChatGPT. `/switch-chat` abre la web; `/reset-chat` reinicia el contexto local de VTuberBot y cancela las generaciones activas, pero no modifica la conversación web.

### Modo bot automático

El proveedor remoto viene seleccionado inicialmente para evitar depender de un modelo local pesado. El valor inicial usa la API compatible con OpenAI de Gemini:

- Base URL: `https://generativelanguage.googleapis.com/v1beta/openai/`
- Modelo: `gemini-3.8-flash`

Crea una clave en [Google AI Studio](https://aistudio.google.com/apikey) y guárdala en ChatSpeak Studio. El nivel gratuito publicado de Gemini incluye modelos de chat y modelos TTS compatibles, con límites que pueden variar por cuenta, región, modelo y uso. Google indica que el contenido del nivel gratuito puede usarse para mejorar sus productos. Revisa la política de privacidad y los límites antes de enviar mensajes reales de Twitch. Si prefieres otro proveedor OpenAI-compatible, cambia su URL, modelo y clave.

El modo automático permanece desactivado por defecto. Solo genera respuestas cuando llega el comando/mención configurado, o cuando la función de comentario periódico está expresamente habilitada. La publicación de texto en Twitch es una opción distinta.

### Comentario periódico del chat

La opción **Comentar automáticamente el chat por intervalos** empieza desactivada y requiere el modo bot. Su intervalo inicial es de 5 minutos, ajustable entre 1 y 60 minutos. El colector conserva únicamente en memoria un máximo de 60 mensajes recientes, corta mensajes largos, omite enlaces, comandos con `!` y menciones directas al bot, y no persiste un transcript en SQLite. Si la cola se llena, el lote se aplaza; si el bot se pausa o se desconecta Twitch, se descarta. La IA puede devolver `[SILENCIO]` si el lote no contiene un tema digno de comentar.

## Voz de personaje femenina

La configuración inicial usa Gemini 3.8 Flash TTS por medio de la API Interactions de Google:

- Base URL: `https://generativelanguage.googleapis.com/v1beta`
- Modelo: `gemini-3.8-flash-tts`
- Voz inicial: `Leda`, con instrucciones para una voz femenina juvenil, cálida, expresiva y de estilo anime original.

El audio se devuelve como WAV y se reproduce por la cola de voz existente. La API TTS de Gemini publica un nivel gratuito sujeto a sus límites; el contenido de ese nivel puede utilizarse para mejorar productos de Google. Si cambias a otro TTS compatible con OpenAI, la ruta `/v1/audio/speech` permite recibir MP3. La clave TTS puede reutilizar la de IA si ambas URL pertenecen al mismo endpoint/proveedor detectado por el programa; para servicios distintos, guarda una clave TTS independiente. También puedes seleccionar el TTS local de Windows, que no requiere conexión ni una API, aunque la calidad depende de las voces instaladas.

Estas voces son generadas, no copias de una actriz ni suplantaciones de personajes protegidos. Se puede modificar el preset de voz y las instrucciones de estilo.

## Credenciales y privacidad

Las claves se cifran para el usuario actual de Windows con DPAPI y se guardan en ficheros `.dpapi` fuera de SQLite y de los logs. No pegues claves API en prompts ni las publiques en Twitch. Las claves de chat y TTS son independientes, aunque el programa puede reutilizar de forma controlada la misma clave cuando detecta que el endpoint coincide.

Los proveedores remotos reciben el texto/contexto necesario para generar una respuesta o sintetizar voz. Elige una política de privacidad y una cuota que aceptes; el uso de endpoints de pago fuera del nivel gratuito puede generar cargos. No se envían solicitudes antes de usar el proveedor remoto y activar la función.

## Directivas visuales con lista permitida

La IA puede solicitar expresiones escribiendo una directiva exacta en una línea independiente:

- `/angri`
- `/happy`
- `/surprised`
- `/idle`
- `/talking`
- `/blink`
- `/left eye close`

El intérprete elimina estas líneas del texto hablado y acepta como máximo cuatro directivas por respuesta. Toda directiva desconocida iniciada por `/` se descarta, no se lee y no se ejecuta. La IA no puede cambiar de chat, reiniciar servicios, activar el modo bot, controlar archivos ni manejar la interfaz.

Los comandos `/reset-chat`, `/restart-chat`, `/switch-chat`, `/bot-on` y `/bot-off` son controles exclusivos del creador, disponibles en la caja de comando manual. `/bot-on` pide confirmación. No se procesan desde la salida de IA.

## Expresiones y overlay

Avatar Studio permite importar sprites para `Angry` y `LeftEyeClose`, además de los estados ya disponibles. Los comandos se pueden guardar mientras el overlay está oculto; el estado se muestra cuando el overlay público vuelve a abrirse. Si falta el sprite, la aplicación informa de que no pudo mostrar la expresión en vez de simular una animación inexistente.

## Limitaciones importantes

- Las cuotas gratuitas no son ilimitadas ni garantizadas para todas las cuentas o regiones.
- Un proveedor remoto requiere conectividad y una clave válida.
- El modo manual con ChatGPT es deliberadamente copiar/pegar; el bot no intenta automatizar ni extraer conversaciones privadas de la web.
- Una suscripción de ChatGPT no concede por sí sola créditos de API de OpenAI.
- No se descargan modelos ni pesos de IA al PC.
