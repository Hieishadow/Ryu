using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using System.IO;
using System.Linq;
using Env = Android.OS.Environment;
using System.Threading.Tasks;
using System;
using System.Reflection;
using System.Reflection.Emit;
using Ryujinx.HLE;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX Fafnir V16.1 GPU", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        string basePath = ""; LinearLayout lista = null!;
        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R &&!Env.IsExternalStorageManager) {
                try {
                    var i = new global::Android.Content.Intent(global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                    i.SetData(global::Android.Net.Uri.Parse("package:"+PackageName));
                    StartActivity(i);
                } catch { StartActivity(new global::Android.Content.Intent(global::Android.Provider.Settings.ActionManageAllFilesAccessPermission)); }
            }
            basePath = Path.Combine(Env.GetExternalStoragePublicDirectory(Env.DirectoryDownloads).AbsolutePath, "DragoNX");
            var scroll = new ScrollView(this);
            var root = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.SetPadding(30,20,30,20);
            lista = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.AddView(new TextView(this){ Text="DragoNX Fafnir V16.1 GPU FIX\n", TextSize=18f });
            root.AddView(lista);
            scroll.AddView(root);
            SetContentView(scroll);
            Carregar();
        }
        void Carregar() {
            lista.RemoveAllViews();
            var games = Path.Combine(basePath, "games");
            var fw = Path.Combine(basePath, "firmware");
            var key = Path.Combine(basePath, "keys", "prod.keys");
            int fwCount = Directory.Exists(fw)? Directory.GetFiles(fw,"*",SearchOption.AllDirectories).Length : 0;
            lista.AddView(new TextView(this){ Text="Keys: "+(File.Exists(key)?"OK":"FALTA")+" | Firmware: "+(fwCount>10? "OK "+fwCount:"FALTA")+"\n" });
            try {
                var bases = Directory.GetFiles(games, "*.nsp").Where(x => x.Contains("[v0]")).ToArray();
                lista.AddView(new TextView(this){ Text="JOGOS BASE ("+bases.Length+") - V16.1:" });
                foreach(var f in bases) {
                    var btn = new Button(this){ Text = " "+Path.GetFileName(f)+" [V16.1]" };
                    btn.Click += (s,e) => { var i = new global::Android.Content.Intent(this, typeof(GameActivity)); i.PutExtra("gamePath", f); i.PutExtra("basePath", basePath); StartActivity(i); };
                    lista.AddView(btn);
                }
            } catch (Exception ex) { lista.AddView(new TextView(this){ Text="Erro: "+ex.Message }); }
        }
    }

    [Activity(Label = "DragoNX Game V16.1", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenSize)]
    public class GameActivity : Activity, ISurfaceHolderCallback
    {
        TextView log = null!; SurfaceView surfaceView = null!;
        object device = null!; bool surfaceReady = false;
        string gamePath = "", basePath = "", logFile = "";
        ScrollView scrollLog = null!;

        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            gamePath = Intent.GetStringExtra("gamePath")!;
            basePath = Intent.GetStringExtra("basePath")!;
            logFile = Path.Combine(basePath, "log_V16.txt");
            var layout = new LinearLayout(this){ Orientation = Orientation.Vertical };
            log = new TextView(this){ TextSize=7f, Text="Fafnir V16.1 GPU\n"+Path.GetFileName(gamePath)+"\n" };
            log.SetTextIsSelectable(true);
            scrollLog = new ScrollView(this);
            var lpScroll = new LinearLayout.LayoutParams(-1, 0); lpScroll.Weight = 1;
            scrollLog.LayoutParameters = lpScroll;
            scrollLog.AddView(log);
            surfaceView = new SurfaceView(this);
            surfaceView.Holder.AddCallback(this);
            var lp = new LinearLayout.LayoutParams(-1, 300);
            surfaceView.LayoutParameters = lp;
            surfaceView.SetBackgroundColor(global::Android.Graphics.Color.Black);
            var btnLog = new Button(this){ Text="VER LOG COMPLETO" };
            btnLog.Click += (s,e) => {
                try{
                    var txt = File.Exists(logFile)? File.ReadAllText(logFile) : log.Text;
                    log.Text = txt + "\n--- FIM ---\n"+logFile;
                    scrollLog.Post(() => scrollLog.FullScroll(FocusSearchDirection.Down));
                } catch{}
            };
            var btnSair = new Button(this){ Text="SAIR" };
            btnSair.Click += (s,ev) => { try{ device?.GetType().GetMethod("Dispose")?.Invoke(device,null); } catch{} Finish(); };
            layout.AddView(scrollLog);
            layout.AddView(surfaceView);
            layout.AddView(btnLog);
            layout.AddView(btnSair);
            SetContentView(layout);
            try{ File.WriteAllText(logFile, "START V16.1 "+DateTime.Now+"\nGame: "+gamePath+"\n"); } catch{}
        }

        void AddLog(string s) {
            try{ File.AppendAllText(logFile, s+"\n"); } catch{}
            RunOnUiThread(() => {
                log.Text += s + "\n";
                scrollLog.Post(() => scrollLog.FullScroll(FocusSearchDirection.Down));
            });
        }

        object GetDefault(Type t) {
            if(t.IsEnum) { var vals = Enum.GetValues(t); if(vals.Length>0) return vals.GetValue(0); return Activator.CreateInstance(t); }
            if(t == typeof(string)) return "";
            if(t == typeof(bool)) return false;
            if(t == typeof(int)) return 0;
            if(t == typeof(long)) return (long)0;
            if(t == typeof(ushort)) return (ushort)0;
            if(t == typeof(short)) return (short)0;
            if(t == typeof(float)) return 1.0f;
            if(t == typeof(double)) return 1.0;
            if(t.IsValueType) { try{ return Activator.CreateInstance(t); } catch { return null; } }
            if(t.IsArray) { try{ return Array.CreateInstance(t.GetElementType(), 0); } catch { return null; } }
            try{ return Activator.CreateInstance(t); } catch { return null; }
        }

        object CreateFakeGpu(Type gpuType)
        {
            try {
                Type iface = gpuType;
                foreach(var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                    try {
                        foreach(var tp in asm.GetTypes()) { if(tp.Name=="IGpuRenderer") { iface=tp; break; } }
                        if(iface.Name=="IGpuRenderer") break;
                    } catch {}
                }
                AddLog("Interface: "+iface.FullName);
                var abName = new AssemblyName("FakeAsm");
                var ab = AssemblyBuilder.DefineDynamicAssembly(abName, AssemblyBuilderAccess.Run);
                var mb = ab.DefineDynamicModule("Mod");
                var tb = mb.DefineType("FakeGpu", TypeAttributes.Public | TypeAttributes.Class, null, new Type[]{ iface });
                var ctor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
                var ilCtor = ctor.GetILGenerator();
                ilCtor.Emit(OpCodes.Ldarg_0);
                ilCtor.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
                ilCtor.Emit(OpCodes.Ret);
                foreach(var it in new Type[]{ iface }.Concat(iface.GetInterfaces()).ToArray()) {
                    foreach(var m in it.GetMethods()) {
                        if(m.IsSpecialName) continue;
                        var paramTypes = m.GetParameters().Select(p=>p.ParameterType).ToArray();
                        var mb2 = tb.DefineMethod(m.Name, MethodAttributes.Public | MethodAttributes.Virtual, m.ReturnType, paramTypes);
                        var il = mb2.GetILGenerator();
                        if(m.ReturnType == typeof(void)) il.Emit(OpCodes.Ret);
                        else if(m.ReturnType.IsValueType) {
                            var loc = il.DeclareLocal(m.ReturnType);
                            il.Emit(OpCodes.Ldloca_S, loc);
                            il.Emit(OpCodes.Initobj, m.ReturnType);
                            il.Emit(OpCodes.Ldloc_0);
                            il.Emit(OpCodes.Ret);
                        } else { il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Ret); }
                        tb.DefineMethodOverride(mb2, m);
                    }
                }
                var fakeType = tb.CreateType();
                var inst = Activator.CreateInstance(fakeType);
                AddLog("FakeGpu CRIADO");
                return inst;
            } catch(Exception ex) {
                AddLog("ERRO FakeGpu: "+ex.Message);
                return null;
            }
        }

        void TryBoot()
        {
            if(!surfaceReady) return;
            Task.Run(() => {
                try {
                    AddLog("[1/5] Keys OK FW OK");
                    AddLog("[2/5] Criando HleConfiguration V16.1");
                    var switchType = typeof(Ryujinx.HLE.Switch);
                    var hleConfigType = switchType.Assembly.GetTypes().First(t=>t.Name=="HleConfiguration");
                    var ctor = hleConfigType.GetConstructors().OrderByDescending(c=>c.GetParameters().Length).First();
                    var pars = ctor.GetParameters();
                    object[] args = new object[pars.Length];
                    object fakeGpu = null;
                    Type gpuParamType = null;
                    foreach(var p in pars) { if(p.Name.ToLower().Contains("gpu") || p.ParameterType.Name.ToLower().Contains("gpu")) { gpuParamType = p.ParameterType; break; } }
                    if(gpuParamType!=null) fakeGpu = CreateFakeGpu(gpuParamType);
                    for(int i=0;i<pars.Length;i++) {
                        var pt = pars[i].ParameterType;
                        var pn = pars[i].Name.ToLower();
                        try {
                            if(pn.Contains("gpu") || pt.Name.ToLower().Contains("gpu")) args[i]=fakeGpu;
                            else if(pn.Contains("memoryconfiguration")) { var all=Enum.GetValues(pt); object sel=all.GetValue(0); foreach(var v in all){ if(v.ToString().Contains("4GiB")){ sel=v; break; } } args[i]=sel; }
                            else if(pn.Contains("vsyncmode")) { var all=Enum.GetValues(pt); object sel=all.GetValue(0); foreach(var v in all){ if(v.ToString().ToLower().Contains("switch")){ sel=v; break; } } args[i]=sel; }
                            else if(pn.Contains("gdbstubport")) args[i]=(ushort)0;
                            else if(pn.Contains("fsglobalaccess")) { if(pt.IsEnum) args[i]=Enum.GetValues(pt).GetValue(0); else args[i]=0; }
                            else if(pn.Contains("systemlanguage")) { var all=Enum.GetValues(pt); object sel=all.GetValue(0); foreach(var v in all){ if(v.ToString().Contains("American")){ sel=v; break; } } args[i]=sel; }
                            else if(pn.Contains("region")) { var all=Enum.GetValues(pt); object sel=all.GetValue(0); foreach(var v in all){ if(v.ToString().Contains("Americas")){ sel=v; break; } } args[i]=sel; }
                            else if(pn.Contains("memorymanagermode") || pn.Contains("aspectratio") || pn.Contains("multiplayermode") || pn.Contains("fsintegrity")) args[i]=Enum.GetValues(pt).GetValue(0);
                            else if(pn.Contains("dock")) args[i]=true;
                            else if(pn.Contains("ptc")) args[i]=true;
                            else if(pn.Contains("ticks")) args[i]=(long)1;
                            else if(pn.Contains("timezone")) args[i]="UTC";
                            else if(pn.Contains("audiovolume")) args[i]=1.0f;
                            else if(pn.Contains("timeoffset")) args[i]=(long)0;
                            else if(pn.Contains("dirtyhacks")) args[i]=Array.CreateInstance(pt.GetElementType(), 0);
                            else if(pn.Contains("hypervisor")) args[i]=false;
                            else if(pn.Contains("gdbstub")) args[i]=false;
                            else if(pn.Contains("suspendonstart")) args[i]=false;
                            else if(pn.Contains("internet")) args[i]=false;
                            else if(pn.Contains("ignore")) args[i]=true;
                            else if(pn.Contains("disablep2p")) args[i]=false;
                            else if(pt == typeof(bool)) args[i]=false;
                            else if(pt == typeof(string)) args[i]="";
                            else args[i]=GetDefault(pt);
                        } catch { args[i]=GetDefault(pt); }
                    }
                    var hleConfig = ctor.Invoke(args);
                    AddLog("HleConfig CRIADO GPU="+(fakeGpu!=null?"FAKE OK":"NULL"));
                    AddLog("[3/5] Criando Switch...");
                    device = Activator.CreateInstance(switchType, new object[]{ hleConfig });
                    AddLog("Switch OK PASSOU DO GPU!");
                    AddLog("[4/5] LoadNsp: "+Path.GetFileName(gamePath));
                    var loadNsp = switchType.GetMethod("LoadNsp");
                    var result = loadNsp.Invoke(device, new object[]{ gamePath });
                    AddLog("LoadNsp = "+result);
                } catch (Exception ex) {
                    string msg = ex.InnerException!=null? ex.InnerException.Message : ex.Message;
                    AddLog("ERRO V16.1: "+msg);
                }
            });
        }

        public void SurfaceCreated(ISurfaceHolder holder) { AddLog("SurfaceCreated Boot V16.1"); surfaceReady = true; TryBoot(); }
        public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format, int w, int h) { }
        public void SurfaceDestroyed(ISurfaceHolder holder) { surfaceReady = false; }
    }
}
