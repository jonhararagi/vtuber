# VTuberBot — Producto completo y arquitectura objetivo

**Estado:** especificación activa para la rama `product/complete-vtuber-experience`.
**Base:** PR #9 / commit `4ba87d8f94d65a84dba475c8b0f9103380892777`.
**Objetivo de este documento:** construir un programa VTuber de escritorio utilizable, no detener el desarrollo en una ventana técnica de estado y controles.

## 1. Decisión de producto

VTuberBot será una aplicación de escritorio Windows local-first para gestionar avatar, audio/voz, Twitch, comandos, comunidad, automatizaciones, juegos, OBS, métricas y Telegram Web desde una interfaz coherente.

La versión actual es una fundación técnica: que abra, compile o tenga botones de iniciar/detener no significa que cumpla el producto solicitado. No se aceptará como experiencia final hasta que las áreas descritas aquí estén integradas y las rutas principales puedan recorrerse desde la interfaz.

### Restricciones del usuario

- Equipo objetivo: Ryzen 5 5600G, 16 GB RAM, Windows y OBS Studio.
- Sin APIs de IA pagas u obligatorias en ejecución. Las herramientas de IA solo se usan durante desarrollo.
- Prioridad a software gratuito/local, bajo consumo y arquitectura modular.
- No cerrar programas del usuario, tocar ajustes críticos del sistema ni ejecutar acciones destructivas sin permiso explícito.
- No inventar conexiones activas: cada integración debe mostrar claramente si está desconectada, requiere configuración, se está conectando, está conectada o falló.
- No poner credenciales, tokens, bases de datos personales o logs dentro del repositorio ni dentro del ZIP de distribución.
- No detener el producto en la estabilización del panel básico. El usuario hará una validación global al disponer de un candidato con el alcance completo integrado; mientras tanto, el agente implementador sigue un plan coherente y corrige internamente los fallos de compilación/integración sin pedir informes tras cada modificación menor.

## 2. Experiencia y diseño visual objetivo

### Dirección visual

Interfaz de aplicación streamer moderna, oscura, pulida y de alta densidad útil, con acentos cian/violeta/rosa usados con moderación. No es una consola técnica ni una página llena de botones de estado.

- Ventana inicial sugerida: 1360 × 860, redimensionable; mínimo 1080 × 700.
- Navegación vertical fija con iconos y texto; marca VTuberBot arriba, estado de conexión global y acceso a Ajustes abajo.
- Barra superior contextual con título de página, estado de la sesión, búsqueda/acciones rápidas y acceso a notificaciones.
- Fondo carbón/azul noche, superficies elevadas distinguibles, bordes suaves, espaciado consistente, tipografía legible y jerarquía clara.
- Estados importantes también expresados con texto e icono, nunca solo con color.
- Animaciones de interfaz breves y discretas; opción de reducir animaciones.
- Los gráficos deben mostrar datos reales. Cuando no hay fuente de datos, enseñar “No disponible” o “Conectá una fuente”, nunca datos falsos.
- El avatar debe tener presencia visual central en el Dashboard y una ventana de superposición transparente separada para OBS.
- Componentes reutilizables: tarjetas de resumen, indicador de estado, botones primario/secundario/peligro, paneles, tablas, campos con validación, toasts y estados vacíos.

### Navegación final

