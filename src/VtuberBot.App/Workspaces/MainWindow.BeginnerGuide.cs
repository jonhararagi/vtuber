using System.Windows;
using System.Windows.Controls;

namespace VtuberBot.App;

public partial class MainWindow
{
    private UIElement BuildBeginnerGuidePage()
    {
        var root = new StackPanel();
        root.Children.Add(Label("Guía fácil para empezar", 22, "#F8FAFC", true));
        root.Children.Add(Label(
            "No necesitás saber programar. Esta guía explica qué hace cada módulo, en qué orden conviene configurarlo y cuáles son los límites reales de las conexiones. Elegí una pestaña arriba; los botones te llevan directamente al panel correspondiente.",
            13, "#C4CDE0"));

        var start = Panel("Primero, tres ideas importantes");
        start.Children.Add(Label("1. VTuberBot es tu centro de control: configura tu avatar, audio y comportamiento del bot.", 12, "#C4CDE0"));
        start.Children.Add(Label("2. OBS sigue siendo el programa que captura, compone y transmite el vídeo.", 12, "#C4CDE0"));
        start.Children.Add(Label("3. Twitch y Kick siguen controlando sus cuentas, permisos y funciones exclusivas. Una sesión autorizada no significa que todas las funciones de una plataforma estén implementadas.", 12, "#C4CDE0"));
        start.Children.Add(ActionButton("Seguir el orden recomendado", (_, _) => Navigate("PNGTuber 2D"), true));
        root.Children.Add(start);

        var tabs = new TabControl { MinHeight = 630, Margin = new Thickness(0, 0, 0, 12) };
        tabs.Items.Add(new TabItem
        {
            Header = "Empezar",
            Content = BuildBeginnerGuideTab(
                "Configurá una cosa por vez y probá antes de pasar a la siguiente.",
                new[]
                {
                    ("1. Avatar", "En PNGTuber 2D importá un PNG propio y comprobá que se vea bien. Las imágenes de reposo y hablando son dos estados visuales, no seguimiento facial avanzado."),
                    ("2. Voz y audio", "Elegí el micrófono y el dispositivo de salida. Si usás VOICEVOX, debe estar instalado aparte: no viene incluido en el ZIP de VTuberBot."),
                    ("3. OBS", "Conectá OBS desde su panel y verificá que el estado cambie solo después de una conexión real."),
                    ("4. Bots", "Autoriza primero una sola plataforma. Probá el chat con una cuenta/canal que controles antes de habilitar automatizaciones.")
                },
                "PNGTuber 2D", "Abrir PNGTuber 2D")
        });
        tabs.Items.Add(new TabItem
        {
            Header = "Avatar 2D",
            Content = BuildBeginnerGuideTab(
                "Esta parte controla la imagen que aparecerá durante el stream.",
                new[]
                {
                    ("PNG principal", "Es la ilustración base. Para moverla y colocarla encima de OBS, usá un PNG con transparencia cuando sea posible."),
                    ("Reposo y hablando", "Idle es la imagen en reposo y Talking la imagen mientras VTuberBot detecta voz o reproduce habla. La alternancia no crea fonemas ni sincroniza la boca sílaba por sílaba."),
                    ("Stickers de boca", "Podés asignar tus PNG transparentes de boca cerrada/abierta y ajustar tamaño y posición. Si la ilustración base ya trae una boca dibujada, esa boca puede seguir viéndose debajo: no hay un borrador de boca integrado; prepará una copia sin boca en un editor de imagen."),
                    ("Movimiento", "Sacudida, rebote y flotación son efectos visuales globales del avatar. Reducí la intensidad o desactivá el movimiento si distrae.")
                },
                "PNGTuber 2D", "Abrir editor 2D")
        });
        tabs.Items.Add(new TabItem
        {
            Header = "Voz y audio",
            Content = BuildBeginnerGuideTab(
                "Acá configurás cómo te escucha el programa y cómo reproduce voz.",
                new[]
                {
                    ("Micrófono", "Elegí el dispositivo correcto y hablá para revisar que la detección de voz responda. Si el medidor no reacciona, revisá los permisos de micrófono de Windows y que otra aplicación no lo esté bloqueando."),
                    ("VOICEVOX", "El motor se instala por separado y normalmente escucha solo en tu propio equipo. Iniciá el Engine desde VTuberBot, consultá la lista real de voces y probá antes de usarlo en directo."),
                    ("Idioma y licencias", "Las voces estándar de VOICEVOX son principalmente japonesas; no se garantiza español natural. Consultá y cumplí la licencia del Engine y de cada voicebank que instales."),
                    ("No se oye nada", "Confirmá el dispositivo de salida seleccionado, el volumen de Windows y el de VTuberBot. Consultá Actividad y diagnóstico para diferenciar error de conexión, motor apagado y fallo de reproducción.")
                },
                "Voz y audio", "Abrir Voz y audio")
        });
        tabs.Items.Add(new TabItem
        {
            Header = "Bots y plataformas",
            Content = BuildBeginnerGuideTab(
                "Un bot es el conjunto de reglas y conexiones que lee el chat y puede responder. No es lo mismo que OBS.",
                new[]
                {
                    ("Twitch", "VTuberBot ya dispone de conexión de chat y EventSub. Autorizá la cuenta correcta, elegí el canal destino y comprobá los permisos que Twitch solicite. El login de la cuenta bot y el nombre del canal destino son campos diferentes."),
                    ("Kick", "En esta versión podés autorizar tu propia app, verificar un canal y enviar manualmente un mensaje de prueba. La recepción automática del chat/eventos de Kick todavía no está conectada al sistema de comandos o TTS."),
                    ("Reglas y voz", "Las respuestas y reacciones actuales se alimentan del chat de Twitch. No interpretes una regla guardada como prueba de que Kick ya la ejecuta."),
                    ("Streamer.bot", "Es opcional. Puede servir como puente para acciones que ya tenés ahí, pero evitá definir la misma respuesta en Streamer.bot y VTuberBot a la vez para que el chat no reciba duplicados."),
                    ("Estado real", "Token guardado significa que hay credenciales locales. Solo una consulta real a la API o una conexión real permite confirmar la función correspondiente.")
                },
                "Bots y plataformas", "Abrir panel de bots")
        });
        tabs.Items.Add(new TabItem
        {
            Header = "OBS y transmisiones",
            Content = BuildBeginnerGuideTab(
                "OBS es quien finalmente captura la pantalla, mezcla audio/vídeo y emite el directo.",
                new[]
                {
                    ("Conectar", "En OBS Studio habilitá WebSocket y una contraseña. VTuberBot suele conectar al equipo local en el puerto 4455; el host, puerto y contraseña tienen que coincidir."),
                    ("Seguridad", "No compartas la contraseña de WebSocket ni expongas el puerto de OBS a Internet. La guía oficial recomienda proteger el control remoto con contraseña."),
                    ("Qué puede controlar", "VTuberBot puede ejecutar las operaciones disponibles en el panel, por ejemplo consultar escenas, cambiar visibilidad de fuentes o controlar estados compatibles. No reemplaza el motor de OBS ni garantiza que el encoder funcione en tu PC."),
                    ("Antes de transmitir", "Hacé una grabación local corta y revisá la imagen, el audio y el log. La confirmación de iniciar directo es importante porque la acción es real.")
                },
                "OBS", "Abrir panel OBS")
        });
        tabs.Items.Add(new TabItem
        {
            Header = "Comandos",
            Content = BuildBeginnerGuideTab(
                "La palabra comando se usa para dos cosas distintas en VTuberBot.",
                new[]
                {
                    ("Comandos locales", "La pantalla Comandos permite administrar/probar acciones locales de VTuberBot. Que un comando funcione ahí no quiere decir que esté escuchando el chat de Twitch."),
                    ("Comandos del chat", "Las reglas que reaccionan a mensajes de Twitch se gestionan en Bots y plataformas, dentro de Reglas y voz. Su funcionamiento depende de que el chat esté realmente conectado."),
                    ("Prueba segura", "Probá con un mensaje simple y una cuenta/canal controlado por vos. Empezá con una única respuesta y evitá que un bot externo repita lo mismo."),
                    ("Funciones de plataforma", "Recompensas, puntos de canal y otras opciones exclusivas pueden requerir permisos o APIs específicas. La guía no marca una función como disponible solo porque aparezca un nombre en pantalla.")
                },
                "Bots y plataformas", "Abrir comandos del chat")
        });
        tabs.Items.Add(new TabItem
        {
            Header = "Cuentas y privacidad",
            Content = BuildBeginnerGuideTab(
                "VTuberBot mantiene la configuración en tu PC, pero las conexiones de plataformas siguen necesitando Internet.",
                new[]
                {
                    ("Contraseñas y tokens", "Los secretos admitidos por la aplicación se protegen con DPAPI para el usuario actual de Windows. No copies tokens, contraseñas, Client Secret ni claves de streaming a GitHub, capturas, logs o mensajes de soporte."),
                    ("Permisos mínimos", "Aceptá solo los permisos que entendés y que la función necesita. Para quitar una sesión, distinguí entre borrar el token local y revocar realmente el acceso en la web de Twitch/Kick."),
                    ("IA y costes", "Las funciones locales básicas no necesitan una API de IA paga. Los proveedores remotos, cuando estén configurados, son opcionales y pueden tener sus propios costes y condiciones."),
                    ("Archivos", "La aplicación guarda proyectos y configuraciones en su directorio de datos local. Al compartir diagnóstico, usá la exportación diseñada para omitir secretos y revisá el contenido antes de enviarlo.")
                },
                "Ajustes", "Abrir Ajustes")
        });
        tabs.Items.Add(new TabItem
        {
            Header = "Problemas frecuentes",
            Content = BuildBeginnerGuideTab(
                "Probá estas comprobaciones antes de reinstalar. Si el problema persiste, exportá el diagnóstico sin compartir secretos.",
                new[]
                {
                    ("Twitch no conecta", "Comprobá Internet, el login real de la cuenta bot, el canal destino, la autorización y los scopes. Desconectá y volvé a autorizar si el token perdió permisos. No pegues el token en un mensaje."),
                    ("Kick muestra error OAuth o HTTP 400", "Comprobá que el Client ID y Client Secret sean de la misma app y que la Redirect URL registrada coincida literalmente con la que muestra VTuberBot: http://localhost:17564/callback/. Hay reportes públicos de Kick con respuestas 400 vacías incluso tras revisar PKCE y redirect_uri; si persiste, guardá la hora/código HTTP y revisá el estado del servicio, sin asumir que la contraseña es incorrecta."),
                    ("Kick no recibe comandos", "Es una limitación conocida de esta versión, no una opción escondida. Kick necesita un webhook accesible desde Internet con verificación de firma para los eventos; la recepción y el enrutamiento todavía no están implementados."),
                    ("OBS no conecta", "Confirmá que OBS esté abierto, que WebSocket esté habilitado, que el puerto sea el mismo (normalmente 4455) y que la contraseña coincida. OBS 28 o posterior incorpora obs-websocket; no expongas el puerto a redes no confiables."),
                    ("VOICEVOX no aparece", "Abrí/instalá VOICEVOX Engine por separado, iniciá el motor local, verificá su URL y pulsá actualizar voces. El paquete de VTuberBot no incluye Engine ni voicebanks."),
                    ("No sabés por dónde empezar", "Seguí este orden: PNGTuber 2D → Voz y audio → OBS → Bots y plataformas. Abrí Actividad y diagnóstico si un botón informa que algo falló.")
                },
                null, null)
        });

        root.Children.Add(tabs);
        return root;
    }

