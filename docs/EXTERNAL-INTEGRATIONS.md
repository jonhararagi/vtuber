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


## Streamer.bot: conexión WebSocket

El espacio **Streamer.bot** usa el servidor WebSocket de la instancia, separado del administrador de ejecutables. La fuente del protocolo es la [documentación oficial de WebSocket](https://docs.streamer.bot/api/websocket), en particular [configuración](https://docs.streamer.bot/api/websocket/guide/configuration), [autenticación](https://docs.streamer.bot/api/websocket/guide/authentication), [solicitudes](https://docs.streamer.bot/api/websocket/requests) y [eventos](https://docs.streamer.bot/api/websocket/events).

1. En Streamer.bot abrí **Servers/Clients → WebSocket Server**. Comprobá que el servidor esté habilitado y anotá Address, Port y Endpoint. Los valores por defecto de Streamer.bot son normalmente `127.0.0.1`, `8080` y `/`.
2. En VTuberBot abrí **Streamer.bot**. Configurá host, puerto y endpoint para que coincidan con la instancia. El valor inicial es local; si configurás un host remoto, verificá la confianza de la red. `ws://` no cifra el tráfico y VTuberBot no abre puertos ni cambia el firewall.
3. Si el servidor envía el objeto `authentication` en `Hello`, introducí la contraseña configurada. VTuberBot calcula el desafío SHA-256/Base64 documentado y nunca envía la contraseña como parte de esa solicitud. La contraseña guardada se protege con DPAPI para el usuario actual de Windows; si no puede descifrarse, introducila de nuevo.
4. Pulsá **Conectar / probar**. El estado conectado solo se establece después de recibir `Hello` válido y completar autenticación cuando el servidor la exige. **Desconectar** cierra la sesión actual; volver a conectar es manual, y las suscripciones deben solicitarse de nuevo para esa conexión.
5. Pulsá **Consultar / actualizar acciones** para usar `GetActions`. Elegí una acción del catálogo real y revisá su ID/estado. **Ejecutar acción seleccionada** pide confirmación y usa `DoAction` por GUID. Un `status=ok` confirma la aceptación de la solicitud, no el efecto final en OBS u otros programas.
6. Pulsá **Consultar eventos disponibles** (`GetEvents`), seleccioná eventos concretos y elegí **Suscribir eventos seleccionados** (`Subscribe`). No se usa un comodín de suscripción. La interfaz muestra el resultado confirmado por el servidor. Los eventos recibidos se registran, pero no ejecutan acciones ni atajos automáticamente.

### Alcance y verificación

- **Implementado en código:** cliente `ClientWebSocket`, handshake `Hello`, desafío de autenticación documentado, IDs únicos de solicitud, correlación de respuestas, límite de mensaje y lectura fragmentada, `GetActions`, `DoAction`, `GetEvents`, `Subscribe`, estado real, protección DPAPI y cierre al salir.
- **Pruebas automatizadas añadidas:** endpoint, algoritmo de autenticación y prueba de protocolo con servidor WebSocket simulado que envía `Hello` fragmentado, autentica, responde a solicitudes y publica un evento. El servidor simulado no sustituye una validación con Streamer.bot real.
- **Pendiente de validación en Windows real:** comprobar conexión con la versión instalada, contraseña correcta/incorrecta, acciones seleccionadas de bajo riesgo, eventos Twitch habilitados, comportamiento en OBS y PNGTuber Plus. No se afirma integración directa con expresiones de PNGTuber Plus.
- **Limitaciones actuales:** reconexión manual, sin reconexión automática; no hay un EventManager genérico compartido en el Core actual, por lo que el evento se expone mediante el contrato del cliente y se registra para preparar la coordinación de la Orden 4/5.
