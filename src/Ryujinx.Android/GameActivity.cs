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
    string romPath=""; SurfaceView surfaceView; TextView logView; IntPtr nativeWindow=IntPtr.Zero; Thread emuThread; volatile bool running=false; Switch device; VulkanRenderer gpu;
    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);
    [DllImport("android")] static extern void ANativeWindow_release(IntPtr window);
    void MyLog(string s){ try{ RunOnUiThread(()=>{ if(logView!=null){ logView.Text+= "\n"+s; if(logView.Text.Length>4000) logView.Text=logView.Text.Substring(logView.Text.Length-4000);} }); try{ var p="/storage/emulated/0/Download/Ryubing/ryubing_log.txt"; Directory.CreateDirectory(Path.GetDirectoryName(p)); File.AppendAllText(p, DateTime.Now+": "+s+" [tid="+SysEnv.CurrentManagedThreadId+"]\n"); }catch{} }catch{} }
    protected override void OnCreate(Bundle saved){ base.OnCreate(saved); if(Window!=null) Window.AddFlags(WindowManagerFlags.Fullscreen|WindowManagerFlags.KeepScreenOn); var ex=Intent.GetStringExtra("rom_path"); if(ex!=null) romPath=ex; if(romPath.Length==0){ var d="/storage/emulated/0/Download/Ryubing/games"; if(Directory.Exists(d)) foreach(var f in Directory.EnumerateFiles(d,"*.*",SearchOption.AllDirectories)) if(f.EndsWith(".nsp",StringComparison.OrdinalIgnoreCase)||f.EndsWith(".xci",StringComparison.OrdinalIgnoreCase)){ romPath=f; break; } } surfaceView=new SurfaceView(this); logView=new TextView(this); logView.Text=Path.GetFileName(romPath); logView.SetTextColor(global::Android.Graphics.Color.White); logView.SetBackgroundColor(global::Android.Graphics.Color.Argb(180,0,0,0)); logView.TextSize=9; var root=new FrameLayout(this); root.AddView(surfaceView,new FrameLayout.LayoutParams(-1,-1)); root.AddView(logView,new FrameLayout.LayoutParams(-1,-2){ Gravity=GravityFlags.Top|GravityFlags.Left }); SetContentView(root); surfaceView.Holder.AddCallback(new CB(this)); MyLog($"OnCreate {romPath} mainTid={SysEnv.CurrentManagedThreadId}"); }
    public override void OnBackPressed(){ MyLog("OnBackPressed - stopping"); running=false; try{ emuThread?.Join(3000); }catch{} base.OnBackPressed(); }
    class CB : Java.Lang.Object, ISurfaceHolderCallback{
        readonly GameActivity a; public CB(GameActivity act){ a=act; }
        public void SurfaceCreated(ISurfaceHolder h){
            var r=h.SurfaceFrame; if(r.Width()<=0) return; if(a.emuThread!=null && a.emuThread.IsAlive) return;
            a.MyLog($"Surface {r.Width()}x{r.Height()}");
            try{ a.nativeWindow=ANativeWindow_fromSurface(global::Android.Runtime.JNIEnv.Handle,h.Surface.Handle); }catch(Exception ex){ a.MyLog("ANW fail "+ex.Message); return; }
            a.running=true; a.emuThread=new Thread(a.Emu){ IsBackground=true }; a.emuThread.Start();
        }
        public void SurfaceChanged(ISurfaceHolder h,AFormat f,int w,int ht){}
        public void SurfaceDestroyed(ISurfaceHolder h){
            a.MyLog("SurfaceDestroyed START"); a.running=false; bool ended=true;
            if(a.emuThread!=null){ try{ ended=a.emuThread.Join(5000); }catch{ ended=false; } if(!ended){ a.MyLog("WARNING EmuThread NAO TERMINOU"); return; } }
            a.MyLog("EmuThread OK Dispose"); try{ a.device?.Dispose(); }catch{} a.device=null;
            try{ if(a.gpu is IDisposable d) d.Dispose(); }catch{} a.gpu=null;
            if(a.nativeWindow!=IntPtr.Zero){ ANativeWindow_release(a.nativeWindow); a.nativeWindow=IntPtr.Zero; }
            a.MyLog("SurfaceDestroyed END");
        }
    }
    class DummyUIProxy : DispatchProxy { protected override object Invoke(MethodInfo m, object[] a){ var rt=m.ReturnType; if(rt==typeof(void)) return null; if(rt==typeof(bool)) return true; if(rt.IsValueType) return Activator.CreateInstance(rt); if(a!=null) for(int i=0;i<a.Length;i++) if(m.GetParameters()[i].IsOut) a[i]=null; return null; } }
    static IHostUIHandler CreateDummyUI() => DispatchProxy.Create<IHostUIHandler, DummyUIProxy>();

    void Emu(){
        int tid=SysEnv.CurrentManagedThreadId; MyLog($"Emu THREAD START id={tid}");
        try{
            string baseDir=Path.Combine(FilesDir.AbsolutePath,"Ryujinx"); Directory.CreateDirectory(Path.Combine(baseDir,"system")); Directory.CreateDirectory(Path.Combine(baseDir,"keys"));
            string jitDir=Path.Combine(CacheDir.AbsolutePath,"jit"); Directory.CreateDirectory(jitDir); SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE",jitDir);
            try{ Directory.SetCurrentDirectory(baseDir); }catch{}
            try{ var dk=Path.Combine(baseDir,"keys","prod.keys"); if(!File.Exists(dk)){ foreach(var s in new[]{ "/storage/emulated/0/Ryubing/keys/prod.keys", "/storage/emulated/0/Download/Ryubing/keys/prod.keys" }) if(File.Exists(s)){ File.Copy(s,dk,true); break; } } }catch{}
            try{ typeof(VirtualFileSystem).GetField("_instance",All)?.SetValue(null,null); }catch{}
            var vfs=VirtualFileSystem.CreateInstance(); vfs.ReloadKeySet();
            var audio=new DummyHardwareDeviceDriver();
            gpu=VulkanRenderer.Create("Ryubing",(inst,vk)=>{ unsafe{ var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)nativeWindow }; var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR"); var del=Marshal.GetDelegateForFunctionPointer<CDel>(fp); SurfaceKHR surf; del(inst,&ci,null,&surf); return surf; } },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});
            try{ var mi=gpu.GetType().GetMethod("Initialize",All); if(mi!=null){ if(mi.GetParameters().Length==0) mi.Invoke(gpu,null); else mi.Invoke(gpu,new object[]{0}); } }catch{}
            MyLog("Vulkan OK");
            var conf=BuildHle(vfs,gpu,audio,baseDir,Path.Combine(baseDir,"system"));
            device=new Switch(conf); MyLog($"SWITCH CREATED hash={device.GetHashCode()}");
            MyLog($"Load {Path.GetFileName(romPath)}"); if(romPath.EndsWith(".xci",StringComparison.OrdinalIgnoreCase)) device.LoadXci(romPath); else device.LoadNsp(romPath); MyLog("Load END");
            try{ var winProp=gpu.GetType().GetProperty("Window",All); var win=winProp?.GetValue(gpu); win?.GetType().GetMethod("SetSize",All)?.Invoke(win,new object[]{1280,720}); MyLog("Window SetSize OK"); }catch{}
            MyLog("LOOP RENDER ON"); int frames=0; long last=SysEnv.TickCount64;
            while(running && nativeWindow!=IntPtr.Zero){
                try{
                    MyLog($"Frame {frames} Process START"); device.ProcessFrame(); MyLog($"Frame {frames} Process END");
                    MyLog($"Frame {frames} Present START"); device.PresentFrame(()=>{}); MyLog($"Frame {frames} Present END");
                    frames++; if(SysEnv.TickCount64-last>1000){ MyLog($"RODANDO frames={frames}"); last=SysEnv.TickCount64; }
                }catch(Exception ex){ MyLog($"LOOP EX f={frames} {ex}"); break; }
            }
            MyLog($"LOOP SAIU f={frames}");
        }catch(Exception ex){ MyLog($"CRASH {ex}"); } finally{ MyLog($"Emu THREAD END id={tid}"); }
    }
    unsafe delegate Silk.NET.Vulkan.Result CDel(Instance i,AndroidSurfaceCreateInfoKHR* p,AllocationCallbacks* a,SurfaceKHR* s);
    HleConfiguration BuildHle(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio, string baseDir, string sysDir){
        var lhmType=typeof(LibHacHorizonManager); object lhm=null;
        foreach(var ci in lhmType.GetConstructors(All)){ try{ var pr=ci.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=baseDir; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } lhm=ci.Invoke(ar); if(lhm!=null) break; }catch{} }
        try{ var ms=lhmType.GetMethods(All).Where(m=>m.Name.Contains("Initialize")).ToList(); ms.FirstOrDefault(x=>x.Name=="InitializeServer" && x.GetParameters().Length==0)?.Invoke(lhm,null); ms.FirstOrDefault(x=>x.Name=="InitializeArpServer" && x.GetParameters().Length==0)?.Invoke(lhm,null); ms.FirstOrDefault(x=>x.Name=="InitializeFsServer" && x.GetParameters().Length==1)?.Invoke(lhm,new object[]{vfs}); }catch{}
        object hc=null; try{ var t=lhm.GetType(); hc=t.GetProperty("Client",All)?.GetValue(lhm)??t.GetField("_horizonClient",All)?.GetValue(lhm); }catch{}
        var amType=typeof(AccountManager); object accMan=null;
        try{ foreach(var ctorAm in amType.GetConstructors(All)){ var ps=ctorAm.GetParameters(); if(ps.Length>=1 && ps[0].ParameterType.IsInstanceOfType(hc)){ accMan=ctorAm.Invoke(ps.Length==1?new object[]{hc}:new object[]{hc,null}); break; } } }catch{ accMan=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(amType); amType.GetField("_horizonClient",All)?.SetValue(accMan,hc); var dict=new ConcurrentDictionary<string, UserProfile>(); amType.GetField("_profiles",All)?.SetValue(accMan,dict); var defId=amType.GetField("DefaultUserId",All)?.GetValue(null); if(defId!=null){ var upType=typeof(UserProfile); var p=upType.GetConstructors(All).First(c=>c.GetParameters().Length==3).Invoke(new object[]{defId,"RyuPlayer",new byte[0]}); dict.TryAdd(defId.ToString(),(UserProfile)p); } }
        var cmType=typeof(ContentManager); object cm=null; foreach(var ctorCm in cmType.GetConstructors(All)){ try{ var pr=ctorCm.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=baseDir; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } cm=ctorCm.Invoke(ar); if(cm!=null) break; }catch{} }
        var ucp=Activator.CreateInstance(typeof(UserChannelPersistence),true);
        var hleType=typeof(HleConfiguration); var hleCtor=hleType.GetConstructors(All)[0]; var hps=hleCtor.GetParameters(); var hargs=new object[hps.Length]; for(int k=0;k<hps.Length;k++){ var pt=hps[k].ParameterType; if(pt==typeof(string)) hargs[k]="UTC"; else if(pt==typeof(bool)) hargs[k]=true; else if(pt.IsEnum) hargs[k]=Enum.GetValues(pt).GetValue(0); else if(pt.IsValueType) hargs[k]=Activator.CreateInstance(pt); } var hle=(HleConfiguration)hleCtor.Invoke(hargs);
        try{ var prop = hleType.GetProperties(All).FirstOrDefault(p=>p.PropertyType.Name.Contains("UI")); prop?.SetValue(hle, CreateDummyUI()); }catch{}
        var confM=hleType.GetMethod("Configure",All); var cps=confM.GetParameters(); var cargs=new object[cps.Length];
        for(int k=0;k<cps.Length;k++){ var pt=cps[k].ParameterType; if(pt==typeof(VirtualFileSystem)) cargs[k]=vfs; else if(pt==typeof(LibHacHorizonManager)) cargs[k]=lhm; else if(pt==typeof(ContentManager)) cargs[k]=cm; else if(pt==typeof(AccountManager)) cargs[k]=accMan; else if(pt==typeof(UserChannelPersistence)) cargs[k]=ucp; else if(pt.IsAssignableFrom(gpu.GetType())) cargs[k]=gpu; else if(pt.FullName.Contains("IRenderer")) cargs[k]=gpu; else if(typeof(IHardwareDeviceDriver).IsAssignableFrom(pt)) cargs[k]=audio; else if(typeof(IHostUIHandler).IsAssignableFrom(pt)) cargs[k]=CreateDummyUI(); }
        return confM.Invoke(hle,cargs) as HleConfiguration;
    }
}
