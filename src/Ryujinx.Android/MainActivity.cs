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
            var layout = new LinearLayout(this);
            layout.Orientation = Orientation.Vertical;
            layout.SetPadding(40,80,40,40);
            layout.SetBackgroundColor(Android.Graphics.Color.Rgb(15,15,15));

            var title = new TextView(this);
            title.Text = "Ryu - Build OK ✓";
            title.SetTextSize(Android.Util.ComplexUnitType.Sp,22);
            title.SetTextColor(Android.Graphics.Color.White);
            layout.AddView(title);

            logView = new TextView(this);
            logView.Text = "\nPronto pra rodar Zelda\n";
            logView.SetTextColor(Android.Graphics.Color.White);
            layout.AddView(logView);

            var btn = new Button(this);
            btn.Text = "🎮 RODAR ZELDA LINKS AWAKENING";
            btn.SetBackgroundColor(Android.Graphics.Color.Rgb(0,120,215));
            layout.AddView(btn);

            btn.Click += (s,e) => {
                string nsp = "/storage/emulated/0/Switch/Games/The Legend of Zelda Links Awakening.nsp";
                // verifica keys
                string prod = "/storage/emulated/0/Switch/prod.keys";
                if(!File.Exists(prod)){
                    logView.Text += "\nFALTA prod.keys em /Switch/prod.keys";
                    Toast.MakeText(this,"Coloca prod.keys!",ToastLength.Long).Show();
                    return;
                }
                if(!File.Exists(nsp)){
                    logView.Text += $"\nNSP não achado: {nsp}";
                    return;
                }
                logView.Text += $"\nNSP encontrado! Iniciando...";
                // Chama GameActivity
                var intent = new Intent(this, typeof(GameActivity));
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
            Window.AddFlags(WindowManagerFlags.KeepScreenOn | WindowManagerFlags.Fullscreen);
            var tv = new TextView(this);
            tv.Text = $"Carregando:\n{GamePath}\n\nAqui entra o core do Ryujinx";
            tv.SetTextColor(Android.Graphics.Color.White);
            SetContentView(tv);
        }
    }
}
