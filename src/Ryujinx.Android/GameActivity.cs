#nullable disable
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using System;
using System.IO;
using System.Linq;

namespace Ryujinx.Android;

[Activity(Name = "com.ryubing.android.GameActivity", Exported = false,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    ScreenOrientation = ScreenOrientation.Landscape)]
public class GameActivity : Activity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        string romPath = Intent?.GetStringExtra("rom_path")?? "";
        if(string.IsNullOrEmpty(romPath) ||!File.Exists(romPath)){ Finish(); return; }

        var filesDir = FilesDir.AbsolutePath;
        var baseDir = Path.Combine(filesDir, "Ryujinx");
        var keysDir = Path.Combine(baseDir, "keys");

        try{
            Directory.CreateDirectory(keysDir);
            var admType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}})
            .FirstOrDefault(t=>t.Name=="AppDataManager");
            admType?.GetProperty("BaseDirPath")?.SetValue(null, baseDir);

            // FIX ERRO L47 e L50 - tem que usar global::
            var black = new View(this);
            black.SetBackgroundColor(global::Android.Graphics.Color.Black);
            SetContentView(black);
        }catch(Exception ex){
            global::Android.Util.Log.Error("Ryubing", ex.ToString());
            Finish();
        }
    }
}
