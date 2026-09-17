using Android.App;
using Android.OS;

namespace Ryujinx.Android
{
    [Activity(Label = "Ryujinx", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }
    }
}
