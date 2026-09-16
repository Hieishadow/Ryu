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
    [Activity(Label = "DragoNX Fafnir V17.0 GAL-FIX", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape)]
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
            root.AddView(new TextView(this){ Text="DragoNX Fafnir V17.0 GAL-FIX\n", TextSize=18f });
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
                lista.AddView(new TextView(this){ Text="JOGOS BASE ("+bases.Length+") - V17.0:" });
                foreach(var f in bases) {
                    var btn = new Button(this){ Text = " "+Path.GetFileName(f)+" [V17.0]" };
                    btn.Click += (s,e) => { var i = new global::Android.Content.Intent(this, typeof(GameActivity)); i.PutExtra("gamePath", f); i.PutExtra("basePath", basePath); StartActivity(i); };
                    lista.AddView(btn);
                }
            } catch (Exception ex) { lista.AddView(new TextView(this){ Text="Erro: "+ex.Message }); }
        }
    }

    // FAKE GAL - FORA da GameActivity pra compilar
    public class FakeWindow : Ryujinx.Graphics.GAL.IWindow
    {
        public bool ScreenCaptured { get; set; }
        public void Dispose() {}
        public void Present(Ryujinx.Graphics.GAL.ITexture texture, Ryujinx.Graphics.GAL.ImageCrop crop, Action presentCallback) { try{ presentCallback?.Invoke(); } catch{} }
        public void SetSize(Ryujinx.Common.Configuration.Hid.Size size) {}
        public void SetVisible(bool visible) {}
        public float GetDefaultScaleFactor() => 1.0f;
    }
    public class FakeGALRenderer : Ryujinx.Graphics.GAL.IRenderer
    {
        public Ryujinx.Graphics.GAL.IPipeline Pipeline => null;
        public Ryujinx.Graphics.GAL.IWindow Window { get; } = new FakeWindow();
        public void Dispose() {}
        public Ryujinx.Graphics.GAL.IBuffer CreateBuffer(int size) => null;
        public Ryujinx.Graphics.GAL.IProgram CreateProgram(Ryujinx.Graphics.GAL.Shader.ShaderSource[] shaders) => null;
        public Ryujinx.Graphics.GAL.ISampler CreateSampler(Ryujinx.Graphics.GAL.SamplerCreateInfo info) => null;
        public Ryujinx.Graphics.GAL.ITexture CreateTexture(Ryujinx.Graphics.GAL.TextureCreateInfo info) => null;
        public string GetGpuVendor() => "DragoNX";
        public string GetGpuRenderer() => "Fafnir";
        public string GetGpuVersion() => "1.0";
    }
    public class FakeAudioDriver : Ryujinx.Audio.Integration.IHardwareDeviceDriver
    {
        public Ryujinx.Audio.Integration.IHardwareDeviceSession OpenDeviceSession(Ryujinx.Audio.Renderer.Common.BehaviourContext ctx, int sessionId, int nodeId, Ryujinx.Audio.Renderer.Common.SampleFormat fmt, uint rate, uint count, float vol, bool rec, string name) => null;
        public Ryujinx.Audio.Integration.IHardwareDeviceSession OpenDeviceSession(Ryujinx.Audio.Renderer.Common.BehaviourContext ctx, Ryujinx.Audio.Renderer.Server.AudioDeviceSession session, string name) => null;
    }
    public class FakeUIHandler : Ryujinx.HLE.UI.IHostUIHandler
    {
        public bool DisplayMessageDialog(string title, string message) => true;
        public bool DisplayInputDialog(string title, string message, string defaultText, out string input) { input = ""; return true; }
    }

    [Activity(Label = "DragoNX Game V17.0", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenSize)]
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
            logFile = Path.Combine(basePath, "log_V17.txt");
            var layout = new LinearLayout(this){ Orientation = Orientation.Vertical };
            log = new TextView(this){ TextSize=7f, Text="Fafnir V17.0 GAL-FIX\n"+Path.GetFileName(gamePath)+"\n" };
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
            try{ File.WriteAllText(logFile, "START V17.0 "+DateTime.Now+"\nGame: "+gamePath+"\n"); } catch{}
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
                    AddLog("[1/4] Criando FakeGALRenderer...");
                    object fakeRenderer = new FakeGALRenderer();
                    AddLog("FakeGALRenderer OK: "+fakeRenderer.GetType().FullName);

                    AddLog("[2/4] Criando HleConfiguration 27 params...");
                    var hleConfigType = typeof(Ryujinx.HLE.Switch).Assembly.GetTypes().First(t=>t.Name=="HleConfiguration");
                    var hleCtor = hleConfigType.GetConstructors().OrderByDescending(c=>c.GetParameters().Length).First();
                    var pars = hleCtor.GetParameters();
                    AddLog($"Ctor com {pars.Length} params");
                    object[] args = new object[pars.Length];
                    for(int i=0;i<pars.Length;i++) {
                        var pt = pars[i].ParameterType;
                        var pn = pars[i].Name!=null? pars[i].Name.ToLower() : "";
                        try {
                            if(pn.Contains("memoryconfiguration")) { var all=Enum.GetValues(pt); object sel=all.GetValue(0); foreach(var v in all){ if(v.ToString().Contains("4GiB")){ sel=v; break; } } args[i]=sel; }
                            else if(pn.Contains("systemlanguage")) { var all=Enum.GetValues(pt); object sel=all.GetValue(0); foreach(var v in all){ if(v.ToString().Contains("American")){ sel=v; break; } } args[i]=sel; }
                            else if(pn.Contains("region")) { var all=Enum.GetValues(pt); object sel=all.GetValue(0); foreach(var v in all){ if(v.ToString().Contains("Americas")){ sel=v; break; } } args[i]=sel; }
                            else if(pn.Contains("vsyncmode")) { var all=Enum.GetValues(pt); object sel=all.GetValue(0); foreach(var v in all){ if(v.ToString().ToLower().Contains("switch")){ sel=v; break; } } args[i]=sel; }
                            else if(pt.IsEnum) args[i]=Enum.GetValues(pt).GetValue(0);
                            else if(pt == typeof(bool)) args[i]= pn.Contains("dock")? true : false;
                            else if(pt == typeof(string)) args[i]= pn.Contains("timezone")? "UTC" : "";
                            else if(pt == typeof(float)) args[i]=1.0f;
                            else if(pt == typeof(long) || pt==typeof(Int64)) args[i]= pn.Contains("ticks")? (long)1 : (long)0;
                            else if(pt.IsArray) args[i]=Array.CreateInstance(pt.GetElementType(), 0);
                            else args[i]=GetDefault(pt);
                        } catch { args[i]=GetDefault(pt); }
                    }
                    var hleConfig = hleCtor.Invoke(args);
                    AddLog("HleConfig base criado");

                    AddLog("[3/4] INJETANDO GpuRenderer, Audio, UI nas PROPS...");
                    var propGpu = hleConfigType.GetProperty("GpuRenderer");
                    var propAudio = hleConfigType.GetProperty("AudioDeviceDriver");
                    var propUI = hleConfigType.GetProperty("HostUIHandler");
                    var propVfs = hleConfigType.GetProperty("VirtualFileSystem");
                    if(propGpu!=null) { propGpu.SetValue(hleConfig, fakeRenderer); AddLog("GpuRenderer INJETADO! "+(propGpu.GetValue(hleConfig)!=null)); }
                    if(propAudio!=null) {
                        try { propAudio.SetValue(hleConfig, new FakeAudioDriver()); AddLog("Audio INJETADO!"); }
                        catch(Exception exA){ AddLog("Audio fail: "+exA.Message); }
                    }
                    if(propUI!=null) {
                        try { propUI.SetValue(hleConfig, new FakeUIHandler()); AddLog("HostUIHandler INJETADO!"); }
                        catch(Exception exU){ AddLog("UI fail: "+exU.Message); }
                    }
                    // VirtualFileSystem se tiver que criar
                    if(propVfs!=null && propVfs.GetValue(hleConfig)==null) {
                        try {
                            var vfsType = propVfs.PropertyType;
                            var vfs = Activator.CreateInstance(vfsType);
                            propVfs.SetValue(hleConfig, vfs);
                            AddLog("VirtualFileSystem criado!");
                        } catch(Exception exV){ AddLog("VFS fail: "+exV.Message); }
                    }

                    AddLog("[4/4] Criando Switch SEM NULL...");
                    var switchType = typeof(Ryujinx.HLE.Switch);
                    var deviceObj = Activator.CreateInstance(switchType, new object[]{ hleConfig });
                    device = deviceObj;
                    AddLog("Switch OK!!! GAL FIX FUNCIONOU!");

                    var loadNsp = switchType.GetMethod("LoadNsp");
                    if(loadNsp!=null) {
                        var result = loadNsp.Invoke(device, new object[]{ gamePath });
                        AddLog($"LoadNsp = {result}!!! ZELDA BOOTOU V17.0!!!");
                    }
                } catch(Exception exSw) {
                    AddLog("ERRO V17: "+(exSw.InnerException!=null? exSw.InnerException.ToString() : exSw.ToString()));
                }
            });
        }
        public void SurfaceCreated(ISurfaceHolder holder) { AddLog("SurfaceCreated V17.0"); surfaceReady = true; TryBoot(); }
        public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format, int w, int h) { }
        public void SurfaceDestroyed(ISurfaceHolder holder) { surfaceReady = false; }
    }
}
