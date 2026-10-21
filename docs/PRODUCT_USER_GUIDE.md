> **¿Primera vez con VTuberBot?** Empezá por la [Guía fácil para principiantes](GUIA_PRINCIPIANTES.md), que explica cada panel en lenguaje sencillo e incluye soluciones a errores frecuentes. Dentro de la aplicación, abrí **Guía fácil** en la barra lateral; para bots también existe la pestaña **¿Cómo funciona?**.
>
> **Guía del candidato actual — actualización 2026-10-21:** la navegación incluye Inicio, Guía fácil, PNGTuber 2D, Avatar Studio, Voz y audio, OBS, Bots y plataformas, Moderación, Comandos, Actividad/diagnóstico y Ajustes. Importá tu propia imagen, ajustá el encuadre y asigná reposo (Idle) y hablando (Talking). VOICEVOX Engine y sus voicebanks se instalan por separado; iniciá el motor local, comprobá la lista real de voces y probá audio. Para OBS, conectá WebSocket v5 y verificá el estado real. Twitch reutiliza chat/EventSub y permite operaciones de moderación según scopes. Kick dispone de OAuth, envío de prueba confirmado, moderación y recepción de eventos por webhooks firmados. Para eventos Kick reales, la persona debe crear/configurar su app, activar Enable Webhooks, registrar suscripciones y mantener un túnel HTTPS público que reenvíe `/kick-webhook` a `127.0.0.1:17565`; la app no crea ese túnel. Los mensajes Kick entrantes pueden ejecutar reglas de moderación opt-in, pero todavía no alimentan el router común de comandos, ChatSpeak, reacciones ni TTS. Los webhooks admitidos se guardan en SQLite antes del HTTP 200; un evento interrumpido queda para revisión y no se reproduce automáticamente. Aún no hay una pantalla para inspeccionar o reintentar esos registros. La vista de encuadre OBS es orientativa, no una fuente OBS sincronizada. Guías relacionadas: [VOICEVOX local](VOICEVOX_LOCAL_SETUP.md), [Guía para principiantes](GUIA_PRINCIPIANTES.md).
>
> Las secciones heredadas sobre galería chibi, catálogo de piezas como flujo principal, 2.5D, Telegram Web y PNGTuber Plus no describen el flujo principal actual. Twitch y Kick se conservan como integraciones opcionales; las operaciones reales dependen de permisos y configuración externa. Kick puede recibir eventos por webhooks firmados, pero la entrega necesita un túnel HTTPS público y suscripciones correctamente registradas; su chat entrante todavía no se enruta a comandos comunes ni TTS. Consultá [la especificación definitiva](ESPECIFICACION_DEFINITIVA.md), [la guía para principiantes](GUIA_PRINCIPIANTES.md), [la guía actualizada de Avatar Studio](AVATAR_EDITOR_GUIDE.md) y la [auditoría técnica de bots](TECHNICAL_AUDIT_BOTS_2026-10-18.md).

---

# Guía del candidato integral de VTuberBot

Esta guía corresponde al candidato integrado de VTuberBot. Las funciones externas no se consideran conectadas hasta completar la autenticación real. El proceso para prevenir regresiones está descrito en [Regla de Murphy y puertas de fiabilidad](REGLA_MURPHY_Y_PUERTAS_DE_FIABILIDAD.md).

## Qué es y dónde abrirlo

En **Avatar Studio**, abrí **Creador de waifu PNG por objetos**. Es un editor 2D local para combinar una ilustración propia con accesorios y expresiones independientes y exportar una composición PNG con transparencia. Los recursos de inicio son dibujos vectoriales originales generados por VTuberBot; el editor también permite importar piezas tuyas. No necesita clave de IA ni sube imágenes a la web.

## Histórico — Biblioteca inicial (no es el flujo principal actual)

El catálogo se genera en el equipo y contiene 40 recursos originales:

- **10 accesorios de cabeza:** orejas neko y de conejo, sombrero de bruja, boina pastel, gorra de streamer, mini corona, lazo, halo, corona de flores y gorra marinera.
- **9 conjuntos de ojos:** abiertos, felices cerrados, enojados, tristes, llorosos, de corazón, guiño, somnolientos y sorprendidos.
- **7 bocas:** sonrisa cerrada, boca abierta feliz, risa grande, sorpresa en O, boca triste, grito enojado y puchero.
- **10 efectos:** lágrimas, sudor, rubor, líneas de tristeza, símbolo de enfado, corazones, destellos, confusión, estrellas manga y sombra dramática.
- **4 fondos 1920×1080:** atardecer sakura, galaxia, habitación pastel y noche neón.

También podés importar PNG, JPG, JPEG, BMP y GIF propios. Los archivos se copian a la carpeta de datos local de VTuberBot; no se suben a Internet. Para recursos de terceros, revisá la licencia y los permisos antes de usarlos en una transmisión pública.

