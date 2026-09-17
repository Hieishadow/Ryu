using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.FileSystem;
using LibHac.FsSystem;
using Ryujinx.Graphics.Vulkan;

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
            try {
                var games = Directory.GetFiles("/storage/emulated/0/Download/DragoNX/games");
                if (games.Length > 0) romPath = games[0];
            } catch {}
        }

        if (string.IsNullOrEmpty(romPath))
        {
            Toast.MakeText(this, "Coloque o NSP em /Download/DragoNX/games/", ToastLength.Long).Show();
            return;
        }

        var surfaceView = new SurfaceView(this);
        SetContentView(surfaceView);

        Task.Run(() => {
            try {
                var vfs = new VirtualFileSystem();
                var fileStream = new FileStream(romPath, FileMode.Open, FileAccess.Read);
                var pfs = new PartitionFileSystem(fileStream);

                var renderer = new VulkanRenderer();

                device = new Switch(vfs, pfs, null, null, renderer, null, null, true);

                // Carrega o jogo
                vfs.LoadNsp(pfs, pfs); // depende da sua versão do VFS
                device.LoadApplication(pfs);
                device.Run();
            } catch (System.Exception ex) {
                RunOnUiThread(() => {
                    Toast.MakeText(this, $"ERRO: {ex.Message}\n{ex.InnerException}", ToastLength.Long).Show();
                });
            }
        });
    }
}
