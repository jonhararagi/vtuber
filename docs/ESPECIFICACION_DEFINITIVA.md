# VTuberBot — especificación definitiva de producto y plan de integración

**Estado:** alcance aprobado por el usuario para guiar la integración; no es una afirmación de que todas las funciones estén implementadas.
**Repositorio:** https://github.com/jonhararagi/vtuber
**Rama de documentación:** `docs/definitive-product-scope`
**Plataforma objetivo:** Windows x64, C# / .NET 10 / WPF / SQLite.
**Equipo de referencia:** Ryzen 5 5600G, 16 GB RAM.
**Principio de entrega:** el estado de cada capacidad debe demostrarse con código, pruebas y/o validación documentada. Un botón o una compilación exitosa no prueban que una función esté terminada.

## 1. Visión definitiva

VTuberBot será una aplicación de escritorio que reúna en un solo panel las herramientas necesarias para preparar y controlar un avatar, editar sus imágenes, gestionar la voz local, y operar OBS sin obligar al usuario a navegar por múltiples paneles externos durante el uso habitual.

El objetivo no es reconstruir Twitch ni sustituir OBS como motor de transmisión. OBS seguirá siendo el motor de escenas, fuentes, composición y salida de video. VTuberBot proporcionará su propia interfaz para las operaciones compatibles de OBS y para el avatar/voz. La aplicación debe ser local-first, gratuita en su funcionamiento básico, sencilla y segura.

## 2. Decisiones funcionales confirmadas

### 2.1 Base de la aplicación

- **Conservar:** aplicación Windows de escritorio; C#/.NET 10/WPF; SQLite; configuración persistente; panel principal; asistente inicial; guía contextual; estados claros; confirmaciones; errores comprensibles; controles accesibles; pruebas automatizadas; compilación Windows x64.
- **Arquitectura modular:** queda pendiente de decisión explícita. Mientras tanto, preservar los límites existentes entre Core, UI e integraciones y no introducir acoplamientos nuevos innecesarios.
- **Modo local y gratuito:** no exigir cuentas de IA, APIs pagas, tarjeta ni credenciales remotas para usar las funciones locales. Las conexiones externas serán opcionales y transparentes.
- **No destructivo:** no cambiar escenas, claves, dispositivos, archivos o ajustes críticos sin mostrar la acción y pedir confirmación cuando corresponda.

### 2.2 Avatar e imagen

- **Conservar:** editor PNGTuber, importación de imágenes propias, edición/organización de capas propias cuando sea necesaria para modificar la imagen aportada por el usuario, encuadre, vista previa de composición para OBS y exportación PNG transparente.
- **Sin catálogo de waifus prefabricadas:** no invertir trabajo en una biblioteca de personajes variados ni en generación automática de diseños. El usuario aporta la imagen que quiere usar y la edita.
- **Dos estados básicos:** reposo (boca cerrada) y hablando (boca abierta). No añadir un catálogo de expresiones ni reacciones automáticas en el alcance actual. Si la imagen fuente solo tiene una pose, la aplicación debe explicarlo; no fingir una animación que no existe.
- **Encuadre/OBS:** mantener presets de rostro, busto, cuerpo y personalizado, con controles de posición y tamaño y una vista de composición 1920×1080. La vista es una previsualización orientativa; no equivale a mover una fuente de OBS en tiempo real salvo que esa acción se implemente y pruebe expresamente.
- **3D/VRM:** conservar como capacidad opcional y aislada. No debe bloquear la experiencia principal de avatar 2D. Si solo hay una pantalla informativa o una integración parcial, declararla pendiente y no presentarla como soporte funcional completo.
- **Eliminar del alcance:** parallax/2.5D, editor enfocado en un catálogo de piezas prefabricadas y requisito de instalar/operar PNGTuber Plus como parte del flujo normal.
- **Importante:** retirar PNGTuber Plus como dependencia de usuario no autoriza a borrar código reutilizable sin revisar referencias, perfiles, datos guardados y pruebas. La eliminación se realiza solo después de una auditoría de dependencias y migración.

### 2.3 Twitch y comandos

