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

- **Cabello (8):** coleta alta carmesí, coletas dobles violetas, bob azul noche, ondas rosas, plata/cian, trenza verde marina, corto despeinado y rizos dorados.
- **Conjuntos (10):** idol estelar, uniforme marinero, gótico romántico, ceremonial rojo, streetwear tecnológico, capitana nocturna, armadura fantástica ligera, hoodie pastel, vestido celestial y cyber runner.
- **Accesorios de cabeza (10):** lazos, orejas de gato, orejas de conejo, cuernos, halo estelar, gorra tiburón original, corona, auriculares gamer, flores y pluma.
- **Accesorios faciales / joyería (6):** gafas redondas, pendientes de estrella, choker con corazón, parche estelar, micrófono de diadema y colgante geométrico.
- **Piezas traseras (6):** capa corta, alas demoníacas, alas de luz, mochila conejito, aletas acuáticas y cintas estelares.

Cada pieza existe como capa independiente. El personaje de base usa una prenda neutra/modesta; la ropa, cabello y accesorios son overlays separados. Se pueden mover, escalar, rotar, ocultar o reordenar en el editor. La combinación se crea como **un perfil nuevo**, para no sobreescribir perfiles existentes.

## Límites actuales

- Es un sistema de sprites 2D por capas, con movimiento/parallax ligero y sprites de expresión; **no** es Live2D, no deforma mallas o huesos, y no hace tracking facial por cámara.
- El archivo de diseño de una referencia puede enlazar a imágenes oficiales, pero VTuberBot no almacena ni redistribuye esos dibujos.
- Antes de considerar terminada la incorporación, el CI de Windows debe compilar, ejecutar pruebas y publicar un ZIP asociado al SHA exacto. Después sigue pendiente la comprobación visual manual en el PC del usuario.
