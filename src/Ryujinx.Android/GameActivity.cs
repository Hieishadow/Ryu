using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AFormat = Android.Graphics.Format;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.HLE.HOS.Services.Am.AppletOE.ApplicationProxyService.ApplicationProxy.Types;
using Ryujinx.HLE.UI;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.Audio.Backends.Dummy;
using Silk.NET.Vulkan;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using SysEnv = System.Environment;
using Switch = Ryujinx.HLE.Switch;

namespace DragoNX;

[Activity(Name = "com.ryubing.android.GameActivity", Label = "Ryubing", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = ScreenOrientation.Landscape, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden, Exported = false, MainLauncher = false)]
public class GameActivity : Activity
{
    const string TAG = "Ryubing";
    const BindingFlags CtorFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly string LogFile = "/storage/emulated/0/Download/Ryubing/ryubing_log.txt";

    string romPath = "";
    SurfaceView surfaceView = null!;
    TextView logView = null!;
    TextView fpsView = null!;
    Thread? emuThread;
    volatile bool running = false;
    IntPtr nativeWindow = IntPtr.Zero;
    Switch? device;
    VulkanRenderer? gpu;

    [DllImport("android")]
    static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);
    [DllImport("android")]
    static extern void ANativeWindow_acquire(IntPtr window);
    [DllImport("android")]
    static extern void ANativeWindow_release(IntPtr window);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
        romPath = Intent?.GetStringExtra("rom_path")?? "";

        try { File.WriteAllText(LogFile, $"=== Ryubing LOG {DateTime.Now} ===\nROM: {romPath}\n"); } catch {}

        if (string.IsNullOrEmpty(romPath) ||!File.Exists(romPath))
        {
            var dir = "/storage/emulated/0/Download/Ryubing/games";
            if (Directory.Exists(dir))
            {
                var first = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                 .FirstOrDefault(p => p.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".xci", StringComparison.OrdinalIgnoreCase));
                if (first!= null) romPath = first;
            }
        }

        surfaceView = new SurfaceView(this);
        logView = new TextView(this);
        logView.Text = $"RYUBING\n{Path.GetFileName(romPath)}\nExiste: {File.Exists(romPath)} {(File.Exists(romPath)? new FileInfo(romPath).Length / 1024 / 1024 : 0)}MB";
        logView.Gravity = GravityFlags.Left;
        logView.SetTextColor(global::Android.Graphics.Color.White);
        logView.SetBackgroundColor(global::Android.Graphics.Color.Black);
        logView.TextSize = 10;
        logView.SetPadding(20, 20, 20, 20);
        logView.MovementMethod = new Android.Text.Method.ScrollingMovementMethod();

        fpsView = new TextView(this) { Text = "FPS: --" };
        fpsView.SetTextColor(global::Android.Graphics.Color.Lime);
        fpsView.TextSize = 13;
        fpsView.SetPadding(20, 30, 20, 20);

        var root = new FrameLayout(this);
        root.AddView(surfaceView, new FrameLayout.LayoutParams(-1, -1));
        root.AddView(logView, new FrameLayout.LayoutParams(-1, -1));
        root.AddView(fpsView, new FrameLayout.LayoutParams(-2, -2) { Gravity = GravityFlags.Top | GravityFlags.Left });

        var btnLog = new Button(this) { Text = "Compartilhar LOG" };
        btnLog.Click += (s,e) => {
            var intent = new Android.Content.Intent(Android.Content.Intent.ActionSend);
            intent.PutExtra(Android.Content.Intent.ExtraText, logView.Text);
            intent.SetType("text/plain");
            StartActivity(Android.Content.Intent.CreateChooser(intent, "Log Ryubing"));
        };
        var btnParams = new FrameLayout.LayoutParams(-2,-2);
        btnParams.Gravity = GravityFlags.Bottom | GravityFlags.CenterHorizontal;
        root.AddView(btnLog, btnParams);

        SetContentView(root);
        surfaceView.Holder!.AddCallback(new SurfaceCallback(this));
    }

    void Log(string m)
    {
        global::Android.Util.Log.Info(TAG, m);
        try { File.AppendAllText(LogFile, DateTime.Now.ToString("HH:mm:ss") + " " + m + "\n"); } catch {}
        RunOnUiThread(() => { if(logView!=null) logView.Text += "\n" + m; });
    }
    void LogAppend(string m)
    {
        global::Android.Util.Log.Info(TAG, m);
        try { File.AppendAllText(LogFile, DateTime.Now.ToString("HH:mm:ss") + " " + m + "\n"); } catch {}
        RunOnUiThread(() => { if(logView!=null) logView.Text += "\n" + m; });
    }
    void LogError(string m)
    {
        global::Android.Util.Log.Error(TAG, m);
        try { File.AppendAllText(LogFile, DateTime.Now.ToString("HH:mm:ss") + " ERRO: " + m + "\n"); } catch {}
        RunOnUiThread(() => { if(logView!=null) { logView.Text += "\nERRO: " + m; logView.SetTextColor(global::Android.Graphics.Color.Red); } });
    }

    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
    {
        readonly GameActivity act;
        public SurfaceCallback(GameActivity a) => act = a;
        public void SurfaceCreated(ISurfaceHolder h)
        {
            var rect = h.SurfaceFrame;
            if (rect.Width() <= 0 || rect.Height() <= 0) return;
            act.nativeWindow = ANativeWindow_fromSurface(global::Android.Runtime.JNIEnv.Handle, h.Surface!.Handle);
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
            LogAppend($"Iniciando {Path.GetFileName(romPath)}");
            try { var tz = Java.Util.TimeZone.Default; LogAppend($"TimeZone: {tz.ID}"); } catch { Java.Util.TimeZone.Default = Java.Util.TimeZone.GetTimeZone("UTC"); LogAppend("TimeZone fallback UTC"); }
            string baseDir = Path.Combine(FilesDir!.AbsolutePath, "Ryujinx");
            string systemDir = Path.Combine(baseDir, "system");
            Directory.CreateDirectory(systemDir);
            CopyKeys(baseDir, systemDir);
            CopyFirmware(baseDir);
            InitAppData(baseDir);
            string jitDir = Path.Combine(CacheDir!.AbsolutePath, "jit");
            Directory.CreateDirectory(jitDir);
            SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE", jitDir);
            LogAppend("Criando VFS...");
            VirtualFileSystem vfs;
            try { vfs = VirtualFileSystem.CreateInstance(); LogAppend("VFS Criado"); }
            catch (Exception ex)
            {
                LogAppend($"CreateInstance falhou: {ex.GetType().Name}: {ex.Message}");
                var prop = typeof(VirtualFileSystem).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var inst = prop?.GetValue(null) as VirtualFileSystem;
                if (inst == null) throw new Exception($"VFS null: {ex.Message}");
                vfs = inst; LogAppend("VFS reutilizado");
            }
            try { vfs.ReloadKeySet(); LogAppend("KeySet Reload OK"); } catch (Exception ex) { LogAppend($"KeySet reload falhou: {ex.Message}"); throw; }
            LogAppend("Criando VulkanRenderer...");
            gpu = VulkanRenderer.Create("Ryubing", (inst, vk) => { unsafe { var ci = new AndroidSurfaceCreateInfoKHR { SType = StructureType.AndroidSurfaceCreateInfoKhr, Window = (nint*)nativeWindow }; var fp = vk.GetInstanceProcAddr(inst, "vkCreateAndroidSurfaceKHR"); if (fp == IntPtr.Zero) throw new Exception("vkCreateAndroidSurfaceKHR nao encontrado"); var func = Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(fp); SurfaceKHR surf; var res = func(inst, &ci, null, &surf); if (res!= Silk.NET.Vulkan.Result.Success) throw new Exception($"vkCreateSurface falhou: {res}"); return surf; } }, () => new[] { "VK_KHR_surface", "VK_KHR_android_surface" });
            LogAppend("Vulkan OK");
            var audio = new DummyHardwareDeviceDriver();
            var hleConf = BuildHleConfigurationFIX(vfs, gpu, audio);
            LogAppend("HLE Config OK");

            // FIX TIMEOUT DETECTION
            device = null;
            var switchThread = new System.Threading.Thread(() => {
                try {
                    device = new Ryujinx.HLE.Switch(hleConf);
                    LogAppend("Switch criado THREAD");
                }
                catch (Exception ex) { LogAppend("ERRO Switch thread: " + ex); }
            });
            switchThread.IsBackground = true;
            switchThread.Start();
            bool finished = switchThread.Join(15000);
            if (!finished) {
                LogError("TIMEOUT no new Switch() - travou no Horizon");
                return;
            }
            if (device == null) { LogError("device null mesmo sem timeout"); return; }
            LogAppend("Switch criado");

            if (!device.LoadNsp(romPath)) throw new Exception("LoadNsp false");
            LogAppend("NSP OK");
            RunOnUiThread(() => { if(logView!=null) logView.Visibility = ViewStates.Gone; });
            var sw = System.Diagnostics.Stopwatch.StartNew(); int frames = 0;
            while (running) { device.ProcessFrame(); device.PresentFrame(() => { Thread.Sleep(1); }); frames++; if (sw.ElapsedMilliseconds >= 1000) { int f = frames; frames = 0; sw.Restart(); RunOnUiThread(() => { if(fpsView!=null) fpsView.Text = $"FPS: {f}"; }); } }
        }
        catch (Exception ex) { LogError($"ERRO:\n{ex.Message}\n{ex}"); }
        finally { try { device?.Dispose(); } catch { } try { gpu?.Dispose(); } catch { } }
    }

    void CopyKeys(string baseDir, string systemDir)
    {
        string extKeys = "/storage/emulated/0/Download/Ryubing/keys";
        string legacyKeys = "/storage/emulated/0/Download/DragoNX/keys";
        string keysDir = Path.Combine(baseDir, "keys");
        Directory.CreateDirectory(keysDir);
        foreach (var name in new[] { "prod.keys", "title.keys" })
        {
            string src = Path.Combine(extKeys, name);
            if (!File.Exists(src)) src = Path.Combine(legacyKeys, name);
            if (!File.Exists(src)) { LogAppend($"ATENCAO {name} nao encontrado"); continue; }
            try { File.Copy(src, Path.Combine(systemDir, name), true); File.Copy(src, Path.Combine(keysDir, name), true); LogAppend($"{name} OK {new FileInfo(Path.Combine(systemDir, name)).Length}b"); }
            catch (Exception ex) { LogAppend($"{name} erro: {ex.Message}"); }
        }
    }

    void CopyFirmware(string baseDir)
    {
        try
        {
            string fwSrc = "/storage/emulated/0/Download/Ryubing/firmware";
            string fwDst = Path.Combine(baseDir, "bis", "system", "Contents", "registered");
            Directory.CreateDirectory(fwDst);
            if (!Directory.Exists(fwSrc)) { LogAppend("Firmware: pasta nao existe"); return; }
            var ncas = Directory.GetFiles(fwSrc, "*.nca"); int copied = 0;
            foreach (var nca in ncas) { string dst = Path.Combine(fwDst, Path.GetFileName(nca)); if (File.Exists(dst) && new FileInfo(dst).Length == new FileInfo(nca).Length) continue; File.Copy(nca, dst, true); copied++; }
            LogAppend($"Firmware: {copied}/{ncas.Length} novos");
        }
        catch (Exception ex) { LogAppend($"Firmware erro: {ex.Message}"); }
    }

    void InitAppData(string baseDir)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(baseDir, "bis", "user", "save"));
            Directory.CreateDirectory(Path.Combine(baseDir, "bis", "user", "saveMeta"));
            Directory.CreateDirectory(Path.Combine(baseDir, "system", "save"));
            Directory.CreateDirectory(Path.Combine(baseDir, "bis", "system", "save"));
            Directory.CreateDirectory(Path.Combine(baseDir, "bis", "system", "saveMeta"));

            // FIX: cria TODOS os system saves que o Horizon tenta montar
            for (ulong i = 0x8000000000000000; i <= 0x8000000000000035; i++) {
                try {
                    var dir = Path.Combine(baseDir, "bis", "user", "save", i.ToString("x16"));
                    Directory.CreateDirectory(dir);
                    var p = Path.Combine(dir, "0");
                    if (!File.Exists(p)) File.WriteAllBytes(p, new byte[1]);
                } catch {}
            }

            var appDataType = typeof(AppDataManager);
            var initMethod = appDataType.GetMethod("Initialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (initMethod == null) { LogAppend("AppData.Initialize nao encontrado"); return; }
            var pms = initMethod.GetParameters();
            if (pms.Length == 1) initMethod.Invoke(null, new object[] { baseDir });
            else if (pms.Length == 2)
            {
                object mode; var enumType = pms[1].ParameterType;
                if (Enum.TryParse(enumType, "User", out var m1)) mode = m1!;
                else if (Enum.TryParse(enumType, "UserProfile", out var m2)) mode = m2!;
                else mode = Enum.GetValues(enumType).GetValue(0)!;
                initMethod.Invoke(null, new object[] { baseDir, mode });
            }
            LogAppend($"AppData Base: {AppDataManager.BaseDirPath}");
        }
        catch (Exception ex) { LogAppend($"AppData init: {ex.Message}"); }
    }

    unsafe delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(Instance i, AndroidSurfaceCreateInfoKHR* p, AllocationCallbacks* a, SurfaceKHR* s);

    HleConfiguration BuildHleConfigurationFIX(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio)
    {
        var cmType = typeof(ContentManager);
        var ucpType = typeof(UserChannelPersistence);
        var lhmType = typeof(LibHacHorizonManager);
        var amType = typeof(AccountManager);
        LogAppend($"Tipos: CM={cmType.FullName} | UCP={ucpType.FullName} | LHM={lhmType.FullName} | AM={amType.FullName}");

        object? contentManager = null;
        foreach (var c in cmType.GetConstructors(CtorFlags).OrderByDescending(x => x.GetParameters().Length))
        {
            try
            {
                var pars = c.GetParameters(); var args = new object?[pars.Length];
                for (int i = 0; i < pars.Length; i++) { if (pars[i].ParameterType == typeof(VirtualFileSystem)) args[i] = vfs; else if (pars[i].ParameterType == typeof(string)) args[i] = AppDataManager.BaseDirPath?? ""; else args[i] = null; }
                contentManager = c.Invoke(args); LogAppend($"CM OK {pars.Length}"); break;
            }
            catch (Exception ex) { LogAppend($"CM fail: {ex.InnerException?.Message?? ex.Message}"); }
        }
        if (contentManager == null) throw new Exception("ContentManager falhou");

        object? userChannel = null;
        foreach (var c in ucpType.GetConstructors(CtorFlags).OrderBy(x => x.GetParameters().Length))
        {
            try
            {
                var pars = c.GetParameters(); var args = new object?[pars.Length];
                for (int i = 0; i < pars.Length; i++) { if (pars[i].ParameterType == typeof(bool)) args[i] = true; else if (pars[i].ParameterType.IsValueType) args[i] = Activator.CreateInstance(pars[i].ParameterType); else args[i] = null; }
                userChannel = c.Invoke(args); LogAppend($"UCP OK ctor {pars.Length}"); break;
            }
            catch (Exception ex) { LogAppend($"UCP fail: {ex.InnerException?.Message?? ex.Message}"); }
        }
        if (userChannel == null) userChannel = Activator.CreateInstance(ucpType, true)!;

        object? libHac = null;
        foreach (var c in lhmType.GetConstructors(CtorFlags).OrderByDescending(x => x.GetParameters().Length))
        {
            try
            {
                var pars = c.GetParameters(); var args = new object?[pars.Length];
                for (int i = 0; i < pars.Length; i++) { if (pars[i].ParameterType == typeof(VirtualFileSystem)) args[i] = vfs; else if (pars[i].ParameterType == typeof(string)) args[i] = AppDataManager.BaseDirPath?? ""; else if (pars[i].ParameterType.IsValueType) args[i] = Activator.CreateInstance(pars[i].ParameterType); else args[i] = null; }
                libHac = c.Invoke(args); LogAppend($"LHM OK {pars.Length}"); break;
            }
            catch (Exception ex) { LogAppend($"LHM fail: {ex.InnerException?.Message?? ex.Message}"); }
        }
        if (libHac == null) throw new Exception("LibHacHorizonManager falhou");

        object? accountManager = null;
        foreach (var c in amType.GetConstructors(CtorFlags).OrderByDescending(x => x.GetParameters().Length))
        {
            try
            {
                var pars = c.GetParameters(); var args = new object?[pars.Length];
                for (int i = 0; i < pars.Length; i++) { if (pars[i].ParameterType == typeof(VirtualFileSystem)) args[i] = vfs; else if (pars[i].ParameterType == typeof(string)) args[i] = AppDataManager.BaseDirPath?? ""; else if (pars[i].ParameterType.IsValueType) args[i] = Activator.CreateInstance(pars[i].ParameterType); else args[i] = null; }
                accountManager = c.Invoke(args); LogAppend($"AM OK {pars.Length}"); break;
            }
            catch (Exception ex) { LogAppend($"AM fail: {ex.InnerException?.Message?? ex.Message}"); }
        }
        if (accountManager == null) throw new Exception("AccountManager falhou");

        var dummyUI = new DummyHostUIHandler();
        var hleConfType = typeof(HleConfiguration);
        var ctor = hleConfType.GetConstructors(CtorFlags).OrderByDescending(c => c.GetParameters().Length).First();
        var ctorPars = ctor.GetParameters();
        var ctorArgs = new object?[ctorPars.Length];
        for (int i = 0; i < ctorPars.Length; i++)
        {
            var pt = ctorPars[i].ParameterType;
            if (pt == typeof(string)) ctorArgs[i] = ctorPars[i].Name!.ToLower().Contains("timezone")? "UTC" : "";
            else if (pt == typeof(bool)) ctorArgs[i] = true;
            else if (pt == typeof(int) || pt == typeof(long) || pt == typeof(uint) || pt == typeof(ulong)) ctorArgs[i] = Convert.ChangeType(1, pt);
            else if (pt == typeof(float) || pt == typeof(double)) ctorArgs[i] = Convert.ChangeType(1f, pt);
            else if (pt.IsEnum) { var names = Enum.GetNames(pt); string pick = names.FirstOrDefault(n => n == "AmericanEnglish" || n == "USA" || n == "None" || n == "Disabled" || n == "Switch" || n.Contains("4GiB"))?? names[0]; ctorArgs[i] = Enum.Parse(pt, pick); }
            else if (pt.IsArray) ctorArgs[i] = Array.CreateInstance(pt.GetElementType()!, 0);
            else if (pt.IsValueType) ctorArgs[i] = Activator.CreateInstance(pt);
            else ctorArgs[i] = null;
        }
        var hleConf = (HleConfiguration)ctor.Invoke(ctorArgs);
        LogAppend("HleConfiguration OK via reflection");
        return hleConf.Configure(vfs, (LibHacHorizonManager)libHac, (ContentManager)contentManager, (AccountManager)accountManager, (UserChannelPersistence)userChannel, gpu, audio, dummyUI);
    }

    class DummyHostUIHandler : IHostUIHandler
    {
        public IHostUITheme HostUITheme => null!;
        public bool DisplayInputDialog(SoftwareKeyboardUIArgs args, out string userText) { userText = ""; return false; }
        public bool DisplayMessageDialog(string title, string message) => false;
        public bool DisplayMessageDialog(ControllerAppletUIArgs args) => false;
        public bool DisplayCabinetDialog(out string userText) { userText = ""; return false; }
        public void DisplayCabinetMessageDialog() { }
        public void ExecuteProgram(Switch device, ProgramSpecifyKind kind, ulong value) { }
        public bool DisplayErrorAppletDialog(string title, string message, string[] buttonsText, (uint Module, uint Description)? errorCode = null) => false;
        public IDynamicTextInputHandler CreateDynamicTextInputHandler() => null!;
        public UserProfile ShowPlayerSelectDialog() => null!;
        public void TakeScreenshot() { }
    }

    protected override void OnDestroy() { running = false; try { emuThread?.Join(2000); } catch { } base.OnDestroy(); }
}
