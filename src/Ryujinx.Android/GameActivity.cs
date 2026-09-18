using Android.App;
using Android.OS;
using Android.Views;
using AFormat = Android.Graphics.Format;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.Audio.Backends.Dummy;
using Silk.NET.Vulkan;
using System;
using System.IO;
using System.Runtime.InteropServices;
using VkResult = Silk.NET.Vulkan.Result;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
public class GameActivity : Activity
{
    string romPath = "";
    SurfaceView? surfaceView;
    TextView? logView;

    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
        romPath = Intent?.GetStringExtra("rom_path") ?? "/storage/emulated/0/Download/DragoNX/games/Links Awakening.nsp";
        
        logView = new TextView(this){ Text = $"DragoNX #257 ZELDA\n{Path.GetFileName(romPath)}\nIniciando..." };
        logView.Gravity = GravityFlags.Center;
        logView.SetTextColor(Android.Graphics.Color.White);
        surfaceView = new SurfaceView(this);
        
        var layout = new Android.Widget.FrameLayout(this);
        layout.AddView(surfaceView, new Android.Widget.FrameLayout.LayoutParams(-1,-1));
        layout.AddView(logView, new Android.Widget.FrameLayout.LayoutParams(-1,-1));
        SetContentView(layout);
        surfaceView.Holder!.AddCallback(new SurfaceCallback(this));
    }

    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
    {
        GameActivity act;
        public SurfaceCallback(GameActivity a){ act = a; }
        unsafe delegate VkResult CreateAndroidSurfaceDelegate(Instance instance, AndroidSurfaceCreateInfoKHR* pCreateInfo, AllocationCallbacks* pAllocator, SurfaceKHR* pSurface);

        void Log(string s) => act.RunOnUiThread(() => act.logView!.Text += "\n" + s);

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            act.RunOnUiThread(() => act.logView!.Text = $"Surface OK #257\nROM: {Path.GetFileName(act.romPath)}");
            new System.Threading.Thread(() => {
                try {
                    // 1 - PROD KEYS - ESSENCIAL
                    string prodOrig = "/storage/emulated/0/Download/DragoNX/keys/prod.keys";
                    string prodDest1 = Path.Combine(act.FilesDir.AbsolutePath, "prod.keys");
                    string prodDest2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ryujinx", "system", "prod.keys");
                    
                    if (!File.Exists(prodOrig)) throw new Exception($"prod.keys NAO ACHADA em {prodOrig}");
                    File.Copy(prodOrig, prodDest1, true);
                    Directory.CreateDirectory(Path.GetDirectoryName(prodDest2)!);
                    File.Copy(prodOrig, prodDest2, true);
                    Log($"✓ prod.keys {new FileInfo(prodOrig).Length} bytes copiada");

                    // 2 - FIRMWARE CHECK
                    string firmPath = "/storage/emulated/0/Download/DragoNX/firmware/";
                    int firmCount = Directory.Exists(firmPath) ? Directory.GetFiles(firmPath, "*", SearchOption.AllDirectories).Length : 0;
                    Log(firmCount > 50 ? $"✓ firmware {firmCount} arq" : $"! firmware só {firmCount} arq - pode crashar");

                    // 3 - JIT CACHE
                    string jitDir = "/storage/emulated/0/Download/DragoNX/cache/jit";
                    Directory.CreateDirectory(jitDir);
                    Environment.SetEnvironmentVariable("RYUJINX_JIT_CACHE", jitDir);
                    Log($"✓ JIT {jitDir}");

                    // 4 - VULKAN REAL
                    IntPtr nativeWindow = ANativeWindow_fromSurface(IntPtr.Zero, holder.Surface.Handle);
                    if (nativeWindow == IntPtr.Zero) throw new Exception("ANativeWindow_fromSurface falhou");
                    Log($"✓ ANativeWindow 0x{nativeWindow.ToString("X")}");

                    var vfs = VirtualFileSystem.CreateInstance();
                    var gpu = VulkanRenderer.Create("DragoNX", (instance, vk) => {
                        unsafe {
                            SurfaceKHR surface;
                            var createInfo = new AndroidSurfaceCreateInfoKHR{
                                SType = StructureType.AndroidSurfaceCreateInfoKhr,
                                Window = (nint*)nativeWindow
                            };
                            var funcPtr = vk.GetInstanceProcAddr(instance, "vkCreateAndroidSurfaceKHR");
                            if (funcPtr == IntPtr.Zero) throw new Exception("vkCreateAndroidSurfaceKHR not found");
                            var func = Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(funcPtr);
                            VkResult res = func(instance, &createInfo, null, &surface);
                            if (res != VkResult.Success) throw new Exception($"vkCreateAndroidSurfaceKHR falhou: {res}");
                            return surface;
                        }
                    }, () => new[] { "VK_KHR_surface", "VK_KHR_android_surface" });

                    var audio = new DummyHardwareDeviceDriver();
                    var prop = typeof(HleConfiguration).GetProperty("UserChannelPersistence")!;
                    var userChannel = Activator.CreateInstance(prop.PropertyType, true)!;
                    var memConfig = (MemoryConfiguration)Activator.CreateInstance(typeof(MemoryConfiguration), true)!;
                    var hleConfigObj = new HleConfiguration(
                        memConfig, SystemLanguage.AmericanEnglish, RegionCode.USA, VSyncMode.Switch,
                        true, true, 1, true, IntegrityCheckLevel.None, 0, 0, "UTC",
                        MemoryManagerMode.SoftwarePageTable, true, AspectRatio.Fixed16x9, 1f, false, "",
                        MultiplayerMode.Disabled, false, "", "", false, 0, false, 60, null
                    );
                    var configure = typeof(HleConfiguration).GetMethod("Configure")!;
                    var hleConfig = (HleConfiguration)configure.Invoke(hleConfigObj, new object[]{ vfs, null!, null!, null!, userChannel, gpu, audio, null! })!;
                    var device = new Ryujinx.HLE.Switch(hleConfig);

                    Log($"Loading NSP...");
                    bool ok = device.LoadNsp(act.romPath);
                    if (!ok) throw new Exception($"LoadNsp falhou - {act.romPath} - prod.keys errada?");

                    act.RunOnUiThread(() => {
                        act.logView!.Text = $"Zelda BOOTOU #257!";
                        act.logView!.Visibility = ViewStates.Gone;
                    });
                    
                    // LOOP DO ZELDA
                    while (true) { device.ProcessFrame(); device.PresentFrame(() => {}); }

                }
