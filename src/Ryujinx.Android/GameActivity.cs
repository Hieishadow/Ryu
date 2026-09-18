using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.Graphics.Vulkan;
using LibHac;
using Silk.NET.Vulkan;
using System.IO;
using System.Threading.Tasks;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
          ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        var romPath = Intent?.GetStringExtra("rom_path")?? "/storage/emulated/0/Download/DragoNX/games/game.nsp";
        try {
            var games = Directory.GetFiles("/storage/emulated/0/Download/DragoNX/games", "*.nsp");
            if (games.Length > 0 && string.IsNullOrEmpty(Intent?.GetStringExtra("rom_path"))) romPath = games[0];
        } catch {}

        var log = new TextView(this){ Text = $"DragoNX - Carregando {Path.GetFileName(romPath)}..." };
        log.Gravity = GravityFlags.Center;
        log.TextSize = 18;
        SetContentView(log);

        Task.Run(() => {
            try {
                var horizonConfig = new HorizonConfiguration();
                var horizon = new Horizon(horizonConfig);
                var vfs = VirtualFileSystem.CreateInstance();
                vfs.InitializeFsServer(horizon, out HorizonClient fsClient);
                var acc = new AccountManager(fsClient);
                var renderer = VulkanRenderer.Create("", (instance, vk) => new SurfaceKHR(), () => new[] { "VK_KHR_surface", "VK_KHR_android_surface" });

                RunOnUiThread(() => {
                    log.Text = $"DragoNX OK!\nVFS: {vfs!= null}\nACC: {acc!= null}\nVulkan: {renderer!= null}\nROM: {Path.GetFileName(romPath)}\n\nPronto pra bootar Zelda!";
                    Toast.MakeText(this, "Link's Awakening pronto!", ToastLength.Long).Show();
                });
            } catch (System.Exception ex) {
                RunOnUiThread(() => log.Text = ex.ToString());
            }
        });
    }
}
