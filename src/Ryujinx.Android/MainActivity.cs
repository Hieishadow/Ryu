using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using System.IO;
using System.Linq;
using Env = Android.OS.Environment;
using System.Threading.Tasks;
using System;
using System.Reflection;
using Ryujinx.HLE;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX Fafnir V11 VIDEO", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        string basePath = ""; LinearLayout lista = null!;
        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R &&!Env.IsExternalStorageManager) {
                try {
                    var i = new global::Android.Content.Intent(global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                    i.SetData(global::Android.Net.Uri.Parse("package:"+PackageName));
                    StartActivity(i);
                } catch { StartActivity(new global::Android.Content.Intent(global::Android.Provider.Settings.ActionManageAllFilesAccessPermission)); }
            }
            basePath = Path.Combine(Env.GetExternalStoragePublicDirectory(Env.DirectoryDownloads).AbsolutePath, "DragoNX");
            var scroll = new ScrollView(this);
            var root = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.SetPadding(30,20,30,20);
            lista = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.AddView(new TextView(this){ Text="DragoNX Fafnir V11 - VIDEO FINAL\n", TextSize=18f });
            root.AddView(lista);
            scroll.AddView(root);
            SetContentView(scroll);
            Carregar();
        }
        void Carregar() {
            lista.RemoveAllViews();
            var games = Path.Combine(basePath, "games");
            var fw = Path.Combine(basePath, "firmware");
            var key = Path.Combine(basePath, "keys", "prod.keys");
            int fwCount = Directory.Exists(fw)? Directory.GetFiles(fw,"*",SearchOption.AllDirectories).Length : 0;
            lista.AddView(new TextView(this){ Text=$"Keys: {(File.Exists(key)?"OK":"FALTA")} | Firmware: {(fwCount>10?$"OK {fwCount}":"FALTA")}\n" });
            try {
                var bases = Directory.GetFiles(games, "*.nsp").Where(x => x.Contains("[v0]")).ToArray();
                lista.AddView(new TextView(this){ Text=$"JOGOS BASE ({bases.Length}) - VIDEO FINAL:" });
                foreach(var f in bases) {
                    var btn = new Button(this){ Text = "🌈 "+Path.GetFileName(f)+" [V11 COR]" };
                    btn.Click += (s,e) => { var i = new global::Android.Content.Intent(this, typeof(GameActivity)); i.PutExtra("gamePath", f); i.PutExtra("basePath", basePath); StartActivity(i); };
                    lista.AddView(btn);
                }
            } catch (Exception ex) { lista.AddView(new TextView(this){ Text="Erro: "+ex.Message }); }
        }
    }

    [Activity(Label = "DragoNX Game V11", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenSize)]
    public class GameActivity : Activity, ISurfaceHolderCallback
    {
        TextView log = null!; SurfaceView surfaceView = null!;
        object device = null!; bool surfaceReady = false;
        string gamePath = "", basePath = "";

        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            gamePath = Intent.GetStringExtra("gamePath")!;
            basePath = Intent.GetStringExtra("basePath")!;
            var layout = new LinearLayout(this){ Orientation = Orientation.Vertical };
            log = new TextView(this){ TextSize=8f, Text=$"Fafnir V11 - VIDEO FINAL COR\n{Path.GetFileName(gamePath)}\nAguardando Vulkan...\n" };
            surfaceView = new SurfaceView(this);
            surfaceView.Holder.AddCallback(this);
            var lp = new LinearLayout.LayoutParams(-1, 0); lp.Weight = 1;
            surfaceView.LayoutParameters = lp;
            surfaceView.SetBackgroundColor(global::Android.Graphics.Color.Black);
            var btnVoltar = new Button(this){ Text="VOLTAR" };
            btnVoltar.Click += (s,e) => { try{ device?.GetType().GetMethod("Dispose")?.Invoke(device,null); } catch{} Finish(); };
            layout.AddView(log); layout.AddView(surfaceView); layout.AddView(btnVoltar);
            SetContentView(layout);
        }

        void AddLog(string s) { RunOnUiThread(() => log.Text += s + "\n"); }

        void TryBoot()
        {
            if(!surfaceReady) return;
            Task.Run(() => {
                try {
                    AddLog($"[1/4] Keys: {new FileInfo(Path.Combine(basePath,"keys","prod.keys")).Length} bytes");
                    AddLog($"[2/4] Firmware: {Directory.GetFiles(Path.Combine(basePath,"firmware"),"*",SearchOption.AllDirectories).Length} files");
                    AddLog($"[3/4] Criando HleConfiguration...");

                    // Descobre HleConfiguration
                    var hleConfigType = typeof(Ryujinx.HLE.Switch).Assembly.GetTypes().FirstOrDefault(t=>t.Name=="HleConfiguration");
                    if(hleConfigType==null) { AddLog("ERRO: HleConfiguration nao achado"); return; }
                    AddLog($"> HleConfiguration Type: {hleConfigType.FullName}");

                    var hleCtors = hleConfigType.GetConstructors();
                    foreach(var c in hleCtors) {
                        AddLog($">> HleConfig ctor({string.Join(", ", c.GetParameters().Select(p=>p.ParameterType.Name+" "+p.Name))})");
                    }

                    // Tenta criar HleConfiguration - construtor mais simples
                    object hleConfig = null!;
                    var ctor0 = hleCtors.OrderBy(c=>c.GetParameters().Length).FirstOrDefault();
                    if(ctor0!=null) {
                        var ps = ctor0.GetParameters();
                        object[] args = new object[ps.Length];
                        for(int i=0;i<ps.Length;i++) {
                            var pt = ps[i].ParameterType;
                            if(pt.Name.Contains("VirtualFileSystem") || pt.Name=="VirtualFileSystem") {
                                var vfsType = pt;
                                var vfs = Activator.CreateInstance(vfsType);
                                args[i]=vfs;
                                AddLog($">> VFS criado pro HleConfig");
                            } else if(pt.IsValueType) {
                                args[i]=Activator.CreateInstance(pt);
                            } else {
                                args[i]=null;
                            }
                        }
                        hleConfig = ctor0.Invoke(args);
                        AddLog($">> HleConfig criado!");
                    }

                    AddLog($"[4/4] Criando Switch(HleConfiguration)...");
                    var switchType = typeof(Ryujinx.HLE.Switch);
                    device = Activator.CreateInstance(switchType, new object[]{ hleConfig });
                    AddLog($">> Switch OK!");

                    AddLog($"[5/5] LoadNsp: {Path.GetFileName(gamePath)}");
                    var loadNsp = switchType.GetMethod("LoadNsp");
                    var result = loadNsp.Invoke(device, new object[]{ gamePath });
                    AddLog($">> LoadNsp result: {result}");

                    // Se carregou, tenta loop de video
                    AddLog($">> Iniciando LOOP VIDEO COLORIDO!");
                    AddLog($">> PresentFrame / ProcessFrame no Surface...");
                    RunOnUiThread(() => { log.Visibility = ViewStates.Gone; });

                    // Loop basico de render
                    var processFrame = switchType.GetMethod("ProcessFrame");
                    var presentFrame = switchType.GetMethod("PresentFrame");
                    var consume = switchType.GetMethod("ConsumeFrameAvailable");

                    while(true) {
                        try {
                            bool hasFrame = false;
                            if(consume!=null) {
                                var r = consume.Invoke(device, null);
                                if(r is bool b) hasFrame = b;
                            } else hasFrame = true;

                            if(hasFrame) {
                                processFrame?.Invoke(device, null);
                                presentFrame?.Invoke(device, null);
                            }
                            System.Threading.Thread.Sleep(16); // ~60fps
                        } catch(Exception exLoop) {
                            AddLog($"Loop erro: {exLoop.InnerException?.Message}");
                            break;
                        }
                    }

                } catch (Exception ex) {
                    AddLog($"ERRO V11: {ex.InnerException?.Message?? ex.Message}\n{ex.InnerException?.StackTrace?.Substring(0,800)}\n{ex.StackTrace?.Substring(0,800)}");
                }
            });
        }

        public void SurfaceCreated(ISurfaceHolder holder) { AddLog("SurfaceCreated - Vulkan OK! Bootando V11..."); surfaceReady = true; TryBoot(); }
        public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format, int w, int h) { }
        public void SurfaceDestroyed(ISurfaceHolder holder) { surfaceReady = false; }
    }
}
