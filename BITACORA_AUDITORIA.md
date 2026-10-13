# BITÁCORA DE AUDITORÍA Y DECISIONES

Fecha de inicio: 2026-10-06
Repositorio: inaofumi753-eng/vtuber

## 0. Propósito

Esta bitácora conserva las decisiones, hallazgos y reglas obtenidos durante la auditoría de programas, librerías y APIs que pueden aportar al VTuber Bot.

Objetivo: evitar repetir investigación, evitar reimplementar decisiones ya resueltas y mantener trazabilidad de por qué un componente entra, se adapta, se usa externamente o se descarta.

Esta bitácora pertenece a `main` y debe actualizarse cuando una auditoría produzca una decisión que afecte a la arquitectura futura.

---

## 1. Foco del proyecto

El producto es un **VTuber Bot local-first para Windows**, pensado alrededor de:

- Ryzen 5 5600G
- 16 GB RAM
- OBS Studio
- Python/PySide6
- SQLite
- arquitectura orientada a eventos

El producto final debe separar claramente:

1. Core y estado.
2. Twitch.
3. Avatar/VTuber.
4. Voz.
5. OBS/streaming.
6. Memoria/datos.
7. Automatización/comandos.
8. Panel y diagnóstico.

La integración externa debe estar aislada detrás de adaptadores.

### Principio fundamental

No buscamos copiar una aplicación completa.
Buscamos encontrar **la pieza que resuelve un problema real** y convertir ese hallazgo en:

- una dependencia externa;
- un adaptador propio;
- una reimplementación pequeña;
- o conocimiento de diseño.

---

## 2. Qué se descubrió sobre la estrategia

### 2.1 No conviene construir todo desde cero

Los programas auditados muestran que muchas funciones difíciles ya están resueltas por otros proyectos:

- Twitch/EventSub/OAuth.
- control de VTube Studio.
- control de OBS.
- STT local.
- VAD.
- TTS local.
- colas de audio.
- sistemas de agente.
- memoria/contexto.
- tracking facial.
- automatización.

La estrategia óptima es **composición modular**, no clonación.

### 2.2 El core debe permanecer pequeño

El core no debe depender de un motor concreto de IA, TTS, STT, avatar o streaming.

Los motores pesados deben vivir detrás de una frontera estable.

Ejemplo:

`VAD -> STT -> LLM -> TTS -> Avatar/OBS`

pero cada etapa debe poder cambiar de proveedor.

### 2.3 Aplicaciones externas > copiar aplicaciones completas

Cuando un programa ya hace muy bien una tarea completa, la primera opción es:

`Nuestro Bot -> API/IPC/WebSocket -> programa externo`

Ejemplos:

- Nuestro Bot -> VTube Studio API.
- Nuestro Bot -> OBS WebSocket -> OBS.
- Nuestro Bot -> TTS local externo.
- Nuestro Bot -> tracker local externo.

---

## 3. Hallazgos arquitectónicos de mayor valor

### AIRI / Open-LLM-VTuber

Estos proyectos demostraron que conviene separar:

- agente;
- tipos de entrada/salida;
- proveedores de LLM;
- ASR;
- TTS;
- conversación;
- memoria/contexto;
- cola de audio;
- estado del avatar.

Patrones especialmente útiles:

1. **Provider interfaces** para poder cambiar motores.
2. **Input/output normalizados** para que el resto del sistema no dependa del proveedor.
3. **Compacción de contexto** para reducir historial antiguo.
4. **Proyección de eventos** a mensajes del proveedor.
5. **Cola de TTS con números de secuencia** para generar audio en paralelo y reproducirlo en orden.
6. **Prioridades e interrupción** para que una respuesta urgente pueda desplazar/reemplazar audio normal.
7. **Acciones paralelas al texto**, como expresiones, imágenes o sonidos.
8. **Agentes intercambiables** mediante factory/configuración.

Conclusión:

> Nuestro cerebro no debe conocer directamente Twitch, OBS, VTube Studio o un TTS concreto.

---

## 4. Hallazgos sobre Twitch

### TwitchIO 3.3.2

Licencia: MIT.

Aporta buenas referencias para:

- cliente Twitch;
- EventSub;
- suscripciones tipadas;
- OAuth;
- tokens;
- comandos;
- componentes/listeners;
- rutinas.

### Decisión

No introducir TwitchIO directamente en el core V0.1 porque:

