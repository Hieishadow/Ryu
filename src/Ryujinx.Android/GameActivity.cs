using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using System.IO;
using System.Threading.Tasks;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.Audio.Backends.Dummy;
using Ryujinx.HLE.UI;
using LibHac.Common;
using LibHac.Tools.FsSystem;

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
            Toast.MakeText(this, "Coloca o NSP em /Download/DragoNX/games", ToastLength.Long).Show();
            return;
        }

        var surfaceView = new SurfaceView(this);
        SetContentView(surfaceView);
        var finalPath = romPath;

        Task.Run(() => {
            try {
                var vfs = new VirtualFileSystem();
                var libHacManager = new LibHacHorizonManager();
                var contentManager = new ContentManager(vfs);
                var accountManager = new AccountManager();
                var userChannel = new UserChannelPersistence();
                var gpuRenderer = new VulkanRenderer();
                var audioDriver = new DummyHardwareDeviceDriver();
                var uiHandler = new DummyHostUIHandler();

                var config = new HleConfiguration(
                    memoryConfiguration: new MemoryConfiguration(0x100000000, MemoryConfiguration.MemorySize4GiB),
                    systemLanguage: SystemLanguage.AmericanEnglish,
                    region: RegionCode.Americas,
                    vSyncMode: VSyncMode.Switch,
                    enableDockedMode: true,
                    enablePtc: false,
                    tickScalar: 1,
                    enableInternetAccess: false,
                    fsIntegrityCheckLevel: IntegrityCheckLevel.None,
                    fsGlobalAccessLogMode: 0,
                    systemTimeOffset: 0,
                    timeZone: "UTC",
                    memoryManagerMode: MemoryManagerMode.SoftwarePageTable,
                    ignoreMissingServices: true,
                    aspectRatio: AspectRatio.Fixed16x9,
                    audioVolume: 1.0f,
                    useHypervisor: false,
                    multiplayerLanInterfaceId: "",
                    multiplayerMode: MultiplayerMode.Disabled,
                    multiplayerDisableP2p: false,
                    multiplayerLdnPassphrase: "",
                    multiplayerLdnServer: "",
                    enableGdbStub: false,
                    gdbStubPort: 0,
                    debuggerSuspendOnStart: false,
                    customVSyncInterval: 60
                ).Configure(
                    vfs,
                    libHacManager,
                    contentManager,
                    accountManager,
                    userChannel,
                    gpuRenderer,
                    audioDriver,
                    uiHandler
                );

                device = new Switch(config);
                device.LoadNsp(finalPath);

                while (true)
                {
                    device.ProcessFrame();
                    device.PresentFrame(() => { });
                }

            } catch (System.Exception ex) {
                RunOnUiThread(() => Toast.MakeText(this, ex.ToString(), ToastLength.Long).Show());
            }
        });
    }

    class DummyHostUIHandler : IHostUIHandler
    {
        public bool HasFileExtensionChanged(string e) => false;
        public void HandleErrorMessage(string m) {}
        public void HandleInfoMessage(string m) {}
    }
}
