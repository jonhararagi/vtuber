# VTuberBot — escritorio VTuber local para Windows

VTuberBot es una aplicación de escritorio para Windows centrada en editar tu propia imagen de avatar, operar las funciones compatibles de OBS Studio y usar voz local mediante VOICEVOX. El alcance aprobado y el estado real de cada capacidad se documentan en la [especificación definitiva](docs/ESPECIFICACION_DEFINITIVA.md).

**Objetivo de diseño:** una aplicación moderna con Dashboard y espacios de trabajo reales, controles con estados visuales consistentes y una navegación que marca la sección activa. No se considera terminado por mostrar estado RUNNING o por compilar.

## Especificación del producto

- [Especificación definitiva del producto y plan de integración](docs/ESPECIFICACION_DEFINITIVA.md)
- [Especificación funcional y arquitectura histórica](docs/PRODUCT_BLUEPRINT.md) (debe reconciliarse con el alcance definitivo)
- [Especificación central](PROJECT_SPEC.md)
- [Roadmap de integración completa](ROADMAP.md)
- [Prompt oficial para GPT Obrero — implementación integral](PROMPTS.md)
- [Registro central de continuidad](docs/CONTINUIDAD_PROYECTO.md)
- [Guía de configuración del candidato integral](docs/PRODUCT_USER_GUIDE.md)
- [Guía fácil para principiantes: cada panel y solución de problemas](docs/GUIA_PRINCIPIANTES.md)
- [Instalar e iniciar VOICEVOX Engine localmente](docs/VOICEVOX_LOCAL_SETUP.md)
- [Guía del editor de avatar y encuadre OBS](docs/AVATAR_EDITOR_GUIDE.md)

## Auditorías y continuidad

- [Análisis de Morphy y recomendaciones de flujo de agentes](docs/ANALISIS-MORPHY.md)
- [Auditoría técnica del panel de bots y reportes públicos](docs/TECHNICAL_AUDIT_BOTS_2026-10-18.md)
- [Continuidad del proyecto y evidencia por SHA](docs/CONTINUIDAD_PROYECTO.md)

## Tecnología y restricciones

- C# / .NET 10 / WPF / SQLite.
- Windows como plataforma objetivo; equipo de referencia Ryzen 5 5600G y 16 GB RAM.
- Sin dependencia obligatoria de APIs de IA pagas.
- OBS se conecta de forma opcional y explícita. Twitch incluye chat IRC/TLS, EventSub y moderación según scopes/permisos. Kick incluye OAuth, envío de prueba, moderación manual y reglas automáticas opt-in; también recibe eventos mediante un webhook local con firma verificada. Para que Kick entregue eventos reales, el usuario debe configurar una URL HTTPS pública/túnel, habilitar webhooks y registrar las suscripciones; guardar la URL o iniciar el receptor local no prueba conectividad pública.
- Los datos de usuario se guardan fuera del repositorio y del paquete de distribución.

## Estructura actual

**Estado de producto:** Twitch dispone de chat, EventSub y moderación conectados a sus APIs/permisos. Kick dispone de OAuth, consulta/envío de prueba, acciones de moderación y recepción de eventos por webhooks firmados. El receptor Kick escucha en loopback: requiere un túnel HTTPS público y suscripciones en Kick Developer; la entrega real desde Internet sigue dependiendo de esa configuración y no se considera probada por el CI. Los eventos verificados se guardan en una bandeja SQLite acotada antes de responder HTTP 200; la saturación rechaza nuevas entregas para permitir reintentos. Ninguna credencial externa se empaqueta. El producto prioritario también conserva el editor de imagen propia con encuadre/exportación, dos estados de avatar reposo/habla, operaciones compatibles de OBS, voz local y diagnóstico de memoria. La especificación definitiva ya declara Twitch, Kick y moderación como integraciones opcionales dentro del producto. Las funciones externas requieren autorización y configuración propia; la recepción de Kick depende de un túnel HTTPS público y su cola de eventos sigue siendo solo de memoria frente a cierres forzados. La guía y el registro de continuidad distinguen funciones probadas, parciales y pendientes. El encuadre de OBS es una previsualización orientativa, no un control en tiempo real de fuentes OBS.


## Compilar y ejecutar en Windows

```text
dotnet restore VtuberBot.sln
dotnet build VtuberBot.sln --configuration Release
dotnet test VtuberBot.sln --configuration Release
dotnet run --project src/VtuberBot.App --configuration Release
```

## Descargar el candidato de Windows

No hace falta clonar ni compilar el repositorio para probar el paquete. Abrí [Windows CI de `main`](https://github.com/jonhararagi/vtuber/actions/workflows/windows-ci.yml?query=branch%3Amain), elegí una ejecución con estado **success** y verificá que **Head SHA** coincida con el commit que querés probar. Descargá el ZIP de **Artifacts**, extraelo y ejecutá `VtuberBot.App.exe`. Revisá `BUILD-INFO.txt` y compará el SHA del paquete con el de la ejecución. El artefacto es temporal y no equivale a una release pública. VOICEVOX Engine requiere una instalación local compatible. Twitch y Kick requieren que el usuario autorice sus cuentas; para Kick hay que crear una aplicación propia y guardar sus credenciales localmente.

## Criterio de finalización

El CI verde certifica únicamente la ejecución automatizada para el commit correspondiente. La aceptación del producto requiere además la integración real de las funciones descritas en `docs/ESPECIFICACION_DEFINITIVA.md`, sin botones muertos ni conexiones simuladas, seguida de una validación global en Windows.