- usa `asyncio`;
- introduce `aiohttp`;
- aumenta el peso del runtime;
- nuestro V0.1 fue diseñado deliberadamente como core síncrono y pequeño.

Uso recomendado:

`Twitch -> adaptador Twitch -> EventManager -> feature handler`

El modelo de eventos de TwitchIO sirve como referencia.

### No duplicar variantes

Los snapshots de TwitchIO:

- main;
- master;
- patch 3.2.2;
- pinned_messages;

se consideran comparativas, no cuatro productos diferentes.

El objetivo de integración canónico quedó en TwitchIO 3.3.2.

---

## 5. Hallazgos sobre avatar

### VTube Studio API

Licencia del repositorio API: MIT.

La pieza útil no es copiar VTube Studio.
La pieza útil es la comunicación con VTube Studio.

Capacidades relevantes:

- autenticación;
- solicitudes/respuestas;
- eventos;
- hotkeys;
- expresiones;
- parámetros;
- modelos;
- items.

### pyvts

Licencia: MIT.

Demostró:

- WebSocket;
- autenticación;
- request builders;
- suscripción a eventos;
- parámetros personalizados;
- pruebas con servidor WebSocket falso.

### Decisión

VTube Studio debe permanecer como aplicación externa.

Nuestro código solo debe implementar el adaptador necesario.

Nunca almacenar tokens secretos dentro del repositorio.

---

## 6. Hallazgos sobre OBS

### OBS Studio

Licencia: GPL-2.0-or-later.

Conclusión:

- OBS sigue siendo aplicación externa.
- No copiar su código al Bot.

### OBS WebSocket

El servidor está bajo GPL-2.0-or-later.

La parte útil es el **protocolo**, no el servidor.

El Bot debe:

`Nuestro cliente WebSocket -> OBS WebSocket -> OBS`

No copiar el servidor a nuestro proyecto.

### Regla

La interfaz/protocolo público puede implementarse independientemente.

---

## 7. Hallazgos sobre voz

### VAD — Silero VAD

Licencia: MIT.

Uso previsto:

`Micrófono -> VAD -> decidir si existe voz`

Ventajas:

- local;
- pequeño;
- adecuado para filtrar silencio antes del STT;
- ruta ONNX interesante para reducir dependencias.

### STT — whisper.cpp

Licencia: MIT.

Uso previsto:

- STT offline/local;
- proceso/binario o servidor local;
- backend intercambiable.

No conviene meter todo el C/C++ en el core Python.

### STT/ASR — sherpa-onnx

Licencia del núcleo: Apache-2.0.

Interesante por reunir:

- ASR;
- VAD;
- TTS;
- streaming;
- varios bindings.

Debe permanecer detrás de un adaptador y con modelos/licencias separados.

### TTS — Kokoro ONNX

Código: MIT.
Modelo: tratado separadamente y con licencia propia.

Muy interesante para TTS local por su relación entre calidad, peso y simplicidad de integración.

### TTS — GPT-SoVITS

Código fuente: MIT.

Muy interesante para voz de alta calidad/voice cloning.

Pero:

- runtime pesado;
- PyTorch/CUDA;
- modelos externos;
- ASR y herramientas auxiliares con licencias separadas.

Debe tratarse como motor externo.

### TTS — Piper

El repositorio histórico contiene código MIT, pero el proyecto actual continuó en otra base con GPL.
Por eso la versión exacta del ZIP auditado debe verificarse antes de reutilizar código/binarios.

Estado: **pendiente de verificación exacta del ZIP guardado**.

---

## 8. Tracking

### OpenSeeFace

Núcleo: BSD-2-Clause.

Muy útil como proceso externo.

Arquitectura:

`Cámara -> OpenSeeFace -> UDP -> adaptador -> avatar`

Los paquetes binarios traen dependencias/avisos adicionales; deben conservarse.

### VSeeFace

Aplicación externa.

Útil como referencia/interoperabilidad.
No debe tratarse como código abierto solo por existir bibliotecas públicas relacionadas.

---

## 9. Automatización/Twitch

### Streamer.bot

El programa descargado es una distribución compilada.
No debe tratarse como código fuente MIT.

Utilidad:

- referencia de triggers;
- acciones;
- queues;
- integraciones;
- Twitch;
- OBS;
- VTube Studio.

Uso recomendado:

**referencia + integración externa**, no copia del ejecutable/DLLs.

### Speaker.bot

Distribución compilada con numerosos proveedores TTS y librerías.

