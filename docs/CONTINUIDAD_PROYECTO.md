# Continuidad del proyecto — VTuberBot

> Registro central de continuidad para GPT Cerebro, GPT Obrero, Gemini Inspector y agentes futuros. Actualizar este archivo al finalizar cada tarea de implementación o corrección. No duplicar registros por tarea.
>
> **Regla de evidencia:** separar siempre cambios implementados, pruebas automatizadas, validación manual y pendientes. Un resultado solo corresponde al SHA exacto que lo produjo.

## 1. Estado general

- **Repositorio:** https://github.com/jonhararagi/vtuber
- **Rama principal:** `main`
- **HEAD de `main` comprobado en el PR #9:** `bbcd06c0e0706000cd95d85771b33452d11e7663`. No se modificó `main`.
- **Rama histórica de estabilización:** `v0.2.3/stabilization-gate`.
- **Rama de producto activa:** `product/complete-vtuber-experience` (rama nueva para la entrega integrada; creada desde el HEAD de PR #9 `4ba87d8f94d65a84dba475c8b0f9103380892777`).
- **PR activo:** [#9 — feat: add v0.2.3 stabilization gate](https://github.com/jonhararagi/vtuber/pull/9). Base `main`; permanece abierto y sin fusionar.
- **Versión en desarrollo:** V0.2.3. No implica que el hito esté aprobado ni que se haya publicado una versión final.
- **Arquitectura vigente para el producto completo:** C#/.NET 10/WPF/SQLite; Core independiente de WPF; sin EF Core; módulos de avatar, audio, Twitch, comunidad/engagement, OBS y Telegram desacoplados por contratos/eventos. La especificación activa es `docs/PRODUCT_BLUEPRINT.md`. El usuario autorizó el 2026-10-12 a completar el diseño y el alcance global del producto antes de solicitar una validación manual integrada.
- **Flujo de trabajo:** GPT Cerebro decide y audita; GPT Obrero implementa cambios aprobados en GitHub; Gemini Inspector revisa. GitHub es la fuente de verdad.

## 2. Historial de trabajo

### 2026-10-12 — Corrección de versión V0.2.3 y guía manual

- **Objetivo:** corregir la versión visible en `status`, añadir/ajustar comprobación automatizada y alinear la guía manual con el arranque automático en `RUNNING`.
- **Cambios implementados en el commit** [`54549e108d2acca65061ad704c629f2f213c1122`](https://github.com/jonhararagi/vtuber/commit/54549e108d2acca65061ad704c629f2f213c1122):
  - `src/VtuberBot.Core/Application.cs`: constante `VtuberApplication.Version` cambiada de `0.2.1` a `0.2.3`.
  - `tests/VtuberBot.Tests/Tests.cs`: aserción de la versión y prueba de `status` enlazadas a la constante común.
  - `docs/V0.2.3-MANUAL-VALIDATION.md`: secuencia ajustada para reflejar que la aplicación arranca normalmente en `RUNNING`; resultados manuales permanecen sin ejecutar para el nuevo paquete.
- **CI del HEAD `54549e108d2acca65061ad704c629f2f213c1122`:** [Windows CI, ejecución 38207936768](https://github.com/jonhararagi/vtuber/actions/runs/38207936768) — **SUCCESS**. Este resultado pertenece exactamente a ese HEAD, no a commits posteriores.
- **Pruebas automáticas:** Windows CI pasó en ese SHA. No se ejecutó la suite localmente porque el entorno local de trabajo no tenía `dotnet` disponible.
- **Artefacto generado por CI #90:** [descarga del artefacto de GitHub](https://github.com/jonhararagi/vtuber/actions/runs/38208912295/artifacts/11728915871). Nombre: `VTuberBot-V0.2.3-win-x64-7b145cca39d9037bf7081ee3f43aae404550dbaf`; ID `11728915871`; tamaño `66,340,234` bytes; SHA-256 reportado por GitHub `f807e233025a3445a6ed927bea37b1f4e9306613d33ba0577b8045abded4c5fd`; creado `2026-10-12T05:41:05Z`; expira `2026-10-26T05:41:02Z`. La metadata del artefacto enlaza al run #90 y al `head_sha` de rama `bc1ee445e21334d6f6aa67670c7eb10939f3158a`. El nombre/`Source SHA` del paquete es el merge commit `7b145cca39d9037bf7081ee3f43aae404550dbaf`, cuyo segundo padre es el HEAD de la rama y cuyo primer padre es `main` (`bbcd06c0e0706000cd95d85771b33452d11e7663`). Esto es coherente con el `github.sha` usado por el workflow en el evento `pull_request`; no debe confundirse el merge commit del paquete con el HEAD de la rama.
- **Validación manual del artefacto nuevo:** pendiente; el CI no sustituye la interacción manual con el ejecutable.
- **Estado de CI del HEAD documental `bc1ee445e21334d6f6aa67670c7eb10939f3158a`:** [Windows CI, ejecución #90 / 38208912295](https://github.com/jonhararagi/vtuber/actions/runs/38208912295) — **SUCCESS**. El `head_sha` del run coincide exactamente con el HEAD de la rama y del PR. El job `build-test`, restauración, compilación Release, pruebas automáticas, publicación self-contained win-x64 y subida del artefacto finalizaron correctamente.
- **Archivos de código/documentación modificados por la corrección:** los tres enumerados arriba. Este registro es un cambio documental adicional en el mismo PR.

### Historial previo relevante — evidencia manual del paquete anterior

La validación manual aportada por el usuario corresponde al paquete cuyo **Source SHA era `dc6926eeb42275fab9af79253925a00567852c67`**. Esta evidencia histórica no debe atribuirse a la compilación de `54549e108d2acca65061ad704c629f2f213c1122` ni a paquetes posteriores.

| Comprobación del paquete con Source SHA `dc6926eeb42275fab9af79253925a00567852c67` | Resultado aportado |
|---|---|
| Apertura de la aplicación | **APROBADO** |
| Inicio después de detener | **APROBADO** |
| Detención | **APROBADO** |
| `help` | **APROBADO** |
| `status` | **FALLIDO** — mostraba versión `0.2.1` cuando se esperaba `0.2.3` |
| `ping` | **APROBADO** |
| Cinco ciclos adicionales de inicio/detención | **APROBADO** |
| Diez minutos de estabilidad | **APROBADO** |
| Cierre de ventana y terminación del proceso | **APROBADO** |

El fallo de `status` motivó la corrección de la constante y las aserciones. La corrección de código y el CI verde no demuestran por sí solos que el usuario haya vuelto a ejecutar manualmente todas las pruebas en un paquete nuevo.

## 3. Defecto actual y resolución

### Defecto observado

- En el código inspeccionado antes de la corrección, `VtuberApplication.Version` estaba fijada en `0.2.1`, por lo que `status` no coincidía con la versión esperada V0.2.3.
- La guía manual debía contemplar que la aplicación arranca automáticamente en `RUNNING`, antes de intentar validar la secuencia de detención e inicio.

### Cambios aprobados y estado

1. Cambiar la versión a `0.2.3`: **IMPLEMENTADO** en `54549e1`.
2. Añadir/ajustar prueba automatizada de versión y `status`: **IMPLEMENTADO** en `54549e1`; **CI PASÓ** para ese SHA.
3. Corregir la secuencia de la guía de validación: **IMPLEMENTADO** en `54549e1`; la nueva ejecución manual sigue pendiente.
4. Ejecutar CI y validar el nuevo HEAD: **CI PASÓ** para `54549e108d2acca65061ad704c629f2f213c1122`. El commit de este documento crea un HEAD posterior; verificar su CI antes de actualizar este estado como validado.

## 4. Pendientes y próximo paso

### Cambio de prioridad — 2026-10-12

El usuario indicó que la prioridad es completar el diseño y el producto VTuber de forma integrada antes de volver a invertir tiempo en revisar una aplicación incompleta. No solicitar otra validación manual de la ventana técnica actual como siguiente tarea. La validación manual de V0.2.3 queda registrada como evidencia pendiente histórica, pero se difiere hasta contar con un candidato global de producto.

- **Rama activa:** `product/complete-vtuber-experience`, creada desde `4ba87d8f94d65a84dba475c8b0f9103380892777`. No modifica `main`.
- **Especificación de producto y arquitectura:** `docs/PRODUCT_BLUEPRINT.md` (commit `8bcb2faf3a9a7b2bd57fe7548c0935e58bec8522`). Incluye diseño UX, páginas, funciones finales, estructura de carpetas, límites de dependencia, flujo de eventos, integraciones y referencias investigadas.
- **PROJECT_SPEC actualizado:** commit `67f9950123c7f68706e10db96fb524bba08811dd`.
- **ROADMAP actualizado:** commit `9b7ce831c81f31ce2e9345c64c54501d0eb34397`.
- **PROMPTS actualizado con una instrucción de implementación integral para Obrero:** commit `a541207688321bd7be14e3463a7faec1257f68f4`.
- **README actualizado como punto de entrada al producto completo:** commit `3ced45bba6217cf2686b1e68793a7fe1d781e5d6`.
- **Estado de código:** el código base sigue siendo la aplicación WPF técnica que existía en el candidato de estabilización; la interfaz nueva y los módulos completos todavía no están implementados. No afirmar que el producto ya cambió visual o funcionalmente.
- **CI:** PR #9 Run #91 pasó para el HEAD de estabilización `4ba87d8f94d65a84dba475c8b0f9103380892777`. Ese resultado no valida los commits añadidos a la nueva rama ni una futura implementación del producto.
- **PR #9:** continúa abierto y sin fusionar. No fusionar ni alterar `main`.

### Próxima acción concreta

El implementador debe seguir el prompt **“GPT OBRERO — IMPLEMENTACIÓN INTEGRAL DEL PRODUCTO VTUBER”** añadido al final de `PROMPTS.md`, trabajando en `product/complete-vtuber-experience`. Debe implementar por bloques grandes hasta integrar el candidato global: UI y navegación; Avatar Studio/overlay; audio y voz local; Twitch/chat/comandos; comunidad, puntos, sorteos, minijuegos, moderación y automatización; OBS/Telegram; ajustes, persistencia, diagnóstico y empaquetado. No pedir al usuario una prueba por cada build incompleta. Ejecutar CI/tests internamente, corregir los fallos y solicitar una validación global de Windows cuando el producto integrado esté listo. Cualquier limitación por credenciales, assets o permisos externos debe quedar claramente identificada.

### Criterio de finalización

No equivale a terminar: compilar, abrir una ventana, tener botones, mostrar estado RUNNING o pasar pruebas de núcleo. El candidato global debe incluir las funciones y criterios de `docs/PRODUCT_BLUEPRINT.md`, sin botones muertos ni conexiones simuladas, y con evidencia de compilación/tests sobre el HEAD exacto.

## 5. Reglas permanentes de mantenimiento

- Actualizar este archivo al terminar cada tarea de implementación o corrección.
- Registrar fecha, objetivo, cambios, archivos, commit, PR, CI (incluyendo SHA), pruebas, problemas, limitaciones y siguiente paso.
- No reemplazar resultados históricos correctos ni atribuir pruebas a otro SHA.
- Usar los estados **IMPLEMENTADO**, **PROBADO AUTOMÁTICAMENTE**, **COMPROBADO MANUALMENTE** y **PENDIENTE** de forma explícita.
- Mantener un único registro central: `docs/CONTINUIDAD_PROYECTO.md`.
- No modificar `main` ni fusionar el PR #9 sin autorización explícita. El alcance ampliado de producto completo fue autorizado el 2026-10-12 y debe seguir la especificación `docs/PRODUCT_BLUEPRINT.md`.


## 6. Candidato integral de producto — actualización 2026-10-12

Esta sección supersede el estado de implementación descrito en la sección 4, que reflejaba el momento previo a iniciar la implementación del producto.

### Implementado en la rama de producto

- **Shell UX:** dashboard, navegación por áreas, estado de módulos y espacios de trabajo para avatar, Twitch, comandos, comunidad, sorteos, minijuegos, moderación, voz/audio, OBS, Telegram, ajustes y diagnóstico.
- **Persistencia:** nuevas tablas SQLite para comandos, usuarios, ledger de puntos, sorteos/entradas, sesiones de juego, reglas de moderación, automatizaciones, capas de avatar, actividad y preferencias. Se añadieron migraciones idempotentes para columnas.
- **Avatar Studio:** importación de imagen base y capas, visibilidad/eliminación de capas, composición de preview y overlay transparente movible/redimensionable. Los cues de estado modifican el movimiento/opacidad del overlay; no son un rig facial.
- **Twitch:** flujo OAuth authorization-code con PKCE en navegador y callback TCP local (sin URL ACL de administrador); el token se protege con DPAPI CurrentUser. El cliente IRC usa TLS, maneja PING/PONG, recibe chat y envía mensajes. EventSub WebSocket registra follows, suscripciones, regalos, redenciones de puntos y raids hacia el canal; los eventos se muestran en el registro/diagnóstico y, si el avatar está disponible, como alerta temporal en el overlay OBS. El chat comparte comandos, concede un punto de actividad cada cinco minutos por usuario y soporta `!entrar` para el sorteo abierto.
- **Comandos y comunidad:** comandos persistidos con variables y cooldowns aplicados, ranking, ajustes de puntos y exportación CSV.
- **Sorteos/minijuegos:** entradas únicas, selección y persistencia del ganador, historial de juegos y recompensas.
- **Moderación/automatización:** reglas regex persistidas que se evalúan sobre chat en modo observación; timers de mensajes con intervalo mínimo de cinco minutos y envío condicionado a Twitch conectado.
- **Audio:** medidor de muestras del micrófono seleccionado y TTS local mediante Windows SAPI.
- **OBS:** cliente WebSocket v5 con handshake/identificación, autenticación por challenge-response, consulta de versión/escenas y cambio de escena.
- **Telegram/diagnóstico:** Telegram Web integrado mediante WebView2 con fallback al navegador; exportación de diagnóstico sin secretos.
- **Seguridad/almacenamiento:** datos predeterminados en `%LocalAppData%\VtuberBot`, override `VTUBERBOT_HOME` para pruebas/instalación controlada, y secretos de Twitch/OBS cifrados con DPAPI.

### Evidencia de CI

- **Último SHA con CI completo verde verificado:** `0f5f4f177ed0e9861fde449c7ef584c5b907e88d` — [Windows CI #122](https://github.com/jonhararagi/vtuber/actions/runs/38215869281), restore/build/tests/publish/upload **SUCCESS**.
- **Ejecución #123, SHA `18558d4964b57705ccf16c823f124e3bd6fc7a26`:** compilación Release **SUCCESS**, tests **FAIL** en `ProductStoreTests.CommandsPersistAndRunLocally` porque el caso de prueba usaba un cooldown de 2 segundos y el test tardó más que eso antes de comprobar la segunda llamada. Se cambió la prueba a 60 segundos para evitar que la duración del runner altere el resultado; el cooldown de producción sigue configurable y en segundos. El siguiente CI debe confirmar la corrección.
- **Sprites por expresión:** añadidos en el commit `71946203d89042ac1a756a4f01e2310bea82040c`; validación de CI pendiente.
- **Run:** [Windows CI #38214945910](https://github.com/jonhararagi/vtuber/actions/runs/38214945910) — restore, build Release, tests, publish self-contained Windows x64 y upload de artefacto: **SUCCESS**.
- **Artefacto:** [VTuberBot-Product-Candidate-win-x64](https://github.com/jonhararagi/vtuber/actions/runs/38214945910/artifacts/11732285947) · ID `11732285947` · 77,780,953 bytes · SHA-256 `1c6cd363b2dc4eef1b9fa4dc0f5778b7ea9904a0f81d01e21066466de1381882` · expira el 2026-10-26 06:56:50 UTC.
- Después de ese SHA se añadieron una tarjeta de alerta temporal al overlay y edición/pausa de timers, además de corregir textos y metadatos del paquete. El artefacto anterior no contiene esos cambios y no debe entregarse como el HEAD final; se requiere un nuevo CI/artifact exacto.

### Limitaciones explícitas del candidato

- **PENDIENTE:** reconexión automática de EventSub, recuperación de suscripciones revocadas, estilos/plantillas configurables de alertas y más tipos de eventos.
- **PENDIENTE:** rig 2.5D avanzado con pivotes/huesos/física, expresiones faciales basadas en assets y lip-sync real por fonemas/visemas.
- **IMPLEMENTADO EN CÓDIGO:** detector local de actividad acústica con suavizado, umbral y retención configurable; alimenta el estado Talking/Idle del overlay. No es reconocimiento de habla.
- **IMPLEMENTADO EN CÓDIGO:** cola TTS local limitada a 32 frases, procesada de forma secuencial en un hilo STA; prioridades Alta/Normal/Baja y limpieza de pendientes. La frase en curso no se interrumpe.
- **PENDIENTE:** enrutamiento avanzado de audio y lip-sync por fonemas/visemas.
- **PENDIENTE:** acciones remotas de moderación (borrado/sanción) con permisos y confirmaciones; hoy solo se registran coincidencias en modo observación.
- **IMPLEMENTADO EN CÓDIGO:** editor de capas con posición X/Y, escala, rotación y orden persistidos; preview y overlay comparten la configuración.
- **IMPLEMENTADO EN CÓDIGO:** sprites opcionales por estado (Idle, Talking, Blink, Happy, Surprised), persistidos bajo LocalAppData y utilizados por preview/overlay. Si no existe sprite para un estado, usa la imagen base.
- **PENDIENTE:** pivotes/parentado por huesos, física articulada, presets por expresión y lip-sync real por fonemas/visemas; los timers permiten crear, editar y pausar/reactivar mensajes.
- **PENDIENTE:** validación manual global en el Windows del usuario. El CI no valida dispositivos de audio, OAuth real con su cuenta, runtime de WebView2 ni su instalación local de OBS/Twitch.

### Siguiente paso

1. Repetir CI y generar el artefacto después de las correcciones de guía/metadata para que el paquete coincida con el HEAD final.
2. Crear un único PR de producto contra `v0.2.3/stabilization-gate`, dejando PR #9 abierto y sin modificar ni fusionar `main`.
3. Entregar un único candidato para validación global; pedir una sola prueba integral al usuario, no pruebas por cada corrección.


### Trabajo adicional — actividad acústica del micrófono

- Se añadió `VoiceActivityDetector` en Core, sin dependencias externas, con suavizado, umbral configurable y retención para evitar parpadeo.
- La página Voz y audio aplica el detector a muestras reales del micrófono y actualiza el cue Talking/Idle del overlay. Se distingue explícitamente de reconocimiento de voz y del lip-sync por fonemas.
- Se añadieron pruebas automáticas de activación, retención, liberación, suavizado y validación de parámetros.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en el HEAD de esta actualización.


### Trabajo adicional — sprites por expresión del avatar

- Se añadieron ajustes locales para asignar sprites Idle/Talking/Blink/Happy/Surprised usando archivos del usuario guardados en LocalAppData.
- El preview y el overlay usan el sprite del estado activo cuando existe, con fallback a la imagen base; las capas mantienen sus transformaciones.
- El estado Talking generado por el micrófono activa el sprite correspondiente si fue configurado.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en el HEAD que incluye esta actualización.


### Trabajo adicional — cola de voz local priorizada

- Añadido `LocalSpeechQueue`: voz Windows SAPI secuencial en un hilo STA, con prioridad Alta/Normal/Baja, límite de 32 elementos, contador de pendientes y limpieza de pendientes.
- El panel encola voz sin bloquear la ventana y muestra su estado en el pie; la voz activa termina antes de reproducir la siguiente frase.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD. El motor necesita Windows SAPI y voces instaladas para emitir audio real.


### Trabajo adicional — moderación manual autenticada de Twitch

- Añadido `TwitchModerationClient` para resolver IDs por login y ejecutar los endpoints oficiales de ban/timeout y retirar ban/timeout.
- La página Moderación tiene controles explícitos para timeout con duración seleccionable, ban permanente y desbanear; cada operación requiere confirmación del usuario. Las reglas regex siguen en modo observación y no ejecutan sanciones remotas automáticamente.
- El flujo OAuth solicita `moderator:manage:banned_users`. Las llamadas explican errores de token/scope, permisos del canal y límites; no registran secretos.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD. No se prueba contra una cuenta real sin credenciales/permisos del dueño.


### Trabajo adicional — voz de alertas Twitch

- Añadida una opción persistente en la pantalla Twitch para activar la lectura por voz local de alertas EventSub.
- Las alertas usan la cola TTS con prioridad Alta, sin interrumpir la frase actual; si la cola está llena, el caso se registra y no se presenta como hablado.
- Confirmación visual de alertas continúa en el overlay y el registro de actividad.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD. Requiere EventSub autenticado y voz SAPI instalada para audio real.


### Trabajo adicional — reconexión del chat IRC

- Se reemplazó la sesión IRC de una sola conexión por un supervisor con reconexiones escalonadas (1, 2, 4, 8, 16 y 30 segundos), TLS y respuesta a PING/PONG.
- Se informa conectado solo después de la confirmación de registro/unión de Twitch. Un rechazo explícito de autenticación no queda en un bucle de reintentos: requiere volver a autorizar.
- Los mensajes salientes comparten un semáforo con PONG y el cambio de sesión para evitar escrituras concurrentes al StreamWriter.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD.


### Trabajo adicional — parser correcto de mensajes IRC etiquetados

- Se añadió `TwitchIrcMessage` en Core para separar tags IRCv3, prefix, comando, parámetros y mensaje final, incluida la decodificación de escapes de tags.
- `HandleTwitchMessage` ahora obtiene el usuario desde el prefix y el texto desde el campo trailing de `PRIVMSG`, en lugar de interpretar mal el primer ` :` de un mensaje etiquetado.
- Añadidas pruebas de mensajes `PRIVMSG` con badges/display-name/message ID, escapes IRCv3 y entradas malformadas.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD.


### Corrección de CI — supervisor IRC

- La ejecución #131 falló al compilar por una asignación residual a `connectedChannel` que ya no existía después de quitar ese campo sin uso.
- Se eliminó la referencia residual. No era un fallo de la lógica de reconexión; igualmente el cambio completo necesita volver a pasar build/tests en el nuevo HEAD.
- Estado del parser IRC y de esta corrección: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI**.


### Trabajo adicional — juegos y sorteos conectados al chat

- Crear un sorteo anuncia la apertura y el comando `!entrar`; elegir ganador anuncia el resultado si Twitch IRC está conectado.
- Los minijuegos publican el resultado en el chat cuando la conexión está activa; la sesión y recompensa quedan guardadas en SQLite aunque falle el envío de chat.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD.


### Trabajo adicional — argumentos de comandos de Twitch

- Los comandos personalizados ahora reciben lo escrito después del nombre y admiten las variables `{args}` y `{message}`, además de `{user}`, `{points}` y `{roll}`.
- Las respuestas que superarían el límite de 500 caracteres se rechazan antes de aplicar el cooldown, para no simular envío y no castigar el comando con cooldown si la salida es demasiado larga.
- Añadida prueba de persistencia/ejecución que valida el reemplazo de los argumentos; estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD.


### Trabajo adicional — ciclo de vida de credenciales OAuth

- `TwitchOAuth` ahora conserva AccessToken, RefreshToken, expiración y scopes; valida contra el endpoint oficial de Twitch y refresca con el refresh token rotado cuando quedan 10 minutos o menos.
- La persistencia usa DPAPI CurrentUser y JSON protegido, con lectura compatible de tokens cifrados por versiones anteriores.
- El chat y EventSub validan el token antes de conectar. Mientras están solicitados, un monitor cada cinco minutos valida la sesión y restaura conexiones si hubo rotación de access token. Si el refresh token ya no es válido, se muestra que hace falta volver a autorizar; no se inventa una conexión exitosa.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en el HEAD que contiene esta actualización.


### Revisión posterior — OAuth/credenciales
- La clase de excepción privada de OAuth usa un cuerpo de clase explícito compatible con C# y MainWindow importa `System.Text.Json` para serializar el registro de credenciales.
- El monitor de credenciales no desconecta el chat por fallos transitorios de red; solo desactiva conexiones cuando hay evidencia de token expirado/revocado sin renovación posible.
- Estado: pendiente del resultado del CI de este nuevo HEAD.


### Trabajo adicional — timers persistentes y anti-spam

- La tabla de mensajes automáticos ahora conserva `last_run_utc` con migración compatible para bases existentes.
- Al cambiar un timer se mantiene su última ejecución; reiniciar VTuberBot ya no reinicia todos los cooldowns.
- El scheduler evita ejecuciones solapadas y limita el envío a un mensaje automático por tick; si Twitch está desconectado no consume el intervalo y el diagnóstico de desconexión se limita a una entrada cada 10 minutos por regla.
- Se rechazan mensajes automáticos vacíos o de más de 500 caracteres, y la marca de última ejecución se escribe después del envío confirmado.
- Añadida prueba de SQLite para round-trip de fecha, edición y conservación del intervalo de la última ejecución.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD.


### Corrección de prueba — timers con datos semilla
- La base de pruebas incluye el timer semilla `Bienvenida`; la prueba de persistencia identifica ahora el timer `Timer` por nombre en lugar de asumir que la tabla contiene solo una fila.
- Estado: ajuste de test implementado; validar en Windows CI.


## Estado de continuidad — ejecución retomada (12 de octubre de 2026)

- **Rama de producto:** `product/complete-vtuber-experience`.
- **Último commit de referencia confirmado con CI verde antes de esta mejora de UI:** `45f03622e71491882e5ae8bbc2c7c279cff73a6e`.
- **Windows CI #140 / [run 38218540737](https://github.com/jonhararagi/vtuber/actions/runs/38218540737): SUCCESS** en ese SHA exacto: restore, build Release, tests, publish self-contained win-x64 y upload artifact.
- Artefacto asociado a esa referencia: `VTuberBot-Product-Candidate-win-x64-45f03622e71491882e5ae8bbc2c7c279cff73a6e`; digest `sha256:884c0d6bce125af440870097b58e7c9627a4bf4b87efc1f7d4c33aded18007ad`; artifact ID `11733571463`; caduca `2026-10-26T07:36:14Z`.
- Esta revisión añade eliminación persistida de mensajes programados, botón de limpieza del formulario y una prueba automatizada para borrar el timer y rechazar la repetición de una eliminación. **Pendiente validar CI en el nuevo HEAD.**
- Mantener el PR de estabilización separado: no fusionarlo, no marcar V0.2.3 final y no iniciar V0.3 en esta tarea sin autorización explícita.


### Trabajo adicional — previsualización segura de comandos

- Se separó el render de respuestas del camino de ejecución: `PreviewCommandResponse` acepta la respuesta aún no guardada, las variables y argumentos de prueba sin modificar cooldown, crear perfiles de comunidad ni escribir un evento de ejecución.
- El editor de comandos incluye un campo de argumentos de prueba y muestra `{args}`/`{message}`; el botón de preview ya no ejecuta un comando real ni consume su cooldown.
- Prueba de regresión: una previsualización de la respuesta editada no crea el usuario de prueba y no bloquea ejecuciones reales siguientes.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en el nuevo HEAD.


### Corrección de persistencia — defaults y timers eliminados

- Los defaults se aplican ahora una sola vez con el marcador SQLite `seed.defaults.v1`. En bases existentes sin marcador, la migración detecta datos anteriores y registra la versión sin volver a crear defaults que el usuario quizá eliminó.
- Esto hace persistente eliminar el timer predeterminado `Bienvenida`; el botón ya no dará la impresión de borrarlo solo para que reaparezca al reiniciar.
- La lista de timers muestra también el último envío persistido.
- Se añadió prueba de reinicio del `ProductStore` tras borrar el timer incorporado.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI**.


### Corrección de la prueba de previsualización — CI #142

- Build Release pasó; una prueba falló porque tras la primera ejecución real se esperaba que otro usuario pudiera ejecutar inmediatamente el mismo comando, aunque el cooldown es global por comando.
- Se ajustó la prueba a la semántica correcta: la previsualización no consume el cooldown; la primera ejecución real sí lo inicia y la segunda ejecución durante el intervalo queda bloqueada.
- No fue un fallo del código de preview; el test anterior tenía una expectativa contradictoria con el diseño actual del cooldown. El nuevo HEAD debe validarlo.


### Trabajo adicional — acciones automáticas de moderación con interruptor global

- Las reglas solo siguen en observación si `twitch.auto_moderation.enabled=false` (valor predeterminado). La UI pide confirmación antes de habilitar la ejecución en el chat real.
- Acciones por regla: `flag` registra únicamente; `warn` publica un aviso; `delete` elimina el mensaje individual usando su tag IRCv3 `id`; `timeout` impone 600 segundos; `ban` es permanente.
- Se aplica máximo una acción no informativa por mensaje y una ventana de 5 minutos por usuario/regla. Si falta el mensaje ID para `delete`, no se intenta la API.
- OAuth ahora solicita `moderator:manage:chat_messages` además de `moderator:manage:banned_users`; los usuarios que ya habían autorizado deben repetir OAuth para obtener el nuevo scope.
- Las reglas validan la acción permitida y se añaden pruebas de persistencia de acciones y rechazo de acciones desconocidas.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en el nuevo HEAD. No se ha afirmado que una cuenta Twitch real acepte estas acciones sin probar credenciales/permisos del usuario.


### Aclaración de permisos de moderación
- El manual de usuario y la pantalla Twitch enumeran ahora `moderator:manage:chat_messages` además de `moderator:manage:banned_users`.
- El endpoint de borrado utilizado es el oficial `DELETE /helix/moderation/chat` y requiere `broadcaster_id`, `moderator_id` y el ID individual del mensaje; el cliente rechaza un ID vacío para evitar una solicitud de borrado masivo. Fuente oficial consultada: https://dev.twitch.tv/docs/api/reference#delete-chat-messages
- Las reglas automáticas siguen desactivadas por defecto y necesitan confirmación explícita para afectar a un chat real.


### Trabajo adicional — sorteo seguro y cierre de una sola vez

- La selección del ganador utiliza `RandomNumberGenerator.GetInt32` sobre las entradas almacenadas, en lugar de `Random.Shared`.
- La prueba del sorteo comprueba que el ganador pertenecía al conjunto de entradas y que un segundo intento de sortear el mismo sorteo cerrado se rechaza.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en el nuevo HEAD.


### Trabajo adicional — selección de voz SAPI, ritmo y volumen

- `LocalSpeechQueue` enumera voces SAPI instaladas y guarda el token de voz, velocidad y volumen por frase encolada.
- La página Voz y audio puede actualizar la lista de voces de Windows, elegir voz predeterminada o instalada, ajustar velocidad de -10 a 10 y volumen de 0 a 100 y guardar preferencias en SQLite.
- Las alertas EventSub usan también la voz/ritmo/volumen guardados, manteniendo prioridad alta y sin interrumpir la frase actual.
- No se afirma que Windows incluya una voz anime: depende de las voces SAPI instaladas por el usuario.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD.


### Trabajo adicional — cooldown global o por usuario
- Se agregó la columna `cooldown_scope` con migración compatible; comandos existentes conservan el valor predeterminado `global`.
- Se añadió `product_command_user_cooldowns` para persistir la última ejecución por comando y nombre de usuario, sin reiniciar las marcas al reabrir la app.
- El editor ofrece Global o Por usuario y muestra el modo en la lista. El router bloquea las ejecuciones según el alcance configurado.
- Prueba: mismo espectador bloqueado por su propio cooldown, otro espectador puede usar el comando y la marca sigue bloqueando al primero después de abrir otro ProductStore.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD.


### Revisión de calidad — timeout al enumerar voces SAPI
- La enumeración de voces ahora completa un `TaskCompletionSource` desde su hilo STA y espera con un límite de cinco segundos. Si se vence, la UI recibe un timeout sin cerrar un wait handle que el hilo COM pudiera intentar señalar después.
- La guía de comandos ahora diferencia cooldown global y por usuario, variables `{args}`/`{message}` y previsualización sin efectos laterales. README enumera con más precisión las funciones de la rama y aclara que el CI no valida una cuenta Twitch real.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este nuevo HEAD.


### Trabajo adicional — parpadeo automático del overlay
- El avatar ahora parpadea automáticamente con intervalos variables aproximados de 3,5 a 7,5 segundos mientras el overlay está visible.
- Usa el sprite Blink configurado si existe y conserva la fallback visual si no existe uno.
- La opción de reducir movimiento suspende el parpadeo automático junto con la respiración/escala; el estado Blink manual continúa disponible en Avatar Studio.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD. No es tracking facial ni lip-sync por fonemas.


### Trabajo adicional — alias y comandos preinstalados
- Se agregó la columna SQLite `aliases` con migración compatible y una lista de edición separada por comas en el formulario de comandos.
- El router resuelve nombre principal o alias por el mismo camino de ejecución y cooldown. Se aceptan hasta 20 alias por comando y se rechazan conflictos con nombres/alias de otros comandos.
- Los comandos preinstalados se guardan ahora sin `!` en el nombre; la migración normaliza los tres nombres de fábrica heredados `!hola`, `!puntos`, `!dado`, que no coincidían con la consulta del router al quitar el prefijo.
- Se añadieron pruebas de alias persistidos, resolución por alias, conflictos y comandos de fábrica.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD.


### Prueba de migración de comandos antiguos
- La migración ahora elimina solo el registro preinstalado malformado `!hola`/`!puntos`/`!dado` cuando ya existe el comando canónico correspondiente, y conserva el comando personalizado del usuario.
- Los alias vacíos tras una coma final o con espacios se ignoran, para que ese formato de edición no bloquee el guardado.
- Se añadió una prueba con una base SQLite creada en el esquema antiguo que contiene tanto `hola` personalizado como `!hola` preinstalado; la migración debe preservar el personalizado y dejar ejecutable `!hola`.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI**.


### Corrección de prueba — nombre canónico del comando preinstalado
- El CI #152 compiló correctamente y 137/138 pruebas pasaron. La prueba de cooldown por usuario intentaba crear un segundo comando `hola`, ahora reservado por el seed preinstalado corregido; por eso fallaba la preparación del fixture, no la lógica de cooldown.
- La prueba usa ahora el nombre único `cooldown-demo` para seguir comprobando el cooldown global por usuario, persistencia tras reabrir SQLite y rechazo del scope inválido.
- La corrección de migración y alias del HEAD #153 también queda cubierta por el nuevo CI que se dispara con este commit.


### Trabajo adicional — permisos por rol en comandos
- Se añadió `permission_scope` con migración compatible y selector Todos/Suscriptores/VIP/Moderadores/Broadcaster en el editor de comandos.
- `CommandPermissionPolicy` valida el nivel requerido y resuelve los roles a partir de etiquetas de servidor de Twitch (badges, mod, subscriber, vip) y la identidad del canal. No se acepta un rol declarado dentro del texto del chat.
- Los comandos restringidos se bloquean antes del cooldown y de la creación/actualización de puntos; el rechazo se registra. Moderadores heredan permiso para comandos de suscriptor/VIP; Broadcaster tiene acceso a todos, pero Moderador no puede usar el nivel exclusivo Broadcaster.
- Se añadieron pruebas para la matriz de permisos y el análisis de roles IRC.
- **CI #154** [run 38246959459](https://github.com/jonhararagi/vtuber/actions/runs/38246959459) validó el HEAD anterior con restore/build/tests/publish/upload SUCCESS. Esta integración de permisos está **IMPLEMENTADA EN CÓDIGO; PENDIENTE DE CI**.


## Estado de entrega verificado — 12 de octubre de 2026

- **Commit de producto validado:** `025977f098e601566293fca53d35bbc02a29fa85` (`test: pass subscriber role when testing protected alias`).
- **Windows CI #156:** [run 38247486943](https://github.com/jonhararagi/vtuber/actions/runs/38247486943) — **SUCCESS**. Restore, Build Release, tests (**141/141 passed**), publish self-contained Windows x64 package y upload artifact, todos correctos.
- **Artefacto reproducible:** `VTuberBot-Product-Candidate-win-x64-025977f098e601566293fca53d35bbc02a29fa85`; artifact ID `11745659138`; tamaño `77,836,065` bytes; digest `sha256:cf85192aa35986681a76e62a8e06d102f5cc86836d31c63ca5914bfd34f67ce5`; expiración `2026-10-26T12:13:08Z`.
- **Cambios cubiertos en este candidato:** alias persistentes; nombres canónicos de comandos preinstalados y migración que preserva comandos de usuario; cooldown global/por usuario; preview de comando sin efectos laterales; permisos por rol derivados de tags IRC; selector SAPI/velocidad/volumen; parpadeo automático del overlay; sorteo con aleatoriedad criptográfica.
- **Límite de aceptación:** estos 141 tests y la publicación certifican el build, no un end-to-end con credenciales reales del usuario. No se declara el producto finalizado todavía; las limitaciones corregidas arriba describen los siguientes huecos reales.
- **Estado del control de versiones:** la rama `product/complete-vtuber-experience` es la vía de integración. El PR #9 de estabilización permanece separado y abierto; `main` no se fusionó ni modificó en esta entrega.
- **Siguiente trabajo recomendado dentro de la misma rama:** backup/restauración guiados de SQLite y un modo de prueba/preview de reglas de moderación; después, una única validación end-to-end de la build actual con Twitch/OBS reales.


### Trabajo adicional — backup y restauración guiados de SQLite

- Se añadieron `ProductStore.BackupTo` y `RestoreFromBackup`: usan SQLite Online Backup API (snapshot consistente), verifican `PRAGMA integrity_check`, exigen tablas/columnas compatibles y rechazan archivos corruptos o de otro esquema antes de tocar la base activa.
- Antes de restaurar se crea una copia automática de emergencia de la base actual. Si la restauración falla, el método intenta revertir a esa copia y señala su ruta.
- Ajustes/Privacidad ahora permite crear backup con diálogo SaveFileDialog y restaurar con confirmación explícita. La restauración se bloquea con Chat/EventSub activos o mientras el scheduler procesa un mensaje; la UI invalida caches de cooldown y refresca la página.
- La guía aclara que SQLite no incluye assets de imagen, cookies de Telegram ni credenciales OAuth DPAPI.
- Pruebas nuevas: restauración conserva el estado de la copia y crea una copia de emergencia del estado anterior; archivo inválido se rechaza sin modificar la DB activa.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este nuevo HEAD.


### Corrección de CI — handles de SQLite en Windows al crear backup
- En la primera prueba de backup/restauración, Build Release pasó; 142/143 pruebas pasaron. La prueba nueva falló en `File.Move` porque Microsoft.Data.Sqlite conservaba un handle del archivo temporal en el pool tras cerrar la conexión.
- Las conexiones temporales de escritura, validación y lectura de backup ahora usan `Pooling=false`, para que el handle se libere al salir del `using` antes de renombrar la copia en Windows.
- No se ha marcado el backup como validado hasta que pase la ejecución CI de este nuevo HEAD.


### Protección adicional de moderación automática

- Se centralizó la decisión de ejecución en `AutomaticModerationPolicy` para que los modos flag-only, opt-in global, IDs de mensajes y cooldown se puedan comprobar por pruebas.
- Las sanciones automáticas ya no apuntan al broadcaster ni a los moderadores según las etiquetas de Twitch; los VIP/suscriptores no se protegen implícitamente salvo que el canal configure otra política.
- El cooldown por usuario/regla ahora se registra solo cuando la acción fue ejecutada correctamente; un fallo de API no bloquea el siguiente intento durante cinco minutos.
- Se añadieron pruebas automáticas para opt-in global, usuarios protegidos, ID requerido para delete, cooldown, acciones soportadas y acción desconocida.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD.


### Diagnóstico de permisos de moderación

- Los fallos HTTP al eliminar un mensaje ahora señalan el scope requerido `moderator:manage:chat_messages`; timeout/ban/desbanear señalan `moderator:manage:banned_users`.
- Los errores al resolver la identidad ya no atribuyen automáticamente el fallo al scope de sanciones: informan validez del token, Client ID y posible revocación.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en el HEAD de este cambio.


### Aislamiento de mensajes sancionables

- Si una regla pasa la política de opt-in y límites para ejecutar una acción automática, el mensaje se excluye de puntos de actividad y del router de comandos personalizado.
- Este bloqueo también aplica si la llamada remota falla: un error de permisos/red no convierte el mismo mensaje en una ejecución de comando o recompensa normal. Acciones bloqueadas por usuario protegido, falta de ID o cooldown no aplican la sanción y quedan registradas.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD.


### Mejora de usabilidad del Avatar Studio — edición directa con ratón

- La previsualización permite arrastrar la capa visible seleccionada para ajustar X/Y; los sliders y el arrastre comparten la misma vista previa.
- El desplazamiento solo se persiste al pulsar «Aplicar y guardar transformación». Se añadió «Restablecer posición y giro» para volver a X=0, Y=0, escala=1 y rotación=0 sin borrar la capa.
- La salida overlay ya no reduce la opacidad del personaje entero para fingir un parpadeo. El parpadeo visual requiere un sprite Blink de ojos cerrados; se documentó ese requisito en la guía y README.
- Estado de código: **IMPLEMENTADO; PENDIENTE DE CI** en el commit siguiente.


### Organización de la interfaz y salud en vivo

- Las áreas de trabajo están separadas en clases parciales bajo `src/VtuberBot.App/Workspaces/`: Dashboard, Avatar Studio, Twitch, Comunidad, Moderación, Audio, Integraciones, Diagnóstico y Ajustes. Conservan el mismo shell WPF y los servicios actuales; esto mejora la estructura sin introducir una migración MVVM en bloque.
- El Dashboard lee el `HealthSnapshot` real del monitor Core y actualiza CPU del proceso y RAM residente cada dos segundos mientras esa página está visible. No se muestran valores GPU/FPS inventados; esas fuentes siguen declaradas no disponibles.
- Los mensajes IRC recibidos se procesan secuencialmente por el dispatcher y con una cola acotada de 1000 elementos para evitar que varias ejecuciones de comandos/moderación se solapen mientras esperan llamadas de red. Si la carga excede el límite, se registra el descarte y no se permite crecimiento ilimitado.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en el nuevo HEAD.


### Minijuegos con reglas diferenciadas y saldo transaccional

- Se reemplazó el resultado genérico de ganar/perder por `MiniGameEngine`: moneda Cara/Cruz, ruleta europea (0–36 con 0 verde y colores correctos), dado Bajo/Alto. Los resultados aleatorios usan `RandomNumberGenerator`; las reglas se pueden resolver determinísticamente en pruebas.
- Las elecciones disponibles cambian al cambiar de juego. La vista muestra las reglas, el saldo, el resultado y el historial reciente en la propia página; no depende de un popup para cada victoria/derrota.
- La apuesta válida es 0–10.000 puntos. Cero activa práctica gratuita. Una ronda con apuesta real primero comprueba saldo suficiente y después ejecuta débito, premio, ledger e historial dentro de una misma transacción SQLite. No puede dejar puntos negativos ni cobrar una apuesta sin registrar resultado.
- Pagos: moneda/dado y rojo/negro pagan 2× apuesta total; verde (0) en ruleta paga 36×; perder paga cero. El histórico registra elección, salida, apuesta, pago, neto y saldo.
- Añadidas pruebas de reglas de cada juego, elecciones inválidas, rangos, práctica gratuita, liquidación del saldo y saldo insuficiente.
- **CI Windows #166 SUCCESS**: 173/173 pruebas aprobadas, Build Release y publicación self-contained Windows x64 correctos. Artefacto ID `11749431177`, digest `sha256:d8b33fda5ece6335ab7a1671ff097f08bbd590484c2715d245217d5a397b7070`; run [38252396469](https://github.com/jonhararagi/vtuber/actions/runs/38252396469).


### Pulido visual del shell WPF

- Botones con esquinas redondeadas, hover/pressed/focus/disabled visibles; campos de texto con borde redondeado y foco cian; selección de elementos de lista con contraste consistente.
- La navegación lateral resalta la página actual en cian/azul y conserva el resto en superficies oscuras, ayudando a distinguir la sección activa.
- Estado de código: **IMPLEMENTADO; PENDIENTE DE CI** en el commit siguiente.


### Persistencia de configuración de voz y micrófono

- Se eliminó el umbral/retención hardcodeados del inicializador y se cargan desde SQLite al crear la ventana, con valores predeterminados seguros cuando no hay configuración válida.
- El umbral acústico (2–30%), la retención (100–1200 ms) y el nombre del dispositivo de entrada se guardan en cuanto se cambia el ajuste o se inicia la captura.
- Al abrir Voz y audio, el selector restaura el dispositivo por nombre si Windows aún lo enumera; si ya no existe, selecciona el primer dispositivo disponible sin fingir que está capturando.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en el HEAD siguiente.


### Trabajo adicional — perfiles de avatar independientes

- Añadidas tablas `avatar_profiles` y `avatar_profile_expressions`; cada perfil tiene nombre, imagen base y sprites Idle/Talking/Blink/Happy/Surprised separados.
- Las capas y sus transformaciones quedan asociadas al perfil activo. Crear perfil vacío, duplicar el actual y eliminar un perfil (sin borrar imágenes del disco ni permitir eliminar el último perfil) ya están disponibles desde Avatar Studio.
- La migración conserva el `avatar.path`, capas y expresiones globales de instalaciones anteriores como perfil predeterminado. Las operaciones de capas se filtran por perfil para impedir que una selección antigua modifique otro avatar.
- Se añadieron pruebas de aislamiento entre perfiles, duplicación con transformaciones/expresiones y protección al eliminar el último perfil.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** para el commit de esta actualización. La validación manual global continúa pendiente; este commit no se declara candidato definitivo.


### Revisión estática de la implementación de perfiles
- Se movió la lectura del ID de perfil activo antes de abrir la transacción de creación/eliminación para no ejecutar consultas auxiliares sin asociarlas a una transacción SQLite ya activa en Windows.
- El CI del commit con la función completa sigue siendo la fuente de validación; esta corrección queda pendiente de su nueva ejecución.


### Pruebas extra de perfiles y assets

- Se añadió una prueba de migración desde el esquema antiguo de SQLite que solo tenía `avatar.path`, `avatar.expression.Talking` y `avatar_layers`. Verifica que la imagen base, las capas y el sprite se conserven en el perfil principal al abrir la base con la versión nueva.
- Importar/cambiar/quitar un sprite de expresión ahora refresca también el overlay ya abierto, evitando que la previsualización y la salida de OBS diverjan hasta la próxima apertura.
- La importación de la imagen base agrega un sufijo GUID para no fallar si se importan dos archivos durante el mismo segundo.
- Estado: código actualizado; pendiente CI en el HEAD nuevo.


### Trabajo adicional — paquete portable de avatar

- Avatar Studio ahora exporta un perfil `.vtuberavatar` (ZIP con manifest versionado) que incluye la imagen base, las capas, transformaciones/visibilidad/orden y los sprites de expresión utilizados.
- La importación valida formato y versión, limita cantidad/tamaño de archivos, comprueba extensiones, rechaza rutas inseguras y carga cada imagen antes de crear un perfil nuevo. No extrae los nombres de ruta directamente del ZIP; utiliza rutas locales generadas por la aplicación.
- Si la creación del perfil falla, se intenta retirar el perfil parcial y recuperar el perfil anterior; los assets del paquete solo se conservan cuando la importación termina.
- Credenciales, tokens, cookies y la base de datos completa no forman parte del paquete.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en el HEAD que contiene esta función.


### Límite de tamaño del paquete de avatar

- La exportación ahora rechaza perfiles cuyos assets sumados superan 250 MB, antes de copiar el asset que excedería el límite; mantiene un máximo de 25 MB por imagen.
- Esta limitación evita que un perfil con muchas copias/repeticiones llene el disco durante la compresión. Pendiente de CI en este HEAD.


### Trabajo adicional — deduplicación de eventos EventSub

- Se añadió `EventNotificationDeduplicator` en Core con una ventana acotada de 10.000 IDs, thread-safe y compartida por la instancia EventSub incluso cuando cambia de WebSocket.
- Las notificaciones duplicadas con el mismo `metadata.message_id` se descartan antes de invocar alertas/voz/log de evento; un mensaje sin ID se procesa porque no hay una clave estable para deduplicar.
- Añadidas pruebas de duplicados, IDs distintos, capacidad acotada, IDs evictados y concurrencia.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD.


### Correcciones de CI e integración — EventSub dedupe y recuperación de OBS

- CI #175 detectó la regla xUnit2031 en la prueba de concurrencia del deduplicador; se sustituyó el filtro previo por el overload `Assert.Single(collection, predicate)`, preservando la comprobación de un único evento aceptado.
- Se añadió reconexión automática para OBS WebSocket v5. Tras una caída inesperada, las peticiones pendientes fallan de forma explícita y la sesión reintenta con backoff limitado 1/2/4/8/16/30 s. Una desconexión manual cancela los reintentos; el estado conectado vuelve a activarse solo después de completar Hello/Identify.
- La contraseña de OBS se conserva solo en memoria para reautenticar la conexión durante esa ejecución. No se escribe en mensajes de diagnóstico.
- Se añadieron pruebas de la política de backoff acotada.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** para el nuevo HEAD. No se afirma prueba de reconexión contra OBS real.


### Ajuste preventivo de nulabilidad en OBS reconectable
- Se usan variables locales no nulas para host, contraseña en memoria y socket autenticado antes de arrancar el supervisor. Esto evita depender del análisis de nulabilidad de campos mutables bajo `TreatWarningsAsErrors`.
- La política de reconexión no cambia; pendiente CI en el HEAD nuevo.


### Corrección CI — semáforo de envío de OBS
- CI #176 falló al compilar la clase nueva de OBS porque `SemaphoreSlim` se inicializó sin los parámetros requeridos. Se corrigió a `new(1, 1)`, manteniendo un único escritor concurrente.
- Estado: pendiente del CI completo en el siguiente HEAD.


### Pulido estático del cliente OBS
- Se retiraron tres campos de configuración que ya no se leen porque el supervisor conserva los valores en sus argumentos; así se evita ruido de compilador bajo `TreatWarningsAsErrors`.


### Trabajo adicional — deduplicación de revocaciones y diagnóstico por causa

- La caché de IDs EventSub ahora se aplica tanto a `notification` como a `revocation`, para evitar que una revocación duplicada produzca avisos repetidos en la actividad.
- Se añadió `EventSubRevocationPolicy` para distinguir autorización revocada, usuario eliminado, versión retirada, rol de moderador eliminado y problemas de transporte, con pruebas para los estados y para códigos futuros/desconocidos.
- Solo `authorization_revoked` exige una nueva autorización OAuth explícita; los demás motivos no se presentan como reparados ni se reintentan silenciosamente cuando falta información para hacerlo de forma segura.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en este HEAD.


### Corrección preventiva — campos de guía de revocación
- Cada guía de revocación ahora declara explícitamente si exige reautorización; motivos que requieren actualización/rol o desconocidos no se equiparan a un fallo de OAuth.
- Se comprobarán los overloads de este modelo con el siguiente CI; el cambio no altera la decisión de no reintentar silenciosamente.
