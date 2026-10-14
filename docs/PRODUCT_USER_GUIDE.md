# Guía del candidato integral de VTuberBot

Esta guía corresponde al candidato integrado de VTuberBot. Las funciones externas no se consideran conectadas hasta completar la autenticación real. El proceso para prevenir regresiones está descrito en [Regla de Murphy y puertas de fiabilidad](REGLA_MURPHY_Y_PUERTAS_DE_FIABILIDAD.md).

## Empezar: elegí tu tipo de VTuber

Arriba de **Avatar Studio** hay una galería de seis personajes chibi completos: **Neko gamer**, **Brujita lunar**, **Idol estelar**, **Gótica rosa**, **Cyber neón** y **Streamer casual**. No son una misma ilustración recoloreada: cada modelo combina distinto peinado, ropa, accesorios, rostro, ojos, tono de piel y pose. Al pulsar una tarjeta se crea un perfil propio y se abre el overlay; tus perfiles previos no se reemplazan.

Debajo de esa galería está **Crear PNGTuber con mis imágenes**. Elegí una imagen de reposo y, si ya tenés una, otra con la boca abierta para hablar. Con una sola imagen el avatar queda visualmente estático; con dos, el detector de audio alterna entre ellas.

### Crear tu propio personaje chibi

El panel **Creador de personaje chibi — cambia cada parte** permite combinar independientemente:

- **Peinado:** 11 siluetas, como coletas dobles, bob, ondas, trenza, pelo corto o largo.
- **Ropa:** 15 diseños con formas propias, entre ellos idol, marinero, gótico, ceremonial, streetwear, fantasía, hoodie, cyber y vestidos.
- **Rostro y ojos:** 4 formas de rostro, 7 estilos de ojos y 4 tonos de piel.
- **Pose:** de pie, saludo, señal de paz, manos en la cintura, brazos cruzados o cantando al micrófono.
- **Accesorios:** adornos de cabeza, joyería facial y piezas traseras como alas, capa, mochila o cristales.
- **Colores:** tintes por zona con HEX o muestras rápidas, sin sobreescribir las imágenes originales.

Pulsá **Crear perfil con esta combinación** para guardar otra versión; se crean PNG transparentes separados y podés mover las capas después. El catálogo es arte vectorial original generado localmente; tiene un estilo chibi 2D estilizado y no es un rig Live2D con deformaciones óseas ni seguimiento facial.

### Otros modos

