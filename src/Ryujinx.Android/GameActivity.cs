#nullable disable
#pragma warning disable SYSLIB0050
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AFormat = Android.Graphics.Format;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.Audio;
using Ryujinx.Audio.Common;
using Ryujinx.Audio.Integration;
using Ryujinx.HLE.UI;
using Ryujinx.Memory;
using Silk.NET.Vulkan;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using SysEnv = System.Environment;
using Switch = Ryujinx.HLE.Switch;

namespace Ryujinx.Android
{
[Activity(Name="com.ryubing.android.GameActivity", Theme="@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation=ScreenOrientation.Landscape, ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.ScreenSize|ConfigChanges.ScreenLayout|ConfigChanges.KeyboardHidden, Exported=false)]
public class GameActivity : Activity
{
    class NullAudioSession : IHardwareDeviceSession {
        public bool RegisterBuffer(AudioBuffer b) => true;
        public void UnregisterBuffer(AudioBuffer b) {}
        public void QueueBuffer(AudioBuffer b) {}
        public bool WasBufferFullyConsumed(AudioBuffer b) => true;
        public void SetVolume(float v) {}
        public float GetVolume() => 1f;
        public ulong GetPlayedSampleCount() => 0;
        public void Start() {}
        public void Stop() {}
        public void PrepareToClose() {}
        public void Dispose() {}
    }
    class NullAudioDriver : IHardwareDeviceDriver {
        public static bool IsSupported => true;
        public float Volume { get; set; } = 1f;
        public IHardwareDeviceSession OpenDeviceSession(IHardwareDeviceDriver.Direction d, IVirtualMemoryManager m, SampleFormat f, uint r, uint c) => new NullAudioSession();
        public IHardwareDeviceSession OpenDeviceSession(IHardwareDeviceDriver.Direction d, IVirtualMemoryManager m, SampleFormat f, uint r, uint c, float v=1f) => new NullAudioSession();
        public ManualResetEvent GetUpdateRequiredEvent() => new ManualResetEvent(false);
        public ManualResetEvent GetPauseEvent() => new ManualResetEvent(true);
        public bool SupportsDirection(IHardwareDeviceDriver.Direction d) => true;
        public bool SupportsSampleRate(uint r) => true;
        public bool SupportsSampleFormat(SampleFormat f) => true;
        public bool SupportsChannelCount(uint c) => true;
        public void Dispose() {}
    }
    const BindingFlags All = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static class Holder { public static IntPtr nativeWindow=IntPtr.Zero; public static Thread emuThread; public static volatile bool running=false; public static Switch device; public static VulkanRenderer gpu; }
    string romPath=""; SurfaceView surfaceView; TextView logView;
    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);
    [DllImport("android")] static extern void ANativeWindow_release(IntPtr window);
    void FileLog(string s){ try{ var p="/storage/emulated/0/Download/Ryubing/ryubing_log.txt"; Directory.CreateDirectory(Path.GetDirectoryName(p)); File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss.fff")+" [FILE] "+s+"\n"); }catch{} }
    void CrashLog(string name, string s){ try{ var p=$"/storage/emulated/0/Download/Ryubing/{name}.txt"; Directory.CreateDirectory(Path.GetDirectoryName(p)); File.WriteAllText(p, DateTime.Now.ToString()+"\n"+s+"\n"); }catch{} }
    void DumpLogcat(string reason){
        try{
            var p="/storage/emulated/0/Download/Ryubing/logcat_crash.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            var proc = Java.Lang.Runtime.GetRuntime().Exec("logcat -d -t 300");
            using(var reader = new StreamReader(proc.InputStream)){
                var log = reader.ReadToEnd();
                File.WriteAllText(p, DateTime.Now+"\nREASON: "+reason+"\n\n"+log);
            }
        }catch(Exception ex){ FileLog($"DumpLogcat FAIL {ex.Message}"); }
    }
    void MyLog(string s){ try{ RunOnUiThread(()=>{ if(logView!=null){ logView.Text+="\n"+s; if(logView.Text.Length>4000) logView.Text=logView.Text.Substring(logView.Text.Length-4000);} }); FileLog(s); }catch{ FileLog(s); } }
    protected override void OnCreate(Bundle saved){
        base.OnCreate(saved);
        try {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                var msg=e.ExceptionObject.ToString();
                CrashLog("crash_game_domain", msg); FileLog($"DOMAIN {msg}"); DumpLogcat("DOMAIN "+msg);
            };
            TaskScheduler.UnobservedTaskException += (s, e) => {
                CrashLog("crash_game_task", e.Exception.ToString()); FileLog($"TASK {e.Exception}"); DumpLogcat("TASK "+e.Exception); e.SetObserved();
            };
        } catch {}
        if(Holder.device!=null || Holder.emuThread!=null){
            try{ Holder.running=false; Holder.emuThread?.Join(2000); }catch{}
            try{ Holder.device?.Dispose(); }catch{}
            try{ if(Holder.gpu is IDisposable d) d.Dispose(); }catch{}
            if(Holder.nativeWindow!=IntPtr.Zero){ try{ ANativeWindow_release(Holder.nativeWindow); }catch{} Holder.nativeWindow=IntPtr.Zero; }
            Holder.device=null; Holder.gpu=null; Holder.emuThread=null;
            try{ VirtualFileSystem.ResetForAndroid(); }catch{}
            try{ typeof(VirtualFileSystem).GetField("_instance",All)?.SetValue(null,null); }catch{}
            GC.Collect();
        }
        if(Window!=null) Window.AddFlags(WindowManagerFlags.Fullscreen|WindowManagerFlags.KeepScreenOn);
        var extraPath=Intent.GetStringExtra("rom_path"); if(extraPath!=null) romPath=extraPath;
        surfaceView=new SurfaceView(this); logView=new TextView(this); logView.Text=Path.GetFileName(romPath); logView.SetTextColor(global::Android.Graphics.Color.White); logView.SetBackgroundColor(global::Android.Graphics.Color.Argb(180,0,0,0)); logView.TextSize=10;
        var root=new FrameLayout(this); root.AddView(surfaceView,new FrameLayout.LayoutParams(-1,-1)); root.AddView(logView,new FrameLayout.LayoutParams(-1,-2){ Gravity=GravityFlags.Top|GravityFlags.Left }); SetContentView(root);
        surfaceView.Holder.AddCallback(new CB(this)); MyLog($"OnCreate {romPath} v14 BYPASS");
    }
    public override void OnBackPressed(){ Holder.running=false; try{ Holder.emuThread?.Join(2000); }catch{} try{ VirtualFileSystem.ResetForAndroid(); }catch{} base.OnBackPressed(); }
    class CB : Java.Lang.Object, ISurfaceHolderCallback{
        readonly GameActivity a; public CB(GameActivity act){ a=act; }
        public void SurfaceCreated(ISurfaceHolder h){
            a.FileLog($"v14 BYPASS ANW {h.SurfaceFrame.Width()}x{h.SurfaceFrame.Height()}");
            var r=h.SurfaceFrame; if(r.Width()<=0) return;
            if(Holder.emuThread!=null && Holder.emuThread.IsAlive) return;
            try{
                long ptr = 0;
                try{
                    var surf = h.Surface;
                    var fld = surf.Class.GetField("mNativeObject");
                    if(fld!=null){ ptr = fld.GetLong(surf); a.FileLog($"mNativeObject field = {ptr:X}"); }
                }catch(Exception ex){ a.FileLog($"field fail {ex.Message}"); }
                try{
                    if(ptr==0){
                        var fld2 = Java.Lang.Class.FromType(typeof(Android.Views.Surface)).GetDeclaredField("mNativeObject");
                        fld2.Accessible = true;
                        ptr = fld2.GetLong(h.Surface);
                        a.FileLog($"mNativeObject declared = {ptr:X}");
                    }
                }catch(Exception ex){ a.FileLog($"declared fail {ex.Message}"); }
                if(ptr!=0){
                    Holder.nativeWindow = new IntPtr(ptr);
                    a.FileLog($"BYPASS OK nativeWindow={Holder.nativeWindow} ptr={ptr:X}");
                }else{
                    a.FileLog("BYPASS falhou, tentando ANativeWindow_fromSurface");
                    Thread.Sleep(100);
                    Holder.nativeWindow=ANativeWindow_fromSurface(IntPtr.Zero, h.Surface.Handle);
                    a.FileLog($"ANW fallback OK {Holder.nativeWindow}");
                }
            }catch(Exception e3){ a.CrashLog("crash_anw", e3.ToString()); a.FileLog($"ANW FAIL {e3}"); a.DumpLogcat("ANW "+e3.Message); return; }
            if(Holder.nativeWindow == IntPtr.Zero) { a.FileLog("ANW ZERO FINAL"); a.DumpLogcat("ZERO"); return; }
            Holder.running=true;
            Holder.emuThread=new Thread(()=>{ try{ a.Emu(); }catch(Exception ex){ a.FileLog($"OUTER {ex}"); a.CrashLog("crash_outer", ex.ToString()); a.DumpLogcat("OUTER "+ex.Message); } }){ IsBackground=true };
            Holder.emuThread.Start();
        }
        public void SurfaceChanged(ISurfaceHolder h,AFormat f,int w,int ht){
            a.FileLog($"Changed {w}x{ht}");
            try{ var winProp=Holder.gpu?.GetType().GetProperty("Window",All); var win=winProp?.GetValue(Holder.gpu); win?.GetType().GetMethod("SetSize",All)?.Invoke(win,new object[]{w,ht}); }catch{}
        }
        public void SurfaceDestroyed(ISurfaceHolder h){
            a.FileLog("Destroyed");
            Holder.running=false;
            if(Holder.emuThread!=null){ try{ Holder.emuThread.Join(4000); }catch{} }
            try{ Holder.device?.Dispose(); }catch{} Holder.device=null;
            try{ if(Holder.gpu is IDisposable d) d.Dispose(); }catch{} Holder.gpu=null;
            Holder.nativeWindow=IntPtr.Zero;
            try{ VirtualFileSystem.ResetForAndroid(); }catch{} try{ GC.Collect(); }catch{}
        }
    }
    class DummyUIProxy : DispatchProxy { protected override object Invoke(MethodInfo m, object[] a){ var rt=m.ReturnType; if(rt==typeof(void)) return null; if(rt==typeof(bool)) return true; if(rt.IsValueType) return Activator.CreateInstance(rt); if(a!=null) for(int i=0;i<a.Length;i++) if(m.GetParameters()[i].IsOut) a[i]=null; return null; } }
    static IHostUIHandler CreateDummyUI() => DispatchProxy.Create<IHostUIHandler, DummyUIProxy>();
    void Emu(){
        FileLog("Emu ENTER v14");
        try{
            SysEnv.SetEnvironmentVariable("RYUJINX_DISABLE_PPTC", "1");
            string baseDir=Path.Combine(FilesDir.AbsolutePath,"Ryujinx");
            Directory.CreateDirectory(Path.Combine(baseDir,"system"));
            Directory.CreateDirectory(Path.Combine(baseDir,"keys"));
            string jitDir=Path.Combine(CacheDir.AbsolutePath,"jit"); Directory.CreateDirectory(jitDir);
            SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE",jitDir);
            try{ var srcProd="/storage/emulated/0/Download/Ryubing/keys/prod.keys"; var dstProd=Path.Combine(baseDir,"keys/prod.keys"); if(File.Exists(srcProd)) File.Copy(srcProd,dstProd,true); FileLog($"COPIADO {new FileInfo(dstProd).Length}"); }catch(Exception ex){ FileLog($"COPY FAIL {ex.Message}"); }
            try{ VirtualFileSystem.ResetForAndroid(); }catch{}
            try{ typeof(VirtualFileSystem).GetField("_instance",All)?.SetValue(null,null); }catch{}
            var vfs=VirtualFileSystem.CreateInstance(); vfs.ReloadKeySet(); FileLog("KeySet OK");
            IHardwareDeviceDriver audio = new NullAudioDriver();
            FileLog("ANTES Vulkan");
            Holder.gpu=VulkanRenderer.Create("Ryubing",(inst,vk)=>{ unsafe{ var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)Holder.nativeWindow }; var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR"); var del=Marshal.GetDelegateForFunctionPointer<CDel>(fp); SurfaceKHR surf; var res=del(inst,&ci,null,&surf); FileLog($"vkCreate res {res} surf {surf.Handle}"); return surf; } },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});
            FileLog("DEPOIS Vulkan Create");
            try{ var initMethod = Holder.gpu.GetType().GetMethod("Initialize", All); if(initMethod!= null) { var paramType = initMethod.GetParameters()[0].ParameterType; var enumVal = Enum.ToObject(paramType, 0); initMethod.Invoke(Holder.gpu, new object[]{ enumVal }); FileLog("[VK] Init OK"); } }catch(Exception exInit){ FileLog($"[VK] Init FAIL {exInit}"); }
            var conf=BuildHle(vfs,Holder.gpu,audio,baseDir,Path.Combine(baseDir,"system")); FileLog("BuildHle OK");
            Holder.device=new Switch(conf); FileLog($"ANTES Load {romPath}");
            if(romPath.EndsWith(".xci",StringComparison.OrdinalIgnoreCase)) Holder.device.LoadXci(romPath); else Holder.device.LoadNsp(romPath);
            FileLog("DEPOIS Load");
            int w=1280; int h=720; try{ var sf=surfaceView.Holder.SurfaceFrame; if(sf.Width()>0){ w=sf.Width(); h=sf.Height(); } }catch{}
            try{ var winProp=Holder.gpu.GetType().GetProperty("Window",All); var win=winProp?.GetValue(Holder.gpu); win?.GetType().GetMethod("SetSize",All)?.Invoke(win,new object[]{w,h}); }catch{}
            int frames=0; FileLog($"LOOP {w}x{h}");
            while(Holder.running && Holder.nativeWindow!=IntPtr.Zero){
                try{ Holder.device.ProcessFrame(); Holder.device.PresentFrame(()=>{}); frames++; if(frames==1){ FileLog("FIRST FRAME OK - RENDERIZOU!"); RunOnUiThread(()=>{ try{ logView.Visibility=ViewStates.Gone; }catch{} }); } if(frames%60==0) FileLog($"Frame {frames}"); Thread.Sleep(16); }catch(Exception eLoop){ FileLog($"LOOP EX {eLoop}"); CrashLog("crash_loop", eLoop.ToString()); DumpLogcat("LOOP "+eLoop.Message); break; }
            }
        }catch(Exception eAll){ FileLog($"CRASH GERAL {eAll}"); CrashLog("crash_emu", eAll.ToString()); DumpLogcat("GERAL "+eAll.Message); } finally{ FileLog("Emu END"); }
    }
    unsafe delegate Silk.NET.Vulkan.Result CDel(Instance i,AndroidSurfaceCreateInfoKHR* p,AllocationCallbacks* a,SurfaceKHR* s);
    HleConfiguration BuildHle(VirtualFileSystem vfs, VulkanRenderer gpu, IHardwareDeviceDriver audio, string baseDir, string sysDir){
        var lhmType=typeof(LibHacHorizonManager); object lhm=null; foreach(var ci in lhmType.GetConstructors(All)){ try{ var pr=ci.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=baseDir; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } lhm=ci.Invoke(ar); if(lhm!=null) break; }catch{} }
        try{ var ms=lhmType.GetMethods(All).Where(m=>m.Name.Contains("Initialize")).ToList(); ms.FirstOrDefault(x=>x.Name=="InitializeServer" && x.GetParameters().Length==0)?.Invoke(lhm,null); ms.FirstOrDefault(x=>x.Name=="InitializeArpServer" && x.GetParameters().Length==0)?.Invoke(lhm,null); ms.FirstOrDefault(x=>x.Name=="InitializeFsServer" && x.GetParameters().Length==1)?.Invoke(lhm,new object[]{vfs}); }catch{}
        object hc=null; try{ var t=lhm.GetType(); hc=t.GetProperty("Client",All)?.GetValue(lhm)??t.GetField("_horizonClient",All)?.GetValue(lhm); }catch{}
        var amType=typeof(AccountManager); object accMan=null; try{ foreach(var ctorAm in amType.GetConstructors(All)){ var ps=ctorAm.GetParameters(); if(ps.Length>=1 && ps[0].ParameterType.IsInstanceOfType(hc)){ accMan=ctorAm.Invoke(ps.Length==1?new object[]{hc}:new object[]{hc,null}); break; } } }catch{ accMan=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(amType); amType.GetField("_instance",All)?.SetValue(null,null); amType.GetField("_horizonClient",All)?.SetValue(accMan,hc); var dict=new ConcurrentDictionary<string, UserProfile>(); amType.GetField("_profiles",All)?.SetValue(accMan,dict); var defId=amType.GetField("DefaultUserId",All)?.GetValue(null); if(defId!=null){ var upType=typeof(UserProfile); var p=upType.GetConstructors(All).First(c=>c.GetParameters().Length==3).Invoke(new object[]{defId,"RyuPlayer",new byte[0]}); dict.TryAdd(defId.ToString(),(UserProfile)p); } }
        var cmType=typeof(ContentManager); object cm=null; foreach(var ctorCm in cmType.GetConstructors(All)){ try{ var pr=ctorCm.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=baseDir; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } cm=ctorCm.Invoke(ar); if(cm!=null) break; }catch{} }
        var ucp=Activator.CreateInstance(typeof(UserChannelPersistence),true);
        var hleType=typeof(HleConfiguration); var hleCtor=hleType.GetConstructors(All)[0]; var hps=hleCtor.GetParameters(); var hargs=new object[hps.Length]; for(int k=0;k<hps.Length;k++){ var pt=hps[k].ParameterType; if(pt==typeof(string)) hargs[k]="UTC"; else if(pt==typeof(bool)) hargs[k]=true; else if(pt.IsEnum) hargs[k]=Enum.GetValues(pt).GetValue(0); else if(pt.IsValueType) hargs[k]=Activator.CreateInstance(pt); } var hle=(HleConfiguration)hleCtor.Invoke(hargs);
        try{ var prop = hleType.GetProperties(All).FirstOrDefault(p=>p.PropertyType.Name.Contains("UI")); prop?.SetValue(hle, CreateDummyUI()); }catch{}
        try{ foreach(var p in hleType.GetProperties(All)){ if(p.Name.Contains("MemoryManager") && p.PropertyType.IsEnum){ try{ p.SetValue(hle, Enum.Parse(p.PropertyType, "Software")); }catch{ try{ p.SetValue(hle, Enum.Parse(p.PropertyType, "HostTracked")); }catch{} } } if(p.Name.Contains("ExpandRam")){ try{ p.SetValue(hle, false); }catch{} } } }catch{}
        var confM=hleType.GetMethod("Configure",All); var cps=confM.GetParameters(); var cargs=new object[cps.Length]; for(int k=0;k<cps.Length;k++){ var pt=cps[k].ParameterType; if(pt==typeof(VirtualFileSystem)) cargs[k]=vfs; else if(pt==typeof(LibHacHorizonManager)) cargs[k]=lhm; else if(pt==typeof(ContentManager)) cargs[k]=cm; else if(pt==typeof(AccountManager)) cargs[k]=accMan; else if(pt==typeof(UserChannelPersistence)) cargs[k]=ucp; else if(pt.IsAssignableFrom(gpu.GetType())) cargs[k]=gpu; else if(pt.FullName.Contains("IRenderer")) cargs[k]=gpu; else if(typeof(IHardwareDeviceDriver).IsAssignableFrom(pt)) cargs[k]=audio; else if(typeof(IHostUIHandler).IsAssignableFrom(pt)) cargs[k]=CreateDummyUI(); }
        return confM.Invoke(hle,cargs) as HleConfiguration;
    }
}
}
