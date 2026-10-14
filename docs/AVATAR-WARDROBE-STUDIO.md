# Mini estudio 3D de personaje para VTuberBot

## Estado

Primera integración de interfaz: VTuberBot abre Character Studio dentro de una ventana WPF/WebView2, sin lanzar un navegador externo. El contenido del editor se sirve desde `https://studio.m3org.com/`, por lo que esta primera versión requiere Internet y depende de la disponibilidad del sitio upstream. **No es todavía una copia local ni una integración offline.**

## Usar personajes creados en BotImagen (PNGTuber)

El flujo 2D es el camino disponible para usar ya una ilustración generada en BotImagen dentro del overlay nativo de VTuberBot:

1. En BotImagen, exportar el personaje como PNG transparente (la salida actual del compositor es 512×768 RGBA).
2. En VTuberBot, abrir **Avatar Studio → Importar personaje de BotImagen / crear PNGTuber**.
3. Seleccionar el PNG exportado y crear un perfil con nombre propio.
4. Cuando pregunte por la imagen de habla, elegir **Sí** y seleccionar una segunda imagen con la boca abierta si se dispone de ella; elegir **No** para reutilizar la misma imagen como estado Idle y Talking.
5. VTuberBot copia las imágenes a su biblioteca local y activa el perfil. El overlay nativo usa Idle/Talking y puede recibir sprites adicionales de expresiones desde los controles de Avatar Studio.

La importación copia la imagen a la carpeta de datos local de VTuberBot, sin modificar el archivo original de BotImagen. Cada perfil conserva su propia configuración.

## Flujo 3D


1. Crear el modelo base en VRoid Studio y exportarlo como VRM.
2. Abrir **Avatar Studio → Mini estudio 3D integrado** dentro de VTuberBot.
3. Importar el VRM y probar las opciones de personalización que Character Studio admita para ese modelo.
4. Exportar el resultado como VRM/GLB si el editor y los recursos seleccionados lo permiten.
5. Copiar el modelo exportado a la biblioteca de avatares de VTuberBot. Para usar el VRM dentro de VTuberBot, volver a Avatar Studio y pulsar **Cargar VRM y abrir overlay 3D**. VTuberBot copia el archivo a su biblioteca local y abre un overlay transparente que renderiza el modelo con Three.js/three-vrm dentro de WebView2. Los estados Idle/Talking se sincronizan con el estado de voz de VTuberBot. El renderizador obtiene sus bibliotecas JavaScript desde CDN y, por tanto, necesita Internet la primera vez que carga; el archivo VRM permanece en disco local y no se sube al editor web.

## Recursos abiertos para investigar

- [M3-org/CharacterStudio](https://github.com/M3-org/CharacterStudio): editor de avatares glTF/VRM con licencia MIT; declara importación de modelos/texturas y exportación GLB/VRM.
- [M3-org/loot-assets](https://github.com/m3-org/loot-assets): paquete de recursos usado por Character Studio. Revisar la licencia de cada recurso antes de redistribuirlo.
- [memelotsqui/character-assets](https://github.com/memelotsqui/character-assets): ejemplos de assets para el ecosistema Character Studio. Revisar licencias y compatibilidad de cada archivo antes de incluirlo en VTuberBot.
- [VRoid Studio](https://vroid.com/en/studio): crear el modelo base y exportarlo en un formato compatible.

## Compatibilidad y límites

- Que un archivo sea GLB/VRM no significa que una prenda se adapte automáticamente a cualquier cuerpo. La ropa 3D normalmente necesita una malla y un esqueleto compatibles, pesos de skinning adecuados y, a veces, ajuste manual.
- No descargar ni empaquetar automáticamente prendas de terceros sin comprobar licencia, atribución y permiso de redistribución.
- La ventana integrada carga una aplicación web de un tercero. No enviar modelos privados hasta revisar las prácticas de privacidad del servicio. La siguiente etapa para una distribución plenamente local sería fijar una versión auditada del código y sus assets dentro del repositorio, revisar sus licencias y servirlos localmente.
- El overlay VRM es un renderizador WebView2 independiente del overlay PNG 2D. La carga usa versiones fijadas de Three.js y three-vrm desde CDN; no es todavía una distribución offline. El ajuste automático de ropa y la edición de mallas no forman parte del overlay.

## Pruebas pendientes

- Ejecutar CI de Windows para la rama de integración, incluido el nuevo renderizador VRM.
- Abrir la ventana en Windows con WebView2 Runtime instalado.
- Importar un VRM real y confirmar transparencia, encuadre, rendimiento y estados Idle/Talking en el overlay.
- Comprobar carga de Character Studio, importación del VRM creado por el usuario y exportación del resultado.
- Verificar por separado qué recursos son compatibles con el esqueleto del VRM de destino.
