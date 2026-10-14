# Mini estudio 3D de personaje para VTuberBot

## Estado

Primera integración de interfaz: VTuberBot abre Character Studio dentro de una ventana WPF/WebView2, sin lanzar un navegador externo. El contenido del editor se sirve desde `https://studio.m3org.com/`, por lo que esta primera versión requiere Internet y depende de la disponibilidad del sitio upstream. **No es todavía una copia local ni una integración offline.**

## Flujo previsto

1. Crear el modelo base en VRoid Studio y exportarlo como VRM.
2. Abrir **Avatar Studio → Mini estudio 3D integrado** dentro de VTuberBot.
3. Importar el VRM y probar las opciones de personalización que Character Studio admita para ese modelo.
4. Exportar el resultado como VRM/GLB si el editor y los recursos seleccionados lo permiten.
5. Copiar el modelo exportado a la biblioteca de avatares de VTuberBot. La carga directa de VRM en el overlay y la aplicación automática de ropa al overlay son tareas separadas y no deben considerarse completadas por esta ventana.

## Recursos abiertos para investigar

- [M3-org/CharacterStudio](https://github.com/M3-org/CharacterStudio): editor de avatares glTF/VRM con licencia MIT; declara importación de modelos/texturas y exportación GLB/VRM.
- [M3-org/loot-assets](https://github.com/m3-org/loot-assets): paquete de recursos usado por Character Studio. Revisar la licencia de cada recurso antes de redistribuirlo.
- [memelotsqui/character-assets](https://github.com/memelotsqui/character-assets): ejemplos de assets para el ecosistema Character Studio. Revisar licencias y compatibilidad de cada archivo antes de incluirlo en VTuberBot.
- [VRoid Studio](https://vroid.com/en/studio): crear el modelo base y exportarlo en un formato compatible.

## Compatibilidad y límites

- Que un archivo sea GLB/VRM no significa que una prenda se adapte automáticamente a cualquier cuerpo. La ropa 3D normalmente necesita una malla y un esqueleto compatibles, pesos de skinning adecuados y, a veces, ajuste manual.
- No descargar ni empaquetar automáticamente prendas de terceros sin comprobar licencia, atribución y permiso de redistribución.
- La ventana integrada carga una aplicación web de un tercero. No enviar modelos privados hasta revisar las prácticas de privacidad del servicio. La siguiente etapa para una distribución plenamente local sería fijar una versión auditada del código y sus assets dentro del repositorio, revisar sus licencias y servirlos localmente.
- La integración con el overlay de VTuberBot requiere un flujo de importación VRM separado y validación real en Windows. Esta ventana por sí sola no afirma que el overlay pueda renderizar VRM.

## Pruebas pendientes

- Ejecutar CI de Windows para la rama de integración.
- Abrir la ventana en Windows con WebView2 Runtime instalado.
- Comprobar carga de Character Studio, importación del VRM creado por el usuario y exportación del resultado.
- Verificar por separado qué recursos son compatibles con el esqueleto del VRM de destino.
