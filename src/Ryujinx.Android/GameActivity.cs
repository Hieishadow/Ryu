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

[Android.App.Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
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
        if(string.IsNullOrEmpty(romPath) ||!File.Exists(romPath)){
            var dir = "/storage/emulated/0/Download/DragoNX/games";
            var first = Directory.GetFiles(dir, "*.nsp").FirstOrDefault();
            if(first!=null) romPath = first;
        }

        surfaceView = new SurfaceView(this);
        logView = new TextView(this);
        logView.Text = $"DRAGONX #268 FPS FIX\n{Path.GetFileName(romPath)}\nExiste: {File.Exists(romPath)} {new FileInfo(romPath).Length/1024/1024}MB";
        logView.Gravity = GravityFlags.Center;
        logView.SetTextColor(Android.Graphics.Color.White);
        logView.SetBackgroundColor(Android.Graphics.Color.Black);

        fpsView = new TextView(this);
        fpsView.Text = "FPS: --";
        fpsView.SetTextColor(Android.Graphics.Color.Lime);
        fpsView.TextSize = 13;
        fpsView.SetPadding(20,30,20,20);

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
                    string prodOrig = "/storage/emulated/0/Download/DragoNX/keys/prod.keys";
                    string keysDir = Path.Combine(act.FilesDir.AbsolutePath, "Ryujinx", "keys");
                    Directory.CreateDirectory(keysDir);
                    string prodDest = Path.Combine(keysDir, "prod.keys");
                    if(!File.Exists(prodOrig)) throw new Exception($"prod.keys nao achada {prodOrig}");
                    File.Copy(prodOrig, prodDest, true);
                    Log($"prod.keys {new FileInfo(prodDest).Length}b OK");

                    string jitDir = Path.Combine(act.CacheDir.AbsolutePath, "jit");
                    Directory.CreateDirectory(jitDir);
                    SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE", jitDir);
                    SysEnv.SetEnvironmentVariable("XDG_CONFIG_HOME", act.FilesDir.AbsolutePath);

                    Log($"ROM: {Path.GetFileName(act.romPath)} OK");

                    IntPtr nativeWin = ANativeWindow_fromSurface(IntPtr.Zero, holder.Surface.Handle);
                    if(nativeWin==IntPtr.Zero) throw new Exception("ANativeWindow falhou");

                    var vfs = VirtualFileSystem.CreateInstance();
                    var gpu = VulkanRenderer.Create("DragoNX",(inst,vk)=>{
                        unsafe{ SurfaceKHR surf; var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)nativeWin };
                            var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR");
                            var func=Marshal.GetDelegateForFunctionPointer<CreateDelegate>(fp);
                            func(inst,&ci,null,&surf); return surf; }
                    },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});

                    var audio = new DummyHardwareDeviceDriver();
                    var hleType = typeof(HleConfiguration);
                    var ctor = hleType.GetConstructors().OrderByDescending(c=>c.GetParameters().Length).First();
                    var pars = ctor.GetParameters(); object?[] args = new object?[pars.Length];
                    for(int i=0;i<pars.Length;i++){
                        var pt=pars[i].ParameterType;
                        if(pt.IsEnum) args[i]=Enum.GetValues(pt).GetValue(0);
                        else if(pt==typeof(string)) args[i]="";
                        else if(pt==typeof(bool)) args[i]=false;
                        else if(pt.IsValueType) args[i]=Activator.CreateInstance(pt);
                        else args[i]=Activator.CreateInstance(pt,true);
                    }
                    var cfgObj = ctor.Invoke(args);
                    var userChannel = Activator.CreateInstance(hleType.GetProperty("UserChannelPersistence")!.PropertyType,true);
                    var hleConf = (HleConfiguration)hleType.GetMethod("Configure")!.Invoke(cfgObj,new object?[]{vfs,null,null,null,userChannel,gpu,audio,null})!;
                    var device = new Ryujinx.HLE.Switch(hleConf);

                    Log("Loading NSP...");
                    bool ok = device.LoadNsp(act.romPath);
                    if(!ok) throw new Exception("LoadNsp false - keys ou NSP");

                    act.RunOnUiThread(()=> act.logView.Visibility=ViewStates.Gone);

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    int frames = 0;
                    while(true){
                        device.ProcessFrame();
                        device.PresentFrame(()=>{});
                        frames++;
                        if(sw.ElapsedMilliseconds >= 1000){
                            int f = frames; frames = 0; sw.Restart();
                            act.RunOnUiThread(()=> act.fpsView.Text = $"FPS: {f}");
                        }
                    }
                }catch(Exception ex){ act.RunOnUiThread(()=>{ act.logView.Text=$"ERRO #268:\n{ex.Message}\n{ex}"; act.logView.SetTextColor(Android.Graphics.Color.Red); }); }
            }).Start();
        }
        public void SurfaceChanged(ISurfaceHolder h,AFormat f,int w,int ht){}
        public void SurfaceDestroyed(ISurfaceHolder h){}
    }
}
