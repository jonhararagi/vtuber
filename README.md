# VTuberBot — escritorio VTuber local para Windows

VTuberBot está pensado para ser un programa de escritorio completo para gestionar avatar, audio, Twitch, comandos, comunidad, sorteos, minijuegos, moderación, automatizaciones, OBS, Telegram Web y diagnóstico desde una experiencia visual coherente.

**Objetivo de diseño:** una aplicación moderna con Dashboard y espacios de trabajo reales, controles con estados visuales consistentes y una navegación que marca la sección activa. No se considera terminado por mostrar estado RUNNING o por compilar.

## Especificación del producto

- [Especificación funcional, interfaz, carpetas y arquitectura objetivo](docs/PRODUCT_BLUEPRINT.md)
- [Especificación central](PROJECT_SPEC.md)
- [Roadmap de integración completa](ROADMAP.md)
- [Prompt oficial para GPT Obrero — implementación integral](PROMPTS.md)
- [Registro central de continuidad](docs/CONTINUIDAD_PROYECTO.md)
- [Guía de configuración del candidato integral](docs/PRODUCT_USER_GUIDE.md)
- [ChatSpeak Studio: IA remota, voz femenina, comandos y modos manual/automático](docs/CHATSPEAK-STUDIO.md)
- [Guía sencilla para emitir en Twitch y YouTube](docs/STREAMING-YOUTUBE-TWITCH.md)

## Tecnología y restricciones

- C# / .NET 10 / WPF / SQLite.
- Windows como plataforma objetivo; equipo de referencia Ryzen 5 5600G y 16 GB RAM.
- Sin dependencia obligatoria de APIs de IA pagas.
- Integraciones Twitch y OBS opcionales, con autenticación y configuración explícitas.
- Los datos de usuario se guardan fuera del repositorio y del paquete de distribución.

## Estructura actual

La rama `product/complete-vtuber-experience` es el candidato de integración del producto. Incluye dashboard con métricas CPU/RAM del proceso en vivo, navegación, editor de capas con arrastre directo y sprites por expresión, overlay transparente con parpadeo mediante sprite Blink configurado, OAuth PKCE con validación/renovación, chat IRC/EventSub con reconexión, comandos con alias, cooldown global o por usuario, preview segura y permisos por rol de Twitch (Todos/Suscriptor/VIP/Moderador/Broadcaster), puntos y ranking, sorteos con selección criptográfica, minijuegos con reglas y apuestas SQLite atómicas, moderación manual y reglas automáticas opcionales (desactivadas por defecto), timers persistentes, micrófono/VAD acústico y TTS SAPI con selección de voz/velocidad/volumen, OBS WebSocket v5 con configuración segura de destino RTMPS para Twitch o YouTube, guía para multistream opcional, Telegram Web, SQLite y diagnóstico. El candidato también incluye ChatSpeak Studio: modo manual con ChatGPT por copiar/pegar, modo bot con Gemini API configurable, voz TTS remota de estilo femenino anime, comandos de avatar limitados por allowlist y comentario periódico del chat configurable. El nivel gratuito remoto tiene cuotas y condiciones de privacidad; consultar docs/CHATSPEAK-STUDIO.md. Las funciones dependientes de cuentas/credenciales requieren configuración real; el CI no equivale a validación con tu cuenta. Consultá la guía y la continuidad para las limitaciones pendientes. El PR #9 se conserva abierto y `main` no se modifica.

## Compilar y ejecutar en Windows

```text
dotnet restore VtuberBot.sln
dotnet build VtuberBot.sln --configuration Release
dotnet test VtuberBot.sln --configuration Release
dotnet run --project src/VtuberBot.App --configuration Release
```

## Descargar el candidato de Windows

No hace falta clonar ni compilar el repositorio para probar el paquete. Abrí [Windows CI de la rama de producto](https://github.com/jonhararagi/vtuber/actions/workflows/windows-ci.yml?query=branch%3Aproduct%2Fcomplete-vtuber-experience), elegí la ejecución cuyo **Head SHA coincide exactamente** con el commit que querés probar, y descargá el ZIP en la sección **Artifacts**. Extraelo a una carpeta normal y ejecutá `VtuberBot.App.exe`. Revisá `BUILD-INFO.txt` dentro del paquete. El artefacto es temporal (retención configurada de 14 días); no es una release pública. Streamer.bot, PNGTuber Plus y otras aplicaciones externas se instalan por separado.

## Criterio de finalización

El CI verde certifica únicamente la ejecución automatizada para el commit correspondiente. La aceptación del producto requiere además la integración real de las funciones descritas en PRODUCT_BLUEPRINT.md, sin botones muertos ni conexiones simuladas, seguida de una validación global en Windows.
