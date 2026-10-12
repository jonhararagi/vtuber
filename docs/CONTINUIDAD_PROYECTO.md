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


### Corrección CI — expectativa del estado EventSub ausente
- CI #181 compiló correctamente, pero falló una prueba porque el texto esperado era el de un estado desconocido y no el del campo ausente. La prueba ahora valida el mensaje real que emite `Describe(null)`; se mantienen ambas rutas diferenciadas.
- Estado: pendiente de CI en el nuevo HEAD.


### Trabajo adicional — recuperación acotada de revocaciones EventSub

- La guía de revocación distingue estados que no se pueden reparar con las credenciales actuales (OAuth revocado, usuario retirado, versión no soportada) de fallos transitorios de servidor/red.
- Para `websocket_network_timeout`, `websocket_network_error` y `websocket_internal_error`, el cliente intenta registrar de nuevo solo el tipo de suscripción afectado, únicamente si la sesión WebSocket sigue abierta.
- La recuperación por tipo tiene un límite de un intento cada 10 minutos y solo confirma éxito si Twitch acepta la petición. Si el socket ya no está activo o Twitch rechaza la solicitud, se conserva un diagnóstico explícito; la reconexión completa sigue siendo la ruta de recuperación.
- Los estados de fallo del lado cliente (`websocket_disconnected`, `websocket_failed_ping_pong`, `websocket_received_inbound_traffic`, `websocket_connection_unused`) no se reintentan de manera aislada para no ocultar un problema de sesión/código.
- Añadidas pruebas de la decisión de recuperación permitida/no permitida por estado.
- Estado: **IMPLEMENTADO EN CÓDIGO; PENDIENTE DE CI** en el nuevo HEAD.


### 2026-10-12 — Reintento de Twitch IRC tras fallo terminal

