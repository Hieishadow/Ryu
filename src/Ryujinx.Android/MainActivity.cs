using Android.App;
using Android.OS;
using Android.Widget;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX", MainLauncher = true, Exported = true)]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            var tv = new TextView(this);
            tv.Text = "DragoNX ABRIU! Se apareceu isso, o problema era a GameActivity.";
            tv.TextSize = 20f;
            tv.SetPadding(40,100,40,40);
            SetContentView(tv);
        }
    }
}
