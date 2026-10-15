# Auditoría de brechas de producto — VTuberBot

**Fecha:** 2026-10-15  
**Rama objetivo:** product/complete-vtuber-experience  
**Método:** lectura de README, PRODUCT_BLUEPRINT, PROJECT_SPEC, ROADMAP, continuidad y documentación de integraciones. Esta lista distingue límites documentados de validaciones pendientes; no presenta una función como ausente solo porque no se haya ejecutado manualmente.

## Resumen ejecutivo

El repositorio ya contiene una base considerable de producto: WPF/.NET 10, persistencia SQLite, varios módulos de Twitch, OBS, chat/automatización, Avatar Studio, perfiles de personalidad, TTS local y overlay VRM. La brecha principal no es solo añadir pantallas: es demostrar que las rutas de usuario funcionan juntas y ampliar la biblioteca artística modular con recursos variados y legalmente utilizables.

## Hallazgos priorizados

| Prioridad | Área | Estado según documentación | Trabajo recomendado |
|---|---|---|---|
| P0 | Validación global Windows | Pendiente para integraciones que requieren cuentas, hardware o apps externas | Ejecutar una sola aceptación global sobre el candidato final; registrar cada resultado y el SHA exacto. |
| P0 | Evidencia CI | El nuevo commit de investigación aún no tiene estado de CI disponible en esta revisión | Esperar CI para el SHA final y no reutilizar resultados de otro commit. |
| P1 | Biblioteca artística | Existe catálogo modular y galería chibi, pero la continuidad la describe como arte vectorial local de referencia, no como biblioteca amplia de ilustraciones profesionales | Añadir gestor de recursos locales con etiquetas, favoritos, categorías, previsualización, asignación de capas y metadatos de origen/licencia. |
| P1 | Diversidad visual | Existen opciones y presets, pero no se demuestra una biblioteca extensa de prendas, peinados y siluetas artísticas de calidad heterogénea | Priorizar formas y diseños diferenciados: ropa casual, fantasy, idol, formal, streetwear, armaduras, capas, accesorios y variantes de cuerpo/rostro. No limitarse a recolorear una base. |
| P1 | Trazabilidad de licencias | Ya hay documentos de referencias y avisos de terceros; falta que cada recurso nuevo tenga metadatos y estado de revisión consistente | Registrar autor, URL, licencia, permiso comercial, redistribución y atribución. Mantener “pendiente de revisión” cuando no haya evidencia. |
| P1 | Chat → IA → voz → avatar | Hay modo manual con ChatGPT/Gemini web, modo Gemini API opcional, perfiles de personalidad y TTS; la continuidad no certifica la ruta completa en un stream real | Probar de extremo a extremo con credenciales propias opcionales, errores, timeout, límites, privacidad, cancelación y fallback local. |
| P1 | Overlay VRM | Importación y overlay transparente documentados; requiere WebView2 e Internet para bibliotecas CDN | Validar un VRM real, Idle/Talking, transparencia, rendimiento y recuperación de error. Documentar claramente que no incluye tracking facial ni edición de mallas. |
| P1 | Integraciones externas | Twitch, OBS, Streamer.bot, PNGTuber Plus, Telegram y VOICEVOX necesitan entorno/configuración para prueba real | Validar una por una en una única sesión de aceptación; distinguir “no configurado”, “no instalado”, “conectado” y “falló”. |
| P2 | Generación de imágenes desde la app | No hay evidencia en los documentos revisados de una integración de generación de imágenes que sea local, gratuita y completa dentro de VTuberBot | Mantener primero importación local. Evaluar más adelante API oficial opcional o backend local optativo, sin scraping ni credenciales obligatorias. |
| P2 | Compatibilidad/performance IA local | El equipo objetivo tiene Ryzen 5 5600G y 16 GB RAM; el rendimiento de generadores depende del modelo y GPU disponible | No instalar modelos grandes automáticamente ni prometer velocidad sin medir RAM/VRAM y tiempo de generación. |

## Investigación externa guardada

La carpeta docs/investigacion-arte-ia/ contiene el catálogo de PixAI, NovelAI, Tensor.Art, SeaArt, Civitai, Hugging Face, ComfyUI y Krita AI Diffusion, con enlaces oficiales, capacidades, cautelas de licencia y un flujo de trabajo recomendado.

## Recomendación de ejecución

1. Completar el catálogo de recursos y metadatos antes de automatizar generación remota.
2. Añadir recursos originales de variedad real y probar combinaciones, no solo recoloración.
3. Revisar la implementación actual de la biblioteca y mapear cada requisito de PRODUCT_BLUEPRINT a una ruta de interfaz, prueba y evidencia.
4. Cerrar defectos encontrados y ejecutar CI en el SHA final.
5. Entregar un único candidato para validación global Windows; no interrumpir al usuario con pruebas pequeñas por cada commit.

## Criterio de cierre

Una fila solo se marca como completada cuando hay evidencia reproducible: código revisado, pruebas relevantes y, para integraciones externas, resultado de prueba real. CI verde no prueba por sí sola Twitch, OBS, audio físico, Telegram, VOICEVOX, Streamer.bot, PNGTuber Plus ni un modelo VRM concreto.