1. **2.5D con IA local:** seleccioná una ilustración para intentar separar cabello, ropa, piel/cuerpo y detalles. La primera vez se descarga un modelo pequeño y después el procesamiento se realiza en la PC. Tiene un límite de 2 GiB para el proceso de IA y diez minutos; es experimental y puede requerir correcciones.
2. **3D con VRoid:** abre [VRoid Studio](https://vroid.com/en/studio), un creador externo de modelos 3D. La importación y animación de VRM dentro del overlay de VTuberBot todavía no está implementada.

**La ruta recomendada para empezar es elegir un personaje de la galería y luego personalizarlo.** PNGTuber es lo más ligero; 2.5D sigue siendo experimental y 3D puede integrarse cuando tengamos un runtime VRM funcional.

## Primer inicio y datos

- Ejecutá `VtuberBot.App.exe` en Windows.
- La base SQLite, los logs, preferencias, perfiles y assets importados se guardan bajo `%LocalAppData%\VtuberBot\`.
- El token de Twitch y la contraseña de OBS se cifran con DPAPI para el usuario de Windows actual. No copies archivos de secretos entre usuarios ni los subas a GitHub.
- El paquete portable no debe incluir `config.toml`, base de datos, logs, tokens ni cookies.

## Dashboard, Avatar Studio y OBS overlay
El Dashboard muestra CPU y RAM reales del proceso cada dos segundos mientras está abierto; no presenta valores GPU/FPS inventados.

### Probar un avatar sin traer imágenes externas

1. Abrí **Avatar Studio** y usá la tarjeta de inicio **Empezá por acá**.
2. **PNGTuber de ejemplo** crea un perfil con sprites para reposo, habla, parpadeo, alegría y sorpresa. Al configurar el micrófono, el detector acústico puede alternar Idle/Talking. Es un PNGTuber por estados, no un avatar con tracking facial.
3. El ejemplo 2D por capas está dentro de **Personalización avanzada** y crea un perfil separado. El flequillo y los destellos son imágenes transparentes independientes: seleccioná una capa, arrastrala en la previsualización y guardá X/Y, escala o rotación. El overlay agrega un movimiento ligero, pero no es un rig Live2D con huesos, deformación facial o seguimiento por cámara.
4. Los sprites de demostración se generan localmente la primera vez. No hace falta descargar arte, configurar una API ni sobrescribir tus perfiles propios. Al pulsar de nuevo un ejemplo ya creado, se activa el perfil existente sin borrar tus cambios.
5. La opción avanzada **Configurar PNGTuber Plus externo** abre su página de configuración. PNGTuber Plus es otra aplicación y necesita instalarse aparte; el botón de ejemplo integrado no pretende simular que esa aplicación ya está instalada.

### Modo local lento y seguro: separar una ilustración

En la tarjeta superior **Empezá por acá**, pulsá **2 · 2.5D con IA local**. Se admiten PNG, JPG/JPEG y BMP de hasta 25 MB. La primera vez el programa descarga un modelo de unos 8,7 MB; después la inferencia se realiza localmente y la imagen no se envía a un servidor.

- La IA se ejecuta en un proceso separado, solo en CPU, con un hilo de inferencia, prioridad baja, una fecha límite de 10 minutos y un límite de memoria de proceso de 2 GiB aplicado por Windows Job Object. Si el sistema no permite imponer ese límite, el trabajo no se inicia. Podés cancelar sin cerrar el bot.
- El modelo intenta separar **piel/cuerpo, ropa y cabello**. Los píxeles cercanos que no entran en esas zonas se conservan en una capa de **detalles y contorno**. Al finalizar se crea un perfil nuevo con las capas PNG transparentes.
- Es un modelo experimental: su autor advierte que la precisión en ilustraciones puede ser limitada. Puede clasificar mal pelo/ropa/piel, dejar huecos o fallar con fondos complejos. No reconstruye de manera generativa el reverso de una prenda, no detecta de forma independiente cada ojo/accesorio y no crea automáticamente expresiones distintas.
- El límite de 2 GiB se aplica al **proceso de IA**, no a la suma de VTuberBot, OBS y otros programas. La primera descarga requiere internet; después se puede procesar sin conexión, siempre que el modelo ya esté descargado.
- El modelo proviene de [Skin-Clothes-Hair-Segmentation-using-SMP](https://github.com/Kazuhito00/Skin-Clothes-Hair-Segmentation-using-SMP), publicado con licencia MIT. El programa verifica el tamaño y la huella del archivo antes de ejecutarlo. Ver [avisos de terceros](THIRD_PARTY_NOTICES.md).

### Crear una VTuber desde una sola imagen

Dentro de **Personalización avanzada** existe una alternativa web para probar una separación más compleja, usando dos accesos directos:

1. **Separar imagen automáticamente:** abre [See-through](https://huggingface.co/spaces/24yearsold/see-through-demo), una demo online que usa IA para inferir capas a partir de una ilustración anime y exportarlas como PSD. Cargá una imagen, esperá el procesamiento y descargá el PSD.
2. **Animar el PSD como VTuber:** abre [Anime2.5DRig](https://852wa.github.io/Anime2.5DRig/). Cargá el PSD que descargaste para probar parpadeo, movimiento de pelo, boca, cámara/micrófono y salida para OBS.

**Importante:** este flujo abre servicios externos en el navegador; el separador no se ejecuta dentro de VTuberBot y hay que subir la imagen manualmente. Las ejecuciones gratuitas del separador pueden ser limitadas (la documentación del proyecto indica alrededor de 1–2 extracciones diarias por usuario registrado). La imagen sale de tu PC, así que no uses arte privado. El separador local requiere alrededor de 12–16 GB de VRAM en la configuración recomendada y no es una opción realista para el equipo objetivo con gráficos integrados; por eso el atajo usa la demo online.

La descomposición de una imagen plana es una reconstrucción estimada: el modelo intenta generar lo que queda oculto tras el cabello o la ropa y puede equivocarse. Puede necesitar ajustes y no crea por sí solo un rig Live2D profesional. El camino integrado en la aplicación sigue siendo un acceso sencillo a esas herramientas, no una importación directa del PSD al overlay de VTuberBot.

### Combinar ropa, peinados y accesorios

1. En Avatar Studio, bajá hasta **Armario modular — piezas originales inspiradas en estilos VTuber**.
2. Elegí por separado **Cabello**, **Conjunto / ropa**, **Accesorio de cabeza**, **Accesorio facial / joyería** y **Espalda / capa / alas / mochila**.
3. Pulsá **Crear perfil con esta combinación**. El programa genera los PNG transparentes localmente y guarda una combinación nueva como perfil independiente.
4. Podés volver al armario y crear muchas combinaciones distintas. Tus perfiles anteriores no se sobrescriben.
5. En la lista de capas, seleccioná una prenda o accesorio para moverlo con arrastre, ajustar escala/rotación, ocultarlo o cambiar su orden. Las piezas traseras aparecen debajo del personaje.
6. El catálogo tiene 11 peinados, 15 conjuntos, 14 adornos de cabeza, 9 accesorios faciales/joyería y 9 piezas traseras. Son **diseños originales genéricos**, estudiados a partir de tendencias visibles en VTubers conocidos; no son modelos de esos VTubers ni utilizan sus ilustraciones oficiales.

Para saber qué referencias visuales se estudiaron y cómo se mapearon a categorías, consultá [Referencias visuales y biblioteca modular de VTuberBot](VTUBER_REFERENCIA_Y_BIBLIOTECA_MODULAR.md).

### Buscar más diseños de personaje en la web

Dentro de Avatar Studio → Creador de personaje chibi hay enlaces a herramientas públicas con estilos distintos al catálogo interno:

- [Humation](https://apps.humation.app/humation): creador kawaii dibujado a mano, exporta PNG/SVG. El repositorio indica que código y 86 piezas están bajo MIT: [repositorio/licencia](https://github.com/humation-labs/humation).
- [Open Peeps Studio](https://peepstudio.msgenie.art/): ilustraciones de cuerpo entero, poses, expresiones, cabello y ropa; exporta PNG/SVG. Está basado en las ilustraciones originales Open Peeps CC0: [fuente/licencia](https://www.openpeeps.com/).
- [Chibi Maker](https://squarefaceicongenerator.co/pricing/chibi-maker): editor de iconos chibi con peinados, ojos y atuendos, exporta PNG. Es principalmente un icono cuadrado, no un cuerpo completo.

Flujo: abrí el creador, armá el diseño, descargalo e importá la imagen en un perfil. Estas páginas no se integran automáticamente con el rig: la imagen es estática hasta asignar sprites distintos de reposo, habla y expresiones. No uses un retrato de perfil como si fuera un modelo animado de cuerpo entero. Revisá los términos de la herramienta y el modo elegido antes de distribuir o vender el arte.

## Sincronizar la voz del bot con el avatar

El estado visual Talking permanece activo si hay actividad del micrófono o una frase en la cola TTS de VTuberBot, y vuelve a Idle cuando ambas fuentes están inactivas. Esto es sincronización por estado, no seguimiento facial ni lip-sync por fonemas. Una imagen distinta de boca abierta para Talking hace visible el cambio; reutilizar la misma imagen en reposo y habla no anima la boca.

## Personalizar el avatar

1. Cada perfil conserva su propia imagen base, capas/transformaciones y sprites de expresión. Podés crear un perfil vacío, duplicar el actual o cambiar entre perfiles desde la parte superior. Eliminar un perfil no borra los archivos de imagen importados, y siempre debe quedar al menos un perfil. **Exportar perfil (.vtuberavatar)** guarda base, capas, transformaciones y expresiones juntos; al importarlo se crea otro perfil sin sobrescribir los existentes.
2. Importá una imagen base propia si querés reemplazar el personaje de ejemplo.
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

## Transmitir en Twitch o YouTube

En la página **OBS**, conecta OBS WebSocket con host `127.0.0.1`, puerto `4455` y tu contraseña. En **Transmitir a Twitch o YouTube**, elige la plataforma, pulsa **Abrir panel de la plataforma** para preparar un directo, pega la clave de emisión y pulsa **Guardar clave cifrada**. Después usa **Configurar destino en OBS** y, cuando hayas comprobado escena, audio y privacidad, pulsa **Iniciar transmisión**. Puedes dejar el campo de clave vacío para reutilizar la clave cifrada ya guardada. Cambiar de plataforma exige parar primero la emisión.

Para emitir en las dos plataformas a la vez, seguí la guía [Transmisión a Twitch y YouTube](STREAMING-YOUTUBE-TWITCH.md). Necesitarás configurar por separado la salida secundaria en un complemento compatible de OBS. VTuberBot controla la salida principal; no afirma que el botón de inicio maneje automáticamente un complemento externo.

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


## Bot social / ChatSpeak Studio — guía de inicio simple

La página **Bot social / IA** está organizada en pestañas para no mostrar todas las opciones de golpe. La primera pestaña, **Empezar aquí**, explica el orden recomendado. Las respuestas automáticas y la publicación en Twitch permanecen desactivadas hasta que tú las actives.

### Conseguir la voz aguda y chillona

1. Abrí **Bot social / IA** y entrá en **Voz y audio**.
2. Dejá seleccionado **Remoto (voz de personaje)** y mantén el modelo predeterminado de Gemini TTS. La voz inicial es **Zephyr**, con instrucciones para que el resultado sea muy agudo, brillante, nasal/juguetón y exagerado, como una voz caricaturesca de anime.
3. Si no guardaste una clave de Google AI Studio, creá una desde https://aistudio.google.com/apikey y pegala en el campo de clave TTS. Si ya guardaste una clave de IA de Google, la aplicación puede reutilizarla.
4. Pulsa **Guardar toda la configuración** y después **Probar voz chillona**. Escucha la frase y ajusta las instrucciones antes de usarla en un directo. La etiqueta Zephyr es un preset; la altura exacta del tono depende del modelo y del proveedor, así que la prueba auditiva es importante.
5. Para usar una voz de Windows, seleccioná **Local (voces de Windows)** en lugar del proveedor remoto. La calidad y el tono dependerán de las voces instaladas en ese equipo.

Las claves se cifran con DPAPI para tu usuario de Windows y no se escriben en SQLite ni en los logs. La voz remota necesita conexión y cuota del proveedor. El nivel gratuito puede tener límites y reglas de privacidad: revisa la guía [ChatSpeak Studio](CHATSPEAK-STUDIO.md) antes de enviar contenido real del chat.

### Leer el chat en voz alta

1. Entrá en la pestaña **Leer el chat** y marca **Activar lectura de chat**.
2. Mantén activos los filtros de comandos y enlaces y conserva una longitud e intervalo razonables. Puedes añadir usuarios ignorados.
3. Pulsa **Guardar toda la configuración** para aplicar los cambios.
4. Usa **Encolar frase de prueba** para revisar la salida seleccionada. **Detener voz actual** cancela la frase activa y **Vaciar cola de voz** elimina las que esperan.

La lectura del chat es independiente de las respuestas de IA. «Leer TODOS los mensajes» puede pronunciar mensajes ofensivos o incómodos y requiere aceptar una advertencia.

### Conversar con IA y automatizar respuestas

1. En **Bot automático**, deja seleccionado **Remoto (API HTTPS)** para evitar instalar un modelo pesado en el PC. Pega la clave API remota si todavía no existe y conserva el modelo predeterminado si quieres usar Gemini.
2. Configura nombre, personalidad y límites. Pulsa **Guardar toda la configuración** y prueba el proveedor en **Pruebas y estado**; la prueba privada no publica mensajes.
3. Solo cuando estés listo, activa **Habilitar respuestas automáticas** y confirma el aviso. Por defecto, el bot responde a una mención o al comando configurado (`!bot`), no a cada mensaje.
4. **Publicar las respuestas en Twitch** es una opción separada y viene desactivada. La voz de las respuestas también es independiente de la publicación de texto.
5. Para usar ChatGPT web manualmente, entra en **Chat manual**, copia el prompt, pégalo en la web, y vuelve a pegar la respuesta generada. VTuberBot no lee tu sesión privada, las cookies ni el contenido de la web de ChatGPT.

No hace falta configurar OBS ni activar el bot automático para probar la voz. Para una explicación completa de modo manual, IA remota, privacidad, límites y comandos del avatar, consulta [ChatSpeak Studio](CHATSPEAK-STUDIO.md).

## Bot social — pausa y desconexión

- Al guardar **Habilitar respuestas automáticas** desactivado, el servicio incrementa la generación de sesión, descarta conversaciones pendientes y cancela las inferencias conversacionales activas. Los trabajos antiguos no se reanudan al volver a activar el bot.
- Una desconexión real después de haber conectado Twitch cancela las conversaciones de esa sesión. Al reconectar solo se aceptan menciones nuevas. El modo local sin conexión inicial sigue disponible si no se solicita publicar en Twitch.
- La lectura TTS de mensajes entrantes es independiente de la IA conversacional: apagar las respuestas no apaga la lectura configurada. Las respuestas generadas solo se encolan para voz si siguen autorizadas al terminar.
- La cancelación impide publicaciones nuevas mientras la operación aún no se haya iniciado. Si Twitch ya aceptó un mensaje antes de la solicitud de pausa, la aplicación no puede retirarlo de forma fiable. «Encolado para voz» tampoco confirma que el audio haya sido oído.
- La señal de estado de conexión se basa en el estado tipado de TwitchChatClient, no en el análisis de mensajes de log.

## Centro de directo y reglas de reacción del chat

1. Abrí **Centro de directo**. La zona pública es la ventana separada **VTuberBot Avatar Overlay**, transparente y sin controles privados; elegí esa ventana como fuente de captura de ventana en OBS. No captures la ventana principal de VTuberBot si no querés mostrar configuraciones privadas.
2. En **Controles privados**, comprobá el estado real de Twitch y el perfil del avatar. Los botones de expresión se deshabilitan si falta el sprite correspondiente. **Mostrar overlay público** y **Ocultar overlay público** actúan sobre la ventana real del avatar.
3. En **Editor de reglas**, introducí nombre, activador, tipo de coincidencia, respuestas alternativas (una por línea), opción de voz, expresión, cooldowns y máximo por minuto. `{user}` se sustituye por el nombre de usuario validado.
4. **Guardar regla** crea una regla desactivada. Seleccionala y pulsá **Activar regla seleccionada** para habilitarla explícitamente. La primera regla coincidente de la lista tiene prioridad; usa **Subir/Bajar prioridad** para cambiar el orden.
5. **Probar regla local** ejecuta solo una simulación: muestra la regla y la respuesta prevista. No conecta Twitch ni envía chat, voz o expresiones. Para un directo real, conectá Twitch desde **Twitch y chat** y activá únicamente las reglas deseadas.
6. Para detener las reacciones, marcá **Pausar todas las reacciones automáticas**. Esto invalida trabajos activos/pendientes de reglas. No apaga la lectura TTS independiente del chat. Para ocultar la salida visual, usa **Ocultar overlay público**.
7. El registro indica si una regla coincidió, se descartó, fue cancelada, si la cola se saturó, si la voz fue encolada o si la escritura IRC terminó. «Voz encolada» no significa que el audio ya se oyó; una escritura IRC completada tampoco confirma que los espectadores vieran el mensaje.

Las reglas se guardan en SQLite local, con un máximo de 100 reglas, 8 respuestas por regla, 400 caracteres por respuesta, 64 trabajos pendientes y límites de cooldown/ejecuciones. No se permiten regex, scripts ni código del usuario. Las reglas no necesitan IA local.


## ChatGPT en ventana propia y muestras PNGTuber (14/10/2026)

El acceso **ChatGPT en ventana** abre el sitio oficial de ChatGPT en una ventana separada integrada con WebView2. Iniciá sesión con tu cuenta en esa página. Es acceso web manual: VTuberBot no extrae conversaciones ni controla la cuenta. Si la autenticación integrada no funciona o falta el runtime WebView2, la ventana incluye **Abrir navegador normal** como alternativa. Las respuestas automáticas del personaje son una función distinta y requieren configurar un proveedor de IA en Bot social / IA.

En Avatar Studio hay tres perfiles PNGTuber de demostración generados localmente: **violeta (Hikari), azul (Aoi) y rosa (Mio)**. Comparten la ilustración original de ejemplo, pero usan paletas y acentos distintos y cada uno genera imágenes transparentes para reposo, habla, parpadeo, alegría y sorpresa. Se pueden probar sin descargar imágenes ni reemplazar perfiles existentes. La voz local usa SAPI y las voces instaladas en Windows; no requiere una clave de API.

## Estado de verificación del producto

El candidato de producto se compiló y probó en Windows CI para la revisión de código `d336f0040a3b3032fde39381a5ef8cec6319a237`: [ejecución #630](https://github.com/jonhararagi/vtuber/actions/runs/38560968924). El runner informó 519 pruebas totales, ninguna fallida ni omitida, y el smoke test WPF forma parte de la suite. El [paquete win-x64](https://github.com/jonhararagi/vtuber/actions/runs/38560968924/artifacts/11891629783) incluye el ejecutable y `BUILD-INFO.txt`; este resultado no sustituye la prueba manual de audio, interfaz visual ni servicios externos.

- **Twitch:** OAuth, IRC y EventSub están implementados; necesitás autorizar una cuenta y conceder los permisos indicados. La conexión real y los efectos de moderación/alertas aún deben comprobarse con tu canal.
- **Voz:** SAPI local y cola de reproducción no requieren una API de pago. La suite prueba la lógica de la cola y el estado del avatar; todavía tenés que verificar el sonido con una voz/dispositivo instalado en Windows.
- **Avatar:** los personajes chibi son composiciones vectoriales 2D de piezas combinables. Podés cambiar pelo, ropa, rostro, ojos, piel, pose, accesorios y tintes. No son modelos Live2D, no tienen tracking facial y las poses cambian capas/gestos, no una animación ósea.
- **IA conversacional:** es opcional. El proveedor local compatible con OpenAI puede apuntar a un servidor local ya instalado; VTuberBot no descarga modelos grandes sin autorización. Los proveedores remotos son opcionales y pueden requerir clave.
- **OBS / Streamer.bot / PNGTuber Plus:** configuración y clientes de protocolo/acciones existen en el código; las pruebas automatizadas usan límites simulados cuando corresponde. Confirmá conexión y efectos en las aplicaciones reales. Guardar una ruta o enviar un hotkey no demuestra por sí solo que una expresión haya cambiado.
- **Telegram Web:** se abre en WebView2 y ofrece navegador externo como alternativa. No se valida una sesión real de Telegram en CI.
- **Videojuegos:** VTuberBot no incluye un agente que observe y juegue videojuegos arbitrarios. Los minijuegos disponibles son interacciones del chat.
- **Pendiente de tu entorno Windows:** revisar visualmente las seis tarjetas y combinaciones del creador chibi, transparencia y orden de capas del overlay en OBS, micrófono/SAPI reales, Twitch OAuth/IRC/EventSub, conexión/acciones de bajo riesgo en OBS y Streamer.bot, y respuesta de hotkeys en PNGTuber Plus.
