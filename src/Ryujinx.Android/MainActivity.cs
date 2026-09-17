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
            tv.TextSize = 12f;
            tv.SetPadding(20, 20, 20, 20);
            tv.SetTextIsSelectable(true);
            SetContentView(tv);

            try
            {
                var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } }).ToList();

                var hasVulkan = allTypes.Any(t => t.Name.Contains("Vulkan"));
                var hasSwitch = allTypes.Any(t => t.Name == "Switch");
                var count = allTypes.Count;

                var candidates = allTypes.Where(t => t.Name.Contains("Vulkan") || t.Name.Contains("Renderer"))
                                     .Where(t =>!t.IsInterface &&!t.IsAbstract)
                                     .Take(10).ToList();

                string list = candidates.Count > 0? string.Join("\n", candidates.Select(t => $"{t.Name}")) : "NENHUM";

                tv.Text = $"#78 - TESTE 280MB\n\nTotal Types: {count}\nHas Switch: {hasSwitch}\nHas Vulkan: {hasVulkan}\n\nTOP RENDERERS:\n{list}\n\n";

                if (count > 5000 && hasVulkan)
                    tv.Text += "APK 280MB OK! ME CHAMA QUE TE MANDO #79 COM VIDEO";
                else if (count < 2000)
                    tv.Text += "Ainda 148MB - refaz o yml que te mandei";
                else
                    tv.Text += "Quase la - manda print desse texto";
            }
            catch (Exception ex)
            {
                tv.Text = $"FAIL #78\n{ex}";
            }
        }
    }
}