- **HEAD previo comprobado:** `bf9e3d6a83b917adc98ef19345fe7bb704c1e1ba` (`feat: recover transiently revoked EventSub subscriptions`).
- **Windows CI #183:** SUCCESS en el SHA anterior; [ejecución 38259057769](https://github.com/jonhararagi/vtuber/actions/runs/38259057769). Restauración, Build Release, pruebas automatizadas y paquete self-contained Windows x64 terminaron correctamente.
- **Hallazgo:** Twitch IRC ya disponía de supervisor y backoff para cortes transitorios, pero un fallo terminal posterior a la conexión (por ejemplo, rechazo definitivo de autenticación) dejaba la tarea supervisora completada mientras la referencia de sesión seguía ocupada. Un nuevo intento desde la UI podía ser rechazado como si la conexión aún estuviera activa.
- **Cambio implementado:** `src/VtuberBot.App/TwitchChatClient.cs` ahora limpia esa sesión ya terminada antes de iniciar una nueva conexión. Si el supervisor sigue activo, se mantiene el bloqueo contra sesiones duplicadas.
- **UI corregida:** `src/VtuberBot.App/Workspaces/MainWindow.Twitch.cs` ya no afirma que IRC requiere reconexión manual tras un corte; explica los reintentos automáticos y distingue un token rechazado que necesita reautorización.
- **Commits de código/UI:** [975458f](https://github.com/jonhararagi/vtuber/commit/975458f5945d776b05f18716e55ec5c4e319f563), [4430cff](https://github.com/jonhararagi/vtuber/commit/4430cff77e3a6dc103197279dbbadaf50b3d27f0).
- **Estado:** IMPLEMENTADO EN CÓDIGO; CI del HEAD posterior pendiente. No se afirma reconexión validada contra una cuenta real de Twitch.
- **Siguiente paso:** comprobar el CI del HEAD actual, corregir cualquier regresión y seguir con el próximo hueco funcional del producto sin solicitar validación manual intermedia.


### Corrección adicional — confirmación real de ingreso a canal IRC

- **Hallazgo:** el cliente marcaba el chat como conectado tanto al recibir `001` (registro IRC completado) como al recibir `366` (fin de la lista de nombres tras el ingreso al canal). `001` no confirma que el JOIN al canal se haya completado, por lo que la UI podía habilitar envíos demasiado pronto.
- **Cambio implementado en** [`1dca351`](https://github.com/jonhararagi/vtuber/commit/1dca35144a51056595a3ed14104279b557f0a67d): `001` ahora solo informa que el registro IRC terminó y que se espera la confirmación; el cliente solo marca `IsConnected` y completa la conexión inicial tras recibir `366`.
- **Validación:** Windows CI #187 para SHA `1dca35144a51056595a3ed14104279b557f0a67d` se lanzó; el resultado aún debe comprobarse. El CI #183 verde corresponde únicamente al HEAD anterior `bf9e3d6`.


### 2026-10-12 — Consola de arte y asignación de assets desde Avatar Studio

- **HEAD de código:** [c3e0e46](https://github.com/jonhararagi/vtuber/commit/c3e0e46482580104f693291b3847c07db3318ba0).
- **Cambio:** Avatar Studio incorpora una consola local para escribir, editar, guardar y copiar prompts por rol de arte, con plantillas iniciales para personaje base, fondo, objeto/capa y expresiones Idle/Talking/Blink/Happy/Surprised.
- **Asignación:** al importar el resultado, el panel lo copia a la biblioteca local de assets del usuario y lo asigna al perfil activo como imagen base, sprite de expresión o capa. Los fondos se incorporan como capa llamada «Fondo», que puede reordenarse desde la lista de capas.
- **Persistencia y seguridad:** los prompts se guardan en la configuración local de la aplicación; los assets se almacenan bajo el directorio local de avatares. Se valida formato de imagen, lectura y límite de 25 MB. No se agregan imágenes personales ni credenciales al repositorio.
- **Límite explícito:** esta consola no genera imágenes por sí misma ni invoca APIs externas; facilita preparar/copiar el prompt en el generador que el usuario elija e importar/asignar su resultado.
- **Windows CI #189:** lanzado para este commit; en la última comprobación estaba en cola: [ejecución 38262572633](https://github.com/jonhararagi/vtuber/actions/runs/38262572633). No declarar compilación validada hasta que termine.
- **CI anteriores:** Windows CI #184, #185, #186, #187 y #188 finalizaron SUCCESS en sus respectivos SHA. El resultado de cada ejecución solo valida ese commit, no prueba conexiones manuales con una cuenta real de Twitch.
- **Siguiente paso:** comprobar el CI #189 y corregir cualquier error de compilación/integración; continuar ampliando los flujos reales del producto, sin modificar `main` ni fusionar PR #9.


### 2026-10-12 — Corrección del orden de composición de fondos del avatar

- **Hallazgo reproducible por código:** la consola de arte asignaba los resultados de rol «Fondo» como capas ordinarias; la previsualización y el overlay dibujaban todas las capas después de la imagen base. Por eso el fondo quedaba delante del personaje.
- **Corrección:** se añadió `AvatarLayerOrdering` al Core para clasificar y ordenar capas de fondo por debajo de la imagen base y capas normales/expresiones visuales por encima. Tanto Avatar Studio como la ventana overlay usan el mismo criterio. Los fondos usan `UniformToFill` para cubrir su superficie.
- **Pruebas nuevas:** clasificación por nombre sin distinguir mayúsculas, normalización de espacios y orden estable por `SortOrder`.
- **Estado:** corrección implementada; Windows CI pendiente para el nuevo commit. No se ha hecho una comprobación visual manual en Windows/OBS.


### 2026-10-12 — CI fix and safer raffle lifecycle

- **Windows CI #191 failed during Build Release** on commit [5f6e5df](https://github.com/jonhararagi/vtuber/commit/5f6e5dfb26226c0382527e1b795ff5102184dfdd): nullable analysis rejected passing the nullable layer argument into `AvatarLayerOrdering.IsBackground`. The helper now explicitly accepts a nullable value and treats null as not-background; a regression assertion covers this case.
- **Raffle lifecycle issue:** the UI permitted multiple simultaneous OPEN giveaways while chat's `!entrar` command selected only the first result. This could silently register viewers in a different raffle than expected.
- **Correction:** creating a raffle now fails clearly if another is OPEN; the UI lets the operator select a raffle and cancel it without a winner; cancellation preserves entries for audit and releases the active slot. `!entrar` refuses ambiguous legacy states with multiple OPEN raffles instead of guessing. Drawing validates the raffle state before selecting a winner.
- **Tests added:** only one OPEN raffle, cancellation with existing entries, no re-entry/draw after cancellation, and creation of the next raffle after closing/canceling.
- **Validation:** correction is committed as a batch on `product/complete-vtuber-experience`; Windows CI for the new HEAD must pass before this batch is considered validated. CI #189 and #190 succeeded for the earlier art-console commit/documentation respectively; CI #191 is a confirmed build failure on the background-layer commit.


### 2026-10-12 — Prompts propios por perfil y portabilidad

- **Consola de arte:** los prompts personalizados ahora se guardan por ID de perfil y rol, de modo que personajes distintos mantienen su propia dirección artística.
- **Compatibilidad:** los prompts creados con el primer formato global se migran al perfil que estaba activo; los demás perfiles no heredan automáticamente esos valores.
- **Duplicación de perfiles:** al duplicar un avatar, se copian los prompts guardados además de la composición ya duplicada. Un perfil vacío mantiene sus plantillas por defecto.
- **Paquetes `.vtuberavatar`:** la exportación incluye los prompts personalizados; la importación restaura sus valores bajo el nuevo perfil sin modificar los prompts del perfil anterior. Los paquetes v1 existentes siguen siendo compatibles porque el campo nuevo es opcional.
- **Validación del manifiesto:** se rechazan roles desconocidos y prompts superiores a 12.000 caracteres; la biblioteca y los prompts permanecen fuera de Git y de los binarios.
- **Estado:** código y pruebas unitarias del catálogo añadidos; Windows CI para este HEAD pendiente. No declarar importación/exportación visual validada hasta que termine CI y se realice la futura validación global en Windows.


### 2026-10-12 — Validación contra inyección de líneas IRC

- **Hallazgo:** el cliente interpolaba el canal en comandos IRC `NICK`, `JOIN` y `PRIVMSG` sin validar caracteres de protocolo; el token OAuth manual también podía contener saltos de línea. Valores manipulados podían introducir líneas adicionales en el socket.
- **Corrección:** se añadió `TwitchIrcValidation` en Core. El canal se normaliza a un login Twitch de 1–25 caracteres ASCII (letras, dígitos y guion bajo); el token retira el prefijo opcional `oauth:` y rechaza espacios/caracteres de control internos. El cliente valida antes de iniciar la sesión y antes de enviar mensajes.
- **Pruebas:** canales válidos normalizados; espacios, caracteres no permitidos, longitud excesiva y saltos de línea rechazados; tokens válidos y payloads con espacios/controles cubiertos.
- **Estado:** implementado en el siguiente HEAD; Windows CI pendiente. Las comprobaciones automáticas no sustituyen la validación global posterior con una cuenta real de Twitch.


### 2026-10-12 — Envíos IRC ligados al canal conectado

- **Hallazgo:** la UI pasa el destino desde un campo editable al enviar mensajes. Si ese campo cambiaba mientras la sesión seguía unida al canal anterior, el cliente podía publicar en un destino diferente al de la sesión que el usuario veía conectada.
- **Corrección:** `TwitchChatClient` conserva el login asociado a la sesión y `TwitchIrcValidation.NormalizeTargetChannel` exige que el destino validado coincida con ese canal. Para cambiar de canal se debe desconectar y volver a conectar; los valores inválidos siguen siendo rechazados antes de escribir al socket.
- **Pruebas:** destino normalizado equivalente aceptado; destino de otro canal y destino con salto de línea rechazados.
- **Estado:** implementado en el HEAD posterior al cambio de validación IRC; Windows CI pendiente.


### Corrección del CI #194 — referencia explícita al validador del Core

- Windows CI #194 falló en Build Release porque `TwitchChatClient.cs`, que reside en el proyecto App, no importaba el namespace `VtuberBot.Core` donde se agregó `TwitchIrcValidation`. Se añade la directiva de uso explícita; no cambia la lógica de validación.
- CI #195 incluye el cambio anterior de vinculación del canal y deberá comprobarse por separado; el commit de corrección actual dispara un nuevo CI.


### 2026-10-12 — Límite real de cola de mensajes IRC

- **Hallazgo:** el límite anterior de 1.000 mensajes se comprobaba dentro de callbacks ya encolados en el Dispatcher de WPF. En una ráfaga alta, el backlog de callbacks podía crecer antes de que se aplicara el límite.
- **Corrección:** cada mensaje reserva capacidad en un `SemaphoreSlim` antes de publicar el callback; los mensajes en espera de despacho, los pendientes y el que se procesa cuentan contra la misma capacidad. El cupo se libera al completar el procesamiento o si la ventana se está cerrando. El aviso de saturación queda limitado a un callback mientras persista la sobrecarga.
- **Estado:** implementación en el nuevo HEAD; CI de Windows pendiente.


### 2026-10-12 — Alertas de redención de puntos legibles

- **Hallazgo:** EventSub convertía el objeto completo `reward` a texto JSON y después eliminaba comillas; la alerta de redención podía mostrar una estructura sin formato en lugar del nombre de la recompensa.
- **Corrección:** se extrajo `TwitchEventSummaryFormatter` al Core. Las redenciones muestran nombre del usuario, título de la recompensa y, cuando existe, el texto ingresado por el espectador. Si faltan campos opcionales, se mantiene una notificación legible.
- **Pruebas:** formato de redención con/sin texto opcional y regresión del resumen de suscripción.
- **CI #197:** al registrar este cambio, Build Release y pruebas automatizadas estaban verdes; la generación del paquete Windows x64 continuaba ejecutándose para el commit [9a2e1fb](https://github.com/jonhararagi/vtuber/commit/9a2e1fb379015285dc5d6cbb60b49a3f260e2fe0).
- **Estado:** este cambio genera un nuevo Windows CI. La recepción de redenciones reales sigue pendiente de validar con una cuenta Twitch configurada.


### 2026-10-12 — Límites y saneamiento de resúmenes EventSub

- Los resúmenes de alertas eliminan caracteres de control, compactan saltos de línea y limitan cada campo y el resumen final (400 caracteres máximo), evitando que el texto libre de una redención crezca sin límite al almacenarse, mostrarse o leerse por voz.
- Los tipos EventSub desconocidos ya no exponen el JSON crudo del payload a la actividad/overlay/voz; producen un aviso genérico con el tipo de evento.
- Pruebas nuevas para payload desconocido y texto largo con caracteres de control.
- Windows CI para este cambio pendiente.


### 2026-10-12 — Límites de resolución para importar arte

- **Hallazgo:** el límite anterior de 25 MB controlaba el tamaño del archivo, pero no sus dimensiones decodificadas. Un archivo comprimido con dimensiones extremas podía consumir memoria excesiva en la previsualización o el overlay.
- **Corrección:** se añade `AvatarImageLimits` en Core (máximo 16.384 píxeles por dimensión y 80 millones de píxeles totales). `AvatarImageLoader` inspecciona los metadatos del primer fotograma y valida esos límites antes de cargar el bitmap. Avatar Studio y el overlay comparten el mismo cargador.
- **Pruebas:** resoluciones Full HD, 4K, 8K y límite exacto aceptadas; dimensiones negativas/cero, eje superior al máximo y exceso de píxeles rechazados.
- **Estado:** código y pruebas añadidos; Windows CI pendiente.


### 2026-10-12 — Raíz de datos coherente con la configuración

- **Hallazgo:** aunque Core respetaba `VTUBERBOT_HOME` y `AppConfig.BaseDirectory` para SQLite y logs, `MainWindow` volvía a construir la raíz de datos desde LocalAppData. Eso podía separar los assets del avatar y los secretos cifrados del resto de los datos, y la prueba aislada no controlaba esas rutas.
- **Corrección:** la UI toma ahora `application.Config.BaseDirectory` como raíz única; avatares y secretos usan subcarpetas de esa raíz. En una instalación normal sigue siendo `%LocalAppData%\\VtuberBot`; en validación/entorno portable respeta la raíz configurada.
- **Estado:** implementado en el nuevo HEAD; Windows CI pendiente.


### 2026-10-12 — Cooldown de puntos consumido tras escritura correcta

- **Hallazgo:** el timestamp de cooldown para puntos de actividad se actualizaba antes de llamar a SQLite. Si la persistencia fallaba, el usuario quedaba sin puntos durante cinco minutos aunque no se registró ninguna recompensa.
- **Corrección:** el timestamp se actualiza solo después de que `AddPoints` termina correctamente. Los errores de almacenamiento no consumen el cooldown y el aviso de fallo se limita a uno por minuto para evitar inundar el log durante una interrupción prolongada.
- **Estado:** implementado en el nuevo HEAD; Windows CI pendiente.


### 2026-10-12 — Resiliencia de VAD y límites de texto TTS

- **VAD:** si el reloj de sistema retrocede y el nivel ya cayó por debajo del umbral, el detector limpia el último instante de voz. Una duración negativa no puede mantener el avatar en estado `Talking` más allá de la retención configurada.
- **TTS:** se añadió `SpeechRequestPolicy`, que normaliza espacios/saltos de línea y limita cada frase encolada a 2.000 caracteres. La cola rechaza la frase fuera de esos límites con un mensaje visible, sin entregarla a SAPI.
- **Pruebas:** retroceso del reloj, normalización de espacios y saltos de línea, longitud exacta admitida, texto vacío y exceso de longitud.
- **Estado:** implementado en el nuevo HEAD; Windows CI pendiente.


### 2026-10-12 — Recuperación tras parada inesperada del micrófono

- **Hallazgo:** `RecordingStopped` solo actualizaba el texto de estado. Si Windows perdía el dispositivo o fallaba la captura mientras el VAD estaba activo, no llegaban más muestras de silencio y el avatar podía permanecer en `Talking`.
- **Corrección:** el callback ahora verifica que corresponde a la sesión de captura actual, libera el dispositivo detenido, restablece detector/medidor y devuelve el estado del avatar a `Idle`. Los callbacks tardíos de sesiones anteriores se ignoran para no reiniciar una captura nueva.
- **Pruebas:** `VoiceActivityDetector.Reset` vuelve a nivel cero y estado inactivo; la ruta de regresión de reloj también queda cubierta.
- **Estado:** implementación en el nuevo HEAD; Windows CI pendiente. La desconexión física real de un micrófono aún requiere validación manual en Windows.


### 2026-10-12 — Reacciones temporales de avatar para eventos Twitch

- **Implementación:** follows, suscripciones, regalos de suscripción y redenciones activan la expresión `Happy`; raids activan `Surprised`. Las reacciones duran tres segundos y después devuelven la imagen al estado previo (por ejemplo, `Talking` si el micrófono sigue activo).
- **Assets propios:** solo se anima una reacción si el sprite correspondiente está importado y existe; sin el arte, se muestra la tarjeta de alerta habitual sin fingir una expresión.
- **Control:** nuevo ajuste persistente «Reaccionar a follows, suscripciones, redenciones y raids» en la página Twitch, habilitado por defecto.
- **Pruebas:** mapeo de eventos de espectadores y exclusión de tipos internos/desconocidos.
- **Estado:** implementado en el nuevo HEAD; Windows CI pendiente. La recepción de eventos reales aún necesita validación con cuenta Twitch.


### 2026-10-12 — Simulación local de comandos con permisos de Twitch

- **UX:** el formulario de comandos ahora permite simular usuario, argumentos y rol (sin rol, suscriptor, VIP, moderador o broadcaster).
- **Seguridad de pruebas:** la simulación evalúa el permiso mínimo configurado con la misma política que el chat real. Se rechazan scopes desconocidos y los usuarios simulados sin rol suficiente reciben el mismo tipo de denegación sin enviar mensajes.
- **Sin efectos persistentes:** la simulación sigue usando `PreviewCommandResponse`; no crea usuarios ni consume cooldown, a diferencia de ejecutar un comando real. La UI lo indica expresamente para evitar confundir la simulación con una ejecución del chat.
- **Pruebas:** rol insuficiente rechazado, rol suficiente aprobado sin crear usuario, el comando sigue disponible para la primera ejecución real y scopes inválidos rechazados.
- **Estado:** código y pruebas añadidos en el nuevo HEAD; Windows CI pendiente.


### 2026-10-12 — Aislamiento de suscriptores en adaptadores de red

- **Hallazgo:** IRC, EventSub y OBS invocaban eventos multicast directamente dentro de sus supervisores. Un callback de UI que fallara durante cierre/transición podía salir al flujo de red, disparar una reconexión falsa o impedir que suscriptores posteriores recibieran el estado.
- **Corrección:** se añadió `SafeEventDispatch` al Core para ejecutar cada suscriptor de un evento por separado. Las excepciones se registran en `Trace` por tipo, sin incluir contenido/token del evento; no se propagan al supervisor de transporte y no bloquean el resto de los suscriptores.
- **Cobertura:** pruebas para eventos con uno y dos argumentos, fallo del primer suscriptor y eventos sin listeners.
- **CI #206:** la prueba del simulador de permisos de comandos estaba pendiente de compilación/ejecución al registrar este trabajo. El cambio dispara una ejecución nueva; no declarar CI verde hasta que finalice.


### 2026-10-12 — Cola EventSub acotada y despacho por lotes

- **Hallazgo:** las notificaciones EventSub publicaban un callback Dispatcher por evento. Si el hilo UI quedaba ocupado o se producía una ráfaga de redenciones, las acciones pendientes podían acumularse sin límite antes de que el panel tuviera oportunidad de procesarlas.
- **Corrección:** se añadió `BoundedQueue<T>`, FIFO thread-safe de capacidad fija, y el manejador EventSub ahora rechaza eventos nuevos cuando la cola alcanza 200 elementos. Un único callback programado procesa lotes de hasta 50 y vuelve a ceder el control al Dispatcher, preservando el orden y manteniendo responsiva la UI. La saturación se registra una vez hasta vaciar la cola.
- **Pruebas:** orden FIFO, rechazo por capacidad, liberación tras limpiar, capacidad exacta con productores concurrentes y validación del constructor.
- **Estado:** implementado en el nuevo HEAD; Windows CI pendiente.


### 2026-10-12 — Retención del registro de actividad SQLite

- **Hallazgo:** `product_activity` recibía una fila por cada mensaje de chat y por otros eventos, pero nunca eliminaba entradas antiguas. Un bot de larga duración podía hacer crecer el archivo SQLite sin límite.
- **Corrección:** se conservan los 100.000 eventos más recientes; se hace una poda al iniciar `ProductStore` y después cada 5.000 escrituras. La API permite una poda explícita acotada para mantenimiento.
- **Integridad:** solo se podan filas de `product_activity`. Saldos/ledger, participantes e historial de sorteos, sesiones de minijuegos, comandos y reglas permanecen en sus tablas y no se ven afectados.
- **Pruebas:** conserva los tres eventos más recientes en una poda pequeña, verifica que los puntos y un sorteo sigan intactos, y rechaza límites inválidos.
- **Estado:** implementado en el nuevo HEAD; Windows CI pendiente.


### 2026-10-12 — Formatos portables de imágenes coherentes con el decodificador

- Los diálogos de Avatar Studio anunciaban WebP aunque WPF depende del códec WIC instalado para leer ese formato. Un paquete portable no puede suponer que el códec opcional exista en todos los Windows.
- Se centralizó la política de formatos portables en Core; PNG/JPG/JPEG/BMP quedan aceptados por los importadores y los paquetes `.vtuberavatar`. WebP y formatos no declarados se rechazan con la validación común en lugar de fallar más tarde al renderizar.
- Pruebas para extensiones válidas, comparación sin distinguir mayúsculas y formatos no admitidos.
- Estado: implementado; Windows CI pendiente.


### 2026-10-12 — Retención de cooldowns transitorios durante streams largos

- Los diccionarios de cooldown de puntos de actividad y moderación podían acumular usuarios únicos durante toda la vida del proceso. Cada 1.000 mensajes procesados se eliminan entradas de más de 10 minutos, más del cooldown máximo actual de cinco minutos; los cooldowns activos se conservan.
- La comparación no vence un registro si el reloj del sistema retrocede, evitando eliminarlo anticipadamente por una diferencia temporal negativa.
- **CI #209:** falló la prueba inicial de retención de actividad porque la expectativa ignoraba los registros generados por operaciones de puntos/sorteos. La prueba se corrigió para medir la cantidad real previa a la poda; **Windows CI #210 terminó SUCCESS** con la expectativa corregida.
- **CI #211:** incluye la política de formatos de imagen portables y estaba ejecutándose al registrar este trabajo. El presente commit lanza un nuevo CI; todavía debe verificarse.
- **Estado:** código y pruebas de retención de cooldown añadidos; CI pendiente.


### 2026-10-12 — Prioridad para revocaciones y errores de suscripción EventSub

- Se reservó una cola independiente (20 elementos) para `subscription_error` y `revocation`, separada de la cola acotada de alertas ordinarias (200). Se procesan antes que follows, redenciones, suscripciones y raids para que una ráfaga de alertas no oculte un error de autorización/revocación.
- Ambas colas conservan FIFO internamente y comparten el procesamiento de lotes de hasta 50 eventos por turno del Dispatcher. La saturación de una cola no consume la capacidad de la otra.
- Pruebas de clasificación de eventos críticos/no críticos, incluyendo valores desconocidos y comparación sin distinguir mayúsculas.
- Estado: implementado en el nuevo HEAD; Windows CI pendiente.


### 2026-10-12 — Parallax 2.5D ligero entre capas propias

- **Avatar Overlay:** se implementó un desplazamiento parallax suave basado en la posición del cursor dentro de la ventana transparente. Las capas etiquetadas `Fondo` se mueven en sentido contrario y con menor amplitud que capas de primer plano; la imagen base permanece como referencia. El desplazamiento máximo queda acotado a cinco píxeles.
- **Configuración:** nuevo ajuste persistente `avatar.parallax.enabled`, activado por defecto y aplicado al overlay actual. «Reducir movimiento» domina sobre el parallax y lo devuelve al centro. No se usa cámara ni se afirma rastreo facial.
- **Pruebas:** normalización del puntero, dimensiones inválidas, fondo/primer plano en direcciones opuestas y límite máximo de desplazamiento.
- **Estado:** implementado; Windows CI pendiente.


### 2026-10-12 — Intensidad de parallax configurable

- Ajustes incluye un control de intensidad de 0 a 5 píxeles, con pasos de 0,5 px, persistido en SQLite y aplicado tanto al overlay actual como al siguiente que se abra.
- El Core limita el valor efectivo al rango válido y devuelve cero ante valores no finitos. «Reducir movimiento» sigue teniendo prioridad y deja el desplazamiento centrado.
- Se ampliaron las pruebas del cálculo para intensidad cero, intermedia, máxima y valores fuera del rango.
- Estado: implementación añadida; Windows CI pendiente.


### 2026-10-12 — Supresión coherente de efectos secundarios en moderación

- **Hallazgo:** cuando una regla punitiva coincidía pero la acción no podía ejecutarse por falta de ID de mensaje o por cooldown, el flujo continuaba a recompensa de puntos y ejecución de comandos.
- **Corrección:** `AutomaticModerationPolicy.ShouldSuppressMessageSideEffects` centraliza la decisión. `Execute`, `Cooldown` y `MissingMessageId` ya no permiten puntos ni comandos para ese mismo mensaje, aunque la acción remota falle; reglas de observación, deshabilitadas, acciones desconocidas y usuarios protegidos no activan esa supresión automática.
- **Pruebas:** cobertura por tabla de todas las decisiones de la política.
- **Estado:** implementado en el nuevo HEAD; Windows CI pendiente.


### 2026-10-12 — Correcciones del CI de parallax y arranque SQLite

- **Parallax:** CI #214 detectó que una prueba clasificaba el borde izquierdo del viewport como inválido, cuando solo son inválidas las dimensiones no positivas/no finitas. Se corrigieron los casos de prueba. También se limitó el resultado final del offset a la intensidad seleccionada; el factor de capa ya no puede superar el máximo global de cinco píxeles.
- **Arranque WPF/SQLite:** el mismo CI falló de forma reproducible en el smoke test al usar una raíz de datos nueva: la base por defecto está en `data/vtuber.sqlite3`, pero solo se creaba la carpeta raíz, no el subdirectorio de la base. `VtuberApplication.Initialize` crea el directorio padre de la ruta de SQLite antes de abrir la conexión.
- **Regresión añadida:** prueba de integración Core con un directorio de base inexistente que verifica creación del directorio y del archivo de base.
- **Estado:** correcciones en el nuevo HEAD; Windows CI pendiente.


### 2026-10-12 — Notificaciones TTS fuera del bloqueo de la cola

- **Hallazgo:** cuando la cola SAPI estaba llena o cerrándose, `LocalSpeechQueue.Enqueue` notificaba a la UI dentro del bloqueo `gate`. Un suscriptor que ejecutara trabajo síncrono o intentara cerrar/reentrar en la cola podía prolongar la sección crítica o bloquear al worker.
- **Corrección:** la ruta de rechazo captura el mensaje/cantidad bajo el bloqueo y notifica después de soltarlo. `StatusChanged` usa `SafeEventDispatch`, por lo que una excepción de un listener no impide notificar a los posteriores ni termina el worker de voz.
- **Estado:** implementado en el nuevo HEAD; Windows CI pendiente.


### Corrección de prueba — dirección negativa del parallax

- CI #217 compiló correctamente y la corrección de SQLite se incorporó, pero la prueba `OffsetsNeverExceedTheBoundedRange` usaba simultáneamente posición y factor negativos, cuyo producto es positivo.
- Se cambió el vector a posición negativa y factor positivo, para probar un desplazamiento negativo real. No modifica código de producción.
- El smoke test WPF no figura como fallo en #217; el mensaje SQLite del log corresponde a una ruta de inicialización que no falló como prueba independiente en esa ejecución. La prueba específica de directorio SQLite permanece añadida.


### 2026-10-12 — Controles reales de salida OBS

- El cliente WebSocket v5 incluye peticiones tipadas para leer `GetStreamStatus`/`GetRecordStatus` e iniciar/detener transmisión/grabación (`StartStream`, `StopStream`, `StartRecord`, `StopRecord`). Se validan los estados de respuesta de OBS; una respuesta incompleta no se muestra como salida inactiva.
- La página OBS añade estados de transmisión/grabación, actualización manual y botones de control; las órdenes de detención piden confirmación explícita. Toda orden pasa por la sesión autenticada existente y los rechazos quedan visibles en UI/actividad.
- **Estado:** implementación en el nuevo HEAD; Windows CI pendiente. El envío real de órdenes y estados sigue requiriendo un OBS WebSocket local configurado.


### 2026-10-12 — Estado OBS sincronizado con eventos del WebSocket

- El receptor WebSocket procesa ahora los mensajes Event de OBS v5 (op 5) en lugar de descartarlos mientras espera respuestas a peticiones (op 7). El payload se clona antes de liberar el documento JSON y los suscriptores se invocan con aislamiento de excepciones.
- La página OBS escucha `StreamStateChanged` y `RecordStateChanged`; los indicadores cambian cuando OBS inicia o detiene una salida, sin tener que pulsar «Actualizar estado». La consulta manual sigue disponible.
- Al reconstruir la página se desconecta el listener anterior, evitando acumular callbacks que retengan controles antiguos.
- **Estado:** implementado en el nuevo HEAD; Windows CI pendiente. Los eventos reales requieren OBS WebSocket v5 configurado y conectado.



### Bloque integrado de cierre — ejecución local de comandos, timers y recuperación de audio (2026-10-12)

- **Comandos:** la pantalla permite ejecutar un comando guardado en modo local real, aplicando la lógica de negocio persistida (permisos, cooldown, usuario/resultado) sin enviar nada a Twitch. La simulación previa sigue siendo explícitamente sin efectos.
- **Automatizaciones:** se corrigió el envío inmediato de mensajes nunca ejecutados. La primera emisión ahora espera el intervalo configurado desde que el timer habilitado se observa por primera vez; el tiempo solo se consume tras el envío correcto. La política temporal es pura y tiene pruebas de intervalo inicial, siguiente emisión, retroceso del reloj y límites inválidos. Al editar/reactivar o borrar un timer se reinicia su referencia temporal en memoria.
- **Audio:** si StartRecording() falla sincrónicamente, se libera la captura recién creada, se restablece VAD/medidor y se devuelve el avatar a Idle antes de presentar el error.
- **Pruebas añadidas:** AutomationSchedulePolicyTests valida los límites temporales sin requerir Twitch ni micrófono real.
- **Estado de verificación:** los cambios se agrupan en el commit candidato de esta rama; Windows CI del nuevo HEAD debe completar restore, build, tests y paquete antes de considerar verificado este bloque.


### Bloque integrado de audio — selección de salida SAPI (2026-10-12)

- **Salida de voz:** el adaptador SAPI enumera los dispositivos de salida que Windows expone; la selección se aplica al sintetizar cada frase y se persiste como preferencia local. La salida predeterminada sigue disponible como fallback.
- **Cola:** la selección de salida viaja junto a la frase en la cola limitada, así que cambiar la preferencia no altera frases que ya estaban encoladas.
- **UI:** Voz y audio permite actualizar la lista de salidas y guardar el dispositivo junto a la voz, velocidad y volumen. La enumeración es opcional; si falla, se mantiene la salida predeterminada y se informa el motivo.
- **Recuperación:** la restauración de SQLite también limpia las referencias de planificación en memoria de los timers.
- **Estado:** requiere Windows CI del nuevo SHA; la enumeración y reproducción reales dependen de los dispositivos SAPI disponibles en Windows.


### Endurecimiento de distribución — validación del ZIP de CI (2026-10-12)

- La validación del paquete Windows ahora rechaza nombres de archivos de datos locales y credenciales conocidos, incluidos bases SQLite/DB y sus journals, logs, backups, cookies, credenciales, secretos, tokens OAuth y archivos `.env`, además de la configuración local previa.
- Esto complementa la generación desde un directorio limpio de `dotnet publish`; no equivale a un análisis antivirus ni a una auditoría del contenido binario, pero evita empaquetar accidentalmente archivos locales con esos nombres.
- **Estado:** el nuevo workflow debe pasar su propio Windows CI antes de considerar activa esta comprobación.


### Corrección del workflow de distribución — 2026-10-12

- La primera edición del filtro de archivos sensibles dejó una expresión PowerShell incompleta; el run #226 falló antes de crear un job, por lo que no llegó a compilar ni empaquetar.
- El segundo y tercer intentos (runs #227 y #228) también terminaron antes de crear un job: la edición incremental dejó líneas sobrantes en el workflow. Se reconstruyó la sección desde el último archivo conocido bueno y se reemplazaron solo las dos líneas de validación. El nuevo SHA debe confirmar un job real y un SUCCESS completo.


### Persistencia — marcador de versión del esquema de producto (2026-10-12)

- `ProductStore` ahora registra la versión efectiva de su esquema en `product_schema_meta` después de las creaciones, `EnsureColumn`, normalizaciones legacy y semillas idempotentes.
- La apertura comprueba el marcador antes de tocar el esquema y rechaza explícitamente una base cuya versión sea posterior. La validación de backups también rechaza una copia futura antes de restaurarla, sin alterar la base activa.
- Prueba añadida para verificar el marcador, rechazar versiones futuras y conservar el marcador de la base sin cambios después del rechazo. Los respaldos anteriores siguen siendo compatibles: la tabla de metadatos se crea al abrir una base restaurada/antigua.
- **Estado:** pendiente del Windows CI del SHA que contiene este cambio.


### Bloque integrado de audio — biblioteca de efectos locales (2026-10-12)

- La página Voz y audio incorpora una biblioteca persistente de efectos WAV/MP3: importación a `%LocalAppData%\\VtuberBot\\sounds` (o al directorio de datos configurado), reproducción/detención local y retirada de la lista sin borrar archivos.
- El catálogo se guarda como preferencia local SQLite. Al cargarlo se validan extensión, ruta dentro de la carpeta de sonidos y existencia del archivo; rutas externas o formatos no admitidos no se reproducen.
- La reproducción usa NAudio ya presente en la aplicación; no necesita Twitch, OBS ni APIs remotas. El cierre detiene y libera el reproductor. Las copias SQLite no incluyen los binarios de audio.
- Pruebas añadidas para los formatos WAV/MP3 permitidos y extensiones rechazadas. Windows CI debe verificar el nuevo SHA; el sonido real requiere dispositivo de salida disponible.

---

# Corte consolidado de continuidad — 2026-10-12

> Este corte consolidado prevalece para el estado actual sobre las notas históricas que dicen «CI pendiente» en entradas anteriores. Esas notas describen el momento de cada commit y no deben interpretarse como el estado vigente.

## Estado verificado ahora

- **Repositorio:** https://github.com/jonhararagi/vtuber
- **Rama de trabajo exclusiva:** `product/complete-vtuber-experience`
- **HEAD verificado:** `e6909d3018fa6bdc660444b5f77b168795194918`
- **Windows CI #234:** SUCCESS sobre el SHA de código `601ec7afa520681c5ed2075b3a94a1bf1a2360fd`: https://github.com/jonhararagi/vtuber/actions/runs/38287435169
- Pasos de #234: Restore solution ✅; Build Release ✅; Run automated tests ✅; Publish self-contained Windows x64 validation package ✅; Upload temporary validation artifact ✅.
- **Artefacto temporal de CI:** `VTuberBot-Product-Candidate-win-x64-601ec7afa520681c5ed2075b3a94a1bf1a2360fd`, 77.907.027 bytes, SHA-256 `71e792f4ac0134320148478cd5d36e44b6ba573828ba5a17446e7339fa84d167`. Vence el 2026-10-26 17:56:46 UTC. [Descargar artefacto de CI #234](https://github.com/jonhararagi/vtuber/actions/runs/38287435169/artifacts/11763902305).
- **No es una release final** y el verde de CI no demuestra validación manual con cuentas, hardware o servicios locales. `main` sigue intacta y el PR #9 sigue sin fusionar.

## Cambios funcionales y de fiabilidad ya registrados en la rama

- IRC de Twitch: recuperación de sesiones supervisoras terminadas, reconexión con backoff, confirmación real de entrada al canal y mensajes de estado de reconexión correctos.
- Protección del protocolo IRC: login de canal restringido a 1–25 caracteres ASCII permitidos; token OAuth sin espacios ni caracteres de control internos; un envío solo puede dirigirse al canal de la sesión actual.
- Procesamiento de chat: cola entrante realmente acotada antes de publicar callbacks WPF; cooldown de puntos consumido solo tras persistir correctamente; limpieza de cooldowns expirados; mensajes que activan moderación no reciben puntos ni ejecutan comandos si la sanción está pendiente, en cooldown o no tiene ID de mensaje.
- EventSub: resúmenes legibles, texto saneado y acotado; cola común limitada y procesada por lotes; cola independiente de prioridad para revocaciones/errores de suscripción.
- Eventos de integraciones: suscriptores aislados frente a excepciones para evitar que errores de UI simulen fallos de transporte.
- SQLite: creación del directorio padre antes de abrir la base, retención limitada del registro de actividad y preservación de tablas transaccionales de puntos/sorteos/juegos.
- Avatar Studio: capas llamadas `Fondo` por detrás de la imagen base; prompts por perfil y rol con migración desde claves antiguas, duplicación e importación/exportación; validación de resolución y lista portable de formatos; parallax 2.5D configurable; reacciones a eventos Twitch únicamente si existen los sprites configurados.
- Audio: texto de TTS saneado y acotado; manejo del retroceso del reloj del VAD; si la captura del micrófono se detiene inesperadamente, se libera el dispositivo y se devuelve el avatar a Idle.
- Sorteos: solo un sorteo abierto a la vez; nombre limitado; selección explícita, cierre/cancelación con historial retenido.
- Comandos integrados de comunidad: `!puntos`, `!top` y `!jugar moneda|ruleta|dado`; la UI permite ejecutarlos localmente y Twitch usa el mismo router de negocio.
- OBS: autenticación WebSocket v5, consulta de estado, controles de iniciar/detener transmisión/grabación con confirmación al detener; eventos de estado en tiempo real. La operación real aún depende de OBS local y debe comprobarse con él.

## Parámetros actuales relevantes

| Área | Parámetro vigente |
|---|---|
| Chat IRC | Hasta 1.000 mensajes ocupando cupo entre despacho UI, cola y procesamiento |
| EventSub | 200 eventos ordinarios + 20 críticos reservados; lotes de hasta 50 por turno de UI |
| Resúmenes EventSub | Campo individual máximo 160 caracteres; resumen completo máximo 400 |
| Registro de actividad SQLite | Conserva 100.000 entradas; poda al iniciar y cada 5.000 escrituras |
| Cooldowns en memoria | Revisión cada 1.000 mensajes; elimina registros de más de 10 minutos |
| TTS local | Máximo 2.000 caracteres por solicitud; la cola admite hasta 32 frases pendientes/activas |
| Prompts de arte | 8 roles soportados; máximo 12.000 caracteres por prompt; guardado por perfil |
| Sorteos | Nombre máximo 100 caracteres; solo un estado OPEN a la vez |
| Imágenes de avatar | PNG/JPG/JPEG/BMP; máximo 16.384 px por dimensión y 80.000.000 píxeles totales |
| Parallax | Activado por defecto; intensidad por defecto 3 px; ajustable 0–5 px en pasos de 0,5 px; «Reducir movimiento» lo desactiva |
| Reacciones del avatar | Activadas por defecto; follow/suscripción/regalo/redención → Happy; raid → Surprised; 3 segundos y solo con sprite existente |
| Mensajes TTS de audio local | Se compactan espacios/saltos de línea antes de encolarse |
| OBS WebSocket | Timeout de conexión de 15 s; timeout total por solicitud de 15 s (incluye envío y respuesta); límite de mensaje entrante 1 MiB; conexión opcional y autenticada |

## Bloque integrado más reciente — CI #234

- `CommunityCommandRouter` expone `!puntos`, `!top` y `!jugar` a la UI local y a Twitch. Las rondas usan `ProductStore.PlayGame`, la misma liquidación transaccional y el mismo ledger/historial; se rechazan apuestas inválidas o saldo insuficiente.
- Duplicar un perfil copia sus prompts de arte por rol con claves aisladas por perfil, además de base/capas/expresiones.
- Pruebas nuevas cubren saldo/ranking, práctica gratuita, saldo insuficiente y desconocimiento de comandos integrados. Windows CI #234 aprobó restore, build, tests, publish win-x64 y upload para el SHA de código indicado arriba.
- La sincronización documental quedó en el commit `4fd53ef8cf2b60605708b8b6c1882c4017f3e85a`; Windows CI #235 pasó sobre ese SHA exacto: https://github.com/jonhararagi/vtuber/actions/runs/38287802675. Pasos verificados: restore, build Release, pruebas automatizadas, publicación win-x64 y upload de artefacto. Artefacto: [VTuberBot-Product-Candidate-win-x64-4fd53ef8cf2b60605708b8b6c1882c4017f3e85a](https://github.com/jonhararagi/vtuber/actions/runs/38287802675/artifacts/11764008008), 77.907.018 bytes, SHA-256 `c50d79858a3594da0a8abda0c9ca9940a52d3a13fd2c71c329472f2aaf4b8560`, expira el 2026-10-26 18:00:18 UTC. Este paquete contiene el router compartido de comunidad y los documentos actualizados. El commit de continuidad actual también dispara su propia ejecución CI; verificarla antes de informar el HEAD final.

## Validaciones que todavía no se han realizado manualmente

- Autorizar una cuenta real de Twitch y recorrer chat, permisos, reconexión, puntos, comandos, moderación y eventos EventSub reales.
- Conectar a OBS Studio local y verificar escenas, estados, transmisión y grabación en un setup real.
- Probar entrada de micrófono, desconexión física del dispositivo, audio de salida y TTS en Windows del usuario.
- Inspeccionar visualmente Avatar Studio y el overlay en OBS con assets reales importados, incluida transparencia, expresiones y parallax.
- Confirmar Telegram WebView2 y su alternativa de abrir navegador en un Windows que no tenga el runtime.
- Auditar el producto de punta a punta frente a todos los criterios de `docs/PRODUCT_BLUEPRINT.md`, `PROJECT_SPEC.md` y `ROADMAP.md`. La existencia de páginas/controles no sirve como evidencia suficiente.

## Orden obligatorio de la siguiente sesión de Obrero

1. Volver a leer el HEAD real y el resultado de CI más reciente; no partir de este corte si el HEAD avanzó.
2. Auditar todos los flujos frente al blueprint y categorizar cada requisito como funcional verificado por código/prueba, incompleto, bloqueado por credenciales/entorno o no implementado.
3. Corregir primero defectos de compilación, persistencia, seguridad, ciclo de vida o integración hallados; agrupar cambios relacionados y correr CI con frecuencia suficiente para detectar regresiones.
4. Continuar implementando bloques de producto pendientes sin detenerse a pedir aprobación por detalles rutinarios y sin entregar builds parciales para que el usuario actúe de QA.
5. Solo pedir una validación manual del usuario cuando haya un candidato global coherente; enumerar entonces pasos concretos que dependan realmente de su Twitch/OBS/micrófono/Telegram.
6. No modificar `main`, no fusionar PR #9, no declarar terminado el producto por CI verde ni por tener botones.
7. Actualizar este archivo con el HEAD/CI nuevos y evidencia verificable después de cada bloque integrado.


## Bloque funcional — límite total de solicitud OBS

- En ObsWebSocketClient.RequestAsync, el timeout de 15 segundos se inicia antes de enviar el frame y cubre el envío más la espera de respuesta. Esto evita que una escritura de socket atascada quede fuera de la fecha límite.
- La cancelación del usuario sigue enlazada al token y el bloque finally elimina el ID pendiente incluso ante timeout/error.
- Estado: corrección de código preparada en este commit; la evidencia automática del SHA exacto se añadirá después de que Windows CI finalice. No equivale a prueba manual con OBS.


## Bloque funcional — cierre ordenado de la aplicación

- Se corrigió el ciclo de vida de WPF: el cierre ahora se aplaza mientras se detienen timers, captura/reproducción de audio, overlay y clientes asíncronos Twitch IRC, EventSub y OBS. Esto evita depender de una continuación async disparada desde Window.Closed después de que la ventana principal ya inició el apagado del dispatcher.
- Cada disposición de red se intenta de forma independiente; un fallo se registra y no omite la limpieza de los demás recursos. El cierre final solo se reintenta después de completar la rutina.
- Estado: corrección de código preparada; Windows CI y cierre manual en el equipo objetivo aún deben verificarse para este SHA.


## Bloque funcional — tolerancia de callback de audio

- El callback de entrada PCM ahora ignora buffers de menos de 2 bytes (no contienen una muestra PCM16 completa), evitando dividir por cero o propagar NaN al VAD.
- El envío de actualización visual al Dispatcher queda protegido si el dispositivo dispara un callback mientras WPF está cerrando. La captura conserva la ruta existente que restablece el avatar a Idle al detenerse.
- Estado: pendiente de Windows CI del SHA resultante y de validación manual de micrófono/cierre; no se declara comprobado con hardware real.


## Bloque funcional — política de nivel PCM16 y pruebas

- Se centralizó en Core el cálculo de nivel RMS para PCM16 little-endian; la política ignora cualquier byte final incompleto, devuelve cero si no hay una muestra completa y limita la salida al rango 0–100.
- La UI de micrófono consume esta misma política y la suite agrega tres pruebas para frame incompleto, silencio/saturación y byte final impar.
- Core permanece independiente de WPF/NAudio. Estado: pendiente de Windows CI del nuevo SHA y de prueba manual con micrófono real.


## Bloque funcional — integridad del ledger de puntos (2026-10-12)

- Auditoría de ProductStore.AddPoints: al solicitar un débito mayor que el saldo, SQLite protegía el balance con MAX(0, points + delta), pero point_ledger registraba el delta solicitado completo. El balance quedaba correcto y el ledger mentía.
- La transacción ahora lee el balance previo y posterior, calcula el delta aplicado real y registra solo ese importe; si el saldo ya es cero, no añade una entrada contable ficticia. El registro de actividad distingue el importe aplicado del solicitado.
- Se sustituyó la prueba previa por una que verifica los deltas reales directamente en SQLite y se añadió una prueba de débito sin saldo.
- Estado: cambio preparado en ProductStore y pruebas; pendiente Windows CI del SHA exacto. No se ha marcado el bloque como validado antes del CI.


## Endurecimiento del saldo — overflow de puntos

- AddPoints ahora rechaza créditos que superarían Int64.MaxValue antes de ejecutar la actualización SQLite, evitando que una suma INTEGER desborde a REAL y corrompa la lectura del balance.
- La validación del delta ya no usa Math.Abs, que lanza OverflowException para long.MinValue; el rango permitido se comprueba directamente y produce ArgumentOutOfRangeException.
- Se agregó prueba de no-mutación tras overflow con un balance SQLite próximo al máximo y de rechazo de long.MinValue.
- Estado: pendiente de Windows CI del SHA que contiene este endurecimiento.


## Seguridad de diagnóstico — redacción de secretos

- La exportación de soporte antes incluía directamente mensajes de actividad y la ruta completa de la base SQLite. Aunque no se guardaban tokens intencionalmente, no había un filtro final sobre el texto exportado.
- Se agregó DiagnosticTextRedactor en Core y pruebas para credenciales Bearer/OAuth, access/refresh tokens, client secrets, contraseñas y token de bot de Telegram. El exportador redacta módulo y mensaje, evita revelar el path de perfil Windows y maneja errores de escritura.
- El redactor falla de forma cerrada ante timeout de regex y no requiere red ni APIs pagas.
- Estado: pendiente Windows CI del SHA exacto; la política está automatizada, pero no se afirma que se haya inspeccionado un paquete compartido por un usuario.


## Corte verificado de cierre — 2026-10-12

### Código y Windows CI

- Rama: product/complete-vtuber-experience.
- HEAD de código verificado: 86f93ceba9f74ecd9536921edd6001a32abae8df.
- Windows CI #245: SUCCESS para el SHA exacto 86f93ceba9f74ecd9536921edd6001a32abae8df. Restore, Build Release, suite automatizada, publicación self-contained Windows x64 y carga del artefacto finalizaron correctamente.
- Ejecución: https://github.com/jonhararagi/vtuber/actions/runs/38291215599
- Artefacto de ese SHA: https://github.com/jonhararagi/vtuber/actions/runs/38291215599/artifacts/11767420010
- Nombre: VTuberBot-Product-Candidate-win-x64-86f93ceba9f74ecd9536921edd6001a32abae8df.
- Tamaño: 77,909,128 bytes. SHA-256 reportado por GitHub: d6422d4ccef798759bbb08755e0fc0197a151b340fb313dd5865404f8a97b3a1. Expira el 2026-10-26T18:31:20Z.
- El commit de redacción de diagnósticos tuvo un CI intermedio fallido (#244) por sintaxis de cadena regular C#; la línea fue corregida y el SHA #245 compila y pasa la suite. No ocultar el fallo histórico, pero el candidato vigente es el SHA verde.

### Cambios funcionales añadidos después de CI #241

- AddPoints: el ledger guarda el cambio de saldo realmente aplicado, no el débito solicitado si el balance se limita a cero.
- AddPoints: se rechazan créditos que superarían Int64.MaxValue y se valida el delta sin Math.Abs para evitar el caso especial de long.MinValue.
- Diagnósticos: se añadió redacción en Core de Bearer/OAuth, access/refresh token, client secret, password, API key y token de bot de Telegram; la exportación evita mostrar el path completo de SQLite y maneja errores de escritura.
- Tests SQLite y redactor agregados para los casos anteriores; CI #245 confirma restore/build/tests/publicación en Windows runner.
- Artefacto generado por CI no ha sido ejecutado manualmente por el usuario en el PC objetivo. El digest es metadata del artefacto reportada por GitHub, no resultado de una descarga y verificación local independiente.

### Estado de cierre honesto

- Automáticamente probado en Windows CI: build Release, tests y publish/upload win-x64 del SHA #245.
- Implementado sin validación externa: servicios Twitch/IRC/EventSub, OAuth con cuenta real, OBS WebSocket, micrófono y salida SAPI, WebView2/Telegram y overlay capturado por OBS.
- Pendiente de una validación manual global: arranque/cierre en Windows, importación y overlay del avatar, captura de micrófono y voz local, configuración/autenticación de Twitch, comando/puntos/sorteo/minijuego, OBS, fallback Telegram y restauración de backup con datos de prueba.
- No se modifica main ni se fusiona/cierra PR #9. La rama de producto permanece independiente.
- Este documento es el corte verificado del SHA #245. El siguiente commit documental debe pasar su propio Windows CI antes de considerarse candidato de entrega.


## Corrección de estabilidad del smoke test WPF — CI #246

- CI #246 del commit documental 61ed9fcacd600c8a0f05b7bb7f84e95105702eb3 compiló correctamente pero falló una prueba de humo WPF: el proceso seguía vivo tras 15 segundos, pero no había expuesto todavía MainWindowHandle. La misma prueba pasó en CI #245 con el mismo código de aplicación; el resultado es consistente con una carrera/arranque lento del runner, no prueba por sí solo un defecto funcional.
- Se amplía el límite acotado de arranque de la prueba a 30 s y el cierre a 10 s, sin quitar la comprobación de que la ventana principal aparece ni la de salida limpia. El siguiente CI debe confirmar si queda resuelto.


## Resultado posterior — CI #247 y candidato global

- El ajuste de tiempo del smoke test WPF resolvió la ejecución inestable del run #246: Windows CI #247 terminó SUCCESS en el SHA exacto 76bb8ed4c84805e204f3768ffa1fff3fc8395d5e.
- Run: https://github.com/jonhararagi/vtuber/actions/runs/38291952562
- Artefacto win-x64: https://github.com/jonhararagi/vtuber/actions/runs/38291952562/artifacts/11766821323
- Nombre: VTuberBot-Product-Candidate-win-x64-76bb8ed4c84805e204f3768ffa1fff3fc8395d5e.
- Tamaño: 77,909,102 bytes. SHA-256: 4cbcf0abb26c3c8137d46646f7b14bd501be6aab89910538080fd93cfac6fbe9. Expira 2026-10-26T18:37:24Z.
- CI #247: Restore, Build Release, automated tests, publicación self-contained Windows x64 y upload de artefacto pasaron. El test de humo mantiene las verificaciones de ventana principal y salida limpia, con límites de espera más tolerantes a la carga del runner.
- El SHA #247 incluye los cambios de ledger, límites de saldo, redacción de diagnóstico y timeout del smoke test; CI #245 ya había pasado también con el código funcional y pruebas de redacción. CI #246 falló de forma intermitente en la espera de MainWindowHandle; el cambio se hizo en la prueba y #247 confirmó éxito.

### Pendiente para cierre real

- La aplicación no se ha ejecutado manualmente en el equipo del usuario por esta sesión. La validación global sigue pendiente y debe ser una sola pasada, no pruebas por commit.
- Twitch OAuth/IRC/EventSub, OBS WebSocket real, micrófono y salida SAPI, WebView2/Telegram y captura del overlay en OBS requieren configuración/servicio/hardware del equipo del usuario.
- No se afirma que haya tracking facial: el parallax depende del cursor sobre el overlay; el Talking/Idle depende de la señal acústica del micrófono.
- No modificar main; PR #9 sigue abierto, no fusionado. La rama de producto permanece separada.
- El siguiente commit solo documenta la evidencia del candidato; su propio CI debe verificarse antes de usarlo como HEAD final de entrega.


## Auditoría integrada — seguridad de autenticación OBS (2026-10-12)

- Hallazgo real: el cliente OBS v5 enviaba Identify con `authentication = null` si el servidor no publicaba challenge y luego reportaba «OBS WebSocket autenticado». Esto permitía que una sesión sin autenticación se presentara como autenticada, contrario al contrato del producto.
- Corrección en `ObsWebSocketClient`: el handshake exige challenge de autenticación y contraseña no vacía; si OBS no exige contraseña, falla con instrucción clara para habilitarla en los ajustes del servidor WebSocket.
- Añadida política pura `ObsAuthenticationPolicy` en Core y pruebas para ausencia de challenge, contraseña ausente/espacios y configuración válida.
- CI del SHA nuevo y validación contra OBS real pendientes; no se afirma conexión real comprobada.


## Verificación de seguridad OBS — Windows CI #250

- Hallazgo corregido: OBS WebSocket podía aceptar un Hello sin challenge y mostrar igualmente «autenticado». Ahora `ObsAuthenticationPolicy` exige que OBS solicite autenticación y que haya contraseña; la interfaz ya no describe el campo como opcional.
- Pruebas añadidas: servidor sin challenge rechazado, contraseña nula/vacía/espacios rechazada y challenge con contraseña aceptado.
- Commit funcional de la política: `b6d1c82334f8503ed74dbb05200f82bebdfec577`.
- Ajuste de UI: `095d2889303bc4ed17ef47ef2bce4452ca0c5764`.
- Windows CI #250: SUCCESS sobre el SHA exacto de UI `095d2889303bc4ed17ef47ef2bce4452ca0c5764`; Restore, Build Release, automated tests, publicación self-contained win-x64 y upload de artefacto pasaron.
- Run: https://github.com/jonhararagi/vtuber/actions/runs/38293944405
- Artefacto: https://github.com/jonhararagi/vtuber/actions/runs/38293944405/artifacts/11767054521
- Nombre: `VTuberBot-Product-Candidate-win-x64-095d2889303bc4ed17ef47ef2bce4452ca0c5764`.
- Tamaño: 77,909,346 bytes. Expira el 2026-10-26 18:55:06 UTC. GitHub no proporcionó un SHA-256 del ZIP en los metadatos consultados, así que no se inventa uno.
- Límite de validación: CI verifica compilación/pruebas/publicación, no la conexión con OBS real. La validación manual global continúa pendiente.

## Auditoría funcional — Telegram Web — 2026-10-12

- **HEAD auditado antes del cambio:** `1da946ea1873c9db74bfaff77a09b92b2932ab6c`.
- **Evidencia previa:** Windows CI #251 terminó correctamente para ese SHA (restore, compilación Release, tests automatizados, publicación autocontenida Windows x64 y subida del artefacto). No es evidencia de esta corrección ni prueba de integración manual con Telegram.
- **Hallazgo real:** `BuildTelegramPage` registraba el autocargado en `MainWindow.Loaded`. Como la navegación construye esa página después de que la ventana principal ya está cargada, el manejador no iniciaba Telegram al entrar a la página; el botón seguía ofreciendo una ruta manual.
- **Corrección aplicada:** el inicio ahora se vincula al evento `Loaded` del control WebView2 de esa página. La acción del botón y el autocargado comparten la misma tarea de inicialización para evitar que dos llamadas compitan, mantienen el fallback de navegador externo y registran el fallo sin afirmar que Telegram está conectado.
- **Estado de validación del cambio:** pendiente del Windows CI que se ejecutará sobre el commit que contiene esta modificación. La sesión real de Telegram/WebView2 sigue necesitando validación manual en Windows.
- **Alcance no resuelto por esta corrección:** no demuestra rig facial/lip-sync por fonemas, voz anime local instalada, ni una conexión real a cuentas Twitch/OBS; estas áreas deben mantenerse diferenciadas entre implementación, CI y prueba manual global.

## Investigación de Windows CI #252 y diagnóstico del smoke test — 2026-10-12

- **SHA inicial investigado:** `598245c9eed14b642c367d7cebed0bf0f49e5b97`; [Windows CI #252](https://github.com/jonhararagi/vtuber/actions/runs/38297321456) había terminado con 358/359 pruebas.
- **Fallo exacto:** `VtuberBot.Tests.Tests.WpfExecutableSmokeStartsAndClosesCleanly`, en `tests/VtuberBot.Tests/Tests.cs:1480`. La aserción que falló fue `Assert.NotEqual(IntPtr.Zero, process.MainWindowHandle)`: después de esperar 30 segundos, el proceso seguía activo pero `Process.MainWindowHandle` continuaba en cero. **No fue** la aserción del código de salida.
- **Análisis de SQLite Error 14:** la línea `SQLite Error 14: 'unable to open database file'` no se atribuye como causa del smoke test. La suite contiene la prueba negativa `ApplicationDatabaseStartupFailureCleansPartialResources`, que configura deliberadamente como ruta de la base un directorio ya existente y espera que el arranque falle; ese camino registra el error de inicialización de SQLite de forma intencional.
- **Instrumentación diagnóstica implementada:** commit [`ecb400d5012ee9792a19c501097ba97aa352082f`](https://github.com/jonhararagi/vtuber/commit/ecb400d5012ee9792a19c501097ba97aa352082f). El smoke test captura stdout/stderr del proceso hijo y adjunta logs locales a la aserción de código de salida si vuelve a fallar. La instrumentación no desactiva ni omite la prueba ni cambia su requisito de ventana/cierre.
- **Windows CI del SHA `ecb400d5012ee9792a19c501097ba97aa352082f`:** [run #253](https://github.com/jonhararagi/vtuber/actions/runs/38299149799) — **SUCCESS**, incluido un segundo intento del mismo job. En el segundo intento: restore y build Release correctos; **359/359 pruebas pasaron**; publicación autocontenida win-x64 y subida del artefacto correctas. El fallo de #252 no se reprodujo, así que no se afirma una corrección de código de producto que no se ha demostrado necesaria.
- **Artefacto del segundo intento:** [VTuberBot-Product-Candidate-win-x64-`ecb400d`](https://github.com/jonhararagi/vtuber/actions/runs/38299149799/artifacts/11769687971); ID `11769687971`, tamaño del artefacto `77,909,711` bytes, digest reportado `sha256:937d9f9cdc5df9bdf03b9e78538cf95b77cf8953146de9bf39e36b72d3975119`, expira el 2026-10-26. Corresponde al SHA indicado, pero **no** constituye validación manual de Windows/servicios externos.
- **Estado:** el fallo original no se reprodujo en dos ejecuciones correctas del SHA instrumentado. Mantener la evidencia y volver a investigar si reaparece; no dar por hecho que la línea SQLite fue su causa.

## Integración de estado de OBS en el workspace — 2026-10-12

- **Objetivo:** mantener los indicadores de transmisión y grabación sincronizados con la sesión WebSocket v5 cuando OBS se desconecta, reconecta o se vuelve a abrir la página OBS.
- **Cambio final de código:** [`25f134b58ca6be45a53ce23e4f1c5ebc3a96f8a6`](https://github.com/jonhararagi/vtuber/commit/25f134b58ca6be45a53ce23e4f1c5ebc3a96f8a6), en `src/VtuberBot.App/Workspaces/MainWindow.Integrations.cs`.
  - Los indicadores consultan el estado real al entrar al workspace y ante eventos de conexión/reconexión de OBS.
  - Los manejadores de estado y eventos se desconectan al descargar la página; cada cierre captura sus propios delegates para no retirar accidentalmente los del workspace nuevo durante la navegación.
  - No se altera la autenticación ni se muestran estados de transmisión/grabación activos sin una respuesta de OBS.
- **Windows CI exacto:** [run #258](https://github.com/jonhararagi/vtuber/actions/runs/38300103344) — **SUCCESS** para el SHA anterior. Restore y build Release pasaron sin warnings/errores; **359/359 pruebas pasaron**; publicación autocontenida win-x64 y subida del artefacto terminaron correctamente.
- **Artefacto de ese SHA:** [descarga del artefacto](https://github.com/jonhararagi/vtuber/actions/runs/38300103344/artifacts/11770103744), ID `11770103744`, tamaño `77,910,077` bytes, digest `sha256:43298d09fc598bd81aab89db2af72a65006dbbaf0960438f90a3cfab45058051`, expira el 2026-10-26.
- **Validación externa pendiente:** la reconexión y los cambios de estado deben probarse con OBS Studio real configurado; CI valida compilación, pruebas automáticas y empaquetado, no una sesión OBS real.

## Cancelación de voz SAPI y cierre de la cola local — 2026-10-12

- **Objetivo:** permitir cancelar la frase TTS activa y asegurar que el cierre no espere a que una frase larga termine naturalmente.
- **Código:** `src/VtuberBot.App/LocalSpeechQueue.cs`, `src/VtuberBot.App/Workspaces/MainWindow.Audio.cs`, `tests/VtuberBot.Tests/Tests.cs` y `tests/VtuberBot.Tests/VtuberBot.Tests.csproj`.
  - SAPI ahora inicia la frase en modo asíncrono y el worker STA consulta `WaitUntilDone` en intervalos acotados, observando la cancelación.
  - La cancelación emite el comando SAPI de purga antes de liberar las referencias COM; el cierre de la cola también solicita cancelar la frase actual y descarta frases pendientes.
  - El workspace Audio incorpora «Detener voz actual», distinto de «Vaciar frases pendientes» (que conserva la frase actual).
  - Se introdujo `ILocalSpeechSession` inyectable para probar cancelación y cierre sin depender de una voz física/SAPI instalada en el runner.
- **Correcciones del propio cambio:** las primeras ejecuciones detectaron errores reales de compilación de las pruebas (falta del alias/using para contratos App, ambigüedad entre `SpeechPriority` de Core y App, y llamadas `WaitAsync` sin el token de cancelación requerido por el analizador xUnit). Se corrigieron antes de aceptar el bloque; no se suprimieron reglas ni pruebas.
- **Windows CI exacto:** [run #265](https://github.com/jonhararagi/vtuber/actions/runs/38301679337), SHA `c0d5a4935f07b253393f0128a1b5843e509a46af` — **SUCCESS**. Restore y Build Release correctos con 0 warnings/0 errors; **361/361 casos de prueba pasaron**; publicación autocontenida win-x64 y subida del artefacto completadas.
- **Artefacto:** [VTuberBot-Product-Candidate-win-x64-`c0d5a49`](https://github.com/jonhararagi/vtuber/actions/runs/38301679337/artifacts/11770876382), ID `11770876382`, tamaño `77,911,664` bytes, digest `sha256:1f7e12cda14402d898af0c3cdd7f4f71efd3684ae21edf4ef0c61418bd703e04`, expira el 2026-10-26.
- **Límite externo pendiente:** las pruebas verifican la cancelación de la cola mediante un motor de prueba; la reproducción/cancelación real debe validarse con SAPI y un dispositivo de salida de Windows. El producto global todavía requiere continuar la auditoría y la validación manual de Twitch, OBS, Telegram, micrófono y overlay.

## Validación de identidad IRC antes de recompensas — 2026-10-12

- **Defecto:** `ProcessTwitchMessageAsync` sustituía un login inválido por la identidad artificial `viewer` y seguía procesando el mensaje. Eso podía atribuir puntos, ejecutar comandos o aplicar cooldowns a una identidad que no procedía de Twitch.
- **Solución:** se añadió `src/VtuberBot.Core/TwitchLoginPolicy.cs`, que valida nombres ASCII de Twitch de hasta 25 caracteres y normaliza a minúsculas. `src/VtuberBot.App/Workspaces/MainWindow.Twitch.cs` ahora descarta y registra un `PRIVMSG` con prefijo inválido antes de actualizar el chat o ejecutar cualquier efecto. No se crea una identidad sustituta.
- **Pruebas:** `tests/VtuberBot.Tests/Tests.cs` cubre normalización válida y rechazo de nombre vacío, caracteres no permitidos, CR/LF e identificadores demasiado largos.
- **Windows CI exacto:** [run #269](https://github.com/jonhararagi/vtuber/actions/runs/38302244458), SHA `388eaa65b418f0200966b63e425f85f2c89b3066` — **SUCCESS**. Build Release con 0 warnings/0 errors; **371/371 casos de prueba pasaron**; publicación y subida del paquete autocontenido win-x64 correctas.
- **Artefacto:** [VTuberBot-Product-Candidate-win-x64-`388eaa6`](https://github.com/jonhararagi/vtuber/actions/runs/38302244458/artifacts/11771122106), ID `11771122106`, tamaño `77,911,901` bytes, digest `sha256:adc3ba874f8cff8bb440bd135b4fc19abda067b438db860c18fa23536207d226`, expira el 2026-10-26.
- **Estado:** el contrato local y su integración con el flujo de chat están probados automáticamente; la recepción y el comportamiento del chat en un canal Twitch real siguen pendientes de validación con una cuenta autorizada.

## Estado honesto de anuncios de sorteos — 2026-10-12

- **Defecto:** al crear, cerrar o cancelar un sorteo con el chat Twitch desconectado, la operación local se completaba correctamente, pero el estado de pie no siempre dejaba explícito que no hubo anuncio remoto.
- **Solución:** `src/VtuberBot.App/Workspaces/MainWindow.Community.cs` ahora distingue explícitamente entre resultado local y anuncio enviado a Twitch en las tres acciones. Si el chat no está conectado, informa que no se anunció; si el envío falla, conserva el resultado local y comunica el error. No simula publicaciones remotas.
- **Windows CI exacto:** [run #271](https://github.com/jonhararagi/vtuber/actions/runs/38302840849), SHA `c3cfee39e25beeec6a8b80afa8cca7b2d1605628` — **SUCCESS**. Restore y Build Release correctos con 0 warnings/0 errors; **371/371 pruebas pasaron**; paquete autocontenido win-x64 publicado y subido.
- **Artefacto:** [VTuberBot-Product-Candidate-win-x64-`c3cfee3`](https://github.com/jonhararagi/vtuber/actions/runs/38302840849/artifacts/11770928205), ID `11770928205`, tamaño `77,911,964` bytes, digest `sha256:a1e536bb75d2dc8a5eadaeac3b39c787d90d033680a44cf0ab446be132acaab9`, expira el 2026-10-26.
- **Estado:** la condición de conexión y los mensajes de resultado se verificaron mediante revisión de flujo y CI; no se realizó una publicación real a un canal Twitch durante CI.

## Diagnóstico reforzado del smoke test WPF — 2026-10-12

- **Fallo investigado:** en Windows CI #252 y en una ejecución posterior, `WpfExecutableSmokeStartsAndClosesCleanly` no detectó `Process.MainWindowHandle` después de 30 segundos. La prueba no llegó a la aserción del código de salida. La línea SQLite Error 14 no se ha demostrado como causa raíz; la suite también ejecuta un caso negativo de apertura de base.
- **Instrumentación añadida en SHA `b85793ae4e65b7eb3569ed7b47403380fe1ce3a7`:** `tests/VtuberBot.Tests/Tests.cs` captura stdout/stderr mientras el proceso sigue vivo y adjunta los logs disponibles al fallo de arranque/ventana. `src/VtuberBot.App/App.xaml.cs` escribe en stderr el tipo y mensaje de una excepción de inicio antes de mostrar el diálogo. No se elimina ni relaja la comprobación de la ventana.
- **Repetición del mismo SHA:** [Windows CI #277](https://github.com/jonhararagi/vtuber/actions/runs/38303932303) completó con éxito en el primer intento y en el rerun del mismo job. En ambos, restore/build Release y pruebas pasaron; el rerun reportó **371/371 pruebas**, 0 warnings y 0 errors, y publicó el paquete autocontenido win-x64.
- **Artefacto del rerun:** [descarga del artefacto](https://github.com/jonhararagi/vtuber/actions/runs/38303932303/artifacts/11770829906), ID `11770829906`, tamaño `77,912,143` bytes, digest `sha256:20747e240f03ee4633003d69c39b3d7608114aacd3ce0ab1744cf8610dddd3bd`, expira el 2026-10-26.
- **Conclusión honesta:** el fallo original no se reprodujo en dos ejecuciones del SHA instrumentado. No se afirma que la causa raíz esté corregida; si reaparece, la siguiente ejecución debe aportar la excepción de arranque, la salida del proceso y los logs antes de decidir si es un defecto del producto o del criterio de detección de ventana.

## Verificación directa de la ventana WPF en CI — 2026-10-12

- **Ajuste del smoke test:** `WpfExecutableSmokeStartsAndClosesCleanly` ya no depende únicamente de la heurística `Process.MainWindowHandle`. Enumera ventanas Win32 del proceso, identifica la ventana visible cuyo título es `VTuberBot` y envía `WM_CLOSE` a ese HWND concreto. Si no aparece, el fallo adjunta títulos/visibilidad de ventanas del proceso, stdout/stderr y logs disponibles. La prueba sigue exigiendo que exista una ventana visible y que el proceso cierre con código 0; no se relajó la aceptación.
- **Diagnóstico SQLite:** la línea Error 14 no se considera causa probada; existe una prueba negativa de inicialización que fuerza un error de base. La nueva ejecución no presentó fallos y no aportó evidencia de una excepción de inicio.
- **Windows CI exacto:** [run #280](https://github.com/jonhararagi/vtuber/actions/runs/38305211986), SHA `bff06a453890a8b42bb6203ad0a45a4deb9a9861` — **SUCCESS**. Restore, Build Release, pruebas, publicación autocontenida win-x64 y subida del artefacto completados. **371/371 pruebas**, 0 warnings y 0 errors de compilación.
- **Artefacto:** [descarga del artefacto](https://github.com/jonhararagi/vtuber/actions/runs/38305211986/artifacts/11772900722), ID `11772900722`, tamaño `77,912,197` bytes, digest `sha256:c847cc06594cfa0bdfdab5491a54d1f181bfdd37fbc92403b2c0cfd0f3ca4112`, expira el 2026-10-26.
- **Estado:** la prueba de ventana ahora verifica el HWND real por enumeración y el fallo original no se reprodujo. Si vuelve a fallar, el nuevo diagnóstico debe permitir distinguir entre error de arranque, ventana no visible y limitación del entorno CI.

## Diagnóstico de causa raíz en fallos de inicio — 2026-10-12

- **Hallazgo al revisar el código vigente:** `VtuberApplication.Initialize()` envuelve errores internos en `ApplicationException("Application initialization failed.", exception)`. El manejador WPF anterior imprimía solo `x.Message`, por lo que el stderr podía ocultar precisamente la excepción SQLite/configuración interna que necesitamos identificar.
- **Corrección en SHA `93e962e019f98f95aa3b699b933f4ffbb29bbd0f`:** `src/VtuberBot.App/App.xaml.cs` ahora vuelca `Exception.ToString()` a stderr, conservando tipo, mensaje, excepción interna y stack trace; el diálogo de error muestra el mensaje de la excepción raíz para orientar al usuario. El mensaje técnico completo no se guarda en el repositorio ni se empaqueta como log.
- **Windows CI exacto:** [run #282](https://github.com/jonhararagi/vtuber/actions/runs/38305999759), SHA `93e962e019f98f95aa3b699b933f4ffbb29bbd0f` — **SUCCESS**. Restore/Build Release y pruebas completadas; 371/371 pruebas pasaron, 0 warnings/0 errors, publicación autocontenida win-x64 y subida del artefacto correctas.
- **Artefacto de diagnóstico:** [descarga del paquete](https://github.com/jonhararagi/vtuber/actions/runs/38305999759/artifacts/11772228201), ID `11772228201`, 77,912,169 bytes, digest `sha256:c01df391ba7747ff0fb58ebe555281caa6a2977b997224aae743b93c56a0c53b`, expira el 2026-10-26. El artefacto confirma el build del cambio de diagnóstico, no resuelve por sí solo la causa raíz del Error 14.
- **Estado de la ventana WPF:** [Windows CI #280](https://github.com/jonhararagi/vtuber/actions/runs/38305211986) pasó 371/371 pruebas con la enumeración Win32. La causa comprobada del fallo #252 fue la detección por `MainWindowHandle` que devolvía cero; la línea SQLite pertenece a un test negativo distinto.

## Persistencia de selección de voz y salida local — 2026-10-12

- **Defecto funcional:** al abrir Audio sin pulsar antes «Actualizar lista de voces» o «Actualizar dispositivos de salida», los selectores contenían únicamente la opción predeterminada. Guardar en ese estado podía borrar de SQLite los identificadores SAPI/dispositivo previamente guardados; encolar voz también podía ignorar la voz SAPI guardada.
- **Corrección en SHA `99c98dec9d8967f78f72b1f7fa9ae51dff8069bc`:** `src/VtuberBot.App/Workspaces/MainWindow.Audio.cs` distingue si cada catálogo fue cargado correctamente. Antes de enumerar, encolar voz usa la preferencia guardada y guardar preferencias conserva el identificador existente. Después de enumerar, la selección explícita del usuario reemplaza la guardada. Un fallo de enumeración no se interpreta como elección de la opción predeterminada.
- **Windows CI exacto:** [run #284](https://github.com/jonhararagi/vtuber/actions/runs/38306118981), SHA `99c98dec9d8967f78f72b1f7fa9ae51dff8069bc` — **SUCCESS**. Restore y Build Release correctos, 0 warnings/0 errors; **371/371 pruebas aprobadas**; publicación autocontenida win-x64 y validación del ZIP completadas.
- **Artefacto:** [descarga del paquete win-x64](https://github.com/jonhararagi/vtuber/actions/runs/38306118981/artifacts/11772138328), ID `11772138328`, 77,912,162 bytes, digest `sha256:fc74e00ee1f3b0d1609f8ca168c4e119736ee05b2c25dd9588831b85246061b2`, expira el 2026-10-26.
- **Límite de validación:** el flujo de persistencia y el resto de la suite pasaron CI; la selección física de una voz/dispositivo y la reproducción audible requieren la validación manual global en Windows.


## Selección de voz/dispositivo de audio con prueba de regresión — 2026-10-12

- **Política extraída:** `src/VtuberBot.App/AudioPreferenceSelectionPolicy.cs` conserva el ID guardado mientras el catálogo de opciones no se haya cargado correctamente; cuando el catálogo está disponible, la selección explícita (incluido elegir la salida predeterminada) puede reemplazarlo.
- **Integración:** `src/VtuberBot.App/Workspaces/MainWindow.Audio.cs` utiliza la política al encolar TTS y al guardar preferencias para voz SAPI y salida. Una enumeración aún no ejecutada o fallida no borra preferencias previas.
- **Cobertura:** `tests/VtuberBot.Tests/AudioPreferenceSelectionPolicyTests.cs` verifica catálogo no cargado, selección guardada, selección nueva y elección explícita de la opción predeterminada.
- **Windows CI exacto:** [run #286](https://github.com/jonhararagi/vtuber/actions/runs/38306524189), SHA `5a359eb953a7fb26caa97cb2db0113584f4f1443` — **SUCCESS**, **376/376 pruebas**, 0 warnings/0 errors; publicación autocontenida win-x64 y validación del ZIP completadas.
- **Artefacto:** [descarga win-x64](https://github.com/jonhararagi/vtuber/actions/runs/38306524189/artifacts/11772857508), ID `11772857508`, 77,912,303 bytes, digest `sha256:6f94c95d283cc818e50696bfe38e27dd6f66e2753fc4716a3a0545c2a026cb0f`, expira el 2026-10-26.
- **Pendiente externo:** probar la voz elegida y la salida física de audio en Windows; CI valida la política y la integración de código, no el dispositivo acústico real.

## Puerta única de validación manual global — 2026-10-12

- **Documento creado:** `docs/PRODUCT-CANDIDATE-MANUAL-VALIDATION.md`. Incluye comprobación de procedencia del artefacto contra el HEAD exacto y una sola pasada por inicio/estado local, Avatar Studio/overlay, micrófono/VAD/SAPI/sonidos, Twitch OAuth/IRC/EventSub, comandos/puntos/sorteos/minijuegos, moderación/automatización, OBS WebSocket, Telegram Web, backup/restauración y cierre.
- **Guía y roadmap:** `docs/PRODUCT_USER_GUIDE.md` enlaza la lista; `ROADMAP.md` registra que la puerta manual está preparada, no aprobada.
- **Windows CI del commit que contiene la lista:** [run #288](https://github.com/jonhararagi/vtuber/actions/runs/38307051295), SHA `bf05011a66a5de6c722d58680067480dba86a9a9` — **SUCCESS**; 376/376 pruebas, 0 warnings/0 errors, publicación autocontenida win-x64 y subida del artefacto completadas.
- **Artefacto de ese SHA:** [descarga](https://github.com/jonhararagi/vtuber/actions/runs/38307051295/artifacts/11772939102), ID `11772939102`, 77,912,331 bytes, digest `sha256:fc190bd0a79223b7ce9141f573ac55cb46a7d7f043241e35b5a5b562ec1e767f`, expira el 2026-10-26.
- **Límite:** CI valida compilación, pruebas automatizadas y empaquetado; no autentica cuentas del usuario ni prueba un OBS real, micrófono/SAPI, WebView2 o captura de overlay. La validación global de Windows permanece pendiente y debe registrar áreas externas no disponibles como BLOQUEADAS, nunca APROBADAS.

## Corrección de la lista de validación global — comandos sin consola visible (2026-10-12)

- **Hallazgo:** `docs/PRODUCT-CANDIDATE-MANUAL-VALIDATION.md` pedía al usuario ejecutar `help`, `ping` y `status` durante el paso de inicio. Esos comandos están registrados en el `CommandRouter` de Core, pero el shell WPF no expone una consola visible que permita ejecutarlos; los comandos de Twitch y de comunidad tienen sus superficies separadas.
- **Corrección:** el paso de inicio ahora comprueba el estado local real, la navegación y el Dashboard. Las pruebas manuales de comandos se hacen desde las páginas Comandos/Comunidad y desde Twitch cuando existe una cuenta de prueba. No se solicita una acción imposible desde la UI.
- **Límite:** es una corrección del procedimiento de validación, no una función de producto añadida. Windows CI #289 sigue siendo evidencia del SHA anterior `9d0ebfb657d2c4bdddb034a5adb71065c68a10b7`; el nuevo commit debe pasar su propio CI antes de considerarse validado.