1. **Inicio / Dashboard:** previsualización del avatar, estado de bot/Twitch/OBS/audio, resumen de actividad, uso de CPU/RAM, alertas accionables y accesos a las tareas frecuentes.
2. **Avatar Studio:** importar imágenes, administrar capas/estados, previsualizar movimiento y expresiones, probar boca/ojos, configurar sensibilidad y mostrar/ocultar la ventana del avatar.
3. **Twitch y chat:** conectar/desconectar la cuenta, estado de autenticación, chat en directo, enviar mensaje manual, permisos/scopes, reconexión y diagnóstico.
4. **Comandos:** lista/tabla de comandos, activación, alias, descripción, respuesta y cooldown; probar un comando localmente, crear/editar/eliminar con confirmación.
5. **Comunidad:** usuarios, puntos, rankings, perfiles e historial con búsqueda/filtros y persistencia SQLite.
6. **Sorteos:** crear/configurar sorteo, entradas, requisitos, comenzar/cerrar, escoger ganador de forma trazable y conservar historial.
7. **Minijuegos:** catálogo de minijuegos locales, configuración de reglas/recompensas, sesiones e historial.
8. **Moderación y automatización:** filtros y límites configurables, cooldowns, mensajes programados, triggers, acciones y registro de cada ejecución. Acciones punitivas remotas siempre sujetas a permisos y configuración explícita.
9. **Voz y audio:** dispositivos disponibles, medidor, pruebas de entrada/salida, detección local de voz, cola/prioridades, volumen, pruebas de TTS local y reacciones pregrabadas.
10. **OBS:** estado de la conexión opcional, guía de configuración de captura/overlay, prueba de transparencia, escenas/fuentes solo si se habilita el protocolo oficial local.
11. **Telegram Web:** página integrada de Telegram Web cuando WebView2 esté disponible, con estado claro y opción para abrir en navegador si no puede inicializarse. No almacenar cookies de Telegram en el repositorio ni exportarlas.
12. **Actividad y diagnósticos:** log legible, filtros por nivel/módulo, errores recientes, estado de adaptadores y exportación de diagnóstico sin secretos.
13. **Ajustes:** perfil de rendimiento, dispositivos, rutas de datos, idiomas, apariencia, inicio de módulos y privacidad/seguridad.

Las páginas no pueden ser botones muertos ni contenido decorativo. Cada página debe permitir completar sus flujos locales. Para integraciones que necesitan cuentas, permisos o configuración externa, la UI debe explicar lo que falta y proporcionar el flujo de conexión real.

## 3. Funciones integradas esperadas

### Núcleo y persistencia
- Inicio, cierre y recuperación ordenados; configuración validada; logs rotativos; eventos tipados; programación de tareas; gestión de recursos y salud.
- SQLite versionada para usuarios, puntos, perfiles, inventario si se activa, comandos, reglas, sorteos, juegos, mensajes automáticos, configuración no secreta e historial.
- Migraciones de base de datos y copias de seguridad/exportación controlada.

### Avatar PNG/2.5D
- Importación de imágenes propias y validación de formato/tamaño; almacenamiento de assets del usuario en LocalAppData.
- Perfiles de avatar y estados: idle, talking, blink, expresiones y reacción/evento.
- Renderizador de sprites/capas, composición, orden, posición, escala, pivote, parentado opcional y configuración de movimiento leve/parallax. No fingir tracking facial si no existe una fuente real.
- Parpadeo y movimiento de boca/ojos; respiración y movimiento ligero con consumo limitado.
- Previsualización en el panel y ventana overlay separada, transparente y sin bordes para OBS; controles de mostrar/ocultar y tamaño/posición.
- La ausencia de assets no debe romper el programa: onboarding para importar el primer avatar y una ilustración de estado vacío no presentada como avatar real.

### Twitch
- Adaptador oficial separado: autenticación OAuth, conexión de chat mediante Twitch IRC sobre TLS, envío/recepción de mensajes y EventSub para eventos soportados, con reconexión, keepalive, deduplicación, límites, permisos y errores visibles.
- Ningún token o secreto se guarda en Git o logs. Persistencia local protegida, preferentemente Windows Credential Manager/DPAPI.
- Los comandos locales se ejecutan igual con Twitch desconectado para poder probar el sistema sin internet.
- Las funciones que requieren scopes deben listar el permiso faltante y no simular resultados.
- **Aclaración de alcance:** “sin API” se interpreta como sin APIs de IA pagas/obligatorias. Un chatbot real de Twitch requiere usar los mecanismos oficiales gratuitos y su autenticación; el código no debe prometer integración completa bajo una prohibición absoluta de comunicarse con Twitch.

