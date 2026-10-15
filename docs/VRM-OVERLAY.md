# Overlay VRM local de VTuberBot

## Flujo disponible

1. Exportá un modelo `.vrm` desde VRoid Studio u otra herramienta compatible.
2. Abrí **Avatar Studio → Cargar modelo VRM y abrir overlay 3D**.
3. Elegí el archivo. VTuberBot rechaza archivos ausentes, vacíos o de más de 200 MB y copia el modelo a una carpeta única bajo los datos locales de avatares.
4. Se abre una ventana transparente y siempre visible que muestra el modelo. La aplicación sincroniza los estados Idle/Talking con la voz.

## Seguridad y dependencias

- El archivo VRM permanece en el equipo y no se sube al editor web.
- El overlay usa WebView2 y descarga Three.js y three-vrm desde versiones fijadas en CDN; necesita Internet y Microsoft Edge WebView2 Runtime.
- Cada modelo se guarda en su propia carpeta para que el HTML del renderizador no sobrescriba archivos de otros modelos.
- El renderizador desactiva DevTools y la barra de estado del WebView.

## Límites

- No hay tracking facial, captura de movimiento, lip-sync por fonemas, animación avanzada de expresiones ni edición de malla.
- El taller paramétrico integrado exporta GLB estático; no convierte ese GLB en VRM.
- No hay ajuste automático de ropa. Las prendas VRM deben ser compatibles con el esqueleto del modelo y pueden requerir una herramienta externa.
- La compilación CI no demuestra que un VRM real se haya mostrado correctamente en la PC del usuario. Se necesita una comprobación manual en Windows de transparencia, encuadre, rendimiento y estados Idle/Talking.
