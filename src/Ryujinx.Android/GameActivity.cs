using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using System.IO;
using System.Threading.Tasks;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.Common.Configuration;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.Audio.Backends.Dummy;
using Ryujinx.HLE.UI;
using Ryujinx.Memory;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
          ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    Switch device;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn | WindowManagerFlags.HardwareAccelerated);

        var romPath = Intent?.GetStringExtra("rom_path");
        if (string.IsNullOrEmpty(romPath))
        {
            var files = Directory.GetFiles("/storage/emulated/0/Download/DragoNX/games");
            if (files.Length > 0) romPath = files[0];
        }

        if (string.IsNullOrEmpty(romPath))
        {
            Toast.MakeText(this, "Coloca o NSP em /Download/DragoNX/games", ToastLength.Long).Show();
            return;
        }

        var surfaceView = new SurfaceView(this);
        SetContentView(surfaceView);

        var finalPath = romPath;

        Task.Run(() => {
            try {
                // 1. VFS e FileSystem do seu Ryubing novo
                var vfs = new VirtualFileSystem();

                // 2. Renderer Vulkan nativo
                var gpuRenderer = new VulkanRenderer();

                // 3. Audio dummy pra não crashar no Android
                var audioDriver = new DummyHardwareDeviceDriver();

                // 4. Config nova que seu Switch pede
                var config = new HleConfiguration(
                    vfs,
                    new ContentManager(vfs),
                    new AccountManager(),
                    new LibHacHorizonManager(),
                    new UserChannelPersistence(),
                    new MemoryConfiguration(0x100000000, MemoryConfiguration.MemorySize4GiB),
                    audioDriver,
                    gpuRenderer,
                    new DummyHostUIHandler(),
                    SystemLanguage.AmericanEnglish,
                    Region.Americas,
                    VSyncMode.Switch,
                    60,
                    false, false, false, false,
                    MemoryManagerMode.SoftwarePageTable,
                    true, true, true, true
                );

                device = new Switch(config);

                // 5. Carrega e roda o NSP - API nova
                device.LoadNsp(finalPath);

                // Loop de frames
                while (true) {
                    device.ProcessFrame();
                    device.PresentFrame(() => {});
                }

            } catch (System.Exception ex) {
                RunOnUiThread(() => Toast.MakeText(this, ex.ToString(), ToastLength.Long).Show());
            }
        });
    }

    // Host vazio pra compilar no Android
    class DummyHostUIHandler : IHostUIHandler {
        public bool HasFileExtensionChanged(string e) => false;
        public void HandleErrorMessage(string m) {}
        public void HandleInfoMessage(string m) {}
    }
}
