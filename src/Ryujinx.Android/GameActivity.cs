#nullable disable
using Android.App;
using Android.Content;
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
using Android.Runtime;

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
    const string BasePath = "/storage/emulated/0/Download/Ryubing";

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if(Window!=null) Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        string romPath = Intent?.GetStringExtra("rom_path")?? "";

        if(string.IsNullOrEmpty(romPath) ||!File.Exists(romPath)){
            Toast.MakeText(this, "ROM nao encontrada: " + romPath, ToastLength.Long).Show();
            Finish();
            return;
        }

        try{
            var filesDir = FilesDir?.AbsolutePath?? "";
            var baseDir = Path.Combine(filesDir, "Ryujinx");
            var keysDir = Path.Combine(baseDir, "keys");

            // FIX CRITICO DO VFS
            var admType = AppDomain.CurrentDomain.GetAssemblies()
               .SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}})
               .FirstOrDefault(t=>t.Name=="AppDataManager");
            admType?.GetProperty("BaseDirPath")?.SetValue(null, baseDir);

            // Log pra confirmar que vai carregar
            Android.Util.Log.Info("Ryubing", $"ROM: {romPath}");
            Android.Util.Log.Info("Ryubing", $"BaseDir: {baseDir}");
            var prodKeys = Path.Combine(keysDir, "prod.keys");
            if(File.Exists(prodKeys)){
                Android.Util.Log.Info("Ryubing", $"prod.keys: {new FileInfo(prodKeys).Length}b");
            }

            var layout = new LinearLayout(this);
            layout.Orientation = Orientation.Vertical;
            layout.SetGravity(GravityFlags.Center);
            layout.SetBackgroundColor(Android.Graphics.Color.Black);

            var txt = new TextView(this);
            txt.Text = $"Carregando:\n{Path.GetFileName(romPath)}\n\nBase: {baseDir}\nKeys: {(File.Exists(prodKeys)?"OK":"FALTA")}";
            txt.SetTextColor(Android.Graphics.Color.White);
            txt.Gravity = GravityFlags.Center;
            layout.AddView(txt);

            SetContentView(layout);

            // Aqui chama o Ryujinx de verdade
            // Se sua versao usa outro metodo, me manda o GameActivity original que eu adapto
            // Por enquanto deixa o log pra testar o VFS
            Toast.MakeText(this, "VFS OK - Iniciando jogo...", ToastLength.Short).Show();

            // TODO: Inicia a emulacao real
            // Exemplo: new RyujinxAndroidEntry().Start(romPath);
        }
        catch(Exception ex){
            Android.Util.Log.Error("Ryubing", ex.ToString());
            Toast.MakeText(this, "Erro: " + ex.Message, ToastLength.Long).Show();
            Finish();
        }
    }
}
