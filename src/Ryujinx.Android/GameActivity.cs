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
using Android.Util;

namespace DragoNX;

[Android.App.Activity(
    Label = "DragoNX",
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape,
    ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation |
                           Android.Content.PM.ConfigChanges.ScreenSize |
                           Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    const string TAG = "DragoNX";

    string romPath = "";
    SurfaceView surfaceView = null!;
    TextView logView = null!;
    TextView fpsView = null!;

    Thread? emuThread;
    volatile bool running = false;
    ManualResetEventSlim surfaceReady = new(false);
    IntPtr nativeWindow = IntPtr.Zero;
    Ryujinx.HLE.Switch? device;
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
        Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        romPath = Intent?.GetStringExtra("rom_path") ?? "";
        if (string.IsNullOrEmpty(romPath) || !File.Exists(romPath))
        {
            var dir = "/storage/emulated/0/Download/DragoNX/games";
            if (Directory.Exists(dir))
            {
                var first = Directory.GetFiles(dir, "*.nsp").FirstOrDefault();
                if (first != null) romPath = first;
            }
        }

        surfaceView = new SurfaceView(this);
        logView = new TextView(this);
        logView.Text = $"DRAGONX #269\n{Path.GetFileName(romPath)}\n" +
                       $"Existe: {File.Exists(romPath)} " +
                       $"{(File.Exists(romPath) ? new FileInfo(romPath).Length / 1024 / 1024 : 0)}MB";
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

    void Log(string msg)
    {
        Android.Util.Log.Info(TAG, msg);
        RunOnUiThread(() => logView.Text += "\n" + msg);
    }

    void LogError(string msg)
    {
        Android.Util.Log.Error(TAG, msg);
        RunOnUiThread(() =>
        {
            logView.Text += "\n" + msg;
            logView.SetTextColor(Android.Graphics.Color.Red);
        });
    }

    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
    {
        readonly GameActivity act;
        public SurfaceCallback(GameActivity a) => act = a;

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            Android.Util.Log.Info(TAG, "SurfaceCreated chamado");

            // IMPORTANTE: ANativeWindow_fromSurface precisa rodar na UI Thread
            // porque precisamos do JNIEnv correto da thread atual.
            act.nativeWindow = ANativeWindow_fromSurface(
                Android.Runtime.JNIEnv.Handle,
                holder.Surface!.Handle);

            if (act.nativeWindow == IntPtr.Zero)
            {
                act.LogError("ANativeWindow_fromSurface retornou Zero!");
                return;
            }

            ANativeWindow_acquire(act.nativeWindow);
            Android.Util.Log.Info(TAG, $"NativeWindow adquirido: {act.nativeWindow}");

            // Só inicia a thread depois que temos a janela
            act.surfaceReady.Set();
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
            Android.Util.Log.Info(TAG, $"SurfaceChanged {w}x{ht} fmt={f}");
        }

        public void SurfaceDestroyed(ISurfaceHolder h)
        {
            Android.Util.Log.Info(TAG, "SurfaceDestroyed");
            act.running = false;
            if (act.nativeWindow != IntPtr.Zero)
            {
                ANativeWindow_release(act.nativeWindow);
                act.nativeWindow = IntPtr.Zero;
            }
        }
    }

    // Chamado na thread de emulação
    void EmulationLoop()
    {
        // Anexa esta thread à JVM do Android (obrigatório p/ JNI)
        Android.Runtime.JNIEnv.AttachCurrentThread();

        try
        {
            Log($"Iniciando emulação. ROM: {Path.GetFileName(romPath)}");

            // ---- prod.keys ----
            string prodOrig = "/storage/emulated/0/Download/DragoNX/keys/prod.keys";
            string keysDir = Path.Combine(FilesDir!.AbsolutePath, "Ryujinx", "keys");
            Directory.CreateDirectory(keysDir);
            string prodDest = Path.Combine(keysDir, "prod.keys");
            if (!File.Exists(prodOrig))
                throw new Exception($"prod.keys não encontrada em {prodOrig}");

            File.Copy(prodOrig, prodDest, true);
            Log($"prod.keys OK ({new FileInfo(prodDest).Length} bytes)");

            // ---- Variáveis de ambiente ----
            string jitDir = Path.Combine(CacheDir!.AbsolutePath, "jit");
            Directory.CreateDirectory(jitDir);
            SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE", jitDir);
            SysEnv.SetEnvironmentVariable("XDG_CONFIG_HOME", FilesDir.AbsolutePath);

            if (nativeWindow == IntPtr.Zero)
                throw new Exception("NativeWindow inválido antes do Vulkan");

            // ---- Virtual File System ----
            var vfs = VirtualFileSystem.CreateInstance();

            // ---- Vulkan Renderer ----
            Log("Criando VulkanRenderer...");
            gpu = VulkanRenderer.Create("DragoNX", (inst, vk) =>
            {
                unsafe
                {
                    var ci = new AndroidSurfaceCreateInfoKHR
                    {
                        SType = StructureType.AndroidSurfaceCreateInfoKhr,
                        Window = (nint*)nativeWindow
                    };
                    var fp = vk.GetInstanceProcAddr(inst, "vkCreateAndroidSurfaceKHR");
                    if (fp == IntPtr.Zero)
                        throw new Exception("vkCreateAndroidSurfaceKHR não encontrado");

                    var func = Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(fp);
                    SurfaceKHR surf;
                    var res = func(inst, &ci, null, &surf);
                    if (res != Silk.NET.Vulkan.Result.Success)
                        throw new Exception($"vkCreateAndroidSurfaceKHR falhou: {res}");
                    return surf;
                }
            },
            () => new[] { "VK_KHR_surface", "VK_KHR_android_surface" });

            Log("VulkanRenderer criado OK");

            // ---- Áudio dummy ----
            var audio = new DummyHardwareDeviceDriver();

            // ---- HleConfiguration ----
            Log("Montando HleConfiguration...");
            var hleConf = BuildHleConfiguration(vfs, gpu, audio);

            // ---- Switch (emulador) ----
            device = new Ryujinx.HLE.Switch(hleConf);

            Log("Carregando NSP...");
            bool ok = device.LoadNsp(romPath);
            if (!ok) throw new Exception("LoadNsp retornou false (keys ou NSP inválidos)");
            Log("NSP carregado OK");

            RunOnUiThread(() => logView.Visibility = ViewStates.Gone);

            // ---- Loop de renderização ----
            Log("Entrando no loop de frames...");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int frames = 0;

            while (running)
            {
                try
                {
                    device.ProcessFrame();

                    // O callback DEVE apresentar o frame.
                    // No Ryujinx, PresentFrame é chamado com uma Action que
                    // representa o "swap buffers". Sem isso, nada aparece.
                    device.PresentFrame(() =>
                    {
                        // Aqui, em teoria, o backend Vulkan já apresentou
                        // o frame via swapchain. Alguns builds precisam
                        // que você chame gpu.Present() ou similar.
                        // Se a sua versão do Ryujinx tiver um método
                        // específico, chame ele aqui.
                    });

                    frames++;
                }
                catch (Exception exFrame)
                {
                    LogError($"Erro no frame: {exFrame.Message}");
                    break;
                }

                if (sw.ElapsedMilliseconds >= 1000)
                {
                    int f = frames;
                    frames = 0;
                    sw.Restart();
                    RunOnUiThread(() => fpsView.Text = $"FPS: {f}");
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"ERRO #269:\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            try { device?.Dispose(); } catch { }
            try { gpu?.Dispose(); } catch { }
            Android.Runtime.JNIEnv.DetachCurrentThread();
        }
    }

    delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(
        Instance instance,
        AndroidSurfaceCreateInfoKHR* pCreateInfo,
        AllocationCallbacks* pAllocator,
        SurfaceKHR* pSurface);

    HleConfiguration BuildHleConfiguration(
        VirtualFileSystem vfs,
        VulkanRenderer gpu,
        DummyHardwareDeviceDriver audio)
    {
        var hleType = typeof(HleConfiguration);
        var cfg = hleType
            .GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var pars = cfg.GetParameters();
        var args = new object?[pars.Length];

        for (int i = 0; i < pars.Length; i++)
        {
            var pt = pars[i].ParameterType;
            if (pt.IsEnum) args[i] = Enum.GetValues(pt).GetValue(0);
            else if (pt == typeof(string)) args[i] = "";
            else if (pt == typeof(bool)) args[i] = false;
            else if (pt.IsValueType) args[i] = Activator.CreateInstance(pt);
            else args[i] = null; // deixa o método Configure preencher
        }

        var cfgObj = cfg.Invoke(args);

        var configure = hleType.GetMethod("Configure")
            ?? throw new Exception("HleConfiguration.Configure não encontrado");

        var userChannelProp = hleType.GetProperty("UserChannelPersistence")
            ?? throw new Exception("UserChannelPersistence não encontrado");
        var userChannel = Activator.CreateInstance(userChannelProp.PropertyType, true);

        var result = configure.Invoke(cfgObj, new object?[]
        {
            vfs, null, null, null, userChannel, gpu, audio, null
        });

        return (HleConfiguration)result!;
    }

    protected override void OnDestroy()
    {
        running = false;
        try { emuThread?.Join(2000); } catch { }
        surfaceReady.Dispose();
        base.OnDestroy();
    }
}
