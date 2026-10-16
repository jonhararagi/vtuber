using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VtuberBot.App;

public partial class MainWindow
{
    private UIElement BuildProjectFinishChecklistPanel()
    {
        var card = new Border
        {
            Margin = new Thickness(0, 18, 0, 20),
            Padding = new Thickness(2),
            Background = new SolidColorBrush(Color.FromRgb(22, 31, 51)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(55, 75, 105)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };
        var body = new StackPanel { Margin = new Thickness(16) };
        body.Children.Add(new TextBlock
        {
            Text = "Plan para terminar VTuberBot",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });
        body.Children.Add(new TextBlock
        {
            Text = "Elegí qué querés que el equipo priorice y tocá Enviar. Antes de implementar, el agente debe volver a revisar GitHub y la CI actual: estas opciones son una lista de cierre, no una afirmación de que todo siga pendiente.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 192, 215)),
            Margin = new Thickness(0, 0, 0, 12)
        });
        body.Children.Add(new TextBlock
        {
            Text = "Objetivo general",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 5)
        });
        var scope = new ComboBox
        {
            ItemsSource = new[]
            {
                "Primera versión utilizable (recomendado)",
                "Incluir también funciones avanzadas",
                "Solo auditar y devolver un plan; no implementar"
            },
            SelectedIndex = 0,
            MinHeight = 32,
            Margin = new Thickness(0, 0, 0, 12)
        };
        body.Children.Add(scope);

        var tasks = new List<(string Key, string Title, CheckBox Check)>();
        void AddTask(string key, string title, string detail, bool isChecked)
        {
            var group = new StackPanel { Margin = new Thickness(0, 3, 0, 8) };
            var check = new CheckBox
            {
                Content = title,
                IsChecked = isChecked,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 2, 0, 2)
            };
            group.Children.Add(check);
            group.Children.Add(new TextBlock
            {
                Text = detail,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(157, 172, 198)),
                Margin = new Thickness(24, 0, 0, 0),
                FontSize = 11
            });
            body.Children.Add(group);
            tasks.Add((key, title, check));
        }

        body.Children.Add(new TextBlock
        {
            Text = "Pasos posibles de cierre",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        });
        AddTask("AUDIT", "1. Auditoría funcional completa con evidencia", "Revisar módulos, documentación, errores conocidos y funciones reales. No dar por terminado un botón solo porque exista o compile.", true);
        AddTask("WINDOWS", "2. Validación final en Windows", "Abrir el paquete limpio, iniciar y cerrar la app, recorrer paneles y confirmar errores visibles con el equipo real.", true);
        AddTask("AVATAR", "3. Terminar y probar el PNGTuber", "Preparar fondo y zona de boca, alinear boca abierta/cerrada, comprobar reacciones y guardar los cambios del perfil.", true);
        AddTask("OBS", "4. Operar OBS desde VTuberBot", "Verificar inicio minimizado cuando sea viable, conexión WebSocket auténtica, estado real, reconexión y acciones desde el panel.", true);
        AddTask("STREAMERBOT", "5. Cerrar la integración con Streamer.bot", "Comprobar autenticación/desafío, consulta de acciones, ejecución, eventos, desconexión y que secretos nunca aparezcan en logs.", true);
        AddTask("TWITCH", "6. Probar Twitch de extremo a extremo", "Verificar perfiles/OAuth, chat IRC, EventSub, comandos, puntos, sorteos y moderación con estados y errores reales.", true);
        AddTask("VOICE", "7. Probar micrófono, voz local y animación", "Verificar entrada y salida de audio, cambio Idle/Talking, cola de voz y opciones locales sin exigir una API paga.", true);
        AddTask("STABILITY", "8. Estabilidad, persistencia y rendimiento", "Comprobar SQLite, recuperación tras reinicio, colas/cancelación, uso de memoria y carga razonable en Ryzen 5 5600G con 16 GB.", true);
        AddTask("RELEASE", "9. Paquete final y guía de instalación", "Revisar portable/instalación, dependencias, datos de usuario, recuperación de errores y pasos de primer uso para una persona principiante.", true);
        AddTask("SECURITY", "10. Revisión final de seguridad", "Auditar tokens, almacenamiento de secretos, validación de entradas, registro de datos sensibles y manejo de fallos de red.", true);
        AddTask("ADVANCED", "11. Funciones avanzadas del avatar (opcional)", "Lip-sync por visemas/fonemas, apertura proporcional al audio, tracking facial y rig 2.5D real. Separarlas del lanzamiento básico si necesitan otro alcance.", false);
        AddTask("DOCS", "12. Documentación y reporte de cierre", "Actualizar la continuidad central, guía de usuario y roadmap; enlazar CI del SHA exacto, artefacto descargable, límites y comprobaciones manuales pendientes.", true);

        var status = new TextBlock
        {
            Text = "Todavía no se envió ninguna solicitud.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 192, 215)),
            Margin = new Thickness(0, 10, 0, 8)
        };
        var send = new Button
        {
            Content = "Enviar",
            MinHeight = 40,
            MinWidth = 140,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(18, 8, 18, 8),
            FontWeight = FontWeights.SemiBold
        };
        send.Click += (_, _) =>
        {
            var chosen = tasks.Where(item => item.Check.IsChecked == true).ToList();
            if (chosen.Count == 0)
            {
                MessageBox.Show(this, "Marcá por lo menos un paso para preparar la solicitud.", "Plan de cierre", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedScope = scope.SelectedItem as string ?? "Primera versión utilizable (recomendado)";
            var auditOnly = selectedScope.StartsWith("Solo auditar", StringComparison.OrdinalIgnoreCase);
            var includeAdvanced = selectedScope.StartsWith("Incluir también", StringComparison.OrdinalIgnoreCase);
            var request = new StringBuilder();
            request.AppendLine("VTUBERBOT — SOLICITUD DE CIERRE SELECCIONADA DESDE LA APLICACIÓN");
            request.AppendLine();
            request.AppendLine("Repositorio: https://github.com/jonhararagi/vtuber");
            request.AppendLine("Rama de trabajo: product/complete-vtuber-experience");
            request.AppendLine("Objetivo: " + selectedScope);
            request.AppendLine();
            request.AppendLine("INSTRUCCIONES OBLIGATORIAS");
            request.AppendLine("- Auditar el HEAD de GitHub y la documentación actual antes de cambiar nada; la lista es una prioridad solicitada, no prueba de que una función falte.");
            request.AppendLine("- Ejecutar el trabajo directamente en GitHub. No tocar main, no fusionar ni cerrar pull requests sin autorización explícita.");
            request.AppendLine("- Implementar funciones reales, no botones de muestra, respuestas simuladas ni afirmaciones sin evidencia.");
            request.AppendLine("- Mantener la arquitectura C#/.NET/WPF/SQLite, la lógica Core independiente de WPF y el producto local-first; no exigir APIs pagas.");
            request.AppendLine("- Resolver errores encontrados y ejecutar Windows CI para el SHA final exacto. Informar pruebas, artefacto, límites y cualquier validación que solo pueda hacerse en el PC real.");
            request.AppendLine("- Actualizar docs/CONTINUIDAD_PROYECTO.md como documento central y mantener sincronizadas la guía de usuario y el roadmap.");
            request.AppendLine();
            request.AppendLine("PASOS QUE QUIERO PRIORIZAR");
            foreach (var item in chosen)
                request.AppendLine("- [" + item.Key + "] " + item.Title + " — " + item.Check.Content);
            if (includeAdvanced && !chosen.Any(item => item.Key == "ADVANCED"))
                request.AppendLine("- [ADVANCED] Incluir evaluación de funciones avanzadas aunque no estén marcadas individualmente.");
            request.AppendLine();
            request.AppendLine(auditOnly
                ? "ALCANCE: primero auditar y entregar un plan ordenado con evidencia, bloqueos y criterios de aceptación; no comenzar cambios de código hasta presentar ese plan."
                : "ALCANCE: implementar por bloques coherentes lo seleccionado, revisar primero lo ya existente para no duplicar trabajo y continuar hasta que lo verificable esté resuelto; no detenerse a pedir confirmación por cada commit.");
            request.AppendLine("Al final, distinguir claramente: completo y probado por CI; implementado pero pendiente de validación manual; pendiente por decisiones del usuario; y fuera del alcance seleccionado.");
            var payload = request.ToString();

            string? savedPath = null;
            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VTuberBot", "ProjectFinishRequests");
                Directory.CreateDirectory(directory);
                savedPath = Path.Combine(directory, "cierre-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".md");
                File.WriteAllText(savedPath, payload, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                status.Text = "No se pudo guardar el archivo local: " + ex.Message;
            }

            try
            {
                Clipboard.SetText(payload);
                status.Text = savedPath is null
                    ? "Solicitud copiada al portapapeles. Pegala en el chat del agente para iniciar el trabajo."
                    : "Solicitud preparada y copiada al portapapeles. Pegala en el chat del agente para iniciar el trabajo. Archivo guardado: " + savedPath;
            }
            catch (Exception ex)
            {
                status.Text = "Solicitud preparada" + (savedPath is null ? "." : " y guardada en " + savedPath + ".") + " No se pudo copiar al portapapeles: " + ex.Message;
            }
            FooterStatus.Text = "Plan de cierre preparado con " + chosen.Count + " pasos seleccionados.";
        };
        body.Children.Add(send);
        body.Children.Add(status);
        body.Children.Add(new TextBlock
        {
            Text = "Por seguridad, Enviar no transmite tus datos ni utiliza credenciales de GitHub: prepara una solicitud local, la copia al portapapeles y la guarda como Markdown. Después pegala en el chat de GPT Cerebro/Obrero. El agente debe comprobar qué tareas siguen pendientes antes de ejecutarlas.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(157, 172, 198)),
            Margin = new Thickness(0, 12, 0, 0),
            FontSize = 11
        });
        card.Child = body;
        return card;
    }

    private void OpenMouthCutoutEditor()
    {
        var originalPath = avatarPath;
        if (string.IsNullOrWhiteSpace(originalPath) || !File.Exists(originalPath))
        {
            MessageBox.Show(this, "Importá o elegí una imagen base en Avatar Studio antes de editar la zona de la boca.", "Limpiar boca", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        BitmapSource original;
        try { original = LoadImage(originalPath); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo abrir la imagen base: " + ex.Message, "Limpiar boca", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        const double previewWidth = 640;
        const double previewHeight = 560;
        var renderScale = Math.Min(previewWidth / original.PixelWidth, previewHeight / original.PixelHeight);
        var renderedWidth = original.PixelWidth * renderScale;
        var renderedHeight = original.PixelHeight * renderScale;
        var imageLeft = (previewWidth - renderedWidth) / 2;
        var imageTop = (previewHeight - renderedHeight) / 2;
        var imageBounds = new Rect(imageLeft, imageTop, renderedWidth, renderedHeight);

        var editor = new Window
        {
            Owner = this,
            Title = "Limpiar la boca de la imagen base",
            Width = 850,
            Height = 790,
            MinWidth = 760,
            MinHeight = 720,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(17, 23, 39)),
            Foreground = Brushes.White
        };
        var root = new StackPanel { Margin = new Thickness(16) };
        root.Children.Add(new TextBlock
        {
            Text = "Marcá con el mouse un rectángulo que cubra toda la boca anterior, dejando unos píxeles de margen. Se guardará una copia nueva; la imagen original no se sobrescribe.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(205, 215, 232)),
            Margin = new Thickness(0, 0, 0, 10)
        });

        var coverWithSkin = new RadioButton
        {
            Content = "Cubrir con color de piel muestreado (recomendado para una imagen aplanada)",
            IsChecked = true,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 3, 0, 4)
        };
        var transparentCut = new RadioButton
        {
            Content = "Borrar a transparencia (mejor si hay otra capa de cara detrás)",
            Foreground = Brushes.White,
            Margin = new Thickness(0, 3, 0, 8)
        };
        root.Children.Add(coverWithSkin);
        root.Children.Add(transparentCut);

        var preview = new Grid
        {
            Width = previewWidth,
            Height = previewHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipToBounds = true
        };
        var checker = new Canvas { Width = previewWidth, Height = previewHeight, Background = new SolidColorBrush(Color.FromRgb(37, 45, 63)), IsHitTestVisible = false };
        for (var y = 0; y < (int)(previewHeight / 40); y++)
        for (var x = 0; x < (int)(previewWidth / 40); x++)
            checker.Children.Add(new Border
            {
                Width = 40,
                Height = 40,
                Background = new SolidColorBrush((x + y) % 2 == 0 ? Color.FromRgb(48, 57, 76) : Color.FromRgb(37, 45, 63)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(x * 40, y * 40, 0, 0)
            });
        var image = new Image { Source = original, Width = previewWidth, Height = previewHeight, Stretch = Stretch.Uniform, IsHitTestVisible = false };
        var selectionCanvas = new Canvas { Width = previewWidth, Height = previewHeight, Background = Brushes.Transparent };
        preview.Children.Add(checker);
        preview.Children.Add(image);
        preview.Children.Add(selectionCanvas);
        root.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(70, 82, 110)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = preview
        });
        var selectionStatus = new TextBlock
        {
            Text = "Todavía no seleccionaste la zona.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(180, 192, 215)),
            Margin = new Thickness(0, 8, 0, 8)
        };
        root.Children.Add(selectionStatus);

        Point ClampToImage(Point point) => new(
            Math.Clamp(point.X, imageBounds.Left, imageBounds.Right),
            Math.Clamp(point.Y, imageBounds.Top, imageBounds.Bottom));

        Point? start = null;
        Rect selectedRegion = Rect.Empty;
        Rectangle? selectionShape = null;
        selectionCanvas.MouseLeftButtonDown += (_, e) =>
        {
            var point = e.GetPosition(selectionCanvas);
            if (!imageBounds.Contains(point)) return;
            start = ClampToImage(point);
            selectedRegion = Rect.Empty;
            selectionCanvas.Children.Clear();
            selectionShape = new Rectangle
            {
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(70, 30, 144, 255)),
                IsHitTestVisible = false
            };
            selectionCanvas.Children.Add(selectionShape);
            Canvas.SetLeft(selectionShape, start.Value.X);
            Canvas.SetTop(selectionShape, start.Value.Y);
            selectionCanvas.CaptureMouse();
            e.Handled = true;
        };
        selectionCanvas.MouseMove += (_, e) =>
        {
            if (start is null || e.LeftButton != MouseButtonState.Pressed || selectionShape is null) return;
            var point = ClampToImage(e.GetPosition(selectionCanvas));
            var left = Math.Min(start.Value.X, point.X);
            var top = Math.Min(start.Value.Y, point.Y);
            selectionShape.Width = Math.Abs(point.X - start.Value.X);
            selectionShape.Height = Math.Abs(point.Y - start.Value.Y);
            Canvas.SetLeft(selectionShape, left);
            Canvas.SetTop(selectionShape, top);
            e.Handled = true;
        };
        selectionCanvas.MouseLeftButtonUp += (_, e) =>
        {
            if (start is null) return;
            var point = ClampToImage(e.GetPosition(selectionCanvas));
            selectedRegion = new Rect(start.Value, point);
            if (selectedRegion.Width < 4 || selectedRegion.Height < 4)
            {
                selectedRegion = Rect.Empty;
                selectionStatus.Text = "La selección es demasiado pequeña; arrastrá un rectángulo más grande.";
            }
            else
                selectionStatus.Text = $"Zona seleccionada: {selectedRegion.Width / renderScale:0} × {selectedRegion.Height / renderScale:0} px de la imagen original.";
            start = null;
            selectionCanvas.ReleaseMouseCapture();
            e.Handled = true;
        };

        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "Cancelar", MinWidth = 100, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => editor.DialogResult = false;
        var apply = new Button { Content = "Aplicar y guardar copia", MinWidth = 180, Padding = new Thickness(12, 7, 12, 7), FontWeight = FontWeights.SemiBold };
        apply.Click += (_, _) =>
        {
            if (selectedRegion.IsEmpty || selectedRegion.Width < 4 || selectedRegion.Height < 4)
            {
                MessageBox.Show(editor, "Primero arrastrá un rectángulo alrededor de la boca anterior.", "Limpiar boca", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var x0 = Math.Clamp((int)Math.Floor((selectedRegion.Left - imageLeft) / renderScale), 0, original.PixelWidth - 1);
            var y0 = Math.Clamp((int)Math.Floor((selectedRegion.Top - imageTop) / renderScale), 0, original.PixelHeight - 1);
            var x1 = Math.Clamp((int)Math.Ceiling((selectedRegion.Right - imageLeft) / renderScale), x0 + 1, original.PixelWidth);
            var y1 = Math.Clamp((int)Math.Ceiling((selectedRegion.Bottom - imageTop) / renderScale), y0 + 1, original.PixelHeight);

            var transparent = transparentCut.IsChecked == true;
            var mode = transparent ? "borrar a transparencia" : "cubrir con color muestreado";
            if (MessageBox.Show(editor,
                $"Se creará una copia nueva de la imagen para {mode}. La original permanecerá intacta. En modo color se usa un promedio del borde, por lo que el retoque puede requerir revisión visual. ¿Continuar?",
                "Confirmar edición de boca", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                var cleaned = ApplyMouthCutout(original, x0, y0, x1, y1, transparent);
                Directory.CreateDirectory(avatarDirectory);
                var destination = Path.Combine(avatarDirectory, "avatar-mouth-cleaned-" + Guid.NewGuid().ToString("N") + ".png");
                SaveBitmapAsPng(cleaned, destination);
                avatarPath = destination;
                store.SetActiveAvatarProfileBasePath(destination);
                RefreshActiveAvatarOverlay();
                FooterStatus.Text = $"Zona de boca procesada ({mode}). Se guardó una copia y se conservó el original.";
                store.Log("Avatar", "Se creó una copia de la imagen base para limpiar la boca (" + mode + ").");
                Navigate("Avatar Studio");
                editor.DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(editor, "No se pudo procesar la zona de la boca: " + ex.Message, "Limpiar boca", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        actions.Children.Add(cancel);
        actions.Children.Add(apply);
        root.Children.Add(actions);
        editor.Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = root };
        editor.ShowDialog();
    }

    private static BitmapSource ApplyMouthCutout(BitmapSource source, int x0, int y0, int x1, int y1, bool transparent)
    {
        if (x1 <= x0 || y1 <= y0)
            throw new ArgumentException("La zona seleccionada no tiene tamaño.");

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        converted.CopyPixels(pixels, stride, 0);

        var feather = Math.Clamp(Math.Min(x1 - x0, y1 - y0) / 8, 3, 10);
        byte averageB = 0, averageG = 0, averageR = 0;
        if (!transparent)
        {
            long sumB = 0, sumG = 0, sumR = 0, samples = 0;
            var left = Math.Max(0, x0 - feather);
            var right = Math.Min(width, x1 + feather);
            var top = Math.Max(0, y0 - feather);
            var bottom = Math.Min(height, y1 + feather);
            for (var y = top; y < bottom; y++)
            for (var x = left; x < right; x++)
            {
                if (x >= x0 && x < x1 && y >= y0 && y < y1) continue;
                var index = y * stride + x * 4;
                if (pixels[index + 3] < 128) continue;
                sumB += pixels[index];
                sumG += pixels[index + 1];
                sumR += pixels[index + 2];
                samples++;
            }
            if (samples == 0)
                throw new InvalidOperationException("No hay píxeles opacos alrededor de la selección para calcular el color de cobertura.");
            averageB = (byte)(sumB / samples);
            averageG = (byte)(sumG / samples);
            averageR = (byte)(sumR / samples);
        }

        var expandedLeft = Math.Max(0, x0 - feather);
        var expandedRight = Math.Min(width, x1 + feather);
        var expandedTop = Math.Max(0, y0 - feather);
        var expandedBottom = Math.Min(height, y1 + feather);
        for (var y = expandedTop; y < expandedBottom; y++)
        for (var x = expandedLeft; x < expandedRight; x++)
        {
            var dx = x < x0 ? x0 - x : x >= x1 ? x - (x1 - 1) : 0;
            var dy = y < y0 ? y0 - y : y >= y1 ? y - (y1 - 1) : 0;
            var distance = Math.Max(dx, dy);
            if (distance > feather) continue;
            var offset = y * stride + x * 4;
            if (transparent)
            {
                if (distance == 0)
                    pixels[offset + 3] = 0;
                else
                    pixels[offset + 3] = (byte)Math.Round(pixels[offset + 3] * Math.Clamp(distance / (double)feather, 0, 1));
            }
            else
            {
                var weight = distance == 0 ? 1 : Math.Clamp(1 - distance / (double)feather, 0, 1);
                pixels[offset] = (byte)Math.Round(pixels[offset] * (1 - weight) + averageB * weight);
                pixels[offset + 1] = (byte)Math.Round(pixels[offset + 1] * (1 - weight) + averageG * weight);
                pixels[offset + 2] = (byte)Math.Round(pixels[offset + 2] * (1 - weight) + averageR * weight);
            }
        }

        var result = new WriteableBitmap(width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null);
        result.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        result.Freeze();
        return result;
    }
}
