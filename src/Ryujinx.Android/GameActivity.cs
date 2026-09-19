#nullable disable
using Android.App; using Android.Content.PM; using Android.OS; using Android.Views; using Android.Widget;
using AFormat = Android.Graphics.Format; using Ryujinx.HLE; using Ryujinx.HLE.FileSystem; using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc; using Ryujinx.Graphics.Vulkan; using Ryujinx.Audio.Backends.Dummy;
using Silk.NET.Vulkan; using System; using System.IO; using System.Linq; using System.Reflection;
using System.Runtime.InteropServices; using System.Threading; using SysEnv = System.Environment; using Switch = Ryujinx.HLE.Switch;
namespace DragoNX;
[Activity(Name="com.ryubing.android.GameActivity", Label="Ryubing", Theme="@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation=ScreenOrientation.Landscape, ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.ScreenSize|ConfigChanges.KeyboardHidden, Exported=false, MainLauncher=false)]
public class GameActivity : Activity
{
    const BindingFlags CtorFlags = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static readonly string LogFile = "/storage/emulated/0/Download/Ryubing/ryubing_log.txt"; static string _lastBaseDir = "";
    string romPath = ""; SurfaceView surfaceView; TextView logView; TextView fpsView;
    Thread emuThread; bool running = false; IntPtr nativeWindow = IntPtr.Zero; Switch device; VulkanRenderer gpu;
    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);
    [DllImport("android")] static extern void ANativeWindow_acquire(IntPtr window);
    [DllImport("android")] static extern void ANativeWindow_release(IntPtr window);
    [DllImport("android")] static extern int ANativeWindow_setBuffersGeometry(IntPtr window, int width, int height, int format);
    protected override void OnCreate(Bundle savedInstanceState)
    {
        try { Console.SetOut(new StringWriter()); Console.SetError(new StringWriter()); } catch {}
        base.OnCreate(savedInstanceState);
        Window.AddFlags(WindowManagerFlags.Fullscreen|WindowManagerFlags.KeepScreenOn);
        var extra = Intent.GetStringExtra("rom_path"); if(extra==null) romPath=""; else romPath=extra;
        bool empty = string.IsNullOrEmpty(romPath); bool ex = false; if(romPath.Length>0) ex = File.Exists(romPath);
        if(empty || ex==false){
            var dir="/storage/emulated/0/Download/Ryubing/games";
            if(Directory.Exists(dir)){
                foreach(var p in Directory.EnumerateFiles(dir,"*.*",SearchOption.AllDirectories)){
                    if(p.EndsWith(".nsp",StringComparison.OrdinalIgnoreCase) || p.EndsWith(".xci",StringComparison.OrdinalIgnoreCase)){ romPath=p; break; }
                }
            }
        }
        surfaceView = new SurfaceView(this); surfaceView.Holder.SetFormat((AFormat)1);
        logView = new TextView(this); logView.Text="RYUBING\n"+Path.GetFileName(romPath)+"\n"; logView.SetTextColor(global::Android.Graphics.Color.White); logView.SetBackgroundColor(global::Android.Graphics.Color.Black); logView.TextSize=10; logView.SetPadding(20,20,20,20);
        fpsView = new TextView(this); fpsView.Text="FPS: --"; fpsView.SetTextColor(global::Android.Graphics.Color.Lime); fpsView.TextSize=13; fpsView.SetPadding(20,30,20,20);
        var root=new FrameLayout(this); root.AddView(surfaceView,new FrameLayout.LayoutParams(-1,-1)); root.AddView(logView,new FrameLayout.LayoutParams(-1,-1)); root.AddView(fpsView,new FrameLayout.LayoutParams(-2,-2){ Gravity=GravityFlags.Top|GravityFlags.Left });
        SetContentView(root); surfaceView.Holder.AddCallback(new SurfaceCallback(this));
    }
    void LogAppend(string m){ try{ File.AppendAllText(LogFile,m+"\n"); }catch{} RunOnUiThread(()=>{ var lv=logView; if(lv==null) return; lv.Text+="\n"+m; }); }
    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback {
        readonly GameActivity act; public SurfaceCallback(GameActivity a){ act=a; }
        public void SurfaceCreated(ISurfaceHolder h){
            var rect=h.SurfaceFrame; if(rect.Width()<=0||rect.Height()<=0) return;
            act.nativeWindow=ANativeWindow_fromSurface(global::Android.Runtime.JNIEnv.Handle,h.Surface.Handle);
            if(act.nativeWindow==IntPtr.Zero) return;
            ANativeWindow_setBuffersGeometry(act.nativeWindow,rect.Width(),rect.Height(),1);
            ANativeWindow_acquire(act.nativeWindow);
            var th=act.emuThread; bool need=false; if(th==null) need=true; else { if(th.IsAlive==false) need=true; }
            if(need){ act.running=true; act.emuThread=new Thread(act.EmulationLoop){ IsBackground=true, Priority=System.Threading.ThreadPriority.Highest }; act.emuThread.Start(); }
        }
        public void SurfaceChanged(ISurfaceHolder h,AFormat f,int w,int ht){ if(act.nativeWindow==IntPtr.Zero==false) ANativeWindow_setBuffersGeometry(act.nativeWindow,w,ht,1); }
        public void SurfaceDestroyed(ISurfaceHolder h){ act.running=false; if(act.nativeWindow==IntPtr.Zero==false){ ANativeWindow_release(act.nativeWindow); act.nativeWindow=IntPtr.Zero; } }
    }
    void EmulationLoop(){
        try{
            string baseDir=Path.Combine(FilesDir.AbsolutePath,"Ryujinx"); _lastBaseDir=baseDir; string systemDir=Path.Combine(baseDir,"system"); Directory.CreateDirectory(systemDir);
            string jitDir=Path.Combine(CacheDir.AbsolutePath,"jit"); Directory.CreateDirectory(jitDir); SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE",jitDir);
            VirtualFileSystem vfs=null; try{ vfs=VirtualFileSystem.CreateInstance(); }catch{ var prop=typeof(VirtualFileSystem).GetProperty("Instance",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic); if(prop==null==false) vfs=prop.GetValue(null) as VirtualFileSystem; }
            vfs.ReloadKeySet();
            gpu=VulkanRenderer.Create("Ryubing",(inst,vk)=>{ unsafe{ var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)nativeWindow }; var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR"); var func=Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(fp); SurfaceKHR surf; var res=func(inst,&ci,null,&surf); return surf; } },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});
            var audio=new DummyHardwareDeviceDriver(); var hleConf=BuildHleConfigurationFIX(vfs,gpu,audio); device=new Switch(hleConf); bool loaded=device.LoadNsp(romPath); if(loaded==false) return;
            while(running){ bool cont=device.ProcessFrame(); if(cont==false) break; device.PresentFrame(()=>{}); Thread.Yield(); }
        }catch(Exception ex){ try{ File.AppendAllText(LogFile,"ERRO:"+ex+"\n"); }catch{} }finally{ try{ var d=device; if(d==null==false) d.Dispose(); }catch{} }
    }
    unsafe delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(Instance i,AndroidSurfaceCreateInfoKHR* p,AllocationCallbacks* a,SurfaceKHR* s);
    HleConfiguration BuildHleConfigurationFIX(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio){
        var cmType=typeof(ContentManager); object contentManager=null;
        foreach(var c in cmType.GetConstructors(CtorFlags).OrderByDescending(x=>x.GetParameters().Length)){ try{ var pars=c.GetParameters(); var args=new object[pars.Length]; for(int i=0;i<pars.Length;i++){ if(pars[i].ParameterType==typeof(VirtualFileSystem)) args[i]=vfs; else if(pars[i].ParameterType==typeof(string)) args[i]=_lastBaseDir; else if(pars[i].ParameterType.IsValueType) args[i]=Activator.CreateInstance(pars[i].ParameterType); else args[i]=null; } contentManager=c.Invoke(args); break; }catch{} }
        var ucpType=typeof(UserChannelPersistence); object userChannel=null;
        foreach(var c in ucpType.GetConstructors(CtorFlags).OrderBy(x=>x.GetParameters().Length)){ try{ var pars=c.GetParameters(); var args=new object[pars.Length]; for(int i=0;i<pars.Length;i++){ if(pars[i].ParameterType==typeof(bool)) args[i]=true; else if(pars[i].ParameterType.IsValueType) args[i]=Activator.CreateInstance(pars[i].ParameterType); else args[i]=null; } userChannel=c.Invoke(args); break; }catch{} }
        if(userChannel==null) userChannel=Activator.CreateInstance(ucpType,true);
        var lhmType=typeof(LibHacHorizonManager); object libHac=null;
        foreach(var c in lhmType.GetConstructors(CtorFlags).OrderByDescending(x=>x.GetParameters().Length)){ try{ var pars=c.GetParameters(); var args=new object[pars.Length]; for(int i=0;i<pars.Length;i++){ if(pars[i].ParameterType==typeof(VirtualFileSystem)) args[i]=vfs; else if(pars[i].ParameterType==typeof(string)) args[i]=_lastBaseDir; else if(pars[i].ParameterType.IsValueType) args[i]=Activator.CreateInstance(pars[i].ParameterType); else args[i]=null; } libHac=c.Invoke(args); break; }catch{} }
        var amType=typeof(AccountManager); object accountManager=null; object horizonClient=null;
        try{ var prop=libHac.GetType().GetProperty("RyujinxClient",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance); if(prop==null==false) horizonClient=prop.GetValue(libHac); }catch{}
        foreach(var c in amType.GetConstructors(CtorFlags).OrderByDescending(x=>x.GetParameters().Length)){ try{ var pars=c.GetParameters(); var args=new object[pars.Length]; for(int i=0;i<pars.Length;i++){ var pt=pars[i].ParameterType; if(pt==typeof(VirtualFileSystem)) args[i]=vfs; else if(pt==typeof(string)) args[i]=_lastBaseDir; else if(pt.Name.Contains("HorizonClient")) args[i]=horizonClient; else if(pt.IsValueType) args[i]=Activator.CreateInstance(pt); else args[i]=null; } accountManager=c.Invoke(args); break; }catch{} }
        var hleType=typeof(HleConfiguration); var hleCtor=hleType.GetConstructors(CtorFlags).OrderByDescending(c=>c.GetParameters().Length).First(); var pars2=hleCtor.GetParameters(); var ctorArgs=new object[pars2.Length];
        for(int i=0;i<pars2.Length;i++){ var pt=pars2[i].ParameterType; if(pt==typeof(string)) ctorArgs[i]="UTC"; else if(pt==typeof(bool)) ctorArgs[i]=true; else if(pt==typeof(int)||pt==typeof(long)||pt==typeof(uint)||pt==typeof(ulong)) ctorArgs[i]=Convert.ChangeType(1,pt); else if(pt==typeof(float)||pt==typeof(double)) ctorArgs[i]=Convert.ChangeType(1f,pt); else if(pt.IsEnum){ var names=Enum.GetNames(pt); string pick=names[0]; foreach(var n in names){ if(n=="AmericanEnglish"||n=="USA"||n=="None"||n=="Disabled"||n=="Switch"||n.Contains("4GiB")){ pick=n; break; } } ctorArgs[i]=Enum.Parse(pt,pick); }else if(pt.IsArray) ctorArgs[i]=Array.CreateInstance(pt.GetElementType(),0); else if(pt.IsValueType) ctorArgs[i]=Activator.CreateInstance(pt); else ctorArgs[i]=null; }
        var hleConfig=(HleConfiguration)hleCtor.Invoke(ctorArgs); var configureMethod=hleType.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).FirstOrDefault(m=>m.Name=="Configure" && m.GetParameters().Length>=7); var confPars=configureMethod.GetParameters(); var confArgs=new object[confPars.Length];
        for(int i=0;i<confPars.Length;i++){ var pt=confPars[i].ParameterType; if(pt==typeof(VirtualFileSystem)) confArgs[i]=vfs; else if(pt==typeof(LibHacHorizonManager)) confArgs[i]=libHac; else if(pt==typeof(ContentManager)) confArgs[i]=contentManager; else if(pt==typeof(AccountManager)) confArgs[i]=accountManager; else if(pt==typeof(UserChannelPersistence)) confArgs[i]=userChannel; else if(pt.IsInstanceOfType(gpu)) confArgs[i]=gpu; else if(pt.Name.Contains("Audio")||pt.Name.Contains("HardwareDeviceDriver")) confArgs[i]=audio; else if(pt.Name.Contains("HostUI")) confArgs[i]=null; else if(pt.IsValueType) confArgs[i]=Activator.CreateInstance(pt); else confArgs[i]=null; }
        return configureMethod.Invoke(hleConfig,confArgs) as HleConfiguration;
    }
    protected override void OnDestroy(){ running=false; try{ var th=emuThread; if(th==null==false) th.Join(2000); }catch{} base.OnDestroy(); }
}
