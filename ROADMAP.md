# ROADMAP — VTuberBot producto completo

Este roadmap prioriza terminar una aplicación coherente y útil, no detener al usuario en cada etapa técnica. El CI y los tests se ejecutan internamente durante el desarrollo, pero la validación manual del usuario se concentra en un candidato global integrado.

Especificación canónica de producto, UI, arquitectura, carpetas, referencias y flujos: [docs/PRODUCT_BLUEPRINT.md](docs/PRODUCT_BLUEPRINT.md).

## Objetivo final

Aplicación Windows local-first para uso real como VTuber Bot, con una interfaz moderna, avatar visible y configurable, voz/audio local, Twitch, comandos, comunidad, sorteos, minijuegos, moderación, automatizaciones, OBS, Telegram Web, persistencia y diagnósticos.

## Estado de la base

- C# / .NET 10 / WPF / SQLite: base tecnológica vigente.
- V0.2.3 stabilization PR #9: trabajo técnico que sigue abierto y no fusionado. Su CI verde solo prueba compilación y tests automatizados del commit exacto; no demuestra que el producto final esté listo.
- Nueva línea de producto: rama product/complete-vtuber-experience, basada en el candidato de PR #9, para materializar el alcance completo y evitar seguir consumiendo rondas manuales en la interfaz mínima.
- No fusionar PR #9 de forma automática ni borrar el historial existente.

## Bloques de producto

### Bloque A — Diseño UX y shell completo
- Tema visual consistente, navegación, sistema de páginas, layout redimensionable y componentes reutilizables.
- Dashboard útil con avatar/estado/actividad/conexiones/rendimiento.
- Espacios de trabajo completos: Avatar, Twitch, comandos, comunidad, sorteos, minijuegos, moderación/automatización, voz/audio, OBS, Telegram, actividad y ajustes.
- Eliminar la sensación de programa técnico formado por un botón de iniciar y otro de detener. Ninguna navegación ni acción visible queda inerte.

### Bloque B — Avatar Studio y salida visual
- Importación de sprites/capas con validación, perfiles configurables y assets en el directorio de datos del usuario.
- Renderizador PNG por capas con idle/talking/blink/expresiones, parpadeo, boca, respiración y movimiento 2.5D ligero.
- Previsualización integrada y overlay transparente, independiente del panel, para OBS.
- Controles claros para ubicar/mostrar/ocultar el overlay; estado vacío honesto si el usuario aún no importó assets.

### Bloque C — Audio y voz local
- Enumerar/seleccionar micrófono y salida, medidor de audio, umbral/smoothing/hold, VAD local, cola de audio, sonidos y TTS local.
- La boca sigue al estado real de voz; no simular lip-sync ni dispositivos inexistentes.
- La aplicación sigue funcionando si el dispositivo de audio no está disponible.

### Bloque D — Twitch, chat y comandos
- Adaptador oficial con OAuth, IRC sobre TLS para chat y EventSub para eventos soportados, reconexión, permisos/scopes y mensajes de diagnóstico.
- Un único router y pipeline de comandos para pruebas locales y para eventos del chat.
- El usuario puede abrir la app y probar la lógica local sin conectarse a Twitch.
- Ningún token o secreto aparece en fuente, logs, documentos o paquetes.

### Bloque E — Comunidad y herramientas de stream
- Usuarios, perfiles, puntos, rankings e historial SQLite.
- Sorteos reales con participantes/ganador registrados.
- Minijuegos locales utilizables, reglas/recompensas e historial.
- Moderación por reglas, timers, mensajes automáticos y límites anti-spam; acciones remotas punitivas requieren configuración explícita.
- Logs y registro de auditoría para cada efecto relevante.

### Bloque F — OBS, Telegram, ajustes y distribución
- Overlay/captura transparente y conexión opcional al protocolo local de OBS.
- Telegram Web en WebView2, con apertura externa si WebView2 no está disponible.
- Configuración segura, perfiles de rendimiento, diagnóstico, exportación de logs filtrados y paquete portable reproducible.
- Datos locales y assets separados de los binarios de la aplicación.

