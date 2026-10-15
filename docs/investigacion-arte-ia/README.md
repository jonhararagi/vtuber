# Investigación de plataformas de arte con IA para VTuberBot

**Fecha:** 2026-10-15  
**Objetivo:** orientar la creación de recursos originales para Avatar Studio. Este catálogo no descarga ni incorpora automáticamente obras de terceros.

## Decisión ejecutiva

VTuberBot necesita una biblioteca local que permita importar, clasificar y reutilizar recursos por personaje, cabello, mechones interiores, ojos, pupilas, ropa, accesorios, expresiones, poses, fondos y objetos. La aplicación no debe depender de una web ni de una API de IA paga para funcionar.

Las plataformas citadas son servicios externos; sus límites, precios, privacidad y licencias pueden cambiar. Antes de usar una imagen o modelo en una emisión pública o distribución del programa, revisar las condiciones actuales y la licencia concreta del recurso.

## Plataformas investigadas

### 1. PixAI — referencia principal para anime
- Web: https://pixai.art/en
- Generador: https://pixai.art/en/ai-tools/ai-anime-generator
- Términos: https://pixai.art/en/terms
- API oficial: https://platform.pixai.art/en

Sus páginas oficiales describen generación anime desde texto, imagen-a-imagen, modelos y LoRA comunitarios, retoque, ampliación y animación. La web anuncia créditos diarios gratuitos, sujetos a las reglas vigentes.

**Para VTuberBot:** explorar estilos, conceptos de personaje, variaciones de vestuario, chibis y fondos. Guardar localmente los prompts y la procedencia de los recursos elegidos.

**Precaución:** sus términos indican que la generación debe realizarse por los medios autorizados; para integraciones, consultar la API oficial. No hacer scraping ni automatizar la web. No asumir que todo modelo comunitario permite redistribución o uso comercial.

### 2. NovelAI — composición y arte anime
- Web: https://novelai.net/
- Documentación: https://docs.novelai.net/en/image/
- Modelos: https://docs.novelai.net/en/image/models/

Su documentación describe generación centrada en anime, etiquetas/prompts, distintos modelos, resolución y ajustes de composición.

**Para VTuberBot:** estudiar cómo describir rasgos persistentes, expresiones, vestuario y composición. El acceso a modelos y funciones depende del plan vigente; no debe considerarse una solución local gratuita garantizada.

### 3. Tensor.Art — modelos comunitarios
- Web: https://tensor.art/
- Términos: https://tensor.art/about/terms-of-service-new

Útil para investigar modelos y estilos comunitarios de anime, cabello, ropa, chibi y fondos. Registrar autor, URL, versión y licencia de cada modelo.

**Precaución:** sus términos dicen que no se debe republicar un modelo sin consentimiento del autor y que el uso comercial debe respetar la disponibilidad comercial del modelo. No copiar pesos ni modelos al repositorio sin permiso y licencia verificables.

### 4. SeaArt — generación y exploración de estilos
- Web: https://www.seaart.ai/
- Guía en español: https://docs.seaart.ai/guide-1/espanol/7-preguntas-frecuentes

Alternativa para explorar estilos y generar conceptos. Su guía indica que el uso comercial de obras ajenas requiere autorización de sus titulares.

**Precaución:** revisar términos actuales, condiciones de cada modelo y tratamiento de archivos cargados. No guardar tokens o cookies de sesión en Git.

### 5. Civitai — descubrimiento de modelos y permisos
- Web: https://civitai.com/
- Documentación de permisos: https://github.com/civitai/civitai-developer-docs/blob/main/site/reference/permissions.md

Útil para investigar autor, versión y permisos de modelos. La documentación de desarrolladores describe comprobaciones de permisos de generación para determinados recursos.

**Precaución:** cada modelo puede tener términos distintos. Que un archivo se pueda descargar no demuestra que pueda redistribuirse o usarse comercialmente.

