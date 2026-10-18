> **Guía del candidato actual — actualización 2026-10-18:** la navegación incluye Inicio, PNGTuber 2D, Avatar Studio, Voz y audio, OBS, **Bots y plataformas**, Comandos locales, Actividad/diagnóstico y Ajustes. En Bots y plataformas, la pestaña Twitch reutiliza la conexión real de chat/EventSub; Kick ofrece OAuth, consulta de canal y envío de prueba. El chat y los eventos entrantes de Kick por webhooks aún no están conectados. Para usar tu arte, importá la imagen propia, ajustá el encuadre y asigná reposo (Idle) y hablando (Talking). Para voz, seleccioná la ruta al run.exe de una instalación oficial de VOICEVOX Engine, iniciá el motor desde el botón **Iniciar VOICEVOX Engine en segundo plano**, comprobá los estilos reales y usá **Probar voz VOICEVOX**. El motor no se incluye dentro del paquete y se inicia ligado a loopback. Para OBS, conectá el WebSocket v5 desde el panel OBS. La vista de encuadre OBS es orientativa, no una fuente OBS sincronizada. La guía específica es [VOICEVOX local](VOICEVOX_LOCAL_SETUP.md).
>
> Los pasos históricos que describían Twitch/sociales como excluidos están desactualizados respecto al panel actual. La integración de Twitch reutiliza el cliente existente; Kick se limita por ahora a OAuth y API de salida. No sigas ejemplos antiguos como evidencia de que Kick recibe mensajes automáticamente. Consultá [la especificación definitiva](ESPECIFICACION_DEFINITIVA.md), [la guía actualizada de Avatar Studio](AVATAR_EDITOR_GUIDE.md) y [la auditoría técnica de Bots](TECHNICAL_AUDIT_BOTS_2026-10-18.md).

---

# Guía del candidato integral de VTuberBot

Esta guía corresponde al candidato integrado de VTuberBot. Las funciones externas no se consideran conectadas hasta completar la autenticación real. El proceso para prevenir regresiones está descrito en [Regla de Murphy y puertas de fiabilidad](REGLA_MURPHY_Y_PUERTAS_DE_FIABILIDAD.md).

## Qué es y dónde abrirlo

En **Avatar Studio**, abrí **Creador de waifu PNG por objetos**. Es un editor 2D local para combinar una ilustración propia con accesorios y expresiones independientes y exportar una composición PNG con transparencia. Los recursos de inicio son dibujos vectoriales originales generados por VTuberBot; el editor también permite importar piezas tuyas. No necesita clave de IA ni sube imágenes a la web.

## Histórico — Biblioteca inicial (no es el flujo principal actual)

El catálogo se genera en el equipo y contiene 40 recursos originales:

- **10 accesorios de cabeza:** orejas neko y de conejo, sombrero de bruja, boina pastel, gorra de streamer, mini corona, lazo, halo, corona de flores y gorra marinera.
- **9 conjuntos de ojos:** abiertos, felices cerrados, enojados, tristes, llorosos, de corazón, guiño, somnolientos y sorprendidos.
- **7 bocas:** sonrisa cerrada, boca abierta feliz, risa grande, sorpresa en O, boca triste, grito enojado y puchero.
- **10 efectos:** lágrimas, sudor, rubor, líneas de tristeza, símbolo de enfado, corazones, destellos, confusión, estrellas manga y sombra dramática.
- **4 fondos 1920×1080:** atardecer sakura, galaxia, habitación pastel y noche neón.

También podés importar PNG, JPG, JPEG, BMP y GIF propios. Los archivos se copian a la carpeta de datos local de VTuberBot; no se suben a Internet. Para recursos de terceros, revisá la licencia y los permisos antes de usarlos en una transmisión pública.

## Crear y editar un personaje

1. Pulsá **Importar PNG principal** y seleccioná la ilustración del personaje. La opción **Quitar fondo blanco conectado al borde** está activada por defecto.
2. El editor intenta hacer transparente el blanco claro conectado al borde de la imagen. Usa la conectividad del borde para conservar, cuando están aislados, los detalles blancos interiores como dientes, brillo de ojos o adornos. Es un método local de eliminación de matte, no una segmentación perfecta: el pelo muy claro o un contorno blanco conectado al fondo pueden requerir retoque manual.