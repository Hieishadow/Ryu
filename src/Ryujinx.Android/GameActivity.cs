using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using System.IO;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
          ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
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

        var textView = new TextView(this);
        textView.Text = $"DragoNX - Build OK\nROM: {romPath?? "nenhuma"}";
        textView.TextSize = 18;
        textView.Gravity = GravityFlags.Center;
        SetContentView(textView);

        Toast.MakeText(this, $"DragoNX carregou: {Path.GetFileName(romPath)}", ToastLength.Long).Show();
    }
}
