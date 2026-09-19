using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AFormat = Android.Graphics.Format;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Applets;
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
using System.Runtime.InteropServices;
using System.Threading;
using SysEnv = System.Environment;
using Switch = Ryujinx.HLE.Switch;

namespace DragoNX;

[Activity(Name = "com.ryubing.android.GameActivity", Label = "Ryubing", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = ScreenOrientation.Landscape, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden, Exported = false, MainLauncher = false)]
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

        romPath = Intent?.GetStringExtra("rom_path") ?? "";

        if (string.IsNullOrEmpty(romPath) || !File.Exists(romPath))
        {
            var dir = "/storage/emulated/0/Download/Ryubing/games";

            if (Directory.Exists(dir))
            {
                var first = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                    .FirstOrDefault(p =>
                        p.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                        p.EndsWith(".xci", StringComparison.OrdinalIgnoreCase));

                if (first != null)
                    romPath = first;
            }
        }

        surfaceView = new SurfaceView(this);
        logView = new TextView(this);

        logView.Text =
            $"RYUBING\n{Path.GetFileName(romPath)}\n" +
            $"Existe: {File.Exists(romPath)} " +
            $"{(File.Exists(romPath) ? new FileInfo(romPath).Length / 1024 / 1024 : 0)}MB";

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

    void Log(string m)
    {
        global::Android.Util.Log.Info(TAG, m);
        RunOnUiThread(() => logView.Text += "\n" + m);
    }

    void LogError(string m)
    {
        global::Android.Util.Log.Error(TAG, m);

        RunOnUiThread(() =>
        {
            logView.Text += "\n" + m;
            logView.SetTextColor(global::Android.Graphics.Color.Red);
        });
    }

    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
    {
        readonly GameActivity act;

        public SurfaceCallback(GameActivity a) => act = a;

        public void SurfaceCreated(ISurfaceHolder h)
        {
            act.nativeWindow =
                ANativeWindow_fromSurface(
                    global::Android.Runtime.JNIEnv.Handle,
                    h.Surface!.Handle);

            if (act.nativeWindow == IntPtr.Zero)
            {
                act.LogError("ANativeWindow Zero!");
                return;
            }

            ANativeWindow_acquire(act.nativeWindow);

            if (act.emuThread == null || !act.emuThread.IsAlive)
            {
                act.running = true;

                act.emuThread = new Thread(act.EmulationLoop)
                {
                    IsBackground = true,
                    Priority = System.Threading.ThreadPriority.Highest,
                    Name = "RyujinxEmu"
                };

                act.emuThread.Start();
            }
        }

        public void SurfaceChanged(ISurfaceHolder h, AFormat f, int w, int ht)
        {
        }

        public void SurfaceDestroyed(ISurfaceHolder h)
        {
            act.running = false;

            if (act.nativeWindow != IntPtr.Zero)
            {
                ANativeWindow_release(act.nativeWindow);
                act.nativeWindow = IntPtr.Zero;
            }
        }
    }

    void EmulationLoop()
    {
        try
        {
            Log($"Iniciando {Path.GetFileName(romPath)}");

            try
            {
                var tz = Java.Util.TimeZone.Default;
                Log($"TimeZone: {tz.ID}");
            }
            catch
            {
                Java.Util.TimeZone.Default =
                    Java.Util.TimeZone.GetTimeZone("UTC");

                Log("TimeZone fallback UTC");
            }

            string baseDir = Path.Combine(FilesDir!.AbsolutePath, "Ryujinx");
            string systemDir = Path.Combine(baseDir, "system");

            Directory.CreateDirectory(systemDir);
            Directory.CreateDirectory(
                Path.Combine(baseDir, "bis", "user", "save", "8000000000000010"));

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
                Log("VFS Criado");
            }
            catch (Exception ex)
            {
                Log($"CreateInstance falhou: {ex.GetType().Name}: {ex.Message}");

                var prop = typeof(VirtualFileSystem).GetProperty(
                    "Instance",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);

                var inst = prop?.GetValue(null) as VirtualFileSystem;

                if (inst == null)
                    throw new Exception($"VFS null: {ex.Message}");

                vfs = inst;
                Log("VFS reutilizado");
            }

            try
            {
                vfs.ReloadKeySet();
                Log("KeySet Reload OK");
            }
            catch (Exception ex)
            {
                Log($"KeySet reload falhou: {ex.Message}");
                throw;
            }

            Log("Criando VulkanRenderer...");

            gpu = VulkanRenderer.Create(
                "Ryubing",
                (inst, vk) =>
                {
                    unsafe
                    {
                        var ci = new AndroidSurfaceCreateInfoKHR
                        {
                            SType = StructureType.AndroidSurfaceCreateInfoKhr,
                            Window = (nint*)nativeWindow
                        };

                        var fp = vk.GetInstanceProcAddr(
                            inst,
                            "vkCreateAndroidSurfaceKHR");

                        if (fp == IntPtr.Zero)
                            throw new Exception(
                                "vkCreateAndroidSurfaceKHR nao encontrado");

                        var func =
                            Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(fp);

                        SurfaceKHR surf;

                        var res = func(inst, &ci, null, &surf);

                        if (res != Silk.NET.Vulkan.Result.Success)
                            throw new Exception(
                                $"vkCreateSurface falhou: {res}");

                        return surf;
                    }
                },
                () => new[]
                {
                    "VK_KHR_surface",
                    "VK_KHR_android_surface"
                });

            Log("Vulkan OK");

            var audio = new DummyHardwareDeviceDriver();
            var hleConf = BuildHleConfigurationFIX(vfs, gpu, audio);

            Log("HLE Config OK");

            device = new Switch(hleConf);

            Log("Switch criado");

            if (!device.LoadNsp(romPath))
                throw new Exception("LoadNsp false");

            Log("NSP OK");

            RunOnUiThread(() => logView.Visibility = ViewStates.Gone);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int frames = 0;

            while (running)
            {
                device.ProcessFrame();
                device.PresentFrame(() => { });

                frames++;

                if (sw.ElapsedMilliseconds >= 1000)
                {
                    int f = frames;
                    frames = 0;
                    sw.Restart();

                    RunOnUiThread(() =>
                        fpsView.Text = $"FPS: {f}");
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"ERRO:\n{ex.Message}\n{ex}");
        }
        finally
        {
            try
            {
                device?.Dispose();
            }
            catch
            {
            }

            try
            {
                gpu?.Dispose();
            }
            catch
            {
            }
        }
    }

    void CopyKeys(string baseDir, string systemDir)
    {
        string extKeys =
            "/storage/emulated/0/Download/Ryubing/keys";

        string legacyKeys =
            "/storage/emulated/0/Download/DragoNX/keys";

        string keysDir = Path.Combine(baseDir, "keys");

        Directory.CreateDirectory(keysDir);

        foreach (var name in new[] { "prod.keys", "title.keys" })
        {
            string src = Path.Combine(extKeys, name);

            if (!File.Exists(src))
                src = Path.Combine(legacyKeys, name);

            if (!File.Exists(src))
            {
                Log($"ATENCAO {name} nao encontrado");
                continue;
            }

            try
            {
                File.Copy(src, Path.Combine(systemDir, name), true);
                File.Copy(src, Path.Combine(keysDir, name), true);

                Log(
                    $"{name} OK " +
                    $"{new FileInfo(Path.Combine(systemDir, name)).Length}b");
            }
            catch (Exception ex)
            {
                Log($"{name} erro: {ex.Message}");
            }
        }
    }

    void CopyFirmware(string baseDir)
    {
        try
        {
            string fwSrc =
                "/storage/emulated/0/Download/Ryubing/firmware";

            string fwDst =
                Path.Combine(
                    baseDir,
                    "bis",
                    "system",
                    "Contents",
                    "registered");

            Directory.CreateDirectory(fwDst);

            if (!Directory.Exists(fwSrc))
            {
                Log("Firmware: pasta nao existe");
                return;
            }

            var ncas = Directory.GetFiles(fwSrc, "*.nca");
            int copied = 0;

            foreach (var nca in ncas)
            {
                string dst =
                    Path.Combine(fwDst, Path.GetFileName(nca));

                if (File.Exists(dst) &&
                    new FileInfo(dst).Length == new FileInfo(nca).Length)
                    continue;

                File.Copy(nca, dst, true);
                copied++;
            }

            Log($"Firmware: {copied}/{ncas.Length} novos");
        }
        catch (Exception ex)
        {
            Log($"Firmware erro: {ex.Message}");
        }
    }

    void InitAppData(string baseDir)
    {
        try
        {
            var appDataType = typeof(AppDataManager);

            var initMethod =
                appDataType.GetMethod(
                    "Initialize",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);

            if (initMethod == null)
            {
                Log("AppData.Initialize nao encontrado");
                return;
            }

            var p = initMethod.GetParameters();

            if (p.Length == 1)
            {
                initMethod.Invoke(null, new object[] { baseDir });
            }
            else if (p.Length == 2)
            {
                object mode;
                var enumType = p[1].ParameterType;

                if (Enum.TryParse(enumType, "User", out var m1))
                    mode = m1!;
                else if (Enum.TryParse(enumType, "UserProfile", out var m2))
                    mode = m2!;
                else
                    mode = Enum.GetValues(enumType).GetValue(0)!;

                initMethod.Invoke(
                    null,
                    new object[] { baseDir, mode });
            }

            Log($"AppData Base: {AppDataManager.BaseDirPath}");
        }
        catch (Exception ex)
        {
            Log($"AppData init: {ex.Message}");
        }
    }

    unsafe delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(
        Instance i,
        AndroidSurfaceCreateInfoKHR* p,
        AllocationCallbacks* a,
        SurfaceKHR* s);

    HleConfiguration BuildHleConfigurationFIX(
        VirtualFileSystem vfs,
        VulkanRenderer gpu,
        DummyHardwareDeviceDriver audio)
    {
        var allTypes =
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                })
                .ToList();

        var cmType = allTypes.First(t => t.Name == "ContentManager");
        var ucpType = allTypes.First(t => t.Name == "UserChannelPersistence");
        var lhmType = allTypes.First(t => t.Name == "LibHacHorizonManager");
        var amType = allTypes.First(t => t.Name == "AccountManager");

        object? contentManager = null;

        foreach (var c in cmType.GetConstructors()
                     .OrderByDescending(x => x.GetParameters().Length))
        {
            try
            {
                var pars = c.GetParameters();
                var args = new object?[pars.Length];

                for (int i = 0; i < pars.Length; i++)
                {
                    if (pars[i].ParameterType == typeof(VirtualFileSystem))
                        args[i] = vfs;
                    else if (pars[i].ParameterType == typeof(string))
                        args[i] = AppDataManager.BaseDirPath ?? "";
                    else
                        args[i] = null;
                }

                contentManager = c.Invoke(args);
                Log($"CM OK {pars.Length}");
                break;
            }
            catch (Exception ex)
            {
                Log($"CM fail {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        object userChannel =
            Activator.CreateInstance(
                ucpType,
                new object[] { true })
            ?? Activator.CreateInstance(ucpType)!;

        Log("UCP OK");

        object? libHac = null;

        foreach (var c in lhmType.GetConstructors()
                     .OrderByDescending(x => x.GetParameters().Length))
        {
            try
            {
                var pars = c.GetParameters();
                var args = new object?[pars.Length];

                for (int i = 0; i < pars.Length; i++)
                {
                    if (pars[i].ParameterType == typeof(VirtualFileSystem))
                        args[i] = vfs;
                    else if (pars[i].ParameterType == typeof(string))
                        args[i] = AppDataManager.BaseDirPath ?? "";
                    else if (pars[i].ParameterType.IsValueType)
                        args[i] = Activator.CreateInstance(pars[i].ParameterType);
                    else
                        args[i] = null;
                }

                libHac = c.Invoke(args);
                Log($"LHM OK {pars.Length}");
                break;
            }
            catch (Exception ex)
            {
                Log($"LHM fail {ex.InnerException?.Message}");
            }
        }

        object? accountManager = null;

        foreach (var c in amType.GetConstructors()
                     .OrderByDescending(x => x.GetParameters().Length))
        {
            try
            {
                var pars = c.GetParameters();
                var args = new object?[pars.Length];

                for (int i = 0; i < pars.Length; i++)
                {
                    if (pars[i].ParameterType == typeof(VirtualFileSystem))
                        args[i] = vfs;
                    else if (pars[i].ParameterType == typeof(string))
                        args[i] = AppDataManager.BaseDirPath ?? "";
                    else if (pars[i].ParameterType.IsValueType)
                        args[i] = Activator.CreateInstance(pars[i].ParameterType);
                    else
                        args[i] = null;
                }

                accountManager = c.Invoke(args);
                Log($"AM OK {pars.Length}");
                break;
            }
            catch (Exception ex)
            {
                Log($"AM fail {ex.InnerException?.Message}");
            }
        }

        if (accountManager == null)
            accountManager = Activator.CreateInstance(amType)!;

        var dummyUI = new DummyHostUIHandler();

        var hleConf = new HleConfiguration(
            MemoryConfiguration.MemoryConfiguration4GiB,
            SystemLanguage.AmericanEnglish,
            RegionCode.USA,
            VSyncMode.Switch,
            true,
            true,
            1,
            false,
            IntegrityCheckLevel.None,
            0,
            0,
            "UTC",
            MemoryManagerMode.SoftwarePageTable,
            true,
            AspectRatio.Fixed16x9,
            1f,
            false,
            "",
            MultiplayerMode.Disabled,
            false,
            "",
            "",
            false,
            0,
            false,
            1,
            Array.Empty<EnabledDirtyHack>());

        Log("HleConfiguration base OK");

        return hleConf.Configure(
            (VirtualFileSystem)vfs,
            (LibHacHorizonManager)libHac!,
            (ContentManager)contentManager!,
            (AccountManager)accountManager!,
            (UserChannelPersistence)userChannel,
            gpu,
            audio,
            dummyUI);
    }

    class DummyHostUIHandler : IHostUIHandler
    {
        public IHostUITheme HostUITheme => null!;

        public bool DisplayInputDialog(
            SoftwareKeyboardUIArgs args,
            out string userText)
        {
            userText = "";
            return false;
        }

        public bool DisplayMessageDialog(
            string title,
            string message) => false;

        public bool DisplayMessageDialog(
            ControllerAppletUIArgs args) => false;

        public bool DisplayCabinetDialog(
            out string userText)
        {
            userText = "";
            return false;
        }

        public void DisplayCabinetMessageDialog()
        {
        }

        public void ExecuteProgram(
            Switch device,
            ProgramSpecifyKind kind,
            ulong value)
        {
        }

        public bool DisplayErrorAppletDialog(
            string title,
            string message,
            string[] buttonsText,
            (uint Module, uint Description)? errorCode = null) => false;

        public IDynamicTextInputHandler CreateDynamicTextInputHandler() =>
            null!;

        public UserProfile ShowPlayerSelectDialog() =>
            null!;

        public void TakeScreenshot()
        {
        }
    }

    protected override void OnDestroy()
    {
        running = false;

        try
        {
            emuThread?.Join(2000);
        }
        catch
        {
        }

        base.OnDestroy();
    }
}
