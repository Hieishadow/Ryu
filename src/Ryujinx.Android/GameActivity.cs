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
        if(string.IsNullOrEmpty(romPath) ||!System.IO.File.Exists(romPath)){ Finish(); return; }

        var filesDir = FilesDir.AbsolutePath;
        var baseDir = System.IO.Path.Combine(filesDir, "Ryujinx");
        var keysDir = System.IO.Path.Combine(baseDir, "keys");
        var prodKeys = System.IO.Path.Combine(keysDir, "prod.keys");

        try{
            System.IO.Directory.CreateDirectory(keysDir);
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(baseDir, "logs"));

            var downloadKeys = "/storage/emulated/0/Download/Ryubing/keys/prod.keys";
            if(System.IO.File.Exists(downloadKeys)){
                if(!System.IO.File.Exists(prodKeys) || new FileInfo(downloadKeys).Length!= new FileInfo(prodKeys).Length)
                    System.IO.File.Copy(downloadKeys, prodKeys, true);
            }

            var admType = AppDomain.CurrentDomain.GetAssemblies()
             .SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}})
             .FirstOrDefault(t=>t.Name=="AppDataManager");
            admType?.GetProperty("BaseDirPath")?.SetValue(null, baseDir);

            // FIX do erro 64 e 65 - usar Android.Graphics completo
            var black = new View(this);
            black.SetBackgroundColor(Android.Graphics.Color.Black);
            SetContentView(black);
        }catch(Exception ex){
            Android.Util.Log.Error("Ryubing", ex.ToString());
            Finish();
        }
    }
}
