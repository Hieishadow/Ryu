using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using Android.Content;
using System;
using System.IO;
using System.Linq;

namespace Ryujinx.Android
{
    [Activity(Label = "Ryu", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar")]
    public class MainActivity : Activity
    {
        TextView logView;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            var layout = new global::Android.Widget.LinearLayout(this);
            layout.Orientation = global::Android.Widget.Orientation.Vertical;
            layout.SetPadding(40,80,40,40);
            layout.SetBackgroundColor(new global::Android.Graphics.Color(15,15,15));

            var title = new global::Android.Widget.TextView(this);
            title.Text = "Ryu - Build OK ✓";
            title.SetTextSize(global::Android.Util.ComplexUnitType.Sp,22);
            title.SetTextColor(global::Android.Graphics.Color.White);
            layout.AddView(title);

            logView = new global::Android.Widget.TextView(this);
            logView.Text = "\nPronto pra rodar Zelda\n";
            logView.SetTextColor(global::Android.Graphics.Color.White);
            layout.AddView(logView);

            var btn = new global::Android.Widget.Button(this);
            btn.Text = "🎮 RODAR ZELDA LINKS AWAKENING";
            btn.SetBackgroundColor(new global::Android.Graphics.Color(0,120,215));
            layout.AddView(btn);

            btn.Click += (s,e) => {
                string nsp = "/storage/emulated/0/Switch/Games/The Legend of Zelda Links Awakening.nsp";
                string prod = "/storage/emulated/0/Switch/prod.keys";
                if(!File.Exists(prod)){
                    logView.Text += "\nFALTA prod.keys em /Switch/prod.keys";
                    global::Android.Widget.Toast.MakeText(this,"Coloca prod.keys!",global::Android.Widget.ToastLength.Long).Show();
                    return;
                }
                if(!File.Exists(nsp)){
                    logView.Text += $"\nNSP não achado: {nsp}";
                    return;
                }
                logView.Text += $"\nNSP encontrado! Iniciando...";
                var intent = new global::Android.Content.Intent(this, typeof(GameActivity));
                GameActivity.GamePath = nsp;
                StartActivity(intent);
            };
            SetContentView(layout);
        }
    }

    [Activity(Label = "Game", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
    public class GameActivity : Activity
    {
        public static string GamePath;
        protected override void OnCreate(Bundle b)
        {
            base.OnCreate(b);
            Window.AddFlags(global::Android.Views.WindowManagerFlags.KeepScreenOn | global::Android.Views.WindowManagerFlags.Fullscreen);
            var tv = new global::Android.Widget.TextView(this);
            tv.Text = $"Carregando:\n{GamePath}\n\nAqui entra o core do Ryujinx";
            tv.SetTextColor(global::Android.Graphics.Color.White);
            SetContentView(tv);
        }
    }
}
