using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using Ryujinx.Ava;
using Ryujinx.Ava.Host;
using System.IO;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", 
          ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | 
                                 Android.Content.PM.ConfigChanges.ScreenSize | 
                                 Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    private GameHost? _host;
    const string BasePath = "/storage/emulated/0/Download/DragoNX";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn | WindowManagerFlags.HardwareAccelerated);

        var romPath = Intent?.GetStringExtra("rom_path");

        if (string.IsNullOrEmpty(romPath))
        {
            Toast.MakeText(this, "Sem jogo em /Download/DragoNX/games/", ToastLength.Long).Show();
            Finish();
            return;
        }

        // Inicializa Ryujinx com Vulkan + Turnip
        _host = new GameHost(this, romPath, BasePath + "/system/");
        SetContentView(_host.View);
        _host.Start();
    }

    protected override void OnDestroy()
    {
        _host?.Stop();
        _host?.Dispose();
        base.OnDestroy();
    }
}
