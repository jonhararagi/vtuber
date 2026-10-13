using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private UIElement BuildSocialBotPage()
    {
        var root = new StackPanel();
        root.Children.Add(Label("Bot social · TTS local e IA conversacional", 22, "#F8FAFC", true));
        root.Children.Add(Label("El lector TTS funciona sin IA. Las respuestas automáticas empiezan desactivadas; la inferencia se limita a un servidor local.", 12, "#98A4BE"));

        var speechPanel = Panel("1. Leer chat con voz local (Windows SAPI)");
        var readEnabled = new CheckBox { Content = "Activar lectura de chat", IsChecked = store.GetSetting("social.tts.enabled", "false") == "true", Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 4, 0, 8) };
        var readAll = new CheckBox { Content = "Leer TODOS los mensajes (puede narrar contenido ofensivo o incómodo)", IsChecked = store.GetSetting("social.tts.readAll", "false") == "true", Foreground = Brush("#F0C674"), Margin = new Thickness(0, 4, 0, 8) };
        readAll.Checked += (_, _) => MessageBox.Show(this, "La lectura de todo el chat puede narrar mensajes ofensivos, enlaces o contenido incómodo. Los filtros no son moderación infalible. Guardá la configuración solo si aceptás ese riesgo.", "Advertencia de lectura de chat", MessageBoxButton.OK, MessageBoxImage.Warning);
        var ignoreCommands = new CheckBox { Content = "Ignorar comandos que empiezan con !", IsChecked = store.GetSetting("social.tts.ignoreCommands", "true") == "true", Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 3, 0, 6) };
        var ignoreLinks = new CheckBox { Content = "Ignorar mensajes con enlaces", IsChecked = store.GetSetting("social.tts.ignoreLinks", "true") == "true", Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 3, 0, 6) };
        var maxChars = TextField("Máximo de caracteres por mensaje", store.GetSetting("social.tts.maxChars", "220"));
        var interval = TextField("Intervalo mínimo entre frases (ms)", store.GetSetting("social.tts.intervalMs", "900"));
        var queueLimit = TextField("Máximo de frases de chat pendientes", store.GetSetting("social.tts.queueLimit", "30"));
        var ignoredUsers = TextField("Usuarios ignorados separados por coma", store.GetSetting("social.tts.ignoredUsers", ""));
        var speechTest = TextField("Frase para probar la voz local", "Hola, esta es una prueba de voz local.");
        speechPanel.Children.Add(Label("Prueba manual de voz", 12, "#98A4BE")); speechPanel.Children.Add(speechTest);
        speechPanel.Children.Add(ActionButton("Encolar frase de prueba", (_, _) =>
        {
            var rate = int.TryParse(store.GetSetting("audio.tts.rate", "0"), out var r) ? Math.Clamp(r, -10, 10) : 0;
            var volume = int.TryParse(store.GetSetting("audio.tts.volume", "100"), out var v) ? Math.Clamp(v, 0, 100) : 100;
            if (speechTest.Text.Length is > 0 and <= 500 &&
                speechQueue.Enqueue(speechTest.Text, SpeechPriority.Normal, store.GetSetting("audio.tts.voiceId"), rate, volume, store.GetSetting("audio.tts.outputId")))
                FooterStatus.Text = "Frase de prueba encolada; esto solo confirma la cola, no que el audio ya se haya oído.";
            else FooterStatus.Text = "No se pudo encolar la prueba: revisá la longitud y el estado de la cola.";
        }));
        speechPanel.Children.Add(readEnabled); speechPanel.Children.Add(readAll); speechPanel.Children.Add(ignoreCommands); speechPanel.Children.Add(ignoreLinks);
        speechPanel.Children.Add(Label("Máximo de caracteres (20–500)", 12, "#98A4BE")); speechPanel.Children.Add(maxChars);
        speechPanel.Children.Add(Label("Intervalo entre frases (0–10000 ms)", 12, "#98A4BE")); speechPanel.Children.Add(interval);
        speechPanel.Children.Add(Label("Capacidad de cola para chat (1–100)", 12, "#98A4BE")); speechPanel.Children.Add(queueLimit);
        speechPanel.Children.Add(Label("Excluir usuarios", 12, "#98A4BE")); speechPanel.Children.Add(ignoredUsers);
        speechPanel.Children.Add(ActionButton("Detener voz actual", (_, _) => { speechQueue.StopCurrentSpeech(); FooterStatus.Text = "Se solicitó detener la voz actual."; }));
        speechPanel.Children.Add(ActionButton("Vaciar cola de voz", (_, _) => { speechQueue.ClearPending(); FooterStatus.Text = "Cola de voz pendiente vaciada."; }));
        speechPanel.Children.Add(Label("La voz, velocidad, volumen y salida se eligen en «Voz y audio».", 11, "#98A4BE"));
        root.Children.Add(speechPanel);

        var aiPanel = Panel("2. Personalidad y proveedor local de IA");
        var botName = TextField("Nombre/personaje del bot", store.GetSetting("social.bot.name", "VTuberBot"));
        var botLogin = TextField("Login real de la cuenta bot (evita responder a su propio eco)", store.GetSetting("twitch.bot.login", store.GetSetting("twitch.channel")));
        var language = TextField("Idioma de respuesta", store.GetSetting("social.bot.language", "español"));
        var tone = TextField("Tono", store.GetSetting("social.bot.tone", "cercano"));
        var personality = new TextBox { Text = store.GetSetting("social.bot.personality", "Simpático, bromista y respetuoso."), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, MinWidth = 280, Margin = new Thickness(0, 0, 0, 8), ToolTip = "Describe la personalidad; no habilita código ni acciones." };
        var baseUrl = TextField("Servidor local compatible OpenAI", store.GetSetting("social.ai.baseUrl", "http://127.0.0.1:8080/v1"));
        var model = TextField("ID del modelo publicado por el servidor", store.GetSetting("social.ai.model", ""));
        var timeout = TextField("Timeout en segundos (1–120)", store.GetSetting("social.ai.timeoutSeconds", "30"));
        var maxInputChars = TextField("Máximo de caracteres de contexto (100–20000)", store.GetSetting("social.ai.maxInputChars", "4000"));
        var maxResponse = TextField("Máximo de caracteres por respuesta (40–500)", store.GetSetting("social.chat.maxResponseChars", "350"));
        var maxTokens = TextField("Máximo de tokens generados (16–1024)", store.GetSetting("social.ai.maxResponseTokens", "180"));
        var command = TextField("Comando para activar (ej. !bot)", store.GetSetting("social.chat.command", "!bot"));
        var globalCooldown = TextField("Cooldown global en segundos", store.GetSetting("social.chat.globalCooldownSeconds", "12"));
        var userCooldown = TextField("Cooldown por usuario en segundos", store.GetSetting("social.chat.userCooldownSeconds", "30"));
        var perMinute = TextField("Máximo de respuestas por minuto", store.GetSetting("social.chat.maxPerMinute", "4"));
        var autoReply = new CheckBox { Content = "Habilitar respuestas automáticas (por mención o comando)", IsChecked = store.GetSetting("social.chat.enabled", "false") == "true", Foreground = Brush("#F0C674"), Margin = new Thickness(0, 5, 0, 8) };
        var publish = new CheckBox { Content = "Publicar las respuestas generadas en el chat de Twitch", IsChecked = store.GetSetting("social.chat.publishToTwitch", "false") == "true", Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 3, 0, 8) };
        var speakReplies = new CheckBox { Content = "Encolar también las respuestas del bot en la voz local", IsChecked = store.GetSetting("social.chat.speakReplies", "false") == "true", Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 3, 0, 8) };
        aiPanel.Children.Add(Label("Nombre/personaje", 12, "#98A4BE")); aiPanel.Children.Add(botName);
        aiPanel.Children.Add(Label("Login real de Twitch de la cuenta bot", 12, "#98A4BE")); aiPanel.Children.Add(botLogin);
        aiPanel.Children.Add(Label("Idioma", 12, "#98A4BE")); aiPanel.Children.Add(language);
        aiPanel.Children.Add(Label("Tono", 12, "#98A4BE")); aiPanel.Children.Add(tone);
        aiPanel.Children.Add(Label("Personalidad", 12, "#98A4BE")); aiPanel.Children.Add(personality);
        aiPanel.Children.Add(Label("URL base local (solo localhost / loopback)", 12, "#98A4BE")); aiPanel.Children.Add(baseUrl);
        aiPanel.Children.Add(Label("Modelo", 12, "#98A4BE")); aiPanel.Children.Add(model);
        aiPanel.Children.Add(Label("Timeout (1–120 s)", 12, "#98A4BE")); aiPanel.Children.Add(timeout);
        aiPanel.Children.Add(Label("Máximo de caracteres de contexto (100–20000)", 12, "#98A4BE")); aiPanel.Children.Add(maxInputChars);
        aiPanel.Children.Add(Label("Máximo de respuesta (40–500 caracteres)", 12, "#98A4BE")); aiPanel.Children.Add(maxResponse);
        aiPanel.Children.Add(Label("Tokens máximos (16–1024)", 12, "#98A4BE")); aiPanel.Children.Add(maxTokens);
        aiPanel.Children.Add(Label("Comando que activa el bot", 12, "#98A4BE")); aiPanel.Children.Add(command);
        aiPanel.Children.Add(Label("Cooldown global (s)", 12, "#98A4BE")); aiPanel.Children.Add(globalCooldown);
        aiPanel.Children.Add(Label("Cooldown por usuario (s)", 12, "#98A4BE")); aiPanel.Children.Add(userCooldown);
        aiPanel.Children.Add(Label("Respuestas por minuto (1–20)", 12, "#98A4BE")); aiPanel.Children.Add(perMinute);
        aiPanel.Children.Add(autoReply); aiPanel.Children.Add(publish); aiPanel.Children.Add(speakReplies);
        aiPanel.Children.Add(ActionButton("Guardar configuración social", (_, _) =>
        {
            if (!int.TryParse(maxChars.Text, out var maxCharsValue) || maxCharsValue is < 20 or > 500 ||
                !int.TryParse(interval.Text, out var intervalValue) || intervalValue is < 0 or > 10000 ||
                !int.TryParse(queueLimit.Text, out var queueValue) || queueValue is < 1 or > 100 ||
                !int.TryParse(timeout.Text, out var timeoutValue) || timeoutValue is < 1 or > 120 ||
                !int.TryParse(maxInputChars.Text, out var inputCharsValue) || inputCharsValue is < 100 or > 20000 ||
                !int.TryParse(maxResponse.Text, out var responseValue) || responseValue is < 40 or > 500 ||
                !int.TryParse(maxTokens.Text, out var tokensValue) || tokensValue is < 16 or > 1024 ||
                !int.TryParse(globalCooldown.Text, out var globalValue) || globalValue is < 1 or > 300 ||
                !int.TryParse(userCooldown.Text, out var userValue) || userValue is < 1 or > 600 ||
                !int.TryParse(perMinute.Text, out var minuteValue) || minuteValue is < 1 or > 20 ||
                string.IsNullOrWhiteSpace(botName.Text) || botName.Text.Length > 40 || botLogin.Text.Length > 25 ||
                (botLogin.Text.Length > 0 && botLogin.Text.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-'))) ||
                string.IsNullOrWhiteSpace(language.Text) || language.Text.Length > 40 ||
                string.IsNullOrWhiteSpace(tone.Text) || tone.Text.Length > 80 || personality.Text.Length > 1000 ||
                command.Text.Trim().Length is < 2 or > 24 || !command.Text.Trim().StartsWith('!') || ignoredUsers.Text.Length > 2000)
            { MessageBox.Show(this, "Revisá los campos: hay valores fuera de rango o texto inválido.", "Configuración no guardada", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var wasEnabled = store.GetSetting("social.chat.enabled", "false") == "true";
            store.SetSetting("social.tts.enabled", readEnabled.IsChecked == true ? "true" : "false");
            store.SetSetting("social.tts.readAll", readAll.IsChecked == true ? "true" : "false");
            store.SetSetting("social.tts.ignoreCommands", ignoreCommands.IsChecked == true ? "true" : "false");
            store.SetSetting("social.tts.ignoreLinks", ignoreLinks.IsChecked == true ? "true" : "false");
            store.SetSetting("social.tts.maxChars", maxCharsValue.ToString()); store.SetSetting("social.tts.intervalMs", intervalValue.ToString());
            store.SetSetting("social.tts.queueLimit", queueValue.ToString()); store.SetSetting("social.tts.ignoredUsers", ignoredUsers.Text.Trim());
            store.SetSetting("social.bot.name", botName.Text.Trim()); store.SetSetting("twitch.bot.login", botLogin.Text.Trim()); store.SetSetting("social.bot.language", language.Text.Trim());
            store.SetSetting("social.bot.tone", tone.Text.Trim()); store.SetSetting("social.bot.personality", personality.Text.Trim());
            store.SetSetting("social.ai.baseUrl", baseUrl.Text.Trim()); store.SetSetting("social.ai.model", model.Text.Trim());
            store.SetSetting("social.ai.timeoutSeconds", timeoutValue.ToString()); store.SetSetting("social.ai.maxInputChars", inputCharsValue.ToString());
            store.SetSetting("social.ai.maxResponseTokens", tokensValue.ToString()); store.SetSetting("social.chat.maxResponseChars", responseValue.ToString());
            store.SetSetting("social.chat.command", command.Text.Trim()); store.SetSetting("social.chat.globalCooldownSeconds", globalValue.ToString());
            store.SetSetting("social.chat.userCooldownSeconds", userValue.ToString()); store.SetSetting("social.chat.maxPerMinute", minuteValue.ToString());
            store.SetSetting("social.chat.enabled", autoReply.IsChecked == true ? "true" : "false");
            store.SetSetting("social.chat.publishToTwitch", publish.IsChecked == true ? "true" : "false");
            store.SetSetting("social.chat.speakReplies", speakReplies.IsChecked == true ? "true" : "false");
            if (!wasEnabled && autoReply.IsChecked == true)
            {
                var confirm = MessageBox.Show(this, "Vas a habilitar respuestas automáticas generadas por IA local. Solo responderá a menciones o al comando configurado. ¿Querés habilitarlo?", "Confirmar automatización", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) store.SetSetting("social.chat.enabled", "false");
            }
            FooterStatus.Text = "Configuración del bot social guardada en SQLite."; store.Log("Bot social", "Preferencias de TTS y conversación guardadas; no se registró el contenido del chat.");
        }, true));
        root.Children.Add(aiPanel);

        var testPanel = Panel("3. Estado y prueba privada");
        var status = Label("IA local: sin comprobar", 12, "#98A4BE");
        var testPrompt = TextField("Escribí una pregunta de prueba (no se publicará)", "¡Hola! Presentate en una frase.");
        var testOutput = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, MinWidth = 280 };
        testPanel.Children.Add(status); testPanel.Children.Add(testPrompt); testPanel.Children.Add(testOutput);
        testPanel.Children.Add(ActionButton("Comprobar servidor y modelo", async (_, _) =>
        {
            try { var result = await socialBot.CheckProviderAsync(); status.Text = result.Success ? "IA local disponible" : "IA local: " + result.Status + " · " + result.Error; FooterStatus.Text = status.Text; }
            catch (Exception ex) { status.Text = "IA local: error · " + ex.Message; }
        }));
        testPanel.Children.Add(ActionButton("Generar respuesta de prueba (privada)", async (_, _) =>
        {
            try { status.Text = "Generando en el modelo local…"; var result = await socialBot.GenerateTestAsync(testPrompt.Text); testOutput.Text = result.Success ? result.Text : result.Status + ": " + result.Error; status.Text = result.Success ? "Prueba generada localmente; no publicada." : "IA local: " + result.Status; }
            catch (Exception ex) { status.Text = "Prueba fallida: " + ex.Message; }
        }, true));
        testPanel.Children.Add(ActionButton("Cancelar generación activa", (_, _) => socialBot.CancelGeneration()));
        root.Children.Add(testPanel);
        root.Children.Add(Label("Para IA conversacional: instalá/configurá por separado llama.cpp server y descargá un modelo GGUF compatible. VTuberBot no descarga modelos, no incluye pesos y no envía mensajes a servicios remotos. La comprobación HTTP usa /v1/models; el servidor debe implementar /v1/chat/completions.", 11, "#98A4BE"));
        return root;
    }
}