### Comandos, comunidad y actividad
- Bus de comandos compartido por UI y Twitch. Un único registro de comandos, validación, cooldown, permisos, respuestas y resultado auditable.
- Puntos, ranking, perfiles e historial persistidos; reglas de recompensa configurables.
- Sorteos con entradas deduplicadas y método de selección documentado; historial de participantes/ganador.
- Minijuegos simples iniciales que funcionen de verdad (por ejemplo, duelo/ruleta/sorteo interactivo), con estado, reglas y recompensas persistidos.
- Moderación basada en reglas configurables, primero en modo observación; acciones automáticas peligrosas deshabilitadas hasta configuración explícita.
- Timers/mensajes automáticos pausables con preview y protección anti-spam.

### Voz y audio
- Enumeración/selección de micrófono y salida; VU-meter; prueba; detección de actividad vocal local configurable con umbral, smoothing y hold.
- Cola de audio con prioridades, cancelación/recuperación y sonidos pregrabados.
- TTS inicial usando motor local disponible en Windows; diseño de proveedor que permita añadir motores locales gratuitos después. No exigir credenciales ni API remota.
- Boca vinculada inicialmente al estado de voz/hablar; lip-sync avanzado solo si hay señal real que lo permita.

### OBS y Telegram
- Overlay pensado para la captura de ventana transparente que ofrece OBS; documentar método de captura, límites de Windows y diagnóstico. No exigir complementos de pago.
- Integración de control de OBS mediante su protocolo oficial local, opt-in, con autenticación y reconexión; la app sigue funcionando si OBS está cerrado.
- Telegram Web integrado mediante WebView2 cuando la dependencia esté disponible, con fallback a abrir el navegador. La sesión y sus cookies no se copian a bases de datos del bot.

## 4. Arquitectura y distribución de carpetas objetivo

La solución debe conservar .NET 10/WPF y SQLite, separando las responsabilidades sin convertir cada clase en un proyecto independiente. No es necesario renombrar todos los proyectos solo por estética; la siguiente estructura es el destino lógico y se integra gradualmente preservando el código útil actual.

```text
VtuberBot.sln
src/
  VtuberBot.App/
    App.xaml
    Views/
      ShellWindow.xaml
      Pages/
        DashboardPage.xaml
        AvatarStudioPage.xaml
        TwitchPage.xaml
        CommandsPage.xaml
        CommunityPage.xaml
        GiveawaysPage.xaml
        MinigamesPage.xaml
        ModerationPage.xaml
        VoicePage.xaml
        ObsPage.xaml
        TelegramPage.xaml
        ActivityPage.xaml
        SettingsPage.xaml
      Overlays/AvatarOverlayWindow.xaml
    ViewModels/
    Navigation/
    Themes/
      Colors.xaml
      Typography.xaml
      Controls.xaml
    Assets/Icons/
  VtuberBot.Core/
    Domain/
      Events/
      Commands/
      Users/
      Points/
      Giveaways/
      Automation/
      Avatar/
      Audio/
    Application/
      Lifecycle/
      UseCases/
      EventRouting/
    Abstractions/
      Storage/
      Twitch/
      Avatar/
      Audio/
      Streaming/
    Scheduling/
    Diagnostics/
  VtuberBot.Infrastructure/
    Configuration/
    Logging/
    Persistence/
      Sqlite/
      Migrations/
      Repositories/
    Security/
    Files/
  VtuberBot.Features.Avatar/
    Import/
    Rig/
    Animation/
    Rendering/
    Overlay/
  VtuberBot.Features.Audio/
    Devices/
    VoiceActivity/
    TextToSpeech/
    Playback/
  VtuberBot.Features.Twitch/
    Authentication/
    Chat/
    EventSub/
    Reconnection/
  VtuberBot.Features.Community/
    Users/
    Points/
    Rankings/
  VtuberBot.Features.Interaction/
    Commands/
    Giveaways/
    Minigames/
    Moderation/
    Automation/
  VtuberBot.Features.Obs/
    Connection/
    Sources/
    Diagnostics/
  VtuberBot.Features.Telegram/
    WebView/
tests/
  VtuberBot.Core.Tests/
  VtuberBot.Infrastructure.Tests/
  VtuberBot.Features.Tests/
  VtuberBot.App.WindowsTests/
docs/
  PRODUCT_BLUEPRINT.md
  ARCHITECTURE.md
  CONTINUITY_PROJECT.md
```

