using Android.App;
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
        logView.SetTextColor(global::Android.Graphics.Color.White);
        logView.SetBackgroundColor(global::Android.Graphics.Color.Black);
        fpsView = new TextView(this);
        fpsView.Text = "FPS: --";
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

    void Log(string msg) { global::Android.Util.Log.Info(TAG, msg); RunOnUiThread(() => logView.Text += "\n" + msg); }
    void LogError(string msg) { global::Android.Util.Log.Error(TAG, msg); RunOnUiThread(() => { logView.Text += "\n" + msg; logView.SetTextColor(global::Android.Graphics.Color.Red); }); }

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
            try { var tz = Java.Util.TimeZone.Default; Log($"TimeZone: {tz.ID}"); }
            catch { Java.Util.TimeZone.Default = Java.Util.TimeZone.GetTimeZone("UTC"); Log("TimeZone fallback UTC"); }

            string baseDir = Path.Combine(FilesDir!.AbsolutePath, "Ryujinx");
            string systemDir = Path.Combine(baseDir, "system");
            Directory.CreateDirectory(systemDir);
            Directory.CreateDirectory(Path.Combine(baseDir, "bis", "user", "save", "8000000000000010"));
            Directory.CreateDirectory(Path.Combine(baseDir, "bis", "system", "save", "8000000000000060"));
            Directory.CreateDirectory(Path.Combine(baseDir, "nand", "user", "save"));
            Directory.CreateDirectory(Path.Combine(baseDir, "sdcard", "Nintendo", "Contents", "registered"));

            // KEYS
            string prodOrig = "/storage/emulated/0/Download/Ryubing/keys/prod.keys";
            if (!File.Exists(prodOrig)) prodOrig = "/storage/emulated/0/Download/DragoNX/keys/prod.keys";
            string prodDest = Path.Combine(systemDir, "prod.keys");
            if (File.Exists(prodOrig))
            {
                File.Copy(prodOrig, prodDest, true);
                string keysDir = Path.Combine(baseDir, "keys");
                Directory.CreateDirectory(keysDir);
                File.Copy(prodOrig, Path.Combine(keysDir, "prod.keys"), true);
                Log($"prod.keys OK {new FileInfo(prodDest).Length}b");
            }
            else Log($"ATENCAO prod.keys nao achada, existe={File.Exists(prodDest)}");

            // FIRMWARE
            try {
                string fwSrc = "/storage/emulated/0/Download/Ryubing/firmware";
                string fwDst = Path.Combine(baseDir, "bis", "system", "Contents", "registered");
                Directory.CreateDirectory(fwDst);
                if (Directory.Exists(fwSrc))
                {
                    var ncas = Directory.GetFiles(fwSrc, "*.nca");
                    foreach (var nca in ncas) File.Copy(nca, Path.Combine(fwDst, Path.GetFileName(nca)), true);
                    Log($"Firmware: {ncas.Length}.nca copiados");
                }
            } catch (Exception ex) { Log($"Firmware erro: {ex.Message}"); }

            // AppData tem que vir ANTES do VFS
            try {
                var appDataType = typeof(AppDataManager);
                var initMethod = appDataType.GetMethod("Initialize", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (initMethod!= null)
                {
                    var p = initMethod.GetParameters();
                    if (p.Length == 1) initMethod.Invoke(null, new object[] { baseDir });
                    else if (p.Length == 2) initMethod.Invoke(null, new object[] { baseDir, AppDataManager.LaunchMode.UserProfile });
                }
                Log($"AppData Base: {AppDataManager.BaseDirPath}");
            } catch (Exception ex) { Log($"AppData init: {ex.Message}"); }

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

            try { vfs.ReloadKeySet(); Log("KeySet Reload OK"); } catch (Exception ex) { Log($"KeySet reload: {ex.Message}"); }

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
                if (sw.ElapsedMilliseconds >= 1000) { int f = frames; frames = 0; sw.Restart(); RunOnUiThread(() => fpsView.Text = $"FPS: {f}"); }
            }
        } catch (Exception ex) { LogError($"ERRO:\n{ex.Message}\n{ex}"); }
        finally { try { device?.Dispose(); } catch { } try { gpu?.Dispose(); } catch { } }
    }

    delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(Instance i, AndroidSurfaceCreateInfoKHR* p, AllocationCallbacks* a, SurfaceKHR* s);

    HleConfiguration BuildHleConfigurationFIX(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio)
    {
        var allTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } }).ToList();
        var cmType = allTypes.First(t => t.Name == "ContentManager");
        var ucpType = allTypes.First(t => t.Name == "UserChannelPersistence");

        object contentManager;
        var cmCtorVfs = cmType.GetConstructor(new[] { typeof(VirtualFileSystem) });
        if (cmCtorVfs!= null) contentManager = cmCtorVfs.Invoke(new object[] { vfs });
        else contentManager = cmType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First().Invoke(new object[] { AppDataManager.BaseDirPath, vfs });

        var userChannel = Activator.CreateInstance(ucpType, new object[] { true })!;

        var hleType = typeof(HleConfiguration);
        var ctor = hleType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var pars = ctor.GetParameters();
        object?[] args = new object?[pars.Length];
        for (int i = 0; i < pars.Length; i++)
        {
            var pt = pars[i].ParameterType;
            var name = pars[i].Name?.ToLower()?? "";
            if (pt == typeof(VirtualFileSystem) || name.Contains("filesystem") || name.Contains("vfs")) args[i] = vfs;
            else if (pt == cmType || name.Contains("content")) args[i] = contentManager;
            else if (pt == ucpType || name.Contains("userchannel") || name.Contains("persistence")) args[i] = userChannel;
            else if (pt.IsAssignableFrom(gpu.GetType()) || name.Contains("gpu") || name.Contains("renderer")) args[i] = gpu;
            else if (pt.IsAssignableFrom(audio.GetType()) || name.Contains("audio") || name.Contains("audiorenderer")) args[i] = audio;
            else if (pt.IsEnum) args[i] = Enum.GetValues(pt).GetValue(0);
            else if (pt == typeof(string)) args[i] = "";
            else if (pt == typeof(bool)) args[i] = false;
            else if (pt.IsValueType) args[i] = Activator.CreateInstance(pt);
            else args[i] = null;
        }

        var cfgObj = ctor.Invoke(args);
        var configureMethod = hleType.GetMethod("Configure");
        if (configureMethod!= null)
        {
            var ret = configureMethod.Invoke(cfgObj, null);
            if (ret is HleConfiguration hc) return hc;
        }
        return (HleConfiguration)cfgObj;
    }

    protected override void OnDestroy() { running = false; try { emuThread?.Join(2000); } catch { } base.OnDestroy(); }
}
