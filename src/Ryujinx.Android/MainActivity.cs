using Android.App;
using Android.OS;
using Android.Widget;

namespace Ryujinx.Android;

[Activity(Label = "Ryubing", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var tv = new TextView(this);
        tv.Text = "Ryubing Android - Snapdragon 865\nBuild OK!\nPróximo passo: UI + Core";
        tv.TextSize = 20;
        tv.SetPadding(40,100,40,40);
        SetContentView(tv);
    }
}
