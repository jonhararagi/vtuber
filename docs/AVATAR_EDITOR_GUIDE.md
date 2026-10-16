# Guía rápida de Avatar Studio y encuadre PNGTuber

Esta guía describe el flujo vigente de VTuberBot. La fuente de alcance es [la especificación definitiva](ESPECIFICACION_DEFINITIVA.md).

## 1. Usar tu propia imagen

1. Abrí **Avatar Studio**.
2. En **Editar mi propia imagen de avatar**, elegí **Crear PNGTuber con mis imágenes** o importá tu arte desde el editor.
3. No necesitás elegir un personaje de una galería. Usá tu propia ilustración.
4. Si tenés una imagen con la boca cerrada, asignala a **Idle** (reposo).
5. Para que el avatar cambie al hablar, asigná una segunda imagen con la boca abierta a **Talking** (hablando). Si solo asignás una imagen, VTuberBot no puede inventar una boca animada.

## 2. Editar y encuadrar

- **🎨 Abrir editor de imagen y capas** abre el editor complementario para importar y organizar imágenes/capas.
- **🎥 Encuadre de cámara y vista previa OBS (editor avanzado)** permite ajustar posición, escala y recorte para rostro, busto, cuerpo completo o un tamaño personalizado.
- Guardá el proyecto de edición para recuperar el encuadre. Exportá un PNG transparente cuando quieras usar la imagen fuera del editor.
- La vista previa de 1920 × 1080 es orientativa. No modifica escenas ni fuentes de OBS en tiempo real.

## 3. Probar reposo/hablando

1. En Avatar Studio, confirmá que existan las imágenes **Idle** y **Talking**.
2. Mostrá el overlay del avatar.
3. En **Voz y audio**, elegí el archivo run.exe de la carpeta vv-engine de una instalación oficial de VOICEVOX y pulsá **Iniciar VOICEVOX Engine en segundo plano**. Si ya está activo, VTuberBot lo detecta y evita iniciar otra instancia.
4. Pulsá **Comprobar y actualizar voces VOICEVOX** y elegí un estilo real devuelto por Engine. Podés seleccionar también la salida de audio.
5. Pulsá **Probar voz VOICEVOX**. La aplicación debe encolar la prueba mediante el motor local; mientras el audio esté reproduciéndose, el avatar pasa a **Talking** y vuelve a **Idle** al terminar o detenerse.
6. También podés probar los estados manualmente desde las herramientas de avatar, sin micrófono ni OBS conectado. Si Engine no responde, se muestra un error y no se utiliza silenciosamente una voz de Windows o una API remota.

## 4. OBS

Conectá OBS desde su panel mediante WebSocket v5. Capturá la ventana del overlay o la fuente correspondiente en OBS. El encuadre del editor no controla la escena de OBS por sí solo.

## 5. Límites importantes

- Solo se exponen los dos estados básicos **Idle** y **Talking** en el flujo principal.
- No hay sincronización de boca por fonemas ni lip-sync avanzado.
- Una imagen única no se convierte automáticamente en dos poses.
- La generación de personajes prefabricados y el flujo 2.5D/parallax no forman parte del flujo principal.
- El modelo VRM/3D es opcional y no es necesario para usar el avatar PNG.
- La conexión real con VOICEVOX u OBS depende de que esos programas estén instalados y ejecutándose; VTuberBot debe mostrar el error si no están disponibles.
