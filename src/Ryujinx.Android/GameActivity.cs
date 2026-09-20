#nullable disable
#pragma warning disable SYSLIB0050
using Android.App; using Android.Content.PM; using Android.OS; using Android.Views; using Android.Widget;
using AFormat = Android.Graphics.Format; using Ryujinx.HLE; using Ryujinx.HLE.FileSystem; using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc; using Ryujinx.Graphics.Vulkan; using Ryujinx.Audio.Backends.Dummy;
using Ryujinx.Audio.Integration; using Ryujinx.HLE.UI; using Silk.NET.Vulkan; using System; using System.Collections.Concurrent;
using System.IO; using System.Linq; using System.Reflection; using System.Runtime.InteropServices; using System.Threading;
using SysEnv = System.Environment; using Switch = Ryujinx.HLE.Switch;

namespace Ryujinx.Android;
[Activity(Name="com.ryubing.android.GameActivity", Theme="@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation=ScreenOrientation.Landscape, ConfigurationChanges=ConfigChanges.Orientation|ConfigChanges.ScreenSize|ConfigChanges.ScreenLayout|ConfigChanges.KeyboardHidden, Exported=false)]
public class GameActivity : Activity
{
    const BindingFlags All = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static class Holder { public static IntPtr nativeWindow=IntPtr.Zero; public static Thread emuThread; public static volatile bool running=false; public static Switch device; public static VulkanRenderer gpu; }
    string romPath=""; SurfaceView surfaceView; TextView logView;
    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);
    [DllImport("android")] static extern void ANativeWindow_release(IntPtr window);
    void MyLog(string s){ try{ RunOnUiThread(()=>{ if(logView!=null){ logView.Text+="\n"+s; if(logView.Text.Length>6000) logView.Text=logView.Text.Substring(logView.Text.Length-6000);} }); try{ var p="/storage/emulated/0/Download/Ryubing/ryubing_log.txt"; Directory.CreateDirectory(Path.GetDirectoryName(p)); File.AppendAllText(p, DateTime.Now+": "+s+" [tid="+SysEnv.CurrentManagedThreadId+"]\n"); }catch{} }catch{} }
    protected override void OnCreate(Bundle saved){
        base.OnCreate(saved);
        if(Holder.device!=null || Holder.emuThread!=null){
            MyLog($"OnCreate holder antigo dev={Holder.device?.GetHashCode()} thr={Holder.emuThread?.ManagedThreadId} alive={Holder.emuThread?.IsAlive}");
            bool ended=false; try{ Holder.running=false; if(Holder.emuThread!=null) ended=Holder.emuThread.Join(5000); }catch{ ended=false; }
            if(!ended && Holder.emuThread!=null && Holder.emuThread.IsAlive){ MyLog("WARNING thread antiga NAO terminou - NAO destruir Vulkan"); }
            else{ try{ Holder.device?.Dispose(); }catch{} try{ if(Holder.gpu is IDisposable d) d.Dispose(); }catch{} if(Holder.nativeWindow!=IntPtr.Zero){ try{ ANativeWindow_release(Holder.nativeWindow); }catch{} Holder.nativeWindow=IntPtr.Zero; } Holder.device=null; Holder.gpu=null; Holder.emuThread=null; }
        }
        if(Window!=null) Window.AddFlags(WindowManagerFlags.Fullscreen|WindowManagerFlags.KeepScreenOn);
        var extraPath=Intent.GetStringExtra("rom_path"); if(extraPath!=null) romPath=extraPath;
        surfaceView=new SurfaceView(this); logView=new TextView(this); logView.Text=Path.GetFileName(romPath); logView.SetTextColor(global::Android.Graphics.Color.White); logView.SetBackgroundColor(global::Android.Graphics.Color.Argb(180,0,0,0)); logView.TextSize=9;
        var root=new FrameLayout(this); root.AddView(surfaceView,new FrameLayout.LayoutParams(-1,-1)); root.AddView(logView,new FrameLayout.LayoutParams(-1,-2){ Gravity=GravityFlags.Top|GravityFlags.Left }); SetContentView(root);
        surfaceView.Holder.AddCallback(new CB(this)); MyLog($"OnCreate {romPath} [tid=1]");
    }
    public override void OnBackPressed(){ MyLog("OnBackPressed"); Holder.running=false; try{ Holder.emuThread?.Join(3000); }catch{} base.OnBackPressed(); }
    class CB : Java.Lang.Object, ISurfaceHolderCallback{
        readonly GameActivity a; public CB(GameActivity act){ a=act; }
        public void SurfaceCreated(ISurfaceHolder h){ var r=h.SurfaceFrame; if(r.Width()<=0) return; if(Holder.emuThread!=null && Holder.emuThread.IsAlive){ a.MyLog($"SurfaceCreated ignorando thread viva w={r.Width()} h={r.Height()}"); return; } a.MyLog($"Surface {r.Width()}x{r.Height()} [tid=1]"); try{ Holder.nativeWindow=ANativeWindow_fromSurface(global::Android.Runtime.JNIEnv.Handle,h.Surface.Handle); }catch(Exception e3){ a.MyLog("ANW fail "+e3.Message); return; } Holder.running=true; Holder.emuThread=new Thread(a.Emu){ IsBackground=true }; Holder.emuThread.Start(); }
        public void SurfaceChanged(ISurfaceHolder h,AFormat f,int w,int ht){}
        public void SurfaceDestroyed(ISurfaceHolder h){ a.MyLog("SurfaceDestroyed START"); Holder.running=false; if(Holder.emuThread!=null){ bool ended=false; try{ ended=Holder.emuThread.Join(5000); }catch{} if(!ended){ a.MyLog("WARNING EmuThread NAO TERMINOU - leak"); return; } } try{ Holder.device?.Dispose(); }catch{} Holder.device=null; try{ if(Holder.gpu is IDisposable d) d.Dispose(); }catch{} Holder.gpu=null; if(Holder.nativeWindow!=IntPtr.Zero){ try{ ANativeWindow_release(Holder.nativeWindow); }catch{} Holder.nativeWindow=IntPtr.Zero; } a.MyLog("SurfaceDestroyed END"); }
    }
    class DummyUIProxy : DispatchProxy { protected override object Invoke(MethodInfo m, object[] a){ var rt=m.ReturnType; if(rt==typeof(void)) return null; if(rt==typeof(bool)) return true; if(rt.IsValueType) return Activator.CreateInstance(rt); if(a!=null) for(int i=0;i<a.Length;i++) if(m.GetParameters()[i].IsOut) a[i]=null; return null; } }
    static IHostUIHandler CreateDummyUI() => DispatchProxy.Create<IHostUIHandler, DummyUIProxy>();
    void Emu(){
        int tid=SysEnv.CurrentManagedThreadId; MyLog($"Emu THREAD START id={tid}");
        try{
            // Força interpreter / sem PPTC pra testar SIGSEGV
            SysEnv.SetEnvironmentVariable("RYUJINX_DISABLE_PPTC", "1");
            SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE", Path.Combine(CacheDir.AbsolutePath,"jit"));
            string baseDir=Path.Combine(FilesDir.AbsolutePath,"Ryujinx"); Directory.CreateDirectory(Path.Combine(baseDir,"system")); Directory.CreateDirectory(Path.Combine(baseDir,"keys"));
            string jitDir=Path.Combine(CacheDir.AbsolutePath,"jit"); Directory.CreateDirectory(jitDir);
            try{ typeof(VirtualFileSystem).GetField("_instance",All)?.SetValue(null,null); }catch{}
            var vfs=VirtualFileSystem.CreateInstance(); vfs.ReloadKeySet();
            var audio=new DummyHardwareDeviceDriver();
            Holder.gpu=VulkanRenderer.Create("Ryubing",(inst,vk)=>{ unsafe{ var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)Holder.nativeWindow }; var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR"); var del=Marshal.GetDelegateForFunctionPointer<CDel>(fp); SurfaceKHR surf; del(inst,&ci,null,&surf); return surf; } },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});
            MyLog($"Vulkan OK [tid={tid}]");
            var conf=BuildHle(vfs,Holder.gpu,audio,baseDir,Path.Combine(baseDir,"system"));
            Holder.device=new Switch(conf);
            MyLog($"SWITCH CREATED hash={Holder.device.GetHashCode()} [tid={tid}]");
            MyLog($"Load {Path.GetFileName(romPath)} [tid={tid}]");
            if(romPath.EndsWith(".xci",StringComparison.OrdinalIgnoreCase)) Holder.device.LoadXci(romPath); else Holder.device.LoadNsp(romPath);
            MyLog($"Load END [tid={tid}]");
            try{
                int w=surfaceView.Width; int h=surfaceView.Height;
                if(w<=0 || h<=0){ var r=surfaceView.Holder.SurfaceFrame; w=r.Width(); h=r.Height(); }
                if(w<=0){ w=1280; h=720; }
                MyLog($"Window SetSize tentando {w}x{h} [tid={tid}]");
                var winProp=Holder.gpu.GetType().GetProperty("Window",All); var win=winProp?.GetValue(Holder.gpu);
                win?.GetType().GetMethod("SetSize",All)?.Invoke(win,new object[]{w,h});
                MyLog($"Window SetSize OK {w}x{h} [tid={tid}]");
            }catch(Exception eSz){ MyLog($"SetSize ERR {eSz.Message}"); }
            int frames=0;
            var hb=new Thread(()=>{ while(Holder.running){ MyLog($"HEARTBEAT tid={SysEnv.CurrentManagedThreadId} frames={frames}"); Thread.Sleep(500); } MyLog($"HEARTBEAT END"); }){ IsBackground=true }; hb.Start();
            MyLog($"LOOP RENDER ON [tid={tid}]");
            while(Holder.running && Holder.nativeWindow!=IntPtr.Zero){
                MyLog($"LOOP ITERATION {frames} START [tid={tid}]");
                try{
                    MyLog($"Frame {frames} Process START [tid={tid}]");
                    bool procDone=false;
                    var pw=new Thread(()=>{ Thread.Sleep(1500); if(!procDone) MyLog($"Frame {frames} Process WATCHDOG 1.5s BLOQUEADO [tid={tid}]"); }){ IsBackground=true }; pw.Start();
                    try{ Holder.device.ProcessFrame(); procDone=true; }catch(Exception eP){ procDone=true; MyLog($"Process EX f={frames} {eP} [tid={tid}]"); throw; }
                    MyLog($"Frame {frames} Process END [tid={tid}]");
                    if(frames==0){
                        MyLog($"SKIP Present 0 p/ diagnóstico tid={tid}");
                        Thread.Sleep(100);
                    }else{
                        MyLog($"Frame {frames} Present START [tid={tid}]");
                        Holder.device.PresentFrame(()=>{});
                        MyLog($"Frame {frames} Present END [tid={tid}]");
                    }
                    MyLog($"LOOP ITERATION {frames} END [tid={tid}]");
                    frames++;
                    Thread.Sleep(16);
                }catch(Exception eLoop){ MyLog($"LOOP EX f={frames} {eLoop} [tid={tid}]"); break; }
            }
            MyLog($"LOOP SAIU f={frames} [tid={tid}]");
        }catch(Exception eAll){ MyLog($"CRASH {eAll} [tid={SysEnv.CurrentManagedThreadId}]"); } finally{ MyLog($"Emu THREAD END id={tid}"); }
    }
    unsafe delegate Silk.NET.Vulkan.Result CDel(Instance i,AndroidSurfaceCreateInfoKHR* p,AllocationCallbacks* a,SurfaceKHR* s);
    HleConfiguration BuildHle(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio, string baseDir, string sysDir){
        var lhmType=typeof(LibHacHorizonManager); object lhm=null; foreach(var ci in lhmType.GetConstructors(All)){ try{ var pr=ci.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=baseDir; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } lhm=ci.Invoke(ar); if(lhm!=null) break; }catch{} }
        try{ var ms=lhmType.GetMethods(All).Where(m=>m.Name.Contains("Initialize")).ToList(); ms.FirstOrDefault(x=>x.Name=="InitializeServer" && x.GetParameters().Length==0)?.Invoke(lhm,null); ms.FirstOrDefault(x=>x.Name=="InitializeArpServer" && x.GetParameters().Length==0)?.Invoke(lhm,null); ms.FirstOrDefault(x=>x.Name=="InitializeFsServer" && x.GetParameters().Length==1)?.Invoke(lhm,new object[]{vfs}); }catch{}
        object hc=null; try{ var t=lhm.GetType(); hc=t.GetProperty("Client",All)?.GetValue(lhm)??t.GetField("_horizonClient",All)?.GetValue(lhm); }catch{}
        var amType=typeof(AccountManager); object accMan=null; try{ foreach(var ctorAm in amType.GetConstructors(All)){ var ps=ctorAm.GetParameters(); if(ps.Length>=1 && ps[0].ParameterType.IsInstanceOfType(hc)){ accMan=ctorAm.Invoke(ps.Length==1?new object[]{hc}:new object[]{hc,null}); break; } } }catch{ accMan=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(amType); amType.GetField("_horizonClient",All)?.SetValue(accMan,hc); var dict=new ConcurrentDictionary<string, UserProfile>(); amType.GetField("_profiles",All)?.SetValue(accMan,dict); var defId=amType.GetField("DefaultUserId",All)?.GetValue(null); if(defId!=null){ var upType=typeof(UserProfile); var p=upType.GetConstructors(All).First(c=>c.GetParameters().Length==3).Invoke(new object[]{defId,"RyuPlayer",new byte[0]}); dict.TryAdd(defId.ToString(),(UserProfile)p); } }
        var cmType=typeof(ContentManager); object cm=null; foreach(var ctorCm in cmType.GetConstructors(All)){ try{ var pr=ctorCm.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=baseDir; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } cm=ctorCm.Invoke(ar); if(cm!=null) break; }catch{} }
        var ucp=Activator.CreateInstance(typeof(UserChannelPersistence),true);
        var hleType=typeof(HleConfiguration); var hleCtor=hleType.GetConstructors(All)[0]; var hps=hleCtor.GetParameters(); var hargs=new object[hps.Length]; for(int k=0;k<hps.Length;k++){ var pt=hps[k].ParameterType; if(pt==typeof(string)) hargs[k]="UTC"; else if(pt==typeof(bool)) hargs[k]=true; else if(pt.IsEnum) hargs[k]=Enum.GetValues(pt).GetValue(0); else if(pt.IsValueType) hargs[k]=Activator.CreateInstance(pt); } var hle=(HleConfiguration)hleCtor.Invoke(hargs);
        try{ var prop = hleType.GetProperties(All).FirstOrDefault(p=>p.PropertyType.Name.Contains("UI")); prop?.SetValue(hle, CreateDummyUI()); }catch{}
        // Tenta forçar HostTrackedUnsafe
        try{
            foreach(var p in hleType.GetProperties(All)){
                if(p.Name.Contains("MemoryManager") && p.PropertyType.IsEnum){
                    try{ p.SetValue(hle, Enum.Parse(p.PropertyType, "HostTrackedUnsafe")); }catch{ try{ p.SetValue(hle, Enum.Parse(p.PropertyType, "HostTracked")); }catch{} }
                }
                if(p.Name.Contains("ExpandRam")){ try{ p.SetValue(hle, false); }catch{} }
            }
        }catch{}
        var confM=hleType.GetMethod("Configure",All); var cps=confM.GetParameters(); var cargs=new object[cps.Length]; for(int k=0;k<cps.Length;k++){ var pt=cps[k].ParameterType; if(pt==typeof(VirtualFileSystem)) cargs[k]=vfs; else if(pt==typeof(LibHacHorizonManager)) cargs[k]=lhm; else if(pt==typeof(ContentManager)) cargs[k]=cm; else if(pt==typeof(AccountManager)) cargs[k]=accMan; else if(pt==typeof(UserChannelPersistence)) cargs[k]=ucp; else if(pt.IsAssignableFrom(gpu.GetType())) cargs[k]=gpu; else if(pt.FullName.Contains("IRenderer")) cargs[k]=gpu; else if(typeof(IHardwareDeviceDriver).IsAssignableFrom(pt)) cargs[k]=audio; else if(typeof(IHostUIHandler).IsAssignableFrom(pt)) cargs[k]=CreateDummyUI(); }
        return confM.Invoke(hle,cargs) as HleConfiguration;
    }
}
