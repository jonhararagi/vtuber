# Arquitectura de IA opcional — VTuberBot

**Estado:** decisión arquitectónica para la rama product/complete-vtuber-experience. Este documento define el contrato de diseño; no afirma que todos los proveedores o adaptadores estén implementados.

## Objetivo

Mantener VTuberBot útil sin clave de IA, suscripción ni conexión remota. La IA ayuda donde aporta valor, pero no controla el ciclo de vida, no sustituye los comandos/reglas normales y no es requisito para usar el avatar, el audio básico, los sorteos, los puntos, las automatizaciones o las integraciones de streaming.

## Modos de operación

| Modo | Predeterminado | Comportamiento esperado |
|---|---:|---|
| Desactivada | Sí | No se hacen llamadas ni se carga un modelo. Todas las funciones no-IA siguen disponibles. |
| Local | No | Un adaptador local opcional ejecuta el análisis/generación en el equipo. La app muestra runtime/modelo ausente, recursos y errores; no descarga pesos sin permiso. |
| Remota oficial | No | Adaptador de una API oficial documentada, con configuración explícita, credenciales seguras, límites y aviso antes de enviar texto. Puede tener coste o cambiar sus cuotas gratuitas. |
| Web manual | No | Abre ChatGPT/Gemini para que la persona interactúe manualmente. No se anuncia como integración automática ni se lee/inserta contenido en la página. |

## Límites de arquitectura

1. VtuberBot.Core no conoce proveedores, SDKs, tokens, WebView2 ni endpoints. Expone contratos pequeños para casos de uso, cancelación y resultados estructurados si se incorpora una capacidad de IA.
2. Los adaptadores viven fuera del núcleo, se pueden omitir del paquete y se registran como capacidades opcionales. La ausencia de un adaptador no impide arrancar la aplicación.
3. Los casos de uso primero consideran una solución determinista/local. No se llama al modelo para comandos, cooldowns, moderación o sorteos que ya tienen reglas explícitas.
4. La UI distingue claramente Desactivada, Local no disponible, Requiere configuración, Conectando, Disponible, Límite alcanzado y Error. Nunca muestra conexión exitosa basándose en que una web abrió.
5. Un fallo de IA no debe perder ni duplicar acciones del chat: los efectos externos deben ejecutarse solo tras validación y con una política de reintentos idempotente definida por el caso de uso.

## Privacidad, credenciales y gasto

- No se transmite texto, historial del chat, prompts, nombres de usuario ni contenido de archivos sin acción informada y configuración del usuario.
- Antes de la primera solicitud remota, informar proveedor, clase de datos enviada, conexión y posibles límites/costes. No usar fallback remoto automático desde un modelo local.
- Guardar secretos en Windows Credential Manager/DPAPI. No registrar claves, tokens, cabeceras, cookies ni contenido sensible en logs o diagnóstico.
- Aplicar timeout, cancelación, límite de caracteres/tokens, cola acotada, cooldown y máximo de solicitudes configurable. Sin procesos de IA en segundo plano ocultos.
- La modalidad local debe informar consumo y requisitos; con 16 GB RAM no se debe iniciar ni descargar un modelo grande automáticamente.

## Estrategia de implementación

1. Auditar capacidades IA ya presentes y etiquetar cada una como local, remota, web manual, experimental o pendiente; no asumir que una etiqueta de UI prueba integración real.
2. Mantener las reglas, comandos y casos de uso tradicionales independientes del proveedor.
3. Definir interfaces pequeñas y tests de contrato antes de añadir SDKs; inyectar transportes para poder probar sin red ni credenciales.
4. Añadir adaptadores locales solo con licencias, origen de modelos, tamaño, runtime y pasos de instalación documentados. Los modelos no se incorporan al repositorio ni al instalador sin revisar licencias y peso.
5. Añadir adaptadores remotos solo si el mecanismo es oficial/documentado y el usuario puede usarlos opcionalmente. Revisar límites y condiciones vigentes antes de recomendar un proveedor concreto.
6. Validar: modo IA desactivado sin red; proveedor ausente; timeout/cancelación; límite de cuota; datos sensibles; credencial incorrecta; cierre ordenado; regresión de los flujos no-IA.

## Criterios de aceptación

- La app inicia y los flujos base funcionan con red desconectada y sin claves de IA.
- Ninguna API remota de IA es obligatoria ni se llama por defecto.
- Abrir Gemini/ChatGPT en una ventana se describe solo como asistencia manual, salvo que exista una integración autorizada que envíe y reciba datos de verdad.
- Cada proveedor real tiene estado, configuración, política de privacidad, límites y pruebas propias.
- Los documentos de continuidad identifican SHA exacto, CI de ese SHA y cualquier validación manual pendiente; no se mezclan estados de commits diferentes.

## Fuera de alcance de esta decisión

No se promete IA local de calidad determinada, voz neural sin requisitos, cuota remota gratuita permanente ni automatización de interfaces web privadas. La disponibilidad, licencias, hardware, cuotas y términos deben comprobarse al implementar cada adaptador.
