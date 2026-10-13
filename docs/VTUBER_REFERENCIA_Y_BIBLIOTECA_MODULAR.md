# Referencias visuales de VTubers y catálogo modular de VTuberBot

Fecha de investigación: 2026-10-13.

## Objetivo

Usar diseños conocidos para estudiar **lenguaje visual** y convertir esos principios en opciones originales de cabello, ropa, accesorios, color y silueta que el usuario pueda combinar dentro de **Avatar Studio**.

No se han descargado ni recortado modelos oficiales para redistribuirlos. La ilustración, el diseño exacto, los logos, los accesorios distintivos y los archivos de modelo de cada VTuber pertenecen a sus respectivos titulares. Las referencias de abajo son para análisis de diseño; el catálogo integrado genera dibujos originales con formas vectoriales y PNG transparentes.

## Muestras investigadas

| VTuber / fuente | Rasgos útiles para análisis de diseño | Qué conviene separar para aprender del lenguaje visual |
|---|---|---|
| [Hoshimachi Suisei — perfil oficial](https://hololive.hololivepro.com/en/talents/hoshimachi-suisei/) | Identidad de idol musical; se presta al contraste de color, a una silueta de escenario y a motivos astrales. | Chaqueta escénica, falda, estrella, accesorios brillantes y peinado con una silueta reconocible. |
| [Sakura Miko — perfil oficial](https://hololive.hololivepro.com/en/talents/sakuramiko/) | Identidad de sacerdotisa de santuario e idol; los motivos ceremoniales pueden convivir con una silueta pop. | Mangas amplias, conjunto ceremonial rojo/ivory, lazo y adorno floral. |
| [Usada Pekora — perfil oficial](https://hololive.hololivepro.com/en/talents/usada-pekora/) | La propia biografía destaca orejas de conejo y zanahorias; un motivo animal ayuda a que la silueta sea legible incluso en tamaño pequeño. | Orejas separadas, lazos, accesorios pequeños y paleta de alto contraste. |
| [Houshou Marine — perfil oficial](https://hololive.hololivepro.com/en/talents/houshou-marine/) | Fantasía de capitana pirata, tesoro y joyas. | Abrigo de capitana, ribetes dorados, pendientes, sombrero o parche como categorías separadas. |
| [Gawr Gura — perfil oficial de alumna](https://hololive.hololivepro.com/en/talents/gawr-gura/) | La descripción oficial destaca el gorro de tiburón que forma parte de su ropa. | Gorro temático, hoodie acuático, dientes/triángulos, paleta de azul y blanco. |
| [Mori Calliope — perfil oficial](https://hololive.hololivepro.com/en/talents/mori-calliope/) | Aprendiz de la Parca; permite estudiar cómo un concepto oscuro se traduce a ropa, acentos de color y utilería. | Abrigo gótico, contraste oscuro/rojo, cadena y accesorio de utilería. |
| [Tokoyami Towa — perfil oficial](https://hololive.hololivepro.com/en/talents/tokoyami-towa/) | Concepto de demonio joven y gamer. | Cuernos, auriculares, conjunto negro/violeta y accesorios de gaming. |
| [Takanashi Kiara — perfil oficial](https://hololive.hololivepro.com/en/talents/takanashi-kiara/) | Concepto de fénix con identidad de idol. | Plumas, dorados y naranjas, chaqueta de escenario y formas de llama. |
| [Robocosan — perfil oficial](https://hololive.hololivepro.com/en/talents/roboco-san/) | Concepto de robot con personalidad gamer. | Paneles tecnológicos, piezas mecánicas, LEDs y formas geométricas. |
| [Kuzuha — perfil oficial de NIJISANJI](https://www.nijisanji.jp/en/talents/l/kuzuha) | Fantasía de vampiro gamer. | Capas oscuras, abrigo urbano, acento carmesí y accesorios discretos. |
| [Vox Akuma — perfil oficial de NIJISANJI](https://www.nijisanji.jp/en/talents/l/vox-akuma) | Concepto de demonio antiguo. | Capas largas, bordes ornamentales, contraste negro/rojo/ivory y pendientes. |
| [Ironmouse Model Index — archivo fan con atribuciones](https://www.ironmousemodelindex.com/home) | Archivo comunitario de múltiples modelos y atuendos; sirve para ver cómo un mismo personaje puede tener versiones alternativas. El propio sitio advierte que es fanmade y sigue en desarrollo. | Separar silueta, cabello, accesorios, ropa alternativa y expresiones; no tratar el archivo como licencia para redistribuir los modelos. |

## Catálogo original que se integra en Avatar Studio

Las piezas se dibujan localmente en un lienzo común de 512 × 768 y se guardan como PNG con transparencia bajo la carpeta local de datos de VTuberBot. No se necesita internet ni API de imágenes para generarlas.

- **Cabello (11):** coleta alta carmesí, coletas dobles violetas, bob azul noche, ondas rosas, plata/cian, trenza verde marina, corto despeinado, rizos dorados, largo liso índigo, trenza lateral coral y dos moños redondos.
- **Conjuntos (15):** idol estelar, uniforme marinero, gótico romántico, ceremonial rojo, streetwear tecnológico, capitana nocturna, armadura fantástica ligera, hoodie pastel, vestido celestial, cyber runner, chaqueta idol casual, yukata de festival, túnica de maga, vestido de baile real y abrigo invernal.
- **Accesorios de cabeza (14):** lazos, orejas de gato, orejas de conejo, cuernos, halo estelar, gorra tiburón original, corona, auriculares gamer, flores, pluma, orejas de zorro, sombrero de bruja, tiara lunar y visor tecnológico.
- **Accesorios faciales / joyería (9):** gafas redondas, pendientes de estrella, choker con corazón, parche estelar, micrófono de diadema, colgante geométrico, mascarilla urbana, estrellas en mejillas y lazo de cuello.
- **Piezas traseras (9):** capa corta, alas demoníacas, alas de luz, mochila conejito, aletas acuáticas, cintas estelares, cola esponjosa, mochila mecánica y cristales flotantes.
- **Configurador de color por zona:** cabello, ropa, accesorio de cabeza, cara/joyería y espalda se pueden teñir por separado con valores HEX #RRGGBB o muestras rápidas. El sistema conserva la transparencia y procura respetar el contorno oscuro y los brillos claros. El original permanece intacto; las variantes teñidas se guardan en otra carpeta y se usan en un perfil nuevo.

Cada pieza existe como capa independiente. El personaje de base usa una prenda neutra/modesta; la ropa, cabello y accesorios son overlays separados. Se pueden mover, escalar, rotar, ocultar o reordenar en el editor. La combinación se crea como **un perfil nuevo**, para no sobreescribir perfiles existentes.

## Programas y catálogos externos que conviene aprovechar

Estos enlaces son fuentes externas para crear o encontrar recursos. **No significa que sus archivos se hayan descargado e incluido automáticamente en VTuberBot**.

| Fuente | Para qué sirve | Cómo encaja con este proyecto |
|---|---|---|
| [VRoid Studio — gratuito](https://vroid.com/en/studio) | Crear personajes 3D y personalizar pelo, ropa, cara y texturas; exporta modelos VRM. | Es una vía potente para diseñar un modelo completo. La versión actual de Avatar Studio trabaja con imágenes PNG en capas, así que no puede importar VRM directamente todavía. |
| [Krita — dibujo digital libre](https://krita.org/es/) | Dibujar pelo, prendas, ojos, accesorios y sprites con capas y transparencia. | Útil para crear PNG separados y luego importarlos en Avatar Studio. |
| [OpenGameArt — recursos y licencias](https://opengameart.org/content/faq) | Buscar sprites, piezas y fondos. El sitio admite distintas licencias; algunas permiten uso comercial con atribución y otras añaden condiciones. | Preferir CC0 o recursos con licencia claramente compatible; guardar autor, enlace y licencia antes de empaquetar un recurso. |
| [itch.io — recursos gratuitos](https://itch.io/game-assets/free) | Buscar personajes, sprites, fondos, accesorios 2D y assets 3D, con filtros por formato y estilo. | Gratis no equivale a dominio público: revisar la licencia y las condiciones de cada paquete individual antes de redistribuirlo. |
| [Live2D Cubism — planes FREE/PRO](https://www.live2d.com/es/cubism/comparison/) | Separar y riggear ilustraciones para animación Live2D. La edición gratuita tiene limitaciones respecto a PRO. | Alternativa para una etapa futura con deformación/rig avanzado; no es un importador automático de rigs dentro del Avatar Studio actual. |
| [BOOTH — ejemplos de modelos y accesorios](https://booth.pm/en/search/VTuber) | Catálogo de modelos y accesorios de creadores, gratuitos y de pago. | Revisar la licencia de cada producto. No redistribuir modelos de pago o archivos descargados solo porque se permita usarlos en streams. |

### Flujo recomendado con fuentes externas

1. Buscar una pieza con licencia expresa para el uso previsto; guardar título, autor, URL, licencia y si admite modificaciones/redistribución.
2. Abrirla en Krita para separar cabello, ropa y accesorios en PNG transparentes del mismo tamaño, si la licencia permite modificarla.
3. Importar la imagen base o cada pieza desde los botones de importación de Avatar Studio y ajustar la capa.
4. No añadir al repositorio assets externos hasta comprobar tanto la licencia como las condiciones de redistribución. Los perfiles personales pueden usar archivos importados localmente sin convertirlos en contenido distribuido junto al programa.

## Límites actuales

- Es un sistema de sprites 2D por capas, con movimiento/parallax ligero y sprites de expresión; **no** es Live2D, no deforma mallas o huesos, y no hace tracking facial por cámara.
- El configurador de color se aplica al crear un perfil desde el armario modular; no es todavía un editor de pintura de píxel libre ni un selector de color que modifique en vivo cualquier PNG arbitrario.
- El archivo de diseño de una referencia puede enlazar a imágenes oficiales, pero VTuberBot no almacena ni redistribuye esos dibujos ni incorpora automáticamente archivos de sitios externos.
- Después de la compilación y pruebas de Windows sigue pendiente la comprobación visual manual en el PC del usuario, especialmente la calidad de solapamiento de cada combinación de ropa, pelo y accesorios.
