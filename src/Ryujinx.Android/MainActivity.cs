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

            var log = "V17.6 HUNTER 6 - TOUCH FORCE + DLL FINDER\n\n";

            // 1. TOUCH FORCE - impede o trimmer de apagar
            log += "--- 1. TOUCH FORCE ---\n";
            try
            {
                var t1 = typeof(Ryujinx.Common.Logging.Logger);
                var t2 = typeof(Ryujinx.Graphics.GAL.IRenderer);
                var t3 = typeof(Ryujinx.HLE.HLESystem);
                var t4 = typeof(ARMeilleure.Translation.PTC.PtcProfiler);
                var t5 = typeof(Ryujinx.Memory.MemoryBlock);
                var t6 = typeof(Ryujinx.Cpu.Jit.JitCpuContext);
                var t7 = typeof(Ryujinx.Graphics.Gpu.Engine.GpuContext);

                log += $"TOUCH OK:\n";
                log += $" {t1.FullName}\n";
                log += $" {t2.FullName}\n";
                log += $" {t3.FullName}\n";
                log += $" {t4.FullName}\n";
                log += $" {t5.FullName}\n";
                log += $" {t6.FullName}\n";
                log += $" {t7.FullName}\n";
            }
            catch (Exception ex)
            {
                log += $"TOUCH FAIL:\n{ex}\n";
            }

            // 2. REFERENCED ASSEMBLIES
            log += "\n--- 2. REFERENCED ASSEMBLIES ---\n";
            try
            {
                var refs = Assembly.GetExecutingAssembly().GetReferencedAssemblies();
                log += $"Refs Count: {refs.Length}\n";
                foreach (var r in refs.OrderBy(x => x.Name))
                {
                    log += $" {r.Name}\n";
                }
            }
            catch (Exception ex)
            {
                log += $"REFS FAIL: {ex.Message}\n";
            }

            // 3. DLL FINDER
            log += "\n--- 3. DLL FINDER ---\n";
            try
            {
                var dirs = new[] {
                    AppDomain.CurrentDomain.BaseDirectory,
                    FilesDir.AbsolutePath,
                    "/data/data/com.ryubing.android/files"
                };

                foreach (var dir in dirs)
                {
                    try
                    {
                        if (Directory.Exists(dir))
                        {
                            var files = Directory.GetFiles(dir, "*.dll");
                            log += $"\n{dir} -> {files.Length} dlls\n";
                            foreach (var f in files.Where(f => f.Contains("Ryujinx") || f.Contains("ARMeilleure")).Take(30))
                            {
                                log += $"  {Path.GetFileName(f)}\n";
                            }
                            if (files.Length == 0) log += "  (nenhuma dll)\n";
                        }
                        else
                        {
                            log += $"\n{dir} -> NAO EXISTE\n";
                        }
                    }
                    catch (Exception ex)
                    {
                        log += $"\n{dir} ERR: {ex.Message}\n";
                    }
                }
            }
            catch (Exception ex)
            {
                log += $"DLL FINDER FAIL: {ex.Message}\n";
            }

            // 4. IRenderer REAL
            log += "\n--- 4. IRenderer REAL ---\n";
            try
            {
                var loaded = AppDomain.CurrentDomain.GetAssemblies().ToList();
                foreach (var ra in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
                {
                    try { if (!loaded.Any(a => a.GetName().Name == ra.Name)) loaded.Add(Assembly.Load(ra)); } catch { }
                }

                var allTypes = loaded.SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } }).ToList();
                var renderers = allTypes.Where(t => t.FullName != null && t.Name == "IRenderer" && t.FullName.Contains("Ryujinx")).ToList();

                log += $"Found {renderers.Count} Ryujinx IRenderer\n";
                foreach (var r in renderers)
                {
                    log += $"\n{r.FullName} Interface:{r.IsInterface}\n";
                    if (r.IsInterface)
                    {
                        foreach (var m in r.GetMethods())
                        {
                            log += $"  {m.ReturnType.Name} {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})\n";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log += $"IRENDERER FAIL: {ex.Message}\n";
            }

            log += "\n\n--- FIM ---\nManda essa print que vamos pra V18!";

            Android.Util.Log.Error("RYU_HUNTER6", log);

            var tv = new Android.Widget.TextView(this);
            tv.Text = log;
            tv.SetTextIsSelectable(true);
            tv.TextSize = 9f;
            tv.SetPadding(20, 20, 20, 20);
            SetContentView(tv);
        }
    }
}
