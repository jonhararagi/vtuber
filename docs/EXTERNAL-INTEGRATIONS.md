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
## Automatizaciones por eventos — implementación en la rama de producto

El espacio **Automatizaciones** coordina eventos de la conexión WebSocket real de Streamer.bot y notificaciones que ya procesa el puente de Twitch EventSub de VTuberBot. No vuelve a ejecutar comandos de chat ni republica cada acción como un evento entrante.

- Las reglas se guardan en las preferencias SQLite existentes bajo `automation.eventActions.rules.v1`; el interruptor global y la autorización de teclas también usan las preferencias existentes. No se crea otra base de datos.
- Las reglas nuevas están desactivadas y las automatizaciones arrancan pausadas si no existe una preferencia previa. El origen de cada regla es explícito.
- Para eventos de Streamer.bot se consulta `GetEvents`; las suscripciones manuales y las requeridas por reglas habilitadas se reconcilian en un solo conjunto. Al editar o borrar reglas se intenta retirar la selección anterior y aplicar la nueva; al reconectar se reconstruye desde SQLite.
- Antes de cada ejecución se comprueba la conexión y el catálogo actual de acciones habilitadas. La confirmación de una acción externa queda vinculada al identificador concreto de destino.
- PNGTuber Plus solo acepta atajos ya registrados y habilitados. El envío automático está desautorizado por defecto y su habilitación advierte que Windows puede cambiar el foco. Un resultado de envío de tecla no confirma un cambio de expresión.
- Las condiciones usan rutas simples de campos JSON y operadores exactos `eq`, `contains` y `present`. No se evalúa código, expresiones C# ni comandos de sistema recibidos en eventos.
- La cola tiene capacidad acotada, el procesamiento se ejecuta fuera del bucle WebSocket, hay cooldown por regla, límites por regla/globales, deduplicación con memoria acotada y límite de profundidad causal. Si falta un ID de proveedor o una marca de tiempo fiable, no se afirma que los eventos equivalentes de Streamer.bot y EventSub se deduplicaron.
- La actividad distingue recepción, saturación, deduplicación, pausa, cooldown/límite, solicitud aceptada, rechazo, resultado desconocido y tecla enviada. La aceptación del servidor no demuestra el efecto posterior en OBS.

**Pendiente de validación:** la implementación necesita Windows CI verde sobre el SHA final y pruebas manuales con una instancia real de Streamer.bot, eventos de Twitch, OBS y PNGTuber Plus. No se afirma que el efecto visual del avatar esté verificado automáticamente.