### Límites de dependencia
- `Core` no referencia WPF, OBS, Twitch, WebView2 ni proveedores externos.
- `Infrastructure` implementa almacenamiento, configuración, logs, secretos y archivos a través de interfaces del Core.
- Cada módulo de feature expone servicios/handlers y depende de contratos compartidos; no llama directamente a otros módulos concretos. Los eventos y casos de uso del Core coordinan interacciones.
- `App` es el composition root y la capa de presentación; conecta módulos, navegación y ViewModels.
- Las ventanas y páginas no acceden directamente a SQL, sockets o archivos sensibles.
- Mantener la arquitectura sencilla: cuando varios módulos no justifiquen nuevos proyectos de compilación, usar carpetas con límites claros y tests.

### Datos y distribución
- Los datos de usuario viven en `%LocalAppData%\VtuberBot\` (configuración, SQLite, logs, assets y caché) o subcarpetas definidas por el usuario.
- `BUILD-INFO.txt` y el ZIP de distribución identifican el commit, no contienen configuración, tokens, cookies, bases de usuario ni logs.
- Assets propios del usuario no se publican por defecto ni se sobrescriben al actualizar.

## 5. Flujo de eventos de extremo a extremo

```text
Twitch IRC / EventSub / UI / Audio / Timer
                    |
                    v
         Adapter + Validación
                    |
                    v
        Eventos de dominio tipados
                    |
                    v
         Event Router / EventManager
                    |
                    v
      Rules + Command Router + Use Cases
                    |
          +---------+----------+
          |         |          |
          v         v          v
       SQLite     Avatar     Audio/TTS
          |                    |
          v                    v
   Historial/Logs         Reacción/Playback
          |
          v
  Dashboard / Diagnósticos / OBS overlay