Utilidad:

- referencia de arquitectura de TTS;
- audio queue;
- dispositivos de audio;
- proveedores intercambiables.

Uso recomendado:

**aplicación externa/referencia**.

### Mix It Up

La licencia actual del proyecto first-party es Business Source License 1.0.1 y contiene restricciones de redistribución y de uso competitivo.

Decisión:

**no copiar código first-party**.

Utilizar solamente como referencia conceptual.

---

## 10. Proyectos que no justifican integración actual

### MediaMTX
MIT. Muy útil técnicamente, pero no necesario para el core actual.
Mantener como servicio externo potencial para transporte de medios.

### DistroAV
GPL. Integración OBS/NDI especializada.
No necesario actualmente.

### StreamFX
Plugin OBS.
No necesario para el Bot central.

### RustDesk
Remote desktop.
Sin relación con el core VTuber actual.

### Quasar
Framework web.
Nuestro panel usa PySide6; no reemplazar arquitectura.

### KoboldCpp
Aplicación AGPL.
Muy interesante como backend externo de LLM local, pero no debe copiarse su aplicación dentro del Bot.

---

## 11. Regla de licencias

Una licencia de repositorio **no significa que todos los archivos/activos tengan esa misma licencia**.

Revisar por separado:

- código;
- dependencias;
- modelos;
- voces;
- avatars;
- imágenes;
- logos;
- fuentes;
- binarios;
- datasets;
- plugins.

### Licencias permisivas

MIT/BSD/Apache pueden ser candidatas a reutilización, respetando:

- copyright;
- licencia;
- NOTICE cuando exista;
- requisitos específicos de Apache;
- licencias de terceros.

### GPL/AGPL/BSL/proprietary

No incorporar automáticamente al Bot.

La primera alternativa debe ser:

- proceso externo;
- API;
- protocolo;
- cliente propio;
- reimplementación independiente.

### Regla absoluta

No borrar `LICENSE`, `NOTICE` o avisos de copyright de código que estamos reutilizando cuando la licencia exige conservarlos.

---

## 12. Lo que ya se convirtió en código de main

La auditoría **sí produjo código real**.

Actualmente `main` contiene:

### `core/process_manager.py`

`OwnedProcess`:

- lanza solamente procesos explícitos;
- usa `shell=False`;
- gestiona únicamente el proceso que él mismo creó;
- soporta stop/restart;
- evita gestionar PIDs arbitrarios.

Objetivo futuro:

- STT;
- TTS;
- face tracking;
- otros motores locales.

### `voice/contracts.py`

Incluye:

- `SpeechRequest`;
- prioridades de voz;
- contrato TTS;
- contrato STT;
- contrato VAD.

Esto permite cambiar:

- Whisper;
- sherpa-onnx;
- Kokoro;
- GPT-SoVITS;
- Piper;

sin rediseñar el resto.

### Contratos de integración

`vtuber/avatar_contracts.py`

`twitch/backend_contracts.py`

`obs/backend_contracts.py`

Estos son interfaces propias del proyecto.

No copian servidores externos.

---

## 13. Regla de arquitectura derivada

La estructura conceptual futura es:

`Entrada -> EventManager -> lógica -> salida`

Con adaptadores:

`Twitch -> EventManager`

`Micrófono -> VAD -> STT -> EventManager`

`Cerebro -> SpeechRequest -> TTS`

`Cerebro -> AvatarBackend`

`Cerebro -> OBSBackend`

El core no debe conocer la implementación del proveedor.

---

## 14. Pipeline de voz recomendado

Una arquitectura candidata es:

`Micrófono`
-> `VAD`
-> `STT`
-> `Evento interno`
-> `Cerebro`
-> `Respuesta`
-> `TTS`
-> `cola/prioridad`
-> `audio`
-> `avatar/OBS`

Ventaja:

Cada componente puede sustituirse.

Ejemplo:

`Silero + whisper.cpp + Ollama + Kokoro`

puede sustituirse por:

`sherpa-onnx + otro LLM local + GPT-SoVITS`

sin rehacer el core.

---

## 15. Cola de voz

Hallazgo importante de AIRI:

No conviene sintetizar y reproducir todo estrictamente de forma serial.

Mejor:

1. generar varias frases;
2. asignar número de secuencia;
3. permitir generación paralela;
4. reproducir en orden;
5. admitir prioridad/interrupción;
6. eliminar archivos temporales.

