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
    void Log(string s){ try{ RunOnUiThread(()=>{ if(logView!=null) logView.Text+= "\n"+s; }); File.AppendAllText(Path.Combine(FilesDir.AbsolutePath,"crash.txt"), SysEnv.NewLine+DateTime.Now+": "+s+SysEnv.NewLine); Directory.CreateDirectory("/storage/emulated/0/Download/Ryubing"); File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", DateTime.Now+": "+s+SysEnv.NewLine); }catch{} }
    protected override void OnCreate(Bundle savedInstanceState){ try{ AppDomain.CurrentDomain.UnhandledException+=(o,e)=>{ Log("UNHANDLED: "+e.ExceptionObject.ToString()); }; base.OnCreate(savedInstanceState); if(Window!=null) Window.AddFlags(WindowManagerFlags.Fullscreen|WindowManagerFlags.KeepScreenOn); string extra=Intent.GetStringExtra("rom_path"); if(extra!=null) romPath=extra; if(romPath.Length==0){ string dir="/storage/emulated/0/Download/Ryubing/games"; if(Directory.Exists(dir)){ foreach(var p in Directory.EnumerateFiles(dir,"*.*",SearchOption.AllDirectories)){ if(p.EndsWith(".nsp",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".xci",StringComparison.OrdinalIgnoreCase)){ romPath=p; break; } } } } surfaceView=new SurfaceView(this); surfaceView.Holder.SetFormat((AFormat)1); logView=new TextView(this); logView.Text="ROM: "+Path.GetFileName(romPath)+"\nAguardando Surface..."; logView.SetTextColor(global::Android.Graphics.Color.White); logView.TextSize=9; var root=new FrameLayout(this); root.AddView(surfaceView,new FrameLayout.LayoutParams(-1,-1)); root.AddView(logView,new FrameLayout.LayoutParams(-2,-2){ Gravity=GravityFlags.Top|GravityFlags.Left }); SetContentView(root); surfaceView.Holder.AddCallback(new SurfaceCallback(this)); Log("OnCreate OK - romPath="+romPath); }catch(Exception ex){ Log("OnCreate CRASH: "+ex.ToString()); } }
    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback{ readonly GameActivity act; public SurfaceCallback(GameActivity a){ act=a; } public void SurfaceCreated(ISurfaceHolder h){ try{ var rect=h.SurfaceFrame; if(rect.Width()<=0||rect.Height()<=0) return; act.Log("SurfaceCreated "+rect.Width()+"x"+rect.Height()); act.nativeWindow=ANativeWindow_fromSurface(global::Android.Runtime.JNIEnv.Handle,h.Surface.Handle); if(act.nativeWindow==IntPtr.Zero){ act.Log("ANativeWindow ZERO"); return; } ANativeWindow_setBuffersGeometry(act.nativeWindow,rect.Width(),rect.Height(),1); ANativeWindow_acquire(act.nativeWindow); if(act.emuThread!=null&&act.emuThread.IsAlive) return; act.running=true; act.emuThread=new Thread(act.EmulationLoop){ IsBackground=true }; act.emuThread.Start(); act.Log("EmuThread Start OK"); }catch(Exception ex){ act.Log("SurfaceCreated CRASH: "+ex.ToString()); } } public void SurfaceChanged(ISurfaceHolder h,AFormat f,int w,int ht){ try{ if(act.nativeWindow!=IntPtr.Zero) ANativeWindow_setBuffersGeometry(act.nativeWindow,w,ht,1); }catch(Exception ex){ act.Log("SurfaceChanged CRASH: "+ex.ToString()); } } public void SurfaceDestroyed(ISurfaceHolder h){ act.running=false; try{ if(act.nativeWindow!=IntPtr.Zero){ ANativeWindow_release(act.nativeWindow); act.nativeWindow=IntPtr.Zero; } }catch{} } }
    void EmulationLoop(){
        try{
            Log("EmulationLoop START"); string baseDir=Path.Combine(FilesDir.AbsolutePath,"Ryujinx"); Directory.CreateDirectory(Path.Combine(baseDir,"system")); string jitDir=Path.Combine(CacheDir.AbsolutePath,"jit"); Directory.CreateDirectory(jitDir); SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE",jitDir);
            VirtualFileSystem vfs=VirtualFileSystem.CreateInstance(); vfs.ReloadKeySet(); Log("VFS OK");
            var audio=new DummyHardwareDeviceDriver(); Log("Audio Dummy OK");
            if(nativeWindow==IntPtr.Zero){ Log("nativeWindow ZERO abort"); return; }
            gpu=VulkanRenderer.Create("Ryubing",(inst,vk)=>{ unsafe{ var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)nativeWindow }; var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR"); if(fp==IntPtr.Zero) throw new Exception("vkCreateAndroidSurfaceKHR not found"); var func=Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(fp); SurfaceKHR surf; var res=func(inst,&ci,null,&surf); Log("CreateSurface result: "+res); return surf; } },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});
            Log("VulkanRenderer OK - "+gpu.GetType().FullName);
            var hleConf=BuildHle(vfs,gpu,audio);
            try{ var p=hleConf.GetType().GetProperty("GpuRenderer",CtorFlags)??hleConf.GetType().GetProperty("Gpu",CtorFlags)??hleConf.GetType().GetProperty("Renderer",CtorFlags); var v=p?.GetValue(hleConf); Log("HLE Config FINAL OK - GpuRenderer="+(v==null?"NULL":v.GetType().FullName)); }catch(Exception ex){ Log("HLE reflection ERROR: "+ex.Message); }
            Log("ANTES new Switch FINAL");
            device=new Switch(hleConf);
            Log("DEPOIS new Switch FINAL OK");
            device.LoadNsp(romPath); Log("LoadNsp OK - "+romPath);
            RunOnUiThread(()=>{ logView.Visibility=ViewStates.Gone; });
            while(running){ device.ProcessFrame(); device.PresentFrame(()=>{}); Thread.Yield(); }
        }catch(Exception ex){ Log("EmulationLoop CRASH: "+ex.ToString()); try{ RunOnUiThread(()=>{ Toast.MakeText(this, ex.Message, ToastLength.Long).Show(); }); }catch{} }
    }
    unsafe delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(Instance i,AndroidSurfaceCreateInfoKHR* p,AllocationCallbacks* a,SurfaceKHR* s);
    HleConfiguration BuildHle(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio){
        var cmType=typeof(ContentManager); object cm=null; foreach(var c in cmType.GetConstructors(CtorFlags)){ try{ var pr=c.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=FilesDir.AbsolutePath; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } cm=c.Invoke(ar); break; }catch{} }
        var ucpType=typeof(UserChannelPersistence); object ucp=Activator.CreateInstance(ucpType,true);
        var lhmType=typeof(LibHacHorizonManager); object lhm=null; foreach(var c in lhmType.GetConstructors(CtorFlags)){ try{ var pr=c.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=FilesDir.AbsolutePath; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } lhm=c.Invoke(ar); break; }catch{} }
        var amType=typeof(AccountManager); object am=null; object hc=null; try{ hc=lhm.GetType().GetProperty("RyujinxClient",CtorFlags).GetValue(lhm); }catch{} foreach(var c in amType.GetConstructors(CtorFlags)){ try{ var pr=c.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ var pt=pr[k].ParameterType; if(pt==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pt.Name.Contains("HorizonClient")) ar[k]=hc; else if(pt.IsValueType) ar[k]=Activator.CreateInstance(pt); } am=c.Invoke(ar); break; }catch{} }
        var hleType=typeof(HleConfiguration); var ctor=hleType.GetConstructors(CtorFlags)[0]; var ps=ctor.GetParameters(); var ca=new object[ps.Length]; for(int k=0;k<ps.Length;k++){ var pt=ps[k].ParameterType; if(pt==typeof(string)) ca[k]="UTC"; else if(pt==typeof(bool)) ca[k]=true; else if(pt.IsEnum) ca[k]=Enum.GetValues(pt).GetValue(0); else if(pt.IsValueType) ca[k]=Activator.CreateInstance(pt); } var hle=(HleConfiguration)ctor.Invoke(ca);
        var conf=hleType.GetMethod("Configure"); var cps=conf.GetParameters(); var cargs=new object[cps.Length];
        for(int k=0;k<cps.Length;k++){
            var pt=cps[k].ParameterType;
            if(pt==typeof(VirtualFileSystem)) cargs[k]=vfs;
            else if(pt==typeof(LibHacHorizonManager)) cargs[k]=lhm;
            else if(pt==typeof(ContentManager)) cargs[k]=cm;
            else if(pt==typeof(AccountManager)) cargs[k]=am;
            else if(pt==typeof(UserChannelPersistence)) cargs[k]=ucp;
            else if(gpu!=null && pt.IsAssignableFrom(gpu.GetType())) cargs[k]=gpu;
            else if(gpu!=null && (pt.FullName.Contains("IGpuRenderer")||pt.FullName.Contains("GpuRenderer")||pt.Name.Contains("GpuRenderer"))){ cargs[k]=gpu; Log("FORCADO GPU em ["+k+"] "+pt.FullName); }
            else if(typeof(IHardwareDeviceDriver).IsAssignableFrom(pt)) cargs[k]=audio;
            Log("Configure["+k+"] "+pt.FullName+" -> "+(cargs[k]==null?"NULL":cargs[k].GetType().FullName));
        }
        return conf.Invoke(hle,cargs) as HleConfiguration;
    }
}
