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

            // COPIA DA PASTA DOWNLOAD SE TIVER
            var downloadKeys = "/storage/emulated/0/Download/Ryubing/keys/prod.keys";
            if(File.Exists(downloadKeys) &&!File.Exists(prodKeys)){
                File.Copy(downloadKeys, prodKeys, true);
            }

            // FIX VFS
            var admType = AppDomain.CurrentDomain.GetAssemblies()
              .SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}})
              .FirstOrDefault(t=>t.Name=="AppDataManager");
            admType?.GetProperty("BaseDirPath")?.SetValue(null, baseDir);

            // VALIDACAO NOVA - NAO DEIXA CRASHAR COM BINARIO
            if(!File.Exists(prodKeys)){
                throw new Exception("prod.keys NAO encontrado em /Ryujinx/keys/");
            }

            var len = new FileInfo(prodKeys).Length;
            var text = File.ReadAllText(prodKeys);
            if(len > 10000 ||!text.Contains("master_key_")){
                // BINARIO DETECTADO
                var layoutErr = new LinearLayout(this);
                layoutErr.Orientation = Orientation.Vertical;
                layoutErr.SetGravity(GravityFlags.Center);
                layoutErr.SetBackgroundColor(Android.Graphics.Color.Black);
                var txtErr = new TextView(this);
                txtErr.Text = $"prod.keys INVALIDO: {len}b\n\nEsse arquivo e BINARIO.\nRyujinx precisa do TXT de 5kb com master_key_00 =...\n\nDelete e importe o correto.";
                txtErr.SetTextColor(Android.Graphics.Color.Red);
                txtErr.Gravity = GravityFlags.Center;
                txtErr.TextSize = 18;
                layoutErr.AddView(txtErr);
                SetContentView(layoutErr);
                return; // NAO CRASHA, SO MOSTRA ERRO
            }

            Android.Util.Log.Info("Ryubing", $"ROM: {romPath} Keys: {len}b OK");

            var layout = new LinearLayout(this);
            layout.Orientation = Orientation.Vertical;
            layout.SetGravity(GravityFlags.Center);
            layout.SetBackgroundColor(Android.Graphics.Color.Black);
            var txt = new TextView(this);
            txt.Text = $"VFS OK - {Path.GetFileName(romPath)}\nKeys OK {len}b\nIniciando...";
            txt.SetTextColor(Android.Graphics.Color.White);
            txt.Gravity = GravityFlags.Center;
            layout.AddView(txt);
            SetContentView(layout);

            Toast.MakeText(this, "Iniciando emulacao...", ToastLength.Short).Show();

            // AQUI CHAMA SEU ENTRY REAL
            // new RyujinxAndroidEntry().Start(romPath);
        }
        catch(Exception ex){
            Android.Util.Log.Error("Ryubing", ex.ToString());
            Toast.MakeText(this, "Erro: " + ex.Message, ToastLength.Long).Show();
            Finish();
        }
    }
}
