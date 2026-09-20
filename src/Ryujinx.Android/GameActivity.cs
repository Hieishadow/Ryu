#nullable disable
#pragma warning disable SYSLIB0050
using Android.App; using Android.Content.PM; using Android.OS; using Android.Views; using Android.Widget;
using AFormat = Android.Graphics.Format; using Ryujinx.HLE; using Ryujinx.HLE.FileSystem; using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc; using Ryujinx.Graphics.Vulkan; using Ryujinx.Audio.Backends.Dummy;
using Ryujinx.Audio.Integration; using Silk.NET.Vulkan; using System; using System.Collections.Concurrent;
using System.IO; using System.Linq; using System.Reflection; using System.Runtime.InteropServices; using System.Threading;
using SysEnv = System.Environment; using Switch = Ryujinx.HLE.Switch;
using AndEnv = Android.OS.Environment;

namespace Ryujinx.Android;
[Activity(Name="com.ryubing.android.GameActivity", Theme="@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation=ScreenOrientation.Landscape, ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.ScreenSize|ConfigChanges.ScreenLayout|ConfigChanges.KeyboardHidden, Exported=false)]
public class GameActivity : Activity
{
    const BindingFlags All = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    string romPath=""; SurfaceView surfaceView; TextView logView; IntPtr nativeWindow=IntPtr.Zero; Thread emuThread; bool running=false; Switch device; VulkanRenderer gpu;
    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);
    [DllImport("android")] static extern void ANativeWindow_release(IntPtr window);
    [DllImport("android")] static extern int ANativeWindow_setBuffersGeometry(IntPtr window, int width, int height, int format);
    void MyLog(string s){ try{ RunOnUiThread(()=>{ if(logView!=null){ logView.Text+= "\n"+s; if(logView.Text.Length>3000) logView.Text = logView.Text.Substring(logView.Text.Length-3000);} }); var p2="/storage/emulated/0/Download/Ryubing/ryubing_log.txt"; try{ Directory.CreateDirectory(Path.GetDirectoryName(p2)); File.AppendAllText(p2, DateTime.Now+": "+s+"\n"); }catch{} }catch{} }
    protected override void OnCreate(Bundle saved){ base.OnCreate(saved); if(Window!=null) Window.AddFlags(WindowManagerFlags.Fullscreen|WindowManagerFlags.KeepScreenOn); string extra=Intent.GetStringExtra("rom_path"); if(extra!=null) romPath=extra; if(romPath.Length==0){ string dir="/storage/emulated/0/Download/Ryubing/games"; if(Directory.Exists(dir)) foreach(var f in Directory.EnumerateFiles(dir,"*.*",SearchOption.AllDirectories)) if(f.EndsWith(".nsp",StringComparison.OrdinalIgnoreCase)||f.EndsWith(".xci",StringComparison.OrdinalIgnoreCase)){ romPath=f; break; } } surfaceView=new SurfaceView(this); logView=new TextView(this); logView.Text="ROM: "+Path.GetFileName(romPath); logView.SetTextColor(global::Android.Graphics.Color.White); logView.SetBackgroundColor(global::Android.Graphics.Color.Argb(180,0,0,0)); logView.TextSize=9; var root=new FrameLayout(this); root.AddView(surfaceView,new FrameLayout.LayoutParams(-1,-1)); root.AddView(logView,new FrameLayout.LayoutParams(-1,-2){ Gravity=GravityFlags.Top|GravityFlags.Left }); SetContentView(root); surfaceView.Holder.AddCallback(new CB(this)); MyLog($"OnCreate OK - {romPath}"); }
    public override void OnBackPressed(){ MyLog("Back pressed - saindo"); running=false; base.OnBackPressed(); }
    class CB : Java.Lang.Object, ISurfaceHolderCallback{ readonly GameActivity a; public CB(GameActivity act){ a=act; } public void SurfaceCreated(ISurfaceHolder h){ var r=h.SurfaceFrame; if(r.Width()<=0) return; if(a.emuThread!=null && a.emuThread.IsAlive){ a.MyLog("Emu já rodando"); return; } a.MyLog($"SurfaceCreated {r.Width()}x{r.Height()}"); try{ a.nativeWindow=ANativeWindow_fromSurface(global::Android.Runtime.JNIEnv.Handle,h.Surface.Handle); ANativeWindow_setBuffersGeometry(a.nativeWindow,r.Width(),r.Height(),1); }catch(Exception ex){ a.MyLog("ANativeWindow fail: "+ex.Message); return; } a.running=true; a.emuThread=new Thread(a.Emu){ IsBackground=true }; a.emuThread.Start(); } public void SurfaceChanged(ISurfaceHolder h,AFormat f,int w,int ht){ if(a.nativeWindow!=IntPtr.Zero) ANativeWindow_setBuffersGeometry(a.nativeWindow,w,ht,1); } public void SurfaceDestroyed(ISurfaceHolder h){ a.MyLog("SurfaceDestroyed"); a.running=false; try{ a.emuThread?.Join(1500); }catch{} try{ a.device?.Dispose(); }catch{} a.device=null; a.gpu=null; if(a.nativeWindow!=IntPtr.Zero){ ANativeWindow_release(a.nativeWindow); a.nativeWindow=IntPtr.Zero; } } }

    void Emu(){ try{
        MyLog("Emu START");
        string baseDir=Path.Combine(FilesDir.AbsolutePath,"Ryujinx");
        string sysDir=Path.Combine(baseDir,"system");
        Directory.CreateDirectory(sysDir);
        Directory.CreateDirectory(Path.Combine(baseDir,"keys"));
        string jitDir=Path.Combine(CacheDir.AbsolutePath,"jit");
        Directory.CreateDirectory(jitDir);
        SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE",jitDir);
        try{ Directory.SetCurrentDirectory(baseDir); }catch{}
        try{
            string[] keySources = new[]{ "/storage/emulated/0/Ryujinx/keys/prod.keys", "/storage/emulated/0/Download/Ryubing/keys/prod.keys" };
            string destKey = Path.Combine(baseDir,"keys","prod.keys");
            if(!File.Exists(destKey)){ foreach(var src in keySources){ if(File.Exists(src)){ File.Copy(src, destKey, true); MyLog($"Keys {new FileInfo(destKey).Length}"); break; } } }
            if(File.Exists(destKey)) MyLog($"prod.keys OK {new FileInfo(destKey).Length}");
        }catch{}
        try{ typeof(VirtualFileSystem).GetField("_instance",All)?.SetValue(null,null); }catch{}
        VirtualFileSystem vfs=VirtualFileSystem.CreateInstance();
        vfs.ReloadKeySet();
        MyLog("VFS OK");
        var audio=new DummyHardwareDeviceDriver();
        if(nativeWindow==IntPtr.Zero){ MyLog("nativeWindow ZERO"); return; }
        gpu=VulkanRenderer.Create("Ryubing",(inst,vk)=>{ unsafe{ var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)nativeWindow }; var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR"); var del=Marshal.GetDelegateForFunctionPointer<CDel>(fp); SurfaceKHR surf; del(inst,&ci,null,&surf); return surf; } },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});
        try{ var mInit=gpu.GetType().GetMethod("Initialize",All); if(mInit!=null){ if(mInit.GetParameters().Length==0) mInit.Invoke(gpu,null); else mInit.Invoke(gpu,new object[]{0}); MyLog("Vulkan Initialize() OK"); } }catch(Exception ex){ MyLog("Vulkan Init fail: "+(ex.InnerException?.Message??ex.Message)); }
        MyLog("Vulkan OK");
        var conf=BuildHle(vfs,gpu,audio, baseDir, sysDir);
        MyLog("HLE FINAL OK");
        device=new Switch(conf);
        MyLog("Switch OK FINAL");
        MyLog($"Load {Path.GetFileName(romPath)} {new FileInfo(romPath).Length}");
        if(romPath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase)) device.LoadXci(romPath); else device.LoadNsp(romPath);
        MyLog("Load retornou OK");
        MyLog("LOOP ENTER - RENDER ON");
        int frames=0;
        long lastTick = SysEnv.TickCount64;
        while(running && nativeWindow!=IntPtr.Zero){
            try{
                device.ProcessFrame();
                device.PresentFrame(()=>{});
                frames++;
                if(frames==1) MyLog("Primeiro frame OK!");
                if(SysEnv.TickCount64 - lastTick > 1000){
                    MyLog($"frames {frames} - RODANDO");
                    lastTick = SysEnv.TickCount64;
                }
            }catch(Exception exL){
                MyLog($"ITER ERRO: {exL.Message}");
                Thread.Sleep(16);
            }
        }
        MyLog($"Fim loop frames={frames}");
    }catch(Exception ex){ MyLog($"Emu CRASH: {ex}"); } }

    unsafe delegate Silk.NET.Vulkan.Result CDel(Instance i,AndroidSurfaceCreateInfoKHR* p,AllocationCallbacks* a,SurfaceKHR* s);

    HleConfiguration BuildHle(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio, string baseDir, string sysDir){
        var lhmType=typeof(LibHacHorizonManager); object lhm=null;
        foreach(var c in lhmType.GetConstructors(All)){ try{ var pr=c.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=baseDir; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } lhm=c.Invoke(ar); if(lhm!=null) break; }catch{} }
        try{
            var methods = lhmType.GetMethods(All).Where(m=>m.Name.Contains("Initialize")).ToList();
            methods.FirstOrDefault(x=>x.Name=="InitializeServer" && x.GetParameters().Length==0)?.Invoke(lhm,null);
            methods.FirstOrDefault(x=>x.Name=="InitializeArpServer" && x.GetParameters().Length==0)?.Invoke(lhm,null);
            methods.FirstOrDefault(x=>x.Name=="InitializeFsServer" && x.GetParameters().Length==1)?.Invoke(lhm,new object[]{vfs});
        }catch{}
        object hc=null; try{ var t=lhm.GetType(); foreach(var m in t.GetMembers(All)){ if(m is PropertyInfo pi && pi.PropertyType.Name.Contains("HorizonClient")){ hc=pi.GetValue(lhm); if(hc!=null) break; } if(m is FieldInfo fi && fi.FieldType.Name.Contains("HorizonClient")){ hc=fi.GetValue(lhm); if(hc!=null) break; } } if(hc==null) hc=t.GetProperty("Client",All)?.GetValue(lhm)?? t.GetField("_horizonClient",All)?.GetValue(lhm); }catch{}
        object accMan=null;
        if(hc!=null){
            try{
                var amType=typeof(AccountManager);
                foreach(var ctor in amType.GetConstructors(All)){ var ps=ctor.GetParameters(); if(ps.Length>=1 && ps[0].ParameterType.IsInstanceOfType(hc)){ object[] args = ps.Length==1? new object[]{ hc } : new object[]{ hc, null }; accMan=ctor.Invoke(args); break; } }
            }catch{
                try{
                    var amType=typeof(AccountManager); accMan=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(amType);
                    amType.GetField("_horizonClient",All)?.SetValue(accMan,hc);
                    var dict=new ConcurrentDictionary<string, UserProfile>(); amType.GetField("_profiles",All)?.SetValue(accMan,dict);
                    var defId = amType.GetField("DefaultUserId",All)?.GetValue(null);
                    if(defId!=null){ var upType=typeof(UserProfile); object profile=null; foreach(var c in upType.GetConstructors(All)){ if(c.GetParameters().Length==3){ profile=c.Invoke(new object[]{ defId, "RyuPlayer", new byte[0] }); break; } } if(profile!=null){ dict.TryAdd(defId.ToString(), (UserProfile)profile); } }
                }catch{}
            }
        }
        if(accMan==null) throw new Exception("AccountManager NULL");
        var cmType=typeof(ContentManager); object cm=null; foreach(var c in cmType.GetConstructors(All)){ try{ var pr=c.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=baseDir; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } cm=c.Invoke(ar); if(cm!=null) break; }catch{} }
        var ucpType=typeof(UserChannelPersistence); object ucp=Activator.CreateInstance(ucpType,true);
        var hleType=typeof(HleConfiguration); var hleCtor=hleType.GetConstructors(All)[0]; var hps=hleCtor.GetParameters(); var hargs=new object[hps.Length]; for(int k=0;k<hps.Length;k++){ var pt=hps[k].ParameterType; if(pt==typeof(string)) hargs[k]="UTC"; else if(pt==typeof(bool)) hargs[k]=true; else if(pt.IsEnum) hargs[k]=Enum.GetValues(pt).GetValue(0); else if(pt.IsValueType) hargs[k]=Activator.CreateInstance(pt); } var hle=(HleConfiguration)hleCtor.Invoke(hargs);
        var confM=hleType.GetMethod("Configure",All); var cps=confM.GetParameters(); var cargs=new object[cps.Length];
        for(int k=0;k<cps.Length;k++){ var pt=cps[k].ParameterType; if(pt==typeof(VirtualFileSystem)) cargs[k]=vfs; else if(pt==typeof(LibHacHorizonManager)) cargs[k]=lhm; else if(pt==typeof(ContentManager)) cargs[k]=cm; else if(pt==typeof(AccountManager)) cargs[k]=accMan; else if(pt==typeof(UserChannelPersistence)) cargs[k]=ucp; else if(pt.IsAssignableFrom(gpu.GetType())) cargs[k]=gpu; else if(pt.FullName.Contains("IRenderer")) cargs[k]=gpu; else if(typeof(IHardwareDeviceDriver).IsAssignableFrom(pt)) cargs[k]=audio; }
        return confM.Invoke(hle,cargs) as HleConfiguration;
    }
}
