using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Wpf;
using NAudio.Wave;
using VtuberBot.Core;

namespace VtuberBot.App;

public partial class MainWindow
{
    private UIElement BuildAvatarStudio()
    {
        var root = new StackPanel();
        var profilePanel = Panel("Perfiles de avatar");
        profilePanel.Margin = new Thickness(0, 0, 0, 14);
        profilePanel.Children.Add(Label("Cada perfil conserva su imagen base, capas, transformaciones y sprites de expresión.", 12, "#98A4BE"));
        var profileList = store.GetAvatarProfiles();
        var activeProfile = store.GetActiveAvatarProfile();
        var profilePicker = new ComboBox
        {
            ItemsSource = profileList,
            DisplayMemberPath = nameof(AvatarProfile.Name),
            SelectedItem = profileList.First(profile => profile.Id == activeProfile.Id),
            MinWidth = 240,
            Margin = new Thickness(0, 0, 0, 8)
        };
        profilePanel.Children.Add(profilePicker);

        void SwitchToProfile(AvatarProfile profile)
        {
            try
            {
                store.SetActiveAvatarProfile(profile.Id);
                var activated = store.GetActiveAvatarProfile();
                avatarPath = activated.BasePath;
                RefreshActiveAvatarOverlay();
                store.Log("Avatar", "Perfil activo: " + activated.Name + ".");
                Navigate("Avatar Studio");
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "No se pudo activar el perfil: " + exception.Message);
            }
        }

