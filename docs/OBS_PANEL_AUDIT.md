# Auditoría del panel de OBS y alternativas de motor

**Fecha:** 2026-10-16  
**Base del trabajo:** `main` en `47054b289fd84c02752cde97b1de68099b03f478`  
**Rama de implementación:** `product/complete-vtuber-experience-obs-panel`

## Decisión ejecutiva

**Mantener OBS Studio como motor de captura, composición, audio, codificación y emisión, y hacer de VTuberBot el panel operativo habitual.** La integración OBS WebSocket v5 existente ya cubre operaciones reales y tiene una ruta de autenticación. Antes de reemplazarla, corresponde medir el consumo en el equipo objetivo con el mismo perfil, fuentes, resolución y duración.

Esta rama añade inicio solicitado minimizado a la bandeja, comprobación del proceso después de solicitar inicio, confirmaciones explícitas antes de comenzar una emisión en vivo o una grabación, medición separada de CPU/RAM de VTuberBot y OBS, y lectura de estadísticas internas de render que publica OBS. El diagnóstico AMD identifica indicios locales, pero **no asegura que un encoder AMF funcione**.

## Auditoría de funciones existentes

| Función | Estado según código | Límite que debe quedar claro |
|---|---|---|
| Abrir OBS desde la interfaz y al arrancar VTuberBot | Implementado; se comprueba que el proceso aparezca | «Inicio solicitado» no significa que OBS esté listo |
| Conexión OBS WebSocket v5 | Cliente implementado con Hello/Identify, autenticación por desafío, timeouts y contraseña protegida con DPAPI para el usuario de Windows | Una conexión a `ws://` no cifra el tráfico; usar loopback por defecto y no exponer el puerto a redes no confiables |
| Consultar y cambiar escenas | Implementado mediante solicitudes a OBS | El nombre mostrado pasa a ser real después de consultar el servidor |
| Consultar y cambiar visibilidad de fuentes | Implementado por `sceneItemId` de OBS | Actúa en OBS, no en la previsualización local de VTuberBot |
| Estado, inicio y detención de transmisión/grabación | Implementado mediante solicitudes y estado/eventos reales | Respuesta aceptada por OBS no garantiza que la plataforma reciba la señal; iniciar transmisión es una acción pública y requiere confirmación |
| Silenciar y reactivar entradas de audio compatibles | Implementado, con consulta de estado y actualización | La lista depende de los tipos de fuente admitidos por OBS |
| CPU y RAM de cada proceso | Añadido: working set de Windows y muestreo de CPU en una ventana corta | Es una instantánea aproximada, no un benchmark de streaming completo ni una medición de GPU |
| FPS y pérdidas de cuadros de OBS | Añadido mediante `GetStats` WebSocket | Estadísticas internas de render/salida de OBS; no equivalen a CPU/RAM y no demuestran por sí solas la calidad de la conexión a la plataforma |
| Encoder AMD/AMF | Añadido diagnóstico de indicios: adaptador del registro, DLL conocida y referencias en logs recientes | **No es una prueba de creación del encoder**. Solo una grabación local correcta con la configuración instalada lo confirma |

Fuentes de la implementación principal: `src/VtuberBot.App/ObsWebSocketClient.cs`, `src/VtuberBot.App/ObsStartupCoordinator.cs` y `src/VtuberBot.App/Workspaces/MainWindow.Integrations.cs`.

## Inicio minimizado y fondo

La opción `--minimize-to-tray` forma parte de los parámetros de inicio documentados de OBS. VTuberBot la envía solo al crear una instancia nueva, de acuerdo con la preferencia guardada `obs.startMinimized`. OBS debe tener habilitado su icono de bandeja del sistema. La opción no convierte una ventana ya abierta en minimizada y tampoco prueba que WebSocket haya autenticado correctamente.

El modo «solo plugins incluidos» (`--only-bundled-plugins`) es independiente y opcional. Puede hacer que una escena pierda una fuente de terceros; no debe activarse como arreglo universal ni prometer mejoras de rendimiento sin medirlas.

Documentación oficial: https://obsproject.com/kb/launch-parameters

## ¿Qué sabemos del hardware AMD?

Para un Ryzen 5 5600G, AMD publica gráficos Radeon integrados. La matriz de AMD AMF identifica a la familia Cezanne/VCN 2.0 con codificación H.264 y HEVC por hardware; esa generación no proporciona codificación AV1 por hardware. Es una capacidad esperada de la familia de silicio, no una observación del equipo actual: driver, versión de OBS, DLL del encoder, configuración y ejecución pueden impedir su uso.

- Información del procesador: https://www.amd.com/es/support/downloads/drivers.html/processors/ryzen/ryzen-5000-series/amd-ryzen-5-5600g.html
- Matriz de funciones AMD AMF: https://github.com/GPUOpen-LibrariesAndSDKs/AMF/wiki/GPU-and-APU-HW-Features-and-Support
- Guía oficial de codificación por hardware de OBS: https://obsproject.com/kb/hardware-encoding

La herramienta del panel inspecciona señales locales de Windows/instalación/logs y no las etiqueta como disponibilidad confirmada. El WebSocket de OBS no ofrece una petición que enumere de forma autoritativa los encoders disponibles en la GUI.

