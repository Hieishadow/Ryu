using Android.App; using Android.Content.PM; using Android.OS; using Android.Views; using Android.Widget;
using AFormat = Android.Graphics.Format; using Ryujinx.HLE; using Ryujinx.HLE.FileSystem; using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc; using Ryujinx.HLE.UI; using Ryujinx.Graphics.Vulkan;
using Ryujinx.Audio.Backends.Dummy; using Silk.NET.Vulkan; using System; using System.IO; using System.Linq;
using System.Reflection; using System.Runtime.InteropServices; using System.Threading; using SysEnv = System.Environment;
using Switch = Ryujinx.HLE.Switch;

namespace DragoNX;

[Activity(Name = "com.ryubing.android.GameActivity", Label = "Ryubing", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = ScreenOrientation.Landscape, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden, Exported = false, MainLauncher = false)]
public class GameActivity : Activity
{
    const string TAG = "Ryubing";
    static readonly string LogFile = "/storage/emulated/0/Download/Ryubing/ryubing_log.txt";
    static string _lastBaseDir = "";
    string romPath = ""; SurfaceView surfaceView = null!; TextView logView = null!; TextView fpsView = null!;
    Thread? emuThread; volatile bool running = false; IntPtr nativeWindow = IntPtr.Zero; Switch? device; VulkanRenderer? gpu;
    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);
    [DllImport("android")] static extern void ANativeWindow_acquire(IntPtr window);
    [DllImport("android")] static extern void ANativeWindow_release(IntPtr window);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        try { Console.SetOut(new StringWriter()); Console.SetError(new StringWriter()); } catch {}
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
        romPath = Intent?.GetStringExtra("rom_path")?? "";
        try { File.WriteAllText(LogFile, $"=== Ryubing LOG {DateTime.Now} ===\nROM: {romPath}\n"); } catch {}
        if (string.IsNullOrEmpty(romPath) ||!File.Exists(romPath)) {
            var dir = "/storage/emulated/0/Download/Ryubing/games";
            if (Directory.Exists(dir)) {
                var first = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories).FirstOrDefault(p => p.EndsWith(".nsp")||p.EndsWith(".xci"));
                if (first!= null) romPath = first;
            }
        }
        surfaceView = new SurfaceView(this); surfaceView.Holder!.SetFormat(AFormat.Rgba8888);
        logView = new TextView(this){ Text = $"RYUBING\n{Path.GetFileName(romPath)}\n" }; logView.SetTextColor(global::Android.Graphics.Color.White); logView.SetBackgroundColor(global::Android.Graphics.Color.Black); logView.TextSize=10; logView.SetPadding(20,20,20,20);
        fpsView = new TextView(this) { Text = "FPS: --" }; fpsView.SetTextColor(global::Android.Graphics.Color.Lime); fpsView.TextSize=13; fpsView.SetPadding(20,30,20,20);
        var root = new FrameLayout(this); root.AddView(surfaceView, new FrameLayout.LayoutParams(-1, -1)); root.AddView(logView, new FrameLayout.LayoutParams(-1, -1)); root.AddView(fpsView, new FrameLayout.LayoutParams(-2, -2) { Gravity = GravityFlags.Top | GravityFlags.Left });
        SetContentView(root); surfaceView.Holder!.AddCallback(new SurfaceCallback(this));
    }
    void LogAppend(string m){ try{ File.AppendAllText(LogFile,DateTime.Now.ToString("HH:mm:ss")+" "+m+"\n"); }catch{} RunOnUiThread(()=>{ if(logView!=null) logView.Text+="\n"+m; }); }
    void LogError(string m){ try{ File.AppendAllText(LogFile,DateTime.Now.ToString("HH:mm:ss")+" ERRO: "+m+"\n"); }catch{} RunOnUiThread(()=>{ if(logView!=null){ logView.Text+="\nERRO: "+m; logView.SetTextColor(global::Android.Graphics.Color.Red); } }); }
    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback {
        readonly GameActivity act; public SurfaceCallback(GameActivity a)=>act=a;
        public void SurfaceCreated(ISurfaceHolder h){ act.nativeWindow=ANativeWindow_fromSurface(global::Android.Runtime.JNIEnv.Handle,h.Surface!.Handle); if(act.nativeWindow==IntPtr.Zero){ act.LogError("ANativeWindow Zero!"); return; } ANativeWindow_acquire(act.nativeWindow); if(act.emuThread==null||!act.emuThread.IsAlive){ act.running=true; act.emuThread=new Thread(act.EmulationLoop){ IsBackground=true, Name="RyujinxEmu" }; act.emuThread.Start(); } }
        public void SurfaceChanged(ISurfaceHolder h,AFormat f,int w,int ht){} public void SurfaceDestroyed(ISurfaceHolder h){ act.running=false; if(act.nativeWindow!=IntPtr.Zero){ ANativeWindow_release(act.nativeWindow); act.nativeWindow=IntPtr.Zero; } }
    }
    void EmulationLoop()
    {
        try{
            LogAppend($"Iniciando {Path.GetFileName(romPath)}");
            string baseDir=Path.Combine(FilesDir!.AbsolutePath,"Ryujinx"); _lastBaseDir = baseDir; string systemDir=Path.Combine(baseDir,"system"); Directory.CreateDirectory(systemDir);
            CopyKeys(baseDir,systemDir); CopyFirmware(baseDir); InitAppData(baseDir);
            string jitDir=Path.Combine(CacheDir!.AbsolutePath,"jit"); Directory.CreateDirectory(jitDir); SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE",jitDir);
            LogAppend("Criando VFS..."); var vfs=VirtualFileSystem.CreateInstance(); vfs.ReloadKeySet(); LogAppend("VFS OK");
            LogAppend("Criando VulkanRenderer...");
            gpu=VulkanRenderer.Create("Ryubing",(inst,vk)=>{ unsafe{ var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)nativeWindow }; var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR"); var func=Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(fp); SurfaceKHR surf; func(inst,&ci,null,&surf); return surf; } },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});
            LogAppend("Vulkan OK");
            var audio=new DummyHardwareDeviceDriver();
            var hleConf=BuildHleConfigurationFIX(vfs,gpu,audio);
            LogAppend("Criando Switch..."); device=new Switch(hleConf); LogAppend("Switch criado");
            if(!device.LoadNsp(romPath)) throw new Exception("LoadNsp false"); LogAppend("NSP OK");
            RunOnUiThread(()=>{ if(logView!=null) logView.Visibility=ViewStates.Gone; });
            var sw=System.Diagnostics.Stopwatch.StartNew(); int frames=0;
            while(running){ device.ProcessFrame(); device.PresentFrame(()=>{ Thread.Sleep(1); }); frames++; if(sw.ElapsedMilliseconds>=1000){ int f=frames; frames=0; sw.Restart(); RunOnUiThread(()=>{ if(fpsView!=null) fpsView.Text=$"FPS: {f}"; }); } }
        }catch(Exception ex){ LogError($"{ex.Message}\n{ex}"); }finally{ try{ device?.Dispose(); }catch{} try{ gpu?.Dispose(); }catch{} }
    }
    void CopyKeys(string baseDir,string systemDir){ string extKeys="/storage/emulated/0/Download/Ryubing/keys"; string keysDir=Path.Combine(baseDir,"keys"); Directory.CreateDirectory(keysDir); foreach(var name in new[]{"prod.keys","title.keys"}){ string src=Path.Combine(extKeys,name); if(!File.Exists(src)) continue; try{ File.Copy(src,Path.Combine(systemDir,name),true); File.Copy(src,Path.Combine(keysDir,name),true); LogAppend($"{name} OK"); }catch{} } }
    void CopyFirmware(string baseDir){ try{ string fwSrc="/storage/emulated/0/Download/Ryubing/firmware"; string fwDst=Path.Combine(baseDir,"bis","system","Contents","registered"); Directory.CreateDirectory(fwDst); if(!Directory.Exists(fwSrc)) return; foreach(var nca in Directory.GetFiles(fwSrc,"*.nca")){ string dst=Path.Combine(fwDst,Path.GetFileName(nca)); if(!File.Exists(dst)) File.Copy(nca,dst,true); } LogAppend("Firmware OK"); }catch(Exception ex){ LogAppend($"Firmware erro: {ex.Message}"); } }
    void InitAppData(string baseDir){ try{ Directory.CreateDirectory(Path.Combine(baseDir,"bis","user","save")); Directory.CreateDirectory(Path.Combine(baseDir,"system","save")); }catch{} }
    unsafe delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(Instance i,AndroidSurfaceCreateInfoKHR* p,AllocationCallbacks* a,SurfaceKHR* s);

    // ===== FUNÇÃO CORRETA E LEVE =====
    HleConfiguration BuildHleConfigurationFIX(VirtualFileSystem vfs,VulkanRenderer gpu,DummyHardwareDeviceDriver audio)
    {
        var libHac = new LibHacHorizonManager(); libHac.InitializeFs(_lastBaseDir, _lastBaseDir, vfs, true, true); libHac.InitializeArp();
        var ucp = new UserChannelPersistence(); var cm = new ContentManager(libHac.RyFs);
        var acc = new AccountManager(libHac.RyFs, libHac.LibHacHorizon.Fs, libHac.LibHacHorizon.ApplicationClient.Fs, libHac.LibHacHorizon.UserManager, new Ryujinx.Common.Configuration.Hid.HidConfiguration());
        var memConf = new Ryujinx.Common.Configuration.MemoryConfiguration();
        var hleConf = new HleConfiguration(memConf, Ryujinx.Common.Configuration.SystemLanguage.AmericanEnglish, Ryujinx.Common.Configuration.RegionCode.USA, Ryujinx.Common.Configuration.VSyncMode.Switch, true, false, 1, false, LibHac.Tools.FsSystem.IntegrityCheckLevel.None, 0, 0, "UTC", Ryujinx.Common.Configuration.MemoryManagerMode.HostMappedUnsafe, true, Ryujinx.Common.Configuration.AspectRatio.Fixed16x9, 1f, false, "", Ryujinx.Common.Configuration.Multiplayer.MultiplayerMode.Disabled, true, "", "", false, 0, false, 1, []);
        hleConf.Configure(vfs, libHac, cm, acc, ucp, gpu, audio, new DummyHostUIHandler());
        LogAppend("HLE Config OK - LEVE"); return hleConf;
    }
    protected override void OnDestroy(){ running=false; try{ emuThread?.Join(2000); }catch{} base.OnDestroy(); }
}
public class DummyHostUIHandler : IHostUIHandler {
 public bool DisplayMessageDialog(string a,string b,string c,string d,string e,string f,int g,bool h,out int i){ i=0; return false; }
 public bool DisplayInputDialog(string a,string b,string c,out string d){ d=c; return false; }
 public System.Threading.Tasks.Task DisplayMessageDialog(string a,string b,string c,string d,System.Threading.CancellationToken e){ return System.Threading.Tasks.Task.CompletedTask; }
 public void DialogShown(){} public void DialogClosed(){} public void UpdateApplesCount(int c){}
}
