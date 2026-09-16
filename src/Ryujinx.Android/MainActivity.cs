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
using Ryujinx.HLE.FileSystem;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX Fafnir V10.2", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape)]
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
            root.AddView(new TextView(this){ Text="DragoNX Fafnir V10.2 VIDEO\n", TextSize=18f });
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
                lista.AddView(new TextView(this){ Text=$"JOGOS BASE ({bases.Length}) - CLICA PRA VIDEO:" });
                foreach(var f in bases) {
                    var btn = new Button(this){ Text = "▶ "+Path.GetFileName(f)+" [V10.2 VIDEO]" };
                    btn.Click += (s,e) => { var i = new global::Android.Content.Intent(this, typeof(GameActivity)); i.PutExtra("gamePath", f); i.PutExtra("basePath", basePath); StartActivity(i); };
                    lista.AddView(btn);
                }
            } catch (Exception ex) { lista.AddView(new TextView(this){ Text="Erro: "+ex.Message }); }
        }
    }

    [Activity(Label = "DragoNX Game V10.2", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenSize)]
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
            log = new TextView(this){ TextSize=8f, Text=$"Fafnir V10.2 - VIDEO REFLECTION\n{Path.GetFileName(gamePath)}\nAguardando Vulkan...\n" };
            surfaceView = new SurfaceView(this);
            surfaceView.Holder.AddCallback(this);
            var lp = new LinearLayout.LayoutParams(-1, 0); lp.Weight = 1;
            surfaceView.LayoutParameters = lp;
            surfaceView.SetBackgroundColor(global::Android.Graphics.Color.Black);
            var btnVoltar = new Button(this){ Text="VOLTAR" };
            btnVoltar.Click += (s,e) => { try{ var m=device?.GetType().GetMethod("Stop"); m?.Invoke(device,null); } catch{} Finish(); };
            layout.AddView(log); layout.AddView(surfaceView); layout.AddView(btnVoltar);
            SetContentView(layout);
        }

        void AddLog(string s) { RunOnUiThread(() => log.Text += s + "\n"); }

        void TryBoot()
        {
            if(!surfaceReady) return;
            Task.Run(() => {
                try {
                    AddLog($"[1/5] Keys: {new FileInfo(Path.Combine(basePath,"keys","prod.keys")).Length} bytes");
                    AddLog($"[2/5] Firmware: {Directory.GetFiles(Path.Combine(basePath,"firmware"),"*",SearchOption.AllDirectories).Length} files");

                    AddLog($"[3/5] Investigando Ryujinx.HLE.Switch...");
                    var switchType = typeof(Ryujinx.HLE.Switch);
                    AddLog($"> Type: {switchType.FullName}");

                    var ctors = switchType.GetConstructors();
                    AddLog($"> Construtores: {ctors.Length}");
                    foreach(var c in ctors) {
                        AddLog($">> ctor({string.Join(", ", c.GetParameters().Select(p=>p.ParameterType.Name+" "+p.Name))})");
                    }

                    var methods = switchType.GetMethods(BindingFlags.Public|BindingFlags.Instance).Where(m=>!m.IsSpecialName).Select(m=>m.Name).Distinct().OrderBy(x=>x).ToArray();
                    AddLog($"> Metodos ({methods.Length}): {string.Join(", ", methods.Take(20))}...");

                    AddLog($"[4/5] Criando VFS...");
                    var vfs = Activator.CreateInstance(typeof(VirtualFileSystem)) as VirtualFileSystem;
                    AddLog($"> VFS: OK");

                    AddLog($"[5/5] Criando Switch via reflection...");
                    // Tenta criar com o primeiro construtor que achar
                    var ctor = ctors.FirstOrDefault();
                    if(ctor!= null) {
                        var ps = ctor.GetParameters();
                        object[] args = new object[ps.Length];
                        for(int i=0;i<ps.Length;i++) {
                            if(ps[i].ParameterType == typeof(VirtualFileSystem)) args[i]=vfs;
                            else if(ps[i].ParameterType.IsValueType) args[i]=Activator.CreateInstance(ps[i].ParameterType);
                            else args[i]=null;
                        }
                        device = ctor.Invoke(args);
                        AddLog($">> Switch criado! Type: {device.GetType().Name}");

                        // Tenta LoadApplication via reflection
                        var loadM = switchType.GetMethod("LoadApplication")?? switchType.GetMethod("Load")?? switchType.GetMethods().FirstOrDefault(m=>m.Name.Contains("Load"));
                        if(loadM!= null) {
                            AddLog($">> Achou metodo: {loadM.Name}({string.Join(",", loadM.GetParameters().Select(p=>p.ParameterType.Name))})");
                            var result = loadM.Invoke(device, new object[]{ gamePath });
                            AddLog($">> Load result: {result}");

                            var runM = switchType.GetMethod("Run")?? switchType.GetMethod("Start")?? switchType.GetMethods().FirstOrDefault(m=>m.Name.Contains("Run"));
                            if(runM!= null) {
                                AddLog($">> BOOTANDO VIDEO! Metodo: {runM.Name}");
                                RunOnUiThread(() => { log.Visibility = ViewStates.Gone; });
                                runM.Invoke(device, null);
                            } else {
                                AddLog($">> Sem metodo Run/Start - lista metodos acima");
                            }
                        } else {
                            AddLog($">> Nao achou LoadApplication - veja lista");
                        }
                    }

                } catch (Exception ex) {
                    AddLog($"ERRO V10.2: {ex.InnerException?.Message?? ex.Message}\n{ex.StackTrace?.Substring(0,1000)}");
                }
            });
        }

        public void SurfaceCreated(ISurfaceHolder holder) { AddLog("SurfaceCreated - Vulkan OK! Investigando..."); surfaceReady = true; TryBoot(); }
        public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format, int w, int h) { }
        public void SurfaceDestroyed(ISurfaceHolder holder) { surfaceReady = false; }
    }
}
