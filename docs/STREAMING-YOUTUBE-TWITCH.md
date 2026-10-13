# Transmitir a Twitch y YouTube con VTuberBot

Esta guía explica las dos formas de emitir desde la aplicación: elegir una plataforma para la salida normal de OBS, o emitir a las dos simultáneamente mediante un complemento opcional de OBS.

## Emitir en una plataforma

1. Instala y abre OBS Studio. Crea tus escenas y fuentes como de costumbre.
2. En OBS, abre **Herramientas → WebSocket Server Settings** (el nombre puede variar según el idioma), activa el servidor y configura una contraseña.
3. En VTuberBot, entra en **OBS**, completa host `127.0.0.1`, puerto `4455` y la contraseña, y pulsa **Conectar y autenticar**.
4. En **Transmitir a Twitch o YouTube**, selecciona una plataforma.
5. Abre el panel web de esa plataforma y crea/selecciona tu transmisión. Copia su clave de emisión sin compartirla públicamente.
6. Pega la clave en VTuberBot y pulsa **Guardar clave cifrada**. La clave se protege con Windows DPAPI para tu usuario y no se registra en logs ni en SQLite.
7. Pulsa **Configurar destino en OBS**. Esto configura el destino, pero todavía no inicia la emisión.
8. Comprueba título, privacidad, escena, audio y vista previa. Pulsa **Iniciar transmisión**. Para terminar, pulsa **Detener transmisión** y confirma.

VTuberBot usa la salida RTMPS personalizada de OBS para seleccionar Twitch o YouTube. Cambiar de destino requiere detener la emisión primero. La clave guardada se puede reutilizar dejando la caja de clave vacía. Si regeneras la clave en la plataforma, actualízala en la aplicación.

Enlaces de las plataformas:
- Twitch: https://dashboard.twitch.tv/settings/stream
- YouTube Studio / emisiones en directo: https://studio.youtube.com/

## Emitir en Twitch y YouTube a la vez

OBS estándar tiene una salida de transmisión principal. Enviar la misma emisión a dos plataformas simultáneamente requiere una función multi-salida, normalmente un complemento local de OBS o un servicio de retransmisión.

VTuberBot ofrece un enlace a la página del complemento **Multiple RTMP outputs** del foro oficial de OBS: https://obsproject.com/forum/resources/multiple-rtmp-outputs-plugin.964/. El complemento es de terceros: no se incluye en VTuberBot y la aplicación no lo instala ni asegura que funcione en todas las versiones de OBS. Lee sus requisitos, comprobaciones de compatibilidad e incidencias recientes antes de instalarlo.

Configuración de una sola vez, si el complemento es compatible con tu OBS:
1. Instala el complemento desde su publicación oficial, reinicia OBS y comprueba que carga sin errores.
2. Mantén la salida principal configurada para una plataforma y añade la segunda plataforma en el panel del complemento. Introduce cada clave directamente en el componente correspondiente.
3. Si el complemento ofrece compartir el codificador principal, evalúa esa opción para reducir carga; prueba localmente antes de una emisión importante.
4. Comprueba que ambos destinos reciben la señal y que el audio funciona en ambos. Si no, usa una emisión de prueba privada/no listada cuando la plataforma lo permita.
5. Inicia y detén cada salida según las opciones que realmente ofrezca la versión instalada. El botón **Iniciar transmisión** de VTuberBot controla la salida principal de OBS; no se presenta como control directo de botones internos de un complemento no integrado.

La emisión doble necesita aproximadamente más ancho de subida que una emisión sola; el consumo de CPU/GPU depende de si cada destino usa un codificador distinto o comparte el principal. En el Ryzen 5 5600G con gráficos integrados, empieza con resolución/bitrate moderados y observa la carga de OBS y los fotogramas perdidos.

## Seguridad y límites

- Nunca envíes una clave de transmisión por chat ni la incluyas en capturas de pantalla, prompts o informes.
- Las claves se guardan cifradas para el usuario de Windows actual; si cambias de cuenta de Windows, puede ser necesario volver a introducirlas.
- VTuberBot confirma que OBS aceptó la configuración o el comando. Eso no significa que Twitch/YouTube hayan aceptado la señal o que la emisión sea pública.
- La disponibilidad de directos, restricciones de cuenta y estados de publicación se gestionan en las propias plataformas.
- La integración de salida doble con un complemento externo no ha sido validada automáticamente por el CI de VTuberBot. Hay que comprobarla en el OBS real instalado.
- YouTube recomienda RTMPS y ajustar el bitrate a la velocidad de subida disponible: https://support.google.com/youtube/answer/2853702
