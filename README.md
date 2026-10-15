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
- [Referencias visuales de VTubers y catálogo de cabello/ropa/accesorios por separado](docs/VTUBER_REFERENCIA_Y_BIBLIOTECA_MODULAR.md)
- [ChatSpeak Studio: voz aguda estilo anime, IA remota y modos manual/automático](docs/CHATSPEAK-STUDIO.md)
- [Guía sencilla para emitir en Twitch y YouTube](docs/STREAMING-YOUTUBE-TWITCH.md)

## Auditorías y continuidad

- [Análisis de Morphy y recomendaciones de flujo de agentes](docs/ANALISIS-MORPHY.md)
- [Continuidad del proyecto y evidencia por SHA](docs/CONTINUIDAD_PROYECTO.md)

## Tecnología y restricciones

- C# / .NET 10 / WPF / SQLite.
- Windows como plataforma objetivo; equipo de referencia Ryzen 5 5600G y 16 GB RAM.
- Sin dependencia obligatoria de APIs de IA pagas.
- Integraciones Twitch y OBS opcionales, con autenticación y configuración explícitas.
- Los datos de usuario se guardan fuera del repositorio y del paquete de distribución.

## Estructura actual

**Estado de ramas:** mientras la integración esté pendiente, la fuente de verdad del producto actualizado es `product/complete-vtuber-experience`. `main` es el destino de integración y puede quedar atrasada; no descargues su ZIP suponiendo que contiene las últimas funciones. La PR #18 documenta la integración pendiente y los cambios exclusivos de ambas ramas. El producto incluye dashboard con métricas CPU/RAM del proceso en vivo, navegación, **perfiles de avatar de ejemplo que puedes crear con un clic** (PNGTuber con sprites de reposo/habla y un ejemplo 2D por capas movibles), editor de capas con arrastre directo y sprites por expresión, overlay transparente con sprites de expresión configurables, OAuth PKCE con validación/renovación, chat IRC/EventSub con reconexión, comandos con alias, cooldown global o por usuario, preview segura y permisos por rol de Twitch (Todos/Suscriptor/VIP/Moderador/Broadcaster), puntos y ranking, sorteos con selección criptográfica, minijuegos con reglas y apuestas SQLite atómicas, moderación manual y reglas automáticas opcionales (desactivadas por defecto), timers persistentes, micrófono/VAD acústico y TTS SAPI con selección de voz/velocidad/volumen, OBS WebSocket v5 con configuración segura de destino RTMPS para Twitch o YouTube, guía para multistream opcional, Telegram Web, SQLite y diagnóstico. Avatar Studio ofrece PNGTuber, separación local experimental 2.5D, un taller paramétrico 3D que exporta GLB estático y carga de modelos VRM propios en un overlay transparente. El overlay VRM sincroniza Idle/Talking, requiere WebView2 e Internet para sus bibliotecas CDN, y no ofrece tracking facial ni edición de mallas. El modelo ONNX externo y su licencia están anotados en [avisos de terceros](docs/THIRD_PARTY_NOTICES.md). ChatSpeak Studio incluye modo manual con ChatGPT o Gemini web por copiar/pegar, perfiles editables de personalidad (incluido estilo Twitch), modo bot con Gemini API configurable, voz TTS y comandos de avatar limitados por allowlist. El Centro de directo también permite seleccionar voces japonesas locales de VOICEVOX y escuchar una muestra cuando el motor está instalado. Los proveedores remotos requieren sus propias credenciales y están sujetos a sus cuotas y condiciones de privacidad. Las funciones dependientes de cuentas/credenciales requieren configuración real; la CI no equivale a validación con tu cuenta. Los perfiles de ejemplo se generan localmente en el primer uso. El modo nativo es animación 2D por sprites/capas con movimiento ligero, no un rig Live2D ni tracking facial. PNGTuber Plus sigue siendo una aplicación externa que se instala por separado. El PR #9 se fusionó el 13 de octubre de 2026. La validación CI y manual siempre debe atribuirse al SHA exacto; las pruebas externas pendientes están registradas en docs/CONTINUIDAD_PROYECTO.md.

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
