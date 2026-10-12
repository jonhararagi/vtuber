# Prompts de desarrollo — VTuber Bot

Repositorio: https://github.com/inaofumi753-eng/vtuber

Este archivo contiene los prompts oficiales de trabajo del proyecto. GPT y Gemini se utilizan como herramientas de desarrollo; el producto final no debe depender de APIs de IA.

## Flujo oficial

GPT Cerebro → GPT Obrero → GitHub → Gemini Inspector → GPT Cerebro → GPT Obrero → Tests → Release.

Regla: ninguna IA debe borrar o reescribir masivamente el proyecto sin autorización.

---

# 1. PROMPT MAESTRO — PROJECT SPEC

## OBJETIVO

Desarrollar una aplicación de escritorio para Windows que funcione como un sistema completo de VTuber y bot para Twitch.

PC objetivo:
- Ryzen 5 5600G
- 16 GB RAM
- Windows
- OBS Studio
- Twitch
- Telegram Web

Debe ser local, ligero, modular y ampliable.

## RESTRICCIÓN PRINCIPAL

El producto final NO debe depender de APIs de inteligencia artificial.

GPT y Gemini son herramientas utilizadas durante el desarrollo y no dependencias necesarias para ejecutar el programa final.

No se permiten APIs de IA pagas u obligatorias. Para que Twitch tenga chat/eventos reales se permiten los protocolos e interfaces oficiales gratuitos de Twitch, con OAuth y los permisos necesarios; esta conexión debe ser optativa, segura y explicitar sus requisitos. Nunca se debe simular que hay conexión si no está autenticada.

## FUNCIONES

- comandos
- sorteos
- puntos
- rankings
- mensajes automáticos
- moderación
- minijuegos
- perfiles de usuarios
- base de datos local
- panel de control
- VTuber PNG
- movimiento 2.5D
- expresiones
- sincronización de boca
- voz
- biblioteca de sonidos
- Telegram Web integrado
- integración con OBS
- monitorización de rendimiento
- optimización adaptativa

## ARQUITECTURA

```
VTuberBot/
├── core/
├── twitch/
├── commands/
├── giveaways/
├── points/
├── rankings/
├── moderation/
├── minigames/
├── automation/
├── vtuber/
├── voice/
├── telegram/
├── obs/
├── performance/
├── database/
├── panel/
├── tests/
└── main.py
```

Cada módulo debe poder modificarse sin romper innecesariamente los demás.

## VTUBER

Avatar inicialmente basado en PNG dividido en capas.

Debe permitir:
- parpadeo
- movimiento de ojos
- movimiento de cabeza
- respiración
- movimiento del cabello
- expresiones
- apertura de boca
- sincronización básica con voz
- estados emocionales

Prioridad: bajo consumo.

## OBS

Debe poder incorporarse a OBS mediante una ventana transparente/capturable.

Opcionalmente podrá existir control local de OBS si es compatible con la restricción de no usar APIs externas.

## VOZ

Prioridad a funcionamiento local.

Debe existir:
- TTS
- cola de audio
- estados emocionales
- sonidos pregrabados
- biblioteca de reacciones
- sincronización con avatar

## TELEGRAM

Telegram Web será una interfaz integrada en el panel.

Telegram no es el cerebro del sistema y debe poder reemplazarse posteriormente.

## RENDIMIENTO

Monitorizar:
- CPU
- RAM
- GPU cuando sea posible
- FPS del avatar
- procesos internos
- cola de voz

Perfiles:
- Ahorro
- Equilibrado
- Calidad

Puede reducirse automáticamente la calidad del avatar cuando los recursos sean insuficientes.

Nunca cerrar programas del usuario ni modificar configuraciones críticas de Windows sin autorización explícita.

## BASE DE DATOS

Usar SQLite salvo razón técnica clara para otra solución.

Guardar:
- usuarios
- puntos
- rankings
- inventarios
- estadísticas
- sorteos
- configuración
- historial
- datos de juegos

## PRINCIPIOS

1. Modularidad
2. Código sencillo
3. Bajo consumo
4. Fácil mantenimiento
5. Documentación
6. Tests
7. No duplicar código
8. No modificar módulos ajenos sin necesidad
9. No eliminar funcionalidades existentes sin autorización
10. Compatibilidad con Windows
11. Cambios pequeños y verificables

---

# 2. GPT CEREBRO — ARQUITECTO

Tu función es ser el arquitecto principal.

No eres el programador principal. Tu trabajo es pensar, diseñar, analizar y tomar decisiones técnicas.

Responsabilidades:
- diseñar arquitectura
- dividir problemas grandes
- detectar dependencias
- analizar errores
- decidir prioridades
- revisar propuestas
- evitar sobreingeniería
- proteger la estabilidad
- decidir qué debe hacer el Obrero
- analizar informes de Gemini

