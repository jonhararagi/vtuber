> **Alcance vigente — 2026-10-16:** este documento contiene requisitos históricos y queda subordinado a [`docs/ESPECIFICACION_DEFINITIVA.md`](docs/ESPECIFICACION_DEFINITIVA.md). El candidato actual prioriza Windows/WPF, SQLite, edición de la imagen propia, encuadre/exportación, solo los estados de avatar reposo/hablando, operaciones reales compatibles de OBS y voz local VOICEVOX. Quedan fuera del alcance aprobado la conexión directa de Twitch y sus funciones sociales, Telegram Web, Streamer.bot y PNGTuber Plus como dependencia, la generación prefabricada de personajes como flujo principal y el parallax/2.5D. No se debe borrar código histórico sin revisar dependencias ni declarar una función completa por la sola presencia de UI o documentación.

---

# PROJECT SPEC — VTuberBot producto completo

**Objetivo:** crear una aplicación de escritorio Windows para operar un canal VTuber, no solamente una demo del núcleo técnico.

**Equipo objetivo:** Ryzen 5 5600G, 16 GB RAM, Windows y OBS Studio.
**Base tecnológica mantenida:** C# / .NET 10 / WPF / SQLite.
**Especificación de producto y arquitectura completa:** [docs/PRODUCT_BLUEPRINT.md](docs/PRODUCT_BLUEPRINT.md).

## 1. Principios obligatorios

- Producto local-first, gratuito en su funcionamiento básico y sin dependencia obligatoria de APIs de IA pagas.
- GPT/Gemini son herramientas de desarrollo, no motores de ejecución del producto.
- SQLite con migraciones y repositorios; sin EF Core.
- No secretos, tokens, cookies, bases de usuario ni logs en Git o en paquetes.
- No cerrar procesos del usuario ni cambiar configuraciones críticas de Windows sin permiso explícito.
- Toda integración externa debe ir detrás de un adaptador reemplazable y poder permanecer desconectada sin bloquear el resto de la app.
- No mostrar estados simulados como si fueran conexiones reales.
- No entregar una interfaz compuesta por botones muertos, textos técnicos o paneles sin función.
- Evitar informes al usuario tras cada ajuste menor. El implementador avanza de forma autónoma por bloques grandes y solicita una validación manual global al tener un candidato integrado.
- CI/tests se pueden ejecutar durante el desarrollo para detectar regresiones, pero el trabajo no se considera terminado por el mero hecho de que compile.

## 2. Experiencia de usuario

Una aplicación oscura, moderna y pulida, adecuada para creadores de contenido, con navegación lateral, encabezado contextual, dashboard útil, cards coherentes, iconografía consistente y espacios de trabajo reales. El Avatar Studio y la previsualización del avatar deben ser protagonistas visuales.

Páginas requeridas:
1. Dashboard
2. Avatar Studio + overlay transparente
3. Twitch + chat
4. Comandos
5. Comunidad (usuarios, puntos, rankings)
6. Sorteos
7. Minijuegos
8. Moderación y automatizaciones
9. Voz y audio
10. OBS
11. Telegram Web
12. Actividad y diagnósticos
13. Ajustes

Diseño visual completo y contratos descritos en [docs/PRODUCT_BLUEPRINT.md](docs/PRODUCT_BLUEPRINT.md). No dejar botones de navegación sin acción ni sustituir funcionalidades por placeholders permanentes.

## 3. Funciones de producto

- Gestión de avatar PNG por capas/estados; preview, parpadeo, boca, expresiones, respiración y movimiento 2.5D ligero; overlay transparente independiente.
- Entrada de micrófono, medición, detección local de actividad de voz, cola de audio, sonidos y TTS local.
- Conexión de Twitch mediante mecanismos oficiales gratuitos, autenticación OAuth, chat, eventos soportados, reconexión, comandos y estados de permiso; no servicios de IA remotos obligatorios.
- Un CommandRouter y pipeline de eventos comunes para controles locales y eventos de Twitch.
- Usuarios, puntos, rankings, perfiles e historial persistentes.
- Sorteos trazables, minijuegos utilizables, moderación configurable, timers y mensajes automáticos anti-spam.
- Integración opcional con OBS, con overlay/captura y control local autenticado cuando se habilite.
- Telegram Web integrado vía WebView2 con alternativa de abrir navegador.
- Rendimiento/health, ajustes, logs, diagnóstico, importación/exportación y configuración segura.

## 4. Aclaración sobre APIs