- **Fuera del alcance actual:** conexión directa de cuenta Twitch, eventos de canal, mensajes automáticos, moderación, puntos/rankings, sorteos, minijuegos, reacciones de avatar, controles público/privado y panel social de Twitch.
- **Conservar comandos personalizados con alcance local:** permitir definir acciones/comandos ejecutables desde VTuberBot o mediante una integración OBS compatible, siempre que la interfaz describa qué los dispara y qué acción realizan.
- **Límite explícito:** sin una conexión Twitch, un comando personalizado no puede activarse automáticamente por un mensaje de chat de Twitch ni leer eventos de Twitch. OBS por sí solo no sustituye la autenticación/API/EventSub/IRC de Twitch ni ofrece automáticamente un panel de chat completo.
- No añadir conectores Twitch ocultos ni solicitar credenciales Twitch en el flujo actual. Si más adelante se quiere activar comandos desde chat, debe aprobarse como alcance separado y explicarse la autenticación, los permisos y el mantenimiento requerido.
- Streamer.bot queda fuera del producto previsto y no debe ser una dependencia obligatoria.

### 2.4 OBS Studio

- **Conservar e integrar:** conexión mediante OBS WebSocket v5, configuración de host/puerto, autenticación protegida, estado real de conexión, manejo de timeout y errores, consulta/ejecución solo de operaciones implementadas y permitidas, y asistente de configuración.
- El usuario debe poder realizar desde VTuberBot las operaciones OBS que efectivamente estén implementadas, sin tener que abrir la interfaz de OBS para cada operación compatible. OBS seguirá instalado y ejecutándose en segundo plano cuando se necesite transmitir.
- No afirmar que VTuberBot “es OBS” ni que puede transmitir por sí solo. No ocultar OBS ni cerrarlo automáticamente.
- El asistente de OBS debe distinguir acciones informativas, acciones que solo abren OBS y operaciones ejecutadas realmente por WebSocket.

### 2.5 Voz local con VOICEVOX

- **Objetivo prioritario:** sustituir la dependencia de voces de Windows/Gemini por síntesis local de VOICEVOX Engine y permitir convertir texto/mensajes configurados en voz desde la propia interfaz de VTuberBot.
- La UI de VTuberBot debe ofrecer selección de speaker/estilo de voz, prueba de audio, volumen y velocidad/entonación cuando estén soportados por el motor, entrada de texto, cola de reproducción, estado del motor y errores con pasos de solución. La conversión de texto a voz debe poder invocarse desde la aplicación; el usuario no debería tener que operar la interfaz de VOICEVOX a diario.
- **Motor local:** integrar el control de VOICEVOX Engine mediante su interfaz local documentada (por defecto suele utilizar localhost y el puerto 50021, pero el puerto real debe poder configurarse y verificarse). El proceso del motor puede gestionarse en segundo plano por VTuberBot si la distribución/licencia y el empaquetado lo permiten; de lo contrario, el asistente debe detectar una instalación local y explicar cómo prepararla sin esconder la dependencia.
- **No asumir que se puede redistribuir cualquier voz:** verificar las licencias y condiciones actuales de VOICEVOX Engine y de cada personaje/voicebank. No empaquetar voces ni recursos sin permiso; mostrar avisos/créditos requeridos y enlazar las fuentes oficiales.
- **No usar Gemini ni una API remota como sustituto silencioso.** Sin motor disponible, mostrar “VOICEVOX no disponible” y pasos concretos. La app no debe simular una voz generada ni declarar éxito si falla la síntesis.
- Limitar longitud de texto, tamaño de cola, cancelación y manejo de errores. No procesar mensajes de Twitch, ya que Twitch queda fuera del alcance actual; la entrada puede ser texto manual, prueba o futuras fuentes explícitamente aprobadas.

### 2.6 Seguridad y configuración

- Los campos de contraseña/token deben mostrarse enmascarados por defecto y contar con un control explícito “Ver/Ocultar”. El control no debe revelar el valor en logs, mensajes de error, telemetría ni capturas diagnósticas.
- Mantener cifrado/almacenamiento seguro ya existente. No degradar el almacenamiento cifrado a texto plano. Nunca guardar credenciales en el repositorio ni dentro del paquete descargable.
- Toda operación de conexión debe mostrar estados reales: no configurado, desconectado, conectando, conectado o error. No presentar “conectado” hasta que exista una respuesta válida del servicio.

## 3. Rendimiento y límite de memoria

