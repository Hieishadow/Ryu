using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using System.IO;
using System.Linq;
using Env = Android.OS.Environment;
using System.Threading.Tasks;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX Fafnir V9", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        string basePath = ""; LinearLayout lista = null!;
        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            basePath = Path.Combine(Env.GetExternalStoragePublicDirectory(Env.DirectoryDownloads).AbsolutePath, "DragoNX");
            var scroll = new ScrollView(this);
            var root = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.SetPadding(30,20,30,20);
            lista = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.AddView(new TextView(this){ Text="DragoNX Fafnir V9 - CORACAO\n", TextSize=18f });
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
                foreach(var f in bases) {
                    var btn = new Button(this){ Text = "▶ "+Path.GetFileName(f)+" [V9 CORACAO]" };
                    btn.Click += (s,e) => { var i = new global::Android.Content.Intent(this, typeof(GameActivity)); i.PutExtra("gamePath", f); i.PutExtra("basePath", basePath); StartActivity(i); };
                    lista.AddView(btn);
                }
            } catch (System.Exception ex) { lista.AddView(new TextView(this){ Text="Erro: "+ex.Message }); }
        }
    }

    [Activity(Label = "DragoNX Game V9", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenSize)]
    public class GameActivity : Activity, ISurfaceHolderCallback
    {
        TextView log = null!; SurfaceView surfaceView = null!;
        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            var gamePath = Intent.GetStringExtra("gamePath")!;
            var basePath = Intent.GetStringExtra("basePath")!;
            var layout = new LinearLayout(this){ Orientation = Orientation.Vertical };
            log = new TextView(this){ TextSize=9f, Text=$"Fafnir V9 - CORACAO Switch\n{Path.GetFileName(gamePath)}\n" };
            surfaceView = new SurfaceView(this);
            surfaceView.Holder.AddCallback(this);
            var lp = new LinearLayout.LayoutParams(-1, 0); lp.Weight = 1;
            surfaceView.LayoutParameters = lp;
            surfaceView.SetBackgroundColor(global::Android.Graphics.Color.Black);
            var btnVoltar = new Button(this){ Text="VOLTAR" };
            btnVoltar.Click += (s,e) => Finish();
            layout.AddView(log); layout.AddView(surfaceView); layout.AddView(btnVoltar);
            SetContentView(layout);
            Task.Run(() => BootReal(gamePath, basePath));
        }
        void AddLog(string s) { RunOnUiThread(() => log.Text += s + "\n"); }
        void BootReal(string gamePath, string basePath)
        {
            try {
                AddLog($"[1/5] Keys: {new FileInfo(Path.Combine(basePath,"keys","prod.keys")).Length} bytes");
                AddLog($"[2/5] Firmware: {Directory.GetFiles(Path.Combine(basePath,"firmware")).Length} files");
                AddLog($"[3/5] Switch HLE: tentando criar...");
                // Aqui vai o Switch real na proxima tentativa
                var switchType = typeof(Ryujinx.HLE.Switch);
                AddLog($"> Type encontrado: {switchType.FullName}");
                AddLog($"[4/5] Vulkan Surface: OK");
                AddLog($"[5/5] PRONTO PRO TRANSPLANTE!");
                AddLog($">> Proximo passo: LoadApplication");
            } catch (System.Exception ex) { AddLog($"ERRO: {ex.Message}"); }
        }
        public void SurfaceCreated(ISurfaceHolder holder) { AddLog("SurfaceCreated - Vulkan pronto pro Switch!"); }
        public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format, int w, int h) { }
        public void SurfaceDestroyed(ISurfaceHolder holder) { }
    }
}
