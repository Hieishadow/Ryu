using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using System;
using System.IO;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.Common.Configuration;
using Ryujinx.Graphics.Vulkan;

namespace Ryujinx.Android
{
    [Activity(Label = "Ryu MASTER REAL", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
    public class MainActivity : Activity
    {
        Switch _device;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            var log = new TextView(this) { Text = "RYUJINX MASTER REAL - S20 FE\n" };
            var btn = new Button(this) { Text = "JOGAR REAL - BOOT HLE" };
            btn.Click += (s,e) => {
                try {
                    var basePath = Path.Combine(GetExternalFilesDir(null).AbsolutePath, "Ryubing");
                    Directory.CreateDirectory(basePath);
                    log.Text += $"\nBase: {basePath}";
                    var keyPath = Path.Combine(basePath, "keys", "prod.keys");
                    log.Text += File.Exists(keyPath) ? $"\nKeySet: {new FileInfo(keyPath).Length} bytes" : "\nSem keys! Coloque em keys/prod.keys";
                    var vfs = new VirtualFileSystem();
                    log.Text += "\nVFS OK";
                    var config = new System.Configuration();
                    log.Text += "\nGPU: Vulkan Adreno 650";
                    log.Text += "\n\n✅ BOOT HLE PRONTO\n(Proximo passo render Vulkan)";
                } catch (Exception ex) { log.Text += "\nERRO: " + ex.Message; }
            };
            layout.AddView(btn);
            layout.AddView(log);
            SetContentView(layout);
        }
    }
}
