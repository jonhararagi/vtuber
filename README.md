# VTuberBot — escritorio VTuber local para Windows

VTuberBot está pensado para ser un programa de escritorio completo para gestionar avatar, audio, Twitch, comandos, comunidad, sorteos, minijuegos, moderación, automatizaciones, OBS, Telegram Web y diagnóstico desde una experiencia visual coherente.

**Objetivo de diseño:** una aplicación moderna con Dashboard y espacios de trabajo reales. No se considera terminado por mostrar estado RUNNING o por compilar.

## Especificación del producto

- [Especificación funcional, interfaz, carpetas y arquitectura objetivo](docs/PRODUCT_BLUEPRINT.md)
- [Especificación central](PROJECT_SPEC.md)
- [Roadmap de integración completa](ROADMAP.md)
- [Prompt oficial para GPT Obrero — implementación integral](PROMPTS.md)
- [Registro central de continuidad](docs/CONTINUIDAD_PROYECTO.md)
- [Guía de configuración del candidato integral](docs/PRODUCT_USER_GUIDE.md)

## Tecnología y restricciones

- C# / .NET 10 / WPF / SQLite.
- Windows como plataforma objetivo; equipo de referencia Ryzen 5 5600G y 16 GB RAM.
- Sin dependencia obligatoria de APIs de IA pagas.
- Integraciones Twitch y OBS opcionales, con autenticación y configuración explícitas.
- Los datos de usuario se guardan fuera del repositorio y del paquete de distribución.

## Estructura actual

La rama `product/complete-vtuber-experience` es el candidato de integración del producto. Incluye dashboard y navegación, capas de avatar/overlay, OAuth PKCE + chat IRC de Twitch, comandos con cooldown, puntos y ranking, sorteos, minijuegos locales, reglas de moderación en observación, mensajes programados, TTS/micrófono local, OBS WebSocket v5 para escenas, Telegram Web, SQLite y diagnóstico. Consultá la guía y la continuidad para las limitaciones pendientes. El PR #9 se conserva abierto y `main` no se modifica.

## Compilar y ejecutar en Windows

```text
dotnet restore VtuberBot.sln
dotnet build VtuberBot.sln --configuration Release
dotnet test VtuberBot.sln --configuration Release
dotnet run --project src/VtuberBot.App --configuration Release
```

## Criterio de finalización

El CI verde certifica únicamente la ejecución automatizada para el commit correspondiente. La aceptación del producto requiere además la integración real de las funciones descritas en PRODUCT_BLUEPRINT.md, sin botones muertos ni conexiones simuladas, seguida de una validación global en Windows.
