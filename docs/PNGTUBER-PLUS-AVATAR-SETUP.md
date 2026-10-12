# Avatar externo: PNGTuber Plus

Este flujo configura una instalación de PNGTuber Plus que el usuario ya posee. VTuberBot no descarga, instala, modifica ni redistribuye sus binarios. El editor nativo de **Avatar Studio** sigue disponible y conserva perfiles/capas propios.

## Fuentes y límites comprobados

- Portal de información y descarga en español: https://pngtuber.cc/es/
- Página de distribución del creador (kaiakairos): https://kaiakairos.itch.io/pngtuber-plus
- El apartado “About” del propio portal se identifica como un sitio independiente de fans, no como el desarrollador: https://pngtuber.cc/about/
- Código fuente público del autor: https://github.com/kaiakairos/PNGTuber-Plus
- Archivo revisado para controles/atajos: https://github.com/kaiakairos/PNGTuber-Plus/blob/master/main_scenes/main.gd

El portal consultado anuncia la versión **1.4.5** y describe PNG transparentes por capas, reacción al micrófono, parpadeo, disfraces y atajos. Esa versión publicada en el portal no prueba que una copia local o cualquier paquete del repositorio sea exactamente la misma versión. Comprobá la versión que instalaste.

En el archivo de referencia main.gd, la rama pública define teclas de disfraces por defecto de 1 a 0, maneja entradas de teclas y emite señales internas para alternar la visibilidad de sprites. En las fuentes revisadas no se encontró un contrato público documentado de IPC, WebSocket o API de control externo. Por eso VTuberBot no afirma que una conexión protocolaria exista.

## Instalar o localizar la copia existente

1. Si todavía no la tenés, abrí el portal de referencia en español y descargá/instalá manualmente la versión que decidas usar. VTuberBot no descarga ni instala nada.
2. Si usás el ZIP de Windows del portal, extraé el paquete antes de ejecutarlo.
3. Mantené juntos en la carpeta de instalación el ejecutable, el archivo .pck y las bibliotecas .dll que formen parte de ese paquete. No copies el ejecutable a otra carpeta por separado ni lo ejecutes desde dentro del ZIP.
4. En **Avatar externo**, seleccioná el ejecutable real. Si lo movés, actualizá la ruta. El estado **Disponible** solo indica que la ruta apunta a un archivo; **En ejecución** indica que se detectó el proceso de esa ruta.

## Asistente de avatar

El progreso del asistente se guarda en SQLite local y se recupera al reabrir VTuberBot. Los pasos automáticos solo comprueban la ruta/estado del proceso. Los demás pasos requieren confirmación del usuario y no se infieren a partir de que el proceso esté abierto.

1. **Localizar la aplicación.** Seleccioná el ejecutable y verifica que la ruta exista.
2. **Abrir la aplicación.** Pulsa “Abrir PNGTuber Plus”, comprueba que aparece su ventana y marca la confirmación. Un proceso detectado no marca el paso por sí solo.
3. **Preparar el avatar.** Importa imágenes PNG con transparencia. Añade cada parte como sprite, ajusta posición, escala, rotación y orden de capas, y vincula las partes que deban moverse juntas. Guarda el proyecto/avatar desde PNGTuber Plus.
4. **Probar el micrófono.** En PNGTuber Plus abre la selección de entrada, elige el micrófono correcto, habla y confirma visualmente que el avatar cambia entre silencio y habla. VTuberBot no puede leer la selección interna del micrófono ni observar esa reacción.
5. **Preparar expresiones/disfraces.** Añade sprites alternativos, agrúpalos en disfraces/expresiones, asigna atajos dentro de PNGTuber Plus y prueba también la visibilidad individual de los sprites. La lista de VTuberBot es un registro local, no una lectura de las asignaciones internas del otro programa.
6. **Verificar OBS.** Añade una fuente de captura de ventana de PNGTuber Plus y selecciona la ventana correcta. Usa transparencia si el método de captura la conserva. Si no, establece un fondo verde sólido en PNGTuber Plus, captura la ventana y aplica a esa fuente un filtro **Chroma Key** en OBS; evita un verde que aparezca en el avatar. Recorta la interfaz sobrante, coloca el avatar y prueba la escena completa.

Los botones **Anterior** y **Siguiente** permiten regresar y continuar. El avance está bloqueado si la ruta no es válida o el paso actual sigue pendiente. Las confirmaciones manuales se pueden retirar si el paso deja de ser válido.

## Registro y prueba de atajos

- El código público revisado documenta atajos para disfraces (por defecto 1–0) y una ruta de entrada para alternar visibilidad de sprites. Configurá la misma tecla dentro de la versión local de PNGTuber Plus.
- VTuberBot guarda nombre, tecla, descripción, aplicación objetivo y estado habilitado en SQLite. Por ahora acepta una tecla individual: 0–9, A–Z, F1–F12.
- **Probar atajo seleccionado** es una acción explícita: comprueba la ruta/proceso, intenta enfocar la ventana identificada y envía la tecla mediante la entrada de Windows. No usa coordenadas del ratón ni clics en posiciones fijas.
- El resultado “ENVIADO, NO VERIFICADO” significa que Windows aceptó los eventos de tecla; no demuestra que PNGTuber Plus cambiara un disfraz o una expresión. Cancelación, aplicación ausente, tecla no compatible y error se muestran como estados distintos.
- El registro está preparado para que una futura integración con Streamer.bot pueda referenciar el ID/nombre/tecla, pero no se implementa ni se simula aquí una conexión WebSocket de Streamer.bot.

## Avatar nativo frente a avatar externo

- **Avatar Studio nativo:** perfiles .vtuberavatar, imagen base, capas y expresiones se administran con los contratos de VTuberBot.
- **PNGTuber Plus:** su avatar se configura y guarda dentro de PNGTuber Plus; VTuberBot conserva solamente la ruta del ejecutable, progreso del asistente y registro de atajos.

No hay conversión automática entre ambos formatos. Guardar la configuración de PNGTuber Plus no debe modificar los perfiles, capas ni sprites nativos.
