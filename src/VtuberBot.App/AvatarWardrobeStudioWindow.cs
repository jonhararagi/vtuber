using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;

namespace VtuberBot.App;

/// <summary>
/// A built-in parametric 3D character workshop. The base body, hair styles, eyes,
/// outfits, colors and accessories are generated in the embedded viewport.
/// This prototype exports/imports appearance presets as JSON; it does not yet export VRM.
/// </summary>
public sealed class AvatarWardrobeStudioWindow : Window
{
    private readonly WebView2 browser = new();
    private readonly TextBlock status = new();

    public AvatarWardrobeStudioWindow()
    {
        Title = "VTuberBot · Taller de personaje 3D";
        Width = 1500;
        Height = 960;
        MinWidth = 950;
        MinHeight = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(12, 16, 32));
        Foreground = Brushes.White;

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        status.Text = "Inicializando el taller 3D…";
        status.Padding = new Thickness(14, 8, 14, 8);
        status.Foreground = new SolidColorBrush(Color.FromRgb(190, 198, 220));
        Grid.SetRow(status, 0);
        layout.Children.Add(status);
        Grid.SetRow(browser, 1);
        layout.Children.Add(browser);
        Content = layout;

        Loaded += async (_, _) =>
        {
            try
            {
                await browser.EnsureCoreWebView2Async();
                browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
                browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
                browser.NavigationCompleted += (_, args) =>
                {
                    status.Text = args.IsSuccess
                        ? "Taller 3D cargado · Podés cambiar cuerpo, peinados, ropa, colores y accesorios. Requiere Internet para cargar Three.js."
                        : "No se pudo cargar el taller. Comprobá la conexión a Internet y reiniciá la ventana.";
                };
                browser.NavigateToString(EditorHtml);
            }
            catch (Exception exception)
            {
                status.Text = "No se pudo iniciar WebView2: " + exception.GetBaseException().Message;
            }
        };
    }

    private const string EditorHtml = """
<!doctype html>
<html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>VTuberBot · Taller de personaje 3D</title>
<style>
:root{color-scheme:dark;--bg:#0c1020;--panel:#151b2d;--line:#2b3550;--text:#f4f5fb;--muted:#a6b0ca;--accent:#bba2ff;--pink:#f1a8d3}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:14px/1.4 "Segoe UI",sans-serif}#app{display:grid;grid-template-columns:minmax(230px,300px) 1fr;min-height:100vh}.side{padding:18px;background:var(--panel);border-right:1px solid var(--line);overflow:auto}.brand{font-size:19px;font-weight:750;letter-spacing:-.4px}.sub{color:var(--muted);font-size:12px;margin:5px 0 18px}.group{padding:13px 0;border-top:1px solid var(--line)}h3{font-size:12px;text-transform:uppercase;letter-spacing:1.1px;color:#c5b9ff;margin:0 0 10px}label{display:block;color:#d9deed;font-size:12px;margin:10px 0 5px}select,input[type=color]{width:100%;background:#0c1222;border:1px solid var(--line);border-radius:8px;color:var(--text);padding:9px}input[type=range]{width:100%;accent-color:var(--accent)}.rangeval{display:flex;justify-content:space-between;color:var(--muted);font-size:11px}.btn{background:#282344;color:white;border:1px solid #55478b;border-radius:9px;padding:10px 12px;width:100%;font-weight:650;cursor:pointer;margin-top:8px}.btn.secondary{background:#1b2438;border-color:var(--line)}.btn:hover{filter:brightness(1.18)}.note{color:var(--muted);font-size:11px;margin-top:13px}.stage{position:relative;min-height:720px;background:radial-gradient(ellipse at 50% 36%,#29304b 0,#171d31 42%,#0c1020 78%);overflow:hidden}#view{position:absolute;inset:0;width:100%;height:100%;display:block}.stagehead{position:absolute;top:18px;left:20px;right:20px;display:flex;justify-content:space-between;gap:12px;pointer-events:none}.pill{background:#0b1020cc;border:1px solid #3b4560;border-radius:999px;padding:7px 11px;color:#dfe4f6;font-size:11px}.tip{position:absolute;bottom:18px;left:20px;color:#c0c8dd;font-size:12px;background:#0b1020bb;padding:9px 12px;border-radius:10px;max-width:75%}.actions{display:flex;gap:8px}.actions .btn{flex:1}.warn{color:#f1d3a2;font-size:11px;margin-top:9px}@media(max-width:800px){#app{grid-template-columns:1fr}.side{order:2;border-right:0;border-top:1px solid var(--line)}.stage{min-height:560px;order:1}}
</style></head><body><div id="app"><aside class="side">
<div class="brand">VTuberBot <span style="color:var(--pink)">✦</span></div><div class="sub">Taller de personaje · prototipo 3D</div>
<div class="group"><h3>01 · Silueta base</h3>
<label for="shape">Constitución</label><select id="shape"><option value="soft">Curvilínea suave</option><option value="slim">Delgada</option><option value="athletic">Atlética</option></select>
<label for="height">Altura</label><div class="rangeval"><span>Petite</span><span id="heightv">100%</span><span>Alta</span></div><input id="height" type="range" min="88" max="112" value="100">
<label for="skin">Tono de piel</label><input id="skin" type="color" value="#f3c6b0">
</div>
<div class="group"><h3>02 · Cara y ojos</h3>
<label for="haircolor">Color del cabello</label><input id="haircolor" type="color" value="#70452f">
<label for="eyes">Iris</label><input id="eyes" type="color" value="#75452f">
<label for="expression">Expresión</label><select id="expression"><option value="smile">Sonrisa suave</option><option value="neutral">Neutra</option><option value="happy">Feliz</option></select>
<div class="note">Pupilas blancas incluidas como rasgo del diseño base.</div></div>
<div class="group"><h3>03 · Peinados</h3>
<label for="hair">Estilo</label><select id="hair"><option value="shoulder">Media melena a los hombros</option><option value="wavy">Ondulado a los hombros</option><option value="ponytail">Media melena recogida</option><option value="twin">Coletas gemelas</option><option value="long">Largo liso</option><option value="bob">Bob corto</option></select>
<label for="bangs">Flequillo</label><select id="bangs"><option value="soft">Flequillo suave</option><option value="side">Flequillo lateral</option><option value="none">Sin flequillo</option></select>
</div>
<div class="group"><h3>04 · Ropa</h3>
<label for="outfit">Conjunto</label><select id="outfit"><option value="cardigan">Cárdigan + falda plisada</option><option value="hoodie">Hoodie + falda casual</option><option value="fantasy">Vestido aventurero</option><option value="uniform">Uniforme con chaqueta</option><option value="goth">Vestido gótico</option></select>
<label for="cloth">Color principal</label><input id="cloth" type="color" value="#7865ad">
<label for="accent">Color secundario</label><input id="accent" type="color" value="#f2d8e7">
<label for="accessory">Accesorios</label><select id="accessory"><option value="none">Sin accesorio</option><option value="ribbon">Lazo para el pelo</option><option value="headphones">Auriculares</option><option value="star">Horquillas estrella</option><option value="halo">Aro decorativo</option></select>
</div>
<div class="group"><h3>05 · Tu diseño</h3><div class="actions"><button class="btn" id="save">Guardar diseño JSON</button><button class="btn secondary" id="reset">Restablecer</button></div><button class="btn" id="exportglb">Exportar modelo 3D (.glb)</button>
<label class="btn secondary" for="loadfile" style="display:block;text-align:center">Cargar diseño guardado</label><input id="loadfile" type="file" accept=".json,application/json" hidden>
<div class="warn">El archivo GLB exporta la malla visible, pero no incluye esqueleto ni animaciones. No equivale a un VRM listo para emitir.</div></div>
</aside><main class="stage"><canvas id="view"></canvas><div class="stagehead"><span class="pill">VISTA 3D · ARRASTRÁ PARA GIRAR</span><span class="pill" id="renderstatus">Preparando modelo…</span></div><div class="tip">Personaje base editable · pelo marrón por defecto · ojos marrones y pupilas blancas. Cambiá las opciones para reconstruir el look.</div></main></div>
<script src="https://cdn.jsdelivr.net/npm/three@0.124.0/build/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.124.0/examples/js/controls/OrbitControls.js"></script><script src="https://cdn.jsdelivr.net/npm/three@0.124.0/examples/js/exporters/GLTFExporter.js"></script>
<script>
(()=> {
const $=id=>document.getElementById(id), canvas=$('view');
if(!window.THREE){$('renderstatus').textContent='No se pudo cargar Three.js (Internet requerida)';return;}
const scene=new THREE.Scene();scene.background=null;
const camera=new THREE.PerspectiveCamera(32,1,.1,100);camera.position.set(0,2.4,9.5);
const renderer=new THREE.WebGLRenderer({canvas,antialias:true,alpha:true});renderer.setPixelRatio(Math.min(devicePixelRatio||1,2));renderer.shadowMap.enabled=true;renderer.shadowMap.type=THREE.PCFSoftShadowMap;
renderer.outputEncoding=THREE.sRGBEncoding;renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.15;
scene.add(new THREE.HemisphereLight(0xe4e9ff,0x49364e,2.1));
const key=new THREE.DirectionalLight(0xffe4d6,3.2);key.position.set(-3,7,6);key.castShadow=true;scene.add(key);
const rim=new THREE.DirectionalLight(0xb7a5ff,2.4);rim.position.set(4,4,-4);scene.add(rim);
const fill=new THREE.PointLight(0xffa9d5,55,14);fill.position.set(2,2,4);scene.add(fill);
const floor=new THREE.Mesh(new THREE.CircleGeometry(3.5,64),new THREE.MeshStandardMaterial({color:0x252a42,roughness:.92,transparent:true,opacity:.7}));floor.rotation.x=-Math.PI/2;floor.position.y=-.05;floor.receiveShadow=true;scene.add(floor);
const controls=new THREE.OrbitControls(camera,renderer.domElement);controls.target.set(0,2.05,0);controls.enableDamping=true;controls.minDistance=6;controls.maxDistance=14;controls.maxPolarAngle=Math.PI*.78;
let avatar=new THREE.Group();scene.add(avatar);let turn=0;
const mat=(color,rough=.72,metal=0)=>new THREE.MeshStandardMaterial({color:new THREE.Color(color),roughness:rough,metalness:metal});
function mesh(g,m,parent=avatar,pos=[0,0,0],scale=[1,1,1]){let o=new THREE.Mesh(g,m);o.position.set(...pos);o.scale.set(...scale);o.castShadow=true;o.receiveShadow=true;parent.add(o);return o}
function ball(parent,m,pos,scale){return mesh(new THREE.SphereGeometry(1,32,24),m,parent,pos,scale)}
function cylinder(parent,m,rt,rb,h,pos,seg=32){return mesh(new THREE.CylinderGeometry(rt,rb,h,seg,1,false),m,parent,pos)}
function tube(parent,points,r,m){const curve=new THREE.CatmullRomCurve3(points.map(p=>new THREE.Vector3(...p)));return mesh(new THREE.TubeGeometry(curve,32,r,8,false),m,parent)}
function lathe(parent,points,m,pos=[0,0,0]){return mesh(new THREE.LatheGeometry(points.map(p=>new THREE.Vector2(p[0],p[1])),48),m,parent,pos)}
function limb(parent,m,a,b,r1,r2){const v1=new THREE.Vector3(...a),v2=new THREE.Vector3(...b),d=v2.clone().sub(v1);const o=mesh(new THREE.CylinderGeometry(r2,r1,d.length(),24),m,parent,v1.clone().add(v2).multiplyScalar(.5).toArray());o.quaternion.setFromUnitVectors(new THREE.Vector3(0,1,0),d.normalize());ball(parent,m,a,[r1,r1,r1]);ball(parent,m,b,[r2,r2,r2]);return o}
function make(){scene.remove(avatar);avatar=new THREE.Group();scene.add(avatar);
const skin=mat($('skin').value), hair=mat($('haircolor').value,.82), iris=mat($('eyes').value,.45), white=mat('#fffefc',.4), dark=mat('#30243b'), blush=mat('#ed9caa'), mouth=mat('#8e4b5c'), cloth=mat($('cloth').value), trim=mat($('accent').value), shoe=mat('#28263b'), scl=+$('height').value/100, shape=$('shape').value, outfit=$('outfit').value, hairtype=$('hair').value, bang=$('bangs').value, expr=$('expression').value, accessory=$('accessory').value;
avatar.scale.y=scl;
const narrow=shape==='slim'?.86:shape==='athletic'?.95:1, hip=shape==='slim'?.40:shape==='athletic'?.48:.56, waist=shape==='slim'?.29:shape==='athletic'?.32:.31;
// legs and boots
for(const side of [-1,1]){const x=side*.22*narrow;
limb(avatar,skin,[x,.30,0],[x,.92,0],.105,.12);
limb(avatar,mat('#e8cbd2'),[x,.91,0],[x,1.28,0],.115,.13);
ball(avatar,shoe,[x, .12,.055],[.145,.11,.25]);
if(outfit==='goth')ball(avatar,mat('#b89bff'),[x,.15,.19],[.035,.025,.025]);
}
// torso shape; lathe points trace ankles-to-neck and smooth waist/hips
const torsoPoints=[[.12,1.22],[.18,1.28],[.26,1.42],[hip,1.52],[hip*.97,1.68],[waist,1.91],[waist*1.05,2.06],[.39,2.27],[.37,2.43],[.27,2.57],[.17,2.62],[.12,2.64]];
lathe(avatar,torsoPoints,skin,[0,0,0]);
// neck and head
cylinder(avatar,skin,.105,.12,.25,[0,2.69,0]);
ball(avatar,skin,[0,3.10,0],[.40,.49,.34]);
const faceZ=.306;
// ears
for(const s of [-1,1])ball(avatar,skin,[s*.392,3.08,0],[.075,.12,.055]);
// eye sockets/eyes; white pupils are intentional defining feature
for(const s of [-1,1]){ball(avatar,mat('#f7e9e8'),[s*.15,3.17,faceZ],[.105,.115,.035]);ball(avatar,iris,[s*.15,3.165,faceZ+.029],[.067,.078,.025]);ball(avatar,white,[s*.15,3.16,faceZ+.05],[.032,.043,.014]);ball(avatar,white,[s*.175,3.195,faceZ+.052],[.014,.016,.008]);tube(avatar,[[s*.25,3.23,faceZ+.01],[s*.17,3.275,faceZ+.018],[s*.07,3.24,faceZ+.015]],.012,dark);
ball(avatar,blush,[s*.25,3.02,faceZ+.005],[.07,.027,.012]);}
ball(avatar,skin,[0,3.075,faceZ+.008],[.035,.045,.025]);
if(expr==='neutral')tube(avatar,[[-.07,2.94,faceZ+.025],[0,2.935,faceZ+.03],[.07,2.94,faceZ+.025]],.009,mouth);
else if(expr==='happy'){tube(avatar,[[-.09,2.96,faceZ+.025],[0,2.90,faceZ+.04],[.09,2.96,faceZ+.025]],.018,mouth);ball(avatar,mat('#fff2f1'),[0,2.925,faceZ+.04],[.045,.012,.01]);}
else tube(avatar,[[-.075,2.95,faceZ+.02],[0,2.925,faceZ+.035],[.075,2.95,faceZ+.02]],.014,mouth);
// Clothing upper torso: fitted tunic/cardi/hoodie. Geometry is separate so styling is truly switchable.
const coatPts=outfit==='hoodie'?[[.14,1.28],[.21,1.33],[.29,1.48],[hip+.025,1.57],[hip,1.75],[waist+.035,1.94],[.36,2.2],[.35,2.38],[.25,2.50],[.15,2.52]]:[[.14,1.30],[.22,1.35],[.30,1.49],[hip+.015,1.57],[hip*.98,1.72],[waist+.025,1.94],[.35,2.2],[.34,2.38],[.24,2.50],[.15,2.52]];
lathe(avatar,coatPts,cloth);
if(outfit==='cardigan'||outfit==='uniform'){cylinder(avatar,trim,.018,.018,1.12,[0,1.94,.345]);for(let y=1.72;y<2.35;y+=.15)ball(avatar,mat('#f8e7bd'),[.025,y,.36],[.018,.018,.01]);}
if(outfit==='hoodie'){ball(avatar,trim,[0,2.36,-.12],[.23,.25,.12]);tube(avatar,[[-.08,2.45,.32],[-.07,2.22,.36]],.012,trim);tube(avatar,[[.08,2.45,.32],[.07,2.22,.36]],.012,trim);}
if(outfit==='fantasy'){const belt=mat('#60412e');cylinder(avatar,belt,.33,.36,.07,[0,1.68,0]);ball(avatar,trim,[0,1.68,.36],[.055,.055,.025]);}
if(outfit==='goth'){const collar=mat('#201a31');cylinder(avatar,collar,.16,.22,.12,[0,2.48,0]);ball(avatar,trim,[0,2.30,.36],[.035,.06,.02]);}
// sleeves
for(const s of [-1,1]){const shoulder=[s*.32,2.35,0], elbow=[s*.48,2.03,.02], wrist=[s*.50,1.76,.06];limb(avatar,cloth,shoulder,elbow,.13,.105);limb(avatar,cloth,elbow,wrist,.105,.075);ball(avatar,skin,[s*.50,1.72,.07],[.07,.09,.065]);if(outfit==='uniform')cylinder(avatar,trim,.105,.10,.055,[s*.48,1.98,.02]);}
// skirt or tunic lower half
if(outfit==='fantasy'){const skirt=mesh(new THREE.CylinderGeometry(.35,.59,.60,48,1,false),cloth,avatar,[0,1.43,0]);skirt.scale.z=.78;for(let a=0;a<12;a++){const t=a*Math.PI*2/12;const p1=[Math.sin(t)*.35,1.71,Math.cos(t)*.28],p2=[Math.sin(t)*.56,1.17,Math.cos(t)*.43];tube(avatar,[p1,p2],.012,trim);}}
else {const length=outfit==='goth'?.70:.48;const skirt=mesh(new THREE.CylinderGeometry(.33,.51,length,48,1,false),cloth,avatar,[0,1.49-length*.5,0]);skirt.scale.z=.77;
for(let a=0;a<16;a++){const t=a*Math.PI*2/16;const r=.51; tube(avatar,[[Math.sin(t)*.32,1.49,.26*Math.cos(t)],[Math.sin(t)*r,1.49-length,Math.cos(t)*r*.77]],.008,trim);}
cylinder(avatar,trim,.35,.35,.035,[0,1.49,0]);}
if(outfit==='goth'){cylinder(avatar,trim,.30,.31,.025,[0,1.14,0]);}
// hair cap, back hair, fringe, and hairstyle-specific locks
ball(avatar,hair,[0,3.40,-.035],[.405,.30,.36]);
const backLength=hairtype==='long'?1.35:hairtype==='bob'?.38:.83;
for(const s of [-1,1]){const x=s*.25;const endY=3.39-backLength;const sidePts=[[s*.30,3.48,-.03],[s*.39,3.30,-.015],[s*.37,3.02,-.005],[s*.33,endY,.025]];
tube(avatar,sidePts,.09,hair);ball(avatar,hair,[s*.33,endY,.025],[.09,.12,.10]);}
if(hairtype==='ponytail'){tube(avatar,[[0,3.52,-.23],[.03,3.40,-.48],[.12,3.12,-.70],[.05,2.90,-.74]],.105,hair);ball(avatar,hair,[.05,2.89,-.74],[.10,.16,.10]);ball(avatar,trim,[0,3.43,-.30],[.13,.06,.09]);}
if(hairtype==='twin'){for(const s of [-1,1]){tube(avatar,[[s*.31,3.43,-.12],[s*.48,3.31,-.18],[s*.57,3.02,-.20],[s*.52,2.80,-.16]],.105,hair);ball(avatar,trim,[s*.37,3.36,-.12],[.10,.075,.08]);}}
if(hairtype==='wavy'){for(const s of [-1,1]){tube(avatar,[[s*.32,3.40,.0],[s*.40,3.23,.07],[s*.29,3.07,.08],[s*.38,2.92,.06],[s*.30,2.78,.05]],.075,hair);}}
if(hairtype==='long'){tube(avatar,[[-.22,3.35,-.13],[-.29,2.95,-.16],[-.25,2.52,-.16]],.09,hair);tube(avatar,[[.22,3.35,-.13],[.29,2.95,-.16],[.25,2.52,-.16]],.09,hair);}
if(bang!=='none'){for(let i=0;i<5;i++){const x=(i-2)*.12;const shift=bang==='side'?(i-2)*.06:0;const top=3.48-Math.abs(i-2)*.025;const end=3.17-Math.abs(i-2)*.02;const strand=mesh(new THREE.SphereGeometry(1,20,16),hair,avatar,[x+shift, (top+end)/2, .275],[.083,(top-end)/2+.055,.09]);strand.rotation.z=(i-2)*-.10;}}
else {tube(avatar,[[-.31,3.35,.20],[-.18,3.43,.28],[0,3.44,.30],[.18,3.43,.28],[.31,3.35,.20]],.035,hair);}
if(accessory==='ribbon'){ball(avatar,trim,[-.29,3.48,.03],[.12,.07,.04]);ball(avatar,trim,[-.42,3.47,.03],[.09,.045,.025]);}
if(accessory==='star'){for(const s of [-1,1]){const star=mesh(new THREE.OctahedronGeometry(.07),trim,avatar,[s*.31,3.38,.28]);star.scale.set(.8,1.2,.35);}}
if(accessory==='headphones'){for(const s of [-1,1]){ball(avatar,trim,[s*.42,3.15,0],[.075,.13,.075]);}tube(avatar,[[-.39,3.20,-.01],[-.34,3.50,-.02],[0,3.64,-.03],[.34,3.50,-.02],[.39,3.20,-.01]],.045,trim);}
if(accessory==='halo'){const ring=mesh(new THREE.TorusGeometry(.28,.025,12,48),trim,avatar,[0,3.86,0]);ring.rotation.x=Math.PI/2;}
// hair shine strands
for(const s of [-1,1])tube(avatar,[[s*.08,3.62,.20],[s*.16,3.57,.25],[s*.22,3.48,.26]],.012,mat('#d5a17d',.4));
avatar.traverse(o=>{if(o.isMesh){o.material.side=THREE.DoubleSide;}});$('renderstatus').textContent='Modelo 3D editable';}
const ids=['shape','height','skin','haircolor','eyes','expression','hair','bangs','outfit','cloth','accent','accessory'];
function update(){ $('heightv').textContent=$('height').value+'%';make();}
ids.forEach(id=>$(id).addEventListener('input',update));
$('reset').addEventListener('click',()=>{const defaults={shape:'soft',height:'100',skin:'#f3c6b0',haircolor:'#70452f',eyes:'#75452f',expression:'smile',hair:'shoulder',bangs:'soft',outfit:'cardigan',cloth:'#7865ad',accent:'#f2d8e7',accessory:'none'};for(const [k,v] of Object.entries(defaults))$(k).value=v;update();});
function settings(){return Object.fromEntries(ids.map(id=>[id,$(id).value]));}
$('save').addEventListener('click',()=>{const blob=new Blob([JSON.stringify({format:'vtuberbot-avatar-look',version:1,settings:settings()},null,2)],{type:'application/json'});const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download='mi-personaje-vtuberbot.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);});
$('exportglb').addEventListener('click',()=>{if(!THREE.GLTFExporter){$('renderstatus').textContent='No se pudo cargar el exportador GLB (Internet requerida)';return;} $('renderstatus').textContent='Exportando modelo 3D…';try{new THREE.GLTFExporter().parse(avatar,result=>{const blob=new Blob([result],{type:'model/gltf-binary'});const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download='mi-personaje-vtuberbot.glb';a.click();setTimeout(()=>URL.revokeObjectURL(url),1500);$('renderstatus').textContent='Modelo GLB exportado · sin rig ni animaciones';},error=>{$('renderstatus').textContent='Error al exportar GLB: '+(error?.message||'error desconocido');},{binary:true,onlyVisible:true});}catch(error){$('renderstatus').textContent='Error al exportar GLB: '+(error?.message||'error desconocido');}});
$('loadfile').addEventListener('change',async e=>{const file=e.target.files?.[0];if(!file)return;try{const data=JSON.parse(await file.text());const vals=data.settings||data;for(const id of ids)if(typeof vals[id]==='string')$(id).value=vals[id];update();}catch{$('renderstatus').textContent='Archivo JSON no válido';}e.target.value='';});
function resize(){const w=canvas.clientWidth,h=canvas.clientHeight;renderer.setSize(w,h,false);camera.aspect=w/h;camera.updateProjectionMatrix();}
new ResizeObserver(resize).observe(canvas);resize();make();
function animate(){requestAnimationFrame(animate);controls.update();renderer.render(scene,camera);}animate();
})();
</script></body></html>
""";
}
