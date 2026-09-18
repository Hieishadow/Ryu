using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.HLE.UI;
using Ryujinx.HLE.HOS.Applets;
using Ryujinx.HLE.HOS.Services.Am.AppletOE.ApplicationProxyService.ApplicationProxy.Types;
using LibHac;
using Silk.NET.Vulkan;
using SwitchDevice = Ryujinx.HLE.Switch;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
          ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        var romPath = Intent?.GetStringExtra("rom_path") ?? "/storage/emulated/0/Download/DragoNX/games/game.nsp";
        SetContentView(new TextView(this){ Text = $"Boot: {romPath}" });

        try {
            var horizon = new Horizon();
            var vfs = VirtualFileSystem.CreateInstance();
            vfs.InitializeFsServer(horizon, out HorizonClient fsClient);
            var acc = new AccountManager(fsClient);
            var renderer = VulkanRenderer.Create("", 
                (instance, vk) => new SurfaceKHR(), 
                () => new string[] { "VK_KHR_surface", "VK_KHR_android_surface" });

            Toast.MakeText(this, $"DragoNX OK - VFS + ACC + Vulkan criados!", ToastLength.Long).Show();
        } catch (System.Exception ex) {
            Toast.MakeText(this, ex.ToString(), ToastLength.Long).Show();
        }
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
