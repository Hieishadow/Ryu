using System;
using System.Linq;
using Android.App;
using Android.OS;
using Android.Content.PM;
using Android.Widget;
using Android.Graphics;

namespace RyujinxAndroid
{
    [Activity(Label = "Ryujinx", MainLauncher = true, ScreenOrientation = ScreenOrientation.Landscape, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Window.AddFlags(Android.Views.WindowManagerFlags.Fullscreen | Android.Views.WindowManagerFlags.KeepScreenOn);
            var tv = new TextView(this);
            tv.SetTextColor(Color.Lime);
            tv.TextSize = 10f;
            tv.SetPadding(20,20,20,20);
            tv.SetTextIsSelectable(true);
            try
            {
                var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                  .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } }).ToList();

                var candidates = allTypes.Where(t => t.Name.Contains("Vulkan") || t.Name.Contains("Renderer"))
                                       .Where(t =>!t.IsInterface &&!t.IsAbstract)
                                       .Take(20).ToList();

                string list = candidates.Count>0? string.Join("\n", candidates.Select(t => $"{t.Name} -> {t.FullName}\n DLL:{t.Assembly.GetName().Name}")) : "NENHUM Renderer achado!";

                string keysPath = "/storage/emulated/0/Ryujinx/bis";
                bool hasProd = System.IO.File.Exists(System.IO.Path.Combine(keysPath, "prod.keys")) || System.IO.File.Exists("/storage/emulated/0/Download/prod.keys");
                bool hasTitle = System.IO.File.Exists(System.IO.Path.Combine(keysPath, "title.keys")) || System.IO.File.Exists("/storage/emulated/0/Download/title.keys");

                var downloadPath = "/storage/emulated/0/Download";
                var games = System.IO.Directory.Exists(downloadPath)? System.IO.Directory.GetFiles(downloadPath).Where(f=>f.EndsWith(".nsp")||f.EndsWith(".xci")||f.EndsWith(".nsz")).Take(5).Select(f=> System.IO.Path.GetFileName(f)).ToList() : new System.Collections.Generic.List<string>();
                string gamesTxt = games.Count>0? string.Join("\n", games) : "Sem jogo em /Download";

                tv.Text = $"V19 - Build 75 - FIND VULKAN\n\nCANDIDATOS A RENDERER:\n{list}\n\nKEYS: prod.keys={hasProd} title.keys={hasTitle}\nPath checado: {keysPath}\n\nJOGOS:\n{gamesTxt}\n\nManda print disso que na #76 dou boot no primeiro!";
            }
            catch(Exception ex){ tv.Text = $"FAIL 75\n{ex}"; }
            SetContentView(tv);
        }
    }
}
