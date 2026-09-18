using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AFormat = Android.Graphics.Format;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.Common.Configuration;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.Audio.Backends.Dummy;
using Silk.NET.Vulkan;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using SysEnv = System.Environment;

namespace DragoNX;

[Activity(
    Name = "com.ryubing.android.GameActivity",
    Exported = false,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    const string TAG = "Ryubing";
    const string ExternalBase = "/storage/emulated/0/Download/Ryubing";
    string romPath = "";
    SurfaceView surfaceView = null!;
    TextView logView = null!;
    TextView fpsView = null!;
    Thread? emuThread;
    volatile bool running = false;
    IntPtr nativeWindow = IntPtr.Zero;
    Ryujinx.HLE.Switch? device;
    VulkanRenderer? gpu;

    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);
    [DllImport("android")] static extern void ANativeWindow_acquire(IntPtr window);
    [DllImport("android")] static extern void ANativeWindow_release(IntPtr window);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
        romPath = Intent?.GetStringExtra("rom_path")?? "";
        if (string.IsNullOrEmpty(romPath) ||!File.Exists(romPath))
        {
            var dir = Path.Combine(ExternalBase, "games");
            if (Directory.Exists(dir))
            {
                var first = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                   .FirstOrDefault(p => p.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".xci", StringComparison.OrdinalIgnoreCase));
                if (first!= null) romPath = first;
            }
        }
        surfaceView = new SurfaceView(this);
        logView = new TextView(this) { Text = $"RYUBING\n{Path.GetFileName(romPath)}\nExiste: {File.Exists(romPath)} {(File.Exists(romPath)? new FileInfo(romPath).Length/1024/1024 : 0)}MB" };
        logView.Gravity = GravityFlags.Center;
        logView.SetTextColor(global::Android.Graphics.Color.White);
        logView.SetBackgroundColor(global::Android.Graphics.Color.Black);
        fpsView = new TextView(this) { Text = "FPS: --" };
        fpsView.SetTextColor(global::Android.Graphics.Color.Lime);
        fpsView.TextSize = 13;
        fpsView.SetPadding(20, 30, 20, 20);
        var root = new FrameLayout(this);
        root.AddView(surfaceView, new FrameLayout.LayoutParams(-1, -1));
        root.AddView(logView, new FrameLayout.LayoutParams(-1, -1));
        root.AddView(fpsView, new FrameLayout.LayoutParams(-2, -2));
        SetContentView(root);
        surfaceView.Holder!.AddCallback(new SurfaceCallback(this));
    }

    void Log(string msg){ global::Android.Util.Log.Info(TAG, msg); RunOnUiThread(()=> logView.Text += "\n"+msg); }
    void LogError(string msg){ global::Android.Util.Log.Error(TAG, msg); RunOnUiThread(()=> { logView.Text += "\n"+msg; logView.SetTextColor(global::Android.Graphics.Color.Red); }); }

    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
    {
        readonly GameActivity act;
        public SurfaceCallback(GameActivity a) => act = a;
        public void SurfaceCreated(ISurfaceHolder holder)
        {
            act.nativeWindow = ANativeWindow_fromSurface(global::Android.Runtime.JNIEnv.Handle, holder.Surface!.Handle);
            if (act.nativeWindow == IntPtr.Zero) { act.LogError("ANativeWindow Zero!"); return; }
            ANativeWindow_acquire(act.nativeWindow);
            if (act.emuThread == null ||!act.emuThread.IsAlive)
            {
                act.running = true;
                act.emuThread = new Thread(act.EmulationLoop){ IsBackground = true, Priority = System.Threading.ThreadPriority.Highest, Name = "RyujinxEmu" };
                act.emuThread.Start();
            }
        }
        public void SurfaceChanged(ISurfaceHolder h, AFormat f, int w, int ht) { }
        public void SurfaceDestroyed(ISurfaceHolder h)
        {
            act.running = false;
            if (act.nativeWindow!= IntPtr.Zero){ ANativeWindow_release(act.nativeWindow); act.nativeWindow = IntPtr.Zero; }
        }
    }

    void EmulationLoop()
    {
        try
        {
            Log($"Iniciando {Path.GetFileName(romPath)}");
            try { var tz = Java.Util.TimeZone.Default; Log($"TimeZone: {tz.ID}"); }
            catch { Java.Util.TimeZone.Default = Java.Util.TimeZone.GetTimeZone("UTC"); Log("TimeZone fallback UTC"); }

            string baseDir = Path.Combine(FilesDir!.AbsolutePath, "Ryujinx");
            string systemDir = Path.Combine(baseDir, "system");
            Directory.CreateDirectory(systemDir);
            Directory.CreateDirectory(Path.Combine(baseDir, "bis", "user", "save", "8000000000000010"));

            CopyKeys(baseDir, systemDir);
            CopyFirmware(baseDir);
            InitAppData(baseDir);

            string jitDir = Path.Combine(CacheDir!.AbsolutePath, "jit");
            Directory.CreateDirectory(jitDir);
            SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE", jitDir);

            Log("Criando VFS...");
            VirtualFileSystem vfs;
            try
            {
                vfs = VirtualFileSystem.CreateInstance();
                Log("VFS Criado via CreateInstance");
            }
            catch (Exception ex)
            {
                Log($"CreateInstance falhou: {ex.GetType().Name}: {ex.Message}");
                var prop = typeof(VirtualFileSystem).GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var inst = prop?.GetValue(null) as VirtualFileSystem;
                if (inst == null) throw new Exception($"VFS indisponivel: {ex.Message} - prop null? {prop==null}");
                vfs = inst;
                Log("VFS reutilizado via Instance");
            }

            try { vfs.ReloadKeySet(); Log("KeySet Reload OK"); }
            catch (Exception ex) { Log($"KeySet reload: {ex.Message}"); throw; }

            Log("Criando VulkanRenderer...");
            gpu = VulkanRenderer.Create("Ryubing", (inst, vk) =>
            {
                unsafe
                {
                    var ci = new AndroidSurfaceCreateInfoKHR{ SType = StructureType.AndroidSurfaceCreateInfoKhr, Window = (nint*)nativeWindow };
                    var fp = vk.GetInstanceProcAddr(inst, "vkCreateAndroidSurfaceKHR");
                    if (fp == IntPtr.Zero) throw new Exception("vkCreateAndroidSurfaceKHR nao encontrado");
                    var func = Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(fp);
                    SurfaceKHR surf; var res = func(inst, &ci, null, &surf);
                    if (res!= Silk.NET.Vulkan.Result.Success) throw new Exception($"vkCreateSurface falhou: {res}");
                    return surf;
                }
            }, () => new[] { "VK_KHR_surface", "VK_KHR_android_surface" });
            Log("Vulkan OK");

            var audio = new DummyHardwareDeviceDriver();
            var hleConf = BuildHleConfigurationFIX(vfs, gpu, audio);
            Log("HLE Config OK");

            device = new Ryujinx.HLE.Switch(hleConf);
            Log("Switch criado - Horizon OK");

            Log("Loading NSP...");
            if (!device.LoadNsp(romPath)) throw new Exception("LoadNsp false");
            Log("NSP OK - INICIANDO JOGO");
            RunOnUiThread(() => logView.Visibility = ViewStates.Gone);

            var sw = System.Diagnostics.Stopwatch.StartNew(); int frames = 0;
            while (running)
            {
                device.ProcessFrame();
                device.PresentFrame(() => { });
                frames++;
                if (sw.ElapsedMilliseconds >= 1000){ int f = frames; frames=0; sw.Restart(); RunOnUiThread(()=> fpsView.Text = $"FPS: {f}"); }
            }
        }
        catch (Exception ex){ LogError($"ERRO:\n{ex.Message}\n{ex}"); }
        finally{ try{ device?.Dispose(); }catch{} try{ gpu?.Dispose(); }catch{} }
    }

    void CopyKeys(string baseDir, string systemDir)
    {
        string extKeys = Path.Combine(ExternalBase, "keys");
        string legacyKeys = "/storage/emulated/0/Download/DragoNX/keys";
        string keysDir = Path.Combine(baseDir, "keys");
        Directory.CreateDirectory(keysDir);
        foreach (var name in new[] { "prod.keys", "title.keys" })
        {
            string src = Path.Combine(extKeys, name);
            if (!File.Exists(src)) src = Path.Combine(legacyKeys, name);
            if (!File.Exists(src)){ Log($"ATENCAO {name} nao encontrado"); continue; }
            try{ File.Copy(src, Path.Combine(systemDir, name), true); File.Copy(src, Path.Combine(keysDir, name), true); Log($"{name} OK {new FileInfo(Path.Combine(systemDir, name)).Length}b"); }
            catch (Exception ex){ Log($"{name} erro: {ex.Message}"); }
        }
    }

    void CopyFirmware(string baseDir)
    {
        try{
            string fwSrc = Path.Combine(ExternalBase, "firmware");
            string fwDst = Path.Combine(baseDir, "bis", "system", "Contents", "registered");
            Directory.CreateDirectory(fwDst);
            if (!Directory.Exists(fwSrc)){ Log("Firmware: pasta origem nao existe"); return; }
            var ncas = Directory.GetFiles(fwSrc, "*.nca");
            int copied = 0;
            foreach (var nca in ncas){ string dst = Path.Combine(fwDst, Path.GetFileName(nca)); if (File.Exists(dst) && new FileInfo(dst).Length == new FileInfo(nca).Length) continue; File.Copy(nca, dst, true); copied++; }
            Log($"Firmware: {copied}/{ncas.Length} novos.nca");
        }catch(Exception ex){ Log($"Firmware erro: {ex.Message}"); }
    }

    void InitAppData(string baseDir)
    {
        try{
            var appDataType = typeof(AppDataManager);
            var initMethod = appDataType.GetMethod("Initialize", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if(initMethod == null){ Log("AppData.Initialize nao encontrado"); return; }
            var p = initMethod.GetParameters();
            if(p.Length == 1) initMethod.Invoke(null, new object[] { baseDir });
            else if(p.Length == 2) initMethod.Invoke(null, new object[] { baseDir, AppDataManager.LaunchMode.UserProfile });
            Log($"AppData Base: {AppDataManager.BaseDirPath}");
        }catch(Exception ex){ Log($"AppData init: {ex.Message}"); }
    }

    delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(Instance i, AndroidSurfaceCreateInfoKHR* p, AllocationCallbacks* a, SurfaceKHR* s);

    HleConfiguration BuildHleConfigurationFIX(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio)
    {
        var allTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try{ return a.GetTypes(); }catch{ return Array.Empty<Type>(); }}).ToList();
        var cmType = allTypes.FirstOrDefault(t => t.Name == "ContentManager")?? throw new Exception("ContentManager nao encontrado (trimming?)");
        var ucpType = allTypes.FirstOrDefault(t => t.Name == "UserChannelPersistence")?? throw new Exception("UserChannelPersistence nao encontrado");

        Log($"HLE ctors: {typeof(HleConfiguration).GetConstructors().Length}, CM: {cmType!=null}, UCP: {ucpType!=null}");

        object? contentManager = null;
        foreach(var c in cmType.GetConstructors().OrderByDescending(x=>x.GetParameters().Length)){
            try{
                var pars = c.GetParameters();
                var args = new object?[pars.Length];
                for(int i=0;i<pars.Length;i++){
                    if(pars[i].ParameterType == typeof(VirtualFileSystem)) args[i]=vfs;
                    else if(pars[i].ParameterType == typeof(string)) args[i]=AppDataManager.BaseDirPath?? baseDir: System.IO.Path.Combine(FilesDir!.AbsolutePath, "Ryujinx");
                    else args[i]=null;
                }
                contentManager = c.Invoke(args);
                Log($"CM OK via ctor {pars.Length}");
                break;
            }catch(Exception ex){ Log($"CM ctor fail: {ex.InnerException?.Message?? ex.Message}"); }
        }

        object? userChannel = null;
        try{ userChannel = Activator.CreateInstance(ucpType, new object[] { true }); Log("UCP OK"); }
        catch{ try{ userChannel = Activator.CreateInstance(ucpType); Log("UCP OK default"); }catch(Exception ex){ Log($"UCP fail {ex.Message}"); } }

        var hleType = typeof(HleConfiguration);
        foreach (var ctor in hleType.GetConstructors().OrderByDescending(c => c.GetParameters().Length))
        {
            var pars = ctor.GetParameters();
            string sig = string.Join(", ", pars.Select(p=> $"{p.ParameterType.Name} {p.Name}"));
            Log($"Tentando HLE({sig})");
            var args = new object?[pars.Length];
            for(int i=0;i<pars.Length;i++){
                var pt = pars[i].ParameterType;
                var name = pars[i].Name?.ToLower()?? "";
                if(pt == typeof(VirtualFileSystem)) args[i]=vfs;
                else if(pt == cmType) args[i]=contentManager;
                else if(pt == ucpType) args[i]=userChannel;
                else if(name.Contains("content") && contentManager!=null && pt.IsAssignableFrom(contentManager.GetType())) args[i]=contentManager;
                else if(name.Contains("user") && userChannel!=null && pt.IsAssignableFrom(userChannel.GetType())) args[i]=userChannel;
                else if(pt.IsInstanceOfType(gpu) || pt.IsAssignableFrom(gpu.GetType()) || name.Contains("gpu") || name.Contains("render") || pt.Name.Contains("Renderer")) args[i]=gpu;
                else if(pt.IsInstanceOfType(audio) || pt.IsAssignableFrom(audio.GetType()) || name.Contains("audio")) args[i]=audio;
                else if(pt.IsEnum) args[i]=Enum.GetValues(pt).GetValue(0);
                else if(pt == typeof(string)) args[i]="";
                else if(pt == typeof(bool)) args[i]=false;
                else if(pt.IsValueType) args[i]=Activator.CreateInstance(pt);
                else args[i]=null;
            }
            try{
                var result = ctor.Invoke(args);
                Log($"HLE ctor {pars.Length} OK!");
                if(result is HleConfiguration hc) return hc;
                var configureMethod = hleType.GetMethod("Configure");
                if(configureMethod!=null){ var ret = configureMethod.Invoke(result,null); if(ret is HleConfiguration hc2) return hc2; }
                return (HleConfiguration)result;
            }catch(Exception ex){ Log($"Fail HLE {pars.Length}: {ex.InnerException?.Message?? ex.Message}"); }
        }
        throw new Exception("Construtor HleConfiguration compativel nao encontrado - todos falharam");
    }

    protected override void OnDestroy(){ running=false; try{ emuThread?.Join(2000);}catch{} base.OnDestroy(); }
}
