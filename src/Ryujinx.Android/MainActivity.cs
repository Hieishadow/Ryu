using System;
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
            tv.SetPadding(30,30,30,30);
            tv.SetTextIsSelectable(true);
            
            try
            {
                var t = typeof(Ryujinx.Graphics.GAL.IRenderer);
                tv.Text = $"V18 - Build 74\nSWITCH OK!!!\n\n{t.FullName}\nDLL: {t.Assembly.GetName().Name}\nTypes: {t.Assembly.GetTypes().Length}\n\nTrimmer OK! Count 8 virou SWITCH OK!";
            }
            catch(Exception ex)
            {
                tv.Text = $"FAIL 74\n{ex}";
            }
            SetContentView(tv);
        }
    }
}
