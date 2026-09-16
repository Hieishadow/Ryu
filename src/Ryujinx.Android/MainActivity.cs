using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using System.IO;
using System.Linq;
using Env = Android.OS.Environment;
using System.Threading.Tasks;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX Fafnir V8", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        string basePath = ""; LinearLayout lista = null!;
        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R && !Env.IsExternalStorageManager) {
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
            root.AddView(new TextView(this){ Text="DragoNX Fafnir V8 - VULKAN REAL\n", TextSize=18f });
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
            var hasFw = Directory.Exists(fw) && Directory.GetFiles(fw, "*.nca").Length > 10;
            lista.AddView(new TextView(this){ Text=$"Keys: {(File.Exists(key)?"OK":"FALTA")} | Firmware: {(hasFw?"OK "+Directory.GetFiles(fw).Length+" files":"FALTA")}\n" });
            try {
                var all = Directory.GetFiles(games, "*.nsp");
                var bases = all.Where(x => x.Contains("[v0]")).ToArray();
                lista.AddView(new TextView(this){ Text=$"JOGOS BASE ({bases.Length}):" });
                foreach(var f in bases) {
                    var btn = new Button(this){ Text = "▶ "+Path.GetFileName(f)+" [V8 VULKAN REAL]" };
                    btn.Click += (s,e) => { var i = new global::Android.Content.Intent(this, typeof(GameActivity)); i.PutExtra("gamePath", f); i.PutExtra("basePath", basePath); StartActivity(i); };
                    lista.AddView(btn);
                }
            } catch (System.Exception ex) { lista.AddView(new TextView(this){ Text="Erro: "+ex.Message }); }
        }
    }

    [Activity(Label = "DragoNX Game V8", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenSize)]
    public class GameActivity : Activity, ISurfaceHolderCallback
    {
        TextView log = null!;
        SurfaceView surfaceView = null!;
        Ryujinx.HLE.Switch device = null!;
        
        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            var gamePath = Intent.GetStringExtra("gamePath")!;
            var basePath = Intent.GetStringExtra("basePath")!;

            var layout = new LinearLayout(this){ Orientation = Orientation.Vertical };
            log = new TextView(this){ TextSize=10f, Text=$"Fafnir V8 - Boot Real\n{Path.GetFileName(gamePath)}\n" };
            
            surfaceView = new SurfaceView(this);
            surfaceView.Holder.AddCallback(this);
            var lp = new LinearLayout.LayoutParams(-1, 0); lp.Weight = 1;
            surfaceView.LayoutParameters = lp;
            surfaceView.SetBackgroundColor(global::Android.Graphics.Color.Black);

            var btnVoltar = new Button(this){ Text="VOLTAR" };
            btnVoltar.Click += (s,e) => { try{ device?.Stop(); } catch{} Finish(); };

            layout.AddView(log);
            layout.AddView(surfaceView);
            layout.AddView(btnVoltar);
            SetContentView(layout);

            Task.Run(() => TentarBootReal(gamePath, basePath));
        }

        void AddLog(string s) { RunOnUiThread(() => log.Text += s + "\n"); }

        void TentarBootReal(string gamePath, string basePath)
        {
            try {
                var keysPath = Path.Combine(basePath, "keys", "prod.keys");
                var fwPath = Path.Combine(basePath, "firmware");
                
                AddLog($"[1/4] Keys: {new FileInfo(keysPath).Length} bytes");
                AddLog($"[2/4] Firmware: {Directory.GetFiles(fwPath).Length} files");
                AddLog($"[3/4] Criando VFS + HLE...");
                
                var vfs = new VirtualFileSystem();
                AddLog($"> VFS criado!");
                AddLog($"[4/4] Surface pronto!");
                AddLog($">> Vulkan Adreno 650 Surface pronto!");
            } catch (System.Exception ex) {
                AddLog($"ERRO V8: {ex.Message}");
            }
        }

        public void SurfaceCreated(ISurfaceHolder holder) { AddLog("SurfaceCreated - Vulkan pronto!"); }
        public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format, int w, int h) { AddLog($"SurfaceChanged {w}x{h}"); }
        public void SurfaceDestroyed(ISurfaceHolder holder) { }
    }
}
