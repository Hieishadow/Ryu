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

namespace DragoNX;

[Android.App.Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
public class GameActivity : Activity
{
    const string TAG = "DragoNX";
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
            var dir = "/storage/emulated/0/Download/DragoNX/games";
            if (Directory.Exists(dir))
            {
                var first = Directory.GetFiles(dir, "*.nsp").FirstOrDefault();
                if (first!= null) romPath = first;
            }
        }
        surfaceView = new SurfaceView(this);
        logView = new TextView(this);
        logView.Text = $"DRAGONX #270\n{Path.GetFileName(romPath)}\nExiste: {File.Exists(romPath)} {(File.Exists(romPath)? new FileInfo(romPath).Length/1024/1024 : 0)}MB";
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
        surfaceView.Holder!.AddCallback(new SurfaceCallback(this));
    }

    void Log(string msg){ Android.Util.Log.Info(TAG,msg); RunOnUiThread(()=> logView.Text+="\n"+msg); }
    void LogError(string msg){ Android.Util.Log.Error(TAG,msg); RunOnUiThread(()=>{ logView.Text+="\n"+msg; logView.SetTextColor(Android.Graphics.Color.Red); }); }

    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
    {
        readonly GameActivity act;
        public SurfaceCallback(GameActivity a) => act = a;
        public void SurfaceCreated(ISurfaceHolder holder)
        {
            act.nativeWindow = ANativeWindow_fromSurface(Android.Runtime.JNIEnv.Handle, holder.Surface!.Handle);
            if(act.nativeWindow==IntPtr.Zero){ act.LogError("ANativeWindow Zero!"); return; }
            ANativeWindow_acquire(act.nativeWindow);
            if(act.emuThread==null ||!act.emuThread.IsAlive){
                act.running=true;
                act.emuThread = new Thread(act.EmulationLoop){ IsBackground=true, Priority=System.Threading.ThreadPriority.Highest, Name="RyujinxEmu" };
                act.emuThread.Start();
            }
        }
        public void SurfaceChanged(ISurfaceHolder h,AFormat f,int w,int ht){}
        public void SurfaceDestroyed(ISurfaceHolder h){ act.running=false; if(act.nativeWindow!=IntPtr.Zero){ ANativeWindow_release(act.nativeWindow); act.nativeWindow=IntPtr.Zero; } }
    }

    void EmulationLoop()
    {
        try
        {
            Log($"Iniciando {Path.GetFileName(romPath)}");
            string prodOrig="/storage/emulated/0/Download/DragoNX/keys/prod.keys";
            string keysDir=Path.Combine(FilesDir!.AbsolutePath,"Ryujinx","keys");
            Directory.CreateDirectory(keysDir);
            string prodDest=Path.Combine(keysDir,"prod.keys");
            if(!File.Exists(prodOrig)) throw new Exception($"prod.keys nao achada {prodOrig}");
            File.Copy(prodOrig,prodDest,true);
            Log($"prod.keys OK {new FileInfo(prodDest).Length}b");
            string jitDir=Path.Combine(CacheDir!.AbsolutePath,"jit");
            Directory.CreateDirectory(jitDir);
            SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE",jitDir);
            SysEnv.SetEnvironmentVariable("XDG_CONFIG_HOME",FilesDir.AbsolutePath);
            var vfs=VirtualFileSystem.CreateInstance();
            Log("Criando VulkanRenderer...");
            gpu=VulkanRenderer.Create("DragoNX",(inst,vk)=>{
                unsafe{
                    var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)nativeWindow };
                    var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR");
                    if(fp==IntPtr.Zero) throw new Exception("vkCreateAndroidSurfaceKHR nao encontrado");
                    var func=Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(fp);
                    SurfaceKHR surf; var res=func(inst,&ci,null,&surf);
                    if(res!=Silk.NET.Vulkan.Result.Success) throw new Exception($"vkCreateSurface falhou: {res}");
                    return surf;
                }
            },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});
            Log("Vulkan OK");
            var audio=new DummyHardwareDeviceDriver();
            var hleConf=BuildHleConfiguration(vfs,gpu,audio);
            device=new Ryujinx.HLE.Switch(hleConf);
            Log("Loading NSP...");
            if(!device.LoadNsp(romPath)) throw new Exception("LoadNsp false");
            Log("NSP OK");
            RunOnUiThread(()=> logView.Visibility=ViewStates.Gone);
            var sw=System.Diagnostics.Stopwatch.StartNew(); int frames=0;
            while(running){
                device.ProcessFrame();
                device.PresentFrame(()=>{});
                frames++;
                if(sw.ElapsedMilliseconds>=1000){ int f=frames; frames=0; sw.Restart(); RunOnUiThread(()=> fpsView.Text=$"FPS: {f}"); }
            }
        }catch(Exception ex){ LogError($"ERRO #270:\n{ex.Message}\n{ex}"); }
        finally{ try{device?.Dispose();}catch{} try{gpu?.Dispose();}catch{} }
    }

    delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(Instance i,AndroidSurfaceCreateInfoKHR* p,AllocationCallbacks* a,SurfaceKHR* s);
    HleConfiguration BuildHleConfiguration(VirtualFileSystem vfs,VulkanRenderer gpu,DummyHardwareDeviceDriver audio){
        var hleType=typeof(HleConfiguration);
        var ctor=hleType.GetConstructors().OrderByDescending(c=>c.GetParameters().Length).First();
        var pars=ctor.GetParameters(); object?[] args=new object?[pars.Length];
        for(int i=0;i<pars.Length;i++){ var pt=pars[i].ParameterType; if(pt.IsEnum) args[i]=Enum.GetValues(pt).GetValue(0); else if(pt==typeof(string)) args[i]=""; else if(pt==typeof(bool)) args[i]=false; else if(pt.IsValueType) args[i]=Activator.CreateInstance(pt); else args[i]=null; }
        var cfgObj=ctor.Invoke(args);
        var userChannel=Activator.CreateInstance(hleType.GetProperty("UserChannelPersistence")!.PropertyType,true);
        var res=hleType.GetMethod("Configure")!.Invoke(cfgObj,new object?[]{vfs,null,null,null,userChannel,gpu,audio,null});
        return (HleConfiguration)res!;
    }
    protected override void OnDestroy(){ running=false; try{emuThread?.Join(2000);}catch{} base.OnDestroy(); }
}
