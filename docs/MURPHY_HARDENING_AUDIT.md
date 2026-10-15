# Auditoría de Murphy — VTuberBot

**Fecha:** 2026-10-15  
**Rama auditada:** `product/complete-vtuber-experience`  
**HEAD de referencia:** `fb4ec9f96457bb0d619561cf28e4403600289a11`  
**Alcance:** revisión estática dirigida de arranque/cierre, colas sociales, proveedores de IA, voz local, OBS WebSocket, configuración y CI. No equivale a una prueba de ejecución en Windows ni a una auditoría completa de todos los archivos.

## Regla de trabajo

Para cada función crítica hay que asumir: dependencia ausente, fallo a mitad de operación, respuesta lenta, evento duplicado, datos inválidos, falta de permisos, disco lleno, apagado concurrente y reintento. Una característica solo se considera validada cuando hay evidencia reproducible; que compile o tenga un botón no prueba la integración externa real.

## Hallazgos verificados en el código

### Salvaguardas ya presentes

- **Cierre de ventana:** `MainWindow.OnClosing` evita cerrar inmediatamente y ejecuta una rutina asíncrona que detiene timers, libera recursos por separado y registra fallos de cierre sin dejar que un error de logging impida salir.
- **Cola social:** `SocialBotService` usa un canal acotado de 64 trabajos y cancelación de generaciones activas; el cierre espera hasta cinco segundos y registra si el trabajador no termina a tiempo.
- **Proveedor IA predeterminado:** `ProviderPreferenceDefaults` considera remoto solo el valor guardado explícitamente como `remote`; los valores ausentes o desconocidos resuelven a local. La UI actual en esta rama usa local como predeterminado. Esto corrige el comportamiento descrito en partes antiguas del historial de continuidad.
- **OBS WebSocket:** exige autenticación antes de reportar conexión, pone límite de 15 segundos a las solicitudes, limita cada mensaje entrante a 1 MiB, correlaciona respuestas por ID y falla las solicitudes pendientes cuando la sesión se interrumpe.
- **Paquete Windows:** el workflow publica un ZIP autocontenido, escribe el SHA de origen y comprueba que no se incluyan bases SQLite, logs o archivos de credenciales detectables por los patrones actuales.

Estas son defensas visibles en el código, no una afirmación de que todos los escenarios hayan sido ejecutados con servicios reales.

## Riesgos prioritarios y corrección/validación necesaria

| Prioridad | Riesgo de Murphy | Estado con evidencia actual | Acción requerida |
|---|---|---|---|
| P0 | Un cambio en rama equivocada o un merge parcial pierde trabajo | PR #18 abierto, sin merge automático; no se ha fusionado | Resolver el conflicto conservando ambos historiales, revisar ramas exclusivas y validar el SHA final antes de integrar |
| P0 | Un evento o comando duplicado ejecuta dos veces una acción externa | La arquitectura tiene coordinador de eventos, pero no se ha validado extremo a extremo cada acción | Añadir pruebas de idempotencia para reconexión, reintentos y eventos repetidos; definir cuáles acciones no se pueden repetir |
| P0 | Token o clave expuesta en logs/paquete | CI filtra nombres de archivos sensibles y existen almacenes cifrados; esto no detecta secretos embebidos en texto o excepciones | Revisar todos los logs y mensajes de error, agregar escaneo de secretos y pruebas con valores centinela; rotar cualquier secreto real que se haya publicado |
| P1 | SQLite bloqueada, disco lleno o cierre durante escritura | Hay pruebas de persistencia/concurrencia, pero esta revisión no ejecutó fallos de disco ni terminación forzada | Probar bloqueo, reintento acotado, recuperación al reiniciar, límites de historial y error visible sin corrupción |
| P1 | Twitch/OBS/Streamer.bot se cae durante una acción | OBS tiene reconexión; el resto requiere pruebas específicas de servidor ausente, token inválido, timeout y reconexión | Pruebas de contrato con servidor falso y verificación manual de cada servicio real; no declarar conectado hasta handshake confirmado |
| P1 | Cola llena bloquea UI o descarta un evento crítico | Las colas sociales y de voz son acotadas, pero faltan resultados de pruebas de estrés en Windows | Probar ráfagas, saturación y cancelación; UI no debe esperar operaciones de red y los eventos críticos deben tener política explícita |
| P1 | Audio/IA se cuelga y bloquea el cierre | Existe timeout de espera al cerrar bot social; no se ha verificado que cada proveedor respete cancelación | Probar proveedor que nunca responde, motor local ausente, voz no disponible y cierre durante reproducción; todo debe finalizar o dejar diagnóstico claro |
| P1 | Imagen corrupta, enorme o perfil parcial rompe Avatar Studio | Hay límites de tamaño/dimensiones y validación de importación en varias rutas; falta recorrido visual completo | Probar archivo truncado, extensión falsa, dimensiones límite, imagen con alfa, perfil sin capas y asignación repetida; mantener un preset de recuperación |
| P1 | Una preferencia antigua/dañada activa modo remoto o rompe inicio | El selector usa valor seguro local si no hay una preferencia válida | Probar null, vacío, valor desconocido, JSON/configuración corrupta y actualización desde versión anterior; preservar ajustes válidos |
| P2 | Fuga de memoria, crecimiento infinito de logs/contexto o temporizadores duplicados | Hay colas acotadas y limpieza periódica en algunos subsistemas; no hay medición sostenida en esta revisión | Prueba de estrés de 30–60 minutos, límites explícitos por cada colección y verificación de que navegar entre paneles no duplica timers/subscripciones |
| P2 | DPI, escala, ventana pequeña o reducción de movimiento rompe la interfaz | No se hizo inspección visual en esta revisión | Probar escalas de Windows 100/125/150/200 %, resolución mínima, redimensionado y movimiento reducido |
| P2 | ZIP correcto pero app no arranca en una PC limpia | CI publica self-contained y prueba contenido del ZIP; no sustituye el arranque de escritorio en un Windows limpio | Ejecutar paquete extraído en perfil Windows limpio sin SDK .NET, verificar primer inicio, permisos, rutas de datos y cierre |
| P2 | Guía o continuidad dice algo que el código ya cambió | La continuidad histórica incluye diagnósticos de proveedores remotos predeterminados que no describen el HEAD actual | Corregir y fechar la documentación por SHA, manteniendo registro histórico y distinguiendo hallazgo antiguo de estado actual |

