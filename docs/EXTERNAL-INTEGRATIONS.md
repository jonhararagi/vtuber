# Centro de integraciones externas

La página **Integraciones** coordina aplicaciones de escritorio existentes. No descarga, instala ni incluye ejecutables de terceros.

## Configurar

1. Abrí **Integraciones** desde el menú lateral.
2. En **PNGTuber Plus** y/o **Streamer.bot**, pulsá **Seleccionar ejecutable…** y elegí el `.exe` que ya tengas instalado.
3. La ruta se guarda localmente en las preferencias SQLite de VTuberBot. Podés corregirla cuando cambie la ubicación.

## Estados

- **Sin configurar:** no se guardó una ruta.
- **No encontrado:** la ruta guardada no apunta a un archivo existente.
- **Configurado:** el archivo existe, pero no se detecta su proceso.
- **En ejecución:** el proceso del ejecutable elegido está presente.
- **Inicio solicitado:** Windows aceptó la solicitud para abrirlo.
- **Error:** Windows o el sistema no pudo comprobar o iniciar la aplicación; la interfaz muestra el mensaje devuelto.

La presencia de un proceso **no significa que exista una conexión** entre esa aplicación y VTuberBot. En esta primera versión el centro configura, abre e inspecciona procesos; no afirma integración por WebSocket/IPC ni controla internamente las funciones de PNGTuber Plus o Streamer.bot. El estado se puede volver a consultar con **Actualizar estado**.

VTuberBot no detiene procesos externos al cerrarse. Las aplicaciones deben cerrarse desde sus propias ventanas.

## Pruebas

ExternalApplicationManager depende de IExternalApplicationRuntime; las pruebas sustituyen ese límite para simular archivo ausente, proceso activo y fallo de inicio sin instalar ni lanzar aplicaciones reales.
