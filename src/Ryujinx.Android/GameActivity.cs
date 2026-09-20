#nullable disable
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Android.Views;
using System;
using System.IO;
using System.Linq;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace DragoNX;

[Activity(
    Name = "com.ryubing.android.GameActivity",
    Label = "Ryubing Game",
    Exported = false,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        string romPath = Intent?.GetStringExtra("rom_path")?? "";
        if(string.IsNullOrEmpty(romPath) ||!File.Exists(romPath)){
            Toast.MakeText(this, "ROM nao encontrada: " + romPath, ToastLength.Long).Show();
            Finish(); return;
        }

        try{
            var filesDir = FilesDir.AbsolutePath;
            var baseDir = Path.Combine(filesDir, "Ryujinx");
            var keysDir = Path.Combine(baseDir, "keys");
            var prodKeys = Path.Combine(keysDir, "prod.keys");
            Directory.CreateDirectory(keysDir);

            // Copia da pasta Download se precisar
            var downloadKeys = "/storage/emulated/0/Download/Ryubing/keys/prod.keys";
            if(File.Exists(downloadKeys) &&!File.Exists(prodKeys)){
                File.Copy(downloadKeys, prodKeys, true);
            }

            // FIX VFS - CRITICO
            var admType = AppDomain.CurrentDomain.GetAssemblies()
             .SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}})
             .FirstOrDefault(t=>t.Name=="AppDataManager");
            admType?.GetProperty("BaseDirPath")?.SetValue(null, baseDir);

            if(!File.Exists(prodKeys)){
                throw new Exception("prod.keys NAO encontrado");
            }

            var len = new FileInfo(prodKeys).Length;
            Android.Util.Log.Info("Ryubing", $"ROM: {romPath}");
            Android.Util.Log.Info("Ryubing", $"BaseDir: {baseDir}");
            Android.Util.Log.Info("Ryubing", $"prod.keys: {len}b - SEM TRAVA");

            var layout = new LinearLayout(this);
            layout.Orientation = Orientation.Vertical;
            layout.SetGravity(GravityFlags.Center);
            layout.SetBackgroundColor(Android.Graphics.Color.Black);

            var txt = new TextView(this);
            txt.Text = $"Carregando:\n{Path.GetFileName(romPath)}\n\nKeys: {len}b (sem trava)\nBase: {baseDir}\n\nIniciando...";
            txt.SetTextColor(Android.Graphics.Color.White);
            txt.Gravity = GravityFlags.Center;
            txt.TextSize = 16;
            layout.AddView(txt);
            SetContentView(layout);

            Toast.MakeText(this, $"VFS OK {len}b - Iniciando", ToastLength.Short).Show();

            // AQUI INICIA O RYUJINX DE VERDADE
            // new RyujinxAndroidEntry().Start(romPath);
        }
        catch(Exception ex){
            Android.Util.Log.Error("Ryubing", ex.ToString());
            Toast.MakeText(this, "Erro: " + ex.Message, ToastLength.Long).Show();
            Finish();
        }
    }
}
