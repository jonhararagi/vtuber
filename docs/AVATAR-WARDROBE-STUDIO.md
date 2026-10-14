# Taller de personaje 3D de VTuberBot

## Estado actual

VTuberBot ahora incluye una ventana WPF/WebView2 con un **taller 3D paramétrico propio**, en lugar de limitarse a abrir una página de terceros. El visor genera una figura anime estilizada mediante geometría 3D y permite cambiar:

- Silueta base: curvilínea suave, delgada o atlética; control de altura.
- Tono de piel, color de pelo y color de iris; las pupilas blancas forman parte del diseño.
- Seis estilos de cabello: media melena, ondulado, recogido, coletas gemelas, largo y bob; además de flequillo.
- Cinco conjuntos: cárdigan con falda, hoodie, aventurero, uniforme y gótico.
- Colores de ropa y accesorios: lazo, auriculares, horquillas de estrella y aro decorativo.
- Expresión facial sencilla.
- Guardar/cargar una configuración de apariencia en JSON.

El renderizador utiliza Three.js cargado desde CDN, por lo que esta primera versión necesita Internet. La geometría es generada por el programa; no son imágenes planas pegadas a la pantalla.

## Límites importantes

- Esto es un **prototipo de editor paramétrico**, no un sustituto completo de VRoid Studio.
- El cuerpo, los peinados y la ropa son mallas geométricas generadas para el visor. No constituyen todavía un rig de animación completo ni un modelo VRM exportable.
- Guardar JSON guarda las opciones de apariencia, no una malla 3D ni un archivo listo para emitir.
- Todavía no se pueden importar prendas VRM/GLB externas ni ajustar automáticamente prendas arbitrarias al cuerpo.
- No se afirma que este avatar ya se esté mostrando en el overlay de VTuberBot u OBS. Para ello hace falta exportar/importar un modelo riggeado y conectar el renderizador de avatar.
- La calidad visual es una base funcional de personalización, no arte final de personaje. El siguiente paso debe reemplazar gradualmente las mallas geométricas por un cuerpo anime de alta calidad, un rig compatible y prendas con skinning correcto.

## Fuentes de recursos para la siguiente etapa

- [M3-org/CharacterStudio](https://github.com/M3-org/CharacterStudio): estudiar sus flujos de importación/exportación glTF/VRM y su arquitectura.
- [M3-org/loot-assets](https://github.com/M3-org/loot-assets): contiene modelos de ropa y equipo 3D. Revisar la licencia de cada archivo y verificar compatibilidad con el esqueleto antes de distribuir.
- [memelotsqui/character-assets](https://github.com/memelotsqui/character-assets): colección de assets Webaverse; comprobar licencia y formato antes de reutilizar.
- [VRoid Studio](https://vroid.com/en/studio): crear el personaje canon y exportar el VRM base.

## Validación pendiente

- CI de Windows debe compilar y ejecutar las pruebas automatizadas.
- Abrir el taller en Windows y verificar renderizado WebGL, controles, peinados, conjuntos y guardado/carga JSON.
- Después de validar el prototipo, diseñar importación de VRM, gestión real de ropa y exportación compatible con el overlay.
