#nullable disable
#pragma warning disable SYSLIB0050
using Android.App; using Android.Content.PM; using Android.OS; using Android.Views; using Android.Widget;
using AFormat = Android.Graphics.Format; using Ryujinx.HLE; using Ryujinx.HLE.FileSystem; using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc; using Ryujinx.Graphics.Vulkan; using Ryujinx.Audio.Backends.Dummy;
using Ryujinx.Audio.Integration; using Silk.NET.Vulkan; using System; using System.Collections.Concurrent;
using System.IO; using System.Linq; using System.Reflection; using System.Runtime.InteropServices; using System.Threading;
using SysEnv = System.Environment; using Switch = Ryujinx.HLE.Switch;

namespace DragoNX;
[Activity(Name="com.ryubing.android.GameActivity", Theme="@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation=ScreenOrientation.Landscape, Exported=false)]
public class GameActivity : Activity
{
    const BindingFlags All = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    string romPath=""; SurfaceView surfaceView; TextView logView; IntPtr nativeWindow=IntPtr.Zero; Thread emuThread; bool running=false; Switch device; VulkanRenderer gpu;
    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);
    [DllImport("android")] static extern void ANativeWindow_acquire(IntPtr window);
    [DllImport("android")] static extern void ANativeWindow_release(IntPtr window);
    [DllImport("android")] static extern int ANativeWindow_setBuffersGeometry(IntPtr window, int width, int height, int format);
    void MyLog(string s){ try{ RunOnUiThread(()=>{ if(logView!=null) logView.Text+= "\n"+s; }); var p1=Path.Combine(FilesDir.AbsolutePath,"crash.txt"); var p2="/storage/emulated/0/Download/Ryubing/ryubing_log.txt"; try{ Directory.CreateDirectory(Path.GetDirectoryName(p2)); File.AppendAllText(p2, DateTime.Now+": "+s+"\n"); }catch{} try{ File.AppendAllText(p1, DateTime.Now+": "+s+"\n"); }catch{} }catch{} }
    protected override void OnCreate(Bundle saved){ base.OnCreate(saved); if(Window!=null) Window.AddFlags(WindowManagerFlags.Fullscreen|WindowManagerFlags.KeepScreenOn); string extra=Intent.GetStringExtra("rom_path"); if(extra!=null) romPath=extra; if(romPath.Length==0){ string dir="/storage/emulated/0/Download/Ryubing/games"; if(Directory.Exists(dir)) foreach(var f in Directory.EnumerateFiles(dir,"*.*",SearchOption.AllDirectories)) if(f.EndsWith(".nsp",StringComparison.OrdinalIgnoreCase)||f.EndsWith(".xci",StringComparison.OrdinalIgnoreCase)){ romPath=f; break; } } surfaceView=new SurfaceView(this); surfaceView.Holder.SetFormat((AFormat)1); logView=new TextView(this); logView.Text="ROM: "+Path.GetFileName(romPath); logView.SetTextColor(global::Android.Graphics.Color.White); logView.TextSize=9; var root=new FrameLayout(this); root.AddView(surfaceView,new FrameLayout.LayoutParams(-1,-1)); root.AddView(logView,new FrameLayout.LayoutParams(-2,-2){ Gravity=GravityFlags.Top|GravityFlags.Left }); SetContentView(root); surfaceView.Holder.AddCallback(new CB(this)); MyLog("OnCreate OK - "+romPath); }
    class CB : Java.Lang.Object, ISurfaceHolderCallback{ readonly GameActivity a; public CB(GameActivity act){ a=act; } public void SurfaceCreated(ISurfaceHolder h){ var r=h.SurfaceFrame; if(r.Width()<=0) return; a.MyLog("SurfaceCreated "+r.Width()+"x"+r.Height()); a.nativeWindow=ANativeWindow_fromSurface(global::Android.Runtime.JNIEnv.Handle,h.Surface.Handle); ANativeWindow_setBuffersGeometry(a.nativeWindow,r.Width(),r.Height(),1); ANativeWindow_acquire(a.nativeWindow); if(a.emuThread!=null&&a.emuThread.IsAlive) return; a.running=true; a.emuThread=new Thread(a.Emu){ IsBackground=true }; a.emuThread.Start(); } public void SurfaceChanged(ISurfaceHolder h,AFormat f,int w,int ht){ if(a.nativeWindow!=IntPtr.Zero) ANativeWindow_setBuffersGeometry(a.nativeWindow,w,ht,1); } public void SurfaceDestroyed(ISurfaceHolder h){ a.running=false; if(a.nativeWindow!=IntPtr.Zero){ ANativeWindow_release(a.nativeWindow); a.nativeWindow=IntPtr.Zero; } } }

    void Emu(){
        try{
            MyLog("Emu START");
            string baseDir=Path.Combine(FilesDir.AbsolutePath,"Ryujinx");
            string sysDir=Path.Combine(baseDir,"system");
            Directory.CreateDirectory(sysDir);
            string keysDir=Path.Combine(baseDir,"keys");
            Directory.CreateDirectory(keysDir);
            string jitDir=Path.Combine(CacheDir.AbsolutePath,"jit");
            Directory.CreateDirectory(jitDir);
            SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE",jitDir);
            try{ Directory.SetCurrentDirectory(baseDir); SysEnv.CurrentDirectory=baseDir; }catch{}
            try{
                string[] prodSources = new[]{ "/storage/emulated/0/Ryujinx/keys/prod.keys", "/storage/emulated/0/Download/Ryubing/keys/prod.keys", "/storage/emulated/0/Download/Ryubing/prod.keys", "/storage/emulated/0/Download/prod.keys" };
                string[] titleSources = new[]{ "/storage/emulated/0/Ryujinx/keys/title.keys", "/storage/emulated/0/Download/Ryubing/keys/title.keys", "/storage/emulated/0/Download/Ryubing/title.keys", "/storage/emulated/0/Download/title.keys" };
                string destProd = Path.Combine(keysDir,"prod.keys");
                string destTitle = Path.Combine(keysDir,"title.keys");
                foreach(var src in prodSources){ if(File.Exists(src)){ File.Copy(src, destProd, true); break; } }
                foreach(var src in titleSources){ if(File.Exists(src)){ File.Copy(src, destTitle, true); break; } }
                MyLog($"Keys prod={File.Exists(destProd)} title={File.Exists(destTitle)}");
            }catch(Exception ex){ MyLog($"Keys FAIL: {ex.Message}"); }
            try{ typeof(VirtualFileSystem).GetField("_instance",All)?.SetValue(null,null); }catch{}
            VirtualFileSystem vfs=VirtualFileSystem.CreateInstance();
            vfs.ReloadKeySet(); vfs.ReloadKeySet();
            MyLog("VFS OK");
            var audio=new DummyHardwareDeviceDriver();
            if(nativeWindow==IntPtr.Zero) return;

            gpu=VulkanRenderer.Create("Ryubing",(inst,vk)=>{ unsafe{ var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)nativeWindow }; var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR"); var del=Marshal.GetDelegateForFunctionPointer<CDel>(fp); SurfaceKHR surf; del(inst,&ci,null,&surf); return surf; } },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});
            try{
                var mInit = gpu.GetType().GetMethod("Initialize", All);
                MyLog($"VK Initialize found={mInit!=null}");
                if(mInit!=null){
                    if(mInit.GetParameters().Length==0) mInit.Invoke(gpu,null);
                    else {
                        var debugLevelType = typeof(VulkanRenderer).Assembly.GetTypes().FirstOrDefault(t=>t.Name=="GraphicsDebugLevel");
                        object logLevel = debugLevelType!=null? Enum.Parse(debugLevelType, "None") : 0;
                        mInit.Invoke(gpu,new object[]{ logLevel });
                    }
                }
                var window = gpu.GetType().GetProperty("Window", All)?.GetValue(gpu);
                window?.GetType().GetMethod("SetSize", All)?.Invoke(window, new object[]{ surfaceView.Width, surfaceView.Height });
                MyLog($"VK OK {surfaceView.Width}x{surfaceView.Height}");
            }catch(Exception ex){ MyLog($"VK Init FAIL: {ex.InnerException?.ToString()??ex.ToString()}"); }
            MyLog("Vulkan OK");

            var conf=BuildHle(vfs,gpu,audio, baseDir, sysDir);
            MyLog("HLE FINAL OK");
            device=new Switch(conf);
            MyLog("Switch OK");

            var fi = new FileInfo(romPath);
            MyLog($"[NSP] {fi.Name} MB={fi.Length/(1024*1024)}");

            var loadMethods = device.GetType().GetMethods(All).Where(m=>m.Name=="LoadNsp").ToList();
            MethodInfo selected = loadMethods.FirstOrDefault();
            MyLog($"[LOAD] metodos={loadMethods.Count}");

            bool ok = false; object result = null;
            try{
                MyLog("[LOAD] Tentando 01006BB00C6F0000");
                result = selected.Invoke(device, new object[]{ romPath, 0x01006BB00C6F0000UL });
                ok = result is bool b? b : true;
            }catch(Exception ex){
                MyLog($"[LOAD] TitleId fail {ex.InnerException?.Message} -> tentando 0UL");
                result = selected.Invoke(device, new object[]{ romPath, (ulong)0 });
                ok = result is bool b2? b2 : true;
            }
            MyLog($"[LOAD] RESULTADO={result} OK={ok}");
            if(!ok) throw new Exception("LoadNsp false");
            MyLog("LoadNsp OK");

            RunOnUiThread(()=>{ logView.Visibility=ViewStates.Gone; });
            int frame=0; MyLog("LOOP INICIADO");
            while(running){
                try{ device.ProcessFrame(); frame++; if(frame%60==0) MyLog($"FRAME {frame}"); Thread.Sleep(16); }
                catch(Exception ex){ MyLog($"[FRAME {frame}] CRASH: {ex.InnerException?.ToString()??ex.ToString()}"); break; }
            }
        }catch(Exception ex){ MyLog("Emu CRASH: "+ex.ToString()); }
    }

    unsafe delegate Silk.NET.Vulkan.Result CDel(Instance i,AndroidSurfaceCreateInfoKHR* p,AllocationCallbacks* a,SurfaceKHR* s);

    HleConfiguration BuildHle(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio, string baseDir, string sysDir){
        var lhmType=typeof(LibHacHorizonManager); object lhm=null;
        foreach(var c in lhmType.GetConstructors(All)){ try{ var pr=c.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=baseDir; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } lhm=c.Invoke(ar); if(lhm!=null) break; }catch{} }
        try{
            var methods = lhmType.GetMethods(All).Where(m=>m.Name.Contains("Initialize")).ToList();
            methods.FirstOrDefault(x=>x.Name=="InitializeServer" && x.GetParameters().Length==0)?.Invoke(lhm,null);
            methods.FirstOrDefault(x=>x.Name=="InitializeFsServer" && x.GetParameters().Length==1)?.Invoke(lhm,new object[]{vfs});
            try{ methods.FirstOrDefault(x=>x.Name=="InitializeSystemClients")?.Invoke(lhm,null); }catch(Exception ex){ MyLog($"SystemClients ignorado: {ex.InnerException?.Message}"); }
        }catch{}
        object hc=null; try{ hc=lhm.GetType().GetProperty("Client",All)?.GetValue(lhm)?? lhm.GetType().GetField("_horizonClient",All)?.GetValue(lhm); }catch{}
        var amType=typeof(AccountManager); object accMan=null;
        try{ accMan=Activator.CreateInstance(amType,All,null,new object[]{ hc },null); }catch{ accMan=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(amType); amType.GetField("_horizonClient",All)?.SetValue(accMan,hc); amType.GetField("_profiles",All)?.SetValue(accMan,new ConcurrentDictionary<string, UserProfile>()); }
        var cmType=typeof(ContentManager); object cm=null; foreach(var c in cmType.GetConstructors(All)){ try{ var pr=c.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=baseDir; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } cm=c.Invoke(ar); if(cm!=null) break; }catch{} }
        var ucpType=typeof(UserChannelPersistence); object ucp=Activator.CreateInstance(ucpType,true);
        var hleType=typeof(HleConfiguration); var hleCtor=hleType.GetConstructors(All)[0]; var hps=hleCtor.GetParameters(); var hargs=new object[hps.Length];
        for(int k=0;k<hps.Length;k++){ var pt=hps[k].ParameterType; if(pt==typeof(string)) hargs[k]="UTC"; else if(pt==typeof(bool)) hargs[k]=true; else if(pt.Name=="MemoryManagerMode"){ try{ hargs[k]=Enum.Parse(pt,"SoftwarePageTable"); }catch{ hargs[k]=Enum.GetValues(pt).GetValue(0); } } else if(pt.IsEnum) hargs[k]=Enum.GetValues(pt).GetValue(0); else if(pt.IsValueType) hargs[k]=Activator.CreateInstance(pt); }
        var hle=(HleConfiguration)hleCtor.Invoke(hargs);
        try{ var memProp=hleType.GetProperties(All).FirstOrDefault(p=>p.Name.Contains("MemoryAllocation")); if(memProp!=null){ var reserve=Enum.Parse(memProp.PropertyType,"Reserve"); memProp.SetValue(hle,reserve); } }catch{}
        var confM=hleType.GetMethod("Configure",All); var cps=confM.GetParameters(); var cargs=new object[cps.Length];
        for(int k=0;k<cps.Length;k++){ var pt=cps[k].ParameterType; if(pt==typeof(VirtualFileSystem)) cargs[k]=vfs; else if(pt==typeof(LibHacHorizonManager)) cargs[k]=lhm; else if(pt==typeof(ContentManager)) cargs[k]=cm; else if(pt==typeof(AccountManager)) cargs[k]=accMan; else if(pt==typeof(UserChannelPersistence)) cargs[k]=ucp; else if(pt.IsAssignableFrom(gpu.GetType())) cargs[k]=gpu; else if(pt.FullName.Contains("IRenderer")) cargs[k]=gpu; else if(typeof(IHardwareDeviceDriver).IsAssignableFrom(pt)) cargs[k]=audio; }
        return confM.Invoke(hle,cargs) as HleConfiguration;
    }
}
