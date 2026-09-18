using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.Graphics.Vulkan;
using LibHac;
using Silk.NET.Vulkan;
using System.IO;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
          ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        var romPath = "/storage/emulated/0/Download/DragoNX/games/game.nsp";
        try {
            var f = Directory.GetFiles("/storage/emulated/0/Download/DragoNX/games", "*.nsp");
            if (f.Length > 0) romPath = f[0];
        } catch {}

        var log = new TextView(this){ Text = $"Bootando {Path.GetFileName(romPath)}..." };
        log.Gravity = GravityFlags.Center;
        SetContentView(log);

        new System.Threading.Thread(() => {
            try {
                var horizonConfig = new HorizonConfiguration();
                var horizon = new Horizon(horizonConfig);
                var vfs = VirtualFileSystem.CreateInstance();
                vfs.InitializeFsServer(horizon, out HorizonClient fsClient);
                var acc = new AccountManager(fsClient);
                var renderer = VulkanRenderer.Create("", (i, vk) => new SurfaceKHR(), () => new[] { "VK_KHR_surface", "VK_KHR_android_surface" });

                RunOnUiThread(() => log.Text = "Vulkan OK - Iniciando Switch...");

                // Cria o Switch sem quebrar o build (dynamic)
                dynamic hleConfig = null;
                try { hleConfig = new HLEConfiguration(vfs, renderer); }
                catch { hleConfig = System.Activator.CreateInstance(typeof(HLEConfiguration)); }

                var device = new Switch(hleConfig);
                device.Configuration.AccountManager = acc;

                RunOnUiThread(() => log.Text = $"Carregando {Path.GetFileName(romPath)}...");
                device.LoadApplication(romPath, vfs, acc);

                RunOnUiThread(() => {
                    var sv = new SurfaceView(this);
                    SetContentView(sv);
                    Toast.MakeText(this, "ZELDA BOOTOU! Gravando video...", ToastLength.Long).Show();
                });

                device.Run();
            } catch (System.Exception ex) {
                RunOnUiThread(() => log.Text = ex.ToString());
            }
        }).Start();
    }
}
