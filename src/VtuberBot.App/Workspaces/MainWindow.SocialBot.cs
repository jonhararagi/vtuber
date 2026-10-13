using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private UIElement BuildSocialBotPage()
    {
        var root = new StackPanel();
        root.Children.Add(Label("ChatSpeak Studio · IA remota y voz de personaje", 22, "#F8FAFC", true));
        root.Children.Add(Label("Modo manual con ChatGPT web (copiar/pegar) y modo bot con proveedor remoto compatible. El bot nunca lee la sesión privada del navegador. Las respuestas automáticas empiezan desactivadas.", 12, "#98A4BE"));

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

        var aiPanel = Panel("2. Personalidad y proveedor de IA");
        var botName = TextField("Nombre/personaje del bot", store.GetSetting("social.bot.name", "VTuberBot"));
        var botLogin = TextField("Login real de la cuenta bot (evita responder a su propio eco)", store.GetSetting("twitch.bot.login", store.GetSetting("twitch.channel")));
        var language = TextField("Idioma de respuesta", store.GetSetting("social.bot.language", "español"));
        var tone = TextField("Tono", store.GetSetting("social.bot.tone", "cercano"));
        var personality = new TextBox { Text = store.GetSetting("social.bot.personality", "Simpático, bromista y respetuoso."), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, MinWidth = 280, Margin = new Thickness(0, 0, 0, 8), ToolTip = "Describe la personalidad; no habilita código ni acciones." };
        var aiProvider = new ComboBox
        {
            ItemsSource = new[] { "Remoto (API HTTPS)", "Local (solo localhost)" },
            SelectedIndex = store.GetSetting("social.ai.provider", "remote") == "local" ? 1 : 0,
            MinWidth = 280, Margin = new Thickness(0, 0, 0, 8)
        };
        var baseUrl = TextField("Servidor local compatible OpenAI", store.GetSetting("social.ai.baseUrl", "http://127.0.0.1:8080/v1"));
        var model = TextField("ID del modelo local", store.GetSetting("social.ai.model", ""));
        var remoteBaseUrl = TextField("URL base remota HTTPS", store.GetSetting("social.ai.remoteBaseUrl", "https://generativelanguage.googleapis.com/v1beta/openai/"));
        var remoteModel = TextField("ID del modelo remoto (gemini-3.8-flash por defecto)", store.GetSetting("social.ai.remoteModel", "gemini-3.8-flash"));
        var remoteAiApiKey = new PasswordBox { MinWidth = 280, Margin = new Thickness(0, 0, 0, 8), MaxLength = 8192 };
        var remoteAiKeyStatus = Label(secretStore.HasSecret("remote-ai-api-key") ? "Clave de IA guardada cifrada para este usuario de Windows." : "Clave de IA todavía no configurada.", 11, "#98A4BE");
        var timeout = TextField("Timeout en segundos (1–120)", store.GetSetting("social.ai.timeoutSeconds", "30"));
        var maxInputChars = TextField("Máximo de caracteres de contexto (100–20000)", store.GetSetting("social.ai.maxInputChars", "4000"));
        var maxResponse = TextField("Máximo de caracteres por respuesta (40–500)", store.GetSetting("social.chat.maxResponseChars", "350"));
        var maxTokens = TextField("Máximo de tokens generados (16–1024)", store.GetSetting("social.ai.maxResponseTokens", "180"));
        var command = TextField("Comando para activar (ej. !bot)", store.GetSetting("social.chat.command", "!bot"));
        var globalCooldown = TextField("Cooldown global en segundos", store.GetSetting("social.chat.globalCooldownSeconds", "12"));
        var userCooldown = TextField("Cooldown por usuario en segundos", store.GetSetting("social.chat.userCooldownSeconds", "30"));
        var perMinute = TextField("Máximo de respuestas por minuto", store.GetSetting("social.chat.maxPerMinute", "4"));
        var autoReply = new CheckBox { Content = "Habilitar respuestas automáticas (por mención o comando)", IsChecked = store.GetSetting("social.chat.enabled", "false") == "true", Foreground = Brush("#F0C674"), Margin = new Thickness(0, 5, 0, 8) };
        var periodicCommentary = new CheckBox { Content = "Comentar automáticamente el chat por intervalos (requiere modo bot)", IsChecked = store.GetSetting("social.chat.periodic.enabled", "false") == "true", Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 4, 0, 6) };
        var periodicIntervalMinutes = TextField("Intervalo de comentario del chat (minutos 1–60)", store.GetSetting("social.chat.periodicIntervalMinutes", "5"));
        var publish = new CheckBox { Content = "Publicar las respuestas generadas en el chat de Twitch", IsChecked = store.GetSetting("social.chat.publishToTwitch", "false") == "true", Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 3, 0, 8) };
        var speakReplies = new CheckBox { Content = "Encolar también las respuestas del bot en la voz local", IsChecked = store.GetSetting("social.chat.speakReplies", "true") == "true", Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 3, 0, 8) };
        aiPanel.Children.Add(Label("Proveedor de respuestas automáticas", 12, "#98A4BE")); aiPanel.Children.Add(aiProvider);
        aiPanel.Children.Add(Label("Nombre/personaje", 12, "#98A4BE")); aiPanel.Children.Add(botName);
        aiPanel.Children.Add(Label("Login real de Twitch de la cuenta bot", 12, "#98A4BE")); aiPanel.Children.Add(botLogin);
        aiPanel.Children.Add(Label("Idioma", 12, "#98A4BE")); aiPanel.Children.Add(language);
        aiPanel.Children.Add(Label("Tono", 12, "#98A4BE")); aiPanel.Children.Add(tone);
        aiPanel.Children.Add(Label("Personalidad", 12, "#98A4BE")); aiPanel.Children.Add(personality);
        aiPanel.Children.Add(Label("URL base local (solo localhost / loopback)", 12, "#98A4BE")); aiPanel.Children.Add(baseUrl);
        aiPanel.Children.Add(Label("Modelo local", 12, "#98A4BE")); aiPanel.Children.Add(model);
        aiPanel.Children.Add(Label("URL base remota (HTTPS)", 12, "#98A4BE")); aiPanel.Children.Add(remoteBaseUrl);
        aiPanel.Children.Add(Label("Modelo remoto (por ejemplo gpt-4.1-mini)", 12, "#98A4BE")); aiPanel.Children.Add(remoteModel);
        aiPanel.Children.Add(Label("Clave API remota (se cifra con DPAPI y no se guarda en SQLite)", 12, "#98A4BE")); aiPanel.Children.Add(remoteAiApiKey);
        aiPanel.Children.Add(remoteAiKeyStatus);
        aiPanel.Children.Add(Label("Timeout (1–120 s)", 12, "#98A4BE")); aiPanel.Children.Add(timeout);
        aiPanel.Children.Add(Label("Máximo de caracteres de contexto (100–20000)", 12, "#98A4BE")); aiPanel.Children.Add(maxInputChars);
        aiPanel.Children.Add(Label("Máximo de respuesta (40–500 caracteres)", 12, "#98A4BE")); aiPanel.Children.Add(maxResponse);
        aiPanel.Children.Add(Label("Tokens máximos (16–1024)", 12, "#98A4BE")); aiPanel.Children.Add(maxTokens);
        aiPanel.Children.Add(Label("Comando que activa el bot", 12, "#98A4BE")); aiPanel.Children.Add(command);
        aiPanel.Children.Add(Label("Cooldown global (s)", 12, "#98A4BE")); aiPanel.Children.Add(globalCooldown);
        aiPanel.Children.Add(Label("Cooldown por usuario (s)", 12, "#98A4BE")); aiPanel.Children.Add(userCooldown);
        aiPanel.Children.Add(Label("Respuestas por minuto (1–20)", 12, "#98A4BE")); aiPanel.Children.Add(perMinute);
        var speechProvider = new ComboBox
        {
            ItemsSource = new[] { "Remoto (voz de personaje)", "Local (voces de Windows)" },
            SelectedIndex = store.GetSetting("social.tts.provider", "remote") == "local" ? 1 : 0,
            MinWidth = 280, Margin = new Thickness(0, 0, 0, 8)
        };
        var remoteTtsBaseUrl = TextField("URL base TTS remoto (HTTPS)", store.GetSetting("social.tts.remoteBaseUrl", "https://generativelanguage.googleapis.com/v1beta"));
        var remoteTtsModel = TextField("Modelo TTS", store.GetSetting("social.tts.remoteModel", "gemini-3.8-flash-tts"));
        var remoteTtsVoice = TextField("ID de voz", store.GetSetting("social.tts.remoteVoice", "Leda"));
        var remoteTtsInstructions = new TextBox
        {
            Text = store.GetSetting("social.tts.remoteInstructions",
                "Habla en español con una voz femenina adulta joven, cálida, expresiva y de estilo anime original. Buena dicción, energía y emoción natural; no imites a un personaje o actriz específica."),
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 65, MinWidth = 280, Margin = new Thickness(0, 0, 0, 8)
        };
        var remoteTtsApiKey = new PasswordBox { MinWidth = 280, Margin = new Thickness(0, 0, 0, 8), MaxLength = 8192 };
        var remoteTtsKeyStatus = Label(secretStore.HasSecret("remote-tts-api-key") ? "Clave TTS guardada cifrada para este usuario de Windows." : "Clave TTS todavía no configurada.", 11, "#98A4BE");
        var voiceTestText = TextField("Frase de prueba (máximo 500 caracteres)", "¡Hola, chat! Soy tu VTuber, ¿nos divertimos hoy?");
        var voicePanel = Panel("Voz de personaje · femenina, original y estilo anime");
        voicePanel.Children.Add(Label("Proveedor de voz", 12, "#98A4BE")); voicePanel.Children.Add(speechProvider);
        voicePanel.Children.Add(Label("URL base TTS remoto", 12, "#98A4BE")); voicePanel.Children.Add(remoteTtsBaseUrl);
        voicePanel.Children.Add(Label("Modelo de voz (Gemini 3.8 Flash TTS por defecto)", 12, "#98A4BE")); voicePanel.Children.Add(remoteTtsModel);
        voicePanel.Children.Add(Label("Voz (por ejemplo Leda, estilo juvenil)", 12, "#98A4BE")); voicePanel.Children.Add(remoteTtsVoice);
        voicePanel.Children.Add(Label("Instrucciones de voz", 12, "#98A4BE")); voicePanel.Children.Add(remoteTtsInstructions);
        voicePanel.Children.Add(Label("Clave API TTS (se cifra con DPAPI y no se guarda en SQLite)", 12, "#98A4BE")); voicePanel.Children.Add(remoteTtsApiKey);
        voicePanel.Children.Add(remoteTtsKeyStatus);
        voicePanel.Children.Add(Label("La voz Gemini puede reutilizar la clave de IA de Google. El nivel gratuito tiene límites; Google indica que su contenido se usa para mejorar sus productos.", 11, "#98A4BE"));
        voicePanel.Children.Add(Label("Probar la voz", 12, "#98A4BE")); voicePanel.Children.Add(voiceTestText);
        voicePanel.Children.Add(ActionButton("Probar voz femenina", (_, _) =>
        {
            var rate = int.TryParse(store.GetSetting("audio.tts.rate", "0"), out var r) ? Math.Clamp(r, -10, 10) : 0;
            var volume = int.TryParse(store.GetSetting("audio.tts.volume", "100"), out var v) ? Math.Clamp(v, 0, 100) : 100;
            if (voiceTestText.Text.Length is > 0 and <= 500 &&
                speechQueue.Enqueue(voiceTestText.Text, SpeechPriority.High, store.GetSetting("audio.tts.voiceId"), rate, volume, store.GetSetting("audio.tts.outputId")))
                FooterStatus.Text = "Prueba de voz encolada; revisá el resultado en el estado de voz.";
            else FooterStatus.Text = "No se pudo encolar la prueba de voz.";
        }, true));

        aiPanel.Children.Add(autoReply); aiPanel.Children.Add(periodicCommentary);
        aiPanel.Children.Add(Label("Frecuencia del comentario periódico", 12, "#98A4BE")); aiPanel.Children.Add(periodicIntervalMinutes);
        aiPanel.Children.Add(publish); aiPanel.Children.Add(speakReplies);
        aiPanel.Children.Add(ActionButton("Guardar configuración ChatSpeak", (_, _) =>
        {
            if (!int.TryParse(maxChars.Text, out var maxCharsValue) || maxCharsValue is < 20 or > 500 ||
                remoteAiApiKey.Password.Length > 8192 || remoteTtsApiKey.Password.Length > 8192 ||
                !int.TryParse(periodicIntervalMinutes.Text, out var periodicMinutesValue) || periodicMinutesValue is < 1 or > 60 ||
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
            store.SetSetting("social.ai.provider", aiProvider.SelectedIndex == 1 ? "local" : "remote");
            store.SetSetting("social.ai.baseUrl", baseUrl.Text.Trim()); store.SetSetting("social.ai.model", model.Text.Trim());
            store.SetSetting("social.ai.remoteBaseUrl", remoteBaseUrl.Text.Trim());
            store.SetSetting("social.ai.remoteModel", remoteModel.Text.Trim());
            if (!string.IsNullOrWhiteSpace(remoteAiApiKey.Password))
            {
                secretStore.Save("remote-ai-api-key", remoteAiApiKey.Password);
                remoteAiApiKey.Clear();
                remoteAiKeyStatus.Text = "Clave de IA guardada cifrada para este usuario de Windows.";
            }
            store.SetSetting("social.tts.provider", speechProvider.SelectedIndex == 1 ? "local" : "remote");
            store.SetSetting("social.tts.remoteBaseUrl", remoteTtsBaseUrl.Text.Trim());
            store.SetSetting("social.tts.remoteModel", remoteTtsModel.Text.Trim());
            store.SetSetting("social.tts.remoteVoice", remoteTtsVoice.Text.Trim());
            store.SetSetting("social.tts.remoteInstructions", remoteTtsInstructions.Text.Trim());
            if (!string.IsNullOrWhiteSpace(remoteTtsApiKey.Password))
            {
                secretStore.Save("remote-tts-api-key", remoteTtsApiKey.Password);
                remoteTtsApiKey.Clear();
                remoteTtsKeyStatus.Text = "Clave TTS guardada cifrada para este usuario de Windows.";
            }
            store.SetSetting("social.ai.timeoutSeconds", timeoutValue.ToString()); store.SetSetting("social.ai.maxInputChars", inputCharsValue.ToString());
            store.SetSetting("social.ai.maxResponseTokens", tokensValue.ToString()); store.SetSetting("social.chat.maxResponseChars", responseValue.ToString());
            store.SetSetting("social.chat.command", command.Text.Trim()); store.SetSetting("social.chat.globalCooldownSeconds", globalValue.ToString());
            store.SetSetting("social.chat.userCooldownSeconds", userValue.ToString()); store.SetSetting("social.chat.maxPerMinute", minuteValue.ToString());
            store.SetSetting("social.chat.enabled", autoReply.IsChecked == true ? "true" : "false");
            store.SetSetting("social.chat.publishToTwitch", publish.IsChecked == true ? "true" : "false");
            store.SetSetting("social.chat.speakReplies", speakReplies.IsChecked == true ? "true" : "false");
            socialBot.SetPeriodicCommentaryEnabled(periodicCommentary.IsChecked == true);
            store.SetSetting("social.chat.periodicIntervalMinutes", periodicMinutesValue.ToString());
            if (!wasEnabled && autoReply.IsChecked == true)
            {
                var confirm = MessageBox.Show(this, "Vas a habilitar respuestas automáticas por el proveedor seleccionado. Solo responderá a menciones o al comando configurado. ¿Querés habilitarlo?", "Confirmar automatización", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                {
                    autoReply.IsChecked = false;
                    store.SetSetting("social.chat.enabled", "false");
                }
            }
            socialBot.SetAutomaticRepliesEnabled(store.GetSetting("social.chat.enabled", "false") == "true");
            FooterStatus.Text = "Configuración de ChatSpeak guardada. Las claves quedan fuera de SQLite."; store.Log("ChatSpeak", "Preferencias guardadas; no se registró el contenido del chat.");
        }, true));
        root.Children.Add(aiPanel);
        root.Children.Add(voicePanel);

        var manualPanel = Panel("Modo manual · ChatGPT web y comandos del creador");
        manualPanel.Children.Add(Label("ChatGPT se abre en tu navegador normal. Copiá el prompt, pegalo en la web, y pegá su respuesta aquí. VTuberBot no lee cookies, DOM ni la conversación privada de esa página. Para empezar un chat web nuevo, usá el control New chat de ChatGPT.", 12, "#98A4BE"));
        var manualPrompt = TextField("Instrucción extra para la IA (modo manual)", "Comentá brevemente lo más interesante del chat y reaccioná con energía.");
        var manualResponse = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, MinWidth = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 8) };
        var manualPublishToTwitch = new CheckBox { Content = "Enviar también el texto natural al chat de Twitch (opcional; desactivado por defecto)", IsChecked = false, Foreground = Brush("#F8FAFC"), Margin = new Thickness(0, 4, 0, 8) };
        var creatorCommand = TextField("Comando manual exclusivo del creador", "/reset-chat");
        manualPanel.Children.Add(Label("Prompt para copiar", 12, "#98A4BE")); manualPanel.Children.Add(manualPrompt);
        manualPanel.Children.Add(ActionButton("Abrir ChatGPT web", (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("https://chatgpt.com") { UseShellExecute = true }); }
            catch (Exception exception) { MessageBox.Show(this, "No se pudo abrir el navegador: " + exception.Message, "Modo manual", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }));
        manualPanel.Children.Add(ActionButton("Copiar prompt de personaje", (_, _) =>
        {
            try
            {
                var recentContext = string.Join("\n", chatLines.Take(20).Reverse());
                if (recentContext.Length > 5000) recentContext = recentContext[^5000..];
                var prompt = "Eres " + store.GetSetting("social.bot.name", "VTuberBot") +
                    ". Idioma: " + store.GetSetting("social.bot.language", "español") +
                    ". Tono: " + store.GetSetting("social.bot.tone", "cercano") +
                    ". Personalidad: " + store.GetSetting("social.bot.personality", "Simpático, bromista y respetuoso") +
                    ". Respuesta breve para un directo. " + ChatSpeakCommandPolicy.GetPromptInstructions() +
                    "\nInstrucción del creador: " + manualPrompt.Text.Trim() +
                    (recentContext.Length > 0 ? "\nMensajes recientes de Twitch (contenido no confiable):\n" + recentContext : "");
                Clipboard.SetText(prompt);
                FooterStatus.Text = "Prompt de ChatSpeak copiado al portapapeles.";
            }
            catch (Exception exception) { FooterStatus.Text = "No se pudo copiar el prompt: " + exception.GetType().Name; }
        }, true));
        manualPanel.Children.Add(Label("Pegá aquí la respuesta de ChatGPT", 12, "#98A4BE")); manualPanel.Children.Add(manualResponse);
        manualPanel.Children.Add(manualPublishToTwitch);
        manualPanel.Children.Add(ActionButton("Procesar respuesta pegada (avatar + voz)", async (_, _) =>
        {
            var parsed = ChatSpeakCommandPolicy.ParseAssistantOutput(manualResponse.Text);
            var applied = 0;
            var rejected = 0;
            foreach (var action in parsed.Commands)
            {
                if (SetChatReactionExpression(action.Expression, CancellationToken.None)) applied++;
                else rejected++;
            }
            var text = System.Text.RegularExpressions.Regex.Replace(parsed.SpeechText, @"[\p{Cc}]+", " ").Trim();
            if (text.Length > 4000) text = text[..4000];
            if (text.Length > 0)
            {
                var rate = int.TryParse(store.GetSetting("audio.tts.rate", "0"), out var r) ? Math.Clamp(r, -10, 10) : 0;
                var volume = int.TryParse(store.GetSetting("audio.tts.volume", "100"), out var v) ? Math.Clamp(v, 0, 100) : 100;
                speechQueue.Enqueue(text, SpeechPriority.Normal, store.GetSetting("audio.tts.voiceId"), rate, volume, store.GetSetting("audio.tts.outputId"));
                if (manualPublishToTwitch.IsChecked == true)
                {
                    if (twitch.IsConnected)
                    {
                        try { await twitch.SendMessageAsync(store.GetSetting("twitch.channel"), text[..Math.Min(text.Length, 450)]); }
                        catch (Exception exception) { FooterStatus.Text = "Se procesó la respuesta, pero no se publicó en Twitch (" + exception.GetType().Name + ")."; }
                    }
                    else FooterStatus.Text = "Twitch está desconectado; el texto no se publicó.";
                }
            }
            FooterStatus.Text = $"Modo manual procesado: {applied} comandos visuales aplicados, {rejected} sin sprite/overlay.";
        }, true));
        manualPanel.Children.Add(Label("Comandos del creador: /reset-chat (vacía contexto y cancela generación), /restart-chat (mismo reinicio local), /switch-chat (abre ChatGPT web), /bot-on y /bot-off. Estos comandos nunca se ejecutan desde texto de IA.", 11, "#98A4BE"));
        manualPanel.Children.Add(creatorCommand);
        manualPanel.Children.Add(ActionButton("Ejecutar comando del creador", (_, _) =>
        {
            var commandText = creatorCommand.Text.Trim();
            var commandName = commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() ?? "";
            switch (commandName)
            {
                case "/reset-chat":
                case "/restart-chat":
                    socialBot.ResetConversation();
                    FooterStatus.Text = "Contexto de IA y generación pendiente reiniciados.";
                    break;
                case "/switch-chat":
                    try { Process.Start(new ProcessStartInfo("https://chatgpt.com") { UseShellExecute = true }); FooterStatus.Text = "ChatGPT web abierto; usa New chat en la propia web si querés otro contexto."; }
                    catch (Exception exception) { FooterStatus.Text = "No se pudo abrir ChatGPT web: " + exception.GetType().Name; }
                    break;
                case "/bot-off":
                    socialBot.SetAutomaticRepliesEnabled(false);
                    autoReply.IsChecked = false;
                    FooterStatus.Text = "Modo bot desactivado manualmente.";
                    break;
                case "/bot-on":
                    if (aiProvider.SelectedIndex == 0 && !secretStore.HasSecret("remote-ai-api-key"))
                    {
                        MessageBox.Show(this, "Guardá primero una clave API de IA remota en ChatSpeak Studio.", "No se puede activar el modo bot", MessageBoxButton.OK, MessageBoxImage.Warning);
                        break;
                    }
                    if (MessageBox.Show(this, "¿Activar el modo bot automático? Solo procesará menciones/comandos configurados; revisá los límites del proveedor.", "Confirmar modo bot", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        socialBot.SetAutomaticRepliesEnabled(true);
                        autoReply.IsChecked = true;
                        FooterStatus.Text = "Modo bot automático activado.";
                    }
                    break;
                default:
                    if (ChatSpeakCommandPolicy.TryParseAvatarCommand(commandText, out var action))
                    {
                        var success = SetChatReactionExpression(action.Expression, CancellationToken.None);
                        FooterStatus.Text = success ? "Comando manual aplicado: " + action.Command : "No se aplicó: falta el sprite correspondiente o el overlay está oculto.";
                    }
                    else FooterStatus.Text = "Comando manual no reconocido. No se ejecutó ninguna acción.";
                    break;
            }
        }));
        root.Children.Add(manualPanel);

        var testPanel = Panel("4. Estado y prueba privada");
        var status = Label("Proveedor local: sin comprobar · respuestas automáticas según configuración", 12, "#98A4BE");
        socialBot.StatusChanged += snapshot => Dispatcher.BeginInvoke(() =>
        {
            status.Text = snapshot.State + ": " + snapshot.Detail;
        });
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
        testPanel.Children.Add(ActionButton("Cancelar generación activa", (_, _) =>
        {
            socialBot.CancelGeneration();
            status.Text = "Cancelación solicitada; no se confirmará una publicación nueva.";
        }));
        root.Children.Add(testPanel);
        root.Children.Add(Label("El proveedor local solo acepta localhost. El proveedor remoto y la voz TTS requieren URLs HTTPS compatibles, una clave API guardada y cuota del proveedor. La suscripción web de ChatGPT no incluye automáticamente el uso de su API. Las respuestas automáticas siguen desactivadas hasta que las habilites.", 11, "#98A4BE"));
        return root;
    }
}
