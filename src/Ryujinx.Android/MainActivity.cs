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

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX Fafnir V16.9 NO-GPU", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape)]
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
            root.AddView(new TextView(this){ Text="DragoNX Fafnir V16.9 NO-GPU\n", TextSize=18f });
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
                lista.AddView(new TextView(this){ Text="JOGOS BASE ("+bases.Length+") - V16.9:" });
                foreach(var f in bases) {
                    var btn = new Button(this){ Text = " "+Path.GetFileName(f)+" [V16.9]" };
                    btn.Click += (s,e) => { var i = new global::Android.Content.Intent(this, typeof(GameActivity)); i.PutExtra("gamePath", f); i.PutExtra("basePath", basePath); StartActivity(i); };
                    lista.AddView(btn);
                }
            } catch (Exception ex) { lista.AddView(new TextView(this){ Text="Erro: "+ex.Message }); }
        }
    }

    [Activity(Label = "DragoNX Game V16.9", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenSize)]
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
            log = new TextView(this){ TextSize=7f, Text="Fafnir V16.9 NO-GPU\n"+Path.GetFileName(gamePath)+"\n" };
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
            try{ File.WriteAllText(logFile, "START V16.9 "+DateTime.Now+"\nGame: "+gamePath+"\n"); } catch{}
        }

        void AddLog(string s) {
            try{ File.AppendAllText(logFile, s+"\n"); } catch{}
            RunOnUiThread(() => {
                log.Text += s + "\n";
                scrollLog.Post(() => scrollLog.FullScroll(FocusSearchDirection.Down));
            });
        }
        string Safe(string s, int max) { if(s==null) return ""; if(s.Length<=max) return s; return s.Substring(0,max); }
        object GetDefault(Type t) {
            if(t==null) return null;
            if(t.IsEnum) { var vals = Enum.GetValues(t); if(vals.Length>0) return vals.GetValue(0); try{ return Activator.CreateInstance(t); } catch { return 0; } }
            if(t == typeof(string)) return "";
            if(t == typeof(bool)) return false;
            if(t == typeof(int)) return 0;
            if(t == typeof(long)) return (long)0;
            if(t == typeof(ulong)) return (ulong)0;
            if(t == typeof(ushort)) return (ushort)0;
            if(t == typeof(short)) return (short)0;
            if(t == typeof(float)) return 1.0f;
            if(t == typeof(double)) return 1.0;
            if(t.IsValueType) { try{ return Activator.CreateInstance(t); } catch { return null; } }
            if(t.IsArray) { try{ return Array.CreateInstance(t.GetElementType(), 0); } catch { return null; } }
            try{ return Activator.CreateInstance(t); } catch { return null; }
        }

        void TryBoot()
        {
            if(!surfaceReady) return;
            Task.Run(() => {
                try {
                    AddLog("[1/5] Carregando TODAS as DLLs do APK...");
                    try {
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        foreach(var dll in Directory.GetFiles(baseDir, "*.dll")) {
                            try { Assembly.LoadFrom(dll); } catch{}
                        }
                        AddLog($"Assemblies: {AppDomain.CurrentDomain.GetAssemblies().Length}");
                        foreach(var a in AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name.Contains("Graphics") || a.GetName().Name.Contains("Ryujinx"))) {
                            AddLog($" - ASM: {a.GetName().Name}");
                        }
                    } catch(Exception ex){ AddLog("Erro load: "+ex.Message); }

                    AddLog("[2/5] Caçando interface grafica...");
                    foreach(var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                        try {
                            foreach(var t in asm.GetTypes()) {
                                if(t.Namespace!=null && t.Namespace.Contains("Graphics")) {
                                    if(t.IsInterface && (t.Name.Contains("Renderer") || t.Name.Contains("Gpu") || t.Name.Contains("Context"))) {
                                        AddLog($"CANDIDATO: {t.FullName} em {asm.GetName().Name}");
                                    }
                                }
                                if(t.Name=="GraphicsConfig" || t.Name=="GpuContext" || t.Name=="Window") {
                                    AddLog($"TIPO: {t.FullName} em {asm.GetName().Name}");
                                }
                            }
                        } catch{}
                    }

                    AddLog("[3/5] Investigando HleConfiguration FUNDO...");
                    var switchType = typeof(Ryujinx.HLE.Switch);
                    AddLog($"Switch em: {switchType.Assembly.GetName().Name}");
                    foreach(var ctor in switchType.GetConstructors()) {
                        var ps = ctor.GetParameters();
                        AddLog($"Switch ctor {ps.Length}: {string.Join(", ", ps.Select(p=> p.Name+":"+p.ParameterType.FullName))}");
                    }
                    var hleConfigType = switchType.Assembly.GetTypes().First(t=>t.Name=="HleConfiguration");
                    AddLog($"HleConfig em: {hleConfigType.Assembly.GetName().Name}");
                    foreach(var ctor in hleConfigType.GetConstructors()) {
                        var ps = ctor.GetParameters();
                        AddLog($"HleConfig ctor {ps.Length}: {string.Join(" | ", ps.Select(p=> p.Name+":"+p.ParameterType.Name))}");
                    }
                    var fields = hleConfigType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    AddLog($"Campos HleConfig ({fields.Length}):");
                    foreach(var f in fields) AddLog($" CAMPO {f.Name} : {f.FieldType.FullName}");

                    var props = hleConfigType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    AddLog($"Props HleConfig ({props.Length}):");
                    foreach(var p in props.Take(40)) AddLog($" PROP {p.Name} : {p.PropertyType.FullName} CanWrite={p.CanWrite}");

                    AddLog("[4/5] Criando HleConfiguration old-school...");
                    var hleCtor = hleConfigType.GetConstructors().OrderByDescending(c=>c.GetParameters().Length).First();
                    var pars = hleCtor.GetParameters();
                    object[] args = new object[pars.Length];
                    for(int i=0;i<pars.Length;i++) {
                        var pt = pars[i].ParameterType;
                        var pn = pars[i].Name!=null? pars[i].Name.ToLower() : "";
                        try {
                            if(pt.FullName!=null && pt.FullName.Contains("GraphicsConfig")) {
                                AddLog($"Criando GraphicsConfig para {pars[i].Name}");
                                var gcCtor = pt.GetConstructors().FirstOrDefault();
                                if(gcCtor!=null) {
                                    var gcPars = gcCtor.GetParameters();
                                    object[] gcArgs = new object[gcPars.Length];
                                    for(int j=0;j<gcPars.Length;j++) {
                                        gcArgs[j]=GetDefault(gcPars[j].ParameterType);
                                        if(gcPars[j].ParameterType == typeof(bool)) gcArgs[j]=false;
                                    }
                                    args[i]=gcCtor.Invoke(gcArgs);
                                    AddLog("GraphicsConfig criado!");
                                } else { args[i]=GetDefault(pt); }
                                continue;
                            }
                            if(pn.Contains("memoryconfiguration")) { var all=Enum.GetValues(pt); object sel=all.GetValue(0); foreach(var v in all){ if(v.ToString().Contains("4GiB")){ sel=v; break; } } args[i]=sel; }
                            else if(pn.Contains("vsyncmode")) { var all=Enum.GetValues(pt); object sel=all.GetValue(0); foreach(var v in all){ if(v.ToString().ToLower().Contains("switch")){ sel=v; break; } } args[i]=sel; }
                            else if(pn.Contains("gdbstubport")) args[i]=(ushort)0;
                            else if(pn.Contains("fsglobalaccess") || pn.Contains("user")) { if(pt.IsEnum) args[i]=Enum.GetValues(pt).GetValue(0); else args[i]=0; }
                            else if(pn.Contains("systemlanguage")) { var all=Enum.GetValues(pt); object sel=all.GetValue(0); foreach(var v in all){ if(v.ToString().Contains("American")){ sel=v; break; } } args[i]=sel; }
                            else if(pn.Contains("region")) { var all=Enum.GetValues(pt); object sel=all.GetValue(0); foreach(var v in all){ if(v.ToString().Contains("Americas")){ sel=v; break; } } args[i]=sel; }
                            else if(pt.IsEnum) args[i]=Enum.GetValues(pt).GetValue(0);
                            else if(pn.Contains("dock")) args[i]=true;
                            else if(pn.Contains("ticks")) args[i]=(long)1;
                            else if(pn.Contains("timezone")) args[i]="UTC";
                            else if(pn.Contains("audiovolume")) args[i]=1.0f;
                            else if(pn.Contains("timeoffset")) args[i]=(long)0;
                            else if(pn.Contains("dirtyhacks") || pt.IsArray) args[i]=Array.CreateInstance(pt.GetElementType(), 0);
                            else if(pt == typeof(bool)) args[i]=false;
                            else if(pt == typeof(string)) args[i]="";
                            else args[i]=GetDefault(pt);
                        } catch { args[i]=GetDefault(pt); }
                    }
                    var hleConfig = hleCtor.Invoke(args);
                    AddLog("HleConfig criado OLD!");

                    AddLog("[5/5] Criando Switch SEM GPU...");
                    var deviceObj = Activator.CreateInstance(switchType, new object[]{ hleConfig });
                    device = deviceObj;
                    AddLog("Switch OK!!! VERSAO ANTIGA SEM GPU!");

                    var loadNsp = switchType.GetMethod("LoadNsp");
                    if(loadNsp!=null) {
                        var result = loadNsp.Invoke(device, new object[]{ gamePath });
                        AddLog("LoadNsp = "+result+" BOOTOU ZELDA!!!");
                    } else {
                        AddLog("LoadNsp null, listando Loads:");
                        foreach(var m in switchType.GetMethods().Where(m=>m.Name.StartsWith("Load"))) {
                            AddLog($"Load: {m.Name} ({string.Join(",", m.GetParameters().Select(p=>p.ParameterType.Name))})");
                        }
                    }
                } catch(Exception exSw) {
                    AddLog("ERRO V16.9: "+(exSw.InnerException!=null? exSw.InnerException.Message : exSw.Message));
                    AddLog(Safe(exSw.InnerException!=null? exSw.InnerException.StackTrace : exSw.StackTrace, 2000));
                }
            });
        }
        public void SurfaceCreated(ISurfaceHolder holder) { AddLog("SurfaceCreated V16.9"); surfaceReady = true; TryBoot(); }
        public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format, int w, int h) { }
        public void SurfaceDestroyed(ISurfaceHolder holder) { surfaceReady = false; }
    }
}
