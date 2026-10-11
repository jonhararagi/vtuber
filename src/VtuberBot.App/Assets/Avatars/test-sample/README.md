# Avatar de prueba: Marmalade (PNG estático)

Este recurso es **solo una muestra provisional** para la futura prueba visual del renderizador WPF. No es el personaje definitivo del VTuber Bot.

## Contenido e inventario

| Archivo | Dimensiones | Formato | Transparencia verificada | Uso |
|---|---:|---|---|---|
| `marmalade-pngtuber-model.png` | 1346 × 1392 px | PNG RGBA, 8 bits por canal | Sí: 746.066 píxeles totalmente transparentes; 762.002 píxeles con alfa menor que 255 | Imagen estática completa del personaje |

La imagen original no se recortó, redimensionó ni recoloreó. Se comprobó el encabezado PNG y se descomprimieron sus datos de imagen para contar los valores alfa. El SHA-1 de objeto Git del archivo original es `3c11ff55082d2a9451b4d8c146b288e98fdfe9ca`.

## Procedencia y permiso

- Personaje: **Marmalade**, creado por **ioiototm**.
- Imagen original: https://github.com/ioiototm/marmalade.you/blob/master/static/images/marmalade-pngtuber-model.png
- Página del paquete de origen: https://github.com/ioiototm/marmalade.you/blob/master/content/lab/marmalade-pngtuber-remix-assets/index.md
- El autor identifica el personaje como CC0 en el README del proyecto y la página específica del PNGTuber establece `license: "CC0"`.
- Licencia aplicable a este recurso: CC0 1.0 Universal. Consulta `LICENSES.md`.

Se conserva la procedencia como buena práctica aunque CC0 no exige atribución.

## Limitaciones importantes

Este paquete incluye **una única imagen aplanada**. No contiene capas separadas de cuerpo/cabeza/cabello, variantes de ojos abiertos/cerrados ni bocas alternativas. Por tanto:

- Sirve como muestra visual estática para una futura prueba de carga y composición WPF.
- No sirve, por sí sola, para probar parpadeo, expresiones intercambiables ni movimiento de boca.
- No se debe anunciar como avatar animado ni como el Avatar MVP terminado.
- La integración en ejecución no está hecha. No se modificaron XAML, C#, el proyecto .NET, los tests ni los workflows.

## Uso previsto

Mantener el archivo en esta carpeta `test-sample`, separado de cualquier avatar definitivo. Cuando se autorice la implementación, el renderizador podrá cargarlo como una sola imagen PNG y usar `Uniform`/ajuste proporcional para presentarlo sin deformación. La prueba visual en Windows sigue siendo necesaria.
