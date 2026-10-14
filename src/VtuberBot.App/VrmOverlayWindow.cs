using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace VtuberBot.App;

/// <summary>
/// Transparent, always-on-top VRM renderer. Three.js and three-vrm are loaded from pinned CDN
/// versions; the user's model is served from a local WebView2 virtual host and is not uploaded.
/// </summary>
public sealed class VrmOverlayWindow : Window
{
    private readonly WebView2 browser = new();
    private readonly string modelPath;
    private bool pageReady;
    private string currentState = "Idle";

    public VrmOverlayWindow(string modelPath)
    {
        this.modelPath = Path.GetFullPath(modelPath);
        if (!File.Exists(this.modelPath))
            throw new FileNotFoundException("No se encontró el modelo VRM seleccionado.", this.modelPath);

        Title = "VTuberBot · Overlay VRM";
        Width = 520;
        Height = 680;
        MinWidth = 240;
        MinHeight = 280;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Content = browser;
        browser.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0, 0, 0, 0);

        Loaded += InitializeAsync;
        Closed += (_, _) => browser.Dispose();
    }

    private async void InitializeAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            await browser.EnsureCoreWebView2Async();
            browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "vtuberbot.local",
                Path.GetDirectoryName(modelPath)!,
                CoreWebView2HostResourceAccessKind.Allow);
            browser.NavigationCompleted += async (_, args) =>
            {
                if (!args.IsSuccess)
                {
                    Title = "VTuberBot · No se pudo iniciar el renderizador VRM";
                    return;
                }

                pageReady = true;
                await browser.ExecuteScriptAsync("window.loadVrm(" + JsonSerializer.Serialize(
                    "https://vtuberbot.local/" + Uri.EscapeDataString(Path.GetFileName(modelPath))) + ")");
                await SetStateAsync(currentState);
            };
            browser.NavigateToString(RendererHtml);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this,
                "No se pudo iniciar el renderizador 3D. Comprueba Microsoft Edge WebView2 Runtime y la conexión a Internet.\n\n"
                + exception.GetBaseException().Message,
                "VTuberBot · VRM", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
        }
    }

    public void SetState(string state)
    {
        currentState = state is "Talking" or "Happy" or "Surprised" ? state : "Idle";
        if (pageReady && IsLoaded)
            _ = SetStateAsync(currentState);
    }

    private Task SetStateAsync(string state) =>
        browser.ExecuteScriptAsync("window.setAvatarState(" + JsonSerializer.Serialize(state) + ")");

    private const string RendererHtml = """
<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<style>
html,body{width:100%;height:100%;margin:0;overflow:hidden;background:transparent}
canvas{display:block;width:100%;height:100%;background:transparent}
#status{position:absolute;left:10px;right:10px;bottom:10px;color:#f8fafc;
font:12px Segoe UI,Arial,sans-serif;text-shadow:0 1px 4px #111;pointer-events:none}
</style>
</head>
<body>
<div id="status">Iniciando renderizador VRM…</div>
<script type="module">
import * as THREE from 'https://esm.sh/three@0.170.0';
import { GLTFLoader } from 'https://esm.sh/three@0.170.0/examples/jsm/loaders/GLTFLoader.js?external=three';
import { VRMLoaderPlugin, VRMUtils } from 'https://esm.sh/@pixiv/three-vrm@3.3.0?external=three';
const status = document.getElementById('status');
const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(30, innerWidth/innerHeight, 0.1, 100);
camera.position.set(0, 1.35, 3.8);
const renderer = new THREE.WebGLRenderer({antialias:true,alpha:true,powerPreference:'low-power'});
renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 1.5));
renderer.setSize(innerWidth,innerHeight);
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.setClearColor(0x000000,0);
document.body.appendChild(renderer.domElement);
scene.add(new THREE.HemisphereLight(0xffffff,0x687080,2.0));
const key = new THREE.DirectionalLight(0xffffff,2.0); key.position.set(1,2,3); scene.add(key);
const loader = new GLTFLoader();
loader.register(parser => new VRMLoaderPlugin(parser));
let vrm = null, state = 'Idle', start = performance.now();
window.setAvatarState = next => {
  state = ['Talking','Happy','Surprised'].includes(next) ? next : 'Idle';
  if (vrm?.expressionManager) {
    for (const name of ['aa','happy','surprised']) {
      try { vrm.expressionManager.setValue(name, name === 'aa' && state === 'Talking' ? 0.55 : name === state.toLowerCase() ? 0.45 : 0); } catch {}
    }
  }
};
window.loadVrm = async url => {
  try {
    status.textContent = 'Cargando modelo VRM local…';
    const gltf = await loader.loadAsync(url);
    vrm = gltf.userData.vrm;
    if (!vrm) throw new Error('El archivo no contiene un modelo VRM compatible.');
    VRMUtils.removeUnnecessaryVertices(gltf.scene);
    VRMUtils.combineSkeletons(gltf.scene);
    scene.add(vrm.scene);
    const box = new THREE.Box3().setFromObject(vrm.scene);
    const size = box.getSize(new THREE.Vector3());
    const center = box.getCenter(new THREE.Vector3());
    vrm.scene.position.x -= center.x;
    vrm.scene.position.y -= box.min.y;
    vrm.scene.position.z -= center.z;
    const height = Math.max(size.y, 1.0);
    camera.position.set(0, height * 0.56, height * 2.8);
    camera.lookAt(0, height * 0.5, 0);
    status.textContent = 'VRM cargado · doble clic para alternar siempre visible';
    window.setAvatarState(state);
  } catch (error) {
    status.textContent = 'No se pudo cargar el VRM: ' + (error?.message || error);
  }
};
let last = 0;
function animate(now) {
  requestAnimationFrame(animate);
  const delta = Math.min((now-last)/1000 || 0, 0.05); last = now;
  if (vrm) {
    vrm.update(delta);
    const t = (now-start)/1000;
    vrm.scene.rotation.y = Math.sin(t*0.45)*0.07;
    const chest = vrm.humanoid?.getNormalizedBoneNode('chest');
    if (chest) chest.rotation.x = Math.sin(t*(state==='Talking'?4:1.2))* (state==='Talking'?0.025:0.008);
  }
  renderer.render(scene,camera);
}
requestAnimationFrame(animate);
addEventListener('resize', () => {
  camera.aspect = innerWidth/innerHeight; camera.updateProjectionMatrix();
  renderer.setSize(innerWidth,innerHeight);
});
</script>
<script>
document.addEventListener('dblclick', () => { window.chrome?.webview?.postMessage('toggle-topmost'); });
</script>
</body>
</html>
""";
}