- Objetivo solicitado: que VTuberBot use como máximo **1 GiB de RAM para el proceso de la aplicación** en el equipo de referencia, o avise si se acerca/supera el límite.
- Implementar medición real del conjunto de trabajo/memoria del proceso, aviso configurable con valor inicial próximo a 800 MiB y advertencia clara al alcanzar 1 GiB. No afirmar un límite duro si la aplicación solo puede advertir.
- Las funciones locales pesadas (motor de voz, imágenes grandes, exportación) deben usar colas acotadas, límites de tamaño, liberación de recursos y cancelación. Medir por separado el proceso auxiliar de VOICEVOX si se ejecuta como proceso independiente; explicar que OBS y otros programas del usuario no cuentan dentro del límite del proceso VTuberBot.
- Evitar trabajo pesado en el hilo de UI. No detener procesos externos ni descartar trabajo del usuario automáticamente para reducir memoria sin pedir permiso.
- CPU: mostrar uso medido y advertir ante consumo sostenido elevado; no prometer que el equipo nunca alcanzará 100%, porque otras aplicaciones, OBS, drivers y el motor de voz también consumen recursos.

## 4. Persistencia, registros y pruebas

- SQLite conserva preferencias, perfiles, ajustes de encuadre, historial técnico acotado y progreso del asistente.
- Mantener límites de cola y limpieza de historial, evitando que un volumen alto de eventos bloquee la interfaz.
- Las pruebas deben cubrir lógica, persistencia, límites, errores, protección de secretos, conexión OBS simulada/real cuando sea posible, gestión del motor de voz y transición entre los dos estados del avatar.
- CI debe ejecutarse en Windows para el SHA exacto que se va a integrar. Registrar SHA, resultado de build, tests, empaquetado y artefacto. Una prueba automatizada no sustituye la inspección visual y una prueba real de OBS/VOICEVOX.
- La validación manual se concentrará en un único candidato global al final, no en pedir al usuario que pruebe cada commit menor.

## 5. Fuera de alcance explícito por decisión del usuario

No construir ni mantener como funciones principales: catálogo de waifus prefabricadas, generación automática de personajes, parallax/2.5D, PNGTuber Plus como aplicación requerida, Streamer.bot, conexión Twitch, panel social Twitch, eventos de canal, mensajes automáticos, moderación, puntos/rankings, sorteos, minijuegos, reacciones automáticas, Telegram Web o voz remota Gemini.

La exclusión de una función de producto no implica borrar código de inmediato. Primero se inspeccionan referencias, datos persistidos, pruebas, menús, documentación, configuración y posibles dependencias indirectas. Si un elemento es compartido con una función conservada, se mantiene o se sustituye de forma segura.

## 6. Plan de integración en GitHub

### Fase 0 — Auditoría del estado real
1. Registrar el SHA actual de `main`, de las ramas de producto y de las PR abiertas.
2. Revisar `PROJECT_SPEC.md`, `PRODUCT_BLUEPRINT.md`, `ROADMAP.md`, `PROMPTS.md`, continuidad, código y pruebas.
3. Crear inventario por función: implementada y probada, parcialmente implementada, solo UI/documentación, ausente, obsoleta o bloqueada por licencia/instalación.
4. Mapear dependencias antes de retirar cualquier función. No fusionar una rama antigua completa sobre `main` si puede reemplazar el editor de sprites, el asistente OBS u otras funciones más recientes.

### Fase 1 — Alinear documentación
1. Incorporar esta especificación como fuente de alcance.
2. Actualizar Blueprint, Project Spec, Roadmap, guía de usuario y continuidad para que no ordenen simultáneamente construir y eliminar las mismas funciones.
3. Separar “decisión de producto” de “estado implementado” y adjuntar evidencia por SHA/CI para cada afirmación de finalización.
4. Mantener las ramas de trabajo separadas y abrir PR pequeñas por área coherente; ninguna rama se integra sin revisar el diff contra el `main` actualizado.

### Fase 2 — Avatar 2D y OBS
1. Conservar el editor de imágenes/layers y los flujos de importación/exportación existentes.
2. Simplificar la lógica de avatar a dos estados: reposo/habla. Definir cómo se seleccionan las dos imágenes o capas y qué ocurre si falta una.
3. Conservar el editor de encuadre y la previsualización; comprobar si exporta PNG correctamente y aclarar qué cambios llegan a OBS de verdad.
4. Integrar operaciones OBS ya disponibles en el panel y etiquetar explícitamente las que aún son solo guía/previsualización.
5. Mantener 3D/VRM opcional; no permitir que bloquee la ruta 2D.

