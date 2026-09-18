using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AFormat = Android.Graphics.Format;
using Ryujinx.HLE;
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
using Ryujinx.HLE.FileSystem;

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

        surfaceView = new SurfaceView(this);
        logView = new TextView(this) { Text = $"RYUBING\n{Path.GetFileName(romPath)}\n" };
        logView.Gravity = GravityFlags.Center;
        logView.SetTextColor(global::Android.Graphics.Color.White);
        logView.SetBackgroundColor(global::Android.Graphics.Color.Black);
        fpsView = new TextView(this) { Text = "FPS: --" };
        fpsView.SetTextColor(global::Android.Graphics.Color.Lime);
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
            // #2 mantido como você pediu - JNIEnv.Handle é safe na UI thread
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

            CopyKeys(baseDir, systemDir);
            CopyFirmware(baseDir);
            InitAppData(baseDir);

            string jitDir = Path.Combine(CacheDir!.AbsolutePath, "jit");
            Directory.CreateDirectory(jitDir);
            SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE", jitDir);

            VirtualFileSystem vfs;
            try { vfs = VirtualFileSystem.CreateInstance(); }
            catch
            {
                var prop = typeof(VirtualFileSystem).GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                vfs = (VirtualFileSystem)prop!.GetValue(null)!;
            }
            try { vfs.ReloadKeySet(); Log("KeySet Reload OK"); } catch (Exception ex){ Log($"KeySet: {ex.Message}"); }

            Log("Criando VulkanRenderer...");
            gpu = VulkanRenderer.Create("Ryubing", (inst, vk) =>
            {
                unsafe
                {
                    var ci = new AndroidSurfaceCreateInfoKHR{ SType = StructureType.AndroidSurfaceCreateInfoKhr, Window = (nint*)nativeWindow };
                    var fp = vk.GetInstanceProcAddr(inst, "vkCreateAndroidSurfaceKHR");
                    var func = Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(fp);
                    SurfaceKHR surf; var res = func(inst, &ci, null, &surf);
                    if (res!= Silk.NET.Vulkan.Result.Success) throw new Exception($"vkCreateSurface falhou: {res}");
                    return surf;
                }
            }, () => new[] { "VK_KHR_surface", "VK_KHR_android_surface" });

            var audio = new DummyHardwareDeviceDriver();
            var hleConf = BuildHleConfigurationFIX(vfs, gpu, audio);
            device = new Ryujinx.HLE.Switch(hleConf);
            Log("Switch criado");

            if (!device.LoadNsp(romPath)) throw new Exception("LoadNsp false");
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
        Directory.CreateDirectory(Path.Combine(baseDir, "keys"));
        foreach(var name in new[] { "prod.keys", "title.keys" })
        {
            var src = Path.Combine(extKeys, name);
            if(!File.Exists(src)) continue;
            File.Copy(src, Path.Combine(systemDir, name), true);
            File.Copy(src, Path.Combine(baseDir, "keys", name), true);
            Log($"{name} OK");
        }
    }
    void CopyFirmware(string baseDir){ /* mesmo seu código com skip por tamanho */
        try{
            var fwSrc = Path.Combine(ExternalBase, "firmware");
            var fwDst = Path.Combine(baseDir, "bis", "system", "Contents", "registered");
            Directory.CreateDirectory(fwDst);
            if(!Directory.Exists(fwSrc)) return;
            foreach(var nca in Directory.GetFiles(fwSrc, "*.nca")){
                var dst = Path.Combine(fwDst, Path.GetFileName(nca));
                if(File.Exists(dst) && new FileInfo(dst).Length == new FileInfo(nca).Length) continue;
                File.Copy(nca, dst, true);
            }
            Log("Firmware OK");
        }catch(Exception ex){ Log($"Firmware: {ex.Message}"); }
    }

    void InitAppData(string baseDir)
    {
        try{
            var appDataType = typeof(AppDataManager);
            var initMethod = appDataType.GetMethod("Initialize", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if(initMethod == null) return;
            var p = initMethod.GetParameters();
            if(p.Length == 1) initMethod.Invoke(null, new object[] { baseDir });
            else if(p.Length == 2)
            {
                // #3 seu fix com TryParse + fallback
                object mode;
                var enumType = p[1].ParameterType;
                if (Enum.TryParse(enumType, "User", out var m1)) mode = m1!;
                else if (Enum.TryParse(enumType, "UserProfile", out var m2)) mode = m2!;
                else mode = Enum.GetValues(enumType).GetValue(0)!;
                initMethod.Invoke(null, new object[] { baseDir, mode });
            }
            Log($"AppData: {AppDataManager.BaseDirPath}");
        }catch(Exception ex){ Log($"AppData: {ex.Message}"); }
    }

    delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(Instance i, AndroidSurfaceCreateInfoKHR* p, AllocationCallbacks* a, SurfaceKHR* s);

    HleConfiguration BuildHleConfigurationFIX(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio)
    {
        var allTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try{ return a.GetTypes(); }catch{ return Array.Empty<Type>(); }}).ToList();
        var cmType = allTypes.FirstOrDefault(t => t.Name == "ContentManager")?? throw new Exception("ContentManager nao encontrado");
        var ucpType = allTypes.FirstOrDefault(t => t.Name == "UserChannelPersistence")?? throw new Exception("UserChannelPersistence nao encontrado");

        // #1 seu fix por tipo real
        var rendererInterface = gpu.GetType().GetInterfaces().FirstOrDefault(i => i.Name is "IRenderer" or "IGpu");
        var hleType = typeof(HleConfiguration);

        var ctor = hleType.GetConstructors()
           .Where(c => c.GetParameters().Any(par => par.ParameterType == typeof(VirtualFileSystem)))
           .Where(c => rendererInterface == null || c.GetParameters().Any(par => par.ParameterType.IsAssignableFrom(rendererInterface) || par.ParameterType == rendererInterface || par.ParameterType.IsAssignableFrom(gpu.GetType())))
           .OrderByDescending(c => c.GetParameters().Length)
           .FirstOrDefault()
           ?? throw new Exception("Construtor HleConfiguration compativel nao encontrado");

        object contentManager;
        var cmCtorVfs = cmType.GetConstructor(new[] { typeof(VirtualFileSystem) });
        if (cmCtorVfs!= null) contentManager = cmCtorVfs.Invoke(new object[] { vfs });
        else contentManager = cmType.GetConstructors().OrderByDescending(c=>c.GetParameters().Length).First().Invoke(new object[] { AppDataManager.BaseDirPath, vfs });

        var userChannel = Activator.CreateInstance(ucpType, new object[] { true })!;
        var pars = ctor.GetParameters();
        object?[] args = new object?[pars.Length];
        for(int i=0;i<pars.Length;i++){
            var pt = pars[i].ParameterType; var name = pars[i].Name?.ToLower()?? "";
            if(pt == typeof(VirtualFileSystem)) args[i]=vfs;
            else if(pt == cmType || name.Contains("content")) args[i]=contentManager;
            else if(pt == ucpType || name.Contains("userchannel")) args[i]=userChannel;
            else if(pt.IsAssignableFrom(gpu.GetType()) || (rendererInterface!= null && pt.IsAssignableFrom(rendererInterface))) args[i]=gpu;
            else if(pt.IsAssignableFrom(audio.GetType()) || name.Contains("audio")) args[i]=audio;
            else if(pt.IsEnum) args[i]=Enum.GetValues(pt).GetValue(0);
            else if(pt == typeof(string)) args[i]="";
            else if(pt == typeof(bool)) args[i]=false;
            else if(pt.IsValueType) args[i]=Activator.CreateInstance(pt);
            else args[i]=null;
        }
        var cfgObj = ctor.Invoke(args);
        var configureMethod = hleType.GetMethod("Configure");
        if(configureMethod!= null){ var ret = configureMethod.Invoke(cfgObj, null); if(ret is HleConfiguration hc) return hc; }
        return (HleConfiguration)cfgObj;
    }

    protected override void OnDestroy(){ running=false; try{ emuThread?.Join(2000);}catch{} base.OnDestroy(); }
}