    private UIElement BuildBotsQuickGuide()
    {
        return BuildBeginnerGuideTab(
            "Esta pestaña explica qué hace cada una de las pestañas del panel de bots y qué podés esperar de ellas hoy.",
            new[]
            {
                ("Twitch", "Conecta el chat de Twitch y EventSub usando la autorización de tu cuenta. Es la ruta actual para reglas y reacciones basadas en mensajes."),
                ("Kick", "Autoriza tu aplicación Kick, consulta los datos del canal y permite un envío de prueba que requiere confirmación. No recibe automáticamente mensajes/eventos todavía."),
                ("Reglas y voz", "Configura respuestas, reacciones y voz asociadas al chat que está conectado actualmente; en esta versión corresponde al flujo de Twitch."),
                ("Streamer.bot (opcional)", "Permite usar la integración con Streamer.bot cuando ya la tenés instalada. No hace falta para el flujo local básico y conviene no duplicar reglas."),
                ("Qué significa una conexión", "Un token guardado no es lo mismo que una conexión de chat. Kick se considera verificado después de consultar su API; la recepción de eventos requiere una implementación aparte.")
            },
            null, null);
    }

    private UIElement BuildBeginnerGuideTab(
        string intro,
        (string Heading, string Detail)[] sections,
        string? targetPage,
        string? targetLabel)
    {
        var content = new StackPanel { Margin = new Thickness(12) };
        content.Children.Add(Label(intro, 13, "#C4CDE0"));

        foreach (var section in sections)
        {
            var card = Panel(section.Heading);
            card.Children.Add(Label(section.Detail, 12, "#C4CDE0"));
            content.Children.Add(card);
        }

        if (!string.IsNullOrWhiteSpace(targetPage))
        {
            var actions = new WrapPanel { Margin = new Thickness(0, 4, 0, 12) };
            actions.Children.Add(ActionButton(targetLabel ?? ("Abrir " + targetPage), (_, _) => Navigate(targetPage), true));
            content.Children.Add(actions);
        }

        return new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }
}
