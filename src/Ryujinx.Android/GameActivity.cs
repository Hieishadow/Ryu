using Android.App;
using Android.Content;
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
            Toast.MakeText(this, "Sem rom em /Download/DragoNX/games", ToastLength.Long).Show();
            Finish();
            return;
        }

        // Tela temporária - prova que o build de 36MB abriu e leu o rom
        // O host Vulkan real entra aqui depois que o build ficar verde
        var layout = new LinearLayout(this);
        layout.Orientation = Orientation.Vertical;
        layout.Gravity = GravityFlags.Center;
        
        var tv = new TextView(this);
        tv.Text = "DragoNX ARM64\n\n" + romPath + "\n\nBuild OK 36MB";
        tv.Gravity = GravityFlags.Center;
        
        layout.AddView(tv);
        SetContentView(layout);
    }
}