## Crear y editar un personaje

1. Pulsá **Importar PNG principal** y seleccioná la ilustración del personaje. La opción **Quitar fondo blanco conectado al borde** está activada por defecto.
2. El editor intenta hacer transparente el blanco claro conectado al borde de la imagen. Usa la conectividad del borde para conservar, cuando están aislados, los detalles blancos interiores como dientes, brillo de ojos o adornos. Es un método local de eliminación de matte, no una segmentación perfecta: el pelo muy claro o un contorno blanco conectado al fondo pueden requerir retoque manual.
3. Elegí una categoría, seleccioná un objeto y pulsá **Añadir objeto seleccionado**. También podés importar un objeto, fondo o GIF externo.
4. Seleccioná la capa en el panel derecho. Arrastrala sobre el lienzo o ajustá posición X/Y, tamaño, giro y opacidad. Podés duplicar, ocultar, reordenar o eliminar capas.
5. Añadí boca, ojos, cejas/ojos enojados y efectos por separado. Para preparar diferentes estados, ocultá o cambiá las piezas que varían, exportá cada versión y asigná luego los PNG por estado desde **Avatar Studio → Sprites por expresión**.

El selector **Lienzo** permite elegir **Cuadrado 1024×1024** (predeterminado), **HD 1280×720** o **Full HD 1920×1080**. Al cambiar de tamaño, las capas se escalan y recentran para conservar su composición, y las capas de categoría **Fondos** vuelven a cubrir el lienzo completo. La resolución elegida se guarda en el proyecto JSON y se restaura al abrirlo. Para aprovechar los fondos 1920×1080 sin recortar los laterales, elegí el lienzo Full HD 16:9; el lienzo cuadrado recorta los bordes de un fondo panorámico. El PNG exportado usa las dimensiones del lienzo actual y conserva el canal alfa si no hay una capa de fondo opaca.

## Guardar, exportar y usarlo como PNGTuber

- **Guardar proyecto** crea un archivo JSON con las capas, transformaciones, visibilidad y rutas. Los archivos de imagen asociados permanecen en la carpeta local de VTuberBot; copiar solo el JSON a otro equipo no incluye automáticamente esos recursos.
- **Exportar PNG transparente** genera una imagen plana con las capas visibles. Para que el fondo permanezca transparente, no añadas una capa de fondo.
- **Crear PNGTuber en VTuberBot** exporta la composición y crea un perfil nuevo de avatar que usa esa imagen. La aplicación guarda una copia dentro de sus datos de avatar para no depender del archivo de exportación original.
- Los objetos se editan por separado dentro del proyecto; la imagen PNG final es una composición aplanada. Conservá el JSON para volver a mover o quitar accesorios.

## GIF y fondos

Los GIF importados se reproducen en la vista previa para que puedas alinear un fondo o un efecto animado. La exportación PNG es estática y usa el fotograma visible en el momento de exportar; no exporta un GIF nuevo ni un vídeo animado. Los cuatro fondos de inicio son ilustraciones estáticas de alta resolución.

## Histórico — Convertir el PNG a 2.5D (fuera del alcance actual)

Pulsá **Crear PNGTuber + IA local 2.5D**. El editor exporta la composición y se la entrega al flujo de segmentación local que ya existe en Avatar Studio. Cuando el modelo está disponible, el procesamiento se ejecuta localmente sobre CPU y crea capas aproximadas de piel/cuerpo, ropa, cabello y detalles. El perfil resultante puede necesitar corregir alineación, recortes o zonas ocultas.

**Límites importantes:**

- Es segmentación semántica experimental, no generación de nuevas partes, reconstrucción 3D completa ni estimación fiable de profundidad.
- La primera ejecución puede necesitar conexión a Internet para descargar el modelo ONNX verificado; luego se reutiliza la copia local en caché. No requiere una clave de API de IA ni llama a un proveedor remoto de generación.
- No crea un rig Live2D, seguimiento facial ni lip-sync por fonemas. Sigue siendo un avatar 2D por capas con movimiento ligero.
- Si la segmentación falla o no encuentra suficientes píxeles útiles, se informa el error y no se debe presentar un perfil segmentado como resultado exitoso.

## Histórico — Perfiles de bots de Twitch (fuera del alcance actual)

En **Twitch y chat** podés guardar varios perfiles, por ejemplo **Mode-chan**, con su login real, canal de destino, Client ID y credenciales OAuth. El catálogo solo contiene metadatos; los tokens de acceso/renovación de cada perfil se cifran con DPAPI de Windows para el usuario actual.

