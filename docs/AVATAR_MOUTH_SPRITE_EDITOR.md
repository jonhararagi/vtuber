# Editor de boca PNG por piezas — requisitos de producto

**Rama objetivo:** `product/complete-vtuber-experience`  
**Estado:** especificación de implementación; no declarar la función terminada hasta integrar UI, persistencia, runtime y pruebas.

## Objetivo

Permitir que una persona sin conocimientos de dibujo técnico prepare un PNGTuber anime usando su propia ilustración y piezas transparentes separadas. La boca debe poder colocarse y ajustarse manualmente, guardarse como PNG mediante el botón `＋` y asignarse a estados de voz sin depender de expresiones caricaturescas.

## Flujo esperado

1. Abrir Avatar Studio → creador de waifu PNG por objetos.
2. Importar el rostro/cuerpo base o una ilustración completa.
3. Desactivar/eliminar las capas de muestra que no correspondan. No agregar ojos, brillos, rubores, corazones ni efectos automáticamente.
4. Importar un PNG transparente de boca, arrastrarlo hasta la posición exacta y ajustar escala, tamaño, opacidad y orden de capa.
5. Pulsar `＋ Guardar pieza`: guardar una copia PNG en la biblioteca local de assets, con nombre editable y categoría Boca. La pieza guardada no debe depender de la ruta original.
6. Asignar piezas guardadas a estados:
   - **Callado / Idle:** boca cerrada.
   - **Hablar / Talking:** boca abierta.
   - **Opcionales:** boca semiabierta, vocales A/I/U/E/O, sonrisa discreta o fonemas adicionales.
7. Previsualizar ambos estados alternándolos manualmente y guardar el perfil. El overlay debe usar los sprites guardados cuando cambia el estado de voz.

## Edición y manipulación

- Seleccionar la boca en el lienzo y moverla arrastrando.
- Controles explícitos para posición X/Y, escala y rotación; permitir valores precisos además del ratón.
- Botones para añadir, duplicar, renombrar, ocultar y eliminar piezas.
- Reordenar capas sin aplanar el resto de la ilustración.
- Mantener transparencia alfa. No eliminar blancos interiores de la boca ni convertir automáticamente un fondo blanco en transparente sin una acción deliberada.
- El botón de guardar debe confirmar el resultado y actualizar la biblioteca inmediatamente.
- No sobrescribir un PNG existente silenciosamente.

## Tipos de boca, no estilos caricaturescos obligatorios

La biblioteca puede incluir muestras neutrales y discretas: línea cerrada, pequeña abertura, abertura vertical, abertura horizontal, sonrisa leve y cinco formas vocálicas. Son puntos de partida editables, no expresiones aplicadas automáticamente. El usuario debe poder importar arte propio y descartar todas las muestras.

La animación avanzada se implementa como opción. Para el modo básico solo se requiere alternar dos sprites: `Idle` (cerrada) y `Talking` (abierta). Si hay varios segmentos/fonemas, el usuario debe poder seleccionar la secuencia y su duración, con fallback a los dos estados básicos cuando no haya audio/fonemas disponibles.

## Persistencia y seguridad

- Guardar las piezas bajo el almacenamiento local de VTuberBot; mantener la ruta de recursos relativa en el proyecto exportado.
- Persistir categoría, nombre, posición, tamaño, escala, rotación, orden, visibilidad y asignaciones Idle/Talking en el perfil.
- Validar PNG/JPEG/BMP según las políticas existentes; preservar transparencia cuando el formato lo soporte.
- No introducir APIs pagas ni generación automática obligatoria.
- No alterar perfiles previos ni `main`; aplicar cambios solo a `product/complete-vtuber-experience`.


## Encuadre de cámara y composición para OBS

El creador por objetos incorpora un marco azul de encuadre dentro del lienzo de 640 × 640, con presets de **Rostro**, **Busto**, **Cuerpo completo** y **Personalizado**. Los controles X/Y/ancho/alto permiten decidir qué porción de la ilustración queda dentro del recorte; el marco no se exporta como parte del personaje.

También muestra una previsualización simplificada del lienzo OBS de 1920 × 1080. Permite ajustar la posición X/Y y el ancho/alto previstos para la fuente del avatar, e informa el porcentaje aproximado del lienzo total que ocuparía. El encuadre y la colocación se guardan dentro del archivo de proyecto `.waifu.json`.

El botón **Exportar encuadre de cámara** genera un PNG transparente del recorte seleccionado, para importarlo como fuente de imagen en OBS. Esto permite mostrar solo rostro, busto u otra porción en lugar de todo el cuerpo.

**Límite actual:** la vista OBS es una previsualización y el PNG exportado es una imagen estática. Este cambio no crea por sí solo una cámara virtual en tiempo real, no sigue el estado de voz y no mueve ni redimensiona automáticamente una fuente dentro de OBS. Esas capacidades requieren conectar el encuadre a la ventana overlay/runtime y/o al control de fuentes de OBS, confirmando la resolución real de la escena.

## Criterios de aceptación

- Se puede importar una imagen de rostro y un PNG de boca por separado.
- La boca se puede mover y ajustar hasta coincidir con la ilustración.
- `＋ Guardar pieza` crea un PNG reutilizable en la biblioteca local.
- La boca cerrada y abierta se asignan de forma independiente y se previsualizan correctamente.
- Guardar/cerrar/reabrir el proyecto conserva la posición y las asignaciones.
- Cambiar Idle/Talking en la vista previa cambia únicamente la boca, no la cara completa.
- No aparecen ojos, rubores, corazones, brillos ni efectos por defecto en el perfil sobrio.
- Hay pruebas para persistencia, exportación PNG transparente, asignación de estados y ausencia de pérdida de recursos.
- Windows CI se comprueba contra el SHA exacto del cambio final; no sustituye la validación manual del overlay y del micrófono.
- El encuadre de cámara se guarda/reabre, el PNG recortado conserva alfa y el porcentaje/posición de OBS corresponde a la resolución seleccionada.

## Nota de implementación

El creador por objetos existente ya permite importar capas, arrastrarlas, cambiar escala/opacidad y exportar un PNG compuesto. Eso no demuestra que exista todavía el flujo completo de biblioteca de piezas, categorías, asignación de boca Idle/Talking ni integración automática con el estado de voz. Esas piezas deben implementarse y probarse antes de describir esta capacidad como terminada.
