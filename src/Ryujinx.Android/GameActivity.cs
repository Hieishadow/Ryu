#nullable disable
using Android.App;
using Android.Content.PM;
using Android.OS;
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
        if (string.IsNullOrEmpty(romPath) ||!File.Exists(romPath))
        {
            Finish(); return;
        }

        var filesDir = FilesDir.AbsolutePath;
        var baseDir = Path.Combine(filesDir, "Ryujinx");
        var keysDir = Path.Combine(baseDir, "keys");
        var prodKeys = Path.Combine(keysDir, "prod.keys");
        var publicLogDir = "/storage/emulated/0/Download/Ryubing/logs";
        var publicLogFile = Path.Combine(publicLogDir, "ryubing.log");

        try
        {
            Directory.CreateDirectory(keysDir);
            Directory.CreateDirectory(Path.Combine(baseDir, "logs"));
            Directory.CreateDirectory(publicLogDir);

            // Copia keys da pasta Download se não tiver dentro
            var downloadKeys = "/storage/emulated/0/Download/Ryubing/keys/prod.keys";
            if (File.Exists(downloadKeys))
            {
                // SEM TRAVA: aceita 14612b e 16025b
                if (!File.Exists(prodKeys) || new FileInfo(downloadKeys).Length!= new FileInfo(prodKeys).Length)
                    File.Copy(downloadKeys, prodKeys, true);
            }

            // FIX VFS - CRITICO pra não dar crash no System.sav
            var admType = AppDomain.CurrentDomain.GetAssemblies()
               .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
               .FirstOrDefault(t => t.Name == "AppDataManager");
            admType?.GetProperty("BaseDirPath")?.SetValue(null, baseDir);

            long len = File.Exists(prodKeys)? new FileInfo(prodKeys).Length : 0;

            // Log só em arquivo público + logcat, NADA NA TELA
            File.AppendAllText(publicLogFile, $"{DateTime.Now:HH:mm:ss} JOGAR {Path.GetFileName(romPath)} Keys={len}b Base={baseDir}\n");
            Android.Util.Log.Info("Ryubing", $"ROM={romPath} Base={baseDir} Keys={len}b");

            // TELA PRETA LIMPA - CONSERTO DA SUA PRINT
            var blackView = new Android.Views.View(this);
            blackView.SetBackgroundColor(Android.Graphics.Color.Black);
            SetContentView(blackView);

            // INICIA O RYUJINX DE VERDADE - tenta achar a classe automaticamente
            var entryType = AppDomain.CurrentDomain.GetAssemblies()
               .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
               .FirstOrDefault(t => t.Name.Contains("RyujinxAndroid") || t.Name.Contains("GameHost") || t.Name.Contains("AndroidEntry"));

            if (entryType!= null)
            {
                var method = entryType.GetMethod("Start")?? entryType.GetMethod("Launch")?? entryType.GetMethod("Run");
                if (method!= null)
                {
                    var instance = Activator.CreateInstance(entryType);
                    method.Invoke(instance, new object[] { romPath });
                    return;
                }
            }
            // Se não achou, deixa preto e não fecha - Ryujinx deve iniciar por outro lado
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(publicLogFile, $"ERRO: {ex}\n"); } catch {}
            Android.Util.Log.Error("Ryubing", ex.ToString());
            Finish();
        }
    }
}