### Bloque G — Integración y aceptación global
- Integrar todos los bloques sin ciclos de dependencias; consolidar configuración, eventos, almacenamiento y errores.
- Ejecutar la batería completa de CI/tests a medida que se construye; solucionar internamente las regresiones.
- No pedir al usuario pruebas manuales tras cada commit, nueva build o corrección pequeña.
- Una vez que todos los bloques del producto estén presentes, entregar un solo candidato global para validar los flujos end-to-end en Windows.
- Las funciones que dependen de credenciales/permisos/cuentas externas se declaran con honestidad y se validan tras configurar esas cuentas; no se simulan como conectadas.

## Arquitectura objetivo

Mantener Core independiente de WPF. Separar en módulos claros: App/UI, Core/contratos/casos de uso, Infrastructure (SQLite, configuración, logging y seguridad), Avatar, Audio, Twitch, Community, Interaction (comandos, sorteos, minijuegos, moderación y automation), OBS y tests. No crear proyectos separados para cada clase; preservar la simplicidad.

Los directorios, contratos y diagrama de flujo concretos están definidos en docs/PRODUCT_BLUEPRINT.md.

## Reglas de trabajo

1. Reutilizar el código que ya funciona y revisar todo el repo antes de reestructurar.
2. Implementar por conjuntos grandes integrados, con autonomía para corregir errores de compilación y tests sin pedir aprobación por cada decisión rutinaria.
3. Mantener una única bitácora en docs/CONTINUIDAD_PROYECTO.md, incluyendo rama, HEAD, commits, CI, features terminadas, dependencias externas y próximos pasos.
4. No confundir compila con está listo; tampoco pedir al usuario que pruebe repetidamente un producto que aún carece de las funciones solicitadas.
5. No añadir dependencias pagas ni APIs de IA obligatorias. Los servicios de terceros deben tener adaptadores, opt-in y fallbacks.
6. No afirmar que se implementó o probó una función sin evidencia.
7. Al terminar la integración completa, ejecutar auditoría general, corregir los problemas encontrados y recién entonces solicitar la validación global del usuario.

## Referencias de investigación

- Firebot: comandos, eventos, timers, acciones/efectos, variables e integraciones — https://github.com/crowbartools/Firebot
- PixelLab Studio: editor de rig 2D, capas, movimiento, persistencia y salida a OBS — https://github.com/WeebLabs/PixelLabStudio
- PNGTuber: selección idle/talk, audio/sensibilidad, configuración persistente y overlay — https://github.com/Aadi3003/PNGTuber
- PNGTuber Plus: escenas UI, sprites/capas y rig — https://github.com/kaiakairos/PNGTuber-Plus
- VTube Studio: referencia para aislar integraciones de modelos detrás de adaptadores — https://github.com/DenchiSoft/VTubeStudio

Se estudian las ideas de arquitectura y los flujos; no se copia código, diseño exacto ni assets.


## Avance integrado de cierre — 2026-10-12

- La pantalla Comandos ofrece una ejecución local real del comando guardado, separada de la simulación sin efectos y sin envío de mensajes externos.
- Los mensajes automáticos no se emiten inmediatamente al primer encuentro con un timer nuevo: se respeta el intervalo configurado y se registran pruebas unitarias de la política de agenda.
- La captura de micrófono limpia recursos y restablece el avatar si el inicio del dispositivo falla.
- Este bloque queda sujeto a la ejecución de Windows CI del SHA que lo contiene. Los flujos que necesitan Twitch, OBS, micrófono y Telegram reales continúan pendientes de validación manual global.


## Avance integrado de audio — 2026-10-12

- Voz y audio ahora permiten enumerar y seleccionar una salida de Windows expuesta por SAPI; el dispositivo elegido se persiste y se adjunta a cada frase encolada. La salida predeterminada queda como fallback si no se elige una salida específica o el dispositivo ya no aparece.
- La restauración de la base también limpia las referencias temporales de timers en memoria.
- Pendiente de verificar mediante Windows CI del nuevo SHA y de probar reproducción real con dispositivos disponibles en el entorno final.


## Distribución segura — endurecimiento CI (2026-10-12)

