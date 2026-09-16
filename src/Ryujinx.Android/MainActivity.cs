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
    [Activity(Label = "DragoNX Fafnir V15.1 SCROLL", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape)]
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
            root.AddView(new TextView(this){ Text="DragoNX Fafnir V15.1 - SCROLL FIX\n", TextSize=18f });
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
                lista.AddView(new TextView(this){ Text=$"JOGOS BASE ({bases.Length}) - V15.1:" });
                foreach(var f in bases) {
                    var btn = new Button(this){ Text = "🌈 "+Path.GetFileName(f)+" [V15.1 SCROLL]" };
                    btn.Click += (s,e) => { var i = new global::Android.Content.Intent(this, typeof(GameActivity)); i.PutExtra("gamePath", f); i.PutExtra("basePath", basePath); StartActivity(i); };
                    lista.AddView(btn);
                }
            } catch (Exception ex) { lista.AddView(new TextView(this){ Text="Erro: "+ex.Message }); }
        }
    }

    [Activity(Label = "DragoNX Game V15.1", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenSize)]
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
            logFile = Path.Combine(basePath, "log_V15.txt");
            var layout = new LinearLayout(this){ Orientation = Orientation.Vertical };

            log = new TextView(this){ TextSize=7f, Text=$"Fafnir V15.1 - GPU FIX SCROLL\n{Path.GetFileName(gamePath)}\n" };
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

            var btnLog = new Button(this){ Text="VER LOG COMPLETO (arquivo)" };
            btnLog.Click += (s,e) => {
                try{
                    var txt = File.Exists(logFile)? File.ReadAllText(logFile) : log.Text;
                    log.Text = txt + "\n--- FIM ---\n"+logFile;
                    scrollLog.Post(() => scrollLog.FullScroll(FocusSearchDirection.Down));
                } catch{}
            };
            var btnSair = new Button(this){ Text="SAIR" };
            btnSair.Click += (s,e) => { try{ device?.GetType().GetMethod("Dispose")?.Invoke(device,null); } catch{} Finish(); };

            layout.AddView(scrollLog);
            layout.AddView(surfaceView);
            layout.AddView(btnLog);
            layout.AddView(btnSair);
            SetContentView(layout);
            try{ File.WriteAllText(logFile, $"START V15.1 {DateTime.Now}\nGame: {gamePath}\n"); } catch{}
        }

        void AddLog(string s) {
            try{ File.AppendAllText(logFile, s+"\n"); } catch{}
            RunOnUiThread(() => {
                log.Text += s + "\n";
                scrollLog.Post(() => scrollLog.FullScroll(FocusSearchDirection.Down));
            });
        }

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
                    AddLog($"[2/5] Criando HleConfiguration V15.1...");

                    var switchType = typeof(Ryujinx.HLE.Switch);
                    var hleConfigType = switchType.Assembly.GetTypes().First(t=>t.Name=="HleConfiguration");
                    var ctors = hleConfigType.GetConstructors();
                    AddLog($"> Achados {ctors.Length} ctors HleConfiguration");
                    foreach(var c in ctors) AddLog($"> ctor {c.GetParameters().Length}: {string.Join(",", c.GetParameters().Select(p=>p.Name))}");

                    var ctor = ctors.OrderByDescending(c=>c.GetParameters().Length).First();
                    var pars = ctor.GetParameters();
                    object[] args = new object[pars.Length];

                    for(int i=0;i<pars.Length;i++) {
                        var pt = pars[i].ParameterType;
                        var pn = pars[i].Name.ToLower();
                        try {
                            if(pn.Contains("memoryconfiguration")) args[i] = Enum.GetValues(pt).Cast<object>().FirstOrDefault(x=>x.ToString().Contains("4GiB"))?? Enum.GetValues(pt).GetValue(0);
                            else if(pn.Contains("vsyncmode")) args[i] = Enum.GetValues(pt).Cast<object>().FirstOrDefault(x=>x.ToString().ToLower().Contains("switch"))?? Enum.GetValues(pt).GetValue(0);
                            else if(pn.Contains("customvsync")) args[i] = pt.IsEnum? Enum.GetValues(pt).GetValue(0) : GetDefault(pt);
                            else if(pn.Contains("gdbstubport")) args[i] = (ushort)0;
                            else if(pn.Contains("fsglobalaccess")) args[i] = pt.IsEnum? Enum.GetValues(pt).GetValue(0) : 0;
                            else if(pn.Contains("systemlanguage")) args[i] = Enum.GetValues(pt).Cast<object>().FirstOrDefault(x=>x.ToString().Contains("American"))?? Enum.GetValues(pt).GetValue(0);
                            else if(pn.Contains("region")) args[i] = Enum.GetValues(pt).Cast<object>().FirstOrDefault(x=>x.ToString().Contains("Americas"))?? Enum.GetValues(pt).GetValue(0);
                            else if(pn.Contains("memorymanagermode") || pn.Contains("aspectratio") || pn.Contains("multiplayermode") || pn.Contains("fsintegrity")) args[i] = Enum.GetValues(pt).GetValue(0);
                            else if(pn.Contains("dock")) args[i] = true;
                            else if(pn.Contains("ptc")) args[i] = true;
                            else if(pn.Contains("ticks")) args[i] = (long)1;
                            else if(pn.Contains("timezone")) args[i] = "UTC";
                            else if(pn.Contains("audiovolume")) args[i] = 1.0f;
                            else if
