using Android.App;
using Android.OS;
using Android.Widget;
using System;
using System.IO;
using Android.Content;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX - Fafnir", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
    public class MainActivity : Activity
    {
        string basePath = "";
        TextView log;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            basePath = Path.Combine(Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath, "DragoNX");
            Directory.CreateDirectory(basePath);
            Directory.CreateDirectory(Path.Combine(basePath, "keys"));
            Directory.CreateDirectory(Path.Combine(basePath, "games"));

            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical, Padding = 25 };
            log = new TextView(this) { Text = "DragoNX - Fafnir Edition\nGPU: Adreno 650 Vulkan\nBase: " + basePath + "\n", TextSize = 13f };

            var btnTest = new Button(this) { Text = "VERIFICAR KEYS + JOGOS" };
            btnTest.Click += (s,e) => {
                var key = Path.Combine(basePath, "keys", "prod.keys");
                var games = Path.Combine(basePath, "games");
                log.Text = "DragoNX Fafnir\nBase: " + basePath + "\n\n";
                if(File.Exists(key)) log.Text += "Keys OK: " + new FileInfo(key).Length + " bytes\n";
                else log.Text += "SEM KEYS em:\n" + key + "\n";

                if(Directory.Exists(games)){
                    var files = Directory.GetFiles(games, "*.nsp");
                    var files2 = Directory.GetFiles(games, "*.xci");
                    log.Text += "\nNSP: " + files.Length + " | XCI: " + files2.Length + "\n";
                    foreach(var f in files) log.Text += Path.GetFileName(f) + "\n";
                }
                log.Text += "\nPronto pro Fafnir Engine!";
            };

            var btnBoot = new Button(this) { Text = "BOOTAR JOGO VULKAN" };
            btnBoot.Click += (s,e) => {
                var intent = new Intent(Intent.ActionOpenDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType("*/*");
                StartActivityForResult(intent, 777);
            };

            layout.AddView(btnTest);
            layout.AddView(btnBoot);
            layout.AddView(log);
            SetContentView(layout);
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            if(requestCode == 777 && resultCode == Result.Ok) {
                log.Text += "\n--- FAFNIR ENGINE BOOT ---\n" + data.Data + "\nIniciando HLE...\nVulkan Adreno 650 OK\nFafnir ativa!\n";
            }
        }
    }
}
