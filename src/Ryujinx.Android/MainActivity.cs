using Android.App;
using Android.OS;
using Android.Widget;
using System.IO;
using Android.Content;
using System.Linq;
using Env = Android.OS.Environment;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX Fafnir V6", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        string basePath = ""; LinearLayout lista = null!;
        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R && !Env.IsExternalStorageManager) {
                try {
                    var i = new Intent(global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                    i.SetData(global::Android.Net.Uri.Parse("package:"+PackageName));
                    StartActivity(i);
                } catch { StartActivity(new Intent(global::Android.Provider.Settings.ActionManageAllFilesAccessPermission)); }
            }
            basePath = Path.Combine(Env.GetExternalStoragePublicDirectory(Env.DirectoryDownloads).AbsolutePath, "DragoNX");
            var scroll = new ScrollView(this);
            var root = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.SetPadding(30,20,30,20);
            lista = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.AddView(new TextView(this){ Text="DragoNX Fafnir V6 VIDEO\n", TextSize=18f });
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
            var hasFw = Directory.Exists(fw) && Directory.GetFiles(fw).Length > 0;
            
            lista.AddView(new TextView(this){ Text=$"Keys: {(File.Exists(key)?"OK":"FALTA")} | Firmware: {(hasFw?"OK":"FALTA - colocar em Download/DragoNX/firmware/")}\n" });

            // Botão firmware pra depois
            var btnFw = new Button(this){ Text = hasFw ? "FIRMWARE OK" : "FIRMWARE FALTANDO (vamos add na V7)" };
            btnFw.Enabled = false;
            lista.AddView(btnFw);

            try {
                var all = Directory.GetFiles(games, "*.nsp");
                var bases = all.Where(x => x.Contains("[v0]")).ToArray(); // só jogo base
                var updates = all.Where(x => !x.Contains("[v0]") && !x.ToLower().Contains("dlc")).ToArray();
                var dlcs = all.Where(x => x.ToLower().Contains("dlc")).ToArray();

                lista.AddView(new TextView(this){ Text=$"\nJOGOS BASE ({bases.Length}):" });
                foreach(var f in bases) {
                    var btn = new Button(this){ Text = "▶ "+Path.GetFileName(f) };
                    btn.Click += (s,e) => Bootar(f);
                    lista.AddView(btn);
                }
                
                lista.AddView(new TextView(this){ Text=$"\nUpdates/DLC ({updates.Length+dlcs.Length}) - ignorar por enquanto:" });
                foreach(var f in updates.Concat(dlcs)) {
                    lista.AddView(new TextView(this){ Text=" - "+Path.GetFileName(f), TextSize=11f });
                }

            } catch (System.Exception ex) { lista.AddView(new TextView(this){ Text="Erro: "+ex.Message }); }
        }

        void Bootar(string path) {
            var i = new Intent(this, typeof(GameActivity));
            i.PutExtra("gamePath", path);
            i.PutExtra("basePath", basePath);
            StartActivity(i);
        }
    }

    [Activity(Label = "Game", ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
    public class GameActivity : Activity
    {
        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            var game = Intent.GetStringExtra("gamePath");
            var basePath = Intent.GetStringExtra("basePath");
            var layout = new LinearLayout(this){ Orientation = Orientation.Vertical };
            layout.SetPadding(30,30,30,30);
            layout.AddView(new TextView(this){ Text=$"Fafnir V6 Bootando\n{Path.GetFileName(game)}\n\nBasePath: {basePath}\n\nIniciando Ryujinx HLE...", TextSize=14f });
            var btn = new Button(this){ Text="VOLTAR" };
            btn.Click += (s,e) => Finish();
            layout.AddView(btn);
            SetContentView(layout);
            // Aqui na #39 a gente linka o Ryujinx.HLE + Vulkan de verdade
        }
    }
}