Esto reduce latencia percibida sin perder orden.

---

## 16. Memoria/contexto

El Bot necesitará eventualmente separar:

- contexto actual;
- historial reciente;
- memoria persistente;
- conocimiento del personaje;
- estado de sesión;
- contexto de Twitch.

La compacción de historial evita mandar todo el pasado permanentemente al LLM.

La memoria no debe convertirse en una sola tabla gigante.

---

## 17. Acciones junto con respuestas

Una respuesta del Bot no tiene por qué ser solo texto.

Puede producir:

`Texto + expresión + sonido + cambio de avatar + acción OBS`

Por eso el futuro formato interno de respuesta debería soportar:

- texto visible;
- texto para TTS;
- acciones.

Esto ya se observó en Open-LLM-VTuber.

---

## 18. Eventos antes que acoplamiento directo

No hacer:

`Twitch -> llamar directamente a avatar.py`

Preferir:

`Twitch -> EventManager -> handler -> AvatarBackend`

Esto permite:

- múltiples consumidores;
- tests aislados;
- logging;
- replay;
- recuperación;
- menor acoplamiento.

---

## 19. Seguridad

Nunca:

- ejecutar comandos recibidos de Twitch directamente;
- pasar texto de usuario a `shell=True`;
- matar PIDs ajenos;
- guardar tokens en Git;
- meter API keys en código;
- asumir que un archivo externo es seguro.

El gestor de procesos incorporado usa explícitamente `shell=False`.

Los secretos deben ir en configuración/almacenamiento local fuera del control de versiones.

---

## 20. Rendimiento

El objetivo no es usar el hardware al máximo.

Prioridad:

1. estabilidad;
2. recuperación;
3. rendimiento;
4. mantenibilidad;
5. funcionalidades;
6. efectos visuales.

Para una máquina de 16 GB, evitar cargar varios modelos pesados simultáneamente si no es necesario.

La arquitectura debe permitir descargar/cerrar motores que no estén siendo utilizados.

---

## 21. V0.1 no debe contaminarse

La auditoría descubrió muchas funcionalidades futuras, pero **no deben entrar automáticamente en V0.1**.

V0.1 sigue siendo:

- configuración;
- logging;
- SQLite;
- EventManager;
- TaskScheduler;
- ResourceManager;
- panel;
- internal.test;
- tests.

Las nuevas interfaces agregadas hasta ahora son infraestructura sin activación de motores externos.

---

## 22. Protocolo para futuras auditorías

Cada nuevo programa debe seguir:

### Paso 1 — Preservar
Guardar ZIP original.
Calcular SHA256.
Conservar release/origen.

### Paso 2 — Legal
Revisar:

- LICENSE;
- NOTICE;
- THIRD-PARTY;
- copyright;
- licencias de modelos/assets.

### Paso 3 — Arquitectura
Identificar:

- entrada;
- salida;
- eventos;
- colas;
- dependencias;
- persistencia;
- procesos;
- APIs.

### Paso 4 — Clasificar

🟢 REUTILIZABLE

🟡 ADAPTABLE

🔵 DEPENDENCIA EXTERNA

🟠 SOLO REFERENCIA

🔴 NO UTILIZAR

### Paso 5 — Extraer
Mover únicamente lo necesario.

### Paso 6 — Integrar
Preferir interfaces propias/adaptadores.

### Paso 7 — Probar
Unit tests + integración + recuperación.

### Paso 8 — Registrar
Actualizar esta bitácora.

### Paso 9 — Cerrar
Solo cuando todo lo útil esté preservado y la decisión esté documentada.

### Paso 10 — Ataúd
Después de borrar realmente una rama de auditoría:

Registrar su nombre exacto en `main/ataud`.

Nunca registrar una rama como eliminada antes de verificar su desaparición.

---

## 23. Regla para continuar después de una interrupción

Si una auditoría queda incompleta:

- guardar `STATUS`;
- guardar `LAST_READ`;
- guardar `NEXT_READ`;
- no repetir lo ya leído;
- continuar desde `NEXT_READ`.

El checkpoint vive en la rama de auditoría.
La síntesis vive en `main/BITACORA_AUDITORIA.md`.

---

## 24. Estado de auditoría actual

Expedientes evaluados: **32/33 cerrados (96,97%)**.

Pendiente:

- `audit/piper-master` — falta verificar exactamente el ZIP binario guardado.

### Importante

El 96,97% es el progreso de **auditoría**, no del producto.

