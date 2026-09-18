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
    SurfaceView surfaceView;
    Android.Widget.LinearLayout logoBox;
    TextView logoView;
    TextView logView;

    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
        romPath = Intent?.GetStringExtra("rom_path") ?? "/storage/emulated/0/Download/DragoNX/games/Links Awakening.nsp";
        
        surfaceView = new SurfaceView(this);

        logoView = new TextView(this);
        logoView.Text = "DRAGONX";
        logoView.Gravity = GravityFlags.Center;
        logoView.SetTextColor(Android.Graphics.Color.ParseColor("#00FF88"));
        logoView.TextSize = 34f;
        logoView.SetTypeface(Android.Graphics.Typeface.Create("monospace", Android.Graphics.TypefaceStyle.Bold), Android.Graphics.TypefaceStyle.Bold);
        
        logView = new TextView(this);
        logView.Text = $"#257 FINAL\n{Path.GetFileName(romPath)}";
        logView.Gravity = GravityFlags.Center;
        logView.SetTextColor(Android.Graphics.Color.White);
        logView.TextSize = 11f;

        logoBox = new Android.Widget.LinearLayout(this);
        logoBox.Orientation = Android.Widget.Orientation.Vertical;
        logoBox.Gravity = GravityFlags.Center;
        logoBox.AddView(logoView);
        logoBox.AddView(logView);

        var root = new Android.Widget.FrameLayout(this);
        root.SetBackgroundColor(Android.Graphics.Color.Black);
        root.AddView(surfaceView, new Android.Widget.FrameLayout.LayoutParams(-1,-1));
        var lp = new Android.Widget.FrameLayout.LayoutParams(-1,-1);
        lp.Gravity = GravityFlags.Center;
        root.AddView(logoBox, lp);

        SetContentView(root);
        surfaceView.Holder.AddCallback(new SurfaceCallback(this));
    }

    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
    {
        GameActivity act;
        public SurfaceCallback(GameActivity a){ act = a; }
        unsafe delegate VkResult CreateAndroidSurfaceDelegate(Instance instance, AndroidSurfaceCreateInfoKHR* pCreateInfo, AllocationCallbacks* pAllocator, SurfaceKHR* pSurface);
        void Log(string s){ act.RunOnUiThread(()=> { act.logView.Text += "\n" + s; }); }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            new System.Threading.Thread(()=> {
                try {
                    // 1. KEYS
                    string basePath = "/storage/emulated/0/Download/DragoNX";
                    string prodOrig = Path.Combine(basePath, "keys/prod.keys");
                    string titleOrig = Path.Combine(basePath, "keys/title.keys");
                    string prodDest = Path.Combine(act.FilesDir.AbsolutePath, "prod.keys");
                    string titleDest = Path.Combine(act.FilesDir.AbsolutePath, "title.keys");

                    if (!File.Exists(prodOrig)) throw new Exception("prod.keys nao achada: " + prodOrig);
                    File.Copy(prodOrig, prodDest, true);
                    if (File.Exists(titleOrig)) File.Copy(titleOrig, titleDest, true);
                    Log($"keys OK {new FileInfo(prodDest).Length}b");

                    // 2. FIRMWARE - copia se existir
                    string firmSrc = Path.Combine(basePath, "firmware");
                    string firmDst = Path.Combine(act.FilesDir.AbsolutePath, "bis/system/Contents/registered");
                    if (Directory.Exists(firmSrc)) {
                        int c = Directory.GetFiles(firmSrc, "*", SearchOption.AllDirectories).Length;
                        Log($"firmware {c} arqs");
                    }

                    // 3. JIT + CACHE
                    string jitDir = Path.Combine(act.CacheDir.AbsolutePath, "jit");
                    Directory.CreateDirectory(jitDir);
                    Environment.SetEnvironmentVariable("RYUJINX_JIT_CACHE", jitDir);
                    Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", act.FilesDir.AbsolutePath);

                    // 4. VULKAN WINDOW
                    IntPtr nativeWin = ANativeWindow_fromSurface(IntPtr.Zero, holder.Surface.Handle);
                    if (nativeWin == IntPtr.Zero) throw new Exception("ANativeWindow falhou");
                    Log("Vulkan...");

                    var vfs = VirtualFileSystem.CreateInstance();
                    var gpu = VulkanRenderer.Create("DragoNX", (instance, vk) => {
                        unsafe {
                            SurfaceKHR surface;
                            var ci = new AndroidSurfaceCreateInfoKHR{ SType = StructureType.AndroidSurfaceCreateInfoKhr, Window = (nint*)nativeWin };
                            var fp = vk.GetInstanceProcAddr(instance, "vkCreateAndroidSurfaceKHR");
                            if (fp == IntPtr.Zero) throw new Exception("vkCreateAndroidSurfaceKHR not found");
                            var func = Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(fp);
                            VkResult r = func(instance, &ci, null, &surface);
                            if (r != VkResult.Success) throw new Exception($"vkCreateAndroid {r}");
                            return surface;
                        }
                    }, ()=> new[] { "VK_KHR_surface", "VK_KHR_android_surface" });

                    var audio = new DummyHardwareDeviceDriver();
                    var prop = typeof(HleConfiguration).GetProperty("UserChannelPersistence");
                    var userChannel = Activator.CreateInstance(prop.PropertyType, true);
                    var memConf = (MemoryConfiguration)Activator.CreateInstance(typeof(MemoryConfiguration), true);
                    var cfgObj = new HleConfiguration(memConf, SystemLanguage.AmericanEnglish, RegionCode.USA, VSyncMode.Switch, true, true, 1, true, IntegrityCheckLevel.None, 0, 0, "UTC", MemoryManagerMode.SoftwarePageTable, true, AspectRatio.Fixed16x9, 1f, false, "", MultiplayerMode.Disabled, false, "", "", false, 0, false, 60, null);
                    var method = typeof(HleConfiguration).GetMethod("Configure");
                    var hleConf = (HleConfiguration)method.Invoke(cfgObj, new object[]{ vfs, null, null, null, userChannel, gpu, audio, null });
                    var device = new Ryujinx.HLE.Switch(hleConf);

                    Log("Boot Zelda...");
                    bool ok = device.LoadNsp(act.romPath);
                    if (!ok) ok = device.LoadXci(act.romPath); // tenta XCI tambem
                    if (!ok) throw new Exception("LoadNsp/Xci falhou");

                    act.RunOnUiThread(()=> {
                        act.logoBox.Animate().Alpha(0f).SetDuration(400).WithEndAction(new Java.Lang.Runnable(()=> { act.logoBox.Visibility = ViewStates.Gone; }));
                    });

                    while (true) {
                        device.ProcessFrame();
                        device.PresentFrame(()=> {});
                    }

                } catch (Exception ex) {
                    act.RunOnUiThread(()=> { act.logView.Text = "CRASH #257:\n" + ex.Message + "\n" + ex.StackTrace; });
                }
            }).Start();
        }
        public void SurfaceChanged(ISurfaceHolder h, AFormat f, int w, int ht){}
        public void SurfaceDestroyed(ISurfaceHolder h){}
    }
}