        string? PromptProfileName(string title, string initialName)
        {
            var dialog = new Window
            {
                Owner = this,
                Title = title,
                Width = 390,
                Height = 190,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brush("#161F33"),
                Foreground = Brush("#F8FAFC")
            };
            var body = new StackPanel { Margin = new Thickness(18) };
            body.Children.Add(Label("Nombre del perfil (1–64 caracteres)", 12, "#98A4BE"));
            var nameBox = new TextBox
            {
                Text = initialName,
                Background = Brush("#101525"),
                Foreground = Brush("#F8FAFC"),
                BorderBrush = Brush("#34415E"),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 6, 0, 12)
            };
            body.Children.Add(nameBox);
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "Cancelar", MinWidth = 90, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 5, 10, 5) };
            cancel.Click += (_, _) => { dialog.DialogResult = false; };
            var save = new Button { Content = "Guardar", MinWidth = 90, Padding = new Thickness(10, 5, 10, 5) };
            save.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text) || nameBox.Text.Trim().Length > 64)
                {
                    MessageBox.Show(dialog, "El nombre debe tener entre 1 y 64 caracteres.");
                    return;
                }
                dialog.DialogResult = true;
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(save);
            body.Children.Add(buttons);
            dialog.Content = body;
            dialog.Loaded += (_, _) => { nameBox.Focus(); nameBox.SelectAll(); };
            return dialog.ShowDialog() == true ? nameBox.Text.Trim() : null;
        }

        void CreateProfile(bool duplicate)
        {
            var suggestedName = duplicate ? activeProfile.Name + " (copia)" : "Nuevo avatar";
            var name = PromptProfileName(duplicate ? "Duplicar perfil de avatar" : "Crear perfil de avatar", suggestedName);
            if (name is null) return;
            try
            {
                var created = store.CreateAvatarProfile(name, duplicate);
                SwitchToProfile(created);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "No se pudo crear el perfil: " + exception.Message);
            }
        }

        var profileActions = new WrapPanel();
        profileActions.Children.Add(ActionButton("＋ Nuevo perfil vacío", (_, _) => CreateProfile(false), true));
        profileActions.Children.Add(ActionButton("Duplicar perfil", (_, _) => CreateProfile(true)));
        profileActions.Children.Add(ActionButton("Eliminar perfil activo", (_, _) =>
        {
            if (store.GetAvatarProfiles().Count <= 1)
            {
                MessageBox.Show(this, "Debe quedar al menos un perfil de avatar.");
                return;
            }
            if (MessageBox.Show(this, "Se eliminará el perfil y sus capas registradas. Los archivos de imagen del disco no se borran. ¿Continuar?",
                "Eliminar perfil de avatar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            try
            {
                var next = store.DeleteAvatarProfile(store.GetActiveAvatarProfile().Id);
                avatarPath = next.BasePath;
                RefreshActiveAvatarOverlay();
                store.Log("Avatar", "Perfil eliminado; perfil activo: " + next.Name + ".");
                Navigate("Avatar Studio");
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, "No se pudo eliminar el perfil: " + exception.Message);
            }
        }));
        profilePicker.SelectionChanged += (_, _) =>
        {
            if (profilePicker.SelectedItem is AvatarProfile profile && profile.Id != store.GetActiveAvatarProfile().Id)
                SwitchToProfile(profile);
        };
        root.Children.Add(profilePanel);
        root.Children.Add(profileActions);

        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var layerImages = new Dictionary<long, Image>();
        Grid? previewCanvas = null;
        Image? basePreviewImage = null;
        var previewState = store.GetSetting("avatar.state", "Idle");
        if (previewState == "Blink") previewState = "Idle";
        var left = Panel("Previsualización del avatar");
        left.MinHeight = 420;
        var preview = new Border { Background = Brush("#101525"), CornerRadius = new CornerRadius(14), Padding = new Thickness(14), MinHeight = 350 };
        if (!string.IsNullOrWhiteSpace(avatarPath) && File.Exists(avatarPath))
        {
            previewCanvas = new Grid { Background = Brush("#101525"), Focusable = true };
            var savedExpression = store.GetAvatarExpression(previewState) ?? string.Empty;
            var initialImagePath = !string.IsNullOrWhiteSpace(savedExpression) && File.Exists(savedExpression) ? savedExpression : avatarPath;
            basePreviewImage = new Image { Source = LoadImage(initialImagePath), Stretch = Stretch.Uniform, MaxHeight = 360 };
            previewCanvas.Children.Add(basePreviewImage);
            foreach (var layer in store.GetAvatarLayers().Where(l => l.Visible && File.Exists(l.Path)).OrderBy(l => l.SortOrder))
            {
                var image = new Image { Source = LoadImage(layer.Path), Stretch = Stretch.Uniform, MaxHeight = 360 };
                image.RenderTransformOrigin = new Point(.5, .5);
                image.RenderTransform = BuildLayerTransform(layer);
                previewCanvas.Children.Add(image);
                layerImages[layer.Id] = image;
            }
            preview.Child = previewCanvas;
        }
        else preview.Child = Label("Todavía no hay arte importado.\nUsá un PNG propio para comenzar.", 16, "#98A4BE");
        left.Children.Add(preview);
        left.Children.Add(Label("Seleccioná una capa visible a la derecha, arrastrala en esta vista y guardá la transformación cuando te guste el resultado.", 12, "#98A4BE"));

        var right = Panel("Avatar y salida");
        right.Margin = new Thickness(18, 0, 0, 0);
        right.Children.Add(Label("Avatar activo", 14, "#F8FAFC", true));
        right.Children.Add(Label(string.IsNullOrWhiteSpace(avatarPath) ? "Ningún archivo seleccionado" : Path.GetFileName(avatarPath), 12, "#98A4BE"));
        right.Children.Add(ActionButton("＋  Importar PNG / imagen base", (_, _) => ImportAvatar(), true));
        right.Children.Add(Label("Capas adicionales — orden de abajo hacia arriba", 13, "#F8FAFC", true));

        var layerList = new ListBox { Height = 142 };
        long? SelectedLayerId()
        {
            if (layerList.SelectedItem is not string item) return null;
            return long.TryParse(item.Split('|')[0].Trim(), out var id) ? id : null;
        }
        void ReloadLayers(long? selectId = null)
        {
            var entries = store.GetAvatarLayers().Select(l => $"{l.Id} | {(l.Visible ? "●" : "○")} | {l.Name}").ToList();
            layerList.ItemsSource = entries;
            if (selectId.HasValue)
                layerList.SelectedItem = entries.FirstOrDefault(s => long.TryParse(s.Split('|')[0].Trim(), out var id) && id == selectId.Value);
        }
        ReloadLayers();
        right.Children.Add(layerList);

        var layerActions = new WrapPanel();
        layerActions.Children.Add(ActionButton("＋ Importar capa", (_, _) => ImportAvatarLayer()));
        layerActions.Children.Add(ActionButton("Mostrar / ocultar", (_, _) =>
        {
            var id = SelectedLayerId();
            if (id is null) return;
            var layer = store.GetAvatarLayers().FirstOrDefault(l => l.Id == id.Value);
            if (layer is null) return;
            store.SetAvatarLayerVisibility(id.Value, !layer.Visible);
            RefreshAvatarPresentation();
        }));
        layerActions.Children.Add(ActionButton("↑ Subir", (_, _) => MoveSelectedLayer(1)));
        layerActions.Children.Add(ActionButton("↓ Bajar", (_, _) => MoveSelectedLayer(-1)));
        layerActions.Children.Add(ActionButton("Eliminar capa", (_, _) =>
        {
            var id = SelectedLayerId();
            if (id is null) return;
            if (MessageBox.Show(this, "¿Eliminar esta capa del perfil? El archivo local no se borra.", "Confirmar", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            store.DeleteAvatarLayer(id.Value);
            RefreshAvatarPresentation();
        }));
        right.Children.Add(layerActions);

        var transforms = Panel("Transformar capa seleccionada");
        transforms.Margin = new Thickness(0, 10, 0, 0);
        var xSlider = new Slider { Minimum = -500, Maximum = 500, TickFrequency = 10 };
        var ySlider = new Slider { Minimum = -500, Maximum = 500, TickFrequency = 10 };
        var scaleSlider = new Slider { Minimum = .1, Maximum = 4, Value = 1, TickFrequency = .1 };
        var rotationSlider = new Slider { Minimum = -180, Maximum = 180, TickFrequency = 5 };
        var xValue = Label("X: 0 px", 11, "#98A4BE");
        var yValue = Label("Y: 0 px", 11, "#98A4BE");
        var scaleValue = Label("Escala: 100%", 11, "#98A4BE");
        var rotationValue = Label("Rotación: 0°", 11, "#98A4BE");
        transforms.Children.Add(xValue); transforms.Children.Add(xSlider);
        transforms.Children.Add(yValue); transforms.Children.Add(ySlider);
        transforms.Children.Add(scaleValue); transforms.Children.Add(scaleSlider);
        transforms.Children.Add(rotationValue); transforms.Children.Add(rotationSlider);
        var loadingTransform = false;

        void PreviewSelectedTransform()
        {
            xValue.Text = $"X: {xSlider.Value:0} px";
            yValue.Text = $"Y: {ySlider.Value:0} px";
            scaleValue.Text = $"Escala: {scaleSlider.Value:P0}";
            rotationValue.Text = $"Rotación: {rotationSlider.Value:0}°";
            if (loadingTransform) return;
            var id = SelectedLayerId();
            if (id is null || !layerImages.TryGetValue(id.Value, out var image)) return;
            var layer = store.GetAvatarLayers().FirstOrDefault(l => l.Id == id.Value);
            if (layer is not null)
                image.RenderTransform = BuildLayerTransform(layer with { OffsetX = xSlider.Value, OffsetY = ySlider.Value, Scale = scaleSlider.Value, RotationDegrees = rotationSlider.Value });
        }

        void LoadSelectedTransform()
        {
            var id = SelectedLayerId();
            var layer = id is null ? null : store.GetAvatarLayers().FirstOrDefault(l => l.Id == id.Value);
            loadingTransform = true;
            xSlider.Value = layer?.OffsetX ?? 0;
            ySlider.Value = layer?.OffsetY ?? 0;
            scaleSlider.Value = layer?.Scale ?? 1;
            rotationSlider.Value = layer?.RotationDegrees ?? 0;
            loadingTransform = false;
            PreviewSelectedTransform();
        }
        layerList.SelectionChanged += (_, _) => LoadSelectedTransform();
        xSlider.ValueChanged += (_, _) => PreviewSelectedTransform();
        ySlider.ValueChanged += (_, _) => PreviewSelectedTransform();
        scaleSlider.ValueChanged += (_, _) => PreviewSelectedTransform();
        rotationSlider.ValueChanged += (_, _) => PreviewSelectedTransform();

        // Dragging moves only the selected visible layer. The edit is preview-only until Save is pressed.
        var dragCanvas = previewCanvas;
        if (dragCanvas is not null)
        {
            System.Windows.Point dragOrigin = default;
            var dragStartX = 0d;
            var dragStartY = 0d;
            var isDraggingLayer = false;

            dragCanvas.MouseLeftButtonDown += (_, e) =>
            {
                var id = SelectedLayerId();
                if (id is null || !layerImages.ContainsKey(id.Value))
                    return;

                dragOrigin = e.GetPosition(dragCanvas);
                dragStartX = xSlider.Value;
                dragStartY = ySlider.Value;
                isDraggingLayer = dragCanvas.CaptureMouse();
                if (isDraggingLayer)
                    e.Handled = true;
            };
            dragCanvas.MouseMove += (_, e) =>
            {
                if (!isDraggingLayer || !dragCanvas.IsMouseCaptured)
                    return;

                var current = e.GetPosition(dragCanvas);
                xSlider.Value = Math.Clamp(dragStartX + current.X - dragOrigin.X, xSlider.Minimum, xSlider.Maximum);
                ySlider.Value = Math.Clamp(dragStartY + current.Y - dragOrigin.Y, ySlider.Minimum, ySlider.Maximum);
            };
            dragCanvas.MouseLeftButtonUp += (_, e) =>
            {
                if (!isDraggingLayer)
                    return;

                isDraggingLayer = false;
                dragCanvas.ReleaseMouseCapture();
                FooterStatus.Text = "Vista previa ajustada. Pulsá «Aplicar y guardar transformación» para conservar la posición.";
                e.Handled = true;
            };
        }

        transforms.Children.Add(ActionButton("Restablecer posición y giro", (_, _) =>
        {
            if (SelectedLayerId() is null)
            {
                MessageBox.Show(this, "Seleccioná una capa primero.");
                return;
            }
            xSlider.Value = 0;
            ySlider.Value = 0;
            scaleSlider.Value = 1;
            rotationSlider.Value = 0;
            PreviewSelectedTransform();
        }));

        transforms.Children.Add(ActionButton("Aplicar y guardar transformación", (_, _) =>
        {
            var id = SelectedLayerId();
            if (id is null) { MessageBox.Show(this, "Seleccioná una capa primero."); return; }
            try
            {
                store.UpdateAvatarLayerTransform(id.Value, xSlider.Value, ySlider.Value, scaleSlider.Value, rotationSlider.Value);
                if (overlay is { IsVisible: true } && !string.IsNullOrWhiteSpace(avatarPath))
                    overlay.SetAvatar(avatarPath, store.GetAvatarLayers(), GetAvatarExpressionPaths());
                FooterStatus.Text = "Transformación de capa guardada en SQLite.";
            }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo guardar la transformación: " + ex.Message); }
        }, true));
        right.Children.Add(transforms);

        right.Children.Add(ActionButton("Mostrar overlay transparente", (_, _) => ShowOverlay()));
        right.Children.Add(ActionButton("Ocultar overlay", (_, _) => { overlay?.Hide(); store.Log("Avatar", "Overlay ocultado."); }));
        right.Children.Add(Label("Estados disponibles", 14, "#F8FAFC", true));
        var states = new WrapPanel();
        foreach (var state in new[] { "Idle", "Talking", "Blink", "Happy", "Surprised" })
            states.Children.Add(ActionButton(state, (_, _) =>
            {
                store.SetSetting("avatar.state", state == "Blink" ? "Idle" : state);
                UpdatePreviewState(state);
                overlay?.SetState(state);
                store.Log("Avatar", $"Estado seleccionado: {state}.");
                FooterStatus.Text = $"Cue visual del avatar: {state}. Si existe un sprite asignado, se muestra ese estado.";
            }));
        right.Children.Add(states);

        void UpdatePreviewState(string selectedState)
        {
            if (basePreviewImage is null || string.IsNullOrWhiteSpace(avatarPath) || !File.Exists(avatarPath)) return;
            var expressionPath = store.GetAvatarExpression(selectedState) ?? string.Empty;
            basePreviewImage.Source = !string.IsNullOrWhiteSpace(expressionPath) && File.Exists(expressionPath)
                ? LoadImage(expressionPath)
                : LoadImage(avatarPath);
        }

        var expressionPanel = Panel("Sprites por expresión");
        expressionPanel.Margin = new Thickness(0, 12, 0, 0);
        expressionPanel.Children.Add(Label("Asigná imágenes del mismo tamaño para cambiar la cara cuando el micrófono activa Talking o al elegir una expresión. Si no asignás una imagen, se usa la base.", 12, "#98A4BE"));
        foreach (var expressionName in new[] { "Idle", "Talking", "Blink", "Happy", "Surprised" })
        {
            var savedPath = store.GetAvatarExpression(expressionName) ?? string.Empty;
            var row = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            row.Children.Add(Label(expressionName + ": " + (!string.IsNullOrWhiteSpace(savedPath) && File.Exists(savedPath) ? Path.GetFileName(savedPath) : "usa imagen base"), 12, "#F8FAFC"));
            row.Children.Add(ActionButton("Importar / cambiar", (_, _) => ImportAvatarExpression(expressionName)));
            row.Children.Add(ActionButton("Quitar", (_, _) =>
            {
                store.SetAvatarExpression(expressionName, null);
                store.Log("Avatar", "Sprite de expresión quitado: " + expressionName);
                Navigate("Avatar Studio");
            }));
            expressionPanel.Children.Add(row);
        }
        right.Children.Add(expressionPanel);

        right.Children.Add(Label("X/Y, escala, rotación y orden se guardan localmente. El runtime admite sprites por estado; la física por huesos y el lip-sync por fonemas/visemas todavía requieren un rig más avanzado.", 12, "#98A4BE"));

        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        columns.Children.Add(left);
        columns.Children.Add(right);
        root.Children.Add(columns);
        return root;

        void MoveSelectedLayer(int direction)
        {
            var id = SelectedLayerId();
            if (id is null) return;
            try { store.MoveAvatarLayer(id.Value, direction); RefreshAvatarPresentation(); }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo cambiar el orden: " + ex.Message); }
        }

        void RefreshAvatarPresentation()
        {
            if (overlay is { IsVisible: true } && !string.IsNullOrWhiteSpace(avatarPath))
                overlay.SetAvatar(avatarPath, store.GetAvatarLayers(), GetAvatarExpressionPaths());
            Navigate("Avatar Studio");
        }
    }

    private void ImportAvatar()
    {
        var dialog=new OpenFileDialog{Title="Importar arte del avatar",Filter="Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.webp",Multiselect=false};
        if(dialog.ShowDialog(this)!=true)return;
        var info=new FileInfo(dialog.FileName);
        if(info.Length>25*1024*1024){MessageBox.Show(this,"El archivo supera el límite de 25 MB.","VTuberBot",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        var extension=Path.GetExtension(dialog.FileName).ToLowerInvariant();
        if(extension is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp"))return;
        try
        {
            _=LoadImage(dialog.FileName);
            var destination=Path.Combine(avatarDirectory,$"avatar-{DateTime.UtcNow:yyyyMMdd-HHmmss}{extension}");
            File.Copy(dialog.FileName,destination,false);avatarPath=destination;store.SetActiveAvatarProfileBasePath(destination);RefreshActiveAvatarOverlay();store.Log("Avatar",$"Asset importado: {Path.GetFileName(destination)}");
            Navigate("Avatar Studio");
        }
        catch(Exception ex){MessageBox.Show(this,$"No se pudo importar la imagen: {ex.Message}","VTuberBot",MessageBoxButton.OK,MessageBoxImage.Error);}
    }

    private void ImportAvatarLayer()
    {
        var dialog=new OpenFileDialog{Title="Importar capa del avatar",Filter="Imágenes PNG/JPG/BMP|*.png;*.jpg;*.jpeg;*.bmp",Multiselect=false};
        if(dialog.ShowDialog(this)!=true)return;
        try
        {
            var info=new FileInfo(dialog.FileName);if(info.Length>25*1024*1024)throw new InvalidOperationException("El archivo supera 25 MB.");
            _=LoadImage(dialog.FileName);
            var destination=Path.Combine(avatarDirectory,"layer-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+Path.GetExtension(dialog.FileName).ToLowerInvariant());
            File.Copy(dialog.FileName,destination,false);store.AddAvatarLayer(Path.GetFileNameWithoutExtension(dialog.FileName),destination);store.Log("Avatar","Capa importada: "+Path.GetFileName(destination));
            Navigate("Avatar Studio");
        }
        catch(Exception ex){MessageBox.Show(this,"No se pudo importar la capa: "+ex.Message);}
    }

    private void ImportAvatarExpression(string expressionName)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar sprite para " + expressionName,
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var info = new FileInfo(dialog.FileName);
            if (info.Length > 25 * 1024 * 1024)
                throw new InvalidOperationException("El archivo supera el límite de 25 MB.");
            _ = LoadImage(dialog.FileName);
            var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            var destination = Path.Combine(avatarDirectory,
                "expression-" + expressionName.ToLowerInvariant() + "-" + Guid.NewGuid().ToString("N") + extension);
            File.Copy(dialog.FileName, destination, false);
            store.SetAvatarExpression(expressionName, destination);
            store.Log("Avatar", "Sprite de expresión importado: " + expressionName);
            Navigate("Avatar Studio");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo importar el sprite de expresión: " + ex.Message);
        }
    }

    private IReadOnlyDictionary<string, string> GetAvatarExpressionPaths() =>
        store.GetAvatarExpressions()
            .Where(item => !string.IsNullOrWhiteSpace(item.Value) && File.Exists(item.Value))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private void RefreshActiveAvatarOverlay()
    {
        if (overlay is { IsVisible: true })
            overlay.SetAvatar(avatarPath ?? string.Empty, store.GetAvatarLayers(), GetAvatarExpressionPaths());
    }

    private void ShowOverlay()
    {
        if(string.IsNullOrWhiteSpace(avatarPath)||!File.Exists(avatarPath)){MessageBox.Show(this,"Primero importá una imagen en Avatar Studio.");return;}
        try { overlay??=new AvatarOverlayWindow(); overlay.SetAvatar(avatarPath, store.GetAvatarLayers(), GetAvatarExpressionPaths()); overlay.ReduceMotion=store.GetSetting("ui.reduceMotion","false")=="true"; overlay.SetState(store.GetSetting("avatar.state","Idle")); if(!overlay.IsVisible)overlay.Show();overlay.Activate();store.Log("Avatar","Overlay mostrado."); }
        catch(Exception ex){MessageBox.Show(this,$"No se pudo abrir el overlay: {ex.Message}");}
    }
}
