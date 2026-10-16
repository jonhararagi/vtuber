# Guía rápida de Avatar Studio y encuadre PNGTuber

Esta guía distingue los dos editores disponibles para evitar probar todos los modos.

## 1. Editor principal: capas y piezas

1. Abrí **Avatar Studio** en el menú lateral.
2. En el panel superior **Crear y personalizar tu personaje**, pulsá **🎨 ABRIR EDITOR PNGTUBER · capas y piezas**.
3. En **Biblioteca de objetos**, elegí una categoría (Sombreros, Ojos, Bocas, Emociones, Fondos o Personalizados) y añadí piezas, o importá tus propios PNG/JPG/BMP/GIF.
4. Seleccioná una capa y arrastrala sobre el lienzo. En el panel derecho podés cambiar posición, tamaño, giro, opacidad, visibilidad y orden.
5. Guardá el proyecto para seguir editando las capas después. **Exportar PNG transparente** aplana las capas visibles; para conservar la edición, guardá también el proyecto.

El editor principal usa una biblioteca local de recursos y no necesita una clave de IA.

## 2. Encuadre de cámara y vista previa de OBS

En **Avatar Studio**, bajá hasta el botón **🎥 Encuadre de cámara y vista previa OBS (editor avanzado)**. Esta herramienta complementaria sirve para:

- elegir presets de recorte: Rostro, Busto, Cuerpo completo o Personalizado;
- ajustar el rectángulo del recorte mediante X/Y/ancho/alto;
- ver una maqueta orientativa del lienzo OBS 1920×1080 y ajustar la colocación/tamaño previstos;
- guardar los valores de encuadre dentro del proyecto `.waifu.json`;
- exportar un PNG transparente del recorte seleccionado para usarlo como fuente de imagen.

**Importante:** la vista de OBS es una previsualización, no una conexión en tiempo real. El PNG exportado es estático; esta herramienta no sigue automáticamente la voz ni mueve o redimensiona una fuente de OBS.

## 3. ¿Qué hacen los otros modos?

- **Crear PNGTuber con mis imágenes:** asistente corto para crear un perfil a partir de imágenes propias.
- **2.5D con IA local (experimental):** intenta separar una ilustración en regiones aproximadas; no reconstruye perfectamente partes ocultas ni crea un rig Live2D.
- **Mini estudio 3D integrado:** crea un modelo paramétrico y puede exportar un GLB estático; no es el editor de capas PNG.
- **VRM / VRoid:** sirven para trabajar con modelos 3D compatibles; no son necesarios para editar un PNG.

## 4. Limitaciones que conviene conocer

- Un PNG exportado es una imagen estática. Para cambiar boca cerrada/abierta automáticamente se necesitan sprites de expresión configurados en el perfil y un runtime que cambie esos estados.
- Los GIF importados se animan en la vista previa, pero la exportación PNG guarda un fotograma.
- La galería de inicio contiene recursos originales de prototipado, no reemplaza una ilustración anime profesional.
- La CI de Windows verifica compilación, pruebas y empaquetado para el SHA correspondiente; no sustituye la revisión visual ni las conexiones con aplicaciones reales en tu PC.
