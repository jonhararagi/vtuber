# VTuberBot — escritorio VTuber local para Windows

VTuberBot está pensado para ser un programa de escritorio completo para gestionar avatar, audio, Twitch, comandos, comunidad, sorteos, minijuegos, moderación, automatizaciones, OBS, Telegram Web y diagnóstico desde una experiencia visual coherente.

**Objetivo de diseño:** una aplicación moderna con Dashboard y espacios de trabajo reales. No se considera terminado por mostrar estado RUNNING o por compilar.

## Especificación del producto

- [Especificación funcional, interfaz, carpetas y arquitectura objetivo](docs/PRODUCT_BLUEPRINT.md)
- [Especificación central](PROJECT_SPEC.md)
- [Roadmap de integración completa](ROADMAP.md)
- [Prompt oficial para GPT Obrero — implementación integral](PROMPTS.md)
- [Registro central de continuidad](docs/CONTINUIDAD_PROYECTO.md)

## Tecnología y restricciones

- C# / .NET 10 / WPF / SQLite.
- Windows como plataforma objetivo; equipo de referencia Ryzen 5 5600G y 16 GB RAM.
- Sin dependencia obligatoria de APIs de IA pagas.
- Integraciones Twitch y OBS opcionales, con autenticación y configuración explícitas.
- Los datos de usuario se guardan fuera del repositorio y del paquete de distribución.

## Estructura actual

La rama product/complete-vtuber-experience contiene la especificación integral del producto y será la rama de trabajo para implementar el alcance completo. El PR #9 de estabilización se conserva abierto y no se fusiona automáticamente.

## Compilar y ejecutar en Windows

```text
dotnet restore VtuberBot.sln
dotnet build VtuberBot.sln --configuration Release
dotnet test VtuberBot.sln --configuration Release
dotnet run --project src/VtuberBot.App --configuration Release
```

## Criterio de finalización

El CI verde certifica únicamente la ejecución automatizada para el commit correspondiente. La aceptación del producto requiere además la integración real de las funciones descritas en PRODUCT_BLUEPRINT.md, sin botones muertos ni conexiones simuladas, seguida de una validación global en Windows.
