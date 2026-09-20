#nullable disable
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using System;
using System.IO;
using System.Threading.Tasks;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.Graphics.Gpu;
using HLESwitch = Ryujinx.HLE.Switch;

namespace Ryujinx.Android;

[Activity(Name = "com.ryubing.android.GameActivity", Exported = false,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    HLESwitch device;
    string romPath;
    SurfaceView surface;
    TextView status;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        romPath = Intent?.GetStringExtra("rom_path") ?? "";
        if (!File.Exists(romPath)) { Finish(); return; }

        var root = new FrameLayout(this);
        surface = new SurfaceView(this);
        status = new TextView(this);
        status.SetTextColor(global::Android.Graphics.Color.White);
        status.Text = $"Carregando {Path.GetFileName(romPath)}...";
        status.SetBackgroundColor(global::Android.Graphics.Color.Argb(180,0,0,0));

        root.AddView(surface, new FrameLayout.LayoutParams(-1,-1));
        root.AddView(status, new FrameLayout.LayoutParams(-2,-2, GravityFlags.Top | GravityFlags.Left));
        SetContentView(root);

        try
        {
            var filesDir = FilesDir.AbsolutePath;
            var baseDir = Path.Combine(filesDir, "Ryujinx");
            var keysDir = Path.Combine(baseDir, "keys");
            Directory.CreateDirectory(keysDir);
            Directory.CreateDirectory(Path.Combine(baseDir, "system", "keys"));

            var src = "/storage/emulated/0/Download/Ryubing/keys/prod.keys";
            if (File.Exists(src))
            {
                File.Copy(src, Path.Combine(keysDir, "prod.keys"), true);
                File.Copy(src, Path.Combine(baseDir, "system", "keys", "prod.keys"), true);
            }

            var adm = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}})
                .FirstOrDefault(t=>t.Name=="AppDataManager");
            adm?.GetProperty("BaseDirPath")?.SetValue(null, baseDir);
        }catch(Exception ex){ global::Android.Util.Log.Error("Ryubing", ex.ToString()); }

        surface.Holder.AddCallback(new SurfaceCallback(this));
    }

    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
    {
        GameActivity act;
        public SurfaceCallback(GameActivity a){ act=a; }
        public void SurfaceChanged(ISurfaceHolder h, global::Android.Graphics.Format f, int w, int h2){}
        public void SurfaceDestroyed(ISurfaceHolder h){}
        public void SurfaceCreated(ISurfaceHolder h)
        {
            Task.Run(()=> act.StartEmulation());
        }
    }

    void StartEmulation()
    {
        try
        {
            RunOnUiThread(()=> status.Text = "Inicializando Ryujinx...");
            Logger.AddLogger(new ConsoleLogger(), LogLevel.Info);

            var vfs = new VirtualFileSystem();
            
            device = new HLESwitch(vfs, null, null, null, null, null, null, null);
            
            RunOnUiThread(()=> status.Text = $"Carregando ROM...");

            var result = device.LoadApplication(romPath);
            if (result == false)
                throw new Exception("Falha ao carregar ROM - keys erradas?");

            RunOnUiThread(()=> { status.Visibility = ViewStates.Gone; });
            device.Run();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("Ryubing", ex.ToString());
            try{ File.AppendAllText("/storage/emulated/0/Download/Ryubing/logs/ryubing.log", ex.ToString()); }catch{}
            RunOnUiThread(()=> {
                status.Text = $"ERRO: {ex.Message}\n{ex.StackTrace}";
                status.Visibility = ViewStates.Visible;
                Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
            });
        }
    }

    protected override void OnDestroy()
    {
        device?.Stop();
        device?.Dispose();
        base.OnDestroy();
    }
}
