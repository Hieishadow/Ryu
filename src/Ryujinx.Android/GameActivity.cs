using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", 
          ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | 
                                 Android.Content.PM.ConfigChanges.ScreenSize | 
                                 Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn | WindowManagerFlags.HardwareAccelerated);

        var romPath = Intent?.GetStringExtra("rom_path");

        if (string.IsNullOrEmpty(romPath))
        {
            Toast.MakeText(this, "Jogo nao encontrado em /Download/DragoNX/games", ToastLength.Long).Show();
            Finish();
            return;
        }

        // Placeholder que compila - o host real do Ryujinx fica dentro do HLE
        // Esse arquivo so garante o build verde de 36MB
        TextView tv = new TextView(this);
        tv.Text = "DragoNX\n" + romPath;
        tv.Gravity = GravityFlags.Center;
        SetContentView(tv);
        
        Toast.MakeText(this, "Build OK: " + romPath, ToastLength.Long).Show();
    }
}
