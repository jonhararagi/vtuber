# Continuidad del proyecto — VTuberBot

> Registro central de continuidad para GPT Cerebro, GPT Obrero, Gemini Inspector y agentes futuros. Actualizar este archivo al finalizar cada tarea de implementación o corrección. No duplicar registros por tarea.
>
> **Regla de evidencia:** separar siempre cambios implementados, pruebas automatizadas, validación manual y pendientes. Un resultado solo corresponde al SHA exacto que lo produjo.

## 1. Estado general

- **Repositorio:** https://github.com/jonhararagi/vtuber
- **Rama principal:** `main`
- **HEAD de `main` comprobado en el PR #9:** `bbcd06c0e0706000cd95d85771b33452d11e7663`. No se modificó `main`.
- **Rama de trabajo:** `v0.2.3/stabilization-gate`
- **PR activo:** [#9 — feat: add v0.2.3 stabilization gate](https://github.com/jonhararagi/vtuber/pull/9). Base `main`; permanece abierto y sin fusionar.
- **Versión en desarrollo:** V0.2.3. No implica que el hito esté aprobado ni que se haya publicado una versión final.
- **Arquitectura aprobada para este hito:** aplicación Windows con C#/.NET 10 y WPF; Core independiente de WPF; persistencia SQLite con `Microsoft.Data.Sqlite` (sin EF Core); pruebas automatizadas y CI en `windows-latest`. Mantener la arquitectura existente y el alcance de estabilización. No iniciar V0.3 ni implementar avatar, integraciones reales u otras funciones fuera del alcance aprobado.
- **Flujo de trabajo:** GPT Cerebro decide y audita; GPT Obrero implementa cambios aprobados en GitHub; Gemini Inspector revisa. GitHub es la fuente de verdad.

## 2. Historial de trabajo

### 2026-10-12 — Corrección de versión V0.2.3 y guía manual

- **Objetivo:** corregir la versión visible en `status`, añadir/ajustar comprobación automatizada y alinear la guía manual con el arranque automático en `RUNNING`.
- **Cambios implementados en el commit** [`54549e108d2acca65061ad704c629f2f213c1122`](https://github.com/jonhararagi/vtuber/commit/54549e108d2acca65061ad704c629f2f213c1122):
  - `src/VtuberBot.Core/Application.cs`: constante `VtuberApplication.Version` cambiada de `0.2.1` a `0.2.3`.
  - `tests/VtuberBot.Tests/Tests.cs`: aserción de la versión y prueba de `status` enlazadas a la constante común.
  - `docs/V0.2.3-MANUAL-VALIDATION.md`: secuencia ajustada para reflejar que la aplicación arranca normalmente en `RUNNING`; resultados manuales permanecen sin ejecutar para el nuevo paquete.
- **CI del HEAD `54549e108d2acca65061ad704c629f2f213c1122`:** [Windows CI, ejecución 38207936768](https://github.com/jonhararagi/vtuber/actions/runs/38207936768) — **SUCCESS**. Este resultado pertenece exactamente a ese HEAD, no a commits posteriores.
- **Pruebas automáticas:** Windows CI pasó en ese SHA. No se ejecutó la suite localmente porque el entorno local de trabajo no tenía `dotnet` disponible.
- **Validación manual del nuevo paquete:** pendiente; el CI no sustituye la interacción manual con el ejecutable.
- **Estado al registrar este documento:** la corrección de código está implementada y el CI del commit anterior a este documento pasó. La creación de este documento produce un nuevo commit/HEAD, por lo que el CI correspondiente a ese nuevo HEAD debe verificarse por separado.
- **Archivos de código/documentación modificados por la corrección:** los tres enumerados arriba. Este registro es un cambio documental adicional en el mismo PR.

### Historial previo relevante — evidencia manual del paquete anterior

La validación manual aportada por el usuario corresponde al paquete cuyo **Source SHA era `dc6926eeb42275fab9af79253925a00567852c67`**. Esta evidencia histórica no debe atribuirse a la compilación de `54549e108d2acca65061ad704c629f2f213c1122` ni a paquetes posteriores.

| Comprobación del paquete con Source SHA `dc6926eeb42275fab9af79253925a00567852c67` | Resultado aportado |
|---|---|
| Apertura de la aplicación | **APROBADO** |
| Inicio después de detener | **APROBADO** |
| Detención | **APROBADO** |
| `help` | **APROBADO** |
| `status` | **FALLIDO** — mostraba versión `0.2.1` cuando se esperaba `0.2.3` |
| `ping` | **APROBADO** |
| Cinco ciclos adicionales de inicio/detención | **APROBADO** |
| Diez minutos de estabilidad | **APROBADO** |
| Cierre de ventana y terminación del proceso | **APROBADO** |

El fallo de `status` motivó la corrección de la constante y las aserciones. La corrección de código y el CI verde no demuestran por sí solos que el usuario haya vuelto a ejecutar manualmente todas las pruebas en un paquete nuevo.

## 3. Defecto actual y resolución

### Defecto observado

- En el código inspeccionado antes de la corrección, `VtuberApplication.Version` estaba fijada en `0.2.1`, por lo que `status` no coincidía con la versión esperada V0.2.3.
- La guía manual debía contemplar que la aplicación arranca automáticamente en `RUNNING`, antes de intentar validar la secuencia de detención e inicio.

### Cambios aprobados y estado

1. Cambiar la versión a `0.2.3`: **IMPLEMENTADO** en `54549e1`.
2. Añadir/ajustar prueba automatizada de versión y `status`: **IMPLEMENTADO** en `54549e1`; **CI PASÓ** para ese SHA.
3. Corregir la secuencia de la guía de validación: **IMPLEMENTADO** en `54549e1`; la nueva ejecución manual sigue pendiente.
4. Ejecutar CI y validar el nuevo HEAD: **CI PASÓ** para `54549e108d2acca65061ad704c629f2f213c1122`. El commit de este documento crea un HEAD posterior; verificar su CI antes de actualizar este estado como validado.

## 4. Pendientes y próximo paso

1. **En curso:** guardar y verificar este documento central en la rama `v0.2.3/stabilization-gate` y en el PR #9.
2. **CI pendiente para el HEAD de este documento:** comprobar que la ejecución disparada por el nuevo commit finalice correctamente; registrar URL, resultado y SHA exacto.
3. **Validación manual pendiente:** obtener/construir el paquete Windows correspondiente al HEAD aprobado más reciente y repetir la lista de `docs/V0.2.3-MANUAL-VALIDATION.md`. Registrar resultados reales y el Source SHA del paquete.
4. **Bloqueos reales conocidos:** no hay un bloqueo de compilación reportado para el SHA `54549e1` (CI verde). La validación manual del nuevo paquete aún no está hecha. La ejecución local de pruebas no estuvo disponible por ausencia de `dotnet` en el entorno de trabajo.
5. **Próxima acción exacta:** verificar el CI del nuevo HEAD; si pasa, obtener el artefacto de ese mismo SHA y pedir/realizar la validación manual documentada. No reutilizar resultados del paquete histórico.
6. **Condiciones para cerrar V0.2.3:** CI verde para el HEAD candidato exacto; paquete identificado por su Source SHA; validación manual repetida con `status` mostrando `0.2.3` y todos los pasos requeridos aprobados; revisión final de GPT Cerebro/Gemini Inspector según el flujo acordado. El PR #9 no se fusiona sin autorización explícita.

## 5. Reglas permanentes de mantenimiento

- Actualizar este archivo al terminar cada tarea de implementación o corrección.
- Registrar fecha, objetivo, cambios, archivos, commit, PR, CI (incluyendo SHA), pruebas, problemas, limitaciones y siguiente paso.
- No reemplazar resultados históricos correctos ni atribuir pruebas a otro SHA.
- Usar los estados **IMPLEMENTADO**, **PROBADO AUTOMÁTICAMENTE**, **COMPROBADO MANUALMENTE** y **PENDIENTE** de forma explícita.
- Mantener un único registro central: `docs/CONTINUIDAD_PROYECTO.md`.
- No modificar `main`, no fusionar el PR #9 y no ampliar el alcance sin aprobación.