- Para crear un perfil, iniciá sesión/autorizá la cuenta bot correcta con Twitch OAuth, completá el login real de esa cuenta y el canal de destino, y elegí **Guardar perfil actual**.
- Para usarlo, seleccioná el perfil y elegí **Activar perfil seleccionado**. VTuberBot desconecta la sesión IRC anterior y conecta la cuenta autorizada al canal elegido. El login enviado en `NICK` es distinto del canal al que se une con `JOIN`; el token debe pertenecer a esa cuenta bot o Twitch rechazará la conexión.
- Solo un perfil de chat nativo puede estar conectado a la vez en esta versión. Los perfiles no crean cuentas de Twitch, no inventan identidades ni cambian el nombre visible de una cuenta: cada cuenta bot debe crearse y autorizarse en Twitch por separado.
- El bot usa los comandos y automatizaciones existentes de VTuberBot; guardar un perfil no inventa comandos ni publica mensajes. Para enviar mensajes necesita una sesión autenticada real.

## OBS Studio: inicio automático y modo ligero

En la página **OBS**, VTuberBot ofrece **Abrir OBS Studio**, **Elegir ubicación de OBS**, y dos preferencias persistentes:

- **Abrir OBS automáticamente al iniciar VTuberBot**: activado por defecto. Si OBS está instalado en una ubicación estándar, VTuberBot intenta abrirlo sin bloquear el arranque. Si no lo encuentra, permite seleccionar `obs64.exe`.
- **Modo ligero: iniciar OBS sin plugins de terceros**: activado por defecto. Al iniciar OBS, VTuberBot pasa `--only-bundled-plugins`, una opción de inicio que omite plugins externos durante esa sesión. No desinstala ni borra archivos. Desactivala si tus escenas usan plugins de terceros.
- **Aplicar perfil 720p / 30 FPS**: al confirmarlo, VTuberBot envía a OBS el lienzo y la salida 1280×720 a 30 FPS, y lee de vuelta los valores para comprobarlos. No se informa éxito si OBS rechaza el cambio o devuelve valores distintos. El ajuste afecta a la configuración global de vídeo y puede cambiar cómo se ven todas las escenas.
- **Conexión automática**: después de guardar por primera vez host, puerto y contraseña de WebSocket desde la página OBS, VTuberBot intenta autenticarse al arrancar. Si la contraseña no existe, el puerto no responde o la autenticación falla, la app sigue funcionando y deja un estado/registro; no simula una conexión.

**Límite importante:** omitir plugins externos y usar 720p30 puede reducir carga en algunos escenarios, pero no garantiza menor uso de CPU/GPU. El perfil no cambia bitrate, codificador, filtros de escenas, fuentes ni calidad de salida de Twitch. La optimización específica para Ryzen 5 5600G/16 GB todavía requiere mediciones de carga y ajustes adicionales en el equipo. La compilación y CI deben comprobarse en el SHA más reciente antes de distribuir este cambio.


## Biblioteca local de arte

En **Avatar Studio**, debajo de las referencias públicas, está la biblioteca local de assets. Importá una o varias imágenes propias o generadas con una herramienta externa. Podés buscar por nombre, categoría, etiquetas y estado de licencia; filtrar favoritos; ver una miniatura del asset seleccionado; y guardar categoría, etiquetas, URL de procedencia y estado de licencia. La app no descarga automáticamente ilustraciones ni verifica por sí sola las condiciones de PixAI u otros servicios.

Cada importación empieza con **Pendiente de revisar**. Cambiá el estado solo después de comprobar la licencia de la imagen y las condiciones del modelo/servicio de origen. Las imágenes se copian al directorio local de datos de VTuberBot. Seleccioná un asset y un rol para asignarlo al perfil activo; la copia asignada no elimina el original de la biblioteca. Se admiten PNG, JPG/JPEG y BMP hasta 25 MB por archivo.

## Empezar: elegí tu tipo de VTuber

Arriba de **Avatar Studio** hay una galería de diecinueve personajes chibi completos. El primero es **Miel de café** (piel marrón medio-profunda, pelo castaño con mechones interiores miel claro, iris marrones y pupilas marrón oscuro), seguido de **Neko gamer**, **Brujita lunar**, **Idol estelar**, **Gótica rosa**, **Cyber neón**, **Streamer casual**, **Zorrita de festival**, **Oráculo invernal**, **Guardiana kitsune**, **Pop celestial**, **Capitana pirata** y **Conejita de café**. Cada uno combina peinado, ropa, accesorios, rostro, ojos, tono de piel y pose distintos. No son una misma ilustración recoloreada: cada modelo combina distinto peinado, ropa, accesorios, rostro, ojos, tono de piel y pose. Al pulsar una tarjeta se crea un perfil propio y se abre el overlay; tus perfiles previos no se reemplazan.

### Buscar referencias de VTubers conocidos sin copiar su arte

Debajo de la galería aparece **Referencias públicas**. Escribí un nombre (por ejemplo, Gawr Gura, Ironmouse, Hoshimachi Suisei o Shylily) y pulsá **Buscar referencia en la web**. La búsqueda abre el navegador para estudiar silueta, paleta, peinado, ojos y accesorios; VTuberBot no descarga ni corta automáticamente el arte oficial.