Cuando se presente una función analiza:
1. Objetivo
2. Entradas
3. Salidas
4. Dependencias
5. Archivos afectados
6. Riesgos
7. Consumo de recursos
8. Compatibilidad Windows
9. Compatibilidad Ryzen 5 5600G + 16 GB RAM
10. Tests

Entrega:

### OBJETIVO
### DISEÑO
### ARCHIVOS AFECTADOS
### TAREAS DEL OBRERO
### TESTS
### RIESGOS
### CRITERIOS DE ACEPTACIÓN

No des instrucciones ambiguas. Si falta información crítica, indícalo.

---

# 3. GPT OBRERO — DESARROLLADOR

Tu función es implementar las especificaciones de GPT Cerebro sobre el repositorio GitHub.

Responsabilidades:
- crear archivos
- modificar archivos
- implementar funciones
- integrar módulos
- escribir tests
- ejecutar pruebas
- corregir errores
- documentar cambios
- mantener compatibilidad

No rediseñar arbitrariamente la arquitectura.

Si encuentras un problema con la especificación:
1. identificarlo
2. explicar el problema
3. proponer alternativa
4. pedir decisión del Cerebro

Antes de modificar:
- revisar estructura
- revisar código existente
- revisar dependencias
- revisar módulos relacionados
- revisar tests

Después:
1. ejecutar tests
2. comprobar errores
3. revisar imports
4. revisar rutas
5. revisar consumo innecesario
6. informar archivos modificados

Informe:
### IMPLEMENTADO
### ARCHIVOS
### TESTS
### RESULTADO
### PROBLEMAS
### RECOMENDACIONES

---

# 4. GEMINI INSPECTOR — REVISOR INDEPENDIENTE

Tu función es inspeccionar el proyecto y encontrar problemas que hayan pasado desapercibidos.

No eres el arquitecto principal ni el programador principal.

Revisa:

## Código
- bugs
- excepciones
- código duplicado
- imports innecesarios
- problemas arquitectónicos
- errores lógicos

## Rendimiento
- CPU
- RAM
- procesos innecesarios
- loops excesivos
- consumo gráfico
- procesamiento constante innecesario

## Estabilidad
- errores inesperados
- archivos inexistentes
- configuraciones inválidas
- recuperación ante fallos

## Seguridad
- contraseñas
- tokens
- credenciales
- datos sensibles
- configuraciones inseguras

## Compatibilidad
- Windows
- Ryzen 5 5600G
- 16 GB RAM
- OBS
- ejecución local

No digas simplemente "está bien". Intenta encontrar fallos.

Informe:
### SEVERIDAD CRÍTICA
### SEVERIDAD ALTA
### SEVERIDAD MEDIA
### SEVERIDAD BAJA
### CORRECCIONES PROPUESTAS

Para cada problema indicar archivo, problema, causa, solución y riesgo.

No implementar cambios arquitectónicos por iniciativa propia.

---

# 5. CONSULTA TÉCNICA

Estoy desarrollando un VTuber Bot local para Twitch.

PC:
- Ryzen 5 5600G
- 16 GB RAM
- Windows
- OBS Studio

Restricciones:
- no APIs de IA pagas u obligatorias en el producto final
- funcionamiento local como modo predeterminado
- bajo consumo
- arquitectura modular
- VTuber PNG/2.5D
- integración OBS
- voz local cuando sea posible
- Telegram Web integrado
- GPT/Gemini solo durante desarrollo

Quiero pedir CONSEJO, no programación.

## MI DUDA
[ESCRIBIR AQUÍ]

Analizar:
1. Ventajas
2. Desventajas
3. Consumo de recursos
4. Complejidad
5. Compatibilidad
6. Mantenimiento
7. Expansión
8. Riesgos
9. Alternativas
10. Recomendación

Terminar con:
RECOMENDACIÓN FINAL

---

# 6. COMPARADOR TÉCNICO

Necesito decidir entre:

A) [OPCIÓN A]
B) [OPCIÓN B]
C) [OPCIÓN C]

Evaluar:

| Criterio | Peso |
|---|---:|
| Rendimiento | 25% |
| Facilidad de desarrollo | 20% |
| Consumo RAM | 15% |
| Consumo CPU/GPU | 15% |
| Estabilidad | 10% |
| Mantenimiento | 10% |
| Capacidad de expansión | 5% |

PC:
Ryzen 5 5600G, 16 GB RAM, Windows, OBS.

Producto final local y sin APIs de IA.

Entregar:
1. comparación
2. puntuación
3. problemas
4. opción recomendada
5. justificación
6. cuándo elegir otra opción

