using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using System.IO;
using System.Linq;
using Env = Android.OS.Environment;
using System.Threading.Tasks;
using System;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX Fafnir V10 VIDEO", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape)]
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
            root.AddView(new TextView(this){ Text="DragoNX Fafnir V10 - VIDEO\n", TextSize=18f });
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
                    var btn = new Button(this){ Text = "▶ "+Path.GetFileName(f)+" [V10 VIDEO COR]" };
                    btn.Click += (s,e) => { var i = new global::Android.Content.Intent(this, typeof(GameActivity)); i.PutExtra("gamePath", f); i.PutExtra("basePath", basePath); StartActivity(i); };
                    lista.AddView(btn);
                }
            } catch (Exception ex) { lista.AddView(new TextView(this){ Text="Erro: "+ex.Message }); }
        }
    }

    [Activity(Label = "DragoNX Game V10", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenSize)]
    public class GameActivity : Activity, ISurfaceHolderCallback
    {
        TextView log = null!; SurfaceView surfaceView = null!;
        Ryujinx.HLE.Switch device = null!; bool surfaceReady = false;
        string gamePath = "", basePath = "";

        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            gamePath = Intent.GetStringExtra("gamePath")!;
            basePath = Intent.GetStringExtra("basePath")!;
            var layout = new LinearLayout(this){ Orientation = Orientation.Vertical };
            log = new TextView(this){ TextSize=8f, Text=$"Fafnir V10 - TENTANDO VIDEO\n{Path.GetFileName(gamePath)}\nAguardando Vulkan...\n" };
            surfaceView = new SurfaceView(this);
            surfaceView.Holder.AddCallback(this);
            var lp = new LinearLayout.LayoutParams(-1, 0); lp.Weight = 1;
            surfaceView.LayoutParameters = lp;
            surfaceView.SetBackgroundColor(global::Android.Graphics.Color.Black);
            var btnVoltar = new Button(this){ Text="VOLTAR" };
            btnVoltar.Click += (s,e) => { try{ device?.Stop(); device?.Dispose(); } catch{} Finish(); };
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
                    AddLog($"[3/5] Criando VFS...");

                    var vfsType = typeof(VirtualFileSystem);
                    var vfs = Activator.CreateInstance(vfsType) as VirtualFileSystem;
                    if(vfs == null) { AddLog("ERRO: VFS null"); return; }
                    AddLog($"> VFS: {vfs.GetType().Name}");

                    AddLog($"[4/5] Criando Switch HOS...");
                    device = new Ryujinx.HLE.Switch(vfs, null, null, null, null, null, null, null);
                    AddLog($"> Switch OK!");

                    AddLog($"[5/5] LoadApplication...");
                    bool loaded = device.LoadApplication(gamePath);
                    AddLog($">> Load: {loaded}");

                    if(loaded)
                    {
                        AddLog($">> BOOTANDO VIDEO COM CORES!");
                        RunOnUiThread(() => { log.Visibility = ViewStates.Gone; });
                        device.Run();
                    }
                    else
                    {
                        AddLog($"FALHA LOAD");
                    }
                } catch (Exception ex) {
                    AddLog($"ERRO V10: {ex.Message}\n{ex.StackTrace?.Substring(0,1200)}");
                }
            });
        }

        public void SurfaceCreated(ISurfaceHolder holder) { AddLog("SurfaceCreated - Vulkan OK!"); surfaceReady = true; TryBoot(); }
        public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format, int w, int h) { AddLog($"SurfaceChanged {w}x{h}"); }
        public void SurfaceDestroyed(ISurfaceHolder holder) { surfaceReady = false; try{ device?.Stop(); } catch{} }
    }
}
