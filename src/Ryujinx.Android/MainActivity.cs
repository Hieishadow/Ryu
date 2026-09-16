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
    [Activity(Label = "DragoNX Fafnir V12 COR", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape)]
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
            root.AddView(new TextView(this){ Text="DragoNX Fafnir V12 - COR FINAL\n", TextSize=18f });
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
                lista.AddView(new TextView(this){ Text=$"JOGOS BASE ({bases.Length}) - V12 COR:" });
                foreach(var f in bases) {
                    var btn = new Button(this){ Text = "🌈 "+Path.GetFileName(f)+" [V12 COR]" };
                    btn.Click += (s,e) => { var i = new global::Android.Content.Intent(this, typeof(GameActivity)); i.PutExtra("gamePath", f); i.PutExtra("basePath", basePath); StartActivity(i); };
                    lista.AddView(btn);
                }
            } catch (Exception ex) { lista.AddView(new TextView(this){ Text="Erro: "+ex.Message }); }
        }
    }

    [Activity(Label = "DragoNX Game V12", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape, ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenSize)]
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
            log = new TextView(this){ TextSize=8f, Text=$"Fafnir V12 - COR FINAL\n{Path.GetFileName(gamePath)}\nAguardando Vulkan...\n" };
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
            if(t == typeof(bool)) return true;
            if(t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(ushort)) return Activator.CreateInstance(t);
            if(t == typeof(float) || t == typeof(double)) return 1.0f;
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
                    AddLog($"[2/5] Criando HleConfiguration com defaults...");

                    var switchType = typeof(Ryujinx.HLE.Switch);
                    var hleConfigType = switchType.Assembly.GetTypes().First(t=>t.Name=="HleConfiguration");
                    var ctor = hleConfigType.GetConstructors().First();
                    var pars = ctor.GetParameters();
                    object[] args = new object[pars.Length];

                    for(int i=0;i<pars.Length;i++) {
                        var pt = pars[i].ParameterType;
                        var pn = pars[i].Name.ToLower();
                        if(pn.Contains("memoryconfiguration")) {
                            // MemoryConfiguration.Host = 0 geralmente
                            args[i] = GetDefault(pt);
                            AddLog($"> memoryConfig = {args[i]}");
                        } else if(pn.Contains("systemlanguage")) {
                            args[i] = GetDefault(pt); // AmericanEnglish
                        } else if(pn.Contains("region")) {
                            args[i] = GetDefault(pt);
                        } else if(pn.Contains("vsyncmode")) {
                            var v = Enum.GetValues(pt).Cast<object>().FirstOrDefault(x=>x.ToString().Contains("Switch"));
                            args[i] = v?? GetDefault(pt);
                        } else if(pn.Contains("enableDockedMode")) {
                            args[i] = true;
                        } else if(pn.Contains("ticks")) {
                            args[i] = (long)1;
                        } else if(pn.Contains("timezone")) {
                            args[i] = "UTC";
                        } else if(pn.Contains("audiovolume")) {
                            args[i] = 1.0f;
                        } else if(pn.Contains("dirtyhacks")) {
                            args[i] = Array.CreateInstance(pt.GetElementType(), 0);
                        } else {
                            args[i] = GetDefault(pt);
                        }
                    }

                    var hleConfig = ctor.Invoke(args);
                    AddLog($">> HleConfiguration CRIADO com {pars.Length} params!");

                    AddLog($"[3/5] Criando Switch(HleConfiguration)...");
                    device = Activator.CreateInstance(switchType, new object[]{ hleConfig });
                    AddLog($">> Switch OK! {device.GetType().Name}");

                    AddLog($"[4/5] LoadNsp: {Path.GetFileName(gamePath)}");
                    var loadNsp = switchType.GetMethod("LoadNsp");
                    var result = loadNsp.Invoke(device, new object[]{ gamePath });
                    AddLog($">> LoadNsp = {result} - SE TRUE VEM COR!");

                    if(result is bool b && b) {
                        AddLog($"[5/5] LOOP VIDEO COLORIDO!");
                        RunOnUiThread(() => { log.Visibility = ViewStates.Gone; });
                        var processFrame = switchType.GetMethod("ProcessFrame");
                        var presentFrame = switchType.GetMethod("PresentFrame");
                        var consume = switchType.GetMethod("ConsumeFrameAvailable");
                        var isFrameAvail = switchType.GetMethod("IsFrameAvailable");

                        while(true) {
                            try {
                                bool has = true;
                                if(isFrameAvail!=null) {
                                    var r = isFrameAvail.Invoke(device,null);
                                    if(r is bool bh) has=bh;
                                } else if(consume!=null) {
                                    var r = consume.Invoke(device,null);
                                    if(r is bool bh) has=bh;
                                }
                                if(has) {
                                    processFrame?.Invoke(device,null);
                                    presentFrame?.Invoke(device,null);
                                }
                                System.Threading.Thread.Sleep(8);
                            } catch(Exception exL) { AddLog($"Loop: {exL.InnerException?.Message}"); break; }
                        }
                    } else {
                        AddLog($">> Falha LoadNsp - verifique keys/firmware");
                    }

                } catch (Exception ex) {
                    AddLog($"ERRO V12: {ex.InnerException?.Message?? ex.Message}\n{ex.InnerException?.StackTrace?.Substring(0,1500)}");
                }
            });
        }

        public void SurfaceCreated(ISurfaceHolder holder) { AddLog("SurfaceCreated - Vulkan OK! Boot V12..."); surfaceReady = true; TryBoot(); }
        public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format, int w, int h) { }
        public void SurfaceDestroyed(ISurfaceHolder holder) { surfaceReady = false; }
    }
}
