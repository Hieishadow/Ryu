#nullable disable
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using System;
using System.IO;
using System.Linq;

namespace Ryujinx.Android;

[Activity(Name = "com.ryubing.android.GameActivity", Exported = false,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    TextView logTxt;
    string logFile = "/storage/emulated/0/Download/Ryubing/logs/ryubing.log";
    void Log(string m)
    {
        try{ File.AppendAllText(logFile, $"{DateTime.Now:HH:mm:ss} {m}\n"); }catch{}
        global::Android.Util.Log.Info("Ryubing", m);
        RunOnUiThread(()=>{ if(logTxt!=null) logTxt.Text += "\n" + m; });
    }

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        var layout = new LinearLayout(this){ Orientation = Orientation.Vertical };
        layout.SetBackgroundColor(global::Android.Graphics.Color.Black);
        logTxt = new TextView(this);
        logTxt.SetTextColor(global::Android.Graphics.Color.White);
        logTxt.TextSize = 12f;
        logTxt.SetPadding(20,20,20,20);
        logTxt.Text = "Ryubing iniciando...";
        layout.AddView(logTxt);
        SetContentView(layout);

        string romPath = Intent?.GetStringExtra("rom_path")?? "";
        if(string.IsNullOrEmpty(romPath) ||!File.Exists(romPath)){ Log($"ROM nao existe: {romPath}"); return; }

        try{
            var filesDir = FilesDir.AbsolutePath;
            var baseDir = Path.Combine(filesDir, "Ryujinx");
            var keysDir = Path.Combine(baseDir, "keys");
            var prodKeys = Path.Combine(keysDir, "prod.keys");
            var pubKeys = "/storage/emulated/0/Download/Ryubing/keys/prod.keys";

            Directory.CreateDirectory(keysDir);
            Directory.CreateDirectory(Path.Combine(baseDir, "logs"));
            Directory.CreateDirectory("/storage/emulated/0/Download/Ryubing/logs");

            if(File.Exists(pubKeys))
            {
                if(!File.Exists(prodKeys) || new FileInfo(pubKeys).Length!=new FileInfo(prodKeys).Length)
                {
                    File.Copy(pubKeys, prodKeys, true);
                    Log($"Keys copiada {new FileInfo(pubKeys).Length}b");
                }
            }

            var admType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}})
                .FirstOrDefault(t=>t.Name=="AppDataManager");
            if(admType!=null)
            {
                admType.GetProperty("BaseDirPath")?.SetValue(null, baseDir);
                Log($"BaseDir={baseDir}");
            }

            long len = File.Exists(prodKeys)? new FileInfo(prodKeys).Length : 0;
            Log($"JOGAR {Path.GetFileName(romPath)} Keys={len}b");

            // TENTA ACHAR O HOST REAL DO RYUJINX
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}}).ToList();

            var entry = types.FirstOrDefault(t=>t.Name=="GameHost")
                     ?? types.FirstOrDefault(t=>t.Name=="AndroidHost")
                     ?? types.FirstOrDefault(t=>t.Name.Contains("GameHost"));

            if(entry!=null)
            {
                Log($"Achado host: {entry.FullName}");
                var m = entry.GetMethod("Start") ?? entry.GetMethod("Launch") ?? entry.GetMethod("Run") ?? entry.GetMethod("StartGame");
                if(m!=null)
                {
                    var inst = Activator.CreateInstance(entry);
                    Log($"Invocando {m.Name}...");
                    m.Invoke(inst, new object[]{ romPath });
                    return;
                }
                Log($"Metodo Start nao achado em {entry.Name}. Metodos: {string.Join(",", entry.GetMethods().Select(x=>x.Name))}");
            }
            else
            {
                Log("ERRO: GameHost nao encontrado. Listando o que tem:");
                foreach(var t in types.Where(t=>t.FullName.Contains("Ryujinx")).Take(30))
                    Log($" - {t.FullName}");
                Log("FIX: Voce precisa iniciar pelo MainActivity do Ryujinx, nao por GameActivity separada.");
            }
        }catch(Exception ex){
            Log($"EXCEPTION: {ex}");
        }
    }
}
