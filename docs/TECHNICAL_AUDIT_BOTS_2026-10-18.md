# Auditoría técnica: Bots y plataformas Twitch/Kick — 2026-10-18

## Resumen

Se incorpora una página principal **Bots y plataformas**. Reutiliza el cliente de chat y EventSub de Twitch ya existentes y agrega una primera integración de Kick para autorizar una aplicación propia, consultar un canal por API y enviar un mensaje de prueba con confirmación previa.

No debe describirse todavía como una solución de chat entrante unificada de Twitch y Kick. Twitch ya procesa mensajes IRC en los servicios existentes; Kick todavía no entrega mensajes entrantes al router de comandos ni a la lectura TTS. La página lo dice en su interfaz para evitar una validación ficticia.

## Qué está implementado en el código

- La navegación muestra **Bots y plataformas**.
- Pestaña **Twitch**: reutiliza el panel existente; incluye OAuth, conexión/desconexión de chat, EventSub, perfiles de bot y las comprobaciones existentes. La cuenta y sus permisos siguen siendo de Twitch.
- Pestaña **Comportamiento del bot**: reutiliza la configuración de ChatSpeak/voz. El procesamiento actual se alimenta del chat de Twitch; no se afirma compatibilidad de respuestas entrantes en Kick.
- Pestaña **Streamer.bot (opcional)**: reutiliza el cliente existente, sin sustituirlo por el router de reglas principal.
- Pestaña **Kick**: usa OAuth Authorization Code + PKCE S256 para una aplicación que el usuario crea en Kick Developer; mantiene el Client Secret, access token y refresh token cifrados mediante DPAPI para el usuario actual de Windows; conserva fecha de expiración y scopes; puede renovar el token si Kick entrega refresh token; consulta el canal real mediante la API pública y permite enviar un mensaje de prueba tras una confirmación explícita.
- Kick recibe solo scopes de lectura de usuario/canal y escritura de chat necesarios (user:read, channel:read, chat:write). No se solicitan scopes para acciones no implementadas.
- El estado de Kick no dice "conectado" por el simple hecho de guardar credenciales; solo la consulta real puede confirmar que el token fue aceptado por la API.

## Límites conocidos y próximos pasos

1. **Chat y eventos entrantes de Kick no están conectados todavía.** Kick ofrece eventos por webhooks; el endpoint debe ser público y la firma de cada entrega debe verificarse usando el esquema oficial. No se debe aceptar una URL de webhook arbitraria ni ejecutar acciones con eventos sin validar.
2. La aplicación de escritorio usa credenciales de su propio usuario: cada usuario crea su app Kick y registra exactamente http://localhost:17564/callback/. El Client Secret se cifra localmente, pero sigue siendo un secreto que el propio usuario tiene que provisionar. No incluirlo nunca en el repositorio ni en el instalador.
3. En esta primera integración, un mensaje publicado por Kick es una prueba de envío iniciada manualmente. No implica que VTuberBot ya copie automáticamente todos los comandos, moderación, lectura TTS, alertas o recompensas de Twitch a Kick.
4. No se hizo una sesión real con las credenciales personales de Kick ni se envió un mensaje en un canal ajeno durante CI. La API se comprueba solamente cuando el usuario inicia ese flujo.
5. Twitch/OBS/Kick dependen de tokens, configuración externa, red y permisos de cada cuenta. CI solo puede acreditar compilación y pruebas automatizadas, no que las cuentas personales estén autorizadas o en vivo.

## Errores reportados públicamente y qué hacemos al respecto

### 1. Kick OAuth: la redirección localhost debe coincidir exactamente

