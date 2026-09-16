using System;
using System.Linq;
using System.Reflection;
using Android.App;
using Android.OS;
using Android.Content.PM;

namespace RyujinxAndroid
{
    [Activity(Label = "Ryujinx", MainLauncher = true, ScreenOrientation = ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            try
            {
                string log = "V17.4 HUNTER4 - ALL REFS\n\n";
                
                // Carrega todos os referenciados
                var loaded = AppDomain.CurrentDomain.GetAssemblies().ToList();
                var refs = Assembly.GetExecutingAssembly().GetReferencedAssemblies();
                log += $"Loaded:{loaded.Count} Refs:{refs.Length}\n";
                
                foreach(var r in refs)
                {
                    try {
                        if(!loaded.Any(a=>a.GetName().Name==r.Name))
                            loaded.Add(Assembly.Load(r));
                        log += $"OK {r.Name}\n";
                    } catch(Exception ex){ log += $"FAIL {r.Name} {ex.Message}\n"; }
                }

                var allTypes = loaded.SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; }}).ToList();
                log += $"\nTotal Types:{allTypes.Count}\n";

                var renderers = allTypes.Where(t => t.FullName != null && t.FullName.Contains("Ryujinx") && t.Name.Contains("IRenderer")).ToList();
                log += $"\n--- IRenderer ({renderers.Count}) ---\n";
                foreach(var r in renderers)
                {
                    log += $"{r.FullName} Int:{r.IsInterface}\n";
                    if(r.IsInterface)
                        foreach(var m in r.GetMethods())
                            log += $" {m.ReturnType.Name} {m.Name}({string.Join(",", m.GetParameters().Select(p=>p.ParameterType.Name))})\n";
                }

                var windows = allTypes.Where(t => t.FullName != null && t.FullName.Contains("Ryujinx") && t.Name.Contains("IWindow")).Take(5).ToList();
                log += $"\n--- IWindow ({windows.Count}) ---\n";
                foreach(var w in windows)
                {
                    log += $"{w.FullName}\n";
                    foreach(var m in w.GetMethods().Take(10))
                        log += $" {m.ReturnType.Name} {m.Name}()\n";
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
