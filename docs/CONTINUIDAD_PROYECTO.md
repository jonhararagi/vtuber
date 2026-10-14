# Continuidad del proyecto — VTuberBot

## 2026-10-14 — Restauración de la galería visual de personajes chibi

- Se restauró en `src/VtuberBot.App/Workspaces/MainWindow.AvatarStudio.cs` la galería que estaba definida por seis presets y sus miniaturas pero ya no se mostraba en la interfaz. Ahora Avatar Studio vuelve a mostrar doce opciones: Neko gamer, Brujita lunar, Idol estelar, Gótica rosa, Cyber neón, Streamer casual, Zorrita de festival, Oráculo invernal, Guardiana kitsune, Pop celestial, Capitana pirata y Conejita de café.
- Cada tarjeta usa `BuildChibiPresetButton` y `ModularWardrobeFactory.EnsureAssets` para previsualizar las piezas PNG transparentes originales generadas localmente; al pulsarla se crea un perfil separado con cabello, ropa, accesorios, forma de cara, ojos, tono de piel y pose propios. No reemplaza los perfiles existentes.
- El catálogo modular ya contiene 11 peinados y 15 conjuntos, además de opciones separadas para cara/ojos, poses y accesorios. Se ampliaron los presets con seis combinaciones más que reutilizan IDs ya existentes en el catálogo modular. Esta corrección vuelve visible una biblioteca que ya existía en el código, en lugar de fingir que se añadieron ilustraciones profesionales externas.
- La guía de usuario ya describe la galería y el catálogo. La primera CI de esta serie detectó una concatenación mal formada en el prompt manual de ChatSpeak; se corrigió en `6cb4487`. La CI del código corregido y la galería ampliada debe confirmarse en el SHA final; no se afirma que la galería se haya comprobado visualmente en Windows todavía.
- Todo sigue en `product/complete-vtuber-experience`; no se tocó `main` ni el estado del PR #9.

## 2026-10-14 — Perfiles editables de personalidad para ChatSpeak

- Se agregó `src/VtuberBot.Core/SocialBotPromptCatalog.cs` con cinco perfiles seleccionables: Twitch streamer, Anime tsundere, Streamer tranquilo, Moderador útil y Personalizado. Los perfiles predefinidos orientan el estilo; no ejecutan comandos arbitrarios ni conceden permisos.
- `Bot social → Personalidad y proveedor de IA` ahora incluye selector de perfil y campo de instrucciones adicionales del creador (hasta 3000 caracteres), guardados en SQLite mediante las claves de configuración existentes. Las instrucciones se agregan al prompt de respuesta automática, al comentario periódico, a la prueba de IA y al prompt copiado para ChatGPT manual.
- Se añadieron pruebas unitarias en `tests/VtuberBot.Tests/SocialBotPromptCatalogTests.cs` para los perfiles, combinación de instrucciones, límites y perfiles desconocidos.
- `docs/CHATSPEAK-STUDIO.md` explica los perfiles y aclara que seleccionar un perfil no conecta por sí solo un proveedor. ChatGPT web sigue siendo copiar/pegar; Gemini u otro proveedor compatible requiere una clave válida para el modo automático.
- Commits: catálogo `e2e91d0`, aplicación al runtime `9dda96e`, selector/editor UI `8bf6db8`, pruebas `134dc5d`, guía `f71183b`.
- Validación pendiente en el momento de este registro: esperar CI de Windows en el SHA final de esta serie y confirmar el resultado; todavía no se declara una prueba manual de ChatSpeak ni una conversación real con Gemini/ChatGPT.
- Todo sigue en `product/complete-vtuber-experience`; no se ha cambiado `main` ni el estado del PR #9.

## 2026-10-14 — Voz japonesa local VOICEVOX por reacción

