#nullable disable
using Android.App; using Android.Content.PM; using Android.OS; using Android.Views; using Android.Widget;
using AFormat = Android.Graphics.Format; using Ryujinx.HLE; using Ryujinx.HLE.FileSystem; using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc; using Ryujinx.Graphics.Vulkan; using Ryujinx.Audio.Backends.Dummy;
using Ryujinx.Audio.Integration; using Silk.NET.Vulkan; using System; using System.IO; using System.Reflection;
using System.Runtime.InteropServices; using System.Threading; using SysEnv = System.Environment; using Switch = Ryujinx.HLE.Switch;

namespace DragoNX;
[Activity(Name="com.ryubing.android.GameActivity", Label="Ryubing", Theme="@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation=ScreenOrientation.Landscape, ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.ScreenSize|ConfigChanges.KeyboardHidden, Exported=false, MainLauncher=false)]
public class GameActivity : Activity
{
    const BindingFlags CtorFlags = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    string romPath=""; SurfaceView surfaceView; TextView logView; IntPtr nativeWindow=IntPtr.Zero; Thread emuThread; bool running=false; Switch device; VulkanRenderer gpu;
    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);
    [DllImport("android")] static extern void ANativeWindow_acquire(IntPtr window);
    [DllImport("android")] static extern void ANativeWindow_release(IntPtr window);
    [DllImport("android")] static extern int ANativeWindow_setBuffersGeometry(IntPtr window, int width, int height, int format);

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if(Window==null==false) Window.AddFlags(WindowManagerFlags.Fullscreen|WindowManagerFlags.KeepScreenOn);
        string extra = Intent.GetStringExtra("rom_path");
        if(extra==null==false) romPath=extra;
        if(romPath.Length==0){
            string dir="/storage/emulated/0/Download/Ryubing/games";
            if(Directory.Exists(dir)){
                foreach(var p in Directory.EnumerateFiles(dir,"*.*",SearchOption.AllDirectories)){
                    if(p.EndsWith(".nsp",StringComparison.OrdinalIgnoreCase)){ romPath=p; break; }
                }
            }
        }
        surfaceView=new SurfaceView(this); surfaceView.Holder.SetFormat((AFormat)1);
        logView=new TextView(this); logView.Text=Path.GetFileName(romPath); logView.SetTextColor(global::Android.Graphics.Color.White);
        var root=new FrameLayout(this); root.AddView(surfaceView,new FrameLayout.LayoutParams(-1,-1)); root.AddView(logView,new FrameLayout.LayoutParams(-2,-2){ Gravity=GravityFlags.Top|GravityFlags.Left });
        SetContentView(root); surfaceView.Holder.AddCallback(new SurfaceCallback(this));
    }
    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback {
        readonly GameActivity act; public SurfaceCallback(GameActivity a){ act=a; }
        public void SurfaceCreated(ISurfaceHolder h){
            var rect=h.SurfaceFrame; if(rect.Width()<=0) return; if(rect.Height()<=0) return;
            act.nativeWindow=ANativeWindow_fromSurface(global::Android.Runtime.JNIEnv.Handle,h.Surface.Handle);
            if(act.nativeWindow==IntPtr.Zero) return;
            ANativeWindow_setBuffersGeometry(act.nativeWindow,rect.Width(),rect.Height(),1);
            ANativeWindow_acquire(act.nativeWindow);
            if(act.emuThread==null==false){ if(act.emuThread.IsAlive) return; }
            act.running=true; act.emuThread=new Thread(act.EmulationLoop){ IsBackground=true }; act.emuThread.Start();
        }
        public void SurfaceChanged(ISurfaceHolder h,AFormat f,int w,int ht){
            if(act.nativeWindow==IntPtr.Zero==false){ ANativeWindow_setBuffersGeometry(act.nativeWindow,w,ht,1); }
        }
        public void SurfaceDestroyed(ISurfaceHolder h){
            act.running=false; if(act.nativeWindow==IntPtr.Zero==false){ ANativeWindow_release(act.nativeWindow); act.nativeWindow=IntPtr.Zero; }
        }
    }
    void EmulationLoop(){
        try{
            string baseDir=Path.Combine(FilesDir.AbsolutePath,"Ryujinx"); string systemDir=Path.Combine(baseDir,"system"); Directory.CreateDirectory(systemDir);
            string jitDir=Path.Combine(CacheDir.AbsolutePath,"jit"); Directory.CreateDirectory(jitDir); SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE",jitDir);
            VirtualFileSystem vfs=VirtualFileSystem.CreateInstance(); vfs.ReloadKeySet();
            gpu=VulkanRenderer.Create("Ryubing",(inst,vk)=>{ unsafe{ var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)nativeWindow }; var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR"); var func=Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(fp); SurfaceKHR surf; func(inst,&ci,null,&surf); return surf; } },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});
            var audio=new DummyHardwareDeviceDriver(); var hleConf=BuildHle(vfs,gpu,audio); device=new Switch(hleConf); device.LoadNsp(romPath);
            while(running){ device.ProcessFrame(); device.PresentFrame(()=>{}); Thread.Yield(); }
        }catch(Exception ex){ try{ File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt",ex.ToString()); }catch{} }
    }
    unsafe delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(Instance i,AndroidSurfaceCreateInfoKHR* p,AllocationCallbacks* a,SurfaceKHR* s);
    HleConfiguration BuildHle(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio){
        var cmType=typeof(ContentManager); object cm=null; foreach(var c in cmType.GetConstructors(CtorFlags)){ try{ var pr=c.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=FilesDir.AbsolutePath; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } cm=c.Invoke(ar); break; }catch{} }
        var ucpType=typeof(UserChannelPersistence); object ucp=Activator.CreateInstance(ucpType,true);
        var lhmType=typeof(LibHacHorizonManager); object lhm=null; foreach(var c in lhmType.GetConstructors(CtorFlags)){ try{ var pr=c.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=FilesDir.AbsolutePath; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } lhm=c.Invoke(ar); break; }catch{} }
        var amType=typeof(AccountManager); object am=null; object hc=null; try{ hc=lhm.GetType().GetProperty("RyujinxClient",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(lhm); }catch{} foreach(var c in amType.GetConstructors(CtorFlags)){ try{ var pr=c.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ var pt=pr[k].ParameterType; if(pt==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pt.Name.Contains("HorizonClient")) ar[k]=hc; else if(pt.IsValueType) ar[k]=Activator.CreateInstance(pt); } am=c.Invoke(ar); break; }catch{} }
        var hleType=typeof(HleConfiguration); var ctor=hleType.GetConstructors(CtorFlags)[0]; var ps=ctor.GetParameters(); var ca=new object[ps.Length]; for(int k=0;k<ps.Length;k++){ var pt=ps[k].ParameterType; if(pt==typeof(string)) ca[k]="UTC"; else if(pt==typeof(bool)) ca[k]=true; else if(pt.IsEnum) ca[k]=Enum.GetValues(pt).GetValue(0); else if(pt.IsValueType) ca[k]=Activator.CreateInstance(pt); } var hle=(HleConfiguration)ctor.Invoke(ca);
        var conf=hleType.GetMethod("Configure"); var cps=conf.GetParameters(); var cargs=new object[cps.Length];
        for(int k=0;k<cps.Length;k++){
            var pt=cps[k].ParameterType;
            if(pt==typeof(VirtualFileSystem)) cargs[k]=vfs;
            else if(pt==typeof(LibHacHorizonManager)) cargs[k]=lhm;
            else if(pt==typeof(ContentManager)) cargs[k]=cm;
            else if(pt==typeof(AccountManager)) cargs[k]=am;
            else if(pt==typeof(UserChannelPersistence)) cargs[k]=ucp;
            else if(pt.IsInstanceOfType(gpu)) cargs[k]=gpu;
            else if(typeof(IHardwareDeviceDriver).IsAssignableFrom(pt)) cargs[k]=audio;
        }
        var cfg = conf.Invoke(hle,cargs) as HleConfiguration;
        try{
            foreach(var prop in cfg.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance)){
                if(prop.PropertyType==typeof(IHardwareDeviceDriver) || prop.Name.Contains("AudioDeviceDriver")){
                    if(prop.CanWrite){
                        object cur = prop.GetValue(cfg);
                        if(cur==null) prop.SetValue(cfg, audio);
                    }
                }
            }
        }catch{}
        return cfg;
    }
}
