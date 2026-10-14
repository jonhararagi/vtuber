# Taller de personaje 3D de VTuberBot

## Estado actual

La ventana WPF/WebView2 contiene un taller paramétrico 3D integrado. La figura se genera en el visor con Three.js y no depende de una página externa. Los controles disponibles son:

- **Biblioteca de cuatro personajes base preconfigurados:** Exploradora bronceada (seleccionada por defecto), Guardiana del desierto (piel cobriza), Aprendiz lunar y Viajera de jade (piel oliva). Cada preset configura silueta, altura, piel, pelo, ojos, expresión, ropa, colores y accesorio; todos los campos quedan editables después.
- La base recomendada utiliza tono de piel bronceado (`#b87955`), cabello marrón oscuro, iris marrones y un conjunto de aventura verde/dorado. Se genera con la geometría paramétrica ya incluida en el taller: **no es un prefab externo importado ni un modelo anime profesional descargado**.
- Silueta base: curvilínea suave, delgada o atlética; control de altura.
- Tono de piel, color de pelo e iris; ojos marrones y pupilas blancas como valores iniciales.
- Seis estilos de cabello: media melena, ondulado, recogido, coletas gemelas, largo y bob; tres opciones de flequillo.
- Cinco conjuntos: cárdigan con falda, hoodie, aventurero, uniforme y gótico.
- Colores principal/secundario y accesorios: lazo, auriculares, horquillas estrella y aro.
- Tres expresiones sencillas.
- Restablecer el diseño, guardar/cargar los ajustes en JSON.
- **Exportar la malla visible como GLB** para llevarla a otra herramienta 3D y continuar el trabajo.

## Qué exporta GLB y qué no

El botón GLB exporta la geometría visible y sus materiales como un archivo glTF binario. Es un primer formato de intercambio real, distinto del JSON de ajustes.

El GLB actual **no tiene esqueleto (rig), pesos de deformación, seguimiento facial, animaciones ni estructura VRM**. Por eso todavía no es un modelo listo para VTube Studio/OBS. La ropa y el cabello siguen siendo geometría paramétrica generada para esta figura; no son prendas externas equipadas sobre un esqueleto.

## Límites y dependencias

- Three.js, OrbitControls y GLTFExporter se cargan desde CDN; el taller y la exportación requieren Internet.
- La calidad de la figura es un prototipo funcional, no arte final de personaje anime.
- La carga de JSON aplica valores de apariencia, no reconstruye mallas importadas.
- No hay importación de VRM/GLB, rigging, edición de topología, sistema de prendas intercambiables basado en skinning ni conexión directa de esta figura con el overlay de VTuberBot.
- La exportación GLB no debe describirse como exportación VRM.

## Próximos pasos técnicos

1. Confirmar CI de Windows en el SHA que incorpora GLB y corregir cualquier fallo.
2. Validar manualmente el visor y la exportación GLB en Windows/WebView2.
3. Definir importación de un VRM canon con rig compatible.
4. Construir el sistema de prendas sobre ese rig, con pesos y compatibilidad explícita, y conectar el avatar al overlay/OBS.
5. Evaluar cada asset de terceros individualmente; verificar licencias, procedencia y compatibilidad antes de redistribuir.

## Recursos para investigación (no integrados automáticamente)

- [M3-org/CharacterStudio](https://github.com/M3-org/CharacterStudio): investigar arquitectura y flujos glTF/VRM.
- [M3-org/loot-assets](https://github.com/M3-org/loot-assets): posibles assets de ropa/equipo; verificar licencia por archivo.
- [memelotsqui/character-assets](https://github.com/memelotsqui/character-assets): colección de assets; comprobar licencia y formato.
- [VRoid Studio](https://vroid.com/en/studio): opción externa para crear un modelo canon y exportar VRM.

## Validación pendiente

- CI de Windows correspondiente al commit actual: pendiente de resultado.
- Verificar visualmente WebGL, controles, los estilos, guardado/carga JSON y descarga GLB.
- Abrir el GLB exportado en un visor compatible y comprobar que la geometría y los materiales se conservan.
- Validar cualquier futura integración con el overlay por separado; compilar no demuestra que el avatar aparezca en OBS.