La comprobación del ZIP de validación se amplió para fallar si aparecen nombres conocidos de bases de datos, journals, logs, backups, cookies, credenciales, secretos, tokens OAuth o archivos `.env`. La generación sigue partiendo de un directorio limpio de publicación; la modificación requiere su propio Windows CI verde.


Corrección del 2026-10-12: la primera versión del filtro de archivos sensibles provocó un fallo de parseo del workflow antes de que comenzara un job. La expresión y el `if` de validación se restauraron; el nuevo SHA necesita Windows CI completo.


## Persistencia versionada — 2026-10-12

Se añadió `product_schema_meta` para registrar la versión del esquema de funciones del producto y rechazar bases marcadas por una versión futura. El marcador se escribe después de aplicar las migraciones idempotentes y compatibilidad legacy existentes. La prueba automatizada valida el registro y el rechazo de un marcador futuro sin mutar el marcador; la validación de backups también rechaza esquemas futuros antes de restaurar. Requiere Windows CI del nuevo SHA.


## Audio local — biblioteca de efectos (2026-10-12)

- Voz y audio permite importar, listar, reproducir y detener efectos WAV/MP3 propios; el catálogo persiste en SQLite y los archivos quedan en la carpeta local de datos, fuera de los backups de base.
- Las rutas cargadas se restringen a la carpeta local de sonidos y los formatos se validan mediante política testeada. La reproducción usa NAudio ya disponible, sin servicios remotos.
- Pendiente de Windows CI del nuevo SHA y de una comprobación de salida real en la validación global.


## Integración de comunidad en comandos — 2026-10-12

- Se incorporó un router común de comandos de comunidad para la UI local y el chat Twitch: `!puntos`, `!top` y `!jugar`.
- Los minijuegos del chat reutilizan las reglas y la liquidación transaccional de SQLite ya existentes. Las apuestas validan el saldo, conservan el ledger/historial y aceptan `0` como práctica gratuita; no se requiere Twitch para probarlos desde la UI.
- Duplicar un perfil de avatar también copia los ocho prompts de arte por rol, con claves independientes para que editar la copia no altere el original.
- Evidencia del código: commit `601ec7afa520681c5ed2075b3a94a1bf1a2360fd`; Windows CI #234 SUCCESS (restore, build Release, tests, publish win-x64 y upload). Los flujos con Twitch real siguen sujetos a validación externa.


## OBS WebSocket — timeout total por solicitud (2026-10-12)

- La fecha límite de 15 segundos ahora incluye el envío del frame y la espera de respuesta de cada solicitud OBS; antes el contador empezaba después del envío y un socket de escritura atascado podía quedar fuera del timeout.
- El token enlazado también respeta la cancelación del llamador y el bloque finally elimina la solicitud pendiente.
- Requiere Windows CI del SHA nuevo; la conexión real y el comportamiento con OBS local siguen pendientes de validación manual.


## Ciclo de vida WPF — cierre ordenado (2026-10-12)

- Se reemplazó el handler async de Window.Closed por OnClosing cancelable y una rutina de cierre que termina las disposiciones asíncronas antes de volver a cerrar la ventana.
- Los fallos al liberar un recurso se registran sin impedir la liberación de Twitch IRC, EventSub u OBS.
- Requiere Windows CI del nuevo SHA y validación real de inicio/cierre en Windows; el cierre no se considera manualmente verificado.


## Audio — callback de micrófono durante apagado (2026-10-12)

- Se ignoran callbacks PCM con menos de una muestra completa de 16 bits y se protege el posteo a WPF Dispatcher si coincide con su apagado.
- El cambio evita NaN por divisor cero y una excepción no controlada durante la desconexión/cierre de la captura.
- Requiere Windows CI y una validación manual con micrófono real para confirmar el cierre bajo captura activa.


## Audio — normalización PCM16 comprobable (2026-10-12)

- El cálculo de nivel del micrófono se extrajo a la política pura Pcm16LevelPolicy en Core, sin dependencia de WPF ni NAudio.
- Las pruebas cubren buffers vacíos/incompletos, silencio, saturación y byte final impar. La UI usa ahora la misma función que se prueba en CI.
- Windows CI del nuevo SHA debe confirmar build y suite; la prueba con dispositivo físico queda para la validación global.
