using Android.App;
using Android.OS;
using Android.Views;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.FileSystem;
using LibHac.FsSystem;
using System.IO;

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
            var games = Directory.GetFiles("/storage/emulated/0/Download/DragoNX/games");
            if (games.Length > 0) romPath = games[0];
        }

        var surfaceView = new SurfaceView(this);
        SetContentView(surfaceView);

        // Inicializa Ryujinx de verdade
        Task.Run(() => {
            try {
                var vfs = VirtualFileSystem.Create();
                var nsp = new FileStream(romPath, FileMode.Open, FileAccess.Read);
                // aqui carrega o Nsp pra dentro do VFS...

                device = new Switch(vfs, null, null, null,
                    new Ryujinx.Graphics.Vulkan.VulkanRenderer(),
                    null, null, true);

                device.LoadApplication(nsp);
                device.Run();
            } catch (Exception ex) {
                RunOnUiThread(() => Toast.MakeText(this, ex.ToString(), ToastLength.Long).Show());
            }
        });
    }
}
