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
using VkResult = Silk.NET.Vulkan.Result;
using SysEnv = System.Environment;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
public class GameActivity : Activity
{
    string romPath = "";
    SurfaceView surfaceView = null!;
    TextView logView = null!;

    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
        romPath = Intent?.GetStringExtra("rom_path")?? "/storage/emulated/0/Download/DragoNX/games/Links Awakening.nsp";

        surfaceView = new SurfaceView(this);
        logView = new TextView(this);
        logView.Text = $"DRAGONX #260 RENDER\n{Path.GetFileName(romPath)}\nIniciando...";
        logView.Gravity = GravityFlags.Center;
        logView.SetTextColor(Android.Graphics.Color.White);
        logView.SetBackgroundColor(Android.Graphics.Color.Black);

        var root = new Android.Widget.FrameLayout(this);
        root.AddView(surfaceView, new Android.Widget.FrameLayout.LayoutParams(-1,-1));
        root.AddView(logView, new Android.Widget.FrameLayout.LayoutParams(-1,-1));
        SetContentView(root);
        surfaceView.Holder.AddCallback(new SurfaceCallback(this));
    }

    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
    {
        GameActivity act;
        public SurfaceCallback(GameActivity a){ act = a; }
        unsafe delegate VkResult CreateAndroidSurfaceDelegate(Instance instance, AndroidSurfaceCreateInfoKHR* pCreateInfo, AllocationCallbacks* pAllocator, SurfaceKHR* pSurface);

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            new System.Threading.Thread(()=> {
                try {
                    void Log(string s){ act.RunOnUiThread(()=> act.logView.Text += "\n" + s); }

                    // 1. KEYS REAL
                    string prodOrig = "/storage/emulated/0/Download/DragoNX/keys/prod.keys";
                    string prodDest = Path.Combine(act.FilesDir.AbsolutePath, "prod.keys");
                    if (!File.Exists(prodOrig)) throw new Exception("prod.keys nao achada em " + prodOrig);
                    File.Copy(prodOrig, prodDest, true);
                    Log($"prod.keys {new FileInfo(prodDest).Length}b OK");

                    string jitDir = Path.Combine(act.CacheDir.AbsolutePath, "jit");
                    Directory.CreateDirectory(jitDir);
                    SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE", jitDir);

                    // 2. VULKAN REAL
                    IntPtr nativeWin = ANativeWindow_fromSurface(IntPtr.Zero, holder.Surface.Handle);
                    if (nativeWin == IntPtr.Zero) throw new Exception("ANativeWindow falhou");

                    var vfs = VirtualFileSystem.CreateInstance();
                    var gpu = VulkanRenderer.Create("DragoNX", (instance, vk) => {
                        unsafe {
                            SurfaceKHR surface;
                            var ci = new AndroidSurfaceCreateInfoKHR{ SType = StructureType.AndroidSurfaceCreateInfoKhr, Window = (nint*)nativeWin };
                            var fp = vk.GetInstanceProcAddr(instance, "vkCreateAndroidSurfaceKHR");
                            var func = Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(fp);
                            func(instance, &ci, null, &surface);
                            return surface;
                        }
                    }, ()=> new[] { "VK_KHR_surface", "VK_KHR_android_surface" });

                    var audio = new DummyHardwareDeviceDriver();

                    // 3. HLE CONFIG SEM ENUM - via reflexao pra nao quebrar build
                    var hleType = typeof(HleConfiguration);
                    var ctor = hleType.GetConstructors().OrderByDescending(c=>c.GetParameters().Length).First();
                    var p = ctor.GetParameters();
                    object?[] args = new object?[p.Length];
                    for(int i=0;i<p.Length;i++){
                        var pt = p[i].ParameterType;
                        if (pt.IsEnum) args[i] = Enum.GetValues(pt).GetValue(0);
                        else if (pt == typeof(string)) args[i] = "";
                        else if (pt == typeof(bool)) args[i] = true;
                        else if (pt == typeof(int) || pt == typeof(long) || pt == typeof(float) || pt == typeof(double)) args[i] = Activator.CreateInstance(pt);
                        else args[i] = Activator.CreateInstance(pt, true);
                    }
                    var cfgObj = ctor.Invoke(args);
                    var configure = hleType.GetMethod("Configure");
                    var userChannelType = hleType.GetProperty("UserChannelPersistence")!.PropertyType;
                    var userChannel = Activator.CreateInstance(userChannelType, true);
                    var hleConf = (HleConfiguration)configure!.Invoke(cfgObj, new object?[]{ vfs, null, null, null, userChannel, gpu, audio, null })!;

                    var device = new Ryujinx.HLE.Switch(hleConf);

                    // 4. RENDER REAL
                    Log("Loading NSP...");
                    bool ok = device.LoadNsp(act.romPath);
                    if (!ok) throw new Exception("LoadNsp falhou - NSP corrompido?");

                    act.RunOnUiThread(()=> {
                        act.logView.Visibility = ViewStates.Gone; // SOME LOGO E MOSTRA JOGO
                    });

                    // LOOP QUE RENDERIZA DE VERDADE
                    while (true) {
                        device.ProcessFrame();
                        device.PresentFrame(()=> {});
                    }

                } catch (Exception ex) {
                    act.RunOnUiThread(()=> {
                        act.logView.Text = $"CRASH #260 REAL:\n{ex}";
                        act.logView.SetTextColor(Android.Graphics.Color.Red);
                    });
                }
            }).Start();
        }
        public void SurfaceChanged(ISurfaceHolder h, AFormat f, int w, int ht){}
        public void SurfaceDestroyed(ISurfaceHolder h){}
    }
}
