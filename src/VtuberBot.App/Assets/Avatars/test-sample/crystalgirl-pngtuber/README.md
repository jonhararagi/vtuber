# CrystalGirl PNGTuber · paquete multestado de prueba

Este paquete es una **muestra provisional de avatar completo** para la futura prueba de cambio visual en WPF. No es el personaje definitivo del VTuber Bot.

## Qué permite probar

- Cambiar entre reposo y habla usando dos imágenes completas.
- Simular parpadeo sin mezclar capas.
- Alternar expresiones felices y tristes, tanto en reposo como hablando.
- Verificar que los cambios de imagen mantienen el encuadre del personaje.

Las imágenes son fotogramas PNG completos; **no son una hoja de sprites ni piezas para componer por separado**.

## Mapeo de estados

| Archivo | Estado visual |
|---|---|
| `CrystalGirl_NeutralIdle.png` | Reposo neutral; ojos abiertos, boca cerrada |
| `CrystalGirl_NeutralTalk.png` | Hablando en neutral; ojos abiertos, boca abierta |
| `CrystalGirl_BlinkIdle.png` | Parpadeo en reposo; ojos cerrados |
| `CrystalGirl_BlinkTalk.png` | Parpadeo mientras habla; ojos cerrados y boca abierta |
| `CrystalGirl_HappyIdle.png` | Expresión feliz, reposo |
| `CrystalGirl_HappyTalk.png` | Expresión feliz, hablando |
| `CrystalGirl_SadIdle.png` | Expresión triste, reposo |
| `CrystalGirl_SadTalk.png` | Expresión triste, hablando |

## Inventario y verificación

Los ocho archivos se inspeccionaron desde sus bytes PNG reales:

- Dimensiones de cada archivo: **735 × 1388 píxeles**.
- Formato: PNG RGBA, 8 bits por canal.
- Cada imagen contiene **430.086 píxeles con alfa 0** y **447.555 píxeles con alfa menor que 255**.
- Caja delimitadora de píxeles visibles idéntica: `x=35..702, y=27..1366`.
- Los fotogramas observados mantienen el personaje completo en la misma posición. Esto es adecuado para cambiar imágenes completas; no demuestra que las partes del rostro sean capas independientes.

El SHA listado en cada fila de `LICENSES.md` es el **SHA-1 del objeto Git de origen**, no un hash SHA-256. Coincide con el blob público del que se copiaron los bytes.

## Procedencia

- Autor del recurso: **Tsuchikabi**.
- Publicación original que declara la licencia CC0: https://www.deviantart.com/tsuchikabi/art/Commercial-FREE-Download-PNGTuber-Vtuber-Assets-1335268487
- Ubicación pública utilizada para obtener los PNG originales: https://github.com/atrox37/CheBo/tree/main/public/chebo/crystalgirl/PNGTuber_CrystalGirl
- La atribución y los enlaces se conservan aunque CC0 no exige crédito. Consulta `LICENSES.md` para el registro específico de derechos.

## Limitaciones

- No hay capas de cuerpo, cabeza, cabello, ojos o boca independientes.
- No incluye animación continua, sincronización real con voz ni integración de micrófono.
- El cambio de estado será una sustitución de imagen completa en una implementación futura.
- El runtime WPF no se modificó; este paquete todavía no se carga ni se muestra en la aplicación.
- La validación visual real en la ventana WPF queda pendiente de una tarea autorizada posterior.

Mantener estos archivos en `test-sample/crystalgirl-pngtuber/`, separados de `default/` y de cualquier personaje definitivo.
