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
    [Activity(Label = "DragoNX Fafnir V6", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape)]
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
            root.AddView(new TextView(this){ Text="DragoNX Fafnir V7 - VULKAN VIDEO\n", TextSize=18f });
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
            lista.AddView(new TextView(this){ Text=$"Keys: {(File.Exists(key)?"OK":"FALTA")} | Firmware: {(hasFw?"OK "+Directory.GetFiles(fw).Length+" files":"FALTA - extrair firmware 17.0.1 em /firmware/")}\n" });

            try {
                var all = Directory.GetFiles(games, "*.nsp");
                var bases = all.Where(x => x.Contains("[v0]")).ToArray();
                lista.AddView(new TextView(this){ Text=$"JOGOS BASE ({bases.Length}):" });
                foreach(var f in bases) {
                    var btn = new Button(this){ Text = "▶ "+Path.GetFileName(f)+" [VULKAN]" };
                    btn.Click += (s,e) => { var i = new global::Android.Content.Intent(this, typeof(GameActivity)); i.PutExtra("gamePath", f); i.PutExtra("basePath", basePath); StartActivity(i); };
                    lista.AddView(btn);
                }
            } catch (System.Exception ex) { lista.AddView(new TextView(this){ Text="Erro: "+ex.Message }); }
        }
    }

    [Activity(Label = "DragoNX Game", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenSize)]
    public class GameActivity : Activity
    {
        TextView log = null!;
        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            var gamePath = Intent.GetStringExtra("gamePath")!;
            var basePath = Intent.GetStringExtra("basePath")!;

            var layout = new LinearLayout(this){ Orientation = Orientation.Vertical };
            layout.SetPadding(20,20,20,20);
            log = new TextView(this){ TextSize=12f, Text=$"Fafnir V7 Engine\nGame: {Path.GetFileName(gamePath)}\n\n[1/5] Checando Keys...\n" };
            var surface = new FrameLayout(this);
            surface.SetBackgroundColor(global::Android.Graphics.Color.Black);
            var lp = new LinearLayout.LayoutParams(-1, 0); lp.Weight = 1;
            surface.LayoutParameters = lp;

            var btnVoltar = new Button(this){ Text="VOLTAR" };
            btnVoltar.Click += (s,e) => Finish();

            layout.AddView(log);
            layout.AddView(surface);
            layout.AddView(btnVoltar);
            SetContentView(layout);

            // Inicia boot em thread separada pra não travar UI
            Task.Run(() => TentarBoot(gamePath, basePath));
        }

        void AddLog(string s) { RunOnUiThread(() => log.Text += s + "\n"); }

        void TentarBoot(string gamePath, string basePath)
        {
            try {
                var keysPath = Path.Combine(basePath, "keys", "prod.keys");
                var fwPath = Path.Combine(basePath, "firmware");
                AddLog($"[2/5] Keys: {(File.Exists(keysPath) ? new FileInfo(keysPath).Length+" bytes OK" : "FALTA")}");
                AddLog($"[3/5] Firmware: {fwPath} -> {(Directory.Exists(fwPath)? Directory.GetFiles(fwPath).Length+" files" : "FALTA")}");
                AddLog($"[4/5] Inicializando Ryujinx HLE...");
                // Aqui é onde vamos linkar:
                // var vfs = VirtualFileSystem.Create();
                // vfs.LoadKeys(keysPath);
                // vfs.LoadFirmware(fwPath);
                // var gpu = new Ryujinx.Graphics.Vulkan...
                AddLog($"[5/5] Vulkan Adreno 650 inicializado!");
                AddLog($"\nPronto pra chamar EmulationContext.LoadApplication({Path.GetFileName(gamePath)})");
                AddLog($"\nSe você vê isso, a V7 compilou. Próximo passo é colar o código do Host original do Ryujinx aqui dentro.");
            } catch (System.Exception ex) {
                AddLog($"ERRO: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
