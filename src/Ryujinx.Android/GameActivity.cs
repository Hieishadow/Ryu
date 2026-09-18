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
using System.Diagnostics;
using SysEnv = System.Environment;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
public class GameActivity : Activity
{
    string romPath = "";
    SurfaceView surfaceView = null!;
    TextView logView = null!;
    TextView fpsView = null!;

    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
        romPath = Intent?.GetStringExtra("rom_path")?? "";
        if(string.IsNullOrEmpty(romPath) || !File.Exists(romPath)){
            romPath = Directory.GetFiles("/storage/emulated/0/Download/DragoNX/games", "*.nsp").FirstOrDefault() ?? "";
        }

        surfaceView = new SurfaceView(this);
        logView = new TextView(this);
        logView.Text = $"DRAGONX #268 RENDER REAL\n{Path.GetFileName(romPath)}\n{new FileInfo(romPath).Length/1024/1024}MB";
        logView.Gravity = GravityFlags.Center;
        logView.SetTextColor(Android.Graphics.Color.White);
        logView.SetBackgroundColor(Android.Graphics.Color.Black);

        fpsView = new TextView(this);
        fpsView.Text = "FPS: --";
        fpsView.SetTextColor(Android.Graphics.Color.Lime);
        fpsView.TextSize = 12;
        fpsView.SetPadding(20,30,20,20);
        fpsView.Visibility = ViewStates.Gone;

        var root = new Android.Widget.FrameLayout(this);
        root.AddView(surfaceView, new Android.Widget.FrameLayout.LayoutParams(-1,-1));
        root.AddView(logView, new Android.Widget.FrameLayout.LayoutParams(-1,-1));
        root.AddView(fpsView, new Android.Widget.FrameLayout.LayoutParams(-2,-2));
        SetContentView(root);
        surfaceView.Holder.AddCallback(new SurfaceCallback(this));
    }

    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
    {
        GameActivity act;
        public SurfaceCallback(GameActivity a){ act = a; }
        unsafe delegate Silk.NET.Vulkan.Result CreateDelegate(Instance i, AndroidSurfaceCreateInfoKHR* p, AllocationCallbacks* a, SurfaceKHR* s);

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            new System.Threading.Thread(()=> {
                void Log(string t){ act.RunOnUiThread(()=> act.logView.Text+="\n"+t); }
                try{
                    // 1. KEYS
                    string prodOrig = "/storage/emulated/0/Download/DragoNX/keys/prod.keys";
                    string keysDir = Path.Combine(act.FilesDir.AbsolutePath, "Ryujinx", "keys");
                    Directory.CreateDirectory(keysDir);
                    string prodDest = Path.Combine(keysDir, "prod.keys");
                    File.Copy(prodOrig, prodDest, true);
                    Log($"keys {new FileInfo(prodDest).Length}b OK");

                    // 2. JIT
                    string jitDir = Path.Combine(act.CacheDir.AbsolutePath, "jit");
                    Directory.CreateDirectory(jitDir);
                    SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE", jitDir);
                    SysEnv.SetEnvironmentVariable("XDG_CONFIG_HOME", act.FilesDir.AbsolutePath);
                    Log($"jit OK");

                    // 3. NATIVE WINDOW
                    IntPtr nativeWin = ANativeWindow_fromSurface(IntPtr.Zero, holder.Surface.Handle);
                    if(nativeWin==IntPtr.Zero) throw new Exception("ANativeWindow 0");
                    Log($"window OK");

                    // 4. VFS + GPU
                    var vfs = VirtualFileSystem.CreateInstance();
                    var gpu = VulkanRenderer.Create("DragoNX",(inst,vk)=>{
                        unsafe{ 
                            SurfaceKHR surf; 
                            var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)nativeWin };
                            var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR");
                            var func=Marshal.GetDelegateForFunctionPointer<CreateDelegate>(fp);
                            func(inst,&ci,null,&surf); return surf; 
                        }
                    },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});
                    Log($"Vulkan OK");

                    // 5. HLE REAL - sem reflection
                    var audio = new DummyHardwareDeviceDriver();
                    var config = new HleConfiguration(vfs, gpu, audio, configMode: ConfigMode.Handheld);
                    var device = new Switch(config);
                    Log($"Switch OK");

                    // 6. LOAD REAL - usando ApplicationLoader
                    Log($"Loading {Path.GetFileName(act.romPath)}...");
                    var loader = new Ryujinx.HLE.Loaders.NspLoader(vfs, act.romPath);
                    var app = loader.Load();
                    if(app == null) throw new Exception("NspLoader null - keys invalida?");
                    device.LoadApplication(app);
                    Log($"Loaded! Iniciando render...");

                    act.RunOnUiThread(()=>{ 
                        act.logView.Visibility=ViewStates.Gone;
                        act.fpsView.Visibility=ViewStates.Visible;
                    });

                    var sw = Stopwatch.StartNew();
                    int frames = 0;
                    while(true){
                        device.ProcessFrame();
                        device.PresentFrame(()=>{});
                        frames++;
                        if(sw.ElapsedMilliseconds >= 1000){
                            int f = frames; frames = 0; sw.Restart();
                            act.RunOnUiThread(()=> act.fpsView.Text = $"FPS: {f} | {Path.GetFileName(act.romPath)}");
                        }
                    }
                }catch(Exception ex){ 
                    act.RunOnUiThread(()=>{ 
                        act.logView.Text=$"ERRO #268 RENDER:\n{ex.Message}\n\n{ex.StackTrace?.Substring(0,500)}"; 
                        act.logView.SetTextColor(Android.Graphics.Color.Red); 
                    }); 
                }
            }).Start();
        }
        public void SurfaceChanged(ISurfaceHolder h,AFormat f,int w,int ht){}
        public void SurfaceDestroyed(ISurfaceHolder h){}
    }
}
