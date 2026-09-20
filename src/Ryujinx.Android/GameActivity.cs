#nullable disable
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views; // <--- ISSO CONSERTA O ERRO CS0103 DA SUA PRINT
using System;
using System.IO;
using System.Linq;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace Ryujinx.Android; // <--- se sua pasta é Ryu/src/Ryujinx.Android/ usa esse, se for DragoNX troca aqui

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

            var downloadKeys = "/storage/emulated/0/Download/Ryubing/keys/prod.keys";
            if (File.Exists(downloadKeys))
            {
                if (!File.Exists(prodKeys) || new FileInfo(downloadKeys).Length!= new FileInfo(prodKeys).Length)
                    File.Copy(downloadKeys, prodKeys, true);
            }

            // FIX VFS - CRITICO
            var admType = AppDomain.CurrentDomain.GetAssemblies()
              .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
              .FirstOrDefault(t => t.Name == "AppDataManager");
            admType?.GetProperty("BaseDirPath")?.SetValue(null, baseDir);

            long len = File.Exists(prodKeys)? new FileInfo(prodKeys).Length : 0;
            try{ File.AppendAllText(publicLogFile, $"{DateTime.Now:HH:mm:ss} {Path.GetFileName(romPath)} Keys={len}b\n"); }catch{}

            // TELA PRETA - SEM AQUELE TEXTO DA SUA OUTRA PRINT
            SetContentView(new View(this){ Background = new Android.Graphics.Drawables.ColorDrawable(Android.Graphics.Color.Black) });

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
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(publicLogFile, $"ERRO: {ex}\n"); } catch {}
            Android.Util.Log.Error("Ryubing", ex.ToString());
            Finish();
        }
    }
}
