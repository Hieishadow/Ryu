using Android.App;
using Android.OS;
using Android.Views;
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

namespace Ryujinx.Android;

[Activity(Label = "Ryubing", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
public class GameActivity : Activity
{
    const string TAG = "Ryubing";
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
        Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
        romPath = Intent?.GetStringExtra("rom_path")?? "";
        if (string.IsNullOrEmpty(romPath) ||!File.Exists(romPath))
        {
            var dir = "/storage/emulated/0/Download/Ryubing/games";
            if (Directory.Exists(dir))
            {
                var first = Directory.GetFiles(dir, "*.nsp").Concat(Directory.GetFiles(dir, "*.xci")).FirstOrDefault();
                if (first!= null) romPath = first;
            }
        }
        surfaceView = new SurfaceView(this);
        logView = new TextView(this);
        logView.Text = $"RYUBING\n{Path.GetFileName(romPath)}\nExiste: {File.Exists(romPath)} {(File.Exists(romPath)? new FileInfo(romPath).Length / 1024 / 1024 : 0)}MB";
        logView.Gravity = GravityFlags.Center;
        logView.SetTextColor(Android.Graphics.Color.White);
        logView.SetBackgroundColor(Android.Graphics.Color.Black);
        fpsView = new TextView(this);
        fpsView.Text = "FPS: --";
        fpsView.SetTextColor(Android.Graphics.Color.Lime);
        fpsView.TextSize = 13;
        fpsView.SetPadding(20, 30, 20, 20);
        var root = new Android.Widget.FrameLayout(this);
        root.AddView(surfaceView, new Android.Widget.FrameLayout.LayoutParams(-1, -1));
        root.AddView(logView, new Android.Widget.FrameLayout.LayoutParams(-1, -1));
        root.AddView(fpsView, new Android.Widget.FrameLayout.LayoutParams(-2, -2));
        SetContentView(root);
        surfaceView.Holder!.AddCallback(new SurfaceCallback(this));
    }

    void Log(string msg) { Android.Util.Log.Info(TAG, msg); RunOnUiThread(() => logView.Text += "\n" + msg); }
    void LogError(string msg) { Android.Util.Log.Error(TAG, msg); RunOnUiThread(() => { logView.Text += "\n" + msg; logView.SetTextColor(Android.Graphics.Color.Red); }); }

    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
    {
        readonly GameActivity act;
        public SurfaceCallback(GameActivity a) => act = a;
        public void SurfaceCreated(ISurfaceHolder holder)
        {
            act.nativeWindow = ANativeWindow_fromSurface(Android.Runtime.JNIEnv.Handle, holder.Surface!.Handle);
            if (act.nativeWindow == IntPtr.Zero) { act.LogError("ANativeWindow Zero!"); return; }
            ANativeWindow_acquire(act.nativeWindow);
            if (act.emuThread == null ||!act.emuThread.IsAlive)
            {
                act.running = true;
                act.emuThread = new Thread(act.EmulationLoop) { IsBackground = true, Priority = System.Threading.ThreadPriority.Highest, Name = "RyujinxEmu" };
                act.emuThread.Start();
            }
        }
        public void SurfaceChanged(ISurfaceHolder h, AFormat f, int w, int ht) { }
        public void SurfaceDestroyed(ISurfaceHolder h) { act.running = false; if (act.nativeWindow!= IntPtr.Zero) { ANativeWindow_release(act.nativeWindow); act.nativeWindow = IntPtr.Zero; } }
    }

    void EmulationLoop()
    {
        try
        {
            Log($"Iniciando {Path.GetFileName(romPath)}");

            // FIX 1: Android 13 TimeZone - seu S20 FE
            try {
                var tz = Java.Util.TimeZone.Default;
                Log($"TimeZone: {tz.ID}");
            } catch {
                Java.Util.TimeZone.Default = Java.Util.TimeZone.GetTimeZone("UTC");
                Log("TimeZone fallback UTC");
            }

            string baseDir = Path.Combine(FilesDir!.AbsolutePath, "Ryujinx");
            string systemDir = Path.Combine(baseDir, "system");
            Directory.CreateDirectory(systemDir);
            Directory.CreateDirectory(Path.Combine(baseDir, "bis", "user", "save", "8000000000000010"));
            Directory.CreateDirectory(Path.Combine(baseDir, "bis", "system", "save", "8000000000000060"));
            Directory.CreateDirectory(Path.Combine(baseDir, "nand", "user", "save"));
            Directory.CreateDirectory(Path.Combine(baseDir, "sdcard", "Nintendo", "Contents", "registered"));

            // FIX 2: prod.keys no lugar certo: system/prod.keys (não keys/)
            string prodOrig = "/storage/emulated/0/Download/Ryubing/keys/prod.keys";
            if (!File.Exists(prodOrig)) prodOrig = "/storage/emulated/0/Download/DragoNX/keys/prod.keys"; // compat velho
            string prodDest = Path.Combine(systemDir, "prod.keys");
            if (File.Exists(prodOrig))
            {
                File.Copy(prodOrig, prodDest, true);
                // copia também pra keys/ por compatibilidade
                string keysDir = Path.Combine(baseDir, "keys");
                Directory.CreateDirectory(keysDir);
                File.Copy(prodOrig, Path.Combine(keysDir, "prod.keys"), true);
                Log($"prod.keys OK {new FileInfo(prodDest).Length}b em system/");
            }
            else
            {
                Log($"ATENÇÃO prod.keys não achada, tentando {prodDest} existe={File.Exists(prodDest)}");
            }

            try {
                var appDataType = typeof(AppDataManager);
                var initMethod = appDataType.GetMethod("Initialize", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if(initMethod!=null){
                    var p = initMethod.GetParameters();
                    if(p.Length==1) initMethod.Invoke(null, new object[]{ baseDir });
                    else if(p.Length==2) initMethod.Invoke(null, new object[]{ baseDir, AppDataManager.LaunchMode.UserProfile });
                }
                Log($"AppData Base: {AppDataManager.BaseDirPath}");
            } catch(Exception ex){ Log($"AppData init: {ex.Message}"); }

            string jitDir = Path.Combine(CacheDir!.AbsolutePath, "jit");
            Directory.CreateDirectory(jitDir);
            SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE", jitDir);

            VirtualFileSystem vfs;
            try { vfs = VirtualFileSystem.CreateInstance(); Log("VFS Criado"); }
            catch {
                var prop = typeof(VirtualFileSystem).GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                vfs = (VirtualFileSystem)prop!.GetValue(null)!;
                Log("VFS reutilizado");
            }

            Log("Criando VulkanRenderer...");
            gpu = VulkanRenderer.Create("Ryubing", (inst, vk) => {
                unsafe {
                    var ci = new AndroidSurfaceCreateInfoKHR { SType = StructureType.AndroidSurfaceCreateInfoKhr, Window = (nint*)nativeWindow };
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
            var hleConf = BuildHleConfiguration(vfs, gpu, audio);
            Log("HLE Config OK");

            // FIX 3: Força reload das keys antes do Horizon
            try { vfs.ReloadKeySet(); Log("KeySet Reload OK"); } catch(Exception ex){ Log($"KeySet reload: {ex.Message}"); }

            device = new Ryujinx.HLE.Switch(hleConf);
            Log("Switch criado - Horizon OK");
            Log("Loading NSP...");
            if (!device.LoadNsp(romPath)) throw new Exception("LoadNsp false");
            Log("NSP OK - INICIANDO JOGO");
            RunOnUiThread(() => logView.Visibility = ViewStates.Gone);
            var sw = System.Diagnostics.Stopwatch.StartNew(); int frames = 0;
            while (running) {
                device.ProcessFrame();
                device.PresentFrame(() => { });
                frames++;
                if (sw.ElapsedMilliseconds >= 1000) { int f = frames; frames = 0; sw.Restart(); RunOnUiThread(() => fpsView.Text = $"FPS: {f}"); }
            }
        } catch (Exception ex) { LogError($"ERRO:\n{ex.Message}\n{ex}"); }
        finally { try { device?.Dispose(); } catch { } try { gpu?.Dispose(); } catch { } }
    }

    delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(Instance i, AndroidSurfaceCreateInfoKHR* p, AllocationCallbacks* a, SurfaceKHR* s);

    HleConfiguration BuildHleConfiguration(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio)
    {
        // FIX 4: ContentManager com VFS real, não null
        object? contentManager = null;
        try {
            var allTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>{try{return a.GetTypes();}catch{return Array.Empty<Type>();}}).ToList();
            var cmType = allTypes.FirstOrDefault(t=>t.Name=="ContentManager");
            if(cmType!=null){
                var cmCtor = cmType.GetConstructors().OrderByDescending(c=>c.GetParameters().Length).FirstOrDefault();
                if(cmCtor!=null){
                    var cmParams = cmCtor.GetParameters();
                    object?[] cmArgs = new object?[cmParams.Length];
                    for(int j=0;j<cmParams.Length;j++){
                        var pt = cmParams[j].ParameterType;
                        if(pt==typeof(string)) cmArgs[j]=AppDataManager.BaseDirPath;
                        else if(pt==typeof(VirtualFileSystem)) cmArgs[j]=vfs;
                        else if(pt.IsValueType) cmArgs[j]=Activator.CreateInstance(pt);
                        else cmArgs[j]=null;
                    }
                    contentManager = cmCtor.Invoke(cmArgs);
                    Log($"ContentManager: {cmType.FullName} criado com VFS");
                }
            }
        } catch(Exception ex){ Log($"ContentManager: {ex.Message}"); }

        var hleType = typeof(HleConfiguration);
        var ctor = hleType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var pars = ctor.GetParameters(); object?[] args = new object?[pars.Length];
        for (int i = 0; i < pars.Length; i++) {
            var pt = pars[i].ParameterType;
            if (pt.IsEnum) args[i] = Enum.GetValues(pt).GetValue(0);
            else if (pt == typeof(string)) args[i] = "";
            else if (pt == typeof(bool)) args[i] = false;
            else if (pt.IsValueType) args[i] = Activator.CreateInstance(pt);
            else args[i] = null;
        }
        var cfgObj = ctor.Invoke(args);
        var userChannel = Activator.CreateInstance(hleType.GetProperty("UserChannelPersistence")!.PropertyType, true);
        var configureMethod = hleType.GetMethod("Configure");
        var configureParams = configureMethod!.GetParameters();
        object?[] configArgs = new object?[configureParams.Length];
        for(int i=0;i<configureParams.Length;i++){
            var pType = configureParams[i].ParameterType;
            if(pType==typeof(VirtualFileSystem)) configArgs[i]=vfs;
            else if(pType.Name=="ContentManager") configArgs[i]=contentManager;
            else if(pType.Name.Contains("UserChannel")) configArgs[i]=userChannel;
            else if(pType.IsAssignableFrom(gpu.GetType()) || pType.Name.Contains("Renderer") || pType.Name.Contains("IGpu")) configArgs[i]=gpu;
            else if(pType.IsAssignableFrom(audio.GetType()) || pType.Name.Contains("Audio")) configArgs[i]=audio;
            else if(pType.IsValueType) configArgs[i]=Activator.CreateInstance(pType);
            else configArgs[i]=null;
        }
        var res = configureMethod.Invoke(cfgObj, configArgs);
        return (HleConfiguration)res!;
    }
    protected override void OnDestroy() { running = false; try { emuThread?.Join(2000); } catch { } base.OnDestroy(); }
}
