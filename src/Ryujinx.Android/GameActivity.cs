using Android.App; using Android.Content.PM; using Android.OS; using Android.Views; using Android.Widget;
using AFormat = Android.Graphics.Format; using Ryujinx.HLE; using Ryujinx.HLE.FileSystem; using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc; using Ryujinx.Graphics.Vulkan; using Ryujinx.Audio.Backends.Dummy;
using Silk.NET.Vulkan; using System; using System.IO; using System.Linq; using System.Reflection;
using System.Runtime.InteropServices; using System.Threading; using SysEnv = System.Environment; using Switch = Ryujinx.HLE.Switch;
using Ryujinx.Graphics; using Ryujinx.Audio; using Ryujinx.HLE.UI;
using Ryujinx.Common.Configuration; using Ryujinx.Common.Configuration.Hid; using Ryujinx.Common.Configuration.Multiplayer;

namespace DragoNX;

[Activity(Name = "com.ryubing.android.GameActivity", Label = "Ryubing", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = ScreenOrientation.Landscape, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden, Exported = false, MainLauncher = false)]
public class GameActivity : Activity
{
    const string TAG = "Ryubing"; const BindingFlags CtorFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly string LogFile = "/storage/emulated/0/Download/Ryubing/ryubing_log.txt"; static string _lastBaseDir = "";
    string romPath = ""; SurfaceView surfaceView = null!; TextView logView = null!; TextView fpsView = null!;
    Thread? emuThread; volatile bool running = false; IntPtr nativeWindow = IntPtr.Zero; Switch? device; VulkanRenderer? gpu;