## Matriz de pruebas de fallo que debe pasar el candidato final

1. Arranque sin Internet, sin credenciales, sin OBS, sin Streamer.bot y sin motor de IA local.
2. Reinicio de Twitch/EventSub/OBS/Streamer.bot durante ráfaga de mensajes y durante ejecución de acción.
3. Entrega repetida de un mismo evento, orden alterado y reconexión inmediata.
4. Proveedor de IA que devuelve 401, 429, 500, JSON inválido o nunca responde; cancelación al pausar y al cerrar.
5. Motor de voz ausente, dispositivo de audio desconectado y cola llena.
6. SQLite ocupada por otro proceso, ruta no escribible, espacio insuficiente y reinicio tras escritura interrumpida.
7. Importación de imagen corrupta, imagen de dimensiones máximas, archivo con extensión falsa y perfil incompleto.
8. Configuración vacía/obsoleta/dañada; clave incorrecta; ningún secreto aparece en logs.
9. Doble clic en acciones de inicio, detener, guardar, sorteo y reconexión.
10. Ejecutar la app desde ZIP autocontenido en Windows limpio y confirmar que el SHA de `BUILD-INFO.txt` coincide con el commit publicado.
11. Estrés sostenido: chat de alta frecuencia, historial llegando al límite, navegación repetida por páginas y cierre ordenado.
12. Validación visual en distintas escalas DPI y comprobación de que todos los estados tienen texto además de color.

## Criterios de cierre

- [ ] Cada P0 cerrado con evidencia.
- [ ] Pruebas unitarias y de integración pasan en el SHA final exacto.
- [ ] Windows CI verde para ese SHA, sin confundirlo con el estado de un commit anterior.
- [ ] ZIP autocontenido generado por ese mismo SHA y con `BUILD-INFO.txt`.
- [ ] Arranque, cierre y recorridos principales comprobados en Windows; integraciones externas probadas por separado.
- [ ] Ningún fallo se presenta como resuelto solo porque se añadió documentación o un test no ejecutado.

## Estado honesto de esta auditoría

Se verificaron estáticamente las defensas enumeradas en “Salvaguardas ya presentes”. No se ejecutaron pruebas .NET en este entorno ni se probaron cuentas, audio, OBS o Twitch reales. La CI del candidato debe comprobarse para el SHA nuevo que resulte de los cambios. Esta auditoría es un mapa de riesgos y criterios reproducibles, no una certificación de que todos los riesgos ya estén corregidos.
