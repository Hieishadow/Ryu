using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.HLE.UI;
using Ryujinx.Common.Logging;
using LibHac;
using Silk.NET.Vulkan;
using System.IO;
using System.Threading.Tasks;
using SwitchDevice = Ryujinx.HLE.Switch;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
          ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    private SwitchDevice _device;
    private VulkanRenderer _renderer;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn | WindowManagerFlags.HardwareAccelerated);

        var romPath = Intent?.GetStringExtra("rom_path");
        if (string.IsNullOrEmpty(romPath))
        {
            try {
                var games = Directory.GetFiles("/storage/emulated/0/Download/DragoNX/games", "*.nsp");
                if (games.Length > 0) romPath = games[0];
            } catch {}
        }

        // Tela de loading
        var frame = new FrameLayout(this);
        var text = new TextView(this) { Text = $"DragoNX - Carregando {Path.GetFileName(romPath)}..." };
        text.Gravity = GravityFlags.Center;
        text.TextSize = 18;
        frame.AddView(text);
        SetContentView(frame);

        Task.Run(() => BootGame(romPath, text));
    }

    void BootGame(string romPath, TextView logView)
    {
        try {
            // 1. HORIZON + VFS + ACC (API REAL do seu Ryubing)
            var horizonConfig = new HorizonConfiguration();
            var horizon = new Horizon(horizonConfig);
            var vfs = VirtualFileSystem.CreateInstance();
            vfs.InitializeFsServer(horizon, out HorizonClient fsClient);
            var acc = new AccountManager(fsClient);

            // 2. VULKAN (usa a surface nativa do Android)
            RunOnUiThread(() => logView.Text = "Inicializando Vulkan...");
            _renderer = VulkanRenderer.Create("",
                (instance, vk) => new SurfaceKHR(),
                () => new[] { "VK_KHR_surface", "VK_KHR_android_surface" });

            // 3. SWITCH DEVICE
            RunOnUiThread(() => logView.Text = "Criando Switch...");
            var config = new HLEConfiguration(vfs, _renderer);
            _device = new SwitchDevice(config);
            _device.Configuration.AccountManager = acc;

            // 4. LOAD NSP do Link's Awakening
            RunOnUiThread(() => logView.Text = $"Carregando {Path.GetFileName(romPath)}...");
            _device.LoadApplication(romPath, vfs, acc);

            // 5. RUN LOOP - ISSO GERA O VIDEO
            RunOnUiThread(() => {
                var surfaceView = new SurfaceView(this);
                SetContentView(surfaceView);
                Toast.MakeText(this, "Link's Awakening BOOTOU!", ToastLength.Long).Show();
            });

            _device.Run();
        } catch (System.Exception ex) {
            RunOnUiThread(() => {
                logView.Text = ex.ToString();
                Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
            });
        }
    }

    protected override void OnDestroy()
    {
        _device?.Stop();
        _renderer?.Dispose();
        base.OnDestroy();
    }

    class DummyHostUIHandler : IHostUIHandler
    {
        public IHostUITheme HostUITheme => null;
        public bool DisplayInputDialog(SoftwareKeyboardUIArgs args, out string t){ t=""; return false; }
        public bool DisplayMessageDialog(string a, string b)=>false;
        public bool DisplayMessageDialog(ControllerAppletUIArgs a)=>false;
        public bool DisplayCabinetDialog(out string t){ t=""; return false; }
        public void DisplayCabinetMessageDialog(){}
        public void ExecuteProgram(SwitchDevice d, ProgramSpecifyKind k, ulong v){}
        public bool DisplayErrorAppletDialog(string a, string b, string[] c, (uint,uint)? d=null)=>false;
        public IDynamicTextInputHandler CreateDynamicTextInputHandler()=>null;
        public UserProfile ShowPlayerSelectDialog()=>null;
        public void TakeScreenshot(){}
    }
}
