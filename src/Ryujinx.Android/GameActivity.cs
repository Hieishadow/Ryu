#nullable disable
#pragma warning disable SYSLIB0050
using Android.App; using Android.Content.PM; using Android.OS; using Android.Views; using Android.Widget;
using AFormat = Android.Graphics.Format; using Ryujinx.HLE; using Ryujinx.HLE.FileSystem; using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc; using Ryujinx.Graphics.Vulkan; using Ryujinx.Audio.Backends.Dummy;
using Ryujinx.Audio.Integration; using Silk.NET.Vulkan; using System; using System.Collections.Concurrent;
using System.IO; using System.Linq; using System.Reflection; using System.Runtime.InteropServices; using System.Threading;
using SysEnv = System.Environment; using Switch = Ryujinx.HLE.Switch;

namespace Ryujinx.Android;
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

    void Emu(){ try{
        MyLog("Emu START");
        string baseDir=Path.Combine(FilesDir.AbsolutePath,"Ryujinx");
        string sysDir=Path.Combine(baseDir,"system");
        Directory.CreateDirectory(sysDir);
        string keysDir=Path.Combine(baseDir,"keys");
        Directory.CreateDirectory(keysDir);
        string jitDir=Path.Combine(CacheDir.AbsolutePath,"jit");
        Directory.CreateDirectory(jitDir);
        SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE",jitDir);
        try{ Directory.SetCurrentDirectory(baseDir); SysEnv.CurrentDirectory=baseDir; MyLog("CWD -> "+baseDir); }catch(Exception ex){ MyLog("CWD fail: "+ex.Message); }
        try{
            string[] keySources = new[]{
                "/storage/emulated/0/Ryujinx/keys/prod.keys",
                "/storage/emulated/0/Download/Ryubing/keys/prod.keys",
                "/storage/emulated/0/Download/Ryubing/prod.keys",
                "/storage/emulated/0/Download/prod.keys"
            };
            string destKey = Path.Combine(keysDir,"prod.keys");
            if(!File.Exists(destKey)){
                foreach(var src in keySources){
                    if(File.Exists(src)){
                        File.Copy(src, destKey, true);
                        MyLog($"Keys copiada de {src} -> {destKey} ({new FileInfo(destKey).Length} bytes)");
                        break;
                    }
                }
            }
            if(File.Exists(destKey)) MyLog($"prod.keys OK {new FileInfo(destKey).Length} bytes");
            else MyLog("AVISO: prod.keys NAO encontrada!");
        }catch(Exception ex){ MyLog("Copy keys fail: "+ex.Message); }
        try{ typeof(VirtualFileSystem).GetField("_instance",All)?.SetValue(null,null); }catch{}
        VirtualFileSystem vfs=VirtualFileSystem.CreateInstance();
        vfs.ReloadKeySet();
        MyLog("VFS OK - Keys loaded: OK");
        var audio=new DummyHardwareDeviceDriver();
        if(nativeWindow==IntPtr.Zero){ MyLog("nativeWindow ZERO"); return; }
        gpu=VulkanRenderer.Create("Ryubing",(inst,vk)=>{ unsafe{ var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)nativeWindow }; var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR"); var del=Marshal.GetDelegateForFunctionPointer<CDel>(fp); SurfaceKHR surf; del(inst,&ci,null,&surf); return surf; } },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});
        try{
            var mInit = gpu.GetType().GetMethod("Initialize", All);
            if(mInit!=null){
                var pars = mInit.GetParameters();
                if(pars.Length==0) mInit.Invoke(gpu, null);
                else mInit.Invoke(gpu, new object[]{ 0 });
                MyLog("Vulkan Initialize() OK via reflection");
            }
        }catch(Exception ex){ MyLog("Vulkan Initialize fail: "+(ex.InnerException?.Message??ex.Message)); }
        MyLog("Vulkan OK "+gpu.GetType().FullName+" Initialized");
        var conf=BuildHle(vfs,gpu,audio, baseDir, sysDir);
        MyLog("HLE FINAL OK");
        device=new Switch(conf);
        MyLog("Switch OK - DEPOIS new Switch FINAL");
        device.LoadNsp(romPath);
        MyLog("LoadNsp OK - Iniciando frames...");
        RunOnUiThread(()=>{ logView.Visibility=ViewStates.Gone; });
        int frames=0;
        while(running){
            device.ProcessFrame();
            device.PresentFrame(()=>{});
            frames++;
            if(frames%60==0) MyLog($"PresentFrame OK - {frames} frames");
            Thread.Yield();
        }
    }catch(Exception ex){ MyLog("Emu CRASH: "+ex.ToString()); try{ typeof(VirtualFileSystem).GetField("_instance",All)?.SetValue(null,null); }catch{} } }

    unsafe delegate Silk.NET.Vulkan.Result CDel(Instance i,AndroidSurfaceCreateInfoKHR* p,AllocationCallbacks* a,SurfaceKHR* s);

    HleConfiguration BuildHle(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio, string baseDir, string sysDir){
        string profilesPath=Path.Combine(sysDir,"Profiles.json");
        var lhmType=typeof(LibHacHorizonManager); object lhm=null;
        foreach(var c in lhmType.GetConstructors(All)){ try{ var pr=c.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=baseDir; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } lhm=c.Invoke(ar); if(lhm!=null) break; }catch{} }
        MyLog("LHM -> "+(lhm==null?"NULL":lhm.GetType().Name));
        try{
            var fsClientProp=lhmType.GetProperty("FsClient",All);
            MyLog("LHM.FsClient BEFORE init -> "+(fsClientProp?.GetValue(lhm)==null?"NULL":"OK"));

            // Inicialização na ordem correta com ignore
            var methods = lhmType.GetMethods(All).Where(m=>m.Name.Contains("Initialize")).ToList();

            // 1. Server
            try{ var m = methods.FirstOrDefault(x=>x.Name=="InitializeServer" && x.GetParameters().Length==0); m?.Invoke(lhm,null); MyLog("LHM.InitializeServer() OK"); }catch(Exception ex){ MyLog($"LHM.InitializeServer fail: {ex.InnerException?.Message??ex.Message}"); }
            // 2. Arp
            try{ var m = methods.FirstOrDefault(x=>x.Name=="InitializeArpServer" && x.GetParameters().Length==0); m?.Invoke(lhm,null); MyLog("LHM.InitializeArpServer() OK"); }catch(Exception ex){ MyLog($"LHM.InitializeArpServer fail (ignorado): {ex.InnerException?.Message??ex.Message}"); }
            // 3. Bcat - IGNORADO (não precisa pro Zelda)
            try{ var m = methods.FirstOrDefault(x=>x.Name=="InitializeBcatServer" && x.GetParameters().Length==0); m?.Invoke(lhm,null); MyLog("LHM.InitializeBcatServer() OK"); }catch(Exception ex){ MyLog($"LHM.InitializeBcatServer fail (ignorado): {ex.InnerException?.Message??ex.Message}"); }
            // 4. Fs - OBRIGATÓRIO
            try{ var m = methods.FirstOrDefault(x=>x.Name=="InitializeFsServer" && x.GetParameters().Length==1); m?.Invoke(lhm,new object[]{vfs}); MyLog("LHM.InitializeFsServer(VFS) OK"); }catch(Exception ex){ MyLog($"LHM.InitializeFsServer FAIL: {ex.InnerException?.Message??ex.Message}"); }
            // 5. SystemClients - TENTA MAS NÃO TRAVA SE FALHAR
            try{ var m = methods.FirstOrDefault(x=>x.Name=="InitializeSystemClients" && x.GetParameters().Length==0); m?.Invoke(lhm,null); MyLog("LHM.InitializeSystemClients() OK"); }catch(Exception ex){ MyLog($"LHM.InitializeSystemClients fail (ignorado): {ex.InnerException?.Message??ex.Message}"); }
            // 6. ApplicationClient se existir
            try{ var m = methods.FirstOrDefault(x=>x.Name=="InitializeApplicationClient"); if(m!=null) MyLog($"LHM has {m.Name} - skip (feito no Switch ctor)"); }catch{}

            var fsAfter=fsClientProp?.GetValue(lhm);
            MyLog("LHM.FsClient AFTER init -> "+(fsAfter==null?"NULL":fsAfter.GetType().Name));
        }catch(Exception ex){ MyLog("LHM init check fail: "+ex.Message); }

        object hc=null; try{ var t=lhm.GetType(); foreach(var m in t.GetMembers(All)){ if(m is PropertyInfo pi && pi.PropertyType.Name.Contains("HorizonClient")){ hc=pi.GetValue(lhm); if(hc!=null) break; } if(m is FieldInfo fi && fi.FieldType.Name.Contains("HorizonClient")){ hc=fi.GetValue(lhm); if(hc!=null) break; } } if(hc==null) hc=t.GetProperty("Client",All)?.GetValue(lhm)?? t.GetField("_horizonClient",All)?.GetValue(lhm)?? t.GetField("_client",All)?.GetValue(lhm); }catch(Exception ex){ MyLog("Get HC ex: "+ex.Message); }
        MyLog("HorizonClient -> "+(hc==null?"NULL":hc.GetType().Name));
        object accMan=null;
        if(hc!=null){
            try{
                var amType=typeof(AccountManager);
                foreach(var ctor in amType.GetConstructors(All)){
                    var ps=ctor.GetParameters();
                    if(ps.Length>=1 && ps[0].ParameterType.IsInstanceOfType(hc)){
                        object[] args = ps.Length==1? new object[]{ hc } : new object[]{ hc, null };
                        accMan=ctor.Invoke(args);
                        MyLog("AM criado via ctor(HorizonClient) OK");
                        break;
                    }
                }
            }catch(Exception ex){
                string msg = ex.InnerException?.Message?? ex.Message;
                MyLog("AM ctor fail: "+msg);
                try{
                    var amType=typeof(AccountManager);
                    accMan=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(amType);
                    amType.GetField("_horizonClient",All)?.SetValue(accMan,hc);
                    var dict=new ConcurrentDictionary<string, UserProfile>();
                    amType.GetField("_profiles",All)?.SetValue(accMan,dict);
                    amType.GetField("_storedOpenedUsers",All)?.SetValue(accMan, new UserProfile[0]);
                    var f_asdm=amType.GetField("_accountSaveDataManager",All);
                    if(f_asdm!=null){
                        var asdmType=f_asdm.FieldType;
                        object asdm=null;
                        try{ asdm=Activator.CreateInstance(asdmType,All,null,new object[]{ dict },null); }
                        catch{ asdm=Activator.CreateInstance(asdmType,true); }
                        foreach(var fi in asdmType.GetFields(All)){
                            if(fi.FieldType==typeof(string)){
                                try{
                                    var v=fi.GetValue(asdm) as string;
                                    if(v!=null && (v.Contains("Profiles") || v.Contains("/system") || v=="system")){
                                        fi.SetValue(asdm, profilesPath);
                                    }
                                }catch{}
                            }
                        }
                        f_asdm.SetValue(accMan,asdm);
                    }
                    var defId = amType.GetField("DefaultUserId",All)?.GetValue(null);
                    if(defId!=null){
                        byte[] img=new byte[0];
                        try{
                            var resType=typeof(Ryujinx.Common.EmbeddedResources);
                            var readM=resType.GetMethod("Read",All);
                            if(readM!=null) img=(byte[])readM.Invoke(null,new object[]{ "Ryujinx.HLE/HOS/Services/Account/Acc/DefaultUserImage.jpg" });
                        }catch{}
                        var upType=typeof(UserProfile);
                        object profile=null;
                        foreach(var c in upType.GetConstructors(All)){
                            if(c.GetParameters().Length==3){
                                profile=c.Invoke(new object[]{ defId, "RyuPlayer", img });
                                break;
                            }
                        }
                        if(profile!=null){
                            dict.TryAdd(defId.ToString(), (UserProfile)profile);
                            amType.GetProperty("LastOpenedUser",All)?.SetValue(accMan, profile);
                            try{ upType.GetProperty("AccountState")?.SetValue(profile, 1); }catch{}
                        }
                    }
                    MyLog("AM fallback PATCHED OK -> "+profilesPath);
                }catch(Exception ex2){ MyLog("AM fallback fail: "+ex2.ToString()); accMan=null; }
            }
        }
        if(accMan==null) throw new Exception("AccountManager NULL - abort");
        MyLog("AccountManager -> "+accMan.GetType().FullName+" | Profiles: "+profilesPath);
        var cmType=typeof(ContentManager); object cm=null; foreach(var c in cmType.GetConstructors(All)){ try{ var pr=c.GetParameters(); var ar=new object[pr.Length]; for(int k=0;k<pr.Length;k++){ if(pr[k].ParameterType==typeof(VirtualFileSystem)) ar[k]=vfs; else if(pr[k].ParameterType==typeof(string)) ar[k]=baseDir; else if(pr[k].ParameterType.IsValueType) ar[k]=Activator.CreateInstance(pr[k].ParameterType); } cm=c.Invoke(ar); if(cm!=null) break; }catch{} }
        var ucpType=typeof(UserChannelPersistence); object ucp=Activator.CreateInstance(ucpType,true);
        var hleType=typeof(HleConfiguration); var hleCtor=hleType.GetConstructors(All)[0]; var hps=hleCtor.GetParameters(); var hargs=new object[hps.Length]; for(int k=0;k<hps.Length;k++){ var pt=hps[k].ParameterType; if(pt==typeof(string)) hargs[k]="UTC"; else if(pt==typeof(bool)) hargs[k]=true; else if(pt.IsEnum) hargs[k]=Enum.GetValues(pt).GetValue(0); else if(pt.IsValueType) hargs[k]=Activator.CreateInstance(pt); } var hle=(HleConfiguration)hleCtor.Invoke(hargs);
        var confM=hleType.GetMethod("Configure",All); var cps=confM.GetParameters(); var cargs=new object[cps.Length];
        for(int k=0;k<cps.Length;k++){ var pt=cps[k].ParameterType; if(pt==typeof(VirtualFileSystem)) cargs[k]=vfs; else if(pt==typeof(LibHacHorizonManager)) cargs[k]=lhm; else if(pt==typeof(ContentManager)) cargs[k]=cm; else if(pt==typeof(AccountManager)) cargs[k]=accMan; else if(pt==typeof(UserChannelPersistence)) cargs[k]=ucp; else if(pt.IsAssignableFrom(gpu.GetType())) cargs[k]=gpu; else if(pt.FullName.Contains("IRenderer")) cargs[k]=gpu; else if(typeof(IHardwareDeviceDriver).IsAssignableFrom(pt)) cargs[k]=audio; }
        return confM.Invoke(hle,cargs) as HleConfiguration;
    }
}