Después podés partir de seis combinaciones originales: **Océano pop**, **Reaper rock**, **Cantante estelar**, **Dragona cyber**, **Demonio gamer** y **Kitsune idol**. Son plantillas editables inspiradas en arquetipos visuales, no reproducciones exactas ni modelos oficiales. Cambiá cabello, ropa, ojos, tono de piel y accesorios con el armario modular, y guardá tu propia combinación. Para importar ilustraciones de terceros, verificá la licencia y respetá la atribución.

Cada retrato de la galería muestra la etiqueta visual **VTUBERBOT · REFERENCIA**. Es una superposición de la interfaz: no se graba en los PNG ni altera el recurso original. El enlace **Ver PNG original sin marca ↗** abre el PNG base local con el visor predeterminado del sistema. La galería está compuesta por recursos generados por el propio programa; por eso no muestra un enlace a una supuesta fuente oficial externa. Si importás arte de terceros, conservá su autoría y licencia y obtené permiso cuando corresponda. Una marca de agua ayuda a identificar el contexto del recurso, pero por sí sola no concede derechos ni evita una infracción de copyright.

Debajo de esa galería está **Crear PNGTuber con mis imágenes**. Elegí una imagen de reposo y, si ya tenés una, otra con la boca abierta para hablar. Con una sola imagen el avatar queda visualmente estático; con dos, el detector de audio alterna entre ellas.

### Abrir un modelo VRM en el overlay 3D

1. Creá o exportá un archivo `.vrm` en VRoid Studio u otra herramienta compatible. El taller paramétrico integrado exporta GLB estático, no VRM.
2. En **Avatar Studio**, pulsá **Cargar modelo VRM y abrir overlay 3D** y elegí el archivo.
3. VTuberBot valida que el archivo exista y pese menos de 200 MB, copia el modelo a una carpeta propia bajo los datos locales y abre una ventana transparente siempre visible.
4. El overlay refleja los estados **Idle/Talking** de VTuberBot. Podés mover y redimensionar la ventana; el modelo permanece local y no se sube a un editor web.

El renderizador usa WebView2 y descarga Three.js/three-vrm desde CDN; necesita Microsoft Edge WebView2 Runtime e Internet. No incluye tracking facial, lip-sync por fonemas, edición de malla ni ajuste automático de ropa. Probalo en Windows con tu propio modelo antes de usarlo en una transmisión.

### Crear tu propio personaje chibi

El panel **Creador de personaje chibi — cambia cada parte** permite combinar independientemente:

- **Peinado:** 12 siluetas, como coletas dobles, bob, ondas, trenza, pelo corto o largo; incluye castaño oscuro con inner hair miel claro.
- **Ropa:** 15 diseños con formas propias, entre ellos idol, marinero, gótico, ceremonial, streetwear, fantasía, hoodie, cyber y vestidos.
- **Rostro y ojos:** 4 formas de rostro, 8 estilos de ojos y 6 tonos de piel, incluida la nueva opción **Morena profunda**, además de ojos marrones con pupilas marrón oscuro.
- **Pose:** de pie, saludo, señal de paz, manos en la cintura, brazos cruzados o cantando al micrófono.
- **Accesorios:** adornos de cabeza, joyería facial y piezas traseras como alas, capa, mochila o cristales.
- **Colores:** tintes por zona con HEX o muestras rápidas, sin sobreescribir las imágenes originales.

Pulsá **Crear perfil con esta combinación** para guardar otra versión; se crean PNG transparentes separados y podés mover las capas después. El catálogo es arte vectorial original generado localmente; tiene un estilo chibi 2D estilizado y no es un rig Live2D con deformaciones óseas ni seguimiento facial.

### Otros modos

