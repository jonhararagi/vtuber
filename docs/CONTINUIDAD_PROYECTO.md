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

- **Último SHA con CI completo verde verificado antes de las nuevas mejoras de producto:** `0f5f4f177ed0e9861fde449c7ef584c5b907e88d` (corregido umbral/tests del detector).
- **Run:** [Windows CI #38214945910](https://github.com/jonhararagi/vtuber/actions/runs/38214945910) — restore, build Release, tests, publish self-contained Windows x64 y upload de artefacto: **SUCCESS**.
- **Artefacto:** [VTuberBot-Product-Candidate-win-x64](https://github.com/jonhararagi/vtuber/actions/runs/38214945910/artifacts/11732285947) · ID `11732285947` · 77,780,953 bytes · SHA-256 `1c6cd363b2dc4eef1b9fa4dc0f5778b7ea9904a0f81d01e21066466de1381882` · expira el 2026-10-26 06:56:50 UTC.
- Después de ese SHA se añadieron una tarjeta de alerta temporal al overlay y edición/pausa de timers, además de corregir textos y metadatos del paquete. El artefacto anterior no contiene esos cambios y no debe entregarse como el HEAD final; se requiere un nuevo CI/artifact exacto.

### Limitaciones explícitas del candidato

- **PENDIENTE:** reconexión automática de EventSub, recuperación de suscripciones revocadas, estilos/plantillas configurables de alertas y más tipos de eventos.
- **PENDIENTE:** rig 2.5D avanzado con pivotes/huesos/física, expresiones faciales basadas en assets y lip-sync real por fonemas/visemas.
- **IMPLEMENTADO EN CÓDIGO:** detector local de actividad acústica con suavizado, umbral y retención configurable; alimenta el estado Talking/Idle del overlay. No es reconocimiento de habla.
- **PENDIENTE:** cola de voz priorizada, enrutamiento avanzado de audio y lip-sync por fonemas/visemas.
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
