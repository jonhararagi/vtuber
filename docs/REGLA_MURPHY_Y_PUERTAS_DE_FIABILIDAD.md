# Regla de Murphy aplicada a VTuberBot: puertas de fiabilidad

**Objetivo:** asumir que dispositivos, WebView2, red, proveedores de IA, voz, archivos y ventanas pueden fallar, y hacer visibles los fallos sin dejar estados falsos ni ocultarlos con reintentos ciegos.

Esta guía es un proceso y un conjunto de defensas específicas. **No es un sistema mágico que encuentra y arregla automáticamente cualquier bug fantasma.** No se debe declarar resuelta una incidencia sin evidencia.

## Puertas para cerrar un bug

1. **Reproducir y acotar.** Registrar la versión/SHA, pasos, estado anterior y esperado. No atribuir a la última edición un fallo de una versión antigua.
2. **Prueba de regresión.** La prueba debe fallar antes del arreglo cuando sea razonable automatizar el defecto. Incluir error, cancelación, cola vacía/llena, cierre y condiciones simultáneas cuando apliquen.
3. **Corregir la causa del estado.** Fuentes simultáneas no deben pisarse. Un error durante la limpieza no debe causar un cierre inesperado del worker.
4. **CI del commit exacto.** Restauración, build Release, pruebas Windows y artefacto deben corresponder al mismo SHA. CI anterior no valida cambios nuevos.
5. **Prueba manual donde corresponda.** CI no prueba completamente sonido real, WebView2, dispositivos físicos ni transparencia de OBS.
6. **Anotar pendientes.** Si no se reproduce, no existe una prueba o falla CI, decirlo. Nunca convertir “desconocido” en “correcto”.

## Defensas concretas

- Colas limitadas, validación de entradas, cancelación de voz y manejo de errores del worker.
- SafeEventDispatch separa suscriptores de eventos: un callback con errores no debe impedir avisar al resto.
- Las políticas del Core tienen pruebas unitarias. Windows CI compila, corre pruebas y publica un candidato portable cuando todo el flujo acaba bien.
- La voz sintética y el micrófono se combinan mediante AvatarTalkStatePolicy, para evitar que el silencio del micrófono mande el avatar a Idle mientras el bot aún habla.
- Un fallo al liberar el motor TTS queda registrado sin permitir que la excepción de limpieza escape como error no controlado del worker.

## Límites

- Estas defensas reducen clases conocidas de fallos; no prueban que no haya bugs.
- Los logs y el informe diagnóstico ayudan a investigar, pero no identifican automáticamente toda causa raíz.
- Los reintentos solo corresponden a operaciones recuperables, con límite; no deben esconder datos inválidos ni ejecutarse indefinidamente.
- Una compilación exitosa no valida la calidad visual del arte, los derechos de recursos externos ni una transmisión real.
- No declarar resuelta una falla fantasma sin caso de reproducción, prueba relevante o explicación honesta del límite de validación.

## Registro de validación

En cada arreglo documentar SHA exacto, pruebas y resultado, enlace a Windows CI, artefacto si existe y prueba manual pendiente. El registro central es docs/CONTINUIDAD_PROYECTO.md.