### Prueba real requerida para afirmar que AMD AMF funciona

1. Abrir OBS instalado en el Windows objetivo y elegir la opción de codificador AMD/AMF H.264 en los ajustes de salida.
2. Realizar una grabación local breve con el mismo lienzo, resolución y FPS previstos para el directo.
3. Detener la grabación, reproducir el archivo y comprobar que el log de esa ejecución muestra la inicialización correcta del encoder y no errores de AMF.
4. Comparar la prueba con el encoder por software bajo la misma escena y duración. Guardar FPS, cuadros omitidos, CPU/RAM de VTuberBot, CPU/RAM de OBS y calidad visual.

Hasta realizar estos pasos, el estado correcto es **«pendiente de prueba local»**, no «AMD confirmado».

## Comparación de alternativas de arquitectura

| Alternativa | Ventajas | Costes/riesgos | Decisión actual |
|---|---|---|---|
| OBS Studio + OBS WebSocket v5 (arquitectura actual) | Conserva el motor, la configuración, escenas, fuentes, audio, codificadores, plugins, emisiones y grabaciones; evita duplicar un stack multimedia | Sigue habiendo un proceso OBS separado; compatibilidad y resultado final dependen de la instalación local | **Elegida**: terminar el panel y medir |
| Motor de emisión propio desde cero | Control total sobre el diseño del motor | Requeriría implementar o integrar captura de escritorio/ventanas, composición, mezcla y sincronización de audio, render, gestión de encoders, transporte RTMP(S), reconexión, fallos de red, configuraciones de plataformas y compatibilidad de dispositivos. Sería un proyecto multimedia grande con riesgo de regresiones | **No iniciar** sin benchmark comparativo, prototipo y plan de paridad funcional |
| Integrar `libobs` directamente | Reutilizaría partes del motor de OBS sin tener que automatizar una aplicación externa | `libobs` es nativa; la distribución necesita DLLs compatibles, dependencias auxiliares, datos de OBS y plugins correctos para arquitectura/versión. También hay que resolver ABI, carga de módulos, encoders, audio y ciclos de vida. No equivale a añadir una biblioteca NuGet ni garantiza menor consumo | **Experimento futuro separado**, no cambiar el núcleo actual |

Referencias oficiales/técnicas para el estudio de `libobs`:

- Código y licencia de OBS Studio (GPL-2.0-or-later): https://github.com/obsproject/obs-studio/
- Dependencias nativas utilizadas por OBS: https://github.com/obsproject/obs-deps
- Referencia de sistema de plugins: https://docs.obsproject.com/plugins
- Guía para desarrolladores de plugins y distribución: https://obsproject.com/kb/plugins-guide

Un binario redistribuido que se enlace con componentes de OBS debe recibir una evaluación legal específica de la licencia GPL, avisos, código fuente correspondiente y forma de entrega. Este documento no equivale a un dictamen legal. Un módulo de plugin y una integración que controla un proceso separado son modelos técnicos diferentes; no se debe asumir que tienen iguales obligaciones ni que los plugins binarios de terceros se pueden incluir libremente.

## Benchmark antes de plantear una sustitución

Realizar las comparaciones en Windows local, no en GitHub Actions:

- Estado A: VTuberBot abierto, OBS cerrado.
- Estado B: VTuberBot abierto, OBS abierto, conectado, sin emitir.
- Estado C: la misma escena y fuentes, grabación local de 720p30 de 60 segundos, encoder por software.
- Estado D: misma escena y duración, encoder AMD/AMF (solo si la prueba real del encoder pasó).

Anotar para cada estado CPU (%) y working set (MiB) por separado para VTuberBot y OBS; además FPS, tiempo promedio de render y porcentajes de cuadros omitidos reportados por OBS. No comparar ejecuciones con escenas, filtros o fuentes diferentes. Para la prueba AMF, verificar el archivo resultante y los logs; el porcentaje de CPU no acredita por sí solo que el vídeo sea correcto.

El panel nuevo mide CPU normalizada sobre el total de capacidad de la máquina durante una ventana de 650 ms y memoria de trabajo en una única lectura. Es útil para una inspección operativa rápida, pero no sustituye una captura sostenida (por ejemplo, Performance Monitor/ETW) ni registra picos históricos. La evidencia del rendimiento debe anexarse con condiciones y resultados medidos, no inferirse a partir de este código.

## Validación automática vs. validación local

La canalización Windows CI comprueba restauración, compilación Release, pruebas automatizadas y publicación del candidato Windows x64 según el workflow. CI **no** inicia OBS real, no carga los drivers AMD del equipo objetivo, no valida una emisión a Twitch/YouTube y no mide el uso de GPU del ordenador del usuario.

Criterio de cierre de esta mejora:

- CI verde en el SHA exacto de la rama/PR.
- Inicio minimizado y conexión WebSocket verificados manualmente en Windows.
- Escena, visibilidad, audio, estados de salida y confirmaciones comprobados contra OBS real.
- Medición de procesos guardada en las condiciones arriba indicadas.
- Encoder AMD declarado disponible únicamente tras grabación local reproducible y revisión del log.