1. **2.5D con IA local:** seleccioná una ilustración para intentar separar cabello, ropa, piel/cuerpo y detalles. La primera vez se descarga un modelo pequeño y después el procesamiento se realiza en la PC. Tiene un límite de 2 GiB para el proceso de IA y diez minutos; es experimental y puede requerir correcciones.
2. **Taller 3D integrado:** permite cambiar silueta, tono de piel, cabello, mechones interiores (inner hair), iris, pupilas, expresión, ropa, colores y accesorios; incluye el preset **Miel de café**. Podés guardar la apariencia en JSON y exportar un modelo GLB estático. El GLB exportado no tiene rig ni animaciones, y el taller carga Three.js desde Internet.
3. **VRoid Studio:** abre [VRoid Studio](https://vroid.com/en/studio), un creador externo de modelos 3D. También podés cargar un archivo `.vrm` propio desde Avatar Studio para abrirlo en un overlay 3D transparente. El overlay sincroniza Idle/Talking con la voz, pero no tiene tracking facial ni animación avanzada de expresiones.

**La ruta recomendada para empezar es elegir un personaje de la galería y luego personalizarlo.** PNGTuber es lo más ligero; 2.5D sigue siendo experimental. Para VRM animado, creá/exportá un modelo `.vrm` con VRoid u otra herramienta y cargalo desde el botón de Avatar Studio.

## Primer inicio y datos

- Ejecutá `VtuberBot.App.exe` en Windows.
- La base SQLite, los logs, preferencias, perfiles y assets importados se guardan bajo `%LocalAppData%\VtuberBot\`.
- El token de Twitch y la contraseña de OBS se cifran con DPAPI para el usuario de Windows actual. No copies archivos de secretos entre usuarios ni los subas a GitHub.
- El paquete portable no debe incluir `config.toml`, base de datos, logs, tokens ni cookies.

### Administrador de almacenamiento

En **Ajustes → Administrador de almacenamiento**, pulsá **Analizar almacenamiento** para ver cuánto ocupa el directorio local y el tamaño por categoría. El umbral configurable es solo un aviso: no detiene VTuberBot ni borra datos en segundo plano.

- La lista de limpieza es una propuesta, no una acción automática. Revisá los motivos, seleccioná archivos y confirmá la vista previa. El programa vuelve a comprobar tamaño y fecha de los archivos antes de borrarlos; si cambiaron, los omite y pide volver a analizarlos.
- Se pueden proponer sonidos que ya no están en la biblioteca, imágenes sin referencias confiables, archivos temporales de más de 24 horas, cachés de más de 7 días y registros antiguos de más de 30 días. Un catálogo/JSON dañado hace que el análisis sea conservador para no perder arte.
- La base SQLite activa y sus archivos auxiliares, los respaldos, los secretos protegidos, los enlaces reconocidos desde perfiles/capas/bocas de todos los avatares y los archivos fuera del directorio local no se ofrecen como residuos. Los elementos no reconocidos no siempre son basura: si no se puede confirmar que estén huérfanos, se conservan.
- **Compactar SQLite…** crea una copia de seguridad verificada antes de ejecutar `VACUUM`. Esto puede reducir el tamaño físico si la base tiene páginas libres, pero no garantiza una reducción; la copia de seguridad se conserva en `backups`.
- La vista de espacio mide el directorio de datos configurado, no otros directorios ni el tamaño que usan OBS o el proceso VOICEVOX. Los borrados se limitan a archivos individuales; no existe un botón que elimine una carpeta completa.

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

## Histórico — PNGTuber Plus (no es una dependencia del candidato actual)

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

### Comandos de reacción anime bilingües
En el primer arranque tras esta actualización, **Centro de directo → Editor de reglas** incorpora diez reglas iniciales habilitadas si aún no se migraron (también se agregan a configuraciones existentes sin sobrescribir las reglas propias): `/ohayo`, `/baka`, `/nani`, `/okaerinasai`, `/soybot` y sus alias `!ohayo`, `!baka`, `!nani`, `!okaerinasai`, `!soybot`. Las cuatro primeras son reacciones cortas en japonés; `/soybot` aporta personalidad en español: «Yo... soy solo un bot... jeje». Las respuestas conversacionales más largas deben seguir en español. Cada regla puede pedir una expresión del avatar y configurar idioma, voz SAPI o VOICEVOX y clip de audio opcional, además de frase, cooldown global/por usuario y máximo por minuto.

**Audio por regla:** en el editor, activá **Reproducir audio de esta reacción**, elegí Automático/Español/Japonés y una voz. Cada reacción guarda su propia voz; elegir un clip de la biblioteca local hace que ese clip sustituya al TTS. Importá antes el WAV/MP3 desde **Voz y audio**. Los clips y el TTS comparten un turno de audio: el clip espera a que la cola de voz existente quede libre (máximo 90 segundos), mantiene el turno durante la reproducción y deja que el TTS que llegue mientras tanto espere detrás. Si no puede obtener el turno, registra que se omitió. Los clips se resuelven por su ID en la biblioteca local y el servicio no ejecuta rutas arbitrarias guardadas en una regla. Las voces SAPI concretas requieren **ChatSpeak Studio → Proveedor de voz → Local**. Las voces VOICEVOX se enrutan directamente por la cola de audio y funcionan aunque el proveedor global sea remoto; el texto se sintetiza localmente y no necesita una clave API.

**Probar una voz japonesa gratuita con VOICEVOX:** descargá el programa/motor desde el [sitio oficial de VOICEVOX](https://voicevox.hiroshiba.jp/) o consultá el [motor oficial y su API](https://github.com/VOICEVOX/voicevox_engine). Instalá y abrí VOICEVOX Engine/Editor en Windows con el servidor local predeterminado `http://127.0.0.1:50021`. Después abrí de nuevo **Centro de directo**: las voces disponibles aparecerán en la lista con la etiqueta **VOICEVOX**. Seleccioná una y pulsá **Escuchar muestra japonesa de la voz seleccionada**; la muestra usa 「お兄ちゃんのばか！ なに!? おかえりなさい！」. El motor debe seguir abierto para sintetizar. Si no aparecen voces, verificá que `http://127.0.0.1:50021/speakers` responda y reabrí la página. VOICEVOX no está incluido ni descargado automáticamente por VTuberBot: se mantiene separado por tamaño y licencias de cada voz/personaje. Revisá y cumplí los términos de crédito/uso de la voz elegida antes de publicar o monetizar contenido.

Los comandos se procesan cuando el mensaje llega a la canalización de chat de VTuberBot. Algunos clientes de Twitch reservan los mensajes que empiezan por `/` como comandos propios y podrían no enviarlos al chat; en ese caso usá el alias `!`. La expresión solo puede cambiar si el avatar local tiene la imagen base/expresión disponible; esto no equivale todavía a controlar VTube Studio.


- **Comandos:** creá respuestas con variables `{user}`, `{points}`, `{roll}`, `{args}` y `{message}`. Podés añadir hasta 20 alias separados por coma; cada alias se guarda y ejecuta como el mismo comando, y los conflictos con nombres/alias existentes se rechazan. También podés restringir su uso a Todos, Suscriptores, VIP, Moderadores o solo Broadcaster; los roles se identifican con etiquetas oficiales de Twitch y los comandos rechazados no consumen cooldown. El cooldown puede ser global (un intervalo para todo el chat) o por usuario; ambas modalidades y sus marcas se guardan en SQLite. La previsualización usa el texto aún no guardado y no consume cooldown ni envía mensajes.
- **Comunidad y puntos:** los perfiles, el ranking, los ajustes manuales y el ledger de puntos se conservan en la base local. La actividad del chat puede otorgar puntos automáticamente. Los comandos integrados `!puntos` y `!top` consultan el saldo y los cinco primeros del ranking desde Twitch o la prueba local de Comandos.
- **Sorteos:** creá un sorteo, registrá participantes manualmente o mediante `!entrar` en el chat y cerralo con selección uniforme usando el generador criptográfico del sistema. Las entradas duplicadas se descartan y un sorteo cerrado no puede volver a sortearse. Si Twitch está desconectado, crear/cerrar/cancelar modifica el historial local y el estado indica explícitamente que no se publicó un anuncio remoto.
- **Minijuegos:** Duelo de moneda (Cara/Cruz), Ruleta europea (Rojo/Negro/Verde, con 0 verde) y Dado (Bajo 1–3/Alto 4–6) tienen reglas distintas y elecciones visibles. En el chat también se puede usar `!jugar moneda cara 10`, `!jugar ruleta rojo 10` o `!jugar dado alto 10`; una apuesta `0` es práctica gratis. Las apuestas son de 0 a 10.000 puntos: 0 juega en práctica sin modificar el saldo; apostar requiere tener saldo suficiente. Una victoria de moneda/dado o rojo/negro en ruleta paga 2× la apuesta total; el verde 0 paga 36×. Si perdés, se descuenta la apuesta y no hay premio. Débito, premio, ledger e historial se guardan en una transacción SQLite para que un fallo no deje el saldo a medias. No requieren Twitch conectado; la publicación es opcional cuando ya está conectado.
- **Moderación:** las reglas regex se prueban localmente. La moderación automática queda desactivada por defecto y solo se habilita tras confirmación. Cada regla puede ser `flag` (registro), `warn` (aviso en chat), `delete` (elimina el mensaje exacto), `timeout` (10 minutos) o `ban` (permanente). Si se habilita la opción global, se limita a una acción no informativa por mensaje y una acción por usuario/regla cada cinco minutos.
- Las acciones manuales permiten timeout de duración elegida, ban permanente y retirar un ban/timeout, con confirmación visible antes de afectar el chat real. Si agregaste scopes después de autorizar, volvé a completar OAuth. Los errores de permiso se muestran como fallos; nunca se simula el éxito.
- La eliminación automática de mensajes usa el endpoint oficial [Delete Chat Messages](https://dev.twitch.tv/docs/api/reference#delete-chat-messages), necesita `moderator:manage:chat_messages` y solo envía el ID de mensaje exacto recibido desde las etiquetas IRC; nunca hace una solicitud vacía que pudiera borrar todo el chat.
- **Automatizaciones:** los mensajes programados respetan un intervalo mínimo de cinco minutos y solo se envían cuando Twitch está conectado. Si está desconectado, la omisión queda registrada.

## Automatizaciones por eventos reales

La página **Automatizaciones** permite crear reglas que escuchan eventos de Streamer.bot, notificaciones EventSub o mensajes reales del chat IRC de Twitch. Para chat, elegí la fuente `Twitch.IRC`: VTuberBot completa `Twitch.ChatMessage` y expone los campos `userName`, `message`, `messageId`, `isCommand`, `command` y `arguments` para condiciones como `message|contains|!escena`. Estos eventos se publican después de la política de moderación que suprime efectos de mensajes sancionados. Cada regla combina fuente, categoría, tipo exacto, condiciones JSON opcionales, cooldown y límite por minuto. Las reglas nuevas nacen desactivadas y las automatizaciones arrancan pausadas.

Destinos disponibles:
- **Streamer.bot:** seleccioná una acción existente de `GetActions`; antes de activar una regla se confirma el ID exacto y, al ejecutarla, se vuelve a validar que siga habilitada. Una respuesta aceptada significa que Streamer.bot aceptó la solicitud, no que el efecto final en OBS esté garantizado.
- **PNGTuber Plus:** seleccioná un atajo previamente registrado. El envío automático requiere autorización global explícita; enviar la tecla no demuestra que la expresión haya cambiado.
- **OBS · cambiar escena:** requiere OBS WebSocket autenticado. La lista se obtiene de las escenas reales de OBS y se vuelve a validar al guardar y ejecutar. La actividad informa que OBS aceptó la solicitud; no equivale a una comprobación visual de la salida.
- **Avatar · expresión:** solo ofrece estados con recursos disponibles en el perfil actual (Idle, Talking, Blink, Happy o Surprised). Si falta la imagen, la acción se rechaza en vez de fingir un cambio.

La cola, las condiciones y los límites evitan bloquear al productor de eventos y reducen ciclos. La actividad distingue acciones rechazadas, límites, timeouts y solicitudes aceptadas. No uses dos proveedores que emitan el mismo evento para la misma regla si no comparten un ID de deduplicación.

## Voz y audio

- Seleccioná el micrófono disponible e iniciá la captura real.
- Ajustá el umbral acústico y el tiempo de retención para que el estado Talking no parpadee por pequeñas pausas.
- El detector local suaviza el nivel de señal y activa el cue Talking del overlay; no reconoce palabras ni equivale a lip-sync por fonemas.
- La cola TTS procesa frases una por una en un hilo STA de fondo para no congelar el panel. Las prioridades Alta/Normal/Baja afectan las frases que todavía esperan. **Detener voz actual** solicita cancelar la frase activa; **Vaciar frases pendientes** solo elimina las frases que esperan y no interrumpe la actual.
- **Comprobar y actualizar voces VOICEVOX** consulta los estilos reales que expone el motor local. Elegí uno y probá el sonido; el motor y sus voicebanks se instalan por separado. Las voces habituales se orientan principalmente al japonés, por lo que no se garantiza pronunciación española natural.
- **Sintonizador de voz anime:** los perfiles Natural, Anime aguda, Anime grave y Anime enérgica ajustan tono, entonación, velocidad y volumen por estilo de voz. Los cambios se guardan localmente; la síntesis completa tiene un límite de espera de 60 segundos por frase.
- **Windows SAPI** queda desactivado como respaldo automático porque sus voces pueden sonar sintéticas. En **Voz y audio**, habilitá explícitamente **Permitir voz de Windows SAPI como respaldo** solo si querés permitirlo. Si no hay un motor local válido, la app informa el error en lugar de cambiar de voz silenciosamente.
- **Encolar voz** usa la voz VOICEVOX configurada cuando hay un estilo seleccionado. El micrófono solo controla el estado Talking del avatar: no transforma la voz del micrófono en tiempo real.
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

## Galería de personajes chibi

En **Avatar Studio** hay una galería de trece personajes chibi completos. Incluye **Miel de café** (piel morena cálida, cabello castaño con mechones interiores miel claro, iris marrones y pupilas marrón oscuro), además de Neko gamer, Brujita lunar, Idol estelar, Gótica rosa, Cyber neón, Streamer casual, Zorrita de festival, Oráculo invernal, Guardiana kitsune, Pop celestial, Capitana pirata y Conejita de café. Cada preset combina peinado, ropa, accesorios, rostro, ojos, tono de piel y pose. Al elegir una tarjeta se crea un perfil propio; no reemplaza los perfiles anteriores. Son ilustraciones chibi generadas localmente, no un rig Live2D profesional.

## Simulador seguro de reglas de automatización

En **Automatizaciones**, al editar una regla, usá **Simulador seguro de condiciones**. Pegá un objeto JSON de ejemplo, por ejemplo:

```json
{"message":"hola chat","userName":"viewer"}
```

Pulsá **Simular condiciones (sin ejecutar acciones)**. Se usa la fuente, categoría, tipo y las condiciones actuales. El simulador no llama a Streamer.bot, no envía hotkeys, no modifica reglas ni ejecuta acciones externas. Si no coincide, revisá los nombres de los campos y el operador `eq`, `contains` o `present`. Los payloads se limitan a 65.536 caracteres.



## Creador de waifu PNG por objetos

Abrí **Avatar Studio** y pulsá **Abrir creador de waifu PNG por objetos**.

- **Importar objetos:** admite PNG, JPG/JPEG, BMP y GIF. Los GIF animados se reproducen en la vista previa; al exportar PNG se captura el fotograma que se ve en ese momento.
- **Catálogo local:** agrega accesorios de cabeza, ojos, bocas, efectos y fondos de muestra. Cada pieza se agrega como capa independiente. Los fondos se colocan detrás del resto. El catálogo se genera localmente con formas vectoriales originales: es material de prototipado, no una colección de ilustraciones anime profesionales.
- **Componer:** arrastrá las piezas, ajustá escala y opacidad, cambiá el orden o eliminá capas. Importá primero tu ilustración base y después las piezas que quieras superponer.
- **Quitar fondo blanco (bordes):** seleccioná la capa y usá el botón. Se elimina el blanco casi puro conectado al borde de la imagen, pero se conserva el blanco interior encerrado por otros píxeles. Los contornos antialiasados o tonos casi blancos pueden necesitar retoque; comprobá la vista previa antes de exportar. Para proteger memoria, el procesamiento rechaza imágenes de más de 20 millones de píxeles.
- **Guardar proyecto:** genera un JSON y una carpeta de recursos con copias PNG de las capas. Conservá ambos juntos si movés el proyecto a otra carpeta o PC.
- **Exportar PNG:** exporta la composición de 640×640 con transparencia. No exporta animación GIF ni un archivo PSD con capas.

**Validación del bloque:** la implementación está registrada en la rama de producto, pero todavía no se debe considerar verificada hasta que Windows CI compile y ejecute las pruebas del SHA final. La galería es un catálogo vectorial básico y requiere revisión visual en la aplicación.


**GIF y proyectos:** si guardás una capa GIF sin procesarla, el proyecto copia el GIF original y vuelve a animarlo al abrirlo. Si aplicás eliminación de fondo a un GIF, la capa pasa a ser estática porque el recorte se aplica al fotograma actual.


## PNGTuber 2D: recorrido principal simple

Al abrir VTuberBot se muestra **PNGTuber 2D**. Esta es la ruta para salir en pantalla con un personaje 2D, sin configurar un modelo 3D ni usar IA.

1. **Importá tu personaje.** Usá el PNG completo con transparencia. No se altera el archivo original.
2. **Asigná las bocas dibujadas (opcional).** Elegí un PNG recortado de boca cerrada y otro de boca abierta. VTuberBot muestra la boca cerrada en `Idle` y la abierta en `Talking`. No inventa ni dibuja una boca realista.
3. **Acomodá la boca.** Arrastrá el sticker sobre la vista previa, ajustá X/Y y escala, y guardá la posición. Se mantiene proporcional al tamaño con el que se presenta el avatar en pantalla; no se estira por toda la ventana.
4. **Probá movimiento.** Elegí vibración al hablar, rebote suave al hablar y flotación en reposo. La intensidad de vibración tiene un máximo para evitar desplazamientos excesivos. El botón de prueba hace una sacudida breve.
5. **Mostrá el avatar transparente.** Activá la ventana de avatar y en OBS agregala como **Captura de ventana**. Configurar WebSocket en la página OBS permite controlar OBS desde VTuberBot, pero no sustituye agregar la fuente del avatar a la escena.

**Si ves dos bocas:** el personaje base ya trae una y el sticker agrega otra. Usá una base sin boca o prepará una copia transparente en tu editor de imágenes. VTuberBot no sobrescribe la imagen original ni incluye todavía un recortador de boca integrado en esta página. Lo ideal es que los stickers de boca sean PNG con transparencia y estén recortados alrededor del dibujo, no una captura de toda la cara.

**Si la boca sale enorme:** asegurate de estar importando un sticker recortado, no la imagen completa del rostro. El render de la boca conserva sus dimensiones en relación con el PNG base.

**Si no cambia al hablar:** comprobá que el archivo de boca abierta esté asignado, que el overlay esté visible y que la detección de micrófono/voz marque el estado `Talking`. La comprobación automatizada no valida el micrófono físico de tu PC.

**Si el avatar no aparece en OBS:** primero confirmá que la ventana transparente esté abierta; luego agregá la fuente Captura de ventana en la escena correcta. WebSocket y Captura de ventana son dos configuraciones diferentes.

Los ajustes de movimiento se guardan en la base de datos local. `Reducir movimiento` los desactiva. Se conservan solo los dos estados faciales principales: `Idle` y `Talking`; la vibración y el rebote son movimientos de todo el dibujo, no físicas independientes por pieza.