### Fase 3 — VOICEVOX
1. Auditar el código de voz existente y quitar rutas remotas/Windows solo después de comprobar dependencias.
2. Implementar un proveedor local VOICEVOX con comprobación de salud, descubrimiento/configuración del motor, listado de speakers, síntesis, reproducción, cola limitada y cancelación.
3. Gestionar credenciales si el motor no las requiere sin crear falsas pantallas de secretos; nunca registrar texto sensible.
4. Verificar licencias y distribución. Crear guía y pruebas con motor ausente, motor activo, voz no disponible y fallo de reproducción.
5. Confirmar uso de RAM/CPU del motor separado y documentar el coste de recursos.

### Fase 4 — Limpieza controlada de funciones descartadas
1. Marcar rutas de Twitch, Streamer.bot, Telegram, minijuegos y funciones sociales como fuera de alcance.
2. Auditar qué código/menús/configuración se comparte con comandos conservados, OBS o SQLite.
3. Eliminar o esconder únicamente lo que no tenga dependencias útiles; migrar o limpiar datos solo con estrategia reversible y pruebas.
4. Revisar README, ayuda, guías, instalador y nombres de funciones para no prometer características retiradas.

### Fase 5 — Rendimiento, seguridad y robustez
1. Instrumentar memoria y CPU; añadir advertencias y limitar trabajo/colas.
2. Revisar almacenamiento seguro, máscara/ver de secretos, logs y mensajes de error.
3. Ejecutar tests y Windows CI en cada PR, incluyendo artefacto de validación cuando la pipeline lo permita.
4. Corregir los fallos en la rama de trabajo y volver a ejecutar CI en el SHA final, no en un SHA anterior.

### Fase 6 — Integración y entrega
1. Actualizar la rama de la PR sobre el `main` vigente y resolver conflictos preservando funciones nuevas.
2. Revisar diff y lista de archivos; exigir build/tests verdes del SHA de HEAD.
3. Fusionar solo PR aprobadas y con estado mergeable, sin tocar PR #9 salvo orden específica.
4. Confirmar el SHA final de `main`, ejecutar/consultar su CI y obtener el artefacto producido por ese mismo SHA.
5. Verificar que el artefacto existe y enlazarlo. No entregar un ZIP de otra ejecución.
6. Entregar un informe final que separe: funciona y está probado, funciona con limitaciones, requiere instalación/configuración, pendiente y descartado.
7. Pedir al usuario una única validación manual global de Windows, centrada en abrir el paquete, probar avatar reposo/habla, encuadre, conexión OBS y voz VOICEVOX.

## 7. Criterios de aceptación de la versión candidata

- Abre en Windows x64 y conserva ajustes tras reiniciar.
- El usuario importa y edita su propia imagen; puede configurar reposo/habla sin catálogo prefabricado.
- El encuadre y la exportación son correctos y sus límites están explicados.
- La conexión OBS muestra estado real y ejecuta únicamente operaciones implementadas.
- VOICEVOX local puede comprobarse, elegir una voz disponible, sintetizar y reproducir texto; el motor ausente produce un diagnóstico útil, no una simulación.
- Los secretos están enmascarados por defecto y no aparecen en logs.
- La aplicación vigila memoria y advierte antes/al superar el umbral de 1 GiB; no se presenta como límite duro salvo que esté técnicamente garantizado.
- Tests y Windows CI están verdes en el SHA final de la PR y el artefacto pertenece a ese mismo SHA.
- README, manual y continuidad coinciden con el comportamiento real.
- Ninguna función descartada se borra sin análisis de dependencias y sin comprobar que las funciones conservadas siguen operativas.

## 8. Riesgos y decisiones que quedan explícitamente abiertos

- **Arquitectura modular:** pendiente de aprobación explícita, aunque se preservan los límites existentes por seguridad.
- **VRM/3D:** conservar como opcional, pero el alcance exacto depende de la implementación real encontrada en la auditoría.
- **Comandos personalizados:** sin Twitch, solo se activan por mecanismos locales/OBS implementados. La activación desde chat requeriría una decisión nueva.
- **VOICEVOX:** la disponibilidad, redistribución y licencia de cada motor/voz deben verificarse antes de empaquetarlos.
- **Límite de RAM:** 1 GiB es objetivo/umbral de aviso para el proceso VTuberBot, no una garantía de todo el sistema ni de procesos externos.
- **Integración en main:** la PR de encuadre/editor debe evaluarse junto a los cambios ya presentes en main; no asumir que todo el historial de la rama de producto es seguro para fusionar.