Estimación actual:

- ahorro de investigación/arquitectura frente a empezar desde cero: **~60–70%**;
- avance adicional real del producto derivado de esta auditoría: **~5–10%**;
- estas cifras son estimaciones de planificación, no mediciones de horas.

---

## 25. Estado de Frankenstein

`frankenstein` es el espacio de integración/auditoría.

Actualmente contiene:

- informes;
- decisiones;
- trazabilidad;
- código fuente de referencia seleccionado;
- licencias;
- evidencia de procedencia.

No debe convertirse en un segundo producto.

Su función es permitir:

`auditar -> comparar -> seleccionar -> extraer -> integrar -> cerrar`

---

## 26. Estado de main

`main` contiene ya infraestructura derivada de la auditoría:

- proceso externo seguro;
- contratos de voz;
- contrato de avatar;
- contrato de Twitch;
- contrato de OBS.

`main` continúa siendo el producto real.

---

## 27. Decisión estratégica final de esta auditoría

La mejor arquitectura encontrada hasta ahora es:

`CORE PROPIO`
+
`ADAPTADORES PROPIOS`
+
`MOTORES LOCALES REEMPLAZABLES`
+
`APLICACIONES EXTERNAS POR API/PROTOCOLO`

No construir una copia de Streamer.bot.
No construir una copia de OBS.
No construir una copia de VTube Studio.
No construir un clon de Speaker.bot.

Construir **nuestro propio sistema**, utilizando las soluciones existentes donde aporten valor y manteniendo una frontera clara entre nuestro código y el software externo.


## 28. Limpieza de ramas — 2026-10-06

Se eliminaron y verificaron 32 ramas de auditoría cerradas. Solo permanece `audit/piper-master` porque la revisión del ZIP histórico exacto quedó pendiente.

La eliminación de una rama no elimina automáticamente los Releases `audit-*` ni sus assets; esos elementos se consideran una limpieza separada.


## 29. Corrección de estado de ramas — 2026-10-06

La sección 24 conserva el estado histórico registrado cuando `audit/piper-master` seguía pendiente de verificación.

Al revisar nuevamente el estado real de GitHub para esta corrección, `audit/piper-master` ya no existe como rama. La única rama existente actualmente es `main`.

Por lo tanto:

- `audit/piper-master` ya no está pendiente como rama de auditoría.
- Su eliminación se registra en `ataud`, junto con las demás ramas de auditoría eliminadas.
- La entrada histórica de la sección 24 se conserva sin reescribirla, para mantener la trazabilidad del estado que existía en ese momento.

## 30. ChatSpeak Studio — integración de IA remota y voz virtual (2026-10-13)

Trabajo registrado en la rama product/complete-vtuber-experience:

- Se añadió un proveedor de chat remoto HTTPS OpenAI-compatible y un router que cambia entre proveedor remoto y el adaptador local restringido a loopback.
- Gemini OpenAI-compatible se propone como configuración inicial para evitar una inferencia pesada local; la voz remota usa Gemini TTS/Interactions por defecto y permite un endpoint TTS OpenAI-compatible.
- La cola existente de voz se conserva, con sesiones inyectables para alternar SAPI y el TTS remoto. La voz inicial se instruye como femenina, juvenil, cálida y de estilo anime original; no clona a una actriz o personaje específico.
- Las claves remotas se protegen mediante DPAPI para el usuario actual y quedan fuera de SQLite y del registro de actividad.
- El modo manual abre ChatGPT y facilita copiar el prompt/pegar la respuesta. No se inspecciona el DOM, cookies ni la sesión web.
- Los comandos del modelo están en una lista permitida, se limitan a cuatro por salida y las líneas con barra se retiran de la voz y del texto público. Cambiar chat, reiniciar, activar o desactivar el bot son controles manuales del creador.
- El comentario periódico recoge un máximo acotado de mensajes en memoria y usa un intervalo configurable con cinco minutos iniciales. Los lotes no se guardan como transcripción, se eliminan al pausar/desconectar y se cancelan cuando se desactiva el modo.
- Estados Angry y LeftEyeClose agregados a ProductStore, Avatar Studio y al renderizador nativo. Si falta un sprite, el motor no afirma que la expresión se mostró.
- Estado en esta anotación: cambios preparados para registrar; Windows CI y prueba de API/audio real deben acreditarse con el SHA final exacto. No se afirma una prueba manual con una cuenta real.
