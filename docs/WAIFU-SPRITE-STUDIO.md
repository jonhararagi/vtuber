# Creador de waifu PNG por objetos

## Qué es y dónde abrirlo

En **Avatar Studio**, abrí **Creador de waifu PNG por objetos**. Es un editor 2D local para combinar una ilustración propia con accesorios y expresiones independientes y exportar una composición PNG con transparencia. Los recursos de inicio son dibujos vectoriales originales generados por VTuberBot; el editor también permite importar piezas tuyas. No necesita clave de IA ni sube imágenes a la web.

## Biblioteca inicial

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

## Convertir el PNG a 2.5D experimental con IA local

Pulsá **Crear PNGTuber + IA local 2.5D**. El editor exporta la composición y se la entrega al flujo de segmentación local que ya existe en Avatar Studio. Cuando el modelo está disponible, el procesamiento se ejecuta localmente sobre CPU y crea capas aproximadas de piel/cuerpo, ropa, cabello y detalles. El perfil resultante puede necesitar corregir alineación, recortes o zonas ocultas.

**Límites importantes:**

- Es segmentación semántica experimental, no generación de nuevas partes, reconstrucción 3D completa ni estimación fiable de profundidad.
- La primera ejecución puede necesitar conexión a Internet para descargar el modelo ONNX verificado; luego se reutiliza la copia local en caché. No requiere una clave de API de IA ni llama a un proveedor remoto de generación.
- No crea un rig Live2D, seguimiento facial ni lip-sync por fonemas. Sigue siendo un avatar 2D por capas con movimiento ligero.
- Si la segmentación falla o no encuentra suficientes píxeles útiles, se informa el error y no se debe presentar un perfil segmentado como resultado exitoso.

## Validación recomendada

Probá con una copia de una imagen PNG con fondo blanco, verificá que el original siga intacto, comprobá un detalle blanco interior, añadí ojos y boca, arrastrá un sombrero, ocultá una capa, exportá y reabrí el proyecto JSON. Después comprobá que el PNG exportado tenga canal alfa y, por separado, que el flujo 2.5D local termine sin error. La CI automatizada no sustituye esta comprobación visual en Windows.
