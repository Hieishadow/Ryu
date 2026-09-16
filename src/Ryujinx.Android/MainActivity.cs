using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using System;
using System.IO;

namespace Ryujinx.Android
{
    [Activity(Label = "Ryu MASTER REAL", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var log = new TextView(this) { Text = "RYUJINX MASTER REAL - S20 FE\nCOMPILOU VERDE!\n" };
            var btn = new Button(this) { Text = "TESTAR PASTAS" };
            btn.Click += (s,e) => {
                try {
                    var basePath = Path.Combine(GetExternalFilesDir(null)!.AbsolutePath, "Ryubing");
                    Directory.CreateDirectory(basePath);
                    log.Text += $"\nBase: {basePath}";
                    var keyPath = Path.Combine(basePath, "keys", "prod.keys");
                    log.Text += File.Exists(keyPath) ? $"\nKeys OK: {new FileInfo(keyPath).Length} bytes" : "\nSem keys - coloque em /keys/prod.keys";
                    log.Text += "\n\n✅ BOOT OK - Próximo passo Vulkan";
                } catch (Exception ex) { log.Text += "\nERRO: " + ex.Message; }
            };
            layout.AddView(btn);
            layout.AddView(log);
            SetContentView(layout);
        }
    }
}
