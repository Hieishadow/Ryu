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
        // FIX 1: pega do Intent e valida se existe
        romPath = Intent?.GetStringExtra("rom_path") ?? "";
        if(string.IsNullOrEmpty(romPath) || !File.Exists(romPath)){
            var dir = "/storage/emulated/0/Download/DragoNX/games";
            romPath = Directory.GetFiles(dir, "*.nsp").FirstOrDefault() ?? romPath;
        }

        surfaceView = new SurfaceView(this);
        logView = new TextView(this);
        logView.Text = $"DRAGONX #264 FIX\n{Path.GetFileName(romPath)}\nExiste: {File.Exists(romPath)}\nIniciando...";
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
        unsafe delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(Instance instance, AndroidSurfaceCreateInfoKHR* pCreateInfo, AllocationCallbacks* pAllocator, SurfaceKHR* pSurface);

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            new System.Threading.Thread(()=> {
                try {
                    void Log(string s){ act.RunOnUiThread(()=> act.logView.Text += "\n" + s); }

                    // FIX 2: prod.keys na pasta certa que o Ryujinx lê
                    string prodOrig = "/storage/emulated/0/Download/DragoNX/keys/prod.keys";
                    string keysDir = Path.Combine(act.FilesDir.AbsolutePath, "Ryujinx", "keys");
                    Directory.CreateDirectory(keysDir);
                    string prodDest = Path.Combine(keysDir, "prod.keys");
                    if (!File.Exists(prodOrig)) throw new Exception("prod.keys nao achada em " + prodOrig);
                    File.Copy(prodOrig, prodDest, true);
                    Log($"prod.keys {new FileInfo(prodDest).Length}b OK em {prodDest}");

                    string jitDir = Path.Combine(act.CacheDir.AbsolutePath, "jit");
                    Directory.CreateDirectory(jitDir);
                    SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE", jitDir);
                    SysEnv.SetEnvironmentVariable("XDG_CONFIG_HOME", act.FilesDir.AbsolutePath);

                    Log($"ROM existe: {File.Exists(act.romPath)} | {new FileInfo(act.romPath).Length/1024/1024}MB");

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

                    var hleType = typeof(HleConfiguration);
                    var ctor = hleType.GetConstructors().OrderByDescending(c=>c.GetParameters().Length).First();
                    var p = ctor.GetParameters();
                    object?[] args = new object?[p.Length];
                    for(int i=0;i<p.Length;i++){
                        var pt = p[i].ParameterType;
                        if (pt.IsEnum) args[i] = Enum.GetValues(pt).GetValue(0);
                        else if (pt == typeof(string)) args[i] = "";
                        else if (pt == typeof(bool)) args[i] = false; // leve pro SD865
                        else if (pt.IsValueType) args[i] = Activator.CreateInstance(pt);
                        else args[i] = Activator.CreateInstance(pt, true);
                    }
                    var cfgObj = ctor.Invoke(args);
                    var userChannelType = hleType.GetProperty("UserChannelPersistence")!.PropertyType;
                    var userChannel = Activator.CreateInstance(userChannelType, true);
                    var hleConf = (HleConfiguration)hleType.GetMethod("Configure")!.Invoke(cfgObj, new object?[]{ vfs, null, null, null, userChannel, gpu, audio, null })!;

                    var device = new Ryujinx.HLE.Switch(hleConf);

                    Log("Loading NSP...");
                    if(!File.Exists(act.romPath)) throw new Exception($"ROM nao existe: {act.romPath}");
                    bool ok = device.LoadNsp(act.romPath);
                    if (!ok) throw new Exception($"LoadNsp retornou false - keys invalida ou NSP corrompido");

                    act.RunOnUiThread(()=> act.logView.Visibility = ViewStates.Gone);

                    while (true) { device.ProcessFrame(); device.PresentFrame(()=> {}); }

                } catch (Exception ex) {
                    act.RunOnUiThread(()=> {
                        act.logView.Text = $"CRASH #264:\n{ex.Message}\n\n{ex}";
                        act.logView.SetTextColor(Android.Graphics.Color.Red);
                    });
                }
            }).Start();
        }
        public void SurfaceChanged(ISurfaceHolder h, AFormat f, int w, int ht){}
        public void SurfaceDestroyed(ISurfaceHolder h){}
    }
}
