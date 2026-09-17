using Android.App;
using Android.OS;
using Android.Views;
using Ryujinx.Ava;
using Ryujinx.Graphics.Vulkan;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    private GameHost _host;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn | WindowManagerFlags.HardwareAccelerated);

        var romPath = Intent.GetStringExtra("rom_path");

        // Inicializa Ryujinx com Vulkan + Turnip
        _host = new GameHost(this, romPath, "/storage/emulated/0/Download/DragoNX/system/");
        SetContentView(_host.View);
        _host.Start();
    }

    protected override void OnDestroy()
    {
        _host?.Stop();
        base.OnDestroy();
    }
}