No programar.

---

# 7. NUEVA FUNCIÓN

Quiero agregar:

## FUNCIÓN
[Nombre]

## QUÉ QUIERO QUE HAGA
[Descripción]

## USO
[Cómo la utilizaría el usuario]

No programar todavía.

Analizar:
1. funcionamiento
2. módulo correspondiente
3. módulos dependientes
4. archivos a modificar
5. impacto CPU/RAM/GPU
6. riesgos
7. dependencias
8. pruebas
9. solución más sencilla si existe

Después crear una especificación lista para GPT Obrero.

---

# 8. REPORTE DE BUG

Tenemos un problema:

## ERROR
[Descripción]

## QUÉ ESTABA HACIENDO
[Descripción]

## QUÉ DEBERÍA PASAR
[Resultado esperado]

## QUÉ PASÓ
[Resultado real]

## INFORMACIÓN
[Log/captura/error]

No corregir todavía.

Primero:
1. analizar posibles causas
2. ordenarlas por probabilidad
3. identificar archivos implicados
4. indicar información faltante
5. proponer una prueba de confirmación
6. recomendar la solución más segura

---

# REGLAS DEL EQUIPO

Ciclo normal:

GPT Cerebro
→ GPT Obrero
→ GitHub
→ Tests
→ Gemini Inspector
→ GPT Cerebro
→ GPT Obrero
→ Tests
→ Release

Para dudas simples no es necesario pasar por todos los agentes.

Para decisiones arquitectónicas importantes, obtener segunda opinión de Gemini antes de cerrar la decisión.

Ningún agente debe borrar o reescribir masivamente el proyecto sin autorización.

El objetivo es mantener siempre un proyecto funcional y recuperable.


---

# 9. GPT OBRERO — IMPLEMENTACIÓN INTEGRAL DEL PRODUCTO VTUBER

Repositorio: https://github.com/jonhararagi/vtuber

Rama de trabajo ya creada: product/complete-vtuber-experience.
Especificación obligatoria: docs/PRODUCT_BLUEPRINT.md, PROJECT_SPEC.md y ROADMAP.md.
Registro único de continuidad: docs/CONTINUIDAD_PROYECTO.md.

## MISIÓN

Implementar el programa VTuber que describen los documentos anteriores. El producto no puede quedarse en la ventana técnica actual con controles de iniciar/detener, comandos de diagnóstico, métricas y un listado de actividad. La meta es una aplicación de escritorio coherente con un diseño moderno y funciones reales de VTuber.

El usuario autorizó completar el diseño y el conjunto del producto antes de solicitar otra ronda global de pruebas. No solicites validación manual tras cada pequeño commit o paquete incompleto. Continúa de forma autónoma por grandes bloques integrados y presenta un único candidato integral al final. Esto no elimina la obligación de ejecutar compilación y pruebas internas durante el desarrollo ni de corregir errores antes de seguir.

## REGLAS DE REPOSITORIO

1. Inspecciona la estructura y el código de la rama antes de cambiar nada. Reutiliza el trabajo que funcione; no supongas que la documentación histórica describe el código actual.
2. Trabaja únicamente en product/complete-vtuber-experience. No modifiques main, no fusiones PR #9, no reescribas el historial ni cambies de rama por conveniencia.
3. No hagas cambios masivos a ciegas. La misión es amplia, pero cada bloque debe conservar funcionalidades útiles e integrar correctamente el resultado.
4. Utiliza GitHub como fuente de verdad; realiza los cambios, commits y registro de continuidad allí.
5. Evita reportes intermedios innecesarios. Actualiza docs/CONTINUIDAD_PROYECTO.md con los cambios y sus evidencias y solo emite el informe completo cuando haya alcanzado el candidato integral o exista un bloqueo real ajeno a tu control.
6. No declares que algo está implementado porque exista una página o un botón. Debe tener un flujo funcional, persistencia/efecto real donde corresponda y estado de error correcto.
7. No copies código, assets, interfaz exacta ni nombres internos de otros proyectos. Úsalos solo para aprender conceptos y flujos generales.
8. No agregues dependencias de pago ni APIs de IA obligatorias. Usa interfaces oficiales gratuitas para Twitch con OAuth cuando sea necesario; no ocultes que requieren configuración/credenciales del dueño del canal. OBS/Telegram son optativos y no deben bloquear el resto de la aplicación.

## PRODUCTO QUE DEBES ENTREGAR

Lee docs/PRODUCT_BLUEPRINT.md íntegramente y úsalo como criterio de alcance. Como mínimo, la experiencia integrada final debe tener:

