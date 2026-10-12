# ROADMAP — VTuber Bot

Este roadmap distingue el núcleo confirmado de las funciones que aún necesitan una decisión de alcance. Las fases son una secuencia de trabajo orientativa, no una autorización para implementar automáticamente cada función.

## Alcance confirmado para V1.0

- Aplicación de escritorio para Windows.
- Núcleo estable, controles funcionales, configuración y registros adecuados.
- Ciclo de vida robusto, manejo de errores y recuperación.
- Arquitectura modular que permita integrar un avatar sin acoplar la presentación a la lógica central.
- Pruebas automatizadas, documentación útil y validación en Windows antes de aceptar una versión.

## V0.1 — Fundación

- Estructura modular, configuración y logging.
- Event Manager, Task Scheduler y Resource Manager.
- Persistencia SQLite y pruebas de humo.

**Salida:** la aplicación inicia, abre la base local y procesa un evento interno de prueba.

## V0.2.0 — Control y comandos

- CommandRouter local y comandos `help`, `status`, `ping`.
- Interfaz WPF con controles básicos y resultados visibles.
- Integración de comandos con el ciclo de vida y pruebas automatizadas.

**Salida:** la app se abre, ejecuta los comandos y permite controlar el ciclo de vida sin proveedores externos.

## V0.2.1 — Rendimiento y salud

- Observación de CPU/RAM y métricas viables.
- Perfiles de rendimiento si están respaldados por la implementación y pruebas.

## V0.2.2 — Concurrencia SQLite

- Acceso serializado y pruebas de concurrencia/recuperación.

## V0.2.3 — Puerta de estabilización

- Regresión automatizada, ciclos repetidos, revisión de recursos, smoke de Windows y documentación.
- La prueba prolongada y la interacción manual de Windows deben quedar documentadas antes de aceptar esta versión.

**Salida:** evidencia automatizada y humana suficiente; defectos críticos y altos resueltos. La aprobación no se infiere únicamente de CI.

## Funciones planificadas que requieren definición antes de comprometer alcance

### Avatar animado
Definir formato y pipeline del avatar, expresiones, animación mínima, integración con ventana transparente y/o OBS, y separación estricta entre presentación y lógica. No integrar recursos concretos en runtime hasta aprobar el alcance y revisar licencias.

### Twitch
Definir capacidades necesarias, autenticación, eventos, permisos, límites y estrategia de reconexión. Documentar las restricciones de APIs y evitar prometer funciones no verificadas.

### Voz local
Definir motor local, idiomas, latencia, calidad, consumo y sincronización con el avatar. No depender de APIs de IA de pago obligatorias durante la ejecución.

### Persistencia, comunidad, juegos y moderación
Aprobar los casos de uso y el alcance mínimo de cada sistema antes de estimar o implementar. No se consideran automáticamente requisitos de V1.0 por existir en versiones históricas del roadmap.

## Funciones opcionales pendientes de aprobación

- Integración con Telegram.
- Panel avanzado adicional a la interfaz WPF actual.
- Otras integraciones externas sin caso de uso confirmado.

No se planifican como trabajo obligatorio de V1.0 hasta que exista una decisión explícita.

## Orden recomendado, sujeto a aprobación

1. Completar y aceptar V0.2.3.
2. Definir requisitos y criterios de aceptación del Avatar MVP.
3. Implementar el Avatar MVP en una rama de trabajo aprobada, conservando el núcleo independiente de la presentación.
4. Definir y priorizar Twitch, voz local, persistencia, juegos y moderación según el producto deseado.
5. Endurecimiento, validación de extremo a extremo y preparación de V1.0.

## Regla de salida

No avanzar solo porque una función parece funcionar manualmente. Debe estar probada, documentada y libre de regresiones críticas conocidas. Prioridad: estabilidad, recuperación, mantenibilidad, funciones aprobadas y, después, pulido visual.