    [DllImport("android")] static extern IntPtr ANativeWindow_fromSurface(IntPtr env, IntPtr surface);
    [DllImport("android")] static extern void ANativeWindow_acquire(IntPtr window);
    [DllImport("android")] static extern void ANativeWindow_release(IntPtr window);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        try { Console.SetOut(new StringWriter()); Console.SetError(new StringWriter()); } catch {}
        try { var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>{try{return a.GetTypes();}catch{return Type.EmptyTypes;}}).ToList(); var logger = all.FirstOrDefault(t=>t.Name=="Logger"); logger?.GetMethod("ClearTargets", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)?.Invoke(null,null); } catch {}
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
        romPath = Intent?.GetStringExtra("rom_path")?? "";
        try { File.WriteAllText(LogFile, $"=== Ryubing LOG {DateTime.Now} ===\nROM: {romPath}\n"); } catch {}
        if (string.IsNullOrEmpty(romPath) ||!File.Exists(romPath)) { var dir = "/storage/emulated/0/Download/Ryubing/games"; if (Directory.Exists(dir)) { var first = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories).FirstOrDefault(p => p.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".xci", StringComparison.OrdinalIgnoreCase)); if (first!= null) romPath = first; } }
        surfaceView = new SurfaceView(this); surfaceView.Holder!.SetFormat(AFormat.Opaque);
        logView = new TextView(this); logView.Text = $"RYUBING\n{Path.GetFileName(romPath)}\n"; logView.SetTextColor(global::Android.Graphics.Color.White); logView.SetBackgroundColor(global::Android.Graphics.Color.Black); logView.TextSize=10; logView.SetPadding(20,20,20,20);
        fpsView = new TextView(this) { Text = "FPS: --" }; fpsView.SetTextColor(global::Android.Graphics.Color.Lime); fpsView.TextSize=13; fpsView.SetPadding(20,30,20,20);
        var root = new FrameLayout(this); root.AddView(surfaceView, new FrameLayout.LayoutParams(-1, -1)); root.AddView(logView, new FrameLayout.LayoutParams(-1, -1)); root.AddView(fpsView, new FrameLayout.LayoutParams(-2, -2) { Gravity = GravityFlags.Top | GravityFlags.Left });
        SetContentView(root); surfaceView.Holder!.AddCallback(new SurfaceCallback(this));
    }
    void LogAppend(string m){ try{ File.AppendAllText(LogFile,DateTime.Now.ToString("HH:mm:ss")+" "+m+"\n"); }catch{} RunOnUiThread(()=>{ if(logView!=null) logView.Text+="\n"+m; }); }
    void LogError(string m){ try{ File.AppendAllText(LogFile,DateTime.Now.ToString("HH:mm:ss")+" ERRO: "+m+"\n"); }catch{} RunOnUiThread(()=>{ if(logView!=null){ logView.Text+="\nERRO: "+m; logView.SetTextColor(global::Android.Graphics.Color.Red); } }); }
    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback {
        readonly GameActivity act; public SurfaceCallback(GameActivity a)=>act=a;
        public void SurfaceCreated(ISurfaceHolder h){ var rect=h.SurfaceFrame; if(rect.Width()<=0||rect.Height()<=0) return; act.nativeWindow=ANativeWindow_fromSurface(global::Android.Runtime.JNIEnv.Handle,h.Surface!.Handle); if(act.nativeWindow==IntPtr.Zero){ act.LogError("ANativeWindow Zero!"); return; } ANativeWindow_acquire(act.nativeWindow); if(act.emuThread==null||!act.emuThread.IsAlive){ act.running=true; act.emuThread=new Thread(act.EmulationLoop){ IsBackground=true, Priority=System.Threading.ThreadPriority.Highest, Name="RyujinxEmu" }; act.emuThread.Start(); } }
        public void SurfaceChanged(ISurfaceHolder h,AFormat f,int w,int ht){} public void SurfaceDestroyed(ISurfaceHolder h){ act.running=false; if(act.nativeWindow!=IntPtr.Zero){ ANativeWindow_release(act.nativeWindow); act.nativeWindow=IntPtr.Zero; } }
    }
    void EmulationLoop()
    {
        try{
            try { Console.SetOut(new StringWriter()); Console.SetError(new StringWriter()); } catch {}
            LogAppend($"Iniciando {Path.GetFileName(romPath)}");
            string baseDir=Path.Combine(FilesDir!.AbsolutePath,"Ryujinx"); _lastBaseDir = baseDir; string systemDir=Path.Combine(baseDir,"system"); Directory.CreateDirectory(systemDir);
            CopyKeys(baseDir,systemDir); CopyFirmware(baseDir); InitAppData(baseDir);
            string jitDir=Path.Combine(CacheDir!.AbsolutePath,"jit"); Directory.CreateDirectory(jitDir); SysEnv.SetEnvironmentVariable("RYUJINX_JIT_CACHE",jitDir);
            LogAppend("Criando VFS..."); VirtualFileSystem vfs; try{ vfs=VirtualFileSystem.CreateInstance(); LogAppend("VFS Criado"); } catch(Exception ex){ LogAppend($"CreateInstance falhou: {ex.GetType().Name}: {ex.Message}"); var prop=typeof(VirtualFileSystem).GetProperty("Instance",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic); var inst=prop?.GetValue(null) as VirtualFileSystem; if(inst==null) throw new Exception($"VFS null: {ex.Message}"); vfs=inst; LogAppend("VFS reutilizado"); }
            vfs.ReloadKeySet(); LogAppend("KeySet Reload OK");
            LogAppend("Criando VulkanRenderer..."); gpu=VulkanRenderer.Create("Ryubing",(inst,vk)=>{ unsafe{ var ci=new AndroidSurfaceCreateInfoKHR{ SType=StructureType.AndroidSurfaceCreateInfoKhr, Window=(nint*)nativeWindow }; var fp=vk.GetInstanceProcAddr(inst,"vkCreateAndroidSurfaceKHR"); var func=Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(fp); SurfaceKHR surf; var res=func(inst,&ci,null,&surf); if(res!=Silk.NET.Vulkan.Result.Success) throw new Exception($"vkCreateSurface falhou: {res}"); return surf; } },()=>new[]{"VK_KHR_surface","VK_KHR_android_surface"});
            LogAppend("Vulkan OK");
            var audio = new DummyHardwareDeviceDriver();
            LogAppend("Antes de BuildHleConfiguration..."); var hleConf = BuildHleConfigurationFIX(vfs, gpu, audio); LogAppend("HLE Config OK");
            LogAppend("=== ANTES DO SWITCH ==="); try{ LogAppend("Chamando new Switch..."); device = new Switch(hleConf); LogAppend("=== SWITCH CRIADO ==="); }catch(Exception ex){ LogError($"EXCECAO NO SWITCH:\n{ex}"); return; }
            LogAppend("=== ANTES DO LOAD NSP ==="); try{ bool loaded = device.LoadNsp(romPath); LogAppend($"LoadNsp retornou: {loaded}"); if (!loaded) { LogError("LoadNsp retornou FALSE"); return; } LogAppend("=== NSP CARREGADO ==="); }catch(Exception ex){ LogError($"EXCECAO NO LOADNSP:\n{ex}"); return; }
            RunOnUiThread(()=>{ if(logView!=null) logView.Visibility=ViewStates.Gone; });
            var sw=System.Diagnostics.Stopwatch.StartNew(); int frames=0;
            while(running){ if(!device.ProcessFrame()) break; device.PresentFrame(()=>{}); Thread.Yield(); frames++; if(sw.ElapsedMilliseconds>=1000){ int f=frames; frames=0; sw.Restart(); RunOnUiThread(()=>{ if(fpsView!=null) fpsView.Text=$"FPS: {f}"; }); } }
        }catch(Exception ex){ LogError($"ERRO GERAL:\n{ex.Message}\n{ex}"); }finally{ try{ device?.Dispose(); }catch{} try{ gpu?.Dispose(); }catch{} }
    }
    void CopyKeys(string baseDir,string systemDir){ string extKeys="/storage/emulated/0/Download/Ryubing/keys"; string legacyKeys="/storage/emulated/0/Download/DragoNX/keys"; string keysDir=Path.Combine(baseDir,"keys"); Directory.CreateDirectory(keysDir); foreach(var name in new[]{"prod.keys","title.keys"}){ string src=Path.Combine(extKeys,name); if(!File.Exists(src)) src=Path.Combine(legacyKeys,name); if(!File.Exists(src)) continue; try{ File.Copy(src,Path.Combine(systemDir,name),true); File.Copy(src,Path.Combine(keysDir,name),true); LogAppend($"{name} OK"); }catch(Exception ex){ LogAppend($"{name} erro: {ex.Message}"); } } }
    void CopyFirmware(string baseDir){ try{ string fwSrc="/storage/emulated/0/Download/Ryubing/firmware"; string fwDst=Path.Combine(baseDir,"bis","system","Contents","registered"); Directory.CreateDirectory(fwDst); if(!Directory.Exists(fwSrc)) return; foreach(var nca in Directory.GetFiles(fwSrc,"*.nca")){ string dst=Path.Combine(fwDst,Path.GetFileName(nca)); if(!File.Exists(dst) || new FileInfo(dst).Length!=new FileInfo(nca).Length) File.Copy(nca,dst,true); } LogAppend("Firmware OK"); }catch(Exception ex){ LogAppend($"Firmware erro: {ex.Message}"); } }
    void InitAppData(string baseDir){ try{ try{ Directory.Delete(Path.Combine(baseDir,"bis","user","save"),true);}catch{} try{ Directory.Delete(Path.Combine(baseDir,"bis","user","saveMeta"),true);}catch{} try{ Directory.Delete(Path.Combine(baseDir,"bis","system","save"),true);}catch{} Directory.CreateDirectory(Path.Combine(baseDir,"bis","user","save")); Directory.CreateDirectory(Path.Combine(baseDir,"bis","user","saveMeta")); Directory.CreateDirectory(Path.Combine(baseDir,"system","save")); Directory.CreateDirectory(Path.Combine(baseDir,"bis","system","save")); Directory.CreateDirectory(Path.Combine(baseDir,"bis","system","saveMeta")); }catch{} }
    unsafe delegate Silk.NET.Vulkan.Result CreateAndroidSurfaceDelegate(Instance i,AndroidSurfaceCreateInfoKHR* p,AllocationCallbacks* a,SurfaceKHR* s);

    HleConfiguration BuildHleConfigurationFIX(VirtualFileSystem vfs, VulkanRenderer gpu, DummyHardwareDeviceDriver audio)
    {
        LogAppend("=== BUILD HLE CONFIG ===");
        var cmType = typeof(ContentManager); object? contentManager = null;
        foreach (var c in cmType.GetConstructors(CtorFlags).OrderByDescending(x => x.GetParameters().Length))
        {
            try{ var pars = c.GetParameters(); var args = new object?[pars.Length]; for (int i = 0; i < pars.Length; i++){ var pt = pars[i].ParameterType; if (pt == typeof(VirtualFileSystem)) args[i] = vfs; else if (pt == typeof(string)) args[i] = _lastBaseDir; else if (pt.IsValueType) args[i] = Activator.CreateInstance(pt); else args[i] = null; } contentManager = c.Invoke(args); LogAppend($"ContentManager OK - ctor {pars.Length}"); break; }
            catch (Exception ex){ LogAppend($"ContentManager falhou: {ex.InnerException?.GetType().Name?? ex.GetType().Name}: {ex.InnerException?.Message?? ex.Message}"); }
        }
        if (contentManager == null) throw new Exception("Não foi possível criar ContentManager.");
        var ucpType = typeof(UserChannelPersistence); object? userChannel = null;
        foreach (var c in ucpType.GetConstructors(CtorFlags).OrderBy(x => x.GetParameters().Length))
        {
            try{ var pars = c.GetParameters(); var args = new object?[pars.Length]; for (int i = 0; i < pars.Length; i++){ var pt = pars[i].ParameterType; if (pt == typeof(bool)) args[i] = true; else if (pt.IsValueType) args[i] = Activator.CreateInstance(pt); else args[i] = null; } userChannel = c.Invoke(args); LogAppend($"UserChannelPersistence OK - ctor {pars.Length}"); break; }
            catch (Exception ex){ LogAppend($"UserChannelPersistence falhou: {ex.InnerException?.GetType().Name?? ex.GetType().Name}: {ex.InnerException?.Message?? ex.Message}"); }
        }
        if (userChannel == null){ try{ userChannel = Activator.CreateInstance(ucpType, true); LogAppend("UserChannelPersistence criado via Activator"); }catch(Exception ex){ throw new Exception($"Não foi possível criar UserChannelPersistence: {ex.InnerException?.Message?? ex.Message}"); } }
        var lhmType = typeof(LibHacHorizonManager); object? libHac = null;
        foreach (var c in lhmType.GetConstructors(CtorFlags).OrderByDescending(x => x.GetParameters().Length))
        {
            try{ var pars = c.GetParameters(); var args = new object?[pars.Length]; for (int i = 0; i < pars.Length; i++){ var pt = pars[i].ParameterType; if (pt == typeof(VirtualFileSystem)) args[i] = vfs; else if (pt == typeof(string)) args[i] = _lastBaseDir; else if (pt.IsValueType) args[i] = Activator.CreateInstance(pt); else args[i] = null; } libHac = c.Invoke(args); LogAppend($"LibHacHorizonManager OK - ctor {pars.Length}"); break; }
            catch (Exception ex){ LogAppend($"LibHacHorizonManager falhou: {ex.InnerException?.GetType().Name?? ex.GetType().Name}: {ex.InnerException?.Message?? ex.Message}"); }
        }
        if (libHac == null) throw new Exception("Não foi possível criar LibHacHorizonManager.");
        var amType = typeof(AccountManager); object? accountManager = null; object? horizonClient = null;
        try{ horizonClient = libHac.GetType().GetProperty("RyujinxClient",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(libHac); LogAppend(horizonClient!= null? "RyujinxClient encontrado" : "RyujinxClient NÃO encontrado"); }catch(Exception ex){ LogAppend($"Erro obtendo RyujinxClient: {ex.Message}"); }
        foreach (var c in amType.GetConstructors(CtorFlags).OrderByDescending(x => x.GetParameters().Length))
        {
            try{ var pars = c.GetParameters(); var args = new object?[pars.Length]; for (int i = 0; i < pars.Length; i++){ var pt = pars[i].ParameterType; if (pt == typeof(VirtualFileSystem)) args[i] = vfs; else if (pt == typeof(string)) args[i] = _lastBaseDir; else if (pt.Name.Contains("HorizonClient")) args[i] = horizonClient; else if (pt.IsValueType) args[i] = Activator.CreateInstance(pt); else args[i] = null; } accountManager = c.Invoke(args); LogAppend($"AccountManager OK - ctor {pars.Length}"); break; }
            catch (Exception ex){ LogAppend($"AccountManager falhou: {ex.InnerException?.GetType().Name?? ex.GetType().Name}: {ex.InnerException?.Message?? ex.Message}"); }
        }
        if (accountManager == null) throw new Exception("Não foi possível criar AccountManager.");
        var hleType = typeof(HleConfiguration);
        var hleCtor = hleType.GetConstructors(CtorFlags).FirstOrDefault(c =>{ var p = c.GetParameters(); return p.Length >= 20 && p.Any(x => x.ParameterType.Name.Contains("Memory")) && p.Any(x => x.ParameterType.Name.Contains("Language")); });
        if (hleCtor == null) hleCtor = hleType.GetConstructors(CtorFlags).OrderByDescending(c=>c.GetParameters().Length).First();
        var parameters = hleCtor.GetParameters(); LogAppend($"HleConfiguration ctor encontrado: {parameters.Length} parâmetros");
        var ctorArgs = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++){
            var p = parameters[i]; var pt = p.ParameterType; object? value = null;
            if (pt.Name.Contains("MemoryConfiguration")){ var names = Enum.GetNames(pt); string? sel = names.FirstOrDefault(x=>x.Contains("4GiB"))?? names.FirstOrDefault(); value = sel!=null?Enum.Parse(pt,sel):Activator.CreateInstance(pt); }
            else if (pt == typeof(SystemLanguage) || pt.Name.Contains("SystemLanguage")){ var names = Enum.GetNames(pt); string? sel = names.FirstOrDefault(x=>x.Contains("AmericanEnglish"))?? names.FirstOrDefault(x=>x.Contains("English"))?? names.FirstOrDefault(); value = sel!=null?Enum.Parse(pt,sel):Activator.CreateInstance(pt); }
            else if (pt.Name.Contains("RegionCode")){ var names = Enum.GetNames(pt); string? sel = names.FirstOrDefault(x=>x.Equals("USA",StringComparison.OrdinalIgnoreCase))?? names.FirstOrDefault(); value = sel!=null?Enum.Parse(pt,sel):Activator.CreateInstance(pt); }
            else if (pt.Name.Contains("VSyncMode")){ var names = Enum.GetNames(pt); string? sel = names.FirstOrDefault(x=>x.Equals("Switch",StringComparison.OrdinalIgnoreCase))?? names.FirstOrDefault(); value = sel!=null?Enum.Parse(pt,sel):Activator.CreateInstance(pt); }
            else if (pt.Name.Contains("AspectRatio")){ var names = Enum.GetNames(pt); string? sel = names.FirstOrDefault(x=>x.Equals("Fixed16x9",StringComparison.OrdinalIgnoreCase))?? names.FirstOrDefault(); value = sel!=null?Enum.Parse(pt,sel):Activator.CreateInstance(pt); }
            else if (pt.Name.Contains("MultiplayerMode")){ var names = Enum.GetNames(pt); string? sel = names.FirstOrDefault(x=>x.Equals("Disabled",StringComparison.OrdinalIgnoreCase))?? names.FirstOrDefault(); value = sel!=null?Enum.Parse(pt,sel):Activator.CreateInstance(pt); }
            else if (pt == typeof(string)){ value = p.Name.Contains("TimeZone",StringComparison.OrdinalIgnoreCase)? "UTC" : ""; }
            else if (pt == typeof(bool)){ value = p.Name.Contains("EnablePtc",StringComparison.OrdinalIgnoreCase) || p.Name.Contains("EnableDockedMode",StringComparison.OrdinalIgnoreCase)? true : false; }
            else if (pt == typeof(ushort)) value = (ushort)0; else if (pt == typeof(int)) value = 0; else if (pt == typeof(long)) value = 1L; else if (pt == typeof(float)) value = 1.0f;
            else if (pt.IsArray){ var et = pt.GetElementType(); value = Array.CreateInstance(et!,0); }
            else if (pt.IsEnum){ var names = Enum.GetNames(pt); string? sel = names.FirstOrDefault(x=>x.Equals("None",StringComparison.OrdinalIgnoreCase))?? names.FirstOrDefault(); value = sel!=null?Enum.Parse(pt,sel):Activator.CreateInstance(pt); }
            else if (pt.IsValueType) value = Activator.CreateInstance(pt);
            ctorArgs[i] = value; LogAppend($"HLE[{i}] {p.Name}: {pt.Name} = {(value==null?"null":value)}");
        }
        HleConfiguration hleConfig; try{ hleConfig = (HleConfiguration)hleCtor.Invoke(ctorArgs); LogAppend("HleConfiguration criado com sucesso"); }catch(Exception ex){ throw new Exception("Falha no construtor de HleConfiguration: "+(ex.InnerException?.Message??ex.Message),ex); }
        var configureMethod = hleType.GetMethod("Configure",BindingFlags.Public|BindingFlags.Instance); if (configureMethod==null) throw new Exception("HleConfiguration.Configure não encontrado.");
        var confParams = configureMethod.GetParameters(); LogAppend($"Configure encontrado: {confParams.Length} parâmetros");
        var confArgs = new object?[confParams.Length];
        for (int i = 0; i < confParams.Length; i++){
            var pt = confParams[i].ParameterType;
            if (pt == typeof(VirtualFileSystem)) confArgs[i]=vfs;
            else if (pt == typeof(LibHacHorizonManager)) confArgs[i]=libHac;
            else if (pt == typeof(ContentManager)) confArgs[i]=contentManager;
            else if (pt == typeof(AccountManager)) confArgs[i]=accountManager;
            else if (pt == typeof(UserChannelPersistence)) confArgs[i]=userChannel;
            else if (pt == typeof(IRenderer)) confArgs[i]=gpu;
            else if (pt.Name.Contains("HardwareDeviceDriver") || pt == typeof(IHardwareDeviceDriver)) confArgs[i]=audio;
            else if (pt == typeof(IHostUIHandler) || pt.Name.Contains("HostUI")) confArgs[i]=null;
            else throw new Exception($"Parâmetro desconhecido em Configure: {pt.FullName}");
            LogAppend($"Configure[{i}] {confParams[i].Name}: {pt.Name}");
        }
        try{ var result = configureMethod.Invoke(hleConfig,confArgs) as HleConfiguration; if(result==null) throw new Exception("Configure retornou null."); LogAppend("=== HLE CONFIG FINALIZADA ==="); return result; }
        catch(Exception ex){ throw new Exception("Falha em HleConfiguration.Configure: "+(ex.InnerException?.Message??ex.Message),ex); }
    }
    protected override void OnDestroy(){ running=false; try{ emuThread?.Join(2000); }catch{} base.OnDestroy(); }
}
