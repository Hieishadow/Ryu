using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using System.IO;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.Audio.Backends.Dummy;
using Ryujinx.Common.Configuration;
using Ryujinx.Memory;
using Ryujinx.HLE.UI;

namespace RyujinxAndroid
{
    [Activity(Label = "DragoNX JIT", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        Switch emu;
        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
            var view = new SurfaceView(this);
            SetContentView(view);

            var dir = "/storage/emulated/0/Download/DragoNX/";
            string game = null;
            if(Directory.Exists(dir))
                foreach(var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    if(f.EndsWith(".nsp") || f.EndsWith(".xci") || f.EndsWith(".nca") || f.EndsWith(".nro"))
                    { game = f; break; }

            if(game == null){ Toast.MakeText(this,"Jogo não achado em /Download/DragoNX/",ToastLength.Long).Show(); return; }

            try
            {
                // 1. Libera JIT no Android (RWX)
                MemoryBlock.EnableForcedRwx = true;

                // 2. FileSystem novo
                var vfs = new VirtualFileSystem();
                var keyPath = Path.Combine(dir, "prod.keys");
                if(File.Exists(keyPath))
                    vfs.ImportKeys(File.ReadAllBytes(keyPath), "prod.keys");

                // 3. Drivers
                var gpuRenderer = new VulkanRenderer();
                var audioDriver = new DummyHardwareDeviceDriver();
                var userChannel = new UserChannelPersistence();
                
                // 4. Config com JIT ON
                var memConfig = new MemoryConfiguration(4294967296, MemoryConfiguration.DefaultHostAddressSpace, MemoryAllocationFlags.Reserve | MemoryAllocationFlags.Mirrorable);
                
                var config = new HleConfiguration(
                    vfs,
                    gpuRenderer,
                    audioDriver,
                    userChannel,
                    memConfig,
                    new Ryujinx.Common.Logging.LogProvider(),
                    new NullHostUIHandler(),
                    new List<LibHac.FsSystem.IAccessor>(),
                    MemoryManagerMode.HostMapped, // <- JIT rápido
                    true, // EnablePtc
                    Ryujinx.HLE.FileSystem.FsGlobalAccessLogMode.None,
                    0,
                    Ryujinx.Common.Configuration.System.Language.AmericanEnglish,
                    Ryujinx.Common.Configuration.System.Region.USA,
                    Ryujinx.Common.Configuration.System.VSyncMode.Switch,
                    false, false, 0, 1.0f, false, false, false, false, false, false, false, new DirtyHacks(), 0, false, System.TimeZoneInfo.Local
                );

                // 5. Cria Switch novo
                emu = new Switch(config);
                emu.LoadNsp(game); // ou LoadXci se for .xci

                new System.Threading.Thread(() => {
                    // Loop principal
                    while(true){
                        emu.ProcessFrame();
                    }
                }){ IsBackground = true }.Start();

                Toast.MakeText(this, "JIT ON - " + Path.GetFileName(game), ToastLength.Short).Show();
            }
            catch(System.Exception e)
            {
                Toast.MakeText(this, "JIT ERRO: " + e.ToString(), ToastLength.Long).Show();
            }
        }
    }
    class NullHostUIHandler : IHostUIHandler { public void DisplayMessage(string t, string m){} public bool DisplayMessageDialog(string t, string m){return true;} public void DisplayErrorMessage(string m){} }
}
