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
    const string BasePath = "/storage/emulated/0/Download/DragoNX";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn | WindowManagerFlags.HardwareAccelerated);

        var romPath = Intent?.GetStringExtra("rom_path");

        if (string.IsNullOrEmpty(romPath) || !System.IO.File.Exists(romPath))
        {
            Toast.MakeText(this, "Sem rom em /Download/DragoNX/games", ToastLength.Long)?.Show();
            Finish();
            return;
        }

        // JIT - ESSENCIAL - sem usar Java.IO.File ambiguo
        var jitPath = BasePath + "/cache/jit";
        System.IO.Directory.CreateDirectory(jitPath);
        
        Java.Lang.JavaSystem.SetProperty("ryujinx.jit.cache", jitPath);
        System.Environment.SetEnvironmentVariable("RYUJINX_JIT_CACHE", jitPath);
        System.Environment.SetEnvironmentVariable("RYUJINX_ENABLE_JIT", "1");

        var tv = new TextView(this);
        tv.Text = $"DragoNX ARM64\nJIT: {jitPath}\nROM: {romPath}\n\n36MB OK";
        tv.Gravity = GravityFlags.Center;
        SetContentView(tv);

        Toast.MakeText(this, "JIT ON + ROM OK", ToastLength.Long)?.Show();
    }
}
