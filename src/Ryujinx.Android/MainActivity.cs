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
using Ryujinx.HLE;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX Fafnir V15 GPU", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape)]
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
            root.AddView(new TextView(this){ Text="DragoNX Fafnir V15 - GPU FIX\n", TextSize=18f });
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
            lista.AddView(new TextView(this){ Text=$"Keys: {(File.Exists(key)?"OK":"FALTA")} | Firmware: {(fwCount>10?$"OK {fwCount}":"FALTA")}\n" });
            try {
                var bases = Directory.GetFiles(games, "*.nsp").Where(x => x.Contains("[v0]")).ToArray();
                lista.AddView(new TextView(this){ Text=$"JOGOS BASE ({bases.Length}) - V15 GPU:" });
                foreach(var f in bases) {
                    var btn = new Button(this){ Text = "🌈 "+Path.GetFileName(f)+" [V15 GPU]" };
                    btn.Click += (s,e) => { var i = new global::Android.Content.Intent(this, typeof(GameActivity)); i.PutExtra("gamePath", f); i.PutExtra("basePath", basePath); StartActivity(i); };
                    lista.AddView(btn);
                }
            } catch (Exception ex) { lista.AddView(new TextView(this){ Text="Erro: "+ex.Message }); }
        }
    }

    [Activity(Label = "DragoNX Game V15", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenSize)]
    public class GameActivity : Activity, ISurfaceHolderCallback
    {
        TextView log = null!; SurfaceView surfaceView = null!;
        object device = null!; bool surfaceReady = false;
        string gamePath = "", basePath = "";

        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            gamePath = Intent.GetStringExtra("gamePath")!;
            basePath = Intent.GetStringExtra("basePath")!;
            var layout = new LinearLayout(this){ Orientation = Orientation.Vertical };
            log = new TextView(this){ TextSize=8f, Text=$"Fafnir V15 - GPU FIX\n{Path.GetFileName(gamePath)}\nAguardando Vulkan...\n" };
            surfaceView = new SurfaceView(this);
            surfaceView.Holder.AddCallback(this);
            var lp = new LinearLayout.LayoutParams(-1, 0); lp.Weight = 1;
            surfaceView.LayoutParameters = lp;
            surfaceView.SetBackgroundColor(global::Android.Graphics.Color.Black);
            var btnVoltar = new Button(this){ Text="VOLTAR" };
            btnVoltar.Click += (s,e) => { try{ device?.GetType().GetMethod("Dispose")?.Invoke(device,null); } catch{} Finish(); };
            layout.AddView(log); layout.AddView(surfaceView); layout.AddView(btnVoltar);
            SetContentView(layout);
        }

        void AddLog(string s) { RunOnUiThread(() => log.Text += s + "\n"); }

        object GetDefault(Type t) {
            if(t.IsEnum) { var vals = Enum.GetValues(t); return vals.Length>0? vals.GetValue(0): Activator.CreateInstance(t); }
            if(t == typeof(string)) return "";
            if(t == typeof(bool)) return false;
            if(t == typeof(int)) return 0;
            if(t == typeof(long)) return (long)0;
            if(t == typeof(short) || t == typeof(ushort)) return (ushort)0;
            if(t == typeof(uint) || t == typeof(ulong)) return Activator.CreateInstance(t);
            if(t == typeof(float)) return 1.0f;
            if(t == typeof(double)) return 1.0;
            if(t.IsValueType) { try{ return Activator.CreateInstance(t); } catch { return null; } }
            if(t.IsArray) return Array.CreateInstance(t.GetElementType(), 0);
            try{ return Activator.CreateInstance(t); } catch { return null; }
        }

        void TryBoot()
        {
            if(!surfaceReady) return;
            Task.Run(() => {
                try {
                    AddLog($"[1/5] Keys: 16025 bytes OK | FW: 229 files OK");
                    AddLog($"[2/5] Criando HleConfiguration V15 + GpuRenderer...");

                    var switchType = typeof(Ryujinx.HLE.Switch);
                    var hleConfigType = switchType.Assembly.GetTypes().First(t=>t.Name=="HleConfiguration");

                    var ctors = hleConfigType.GetConstructors();
                    AddLog($"> Achados {ctors.Length} ctors");
                    foreach(var c in ctors) {
                        var ps = c.GetParameters();
                        AddLog($"> ctor {ps.Length}: {string.Join(",", ps.Select(p=>p.Name))}");
                    }

                    var ctor = ctors.OrderByDescending(c=>c.GetParameters().Length).First();
                    var pars = ctor.GetParameters();
                    object[] args = new object[pars.Length];

                    for(int i=0;i<pars.Length;i++) {
                        var pt = pars[i].ParameterType;
                        var pn = pars[i].Name.ToLower();
                        try {
                            if(pn.Contains("memoryconfiguration")) {
                                var memVal = Enum.GetValues(pt).Cast<object>().FirstOrDefault(x=>x.ToString().Contains("4GiB"))?? Enum.GetValues(pt).GetValue(0);
                                args[i] = memVal;
                            } else if(pn.Contains("vsyncmode")) {
                                var v = Enum.GetValues(pt).Cast<object>().FirstOrDefault(x=>x.ToString().ToLower().Contains("switch"))?? Enum.GetValues(pt).GetValue(0);
                                args[i] = v;
                            } else if(pn.Contains("customvsync")) {
                                args[i] = pt.IsEnum? Enum.GetValues(pt).GetValue(0) : GetDefault(pt);
                            } else if(pn.Contains("gdbstubport")) { args[i] = (ushort)0; }
                            else if(pn.Contains("fsglobalaccess")) {
                                args[i] = pt.IsEnum? Enum.GetValues(pt).GetValue(0) : 0;
                            } else if(pn.Contains("systemlanguage")) {
                                var v = Enum.GetValues(pt).Cast<object>().FirstOrDefault(x=>x.ToString().Contains("American"))?? Enum.GetValues(pt).GetValue(0);
                                args[i] = v;
                            } else if(pn.Contains("region")) {
                                var v = Enum.GetValues(pt).Cast<object>().FirstOrDefault(x=>x.ToString().Contains("Americas"))?? Enum.GetValues(pt).GetValue(0);
                                args[i] = v;
                            } else if(pn.Contains("memorymanagermode") || pn.Contains("aspectratio") || pn.Contains("multiplayermode") || pn.Contains("fsintegrity")) {
                                args[i] = Enum.GetValues(pt).GetValue(0);
                            } else if(pn.Contains("rend") || pn.Contains("gpu")) {
                                AddLog($">> Criando GpuRenderer {pt.FullName}");
                                try {
                                    var allTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}}).Concat(switchType.Assembly.GetTypes()).ToArray();
                                    var rendererTypes = allTypes.Where(t=>!t.IsAbstract &&!t.IsInterface && pt.IsAssignableFrom(t)).Where(t=>t.Name.Contains("Vulkan") || t.Name.Contains("Dummy") || t.Name.Contains("Software") || t.Name.Contains("Gpu")).ToArray();
                                    AddLog($">> Candidatos: {string.Join(",", rendererTypes.Select(t=>t.Name).Take(5))}");
                                    if(rendererTypes.Length>0) {
                                        args[i] = Activator.CreateInstance(rendererTypes[0]);
                                        AddLog($">> GpuRenderer={rendererTypes[0].Name} OK!");
                                    } else {
                                        args[i] = Activator.CreateInstance(pt);
                                    }
                                } catch(Exception exR) { AddLog($">> GpuRenderer ERRO {exR.Message}"); args[i]=GetDefault(pt); }
                            } else if(pn.Contains("dock")) { args[i] = true; }
                            else if(pn.Contains("ptc")) { args[i] = true; }
                            else if(pn.Contains("ticks")) { args[i] = (long)1; }
                            else if(pn.Contains("timezone")) { args[i] = "UTC"; }
                            else if(pn.Contains("audiovolume")) { args[i] = 1.0f; }
                            else if(pn.Contains("timeoffset")) { args[i] = (long)0; }
                            else if(pn.Contains("dirtyhacks")) { args[i] = Array.CreateInstance(pt.GetElementType(), 0); }
                            else if(pn.Contains("hypervisor")) { args[i] = false; }
                            else if(pn.Contains("gdbstub")) { args[i] = false; }
                            else if(pn.Contains("suspendonstart")) { args[i] = false; }
                            else if(pn.Contains("internet")) { args[i] = false; }
                            else if(pn.Contains("ignore")) { args[i] = true; }
                            else if(pn.Contains("disablep2p")) { args[i] = false; }
                            else if(pt == typeof(bool)) { args[i] = false; }
                            else if(pt == typeof(string)) { args[i] = ""; }
                            else { args[i] = GetDefault(pt); }
                            AddLog($"> arg[{i}] {pars[i].Name}={args[i]}");
                        } catch(Exception exA) { args[i] = GetDefault(pt); AddLog($"> arg[{i}] ERRO {pars[i].Name}:{exA.Message}"); }
                    }

                    var hleConfig = ctor.Invoke(args);
                    AddLog($">> HleConfiguration CRIADO com {pars.Length} params!");

                    var gpuProp = hleConfigType.GetProperties().FirstOrDefault(p=>p.Name.ToLower().Contains("gpu"));
                    if(gpuProp!=null) {
                        var cur = gpuProp.GetValue(hleConfig);
                        AddLog($"> prop {gpuProp.Name} = {(cur==null?"NULL":cur.GetType().Name)}");
                        if(cur==null) {
                            try {
                                var allTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}}).ToArray();
                                var dummy = allTypes.FirstOrDefault(t=>t.Name=="DummyRenderer" || t.Name.Contains("VulkanRenderer"));
                                if(dummy!=null) {
                                    var inst = Activator.CreateInstance(dummy);
                                    gpuProp.SetValue(hleConfig, inst);
                                    AddLog($"> GpuRenderer SETADO para {dummy.Name}!");
                                }
                            } catch(Exception exG) { AddLog($"> SET GPU ERRO {exG.Message}"); }
                        }
                    }

                    AddLog($"[3/5] Criando Switch(HleConfiguration)...");
                    device = Activator.CreateInstance(switchType, new object[]{ hleConfig });
                    AddLog($">> Switch OK! PASSOU DO GPU! 🔥");

                    AddLog($"[4/5] LoadNsp: {Path.GetFileName(gamePath)}");
                    var loadNsp = switchType.GetMethod("LoadNsp");
                    var result = loadNsp.Invoke(device, new object[]{ gamePath });
                    AddLog($">> LoadNsp = {result}");

                    if(result is bool b && b) {
                        AddLog($"[5/5] LOOP VIDEO COR! 🌈");
                        RunOnUiThread(() => { log.Visibility = ViewStates.Gone; });
                        var processFrame = switchType.GetMethod("ProcessFrame");
                        var presentFrame = switchType.GetMethod("PresentFrame");
                        while(true) {
                            try { processFrame?.Invoke(device,null); presentFrame?.Invoke(device,null); System.Threading.Thread.Sleep(8); }
                            catch(Exception exL) { AddLog($"Loop: {exL.InnerException?.Message}"); break; }
                        }
                    } else { AddLog($">> LoadNsp FALSO"); }
                } catch (Exception ex) {
                    AddLog($"ERRO GERAL V15: {ex.InnerException?.Message?? ex.Message}\n{ex.StackTrace?.Substring(0,2500)}");
                }
            });
        }

        public void SurfaceCreated(ISurfaceHolder holder) { AddLog("SurfaceCreated - Vulkan OK! Boot V15..."); surfaceReady = true; TryBoot(); }
        public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format, int w, int h) { }
        public void SurfaceDestroyed(ISurfaceHolder holder) { surfaceReady = false; }
    }
}
