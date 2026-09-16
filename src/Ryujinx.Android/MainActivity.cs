using System;
using System.Linq;
using System.Reflection;
using Android.App;
using Android.OS;

namespace RyujinxAndroid
{
    [Activity(Label = "Ryujinx", MainLauncher = true)]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            try
            {
                var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                string log = "V17.2 HUNTER3\n\n";
                var allTypes = allAssemblies.SelectMany(a => {
                    try { return a.GetTypes(); } catch { return new Type[0]; }
                }).ToList();

                var renderers = allTypes.Where(t => t.Name.Contains("Renderer") && t.IsInterface).Take(10).ToList();
                log += $"--- RENDERER ({renderers.Count}) ---\n";
                foreach(var r in renderers)
                {
                    log += $"{r.FullName}\n";
                    foreach(var m in r.GetMethods())
                        log += $" {m.ReturnType.Name} {m.Name}({string.Join(",", m.GetParameters().Select(p=>p.ParameterType.Name))})\n";
                }

                var windows = allTypes.Where(t => t.Name.Contains("IWindow") && t.IsInterface).Take(10).ToList();
                log += $"\n--- WINDOW ({windows.Count}) ---\n";
                foreach(var w in windows)
                {
                    log += $"{w.FullName}\n";
                    foreach(var m in w.GetMethods())
                        log += $" {m.ReturnType.Name} {m.Name}()\n";
                }

                var audios = allTypes.Where(t => t.Name.Contains("Audio") && t.IsInterface && t.Name.Contains("Render")).Take(10).ToList();
                log += $"\n--- AUDIO ({audios.Count}) ---\n";
                foreach(var a in audios) log += $"{a.FullName}\n";

                if(renderers.Count==0) {
                    log += "\n--- FALLBACK IRenderer ---\n";
                    foreach(var t in allTypes.Where(t=>t.Name.Contains("IRenderer")).Take(20))
                        log += $"{t.FullName} IsInterface:{t.IsInterface}\n";
                }

                Android.Util.Log.Error("RYU_HUNTER", log);
                var tv = new Android.Widget.TextView(this);
                tv.Text = log;
                tv.SetTextIsSelectable(true);
                tv.TextSize = 9f;
                SetContentView(tv);
            }
            catch(Exception ex)
            {
                var tv = new Android.Widget.TextView(this);
                tv.Text = "ERRO: " + ex.ToString();
                tv.SetTextIsSelectable(true);
                SetContentView(tv);
            }
        }
    }
}