- Se agregó `src/VtuberBot.App/VoicevoxSpeechSession.cs`, adaptador de TTS local para el motor HTTP oficial de VOICEVOX. Consulta `/speakers`, sintetiza por `/audio_query` + `/synthesis`, reproduce WAV con NAudio y acepta solo destinos localhost/loopback. No requiere clave API ni envía el texto a un servicio remoto.
- `ConfigurableSpeechSession` ahora detecta IDs `voicevox:<speakerId>` y los enruta al proveedor VOICEVOX aun cuando el TTS global esté en modo remoto. La cola de voz existente y su compuerta de audio siguen controlando la reproducción; las reglas conservan expresión, cooldown y límites del motor de reacciones.
- `Centro de directo → Editor de reglas` agrega al selector las voces/estilos detectados en el motor local cuando se abre la página y suma **Escuchar muestra japonesa de la voz seleccionada**. La muestra es 「お兄ちゃんのばか！ なに!? おかえりなさい！」. Si el motor no está activo al abrir la página, hay que iniciarlo y reabrir Centro de directo para cargar su lista. Si una reacción japonesa no tiene voz elegida y el motor local está abierto, el runtime toma la primera voz VOICEVOX disponible automáticamente; una voz configurada por regla siempre tiene prioridad.
- Se añadió `tests/VtuberBot.Tests/VoicevoxSpeechSessionTests.cs` para validar IDs y el enrutamiento hacia el proveedor local sin requerir un motor de voz real.
- Descarga/uso: [sitio oficial VOICEVOX](https://voicevox.hiroshiba.jp/) y [repositorio oficial del motor/API](https://github.com/VOICEVOX/voicevox_engine). El motor no se empaqueta dentro de VTuberBot por su tamaño y por los términos específicos de cada voz/personaje; revisar atribución/licencia antes de publicar o monetizar.
- Archivos: `src/VtuberBot.App/VoicevoxSpeechSession.cs`, `src/VtuberBot.App/RemoteSpeechSession.cs`, `src/VtuberBot.App/MainWindow.xaml.cs`, `src/VtuberBot.App/Workspaces/MainWindow.LiveCenter.cs`, `tests/VtuberBot.Tests/VoicevoxSpeechSessionTests.cs`, `docs/PRODUCT_USER_GUIDE.md`.
- Commits de esta implementación: adaptador `47cda317b8b521f38d8d42ad5ba75abfb9c10828`; enrutamiento `f6f85b54ff1a8a8747153cde87f49af07f66d23c`; conexión desde ventana `d2de9aeda7a8fc3249ea9022576895315d8f29a2`; selector/muestra `f62e195607a7cbb7a4abd679ad324ce1de3de83d`; validación de IDs `ade42ef2754c6cc820c8b4ad2ed778d518c1214e`; corrección de namespace HTTP `42f202ca1e0fba3a74051c13c54d78ce1e0a680b`; corrección de `System.IO` `9679514d6b5fe2ed9dbccc6d8f21de40367297fb`; guía `b250f9ba91032b666a42f2166eb04c8b9dfb738a`; pruebas `41876857661317f92521773df6e5153eff144bb1`; selección automática para reacciones japonesas sin voz explícita `efc3e6781cdcf27856e57e5f1b1b3b8e5173abb0`.
- **Validación Windows CI completada:** la ejecución [#737 / run 38633025885](https://github.com/jonhararagi/vtuber/actions/runs/38633025885) sobre el commit `1166c70719cb87a5fee9c62af17fbf484c10af04` terminó con `success`: Restore, Build Release, pruebas automatizadas, publicación del paquete autocontenido Windows x64 y subida del artefacto completados. Artefacto `VTuberBot-Product-Candidate-win-x64-1166c70719cb87a5fee9c62af17fbf484c10af04` (aprox. 84.7 MB; descarga desde la ejecución de Actions, con vencimiento indicado por GitHub). Los errores CS0246 anteriores por `System.Net.Http` y `System.IO` se corrigieron antes de esta ejecución exitosa. Esta evidencia valida compilación y pruebas automatizadas de ese SHA; no sustituye una prueba de escucha real con VOICEVOX en el PC del usuario. Tampoco se ha probado el audio con el motor VOICEVOX real; las pruebas añadidas son unitarias de identificación/enrutamiento, no demuestran pronunciación ni reproducción física.
- Todo el trabajo permanece en `product/complete-vtuber-experience`; no tocar `main` ni cerrar/fusionar el PR #9.


## 2026-10-14 — Referencias de prendas gratuitas y nuevos estilos de prueba

- Se amplió `src/VtuberBot.App/AvatarWardrobeStudioWindow.cs` con tres opciones geométricas de prueba: vestido retro, pijama pastel y camiseta Y2K oversize; las opciones de pijama/Y2K dibujan pantalón. Son aproximaciones visuales inspiradas en estilos publicados, no los archivos originales.
- Se añadieron enlaces dentro del taller a tres recursos gratuitos para abrir sus imágenes y páginas originales: [V.Y Atelier — vestido retro](https://booth.pm/en/items/5010638), [Darmangy — pijama pastel](https://booth.pm/en/items/7692844), [VroidDressing — camiseta Y2K](https://booth.pm/en/items/6191973). No se descargaron ni copiaron sus archivos a VTuberBot, y todavía no hay importador compatible con `.vroidcustomitem`.
- `docs/AVATAR-WARDROBE-STUDIO.md` documenta los nuevos estilos y la diferencia entre referencia visual e importación real.
- Commits: opciones del selector `b602224acd94a06c854ea74e4446779c129ba8ed`; detalle de vestido retro `b4256925ad35cb6b7efc6876f08e96a2c1dbd8de`; pantalón de prueba `92bb18d24c4487bd9be4f759dae35fe436b326d1`; parte superior holgada `21afb0c427a2172dc36575c94c64010336ee604e`; ajuste de falda/pantalón `f373413f11a144fdc6b112fdc0e77f0e13e016ce`; enlaces de referencias `d32836091abd9d1448671f606657a068b7a9a830`; documentación `94b44cce4d4d8fbb21b8a3e67db72beab0e6ea84`.
- No se ha validado manualmente el aspecto en WebView2. El CI debe comprobarse para el SHA final después de esta actualización de continuidad.
- Se mantuvo todo en `product/complete-vtuber-experience`; no se tocó `main` ni el estado del PR #9.

## 2026-10-14 — Recoloración rápida del vestuario y criterio de licencias

- En `src/VtuberBot.App/AvatarWardrobeStudioWindow.cs` se agregó un control **Variación de tono · ropa y accesorios** (0–360°). Rota el matiz de los colores principal/secundario del vestuario sin cambiar la geometría ni el tono de piel; los selectores de color individuales siguen disponibles. El valor se guarda/carga con el JSON de apariencia.
- `docs/AVATAR-WARDROBE-STUDIO.md` aclara el control y registra que recolorear un asset no elimina derechos de autor ni reemplaza la licencia.
- No se han incorporado modelos, texturas ni prendas de terceros al repositorio. La búsqueda de recursos externos debe filtrar por licencia que permita el uso previsto y, si se van a distribuir dentro de VTuberBot, redistribución y modificación. Las guías oficiales de VRoid advierten que sus contenidos no son CC0 y que las aplicaciones generadoras de modelos basadas en sus mallas/texturas pueden requerir licencia aparte: https://vroid.com/en/studio/guidelines.
- Commits de esta tarea: código del control de tono `8d47544742926c2b53f31754f7e802901761a0c7`, reinicio del tono al cambiar de personaje base `d2a38e38dc210d4761867de2d4bf9bb73035e32f`; documentación `ac3c1c8b13e29d3ad7d13fac722c5441fa243b13`.
- **Validación:** Windows CI anterior #657 terminó SUCCESS para SHA `9f06ff88a69fe3d2ed811d71cabd96d46f69277a`, pero es anterior al nuevo control de tono. Debe verificarse un run para el SHA final después de actualizar esta continuidad. No se ha validado manualmente el visor WebView2.
- Se mantiene el trabajo en `product/complete-vtuber-experience`; no tocar `main` ni cerrar/fusionar el PR #9.

> Registro central de continuidad para GPT Cerebro, GPT Obrero, Gemini Inspector y agentes futuros. Actualizar este archivo al finalizar cada tarea de implementación o corrección. No duplicar registros por tarea.
>
> **Regla de evidencia:** separar siempre cambios implementados, pruebas automatizadas, validación manual y pendientes. Un resultado solo corresponde al SHA exacto que lo produjo.

## 2026-10-14 — Biblioteca de personajes base del taller 3D

- En `src/VtuberBot.App/AvatarWardrobeStudioWindow.cs` se agregó el selector **Personaje base** con cuatro preajustes diferenciados: Exploradora bronceada (predeterminado), Guardiana del desierto (piel cobriza), Aprendiz lunar y Viajera de jade (piel oliva).
- Cada preajuste establece una combinación inicial de silueta/altura, tono de piel, pelo, iris, expresión, vestuario, colores y accesorio. Luego se pueden modificar los campos individualmente, guardar/cargar el aspecto JSON y exportar la geometría visible a GLB.
- La base predeterminada se inicia con piel bronceada `#b87955`, cabello marrón oscuro e iris marrones. Se usa la geometría paramétrica ya implementada en el taller, no un archivo de modelo externo. No registrar esto como integración de un prefab profesional ni como modelo riggeado.
- `docs/AVATAR-WARDROBE-STUDIO.md` actualizado con los cuatro presets y su alcance.
- Commits: código inicial `cdfb3896063c3349bc29f48de33821f1f6dfaa2c`, inicialización de la base predeterminada `55a0b5245aac82d337001bcf6c3a56e8b7254373`, documentación `6142409c6774a2dada48dec92fe036a05b97dcbc` y sincronización del selector al restablecer `a54da435291d281c4182d5e8049769c68492e053`.
- **Validación pendiente:** el rerun de Windows CI de la ejecución anterior terminó SUCCESS, pero correspondía al SHA antiguo `a5fe0e36a33ac277a7bd1d44a1cbf44452724c3d`; no valida estos commits. La vista y la exportación GLB no se han probado manualmente en WebView2. Los presets son variaciones paramétricas y no solucionan la falta de rig/VRM ni el estilo todavía prototípico.

## 2026-10-14 — Taller 3D: exportación GLB inicial

- Se mantuvo el trabajo exclusivamente en `product/complete-vtuber-experience`; no se cambió `main` ni se modificó el estado del PR #9.
- `src/VtuberBot.App/AvatarWardrobeStudioWindow.cs`: el taller añade el botón **Exportar modelo 3D (.glb)** usando `THREE.GLTFExporter`. Exporta la malla y materiales del personaje generado como glTF binario; el JSON de apariencia continúa disponible por separado.
- `docs/AVATAR-WARDROBE-STUDIO.md`: documenta formato, límites y validación pendiente.
- **Límite:** GLB no equivale a VRM. Este prototipo no incorpora rig, pesos de deformación, animaciones, tracking facial, importación de prendas externas ni integración del modelo con el overlay/OBS. Three.js y el exportador siguen cargándose desde CDN y requieren Internet.
- **Evidencia CI anterior:** ejecución [38589295745](https://github.com/jonhararagi/vtuber/actions/runs/38589295745), para el SHA previo `a5fe0e36a33ac277a7bd1d44a1cbf44452724c3d), compiló Release correctamente, pero falló una prueba automatizada ajena al taller: `SchedulerIsolatesCallbackFailure` (timeout de espera, línea 993 de `tests/VtuberBot.Tests/Tests.cs`). La publicación del artefacto fue omitida. Se solicitó reintentar ese job; el reintento y el CI de los nuevos commits deben verificarse antes de declarar verde el estado actual.
- **Validación manual:** no ejecutada. La descarga de GLB debe probarse en Windows/WebView2 y el archivo debe abrirse en un visor glTF.
- Commits de esta etapa: `c63419e7812d4e5b4a42c7c4b2ca7393b51d3f27` (código GLB) y `78a4af1d31b66de0dccab2d6ed86e057c3a5f7f5` (documentación).

## 1. Estado general

- **Repositorio:** https://github.com/jonhararagi/vtuber
- **Rama principal:** `main`
- **Rama de distribución:** `main` es la rama que se entrega al usuario. Los registros históricos más abajo conservan los SHA y resultados que les corresponden; no deben interpretarse como el HEAD actual ni como CI de cambios posteriores.
- **Rama histórica de estabilización:** `v0.2.3/stabilization-gate`.
- **Rama de producto activa:** `product/complete-vtuber-experience` (rama nueva para la entrega integrada; creada desde el HEAD de PR #9 `4ba87d8f94d65a84dba475c8b0f9103380892777`).
- **PR de estabilización histórico:** [#9 — feat: add v0.2.3 stabilization gate](https://github.com/jonhararagi/vtuber/pull/9) fue fusionado el 2026-10-13; esta orden no modificó su estado. **PR de integración del producto:** [#13](https://github.com/jonhararagi/vtuber/pull/13), con la rama `product/complete-vtuber-experience` como origen; consultar su estado actual en GitHub.
- **Versión en desarrollo:** V0.2.3. No implica que el hito esté aprobado ni que se haya publicado una versión final.
- **Arquitectura vigente para el producto completo:** C#/.NET 10/WPF/SQLite; Core independiente de WPF; sin EF Core; módulos de avatar, audio, Twitch, comunidad/engagement, OBS y Telegram desacoplados por contratos/eventos. La especificación activa es `docs/PRODUCT_BLUEPRINT.md`. El usuario autorizó el 2026-10-12 a completar el diseño y el alcance global del producto antes de solicitar una validación manual integrada.
- **Flujo de trabajo:** GPT Cerebro decide y audita; GPT Obrero implementa cambios aprobados en GitHub; Gemini Inspector revisa. GitHub es la fuente de verdad.

## Corrección de producto — 2026-10-13: ejemplos de avatar incluidos

- **Hallazgo confirmado:** el Avatar Studio nativo requería que el usuario importara una imagen antes de mostrar un personaje; el estado vacío mostraba «Todavía no hay arte importado». El modo PNGTuber Plus solo configuraba una aplicación externa instalada aparte. Por eso el candidato no permitía probar un avatar visual desde una instalación limpia.
- **Implementado en main:** `src/VtuberBot.App/DemoAvatarFactory.cs` genera sprites PNG transparentes de ejemplo localmente, sin red ni servicios externos. El editor Avatar Studio ofrece ahora **Probar ejemplo PNGTuber (reposo / habla)** y **Probar ejemplo 2D por capas**.
- El ejemplo PNGTuber crea sprites completos Idle, Talking, Blink, Happy y Surprised en un perfil separado.
- El ejemplo 2D crea un perfil separado con sprites alineados y capas transparentes de flequillo y destellos que se pueden mover, escalar y rotar. Los perfiles propios no se sobrescriben; si el perfil de ejemplo ya existe, el botón lo activa en lugar de recrearlo.
- **Límites explícitos:** son sprites 2D y movimiento/parallax ligero, no un rig Live2D, no deformación ósea ni seguimiento facial por cámara. PNGTuber Plus continúa siendo una aplicación de terceros que el usuario instala separadamente.
- **CI de la implementación:** Windows CI #569 para SHA `834a54c2e641ff7d5812a06ddfefa35c8a9ba77a` terminó **SUCCESS**: restore, compilación Release, pruebas automáticas, publicación self-contained win-x64 y subida del artefacto. [Ejecución #569](https://github.com/jonhararagi/vtuber/actions/runs/38447651443) · [ZIP #11839366497](https://github.com/jonhararagi/vtuber/actions/runs/38447651443/artifacts/11839366497) · 78,077,972 bytes · SHA-256 `9449c6f68eb03c303f509778c0ec740e4a4851a2de799538a8ad3dae5775cc81` · expira el 2026-10-27 18:34:55 UTC. La compilación/pruebas validan ese SHA; todavía no hay prueba visual manual en el PC del usuario.
## 2026-10-13 — Catálogo modular de ropa, cabellos y accesorios VTuber

- Se investigaron perfiles oficiales de Hoshimachi Suisei, Sakura Miko, Usada Pekora, Houshou Marine, Gawr Gura, Mori Calliope, Tokoyami Towa, Takanashi Kiara, Robocosan, Kuzuha y Vox Akuma; también se consultó el archivo fan de modelos/atuendos de Ironmouse. La documentación con enlaces y análisis visual está en `docs/VTUBER_REFERENCIA_Y_BIBLIOTECA_MODULAR.md`.
- No se extrajeron modelos ni ilustraciones oficiales para redistribuir. Se incorporó un catálogo original que genera localmente PNG transparentes en lienzos alineados de 512×768.
- `ModularWardrobeFactory` genera 8 peinados, 10 conjuntos, 10 accesorios de cabeza, 6 accesorios faciales/joyería y 6 piezas traseras. Avatar Studio añade controles para combinar una opción por categoría y crear un perfil nuevo sin sobrescribir los ya existentes.
- Cada pieza se guarda como una capa independiente y puede moverse, escalarse, rotarse, ocultarse o cambiarse de orden. Se amplió el criterio de capa de fondo para que piezas llamadas `Fondo · ...` se rendericen detrás del personaje.
- Se actualizaron `README.md` y `docs/PRODUCT_USER_GUIDE.md` con la ruta del armario modular, los pasos para usarlo y los límites del rig.
- **Windows CI #574:** SHA `730bfbb18e8708f898e911bed238d2d4a1b6941d`, compilación Release, pruebas y publicación self-contained win-x64: **SUCCESS**. [Ejecución #574](https://github.com/jonhararagi/vtuber/actions/runs/38449079952) · [Artefacto 11840121621](https://github.com/jonhararagi/vtuber/actions/runs/38449079952/artifacts/11840121621) · 78,096,918 bytes · SHA-256 `3168345701bb240e42bbd83f36df19f04a45e044de70cb0d22e9dbc1f71cf4cd` · expira el 2026-10-27 18:45:47 UTC.
- Los commits posteriores incluidos hasta este registro son documentación (`README.md`, guía y catálogo de referencias); la comparación GitHub confirmó que no cambian los archivos de aplicación respecto al SHA validado. **Pendiente:** comprobar visualmente las combinaciones y el overlay en el equipo Windows del usuario; CI no acredita calidad artística ni pruebas manuales de pantalla.
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

## Centro de integraciones externas — 2026-10-12

- **Inspección previa:** HEAD comprobado de la rama antes de los cambios: `c70dddf9eec2a7a5871bacf7c3bb098a88cdb80d`. Se inspeccionaron el shell WPF real, la navegación dinámica, `ProductStore` y el runner de pruebas; no se asumió que la documentación representara código ya implementado.
- **Implementado:** `src/VtuberBot.Core/ExternalApplicationManager.cs` añade un límite sustituible `IExternalApplicationRuntime`, gestión de estados y apertura de ejecutables mediante Windows Shell. Comprueba la existencia del archivo y la identidad/ruta del proceso cuando Windows permite inspeccionarla.
- **Interfaz:** `src/VtuberBot.App/Workspaces/MainWindow.ExternalApplications.cs` añade una página WPF funcional para PNGTuber Plus y Streamer.bot, con selector de ejecutable, guardado, apertura, actualización de estado y mensajes de error reales. Se añadió la navegación y el enrutamiento en `MainWindow.xaml` y `MainWindow.xaml.cs`.
- **Persistencia:** las rutas se guardan en preferencias locales SQLite mediante `ProductStore.GetSetting/SetSetting`; claves `integrations.pngTuberPlus.path` e `integrations.streamerBot.path`. No se descargan ni empaquetan aplicaciones externas.
- **Semántica de estado:** sin configurar, no encontrado, configurado, en ejecución, inicio solicitado y error. El texto deja claro que la presencia del proceso no demuestra conexión/autenticación. El cierre de VTuberBot no detiene procesos externos.
- **Pruebas añadidas:** `tests/VtuberBot.Tests/ExternalApplicationManagerTests.cs` cubre ruta vacía, ejecutable ausente, ejecutable configurado, proceso en ejecución sin falsa conexión, fallo real de inicio y solicitud de inicio correcta usando un runtime falso. No requiere instalar o abrir aplicaciones reales.
- **Documentación:** `docs/EXTERNAL-INTEGRATIONS.md`, enlazada desde `docs/PRODUCT_USER_GUIDE.md`.
- **Estado al registrar este bloque:** implementación y pruebas añadidas al repositorio; **CI del HEAD final pendiente de verificación**. No declarar compilación/tests aprobados hasta revisar la ejecución de Windows CI asociada al SHA exacto.
- **Alcance no fingido:** este primer bloque coordina ejecutables locales. Todavía no implementa un protocolo de conexión/acciones internas de PNGTuber Plus o Streamer.bot; esas capacidades requerirán estudiar sus interfaces públicas y verificar cada conexión por separado.

### 2026-10-12 — Orden 2/5: espacio de trabajo PNGTuber Plus

- **Base de trabajo comprobada:** se comparó el commit comunicado al inicio, `81e6dfee3670bad7649507653c1da21cb59d5c8d`, con el commit de trabajo `83db64b94b1822ada82fb9d175792be120c628de`. GitHub informa `ahead`, 20 commits, 0 behind; se conservaron los cambios previos de la rama. No se modificó `main`, ni se fusionó/cerró PR #9.
- **Investigación de fuente:** se revisaron https://pngtuber.cc/es/ , https://pngtuber.cc/about/ , https://github.com/kaiakairos/PNGTuber-Plus y https://github.com/kaiakairos/PNGTuber-Plus/blob/master/main_scenes/main.gd . El portal pngtuber.cc declara ser un sitio independiente de fans; anuncia versión 1.4.5, pero eso no demuestra qué versión tiene el usuario. El archivo de entrada del repositorio público muestra hotkeys de disfraces por defecto `1`–`0` y una señal interna para alternar visibilidad de sprites. No se encontró un contrato público documentado de IPC/WebSocket/API en las fuentes revisadas; no se simula uno.
- **Interfaz:** nueva página WPF `Avatar externo`, selector y persistencia de ejecutable reutilizando la clave `integrations.pngTuberPlus.path`, apertura/actualización de estado, enlaces de referencia y aviso cuando no se detectan .pck/.dll junto al ejecutable. No descarga ni empaqueta binarios de terceros.
- **Asistente:** `PngtuberSetupService` guarda el paso actual y las confirmaciones manuales en SQLite. La ruta y el proceso son señales automáticas; abrir la ventana, avatar preparado, micrófono reactivo, expresiones y OBS necesitan confirmación manual. El proceso en ejecución por sí solo no completa los pasos. Se puede retroceder y recuperar progreso al reiniciar.
- **Atajos:** `PngtuberHotkeyRegistry` valida/persiste nombre, tecla individual, descripción y aplicación objetivo. `WindowsPngtuberHotkeySender` intenta enviar una tecla solo a la ventana identificada de PNGTuber Plus después de enfocarla explícitamente; no usa coordenadas. Los resultados distinguen enviado, cancelado, no configurado, proceso ausente, no compatible y error. `WasConfirmed` siempre es falso: Windows no demuestra que el avatar cambiara.
- **Compatibilidad:** el editor nativo Avatar Studio y los contratos de perfiles/capas no se sustituyeron. Se añadió una prueba de regresión para confirmar que guardar ajustes externos no altera perfiles ni capas nativos; no se declara compatibilidad/conversión de `.vtuberavatar`.
- **Pruebas añadidas:** `tests/VtuberBot.Tests/PngtuberPlusSetupTests.cs` cubre recuperación de progreso, persistencia SQLite del ejecutable, confirmaciones manuales, política de atajos, errores/cancelación, no confirmar cambios visuales y conservación del avatar nativo. Las pruebas usan dependencias sustituibles y no necesitan PNGTuber Plus instalado.
- **Documentación:** `docs/PNGTUBER-PLUS-AVATAR-SETUP.md`, ampliación de `docs/PRODUCT_USER_GUIDE.md` y sección de avance en `ROADMAP.md`.
- **Estado de verificación al registrar este bloque:** CI de Windows para el SHA final de la documentación pendiente de revisar. No declarar aprobado hasta inspeccionar build, tests y artefacto que corresponda al HEAD exacto. La validación manual de PNGTuber Plus/micrófono/OBS sigue pendiente.

- **Seguimiento de la misma orden:** se añadieron pruebas reales de persistencia SQLite reabriendo `ProductStore`, de que la presencia del proceso no marca el paso como confirmado, y de conservación de perfiles/capas. Se corrigió el nombre del método de política compartida en la prueba y se revisó el tamaño nativo de la unión Win32 `INPUT` usada por `SendInput`.
- **Límite de evidencia:** la interfaz del conector GitHub disponible aquí devuelve solo estados de commit y runs de PR; no expone la lista de runs de `push`. Para el SHA de código consultado no aparecen estados publicados. El lector de navegador público no pudo abrir GitHub Actions por falta de créditos. Por tanto, no se debe afirmar que el CI del SHA final pasó hasta que se pueda verificar el run de Windows correspondiente. No se intentó sustituir esta evidencia por una compilación local no ejecutada.

- **Ajuste de fuentes y cierre de código de la Orden 2:** se añadió un acceso directo a la página de distribución del creador en itch.io (`https://kaiakairos.itch.io/pngtuber-plus`) y se aclaró que `pngtuber.cc` es un portal independiente de información/descarga, no el desarrollador. El commit de código previo a este registro fue `c8ee4e9044d1b528fabc3818653356071e6e57ca`; comparación contra la base `81e6dfee3670bad7649507653c1da21cb59d5c8d`: `ahead`, 27 commits, 0 behind.
- **Última compuerta antes de aceptación:** este registro documental generará un SHA nuevo. El workflow `.github/workflows/windows-ci.yml` está configurado para push en la rama de producto, pero la herramienta de Actions disponible solo lista runs de PR y no muestra runs de push; el lector público de navegador devolvió falta de créditos. No hay estados de commit publicados en la respuesta del conector. La aceptación exige verificar externamente el run y el artefacto de Windows del SHA final exacto; hasta entonces el bloque sigue pendiente de CI y de validación manual con PNGTuber Plus/OBS reales.


### 2026-10-12 — Orden 3/5: cliente WebSocket Streamer.bot (pendiente de aceptación CI)

- **HEAD de entrada confirmado en el historial de trabajo:** `c81e18d7aa702ad59114df702aef9ab12d0cd84d`. El primer commit de la nueva implementación añadió `src/VtuberBot.Core/StreamerBotWebSocketClient.cs`; la rama siguió avanzando durante esta orden.
- **Implementación añadida en la rama de producto:** cliente Core `ClientWebSocket`; `Hello` y autenticación SHA-256/Base64 conforme a la guía oficial; estado de conexión derivado del protocolo; solicitudes con GUID y correlación por ID; lectura fragmentada con máximo de 1 MiB; `GetActions`, `DoAction`, `GetEvents` y `Subscribe`; página WPF dedicada; contraseña protegida por DPAPI; limpieza del cliente durante el cierre; prueba con servidor WebSocket simulado.
- **UI:** nuevo espacio `Streamer.bot`, separado de la página de ejecutables externos y del workspace PNGTuber Plus. Las acciones solo se ejecutan tras confirmación explícita. Las suscripciones son concretas y se consultan desde `GetEvents`; no se suscribe a todos los eventos.
- **Pruebas añadidas:** `tests/VtuberBot.Tests/StreamerBotWebSocketTests.cs` cubre validación del endpoint, generación de prueba de autenticación y una conversación de protocolo con servidor WebSocket simulado (Hello fragmentado, autenticación, catálogo vacío, eventos, suscripción y DoAction).
- **Documentación actualizada:** `docs/EXTERNAL-INTEGRATIONS.md`, `docs/PRODUCT_USER_GUIDE.md`, este archivo y `ROADMAP.md`.
- **Límites conocidos:** reconexión manual, sin EventManager genérico encontrado en el Core actual, sin ejecución automática de acciones a partir de eventos. No se probó contra Streamer.bot/OBS/PNGTuber Plus reales.
- **Evidencia pendiente:** no declarar la orden aceptada hasta comprobar restauración, compilación, todas las pruebas y paquete Windows para el SHA final exacto. La existencia del workflow no prueba que haya terminado correctamente.


- **Corrección durante la validación de Orden 3/5:** el primer Windows CI consultado (run [38324588159](https://github.com/jonhararagi/vtuber/actions/runs/38324588159), commit `96e8d284f88ae38732edf5d43ebd4944dfa66b39`) falló en compilación por un literal multilínea sin escapar preexistente en `MainWindow.PngtuberWorkspace.cs:118-121`, perteneciente al trabajo previo de PNGTuber Plus. Se corrigió con un literal raw multilínea de C# sin cambiar la funcionalidad. El fallo no prueba el estado de los commits posteriores; debe verificarse un nuevo run para el SHA final.
### Orden 4/5 — Coordinación de eventos y acciones (estado de trabajo, no aceptado)

- Añadido `VtuberBot.Core.EventActionCoordinator`: cola acotada, evento normalizado, reglas persistentes a través de `IProductSettingsStore` (preferencias SQLite existentes), evaluación de condiciones JSON seguras, cooldown, límites por regla/globales, deduplicación limitada, límite de profundidad causal y cierre cancelable.
- El puente de Streamer.bot publica `EventReceived` en la cola del Core sin esperar el despacho de acciones. El puente interno publica las notificaciones que ya procesa `ProcessEventSubEvent`; no reimplementa comandos de chat. Los eventos internos conservan su origen separado.
- Nuevo espacio WPF **Automatizaciones** para reglas, filtros múltiples simples, destinos reales, pausa global, autorización separada de teclas PNGTuber Plus y actividad de resultados. Las reglas nuevas nacen desactivadas y el arranque por defecto queda pausado.
- Los destinos Streamer.bot se revalidan contra `GetActions` y requieren confirmación asociada al ID exacto de la acción. Los atajos PNGTuber Plus se revalidan contra el registro existente; el envío automático permanece desautorizado por defecto y no se interpreta como cambio visual confirmado.
- Las suscripciones manuales y las solicitadas por reglas se reconcilian mediante `Subscribe`/`UnSubscribe`; no hay eventos preseleccionados por defecto. Los eventos externos no se convierten automáticamente en comandos nativos ni los resultados de `DoAction` se vuelven a publicar como eventos.
- Pruebas nuevas cubren persistencia entre instancias, fuente/tipo/filtros, pausa, reglas desactivadas, deduplicación por ID, eventos legítimos con texto idéntico, cooldown/límite, fallo aislado, autorización PNGTuber, cola saturada y cierre. Se ampliaron las pruebas WebSocket para autenticación desactivada/incorrecta, respuestas fuera de orden, timeout, cancelación, JSON malformado, desconexión y `UnSubscribe`.
- **Pendiente de aceptación:** estas pruebas se añadieron al repositorio, pero no se debe afirmar que compilan o pasan hasta observar Windows CI sobre el SHA final exacto. La prueba manual con Streamer.bot/Twitch/OBS/PNGTuber Plus sigue pendiente. El adaptador de EventSub interno solo dispone del resumen expuesto por el productor actual; no se afirma que sea un payload completo de EventSub.
### Seguimiento real de Windows CI para la Orden 4 — 2026-10-13 UTC

- Run [#380](https://github.com/jonhararagi/vtuber/actions/runs/38328651554), SHA `4159a16939dd4d1ac8bfa1da4cd57ea5c8928b9b`: compilación fallida por literal de carácter inválido en el editor de condiciones; no hubo tests ni artefacto. Corregido posteriormente a un literal C# válido.
- Run [#390](https://github.com/jonhararagi/vtuber/actions/runs/38328971940), SHA `23d9a1840a0eed27cb7c23fd667676073606caf8`: compilación fallida por `System.IO` ausente en archivos de integración y variables del asistente PNGTuber capturadas antes de inicializarse. Se añadieron los imports y se inicializaron las referencias de botones antes de crear los callbacks.
- Run [#397](https://github.com/jonhararagi/vtuber/actions/runs/38329277301), SHA `3a7bfae8b2f569baa59190bc23070144a2553fd0`: restore correcto, compilación fallida por 10 diagnósticos `xUnit1051`; no se ejecutaron tests ni se publicó artefacto. Se propagó `TestContext.Current.CancellationToken` por los tests asíncronos.
- **Resultado final comprobado después de las correcciones:** Windows CI [run #423](https://github.com/jonhararagi/vtuber/actions/runs/38330331610) corresponde exactamente al SHA `2e3fedc7be40f4ac17bc59dc7c3cbbbd8258cf75`. Restore y compilación Release finalizaron sin advertencias ni errores de compilador; la suite pasó **423/423** (0 fallos, 0 omitidos) y el paso de publicación/archivo terminó correctamente. Artefacto `VTuberBot-Product-Candidate-win-x64-2e3fedc7be40f4ac17bc59dc7c3cbbbd8258cf75`, listado en el mismo run. Esto valida CI para ese SHA, no sustituye pruebas manuales con Streamer.bot/Twitch/OBS/PNGTuber Plus reales.
- Run [#415](https://github.com/jonhararagi/vtuber/actions/runs/38329915472), SHA `0a87f8caa9b883a0d42c700e78ae808ca7aee6fb`: Release build passed; 421/422 tests passed. The single failure was `EventActionCoordinatorTests.RealNormalizedEventSelectsOnlyMatchingSourceAndTypeAndChecksFields`: its assertion expected `ConditionNotMet` after a fixed delay, while the activity log showed the event enqueued but the asynchronous worker had not yet recorded the condition result. The test now asserts successful enqueue and polls for the outcome up to three seconds with the xUnit cancellation token. This is a test-timing correction; only the next exact-SHA run can confirm it resolves the failure.


## 2026-10-13 — Orden 5/5: auditoría final del candidato de producto

- **SHA inicial verificado antes de la auditoría:** `dc55ca9bb68dfbb5ff35c024931e930c34730fe5`. Su [Windows CI #427](https://github.com/jonhararagi/vtuber/actions/runs/38382027364) completó restore, build Release (0 warnings, 0 errors), 423/423 pruebas y publicación del ZIP win-x64. Ese resultado solo valida ese SHA.
- **Hallazgo corregido:** `EventActionCoordinator.LoadRulesSafely` informaba que las automatizaciones estaban pausadas ante reglas persistidas dañadas, pero dependía de que la preferencia global ya tuviera el valor seguro. Si estaba guardada como `false`, una regla inválida se omitía sin forzar la pausa. Ahora se fuerza la pausa en memoria y se intenta persistir `true` tanto ante una regla dañada como ante un documento JSON corrupto. Si falla la persistencia, la pausa sigue activa durante esa sesión y se emite una advertencia.
- **Regresión cubierta:** `tests/VtuberBot.Tests/EventActionCoordinatorTests.cs` ahora inicia explícitamente con la pausa desactivada y comprueba que una regla inválida obliga a pausarla y persistirla; se añadió un caso separado para un documento de reglas JSON corrupto.
- **Commits de implementación:** corrección Core en [`5c9a667`](https://github.com/jonhararagi/vtuber/commit/5c9a6679018071763a949a33274b58d46e649289) y pruebas en [`ad0a48d`](https://github.com/jonhararagi/vtuber/commit/ad0a48d296a435dea9399c20c6e4be900d6a7c32).
- **Documentación de producto:** README, guía de usuario, centro de integraciones y roadmap actualizados para describir la descarga del paquete temporal desde Actions y no reutilizar el CI anterior como evidencia de la corrección nueva.
- **Estado de validación:** el resultado de [Windows CI](https://github.com/jonhararagi/vtuber/actions/workflows/windows-ci.yml?query=branch%3Aproduct%2Fcomplete-vtuber-experience) debe comprobarse para el SHA exacto de la revisión final que incluye esta entrada. No se debe reutilizar #427 como evidencia de esa revisión. La aceptación global también requiere pruebas manuales con Streamer.bot, Twitch, OBS y PNGTuber Plus.
- **Paquete previo:** el artefacto de #427 era `VTuberBot-Product-Candidate-win-x64-dc55ca9bb68dfbb5ff35c024931e930c34730fe5`, 77,991,884 bytes, expiración 2026-10-27 10:16:33 UTC. No es el paquete de la auditoría final.
- **Integridad:** no se modificó `main`, no se fusionó ni cerró el PR #9. El PR #9 se comprobó abierto y sin fusionar al inicio de la auditoría.


### Resultado de Windows CI de la auditoría — SHA de código `0609de270aa045912f487c5117c429bce8a25b30`

- [Windows CI #435](https://github.com/jonhararagi/vtuber/actions/runs/38384321638) finalizó `success` para ese SHA exacto.
- Restore: correcto. Build Release: correcto, **0 advertencias y 0 errores**. Tests: **424 aprobados, 0 fallidos, 0 omitidos**.
- Publicación self-contained win-x64: correcta. El workflow validó el ejecutable `VtuberBot.App.exe`, `BUILD-INFO.txt` y los patrones de archivos prohibidos antes de subir el ZIP.
- Artefacto exacto: `VTuberBot-Product-Candidate-win-x64-0609de270aa045912f487c5117c429bce8a25b30`, 77,992,115 bytes, SHA-256 `107075a6f97ae93bdb7e068382225c82fffcfa2bb3e808f29457ea20713a4c11`, expiración 2026-10-27 10:36:30 UTC.
- Esta evidencia valida compilación, suite automatizada y paquete para el SHA citado; no equivale a prueba manual con Twitch, OBS, Streamer.bot, Telegram/WebView2, micrófono ni PNGTuber Plus real. Los commits documentales posteriores deben validarse mediante el run correspondiente a su propio SHA.


## Continuidad — Bot social, TTS local e IA conversacional (2026-10-13)

**Implementación añadida, aceptación pendiente de CI y pruebas manuales.**

- SHA base inspeccionado antes de esta orden: `04cd8ca0bb07ff9ab3261ee0d1eb55f483abead4`.
- Core: `LocalOpenAiCompatibleProvider` implementa una abstracción `IChatIntelligenceProvider`, comprobación de `/v1/models` y generación mediante `/v1/chat/completions`. Rechaza hosts fuera de loopback; timeout, JSON inválido, modelo ausente, respuesta vacía y errores HTTP se representan como estados, no como texto ficticio.
- App: `SocialBotService` consume mensajes ya validados desde el flujo IRC/TLS existente; usa `LocalSpeechQueue` y `TwitchChatClient`, no crea clientes duplicados. La generación corre en un worker con canal acotado. Se ignoran mensajes de la identidad bot configurada y del canal como fallback. Se aplican filtros TTS, repetición limitada, cooldowns y límite de respuestas/minuto.
- UI: nuevo workspace `Bot social` para lectura de chat, filtros, límites, voz de prueba, parar/vaciar cola, personalidad, endpoint/modelo local, prueba privada, estado del proveedor y cancelación. Publicación a Twitch y voz de respuestas son interruptores separados.
- Seguridad por defecto: TTS desactivado; leer todos los mensajes desactivado; conversación automática desactivada; publicar en Twitch desactivado; voz de respuestas desactivada. El proveedor solo acepta localhost/loopback y no descarga modelos. Activar respuestas automáticas solicita confirmación.
- Pruebas añadidas: `LocalOpenAiCompatibleProviderTests.cs` cubre el protocolo simulado, verificación del modelo, rechazo de URL remota, respuesta vacía/JSON inválido y validación de entradas. Aún no se afirma que la suite completa compile o pase en Windows hasta observar el run correspondiente al SHA final.
- Archivos creados: `src/VtuberBot.Core/LocalOpenAiCompatibleProvider.cs`, `src/VtuberBot.App/SocialBotService.cs`, `src/VtuberBot.App/Workspaces/MainWindow.SocialBot.cs`, `tests/VtuberBot.Tests/LocalOpenAiCompatibleProviderTests.cs`.
- Archivos modificados: `src/VtuberBot.App/MainWindow.xaml.cs`, `src/VtuberBot.App/Workspaces/MainWindow.Twitch.cs`, `docs/EXTERNAL-INTEGRATIONS.md`, `docs/PRODUCT_USER_GUIDE.md`, `ROADMAP.md`.
- Pendiente: inspeccionar CI del SHA final y corregir fallos reales; comprobar artefacto asociado a ese mismo SHA; prueba manual con SAPI/Windows, Twitch OAuth y cuenta bot separada, llama.cpp y un modelo instalado. El estado «generado» no implica «publicado», y el envío aceptado por Twitch no demuestra que el espectador haya oído el audio.


### Evidencia de CI durante esta orden

- Run [#459](https://github.com/jonhararagi/vtuber/actions/runs/38399709040), SHA `127320c4e03a7786a19e5426cdd9525d3b7fe010`: restauración y compilación Release correctas; 433/434 pruebas pasaron. Falló `LocalOpenAiCompatibleProviderTests.ReportsEmptyAndMalformedResponsesInsteadOfInventingText`: el JSON malformado devolvía el estado genérico `Unavailable` en lugar de `InvalidResponse`.
- Corrección aplicada en `LocalOpenAiCompatibleProvider.cs`: se separó el tratamiento de `JsonException` para reportar `InvalidResponse`; también se exige que el ID del modelo exista y se expone el límite de contexto en la UI.
- Se añadieron pruebas `SocialBotServiceTests.cs` para defaults desactivados, disparador por mención/comando, procesamiento asíncrono y prevención del eco propio. La aceptación sigue pendiente del run correspondiente al SHA final tras estas correcciones.

## Corrección de ciclo de vida del bot social (2026-10-13)

- `TwitchChatClient` expone `ConnectionStateChanged` tipado; solo cambia a conectado tras el ACK de JOIN. `SocialBotService` invalida la sesión de conversación y cancela inferencias automáticas al perder una conexión previamente activa.
- `SetAutomaticRepliesEnabled` coordina el estado persistido y el ciclo de vida: al pausar incrementa el epoch, drena la cola acotada y cancela generaciones conversacionales; las tareas antiguas no se reutilizan al reactivar.
- La generación valida de nuevo el epoch/estado tras la inferencia y antes de publicar o encolar voz. La lectura TTS entrante permanece separada de la IA.
- Pruebas deterministas añadidas para cancelación de inferencia bloqueada, descarte de cola pendiente, independencia del TTS entrante, ausencia de voz conversacional tras una pausa, límite/saturación de cola, filtros TTS, recuperación tras fallo del proveedor, cierre del servicio, estado de conexión Twitch simulado, comprobación del proveedor y expiración del cooldown. Los cooldowns se contabilizan tras una respuesta válida, no tras un fallo. La transición simulada no sustituye una prueba de red Twitch real.
- Límite: no se puede retirar de forma garantizada un mensaje que Twitch ya haya aceptado antes de que llegue la solicitud de pausa. Aún requiere ejecución Windows CI para el SHA final y prueba manual de desconexión/reconexión con Twitch real y llama.cpp/GGUF.

## Continuidad — Orden 2/4: reacciones de chat y Centro de directo (2026-10-13)

- HEAD base indicado y verificado: `b0fd5336e189ff11a3e368a559f633e15d9ae276`; trabajo directo en `product/complete-vtuber-experience`.
- Se agregó `ChatReactionService`: motor de reglas persistidas en SQLite, match exacto/palabra completa/frase contenida, normalización de acentos y mayúsculas, respuestas alternativas con `{user}`, cooldown global/usuario, límite por minuto, deduplicación por ID IRC, cola acotada, cancelación/invalidación por pausa y sesión Twitch, historial acotado y registro sin contenido de chat.
- La canalización existente `TwitchChatClient.MessageReceived → HandleTwitchMessage → ProcessTwitchMessageAsync` pasa los mensajes ya validados al servicio de reacciones. No hay un segundo cliente ni suscripción. Los mensajes del login del bot/canal se ignoran para prevenir bucles.
- Se agregó el espacio de trabajo **Centro de directo** con previsualización, control del overlay público, controles privados de expresión, pausa global de reglas, control de TTS independiente, simulación local y editor de reglas. La ventana transparente existente sigue siendo la salida para OBS; no se colocan controles privados dentro de ella.
- Las reglas nuevas nacen desactivadas. No invocan IA. La prueba local no envía chat ni activa efectos. Las respuestas solo activan voz/expresión después de que la escritura IRC haya finalizado; eso no confirma entrega/visualización en Twitch.
- No se implementa agente de videojuegos y no se eliminan minijuegos existentes.
- Pendiente de validar: CI Windows del SHA final; ejecución manual con Twitch real, OBS y voz SAPI; verificar sprites en un perfil real; confirmar UX del editor con teclado/escala DPI.

## Validación Windows — Orden 2/4 (2026-10-13)

- SHA probado por Windows CI: `188e8d208879822eda5d4ae6a53ca284172c29b7`.
- Run: https://github.com/jonhararagi/vtuber/actions/runs/38415049993 — restauración, compilación Release, pruebas, publicación win-x64 y subida del artefacto completadas.
- Pruebas según log: 471 aprobadas, 0 fallidas, 0 omitidas.
- Artefacto: `VTuberBot-Product-Candidate-win-x64-188e8d208879822eda5d4ae6a53ca284172c29b7`, 78,037,734 bytes, SHA-256 `eb9690b05a2d35cd9243e0ec85cc9cfd8e44365c90f4a4eec20d61fbe9ee7c19`, expira el 2026-10-27.
- Esta ejecución corresponde exactamente a ese SHA; los commits documentales posteriores requieren su propia CI. La prueba automatizada no sustituye una prueba manual con Twitch autenticado, OBS, Windows SAPI ni un GGUF real.


## Orden 3/4 — ChatSpeak Studio (2026-10-13)

Objetivo: quitar la inferencia pesada del PC de referencia y mantener una experiencia VTuber con voz remota, controles locales pequeños y límites seguros.

- La ruta automática usa una API remota HTTPS compatible con Chat Completions; Gemini OpenAI-compatible es la configuración inicial. La implementación local previa permanece, elegible y estrictamente loopback.
- La salida de voz puede usar Gemini TTS (REST Interactions, WAV) o TTS OpenAI-compatible (MP3). Ambas opciones pasan por la cola de voz actual y sus límites/cancelación. TTS SAPI local sigue disponible como alternativa.
- Modo manual: abrir ChatGPT, copiar prompt/persona, pegar respuesta en VTuberBot. No se intenta leer el contenido de la página web, cookies o sesión privada. La suscripción web no equivale a API.
- Credenciales API: guardado DPAPI vinculado al usuario actual de Windows, fuera de SQLite y los logs.
- La IA puede solicitar exclusivamente /angri, /happy, /surprised, /idle, /talking, /blink y /left eye close. Las directivas válidas no se pronuncian ni se publican en Twitch; desconocidas se descartan. Controles de creador quedan en UI manual.
- Comentario periódico: buffer limitado a 60 entradas en memoria, intervalo configurable entre 1 y 60 minutos (5 inicial), filtros de enlaces/comandos y cancelación al apagar función, detener bot o desconectar Twitch.
- Estados Angry y LeftEyeClose ya están previstos en validación, configuración de sprites y el overlay. En perfiles sin imagen asignada, el control informa de que no puede aplicar el estado.
- Windows CI del commit de implementación cf19663380c914abaa07b7dd7ed5a3163293723b completada: Release build con 0 warnings/errores, 503/503 pruebas pasadas (0 omitidas), publish self-contained win-x64 correcto y artefacto 11833843141 disponible en el run 38438046595. Sigue pendiente probar una clave válida de Google AI Studio, escuchar el audio real y verificar el overlay en OBS; CI no acredita esos flujos externos.


## 2026-10-13 — Voz chillona, interfaz guiada y destinos Twitch/YouTube

### Cambios verificados en la rama de producto

- **Voz:** el preset remoto predeterminado pasó a `Zephyr` y sus instrucciones piden deliberadamente una voz femenina en español muy aguda/chillona, brillante, juguetona, nasal ligera y con expresividad caricaturesca. Solo se migran automáticamente los valores que coinciden exactamente con el preset anterior Leda/instrucciones predeterminadas; preferencias personalizadas se conservan. La percepción de tono depende del proveedor/modelo y requiere escuchar la prueba real.
- **Interfaz ChatSpeak:** la página se reorganizó en pestañas `Empezar aquí`, `Voz y audio`, `Leer el chat`, `Bot automático`, `Chat manual` y `Pruebas y estado`, con un botón global `Guardar toda la configuración`. La navegación lateral se agrupa por tareas para reducir el número de opciones expuestas al comenzar; el resaltado de la página activa recorre los botones anidados.
- **Destinos de streaming:** `ObsStreamingDestinationPolicy` normaliza solo Twitch/YouTube, asigna servidores RTMPS distintos, nombres de secreto separados y comprueba longitud/formato de la clave. `ObsWebSocketClient.ConfigureStreamDestinationAsync` envía `SetStreamServiceSettings` al OBS autenticado y se niega a cambiar destino si OBS está transmitiendo. En el panel OBS, las claves se guardan en ficheros protegidos por DPAPI mediante `UserSecretStore`, fuera de SQLite y de logs. Seleccionar el destino no inicia la emisión: primero se configura y luego se inicia desde los controles de OBS.
- **Emisión simultánea:** no se ha implementado un cliente de doble salida dentro de VTuberBot. OBS principal transmite a un destino; emitir a Twitch y YouTube a la vez requiere configurar por separado un complemento multi-RTMP o un servicio externo. La página y `docs/STREAMING-YOUTUBE-TWITCH.md` explican esta dependencia y advierten que el complemento no se instala ni se considera validado por el bot.
- **Documentación:** README, guía de usuario, ChatSpeak Studio, centro de integraciones, roadmap y blueprint describen estos cambios y sus límites.

### Tests y evidencia

- Se añadió `tests/VtuberBot.Tests/ObsStreamingDestinationPolicyTests.cs` para destinos RTMPS de Twitch/YouTube, normalización, nombres separados de secretos, entradas desconocidas, claves vacías, espacios y claves demasiado largas.
- **No atribuir el CI verde anterior a este cambio final.** Windows CI #550 validó el commit `a4cf0575b443d5bbdd5a11ca92018774eb1efca5` (build Release, pruebas y ZIP de 74.5 MB); ese SHA antecede a la implementación de destinos de streaming y a sus pruebas nuevas. El último commit de código de esta orden es `70ac4ed9a9705e1fdc4c1b499b39827b9121b10a`. En el momento de registrar esta nota, las consultas disponibles no devolvieron una ejecución de Windows CI vinculada de forma demostrable a ese SHA exacto. Por lo tanto, **la compilación/tests finales quedan PENDIENTES de evidencia CI sobre el commit actual**.
- Pendiente de validación manual en Windows: escuchar realmente la voz remota, confirmar las credenciales/cuota del TTS; autenticar OBS WebSocket; configurar destinos con claves válidas sin mostrarlas en capturas/logs; hacer una emisión de prueba privada/no listada en cada plataforma y verificar audio/escena/destino. La emisión simultánea queda pendiente además de comprobar compatibilidad del plugin local y sus dos salidas.

## 2026-10-13 — Auditoría de ramas (sin borrado)

Ramas encontradas mediante búsqueda paginada en el repo: `main`, `product/complete-vtuber-experience`, `v0.2.3/stabilization-gate`, `v0.2.2/sqlite-concurrency-hardening`, `v0.2.1/performance-health`, `v0.2.0/control-plane-command-core`, `fix/scheduler-concurrent-shutdown`, `chore/canonical-repository-identity`, `migration/csharp-net10-wpf` y `assets/marmalade-cc0-static-avatar-sample`.

- Mantener `main` y `product/complete-vtuber-experience`.
- Mantener `v0.2.3/stabilization-gate` porque es la rama de PR #9 que se verificó abierta; no cerrar ni fusionar ese PR como parte de la limpieza.
- `v0.2.0/control-plane-command-core`, `v0.2.1/performance-health`, `v0.2.2/sqlite-concurrency-hardening` y `migration/csharp-net10-wpf` son líneas históricas cuyo código queda superado por main/product en las comparaciones realizadas.
- `fix/scheduler-concurrent-shutdown` contiene el mismo arreglo actual de `Scheduling.cs` y las pruebas de shutdown ya aparecen también en `product/complete-vtuber-experience`; no mostró una corrección de código exclusiva pendiente de portar.
- `chore/canonical-repository-identity` difiere solo en el contenido histórico de `PROMPTS.md`; la rama de producto ya tiene la identidad/canon actualizada.
- **No borrar a ciegas** `assets/marmalade-cc0-static-avatar-sample`: contiene PNGs y avisos CC0 que no figuran en la rama de producto. Son assets de muestra no integrados en el runtime; si se retira la rama, los recursos únicos se perderán del alcance visible del repo salvo que se archiven antes. No se copian automáticamente al paquete de distribución.
- **La eliminación remota no se completó:** la acción GitHub conectada disponible para ramas permite buscar, crear y mover refs, pero no expone eliminación de refs/branches. La herramienta de navegador disponible informó falta de créditos. No se usó una maniobra insegura para simular borrado. Se necesita ejecutar la limpieza desde la UI de GitHub o conceder una acción/API que permita borrar ramas después de revisar cualquier PR que las use.

Esta tabla es un análisis de ramas, no una afirmación de que las ramas enumeradas ya estén borradas. No se modificó `main` ni se fusionó/cerró el PR #9.

## Ampliación del armario modular y configurador de color (13/10/2026)

- `src/VtuberBot.App/ModularWardrobeFactory.cs`: catálogo ampliado con 11 peinados, 15 conjuntos, 14 accesorios de cabeza, 9 faciales/de joyería y 9 piezas traseras. Incluye el helper `TintPng` para producir PNG teñidos preservando transparencia y procurando conservar contornos oscuros y brillos.
- `src/VtuberBot.App/Workspaces/MainWindow.AvatarStudio.cs`: panel de HEX/muestras por cinco zonas (cabello, ropa, cabeza, cara/joyería, espalda). Los colores se aplican al crear un perfil modular nuevo; no sobrescriben el catálogo original ni los perfiles previos.
- `docs/VTUBER_REFERENCIA_Y_BIBLIOTECA_MODULAR.md`: enlaces a VRoid Studio, Krita, OpenGameArt, itch.io, Live2D Cubism y BOOTH, con límites de compatibilidad/licencias. No se descargaron ni incluyeron automáticamente assets externos; debe verificarse licencia por recurso antes de redistribuir.
- `docs/PRODUCT_USER_GUIDE.md`: instrucciones del configurador.
- Commits de esta ampliación de código: `19f5c1cc27b4091b5f14a6fee2469baa56139c48`, `9fb532347571d2774139682f5adcddfc8d49faff`, `5181e35b81501ec24e90dc86811347c0f624a73f`, `a33ccb26625f4b730e078510c1e12b750f450357`.
- Validación de Windows confirmada: CI #584, run [38451299853](https://github.com/jonhararagi/vtuber/actions/runs/38451299853), sobre el commit de código/interfaz `a33ccb26625f4b730e078510c1e12b750f450357`: build correcto y pruebas automatizadas correctas. El commit de código anterior `5181e35b81501ec24e90dc86811347c0f624a73f` también pasó en CI #581.
- Artifact verificado: [VTuberBot Product Candidate win-x64](https://github.com/jonhararagi/vtuber/actions/runs/38451299853/artifacts/11841275624), id `11841275624`, SHA-256 `ee4556487385396887993ecffc172cc98cb6f1cc0e6d214d8a22b3302114be5d`, creado el 13/10/2026 y con vencimiento el 27/10/2026. La modificación posterior fue documental, por lo que el binario corresponde al mismo código de aplicación.
- Sigue pendiente la comprobación visual manual en el PC del usuario: solapamiento real de pelo/ropa/accesorios y calidad de cada combinación. El CI no valida el aspecto visual en pantalla.

## Asistente simplificado «Crear una VTuber desde una sola imagen» (13/10/2026)

- En `src/VtuberBot.App/Workspaces/MainWindow.AvatarStudio.cs` se añadió una sección de dos botones: abrir la demo de separación See-through y abrir Anime2.5DRig para animar el PSD generado. Los enlaces se abren en el navegador predeterminado; no se intenta subir automáticamente la imagen.
- Fuentes: [See-through demo de Hugging Face](https://huggingface.co/spaces/24yearsold/see-through-demo), [repositorio See-through](https://github.com/shitagaki-lab/see-through), [Anime2.5DRig](https://852wa.github.io/Anime2.5DRig/).
- Según el repositorio de See-through, la descomposición semántica puede producir hasta 23 capas y PSD; la demo online puede limitar ejecuciones gratuitas. El pipeline local completo ronda 12–16 GB de VRAM a 1280px, demasiado exigente para el equipo Ryzen 5 5600G con gráfica integrada, por lo que el camino recomendado es el online.
- Advertencia: la imagen debe subirse manualmente al sitio externo. La reconstrucción de partes ocultas es una estimación y puede necesitar retoques. No es un rig Live2D profesional.
- Guía para usuarios actualizada en `docs/PRODUCT_USER_GUIDE.md`; enlaces y limitaciones agregados a `docs/VTUBER_REFERENCIA_Y_BIBLIOTECA_MODULAR.md`.
- Este asistente es un acceso de flujo de trabajo a herramientas externas, no una implementación integrada del modelo ni un importador PSD dentro del overlay VTuberBot. No afirmar que procesa una imagen dentro de la aplicación hasta implementar y validar ese pipeline completo.
- Pendiente verificar CI de Windows del nuevo botón y documentación; revisión visual manual en el PC del usuario aún pendiente.

## Simplificación de Avatar Studio para principiantes (13/10/2026)

- Reorganizado `src/VtuberBot.App/Workspaces/MainWindow.AvatarStudio.cs`: tarjeta principal «Empezá por acá — elegí tu tipo de VTuber», con tres rutas claras.
  - **PNGTuber recomendado:** activar el ejemplo incluido o crear un perfil desde imagen propia. Permite elegir sprite de reposo y opcionalmente una segunda imagen con la boca abierta; si solo se aporta una imagen, se aclara que será estática al hablar.
  - **2.5D con IA local:** utiliza el separador experimental de piel/ropa/cabello creado en commits anteriores; CPU, un hilo de ONNX, prioridad baja, proceso hijo, límite de memoria de 2 GiB aplicado por Windows Job Object, límite de ejecución de 10 minutos, cancelación y salida PNG por capas. Si Windows no permite imponer el límite, no se inicia el trabajo.
  - **3D con VRoid:** botón abre VRoid Studio externo; el modelo VRM aún no se importa al overlay de VTuberBot. La interfaz lo explica para no prometer compatibilidad todavía.
- Los controles de administración de perfiles ahora están plegados bajo «Opciones avanzadas de perfil». La biblioteca de ropa/colores y las herramientas online están bajo «Personalización avanzada». La tarjeta de inicio mantiene visibles el progreso y la cancelación del procesamiento local.
- Guía para principiantes actualizada en `docs/PRODUCT_USER_GUIDE.md`. El objetivo de producto elegido es **PNGTuber primero** para una primera experiencia sencilla y de bajo consumo; 2.5D es modo experimental opcional; 3D requiere una fase posterior de runtime VRM/rig.
- Validación previa: la compilación de Windows de la versión del worker sin la nueva interfaz pasó CI #602, pero los commits de UX deben confirmarse por la nueva ejecución de CI antes de distribuir el ZIP. El proceso de IA no se ha medido todavía en el PC del usuario; el límite de 2 GiB es la restricción del proceso hijo, no de VTuberBot + OBS + Windows en conjunto. La precisión semántica en arte anime es baja/experimental según el autor del modelo.
- Modelo tercero: ONNX de Skin-Clothes-Hair-Segmentation-using-SMP con licencia MIT y verificación SHA/tamaño, descrito en `docs/THIRD_PARTY_NOTICES.md`.



## Entrega funcional — ventana ChatGPT, voz local y galería chibi (14/10/2026)

- **Ventana ChatGPT:** `src/VtuberBot.App/ChatGptMiniWindow.cs` aloja el sitio oficial en una ventana WPF con WebView2, perfil persistente local, navegación atrás/adelante/recargar y apertura externa. No extrae ni automatiza conversaciones privadas y no se presenta como una conexión por API.
- **Voz local:** la cola hablada usa Windows SAPI y las voces instaladas; no requiere clave para esa voz local. La voz remota de personaje es opcional y depende de un proveedor/API.
- **Galería inicial renovada:** los botones violeta/azul/rosa —que compartían una ilustración recoloreada— ya no se presentan como ejemplos principales. Ahora la primera tarjeta muestra seis composiciones chibi distintas: **Neko gamer, Brujita lunar, Idol estelar, Gótica rosa, Cyber neón y Streamer casual**.
- **Personalización modular:** cada preajuste combina distintas formas de peinado, ropa, accesorios, forma facial, estilo/color de ojos, tono de piel y gesto/pose. Al elegir una tarjeta se crea un perfil nuevo; no sobrescribe los anteriores.
- **Creador visible con vista previa en vivo:** el panel permite escoger 11 peinados, 15 conjuntos, 4 formas de rostro, 7 estilos de ojos, 4 tonos de piel, 6 gestos/poses de brazos y accesorios de cabeza/cara/espalda. La composición del preview cambia al elegir otras piezas antes de guardar el perfil. Las partes se guardan como PNG transparentes.
- **Alcance visual, expresado con precisión:** el catálogo se dibuja localmente mediante trazados vectoriales originales del programa; es arte chibi 2D estilizado, no ilustración raster profesional importada, rig Live2D, cuerpo 3D ni tracking facial. Las opciones de pose cambian principalmente gestos de brazos; el cuerpo base no tiene animación ósea.
- **Archivos:** `src/VtuberBot.App/ModularWardrobeFactory.cs` genera las piezas; `src/VtuberBot.App/Workspaces/MainWindow.AvatarStudio.cs` define las seis tarjetas, el compositor de preview y la creación/edición de perfiles; `docs/PRODUCT_USER_GUIDE.md` describe el recorrido de usuario.
- **Rama y cambios:** el trabajo queda en `product/complete-vtuber-experience`, HEAD de código validado `e88e98870cfc91a520890214ccc5aa7bf5a9ae70`. `main` se devolvió al HEAD previo a esta tarea (`000f3b639f288b649bbac25a6d146c6becfe0cb5)); PR #9 ya figura cerrado y fusionado en GitHub; esta tarea no modificó su estado.
- **Validación Windows exacta:** [run #38515515811](https://github.com/jonhararagi/vtuber/actions/runs/38515515811) terminó correctamente en el SHA `e88e98870cfc91a520890214ccc5aa7bf5a9ae70`: compilación Release, pruebas automáticas, empaquetado Windows x64 y subida del artefacto `VTuberBot-Product-Candidate-win-x64-e88e98870cfc91a520890214ccc5aa7bf5a9ae70`, disponible hasta el 28 de octubre de 2026.
- **Pendiente manual:** verificar en Windows que las seis tarjetas se vean correctamente, que la vista previa responda a cambios de cada selector, que la capa de pose se integre bien con mangas/ropa y que el overlay conserve transparencia y orden de capas. No se ha realizado prueba manual en el equipo del usuario.


## Auditoría de integración de voz, recursos de arte y fiabilidad (14/10/2026)

- Hallazgo: LocalSpeechQueue modificaba su estado interno, pero no informaba al Avatar Overlay de los cambios de habla sintética. El cambio añade PlaybackStateChanged, combina micrófono y TTS mediante AvatarTalkStatePolicy, y mantiene Talking entre frases consecutivas. Se añaden pruebas para transiciones de voz y errores simultáneos de TTS/limpieza. No es lip-sync por fonemas.
- ChatGPT web: la ventana WebView2 está implementada y no extrae conversaciones privadas. El modo bot usa un proveedor de IA separado, local o remoto. Las respuestas habladas usan la configuración TTS.
- Arte web: Avatar Studio enlaza Humation (https://apps.humation.app/humation; el repositorio declara licencia MIT para código y arte: https://github.com/humation-labs/humation), Open Peeps Studio (https://peepstudio.msgenie.art/; ilustraciones Open Peeps CC0: https://www.openpeeps.com/) y Chibi Maker (https://squarefaceicongenerator.co/pricing/chibi-maker). Son herramientas externas; no se consideran arte empaquetado ni un rig automático.
- Regla Murphy: se crea docs/REGLA_MURPHY_Y_PUERTAS_DE_FIABILIDAD.md, un proceso verificable de reproducción, regresión, corrección de estado, CI del SHA exacto y validación manual Windows/OBS. No se afirma que VTuberBot se auto-repare ante cualquier bug desconocido.
- Validación de esta revisión: pendiente hasta que Windows CI acabe para el nuevo SHA. El candidato anterior de la rama de producto que pasó CI era 0e2d99ade5c7c7f44c63a967d3989f32f3cf2be6; su resultado no valida estos cambios nuevos.


## Cierre técnico del candidato de producto — 2026-10-14

- **Rama de código revisada:** `product/complete-vtuber-experience`.
- **Corrección de compilación:** commit [`d336f0040a3b3032fde39381a5ef8cec6319a237`](https://github.com/jonhararagi/vtuber/commit/d336f0040a3b3032fde39381a5ef8cec6319a237). Se sustituyeron tres llamadas inválidas de tres argumentos a `SpinWait.SpinUntil` por la sobrecarga válida de dos argumentos, comprobando cancelación dentro del predicado mediante `TestContext.Current.CancellationToken.ThrowIfCancellationRequested()`.
- **Errores anteriores inspeccionados:** [CI #627](https://github.com/jonhararagi/vtuber/actions/runs/38520985811) falló por xUnit1051; [CI #628](https://github.com/jonhararagi/vtuber/actions/runs/38525799452) falló con tres CS1501 en las llamadas a `SpinWait.SpinUntil`. [CI #629](https://github.com/jonhararagi/vtuber/actions/runs/38526431105) volvió a fallar con xUnit1051 antes de la corrección actual.
- **Windows CI de la SHA exacta d336f004:** [ejecución push #630](https://github.com/jonhararagi/vtuber/actions/runs/38560968924), SUCCESS. Restore, build Release, pruebas, publicación self-contained win-x64 y subida de artefacto terminaron correctamente. Resultado informado por el runner: **519 pruebas totales, 0 fallidas, 0 omitidas**; el smoke test WPF está incluido en esa suite. La ejecución de PR [#631](https://github.com/jonhararagi/vtuber/actions/runs/38560976041) también terminó correctamente sobre el candidato de integración.
- **Artefacto correspondiente a d336f004:** [ZIP Windows x64](https://github.com/jonhararagi/vtuber/actions/runs/38560968924/artifacts/11891629783), nombre `VTuberBot-Product-Candidate-win-x64-d336f0040a3b3032fde39381a5ef8cec6319a237`; tamaño del artefacto en GitHub: 84,636,025 bytes; digest `sha256:b30c874548279dd503750f946b518482794deb4758926e279f5eb815b23f4872`; creado 2026-10-14 12:05:44 UTC; caduca 2026-10-28 12:05:41 UTC. El pipeline comprobó 490 entradas del ZIP, incluida `VtuberBot.App.exe` y `BUILD-INFO.txt`, y rechazó nombres de archivos de datos/secrets prohibidos. El ZIP de aplicación que el job validó mide 84,892,569 bytes.
- **Auditoría de alcance basada en código/documentación:** la solución sigue siendo C#/.NET 10/WPF/SQLite; Core no depende de WPF. Twitch tiene OAuth, cliente IRC, procesamiento de mensajes y EventSub, pero no se ha validado contra una cuenta real en esta tarea. Comandos, puntos, sorteos, moderación y automatización tienen rutas locales/persistencia; la prueba de servicios externos sigue pendiente. La IA conversacional es opcional y admite servidor local compatible con OpenAI; proveedor remoto y TTS remoto son opcionales. SAPI local, cola acotada y sincronización de estado Talking/Idle están implementados y cubiertos por pruebas, pero no se probó salida de voz física en este cierre. Avatar Studio tiene perfiles chibi vectoriales, piezas de pelo/ropa/rostro/ojos/pose/accesorios y tintes por zona; es arte 2D por capas, no Live2D ni tracking facial. OBS WebSocket v5 y Streamer.bot WebSocket tienen clientes/protocolo y pruebas de protocolo simuladas; falta probarlos con programas reales. PNGTuber Plus se configura/detecta como proceso y puede recibir hotkeys con autorización, pero eso no prueba que una expresión externa cambie. Telegram Web usa WebView2 con fallback al navegador. No existe adaptador autónomo que observe un videojuego, decida y ejecute acciones: los minijuegos son del chat, no un agente que juegue videojuegos.
- **Validación manual pendiente:** prueba visual interactiva del WPF/Avatar Studio y transparencia del overlay en Windows; micrófono y SAPI reales; OAuth/IRC/EventSub con Twitch; conexión/acciones de bajo riesgo en OBS y Streamer.bot reales; interacción de PNGTuber Plus; persistencia de Telegram Web y sus cookies en el perfil WebView2. Ninguna de esas pruebas se declara completada por CI.
- **Estado de integración al registrar este bloque:** el PR #13 debe seguirse por su página y solo puede considerarse integrado cuando GitHub informe `merged=true` y `main` tenga su SHA resultante. La CI de `d336f004` valida esa revisión; la validación post-merge de `main` debe registrarse aparte cuando exista.


## 2026-10-14 — Comandos anime bilingües para reacciones de chat

- **Rama:** `product/complete-vtuber-experience`. No modificar `main` ni cerrar/fusionar el PR #9.
- **Cambio de código:** `src/VtuberBot.App/ChatReactionService.cs` inicializa las reglas en instalaciones nuevas y ejecuta una migración de una sola vez para incorporar los comandos bilingües a configuraciones existentes, conservando las reglas personalizadas y sin volver a reinsertarlas después de que el usuario las elimine. Incluye `/ohayo`, `/baka`, `/nani`, `/okaerinasai`, `/soybot` y los alias compatibles con chat `!ohayo`, `!baka`, `!nani`, `!okaerinasai`, `!soybot`. Las cuatro primeras son reacciones cortas en japonés; `/soybot` aporta la frase de personalidad en español «Yo... soy solo un bot... jeje». Las respuestas conversacionales más largas deben seguir en español. Las reglas activan voz o clip configurado y solicitan expresiones Happy/Surprised dentro del motor de reacciones ya existente. Todas conservan cooldowns y límites del servicio.
- **Persistencia:** las reglas se guardan en SQLite; las instalaciones existentes que ya tengan una configuración guardada no se sobrescriben. El usuario puede editar/desactivar las reglas desde Reacciones.
- **Pruebas añadidas:** `tests/VtuberBot.Tests/ChatReactionServiceTests.cs` comprueba las diez reglas iniciales, sus textos/expresiones y persistencia; el test que exige una colección vacía configura explícitamente `[]`.
- **Documentación:** `docs/PRODUCT_USER_GUIDE.md` explica los comandos, los alias `!` y las limitaciones de voz/cliente.
- **Historial:** los cambios de código, pruebas, guía y continuidad se guardaron como commits separados en esta rama. Verificar los SHA directamente en el historial de `product/complete-vtuber-experience`; no reutilizar SHA de `main` ni asumir que una ejecución de CI corresponde a estos cambios sin comparar el HEAD exacto.
- **Validación:** no se ha ejecutado aquí Windows CI ni se ha probado una sesión real de Twitch, SAPI o avatar. No marcar como validado hasta que una ejecución de CI corresponda al HEAD exacto y las integraciones se prueben en Windows.
- **Límites importantes:** los mensajes `/` pueden ser interceptados por el cliente de Twitch; usar `!` si no llegan a la canalización IRC. El texto japonés no instala una voz japonesa; la pronunciación depende de las voces SAPI del sistema o de clips importados. La expresión solo cambia si existe un adaptador de avatar conectado que admita esa expresión.


## 2026-10-14 — Configuración de voz e clips por reacción

- **Rama:** `product/complete-vtuber-experience`. No cambiar `main` ni el estado del PR #9.
- **Código:** `ChatReactionRule` ahora persiste `Language` (`auto`, `es`, `ja`), `VoiceId` y `ClipAssetId` junto con la frase, expresión y cooldowns. Los diez comandos iniciales marcan japonés para `/ohayo`, `/baka`, `/nani`, `/okaerinasai` y sus alias; `/soybot` y su alias usan español.
- **Editor:** `MainWindow.LiveCenter.cs` permite elegir idioma, voz SAPI instalada y un clip de la biblioteca local. Un clip configurado sustituye al TTS para esa regla. La reproducción valida el ID contra la biblioteca SQLite y vuelve a validar la ruta local bajo la carpeta de sonidos; no ejecuta rutas arbitrarias guardadas en una regla.
- **Voz japonesa:** el callback en `MainWindow.xaml.cs` exige proveedor local cuando una regla selecciona un ID SAPI concreto, y además exige una voz SAPI identificable como japonesa para las reglas `ja`; si no, registra el motivo y no encola una voz alternativa como si fuera japonesa. Un clip japonés importado es la alternativa explícita. Las voces remotas no se presentan como voces japonesas verificadas.
- **Cola:** `LocalSpeechQueue` expone el estado de reproducción y `GatedSpeechSession` comparte un `SemaphoreSlim` entre TTS y clips. El reproductor de clips espera a que la cola TTS quede libre (límite de 90 segundos), toma el mismo turno de audio y lo libera al terminar; el TTS que llegue mientras el clip suena espera detrás de él. Si la cola no se libera o la reacción se cancela, el clip se omite. La reproducción de clips usa el reproductor local de WAV/MP3 ya existente; no se afirma que este cambio enrute por sí mismo el audio a OBS.
- **Pruebas añadidas/actualizadas:** `ChatReactionServiceTests.cs` verifica persistencia/validación de idioma, voz y clip, idiomas predeterminados y que una regla con clip llama al reproductor de clips en lugar de encolar TTS. El callback de clip está simulado en pruebas; esto no valida reproducción física de audio.
- **Guía:** `docs/PRODUCT_USER_GUIDE.md` describe el editor, la biblioteca de clips y los requisitos para una voz japonesa.
- **CI observado:** Windows CI [run 38625932101](https://github.com/jonhararagi/vtuber/actions/runs/38625932101) para SHA `37f23cff4123f7c93cd2575a181f2176b997ff6d` terminó con `conclusion=success`, completando Restore, Build Release, las pruebas automatizadas y la publicación del artefacto `VTuberBot-Product-Candidate-win-x64-37f23cff4123f7c93cd2575a181f2176b997ff6d`. La validación incluye la configuración por regla y el turno de audio compartido. Después se añadió la comprobación de proveedor local para voces SAPI específicas en `9d30ff6cad9ccc9055eb4fca1192df128f4b7ef7`; Windows CI [run 38626195005](https://github.com/jonhararagi/vtuber/actions/runs/38626195005) para HEAD `f6cae6b3fae345fbaaa82a82680703f44a94d0cf` y [run 38626341460](https://github.com/jonhararagi/vtuber/actions/runs/38626341460) para el HEAD `cf9249bcd3274d858743cfbab5e1700bcde1b5d0` siguen en curso. La reproducción física, la voz SAPI japonesa del usuario, Twitch real y la captura de audio en OBS siguen pendientes.


- **Ajuste de idioma de las frases predeterminadas:** las cuatro reacciones anime usan texto japonés corto para evitar pasar una oración española entera a una voz japonesa. `/soybot` conserva el texto español.
- **Validación pendiente:** la voz SAPI japonesa no se ha probado en el equipo del usuario. El código solo acepta voz SAPI local identificable como japonesa o clip explícito; el proveedor remoto no se presenta como voz japonesa. La prueba automatizada valida la selección del callback y no el audio físico.
