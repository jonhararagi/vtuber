# Auditoría de arquitectura y referencias industriales — 2026-10-21

**Repositorio auditado:** [jonhararagi/vtuber](https://github.com/jonhararagi/vtuber)  
**Base examinada:** `main` en `2a8133796a286de0cef8eefe2406b92e6fb92224`  
**Rama de correcciones:** `fix/kick-webhook-backpressure-2026-10-21`

## 1. Esquema real del producto

- **Interfaz:** Windows x64, .NET 10 y WPF. La ventana principal usa archivos parciales por espacio de trabajo; conserva estado visual y navegación.
- **Persistencia:** SQLite a través de `VtuberBot.Core/ProductStore.cs`; ajustes y datos de producto comparten ese almacén. Los secretos OAuth/Client Secret se protegen localmente con DPAPI en `UserSecretStore`.
- **Avatar/audio:** editor PNGTuber y capas con recursos propios; cola de voz, captura/reproducción de audio y motor VOICEVOX opcional. Los motores y voicebanks instalados por separado no deben confundirse con contenido incluido en el paquete.
- **OBS:** adaptador OBS WebSocket v5 con autenticación, deadlines de petición, supervisión/reconexión y lectura de estado/counters reales. OBS sigue siendo el motor de producción.
- **Plataformas:** Twitch IRC/TLS y EventSub tienen supervisión, backoff, límites de cola y filtros de duplicados. Kick usa OAuth/API y un receptor HTTP local ligado a loopback, con firma RSA/SHA-256, antigüedad máxima, reconocimiento de suscripción/canal, moderación manual y reglas automáticas opt-in.
- **CI:** workflow Windows; los proyectos tratan las advertencias de compilación como errores. El CI demuestra compilación/pruebas para un SHA, no permisos reales de Twitch/Kick, disponibilidad del túnel o prueba visual local.

## 2. Referencias de la industria y lecciones extraídas

| Referencia pública | Evolución observada | Criterio aplicado al proyecto |
|---|---|---|
| [OBS Studio — releases](https://github.com/obsproject/obs-studio/releases) y [obs-websocket — releases](https://github.com/obsproject/obs-websocket/releases) | Las notas recientes registran desincronización de estados al usar WebSocket/herramientas externas, outputs atascados, deadlocks y fallos de cierre/memoria. | No tratar un botón pulsado como prueba del estado externo; usar petición/respuesta, timeout, lectura de estado confirmada y cierre supervisado. Mantener OBS como autoridad. |
| [TwitchIO — changelog](https://twitchio.dev/en/stable/getting-started/changelog.html) y [releases](https://github.com/TwitchIO/TwitchIO/releases) | Las versiones corrigen loops de reconexión, eventos de reconexión, tramas inválidas y tareas asíncronas que podían desaparecer al perder referencias. | Mantener referencias a tareas supervisoras, backoff acotado, deduplicación y colas acotadas; no crear tareas sin dueño ni ejecutar una segunda acción por un evento repetido. |
| [SAMMI — releases](https://github.com/SAMMISolutions/SAMMI-Official/releases) | El registro de eventos con datos inspeccionables ayuda a diagnosticar automatizaciones; sus versiones también documentan compatibilidad y regresiones por servicio. | Registrar resumen, tipo, ID y fallo de admisión; nunca registrar tokens, secretos ni contenido privado por defecto. Diferenciar “recibido”, “encolado” y “acción confirmada”. |
| [pngtuber — GitHub](https://github.com/metruzanca/pngtuber) | Admite skins aportadas por el usuario, manifiestos explícitos y captura mediante OBS, sin redistribuir ilustraciones ajenas. | Priorizar assets del usuario, validación de rutas y manifiestos, exportación transparente y estados reposo/habla; no depender de un catálogo de arte embebido. |
| [VTube Studio API](https://github.com/DenchiSoft/VTubeStudio) y [guía de plugins](https://github.com/DenchiSoft/VTubeStudio/wiki/Plugins) | La API local requiere habilitación/permisos explícitos y el puerto puede variar. | Exponer host/puerto, explicar autorización y mostrar el resultado real de conexión; no suponer que un servicio local está escuchando en el puerto por defecto. |

Estas referencias se usan para comparar decisiones de arquitectura y errores documentados; no implican copiar código ni que VTuberBot haya reproducido cada fallo de esos productos.

## 3. Hallazgo corregido en esta rama: entrega de webhooks Kick

### Fallo

El receptor verificaba la firma, guardaba el ID del evento en el conjunto de deduplicación y después llamaba al consumidor. Si la admisión fallaba, devolvía HTTP 503, pero el ID ya estaba marcado; cuando Kick reintentaba la entrega, el receptor contestaba 204 como duplicado. Eso podía descartar un evento que nunca se había admitido.

### Cambios aplicados

- Nuevo `KickWebhookDeliveryDeduplicator`: serializa la admisión y solo conserva el ID cuando el consumidor acepta el evento. Si el callback falla, el ID queda libre para el reintento.
- Nuevo consumidor con `BoundedQueue<KickWebhookEvent>` de **256 eventos**. Una cola saturada rechaza la admisión; el receptor devuelve 503 en vez de aceptar y perder silenciosamente la entrega.
- El consumidor procesa hasta **20 eventos por lote de UI**, reduciendo ráfagas ilimitadas de `Dispatcher.BeginInvoke`.
- El cierre detiene primero el listener y concede una ventana limitada de **10 segundos** para drenar la cola. Si queda trabajo pendiente, se registra el descarte en vez de afirmar que se procesó.
- Pruebas de idempotencia: una admisión fallida permite reintentar el mismo ID; duplicados concurrentes se admiten una sola vez; la ventana de deduplicación tiene un límite y rechaza IDs inválidos.

### Límite residual

La cola de entrada sigue siendo **en memoria**, no una bandeja persistente SQLite. El cambio evita el error de reintentos después de una cola llena, pero un cierre forzado del proceso después del HTTP 200 todavía puede perder eventos ya admitidos. Una bandeja de entrada durable con estado pendiente/intentos/confirmación sería una fase separada; no se ha declarado implementada.

## 4. Consistencia de documentación

- `README.md` describía todavía la recepción de Kick como pendiente, aunque el código en `main` ya contiene el receptor firmado, suscripciones y moderación Kick. Se corrigió el estado para reflejar el código y la limitación del túnel público.
- `docs/TECHNICAL_AUDIT_BOTS_2026-10-18.md` representa una auditoría histórica previa; se añade una nota visible para evitar usar sus límites antiguos como estado actual.
- **Decisión de producto pendiente de reconciliar:** `docs/ESPECIFICACION_DEFINITIVA.md`, sección 5, enumera Twitch, Kick y moderación entre las funciones fuera de alcance, mientras el código/UI actuales incluyen estas funciones y tareas recientes las ampliaron. Esta rama no elimina ni cambia el alcance aprobado a escondidas; hace visible la contradicción para que la próxima revisión de producto actualice la especificación canónica de forma explícita.

## 5. Riesgos siguientes priorizados

1. **Alta — durabilidad de webhooks:** evaluar una inbox SQLite transaccional para recuperar eventos admitidos tras un cierre anormal.
2. **Alta — alcance documental:** decidir si Twitch/Kick/moderación forman parte del producto aprobado y actualizar la especificación canónica, guía, README y paneles como una sola unidad.
3. **Media — límites de capas:** los archivos parciales de WPF separan pantallas, pero parte de la orquestación y lógica de plataforma aún vive en `MainWindow`. Extraer coordinadores solo cuando puedan cubrirse con pruebas, sin una gran reescritura.
4. **Media — pruebas de integración:** conservar tests deterministas para payloads, firmas, reintentos y errores API; añadir pruebas de extremo a extremo con cuentas/canales de prueba solo cuando el usuario configure esas credenciales.
5. **Media — entrega:** registrar siempre SHA de fuente, resultado de CI y SHA-256 del artefacto exacto; un artefacto exitoso no demuestra una prueba real en el PC del usuario.

## 6. Estado de validación

Esta rama contiene los cambios de código, tests y corrección documental indicados arriba. La validación final queda condicionada al Windows CI de la PR y al SHA que ejecute realmente el workflow. No se han utilizado credenciales de Twitch/Kick reales, no se ha probado un túnel público y no se ha probado el paquete en el PC del usuario.