```

Todos los comandos y triggers usan el mismo pipeline para que probarlos localmente equivalga a probar su lógica principal. Los efectos externos se encapsulan, tienen timeout/cancelación y devuelven resultados estructurados.

## 6. Investigación de referencias — ideas de arquitectura, no copia

- **Firebot** — https://github.com/crowbartools/Firebot y https://docs.firebot.app/  
  Referencia para un bot de stream completo organizado alrededor de comandos, eventos, timers, acciones/efectos, variables e integraciones. Aprendizaje: los triggers y acciones deben ser extensibles y configurables desde UI.
- **PixelLab Studio** — https://github.com/WeebLabs/PixelLabStudio  
  Referencia para el flujo de avatar 2D con sprites, capas/jerarquía, movimiento/physics, importación y salida transparente/OBS. Aprendizaje: el avatar necesita un editor y un runtime separados, configuración persistente y preview, no solo un archivo PNG.
- **PNGTuber** — https://github.com/Aadi3003/PNGTuber  
  Referencia para el flujo mínimo de micrófono, selección de sprites idle/talk, sensibilidad, persistencia y ventana transparente capturable por OBS. Aprendizaje: la configuración de avatar y entrada de audio debe estar integrada y probarse en la propia aplicación.
- **PNGTuber Plus** — https://github.com/kaiakairos/PNGTuber-Plus  
  Referencia para escenas/UI de rig y capas; su README advierte que el proyecto puede tener calidad de código limitada, por lo que se estudian conceptos, no se copia su implementación.
- **VTube Studio plugin examples** — https://github.com/DenchiSoft/VTubeStudio  
  Referencia para aislar futuras integraciones de modelos/acciones detrás de un adaptador. No se adopta VTube Studio como dependencia obligatoria.

No se copiará código, assets, diseño exacto, nombres internos ni estructura propietaria. Se desarrollará código original conforme a la licencia de cada dependencia que se decida añadir.

## 7. Decisiones de integración confirmadas

- Mantener C#/.NET 10/WPF y el núcleo actual como base útil.
- Usar SQLite con migraciones, no EF Core.
- Conectividad Twitch y OBS optativa y aislada; sin IA remota obligatoria.
- Audio/voz local como primera opción; proveedores intercambiables.
- Avatar PNG por capas como primera salida visual; movimiento 2.5D ligero.
- WebView2 es opcional/condicional para Telegram; el resto de la app debe iniciar aunque no esté disponible.
- No empaquetar assets de avatar de terceros por defecto; permitir que el usuario importe los suyos.
- El programa debe tener utilidad local antes de autorizar servicios externos y no depender de que Twitch, Telegram u OBS estén abiertos para llegar al Dashboard.

## 8. Plan de ejecución unificado

El trabajo se hace por grandes conjuntos integrados, sin pedir al usuario que valide cada cambio pequeño ni enviar un informe tras cada commit. El agente debe continuar autónomamente entre tareas y registrar en el documento de continuidad. CI y tests pueden ejecutarse durante desarrollo para detectar errores antes; no se solicita al usuario una nueva ronda manual hasta que exista un candidato global con el producto integrado.

### Bloque A — Diseño y estructura funcional
- Shell WPF con navegación real, tema, layout responsive y componentes reutilizables.
- Dashboard conectado a estado real, actividad, rendimiento y conexiones.
- Todas las páginas principales creadas con flujos y estados vacíos honestos; no dejar botones de adorno.
- Definir contratos, inyección/composición de módulos, navegación, configuración y repositorios.

### Bloque B — Avatar y audio
- Importar/validar assets, guardar perfiles de avatar, renderizar capas y estados, animaciones básicas, preview y overlay transparente.
- Dispositivos de audio, VAD local, TTS local, audio queue y sincronización básica de boca.
- UI Avatar Studio con pruebas interactivas y persistencia.

### Bloque C — Twitch y motor de eventos
- Autenticación, chat real, recepción/envío de mensajes y eventos soportados, reconexión/diagnóstico.
- Comandos configurables y pipeline común desde Twitch y desde la UI.

### Bloque D — Funciones de comunidad y engagement
- Puntos, usuarios, perfiles, rankings, sorteos, minijuegos, moderación basada en reglas, timers y mensajes automáticos; toda la información persistida y editable.

### Bloque E — OBS, Telegram y producto
- Overlay transparente, conexión opcional a OBS, diagnósticos; panel Telegram Web con fallback.
- Ajustes, logs, importación/exportación, validaciones, accesibilidad básica y estados de error.
- Empaquetado reproducible, instalación/extracción limpia y documentación para el usuario.

### Bloque F — Validación global
- Una vez que los bloques integrados estén implementados, ejecutar la batería automatizada y el CI sobre el candidato completo; corregir internamente los fallos que aparezcan.
- Preparar una única ronda de validación manual para comprobar flujos reales end-to-end (incluidas las conexiones que requieran credenciales del usuario).
- Registrar límites que dependan de configuración/cuentas externas, no presentarlos como implementados si no pueden probarse.

**Regla de alcance:** no convertir cada módulo en una versión que el usuario tenga que instalar/probar por separado. La entrega prioritaria es un candidato completo y coherente. Las limitaciones externas que necesitan datos/cuentas del usuario se documentan como requisitos de configuración, no como falsos éxitos.

## 9. Definición de “producto completo”

El candidato solo se considera listo para una validación global cuando:
- La interfaz ya no es una ventana técnica de botones: tiene navegación coherente, Dashboard útil y páginas funcionales.
- El avatar puede importarse, verse en preview, animarse en los estados admitidos y aparecer en su overlay transparente.
- Audio/VAD/TTS local tienen flujos funcionales o muestran claramente cualquier dependencia opcional que falta.
- Twitch chat/commands funcionan tras la configuración OAuth requerida.
- Comandos, comunidad, puntos, rankings, sorteos, minijuegos, moderación y automatización funcionan localmente y usan los mismos servicios/eventos.
- SQLite conserva estado e historial entre reinicios.
- OBS y Telegram tienen integración/flujo de conexión real y fallback claro.
- Logs, diagnóstico, ajustes, seguridad de datos y empaquetado están integrados.
- La solución compila y pasa las pruebas; las funcionalidades probadas y no probadas están declaradas con honestidad.

No se equipara “todos los proyectos compilan” con “producto completo”.
