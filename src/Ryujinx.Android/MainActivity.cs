using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Android.App;
using Android.OS;
using Android.Content.PM;

namespace RyujinxAndroid
{
    [Activity(Label = "Ryujinx", MainLauncher = true, ScreenOrientation = ScreenOrientation.Landscape, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            var log = "V17.6 HUNTER 6 FIX\n\n";

            log += "--- 1. TOUCH FORCE ---\n";
            try
            {
                // Só tipos 100% públicos
                var t1 = typeof(Ryujinx.Common.Logging.Logger);
                var t2 = typeof(Ryujinx.Graphics.GAL.IRenderer);
                var t3 = typeof(Ryujinx.Memory.MemoryBlock);
                log += $"TOUCH OK:\n {t1.FullName}\n {t2.FullName}\n {t3.FullName}\n";
            }
            catch (Exception ex){ log += $"TOUCH FAIL:\n{ex}\n"; }

            log += "\n--- 2. REFS ---\n";
            try
            {
                var refs = Assembly.GetExecutingAssembly().GetReferencedAssemblies();
                log += $"Count: {refs.Length}\n";
                foreach (var r in refs.OrderBy(x => x.Name))
                    log += $" {r.Name}\n";
            }
            catch (Exception ex){ log += $"REFS FAIL {ex.Message}\n"; }

            log += "\n--- 3. DLL FINDER ---\n";
            try
            {
                var dirs = new[] { AppDomain.CurrentDomain.BaseDirectory, FilesDir.AbsolutePath, "/data/data/com.ryubing.android/files" };
                foreach (var dir in dirs)
                {
                    try{
                        if (Directory.Exists(dir)){
                            var files = Directory.GetFiles(dir, "*.dll");
                            log += $"\n{dir} -> {files.Length}\n";
                            foreach (var f in files.Where(f=>f.Contains("Ryujinx")||f.Contains("ARMeilleure")).Take(20))
                                log += $" {Path.GetFileName(f)}\n";
                        } else log += $"\n{dir} -> NO\n";
                    }catch(Exception ex){ log+=$"\n{dir} ERR {ex.Message}\n"; }
                }
            }catch(Exception ex){ log+=$"FINDER FAIL {ex.Message}\n"; }

            log += "\n--- 4. IRenderer ---\n";
            try
            {
                var loaded = AppDomain.CurrentDomain.GetAssemblies().ToList();
                foreach(var ra in Assembly.GetExecutingAssembly().GetReferencedAssemblies()){
                    try{ if(!loaded.Any(a=>a.GetName().Name==ra.Name)) loaded.Add(Assembly.Load(ra)); }catch{}
                }
                var all = loaded.SelectMany(a=>{ try{ return a.GetTypes(); }catch{ return new Type[0]; } }).ToList();
                var ir = all.Where(t=>t.Name=="IRenderer" && t.FullName!=null && t.FullName.Contains("Ryujinx")).ToList();
                log+=$"Found {ir.Count}\n";
                foreach(var r in ir){
                    log+=$"\n{r.FullName} IsInterface:{r.IsInterface}\n";
                    if(r.IsInterface){
                        foreach(var m in r.GetMethods())
                            log+=$" {m.ReturnType.Name} {m.Name}({string.Join(",",m.GetParameters().Select(p=>p.ParameterType.Name))})\n";
                    }
                }
            }catch(Exception ex){ log+=$"IRENDERER FAIL {ex}\n"; }

            log+="\n--- FIM ---\n";
            Android.Util.Log.Error("RYU_HUNTER6", log);
            var tv = new Android.Widget.TextView(this);
            tv.Text=log; tv.SetTextIsSelectable(true); tv.TextSize=9f; tv.SetPadding(20,20,20,20);
            SetContentView(tv);
        }
    }
}
