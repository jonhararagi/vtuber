# Auditoría técnica externa y correcciones — 17 de octubre de 2026

## Alcance

Se contrastó el estado de VTuberBot con fuentes públicas primarias que publican incidencias, cambios de compatibilidad y soluciones. Esto es una revisión de riesgos orientada al código actual, no una garantía de que se hayan descubierto todos los errores ni una prueba dinámica en el PC del usuario.

## Correcciones aplicadas en el repositorio

### 1. Carrera intermitente en la prueba de Streamer.bot

**Síntoma:** Windows CI había fallado una prueba que esperaba que el cliente entrara en estado `ProtocolError` tras recibir JSON malformado; el servidor de la prueba podía enviar ese JSON antes de que el cliente terminara de procesar el saludo.

**Solución aplicada:** PR #31 sustituyó el retraso fijo de 100 ms por una señal `TaskCompletionSource`. El servidor de prueba espera la confirmación de que `ConnectAsync` terminó y después envía el mensaje malformado. Se conservaron los asertos de protocolo en lugar de ocultar el error con un reintento.

- PR: https://github.com/jonhararagi/vtuber/pull/31
- Run de Windows CI del SHA de la PR: https://github.com/jonhararagi/vtuber/actions/runs/38957037580
- Merge commit actual de main tras esa corrección: https://github.com/jonhararagi/vtuber/commit/6ecc22d8866bfc79cbe00cd8419a02f12103a15b

### 2. Tamaño incorrecto de la boca como sticker

**Problema funcional:** en un overlay tradicional, una imagen se estira a la ventana completa; una boca recortada podía quedar gigantesca y anclada al centro.

**Solución portada a la rama de integración:** calcula el tamaño del sticker con la misma escala uniforme del PNG base, alinea el sticker al centro para que sus coordenadas X/Y sean predecibles y vuelve a calcular el tamaño si cambia el viewport. Las pruebas verifican proporciones de un PNG recortado y rechazan dimensiones no finitas o inválidas.

### 3. Persistencia de boca y actualización de SQLite

**Problema funcional:** la base de datos antigua no tenía dónde recordar los archivos de boca abierta/cerrada.

**Solución portada:** ProductStore usa versión de esquema 2, agrega columnas opcionales con `EnsureColumn` y guarda rutas normalizadas únicamente si los archivos existen. Se añadió una prueba para guardar, reabrir, borrar las dos asignaciones y rechazar una ruta inexistente. La validación de backups antiguos no exige esas columnas nuevas.

### 4. Movimiento controlado y estado accesible

El movimiento usa valores finitos acotados: vibración hasta 18 px, rebote hasta 12 px y flotación hasta 6 px. `Reducir movimiento` deja a cero los desplazamientos. Es un efecto simple de todo el PNG en dos estados faciales (`Idle`/`Talking`), no una implementación completa de físicas de sprites de terceros.

## Fuentes públicas consultadas

1. **Microsoft Learn — cambios importantes de .NET 10:** https://learn.microsoft.com/es-es/dotnet/core/compatibility/10  
   Lista cambios de comportamiento y de fuente, incluidos los que afectan a WPF. Se usa como lista de control antes de actualizar SDK/runtime; no se atribuyó automáticamente a VTuberBot un fallo que no se haya reproducido.

2. **dotnet/wpf — incidencia #11678, regresiones de WPF con SDK 10.0.200/10.0.202:** https://github.com/dotnet/wpf/issues/11678  
   Publicada el 20 de abril de 2026; describe fallos de inicio/publicación *single-file* y menciona SDK anteriores que sí funcionaban. Se registró como riesgo externo, no como defecto demostrado del candidato: el workflow de VTuberBot usa `dotnet-version: 10.0.x` y genera un paquete autocontenido **de varios archivos**, no un EXE *single-file*. No se fijó un SDK antiguo sin reproducir el síntoma; fijarlo también podría impedir recibir parches posteriores.

3. **Microsoft Learn — global.json y roll-forward de SDK:** https://learn.microsoft.com/es-es/dotnet/core/tools/global-json  
   Explica cómo controlar la selección del SDK. Antes de fijar una versión conviene verificar el fallo real y mantener alineados el SDK local y Windows CI.

4. **OBS Project — versiones y cambios de obs-websocket:** https://github.com/obsproject/obs-websocket/releases  
   Las notas del proyecto distinguen protocolo 4.x y 5.x; v5 incluye autenticación habilitada por defecto y sus lanzamientos registran correcciones reales de fallos y cierres. VTuberBot debe usar el protocolo v5 del OBS moderno y conservar la autenticación; no se debe recomendar deshabilitar la contraseña para resolver una conexión fallida.

5. **Windows CI propio:** https://github.com/jonhararagi/vtuber/actions  
   La CI confirma compilación, pruebas y empaquetado para un SHA concreto, no que el micrófono físico, la autenticación OBS, la escena de captura o una transmisión real funcionen en la computadora del usuario.

## Hallazgos que siguen sin poder darse por cerrados

- La rama de integración debe pasar Windows CI en su SHA exacto y después de fusionarse se debe verificar otra vez la CI de `main`.
- Falta una sesión manual de Windows para probar: calibración visual de la boca con el arte real, detección del micrófono, conexión WebSocket autenticada a OBS, fuente de captura en la escena y una grabación corta.
- La publicación de varios archivos no implica que exista un modelo, voicebank o audio profesional incluido; sus licencias e instalación siguen siendo independientes.
- No se comprobó captura de tráfico real, ACL de Windows en la instalación del usuario ni rendimiento en vivo; no se declara auditoría de seguridad completa.

## Criterio de cierre

No considerar estable un cambio solo porque compila una máquina del desarrollador. La evidencia esperada es Restore + Build Release + pruebas + Publish + validación del ZIP en Windows CI para el SHA exacto, seguida de verificación de la CI para el merge en `main` y anotación explícita de la validación manual pendiente.


### 5. Retiro de Node 20 en GitHub Actions: actualización aplicada

La revisión del log de Windows CI mostró una advertencia real: `actions/checkout@v4` y `actions/setup-dotnet@v4` estaban utilizando los runtimes antiguos basados en Node 20. GitHub anunció que Node 20 dejó de estar disponible para JavaScript Actions el **23 de septiembre de 2026** y exige actualizar las acciones a releases compatibles con Node 24.

**Cambio aplicado en esta integración:** `.github/workflows/windows-ci.yml` pasa de `actions/checkout@v4` a `actions/checkout@v5` y de `actions/setup-dotnet@v4` a `actions/setup-dotnet@v6`. La función del workflow no cambia: sigue usando un runner Windows, instala el SDK .NET 10 y ejecuta restore/build/tests/publish. La CI debe comprobar que las nuevas versiones de las acciones funcionan con el runner y el flujo completos.

Fuentes oficiales:
- GitHub Changelog: retirada de Node 20, 23/09/2026: https://github.blog/changelog/2026-09-23-node-20-is-no-longer-available-in-github-actions/
- Acción checkout v5 usa Node 24: https://github.com/actions/checkout
- setup-dotnet v5 y posteriores utilizan Node 24; release actual consultada: https://github.com/actions/setup-dotnet/releases
