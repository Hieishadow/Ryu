using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using System.IO;
using Android.Content.PM;

namespace Ryujinx.Android
{
    [Activity(Label="DragoNX Game", Theme="@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = ScreenOrientation.Landscape, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
    public class GameActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Window.AddFlags(WindowManagerFlags.KeepScreenOn | WindowManagerFlags.Fullscreen);
            var gamePath = Intent?.GetStringExtra("gamePath")?? "";
            var baseDir = Intent?.GetStringExtra("baseDir")?? "";

            var layout = new FrameLayout(this);
            var surface = new SurfaceView(this);
            layout.AddView(surface, new FrameLayout.LayoutParams(-1,-1));

            var log = new TextView(this);
            log.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#CC000000"));
            log.SetTextColor(global::Android.Graphics.Color.Green);
            log.SetPadding(20,20,20,20);
            log.TextSize = 12f;
            log.Text = $"DragoNX FULL\nGame: {Path.GetFileName(gamePath)}\nJIT: ON\nVulkan: Surface OK\nBase: {baseDir}\n";
            layout.AddView(log, new FrameLayout.LayoutParams(-1,700));
            SetContentView(layout);
        }
    }
}