La prohibición de APIs aplica a proveedores de IA pagos/obligatorios. Un bot real que lee chat, responde y recibe eventos de Twitch necesita conectarse a Twitch mediante sus protocolos oficiales y autenticación. Ese uso debe ser gratuito para operar con los límites/condiciones de Twitch, documentado y desactivable. No afirmar que existe una integración real si no tiene conexión autenticada.

La voz prioriza motores locales. OBS y Telegram son opcionales: la app se inicia y el resto de módulos sigue funcionando cuando no estén disponibles.

## 5. Arquitectura y directorios

La separación objetivo (UI, Core, Infrastructure, Avatar, Audio, Twitch, Community, Interaction, OBS y tests) se especifica en docs/PRODUCT_BLUEPRINT.md.

Reglas:
- Core no depende de WPF ni de adaptadores externos.
- Infrastructure encapsula SQLite, configuración, logs, secretos y archivos.
- Los módulos de funciones se comunican mediante contratos/eventos; evitar ciclos de dependencias.
- App es el composition root, navegación y ViewModels; las páginas no acceden directamente a SQL ni sockets.
- Reutilizar el código actual que sirva y migrar gradualmente; no reescribir todo sin revisar.
- Datos en %LocalAppData%\VtuberBot\ o ubicación elegida por el usuario; no dentro del ZIP de aplicación.

## 6. Flujo de eventos

Adaptador (Twitch/UI/Audio/Timer) → validación → evento tipado → EventManager/router → comando/regla/caso de uso → SQLite y salidas de avatar/audio/OBS → actividad/diagnóstico.

La lógica se puede ejecutar y probar localmente aunque Twitch/OBS/Telegram estén desconectados.

## 7. Desarrollo y aceptación

El trabajo se ejecuta por grandes bloques integrados sin pedir aprobación para cada detalle rutinario:
- Estructura y diseño UX completos.
- Avatar + audio local.
- Twitch + comandos.
- Comunidad, sorteos, minijuegos, moderación y automatización.
- OBS, Telegram, ajustes, diagnósticos y empaquetado.
- Validación global al completar el producto integrado.

No solicitar al usuario que pruebe paquetes intermedios de un diseño que todavía no satisface el objetivo. El agente debe corregir internamente los errores de implementación/build que pueda resolver. Las dependencias que realmente necesitan credenciales del usuario se preparan con un flujo de conexión y se identifican claramente.

## 8. Definición de terminado

No es suficiente: ventana abierta, estado RUNNING, help/status/ping, CI verde o una captura bonita.

El candidato de producto completo debe incluir el diseño y navegación funcionales, avatar importable y visible, audio/VAD/TTS local, conexión Twitch preparada y funcional tras OAuth, los sistemas de comunidad/gaming/moderación, SQLite persistente, overlay/OBS, Telegram Web con fallback, datos/credenciales protegidos, logs/diagnóstico y paquete reproducible. El resultado de cada flujo debe ser verificable; no se deben simular éxitos.

El inventario, límites, decisiones, dependencias, referencias y secuencia de implementación se mantienen en docs/PRODUCT_BLUEPRINT.md y en el registro central de continuidad.

## 9. ChatSpeak Studio — IA remota, voz virtual y comandos visuales

- El modo manual utiliza ChatGPT web mediante apertura del navegador y flujo explícito de copiar/pegar; no extrae DOM, cookies ni conversaciones de la sesión web.
- El modo automático usa el adaptador remoto HTTPS configurable (Gemini OpenAI-compatible por defecto) o el proveedor local existente limitado a loopback. Respuestas automáticas, publicación a Twitch y comentario periódico permanecen desactivados por defecto.
- La voz virtual remota usa Gemini TTS como configuración inicial, con voz femenina original de estilo anime; se permite cambiar a un endpoint TTS OpenAI-compatible o al SAPI local. Una voz concreta requiere configurar la clave, el modelo y el preset aceptados por el proveedor.
- Las credenciales remotas se cifran por usuario de Windows con DPAPI y quedan fuera de SQLite y los logs.
- Las directivas de IA se limitan a una lista cerrada de expresiones (/angri, /happy, /surprised, /idle, /talking, /blink, /left eye close). Las líneas con comandos se eliminan del texto hablado. Los controles administrativos permanecen exclusivos del creador.
- El comentario periódico de chat utiliza un lote acotado en memoria, intervalo configurable (5 minutos por defecto), deduplicación/filtros básicos y cancelación al pausar/desconectar. No persiste una transcripción completa.
- Los proveedores gratuitos están sujetos a cuotas, elegibilidad y condiciones de privacidad del proveedor; no se prometen llamadas ilimitadas ni una integración gratuita permanente.
