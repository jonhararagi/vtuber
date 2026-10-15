# Análisis de Morphy y aplicación al desarrollo de VTuberBot

**Fecha de consulta:** 2026-10-15  
**Producto analizado:** [Morphy](https://www.appmorphy.com/) y su [documentación oficial](https://www.appmorphy.com/docs).

## Qué es Morphy

Morphy es un espacio de trabajo de escritorio para coordinar agentes de IA y proyectos en un mismo lienzo. No es un creador de avatares ni un motor de VTuber. Su utilidad para VTuberBot está en el proceso de desarrollo: permite mantener varios chats/agentes, carpetas de proyecto, terminal y navegador en una vista compartida.

Según la documentación oficial consultada, integra distintos proveedores y herramientas de agentes (entre ellos Codex, Gemini mediante proveedores compatibles, Claude y Ollama según la configuración disponible), permite asociar chats a carpetas de proyecto y conserva el trabajo localmente. El plan gratuito publicado limita la concurrencia a tres agentes y dos proyectos; las condiciones comerciales pueden cambiar, por lo que hay que verificarlas en su página de planes antes de decidir.

## Qué conviene aprender de Morphy para VTuberBot

1. **Separar roles de trabajo.** Un agente audita arquitectura y riesgos; otro implementa un bloque acotado; un tercero revisa de forma independiente. No deben editar simultáneamente el mismo archivo o rama.
2. **Mantener el contexto del proyecto visible.** Un único documento de continuidad, especificación, roadmap y criterios de aceptación evita que cada conversación reinvente decisiones.
3. **Mostrar evidencia, no solo mensajes de éxito.** Cada bloque debe indicar SHA exacto, CI, pruebas, artefacto y qué validación externa sigue pendiente.
4. **Trabajar por bloques integrables.** Cada bloque debe tener alcance, rutas, pruebas y criterio de cierre; al integrar, ejecutar CI sobre el SHA final, no sobre un commit anterior.
5. **Separar herramientas de desarrollo del producto.** Morphy puede ayudar a construir VTuberBot, pero no debe ser dependencia de ejecución de la aplicación. VTuberBot debe seguir arrancando sin Morphy y sin una suscripción de IA.

## Qué NO conviene copiar literalmente

- No introducir un segundo orquestador de agentes dentro de VTuberBot solo porque Morphy coordina agentes. El producto necesita coordinar eventos de Twitch, voz, avatar y aplicaciones de streaming; es un problema distinto.
- No permitir que agentes autónomos cambien `main`, publiquen releases o alteren credenciales sin una política explícita.
- No confiar en que varios agentes ejecutando tareas a la vez garanticen más calidad. Sin límites de archivos, revisión y CI pueden producir cambios incompatibles.
- No usar Morphy como sustituto de la prueba real con Twitch, OBS, Streamer.bot, PNGTuber Plus o los dispositivos del usuario.

## Recomendación concreta para este repositorio

Mantener GitHub como fuente de verdad y usar el siguiente flujo cuando se disponga de Morphy u otro espacio de agentes:

- **Auditor:** lee `PROJECT_SPEC.md`, `ROADMAP.md`, `docs/CONTINUIDAD_PROYECTO.md` y el código; entrega hallazgos con rutas y evidencia, sin modificar archivos.
- **Implementador:** toma un hallazgo prioritario por rama o bloque, agrega pruebas y actualiza documentación.
- **Revisor:** inspecciona el diff y busca regresiones, estados falsos, seguridad, accesibilidad y casos de error.
- **Integración:** ejecuta Windows CI sobre el SHA final; conserva el artefacto de ese SHA y marca como pendiente cualquier comprobación que requiera cuentas, periféricos o aplicaciones reales.

## Fuentes oficiales

- [Morphy — sitio oficial](https://www.appmorphy.com/)
- [Morphy — documentación](https://www.appmorphy.com/docs)
- [Morphy — planes](https://www.appmorphy.com/pricing)
- [Morphy — preguntas frecuentes](https://www.appmorphy.com/docs/faq)

**Conclusión:** Morphy puede ser útil como entorno para coordinar el desarrollo, pero no arregla automáticamente las brechas de VTuberBot ni reemplaza las pruebas. La mejora de mayor valor es adoptar un proceso de agentes con tareas independientes, revisión del diff y evidencia de CI por SHA.