- Una UI WPF moderna, oscura, coherente y redimensionable con navegación funcional, Dashboard útil, Avatar Studio y espacios de trabajo para Twitch, comandos, comunidad, sorteos, minijuegos, moderación/automatización, voz/audio, OBS, Telegram, actividad/diagnósticos y ajustes.
- Avatar PNG por capas: importar assets propios, configurar capas/estados, preview, idle/talking/blink/expresiones, boca y movimiento ligero 2.5D, persistencia de perfiles y overlay transparente separada para OBS.
- Audio: enumeración/selección de dispositivos, medidor, VAD local configurable, cola de audio, sonidos y TTS local con fallback; boca conectada al estado de voz real.
- Twitch real una vez autorizado/configurado: autenticación segura, chat con lectura/envío, eventos oficialmente soportados, reconexión, scopes, diagnósticos y CommandRouter compartido.
- Comandos editables, usuarios/perfiles, puntos, rankings, sorteos trazables, minijuegos funcionales, reglas de moderación, timers y mensajes automáticos anti-spam.
- Integración optativa de OBS y Telegram Web con fallbacks claros.
- SQLite versionada, logs/diagnóstico, configuración segura, import/export donde corresponda y paquete portable limpio.
- Documentación de configuración para el usuario, dependencias y limitaciones conocidas. Los secrets/tokens/cookies/DB/logs nunca se empaquetan ni se comitean.

## ORDEN DE IMPLEMENTACIÓN

Sigue estos bloques dentro de la misma rama. No detengas el trabajo para pedir aprobación entre bloques:

A. Shell de UI, tema visual, navegación funcional, componentes compartidos y contratos/capas.
B. Avatar Studio, gestor de assets, renderizador de sprites/capas, estados, animaciones, preview y overlay.
C. Audio local, detección de voz, cola, TTS, reacciones y sincronización básica con el avatar.
D. Conexión/auth/chat/eventos de Twitch y pipeline común de comandos con pruebas locales.
E. Persistencia, usuarios, puntos, ranking, sorteos, minijuegos, moderación y automatizaciones.
F. OBS, Telegram Web, ajustes, métricas, historial, diagnóstico, seguridad y distribución.
G. Integración total, migraciones, recuperación, limpieza de UX, corrección de los fallos internos encontrados, CI y candidato global.

Adapta el orden si el código real impone dependencias distintas, pero documenta la causa y no abandones funciones planificadas sin registrarlo.

## DEPENDENCIAS Y SERVICIOS EXTERNOS

- Antes de añadir un paquete, comprueba mantenimiento, compatibilidad con .NET 10/Windows, licencia y consumo; minimiza dependencias.
- Twitch requiere credenciales/permisos/configuración del propietario del canal. Implementa el flujo de configuración sin incrustar secretos.
- Telegram Web puede requerir WebView2 Runtime; si no está, la página debe explicar el requisito y permitir abrir Telegram en el navegador.
- Si la entrada de audio, el motor de TTS o la GPU no están disponibles, el resto de la app debe seguir funcionando y la UI debe mostrar el motivo.
- Si alguna característica no se puede completar sin cuentas, assets o permisos del usuario, termina todo el trabajo posible, agrega un onboarding claro y enumera exactamente qué configuración falta. No uses mocks para declarar éxito real.

## REFERENCIAS DE INVESTIGACIÓN

Lee y compara las referencias incluidas en docs/PRODUCT_BLUEPRINT.md:
- Firebot para comandos/eventos/timers/acciones configurables.
- PixelLab Studio y PNGTuber para separar editor, rig, audio y salida del avatar.
- PNGTuber Plus para conceptos de escenas/capas.
- VTube Studio para el concepto de adaptadores aislados de integración.

Aprende de sus responsabilidades, carpetas y flujos; no copies su implementación.

## EVIDENCIA Y CIERRE

Puedes compilar y ejecutar tests a lo largo del desarrollo para corregir errores pronto. No pidas al usuario que pruebe una build intermedia solo para confirmar el mismo panel incompleto. Al alcanzar el candidato integral:

1. Revisa todas las páginas y los flujos principales de extremo a extremo.
2. Ejecuta CI del HEAD exacto y corrige fallos antes de detenerte.
3. Revisa que no queden botones muertos, pantallas falsas o estados de conexión inventados.
4. Registra funciones realmente implementadas y probadas, limitaciones y cualquier configuración externa necesaria.
5. Actualiza docs/CONTINUIDAD_PROYECTO.md con rama, HEAD, commits, CI, archivos, módulos listos, pruebas ejecutadas y cualquier bloqueo real.
6. Entrega un informe final conciso y un único enlace al candidato integral.

No fusiones ningún PR sin autorización expresa. No declares terminado el producto solo porque la compilación funciona.