En [KickDevDocs issue #211](https://github.com/KickEngineering/KickDevDocs/issues/211) se documentó una diferencia en la normalización de la URL de redirección entre 127.0.0.1 y localhost. La recomendación práctica es registrar la misma cadena en Kick Developer y en el intercambio del código. La integración usa y muestra exactamente http://localhost:17564/callback/ en ambas fases.

### 2. Kick OAuth desktop: dependencia problemática del Client Secret

El reporte abierto [KickDevDocs issue #413](https://github.com/KickEngineering/KickDevDocs/issues/413) explica que una app de escritorio no puede ocultar de forma segura un Client Secret embebido en el binario. La documentación de Kick sigue exigiendo client_secret en el intercambio/renovación. Para no empaquetar un secreto compartido, el panel sigue el enfoque BYOA (bring your own app): el usuario crea la app Kick y guarda su propio secreto en DPAPI. Esto mejora el almacenamiento local, pero agrega configuración; no convierte el secreto en una credencial segura para distribuir si se extrajera del PC del usuario.

### 3. Kick OAuth: HTTP 400 sin cuerpo al intercambiar el código

El reporte [KickDevDocs issue #428](https://github.com/KickEngineering/KickDevDocs/issues/428), creado el 3 de octubre de 2026, describe un 400 Bad Request sin cuerpo aunque el autor comprobó PKCE, scopes y redirect_uri. El issue figura cerrado, pero no contiene una respuesta pública con una corrección técnica verificable. VTuberBot informa el status HTTP y el cuerpo disponible, no inventa una causa y sugiere comprobar que Client ID/Secret provienen de la misma app, que redirect_uri coincide literalmente y que los scopes estén habilitados. Si persiste un 400 vacío, hay que tratarlo como un fallo sin diagnóstico suficiente y no como una contraseña incorrecta confirmada.

### 4. Kick webhooks: un proxy/WAF puede bloquear al emisor y simular una caída de Kick

En [KickDevDocs issue #403](https://github.com/KickEngineering/KickDevDocs/issues/403), un operador investigó cero entregas; los comentarios posteriores aclararon que una regla propia del proxy bloqueaba el User-Agent Go-http-client/2.0. La solución fue excluir la ruta del webhook de la regla equivocada o usar un endpoint adecuado sin esa interferencia. Si se incorpora el receptor en una fase posterior, la guía de diagnóstico debe comprobar llegada desde Internet, challenge de verificación, headers de firma y logs del proxy/WAF; no debe desactivar globalmente la protección anti-bots ni ignorar la firma.

### 5. Kick Chat API: los mensajes tipo Bot pueden necesitar configuración de la cuenta dueña del canal

El issue abierto [KickDevDocs issue #343](https://github.com/KickEngineering/KickDevDocs/issues/343) reporta HTTP 500 al enviar con type=bot, aunque type=user funciona. Los comentarios técnicos señalan dos distinciones útiles: el modo Usuario puede enviar al canal indicado con token de la cuenta emisora; el modo Bot está ligado al canal de la persona que autorizó el token y requiere crear/activar el bot para esa cuenta/app en Kick Developer. La interfaz usa Usuario por defecto; Bot es opt-in y verifica que el canal coincida con el token antes de enviar. No se intenta cambiar silenciosamente de identidad si el tipo Bot falla.

### 6. WPF/.NET 10: distinguir fallos de análisis de diseño de fallos de compilación/ejecución normales

[dotnet/wpf issue #11775](https://github.com/dotnet/wpf/issues/11775) recoge un fallo NETSDK1022 que afecta a DesignTimeBuild=true y al proyecto temporal *_wpftmp.csproj al referenciar tipos locales desde XAML. El workaround publicado afecta al análisis de diseño (evitar ese conjunto de propiedades o fijar SDK solo para esa herramienta); el propio reporte indica que la build normal puede funcionar. No se añade un global.json arbitrario para ocultar ese problema: el workflow de Windows ejecuta compilación y pruebas normales.

[dotnet/wpf issue #11678](https://github.com/dotnet/wpf/issues/11678) denuncia problemas de SDK 10.0.200/10.0.202 principalmente al publicar aplicaciones single-file WPF. VTuberBot distribuye actualmente un ZIP self-contained de varios archivos; ese reporte no es evidencia de que este modo de publicación falle. No cambiamos el SDK ni la estrategia de distribución sin una reproducción en el producto.

## Fuentes técnicas oficiales

- [Kick OAuth 2.1 — autorización, callback y refresh](https://github.com/KickEngineering/KickDevDocs/blob/main/getting-started/generating-tokens-oauth2-flow.md)
- [Kick scopes](https://github.com/KickEngineering/KickDevDocs/blob/main/scopes/scopes.md)
- [Kick Chat API](https://github.com/KickEngineering/KickDevDocs/blob/main/apis/chat.md)
- [Kick Events API](https://github.com/KickEngineering/KickDevDocs/blob/main/events/introduction.md)
- [Kick webhook security / firmas](https://github.com/KickEngineering/KickDevDocs/blob/main/events/webhook-security.md)
- [Kick issue #211: redirect URI](https://github.com/KickEngineering/KickDevDocs/issues/211)
- [Kick issue #413: client secret en clientes de escritorio](https://github.com/KickEngineering/KickDevDocs/issues/413)
- [Kick issue #428: 400 sin cuerpo en token exchange](https://github.com/KickEngineering/KickDevDocs/issues/428)
- [Kick issue #403: webhook bloqueado en un proxy/WAF](https://github.com/KickEngineering/KickDevDocs/issues/403)
- [Kick issue #343: HTTP 500 al usar type=bot y aclaraciones sobre autorización](https://github.com/KickEngineering/KickDevDocs/issues/343)
- [WPF issue #11775: NETSDK1022 en DesignTimeBuild](https://github.com/dotnet/wpf/issues/11775)
- [WPF issue #11678: problemas de SDK en publish single-file](https://github.com/dotnet/wpf/issues/11678)

## Validación pendiente

La CI de Windows debe validar el SHA final de esta rama y luego el SHA exacto tras el merge en main. Sigue pendiente en un PC Windows real completar OAuth Kick con una app creada por el usuario, probar lectura de metadatos y, solo si se desea, enviar el mensaje de prueba a un canal controlado por el usuario. La lectura de chat/eventos de Kick requiere implementar posteriormente el receptor público con validación de firma.
