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
- [Instalar e iniciar VOICEVOX Engine localmente](docs/VOICEVOX_LOCAL_SETUP.md)
- [Guía del editor de avatar y encuadre OBS](docs/AVATAR_EDITOR_GUIDE.md)

## Auditorías y continuidad

- [Análisis de Morphy y recomendaciones de flujo de agentes](docs/ANALISIS-MORPHY.md)
- [Continuidad del proyecto y evidencia por SHA](docs/CONTINUIDAD_PROYECTO.md)

## Tecnología y restricciones

- C# / .NET 10 / WPF / SQLite.
- Windows como plataforma objetivo; equipo de referencia Ryzen 5 5600G y 16 GB RAM.
- Sin dependencia obligatoria de APIs de IA pagas.
- OBS se conecta de forma opcional y explícita; Twitch y sus funciones sociales quedan fuera del alcance aprobado.
- Los datos de usuario se guardan fuera del repositorio y del paquete de distribución.

## Estructura actual

**Estado de producto:** el alcance vigente excluye la conexión directa de Twitch y sus funciones sociales, Telegram Web, Streamer.bot y PNGTuber Plus como dependencias. El producto prioritario es el editor de imagen propia con encuadre/exportación, dos estados de avatar reposo/habla, control real de operaciones compatibles de OBS, voz local VOICEVOX, configuración persistente, seguridad y diagnóstico de memoria. Algunas funciones históricas siguen presentes en el código o en la documentación y no deben considerarse aceptadas hasta que se auditen y se retiren de la UI de forma segura. La guía y el registro de continuidad deben distinguir funciones probadas, parciales y pendientes. El encuadre de OBS es una previsualización orientativa, no un control en tiempo real de fuentes OBS.


## Compilar y ejecutar en Windows

```text
dotnet restore VtuberBot.sln
dotnet build VtuberBot.sln --configuration Release
dotnet test VtuberBot.sln --configuration Release
dotnet run --project src/VtuberBot.App --configuration Release
```

## Descargar el candidato de Windows

No hace falta clonar ni compilar el repositorio para probar el paquete. Abrí [Windows CI de `main`](https://github.com/jonhararagi/vtuber/actions/workflows/windows-ci.yml?query=branch%3Amain), elegí una ejecución con estado **success** y verificá que **Head SHA** coincida con el commit que querés probar. Descargá el ZIP de **Artifacts**, extraelo y ejecutá `VtuberBot.App.exe`. Revisá `BUILD-INFO.txt` y compará el SHA del paquete con el de la ejecución. El artefacto es temporal y no equivale a una release pública. VOICEVOX Engine requiere una instalación local compatible; Twitch, Streamer.bot y PNGTuber Plus no son dependencias del alcance actual.

## Criterio de finalización

El CI verde certifica únicamente la ejecución automatizada para el commit correspondiente. La aceptación del producto requiere además la integración real de las funciones descritas en `docs/ESPECIFICACION_DEFINITIVA.md`, sin botones muertos ni conexiones simuladas, seguida de una validación global en Windows.
