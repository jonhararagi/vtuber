# VOICEVOX Engine local desde VTuberBot

Esta guía describe cómo utilizar el TTS local. El motor y las bibliotecas de voz no se incluyen en el paquete autocontenido de VTuberBot porque sus licencias y condiciones de redistribución deben respetarse por separado.

## 1. Obtener VOICEVOX desde la fuente oficial

- Sitio oficial de descarga: https://voicevox.hiroshiba.jp/
- Repositorio oficial del Engine y documentación: https://github.com/VOICEVOX/voicevox_engine
- Documentación de la API local: http://127.0.0.1:50021/docs (solo estará disponible mientras el Engine esté iniciado en el puerto por defecto).
- Condiciones de uso del programa y las voces: https://voicevox.hiroshiba.jp/term/
- Preguntas frecuentes y atribución de voces: https://voicevox.hiroshiba.jp/qa/

Instalá VOICEVOX Engine para Windows siguiendo las instrucciones oficiales y las condiciones vigentes. Las voces y bibliotecas no necesariamente comparten una sola licencia: revisá las reglas de la voz/estilo que elijas y cualquier requisito de atribución antes de usarla en público.

**Límite de idioma importante:** las voces habituales de VOICEVOX están orientadas al japonés. La API puede aceptar texto en otros idiomas, pero eso no convierte una voz japonesa en una voz española y la pronunciación natural del español no está garantizada. Por eso la prueba predeterminada usa una frase japonesa. Si necesitás narración natural en español, hay que escoger un motor/voicebank que declare soporte real para español; VTuberBot no debe fingir que VOICEVOX lo ofrece.

## 2. Configurarlo dentro de VTuberBot

1. Abrí **Voz y audio**.
2. En **Dirección local de VOICEVOX Engine**, usá http://127.0.0.1:50021, salvo que el Engine local esté configurado en otro puerto.
3. Pulsá **Elegir run.exe oficial** y seleccioná run.exe dentro de vv-engine de tu instalación oficial. Una ruta habitual de instalación de usuario es %LOCALAPPDATA%\Programs\VOICEVOX\vv-engine\run.exe; puede variar según el método de instalación. No selecciones un ejecutable descargado de una fuente no oficial.
4. Elegí el **límite de hilos de CPU**. VTuberBot empieza con 2 hilos por defecto para dejar recursos libres mientras transmitís; podés seleccionar entre 1 y 6. Este valor limita el paralelismo de síntesis, no la memoria del proceso. Para aplicar un cambio de hilos, reiniciá Engine.
5. Pulsá **Iniciar VOICEVOX Engine en segundo plano**. VTuberBot comprueba primero si el Engine ya está respondiendo. Si no lo está, solicita un inicio ligado a 127.0.0.1, con el puerto configurado y el límite de hilos escogido, y espera la API local.
6. Pulsá **Comprobar y actualizar voces VOICEVOX**. Elegí uno de los estilos devueltos por el Engine. Si no aparece ninguno, comprobá instalación, versión, carga de voces y mensaje de error.
7. Elegí un **dispositivo de salida** si no querés usar el predeterminado del sistema.
8. Ajustá **Tono / pitch** (de -0,30 a +0,30) y **Variación de entonación** (de 0,50× a 1,50×). Un tono positivo eleva la altura y una variación mayor exagera más los cambios de tono; probá valores suaves para evitar artefactos.
9. Pulsá **Guardar voz y ajustes** para conservar URL, voz, tono, entonación, volumen, velocidad y salida.
10. Pulsá **Probar voz VOICEVOX** para escuchar una prueba. Usá **Encolar texto VOICEVOX** para texto propio, y **Detener voz actual** para interrumpir la frase.
11. Cuando quieras liberar los recursos del motor, pulsá **Detener Engine iniciado por VTuberBot** y confirmá. Ese botón solo puede detener el proceso que abrió esta instancia del programa. Si Engine ya estaba activo antes de abrir VTuberBot, la aplicación no lo cerrará automáticamente.

La app no debe indicar que la conexión está lista si la API no respondió. Tampoco debe usar una voz Windows/Gemini de manera silenciosa para reemplazar un fallo local. Si aparece “VOICEVOX no disponible”, seguí el diagnóstico y volvé a comprobar.

## 3. Avatar Idle/Talking

En **Avatar Studio**, asigná tu imagen de boca cerrada a Idle y una imagen compatible de boca abierta a Talking. Durante la reproducción real, la aplicación cambia a Talking y vuelve a Idle al terminar o detenerse. La aplicación no genera una segunda boca por sí sola ni realiza sincronización fonema a fonema.

Si solo tenés una imagen, podrás utilizar el avatar estático, pero no habrá un cambio de boca visible. Usá arte propio o arte para el que tengas permiso.

## 4. Seguridad, procesos y memoria

- VTuberBot limita el inicio integrado del Engine a una URL HTTP local y lo liga a 127.0.0.1; no uses ni publiques una dirección del Engine expuesta a la red.
- La voz se genera en el Engine local. Esta función no necesita un token de Gemini ni una API TTS remota.
- El Engine es un proceso separado: su consumo se suma al del resto de aplicaciones aunque no cuente como memoria del proceso VTuberBot. El objetivo de alerta de 1 GiB corresponde al proceso VTuberBot, no al conjunto de todo el sistema.
- En **Voz y audio**, usá **Medir CPU/RAM del proceso VOICEVOX** para muestrear brevemente el proceso cuyo ejecutable coincide con la ruta configurada. Es una lectura puntual de CPU y memoria residente, no un historial ni una medición de GPU. Si el proceso no se puede identificar o Windows restringe el acceso, la interfaz lo informa en lugar de atribuir cifras de otro programa.
- VTuberBot no incluye el Engine/voicebanks ni los actualiza automáticamente. Conservá la instalación oficial; las opciones y el comportamiento pueden variar por versión.
- El botón **Detener Engine iniciado por VTuberBot** permite finalizar únicamente el proceso que abrió esta instancia. Al cerrar VTuberBot, si ese proceso sigue activo, se pregunta si querés detenerlo (Sí), dejarlo activo para otro cliente local (No) o cancelar el cierre. Si el cierre no puede confirmarse, la aplicación avisa que el proceso podría seguir consumiendo memoria.

## 5. Licencias y atribución

No distribuyas el Engine o bibliotecas de voz dentro de un paquete de VTuberBot sin verificar permisos de redistribución y cumplimiento de avisos/licencias. Conservá los enlaces oficiales y los créditos requeridos por VOICEVOX y por la biblioteca de voz seleccionada. Las condiciones particulares pueden cambiar, así que volvé a consultar las páginas oficiales antes de publicar o compartir una distribución.
