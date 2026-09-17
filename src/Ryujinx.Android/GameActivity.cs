using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using System.IO;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX Game", Exported = false, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Landscape)]
    public class GameActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            string gamePath = Intent?.GetStringExtra("gamePath") ?? "N/A";
            string baseDir = Intent?.GetStringExtra("baseDir") ?? "/storage/emulated/0/Download/DragoNX";

            var root = new LinearLayout(this){Orientation=Orientation.Vertical};
            root.SetBackgroundColor(global::Android.Graphics.Color.Black);
            root.SetPadding(20,20,20,20);

            var txt = new TextView(this);
            txt.Text = $"Tentando dar boot em:\n{gamePath}\n\nBase: {baseDir}\n\n[EMULADOR INICIANDO...]\nJIT: ON\nVulkan: ON";
            txt.SetTextColor(global::Android.Graphics.Color.White);
            txt.TextSize = 16f;
            root.AddView(txt);

            var surface = new View(this);
            surface.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#111111"));
            root.AddView(surface, new LinearLayout.LayoutParams(-1, -1));

            SetContentView(root);

            Toast.MakeText(this, "Boot: " + Path.GetFileName(gamePath), ToastLength.Long).Show();
        }
    }
}