### 6. Hugging Face — repositorios con tarjetas y licencias
- Web: https://huggingface.co/

Permite investigar modelos y herramientas publicados con licencia y documentación. Revisar la tarjeta del modelo, la licencia, los requisitos de hardware y las limitaciones. No todos los modelos son abiertos, ligeros o adecuados para uso comercial.

### 7. ComfyUI — generación local modular
- Proyecto: https://github.com/comfyanonymous/ComfyUI

Opción para usuarios avanzados que quieran flujos locales reproducibles. No debe ser dependencia obligatoria. Con Ryzen 5 5600G y 16 GB de RAM no se debe prometer buen rendimiento sin probar modelo, memoria y GPU concretos.

### 8. Krita AI Diffusion — retoque y preparación de arte
- Proyecto: https://github.com/Acly/krita-ai-diffusion

Puede ayudar a retocar imágenes y preparar capas tras generar conceptos. Es una herramienta externa, no reemplaza la biblioteca de recursos ni el editor de avatar. Revisar requisitos, backend y licencias.

## Flujo de trabajo recomendado

1. Explorar plataformas y crear arte original.
2. Guardar el prompt, el modelo y la versión cuando se conozcan.
3. Verificar licencia, autoría, atribución, uso comercial y redistribución.
4. Importar manualmente archivos locales; no extraer cookies ni descargar recursos ocultamente.
5. Clasificar por personaje, cuerpo/base, cabello trasero/delantero, mechones interiores, ojos, iris, pupilas, cejas, boca, ropa, calzado, accesorios, expresiones, poses, fondos y objetos.
6. Validar transparencia, dimensiones, formato y recorte.
7. Componer variantes sin sobrescribir los originales.

## Metadatos recomendados por recurso

- Identidad: id, nombre, categoría, subcategoría, personaje y etiquetas.
- Archivo: ruta local, formato, ancho, alto y transparencia.
- Procedencia: URL, autor, plataforma, modelo, versión, prompt y semilla.
- Derechos: licencia, permiso comercial, permiso de redistribución, atribución requerida y estado de revisión.
- Gestión: fecha de importación, fecha de revisión de licencia y notas.

Si se desconoce la licencia, mostrar **“pendiente de revisión”**; nunca darla por aprobada automáticamente.

## Recomendaciones para implementar en VTuberBot

**Prioridad alta:** biblioteca modular real en Avatar Studio, con importación local, filtros, etiquetas, favoritos, previsualización, asignación a capas y metadatos de procedencia/licencia.

**Prioridad media:** plantillas de prompts editables para conservar la identidad visual del personaje: tono de piel, cabello, mechones interiores, iris/pupilas, ropa, estilo, expresiones, poses, fondos y objetos.

**Posterior:** integración opcional mediante API oficial únicamente si existe una necesidad concreta, el usuario aporta su clave y la app sigue funcionando sin esa integración. Nada de scraping, APIs privadas o automatización contraria a los términos.

## Fuentes consultadas
- PixAI: https://pixai.art/en/ai-tools/ai-anime-generator
- PixAI Terms: https://pixai.art/en/terms
- PixAI Platform API: https://platform.pixai.art/en
- NovelAI docs: https://docs.novelai.net/en/image/
- NovelAI models: https://docs.novelai.net/en/image/models/
- Tensor.Art terms: https://tensor.art/about/terms-of-service-new
- SeaArt guide: https://docs.seaart.ai/guide-1/espanol/7-preguntas-frecuentes
- Civitai permissions: https://github.com/civitai/civitai-developer-docs/blob/main/site/reference/permissions.md
- ComfyUI: https://github.com/comfyanonymous/ComfyUI
- Krita AI Diffusion: https://github.com/Acly/krita-ai-diffusion

Esto es investigación técnica, no asesoramiento jurídico. Revisar las condiciones vigentes antes de distribuir contenido o modelos.
