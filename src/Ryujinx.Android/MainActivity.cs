using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Graphics;
using System.IO;

namespace Ryujinx.Android
{
    [Activity(Label="Ryubing", MainLauncher=true, Theme="@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation=ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        string romPath = "/sdcard/Ryubing/roms";
        LinearLayout list;

        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                RequestPermissions(new[] { Android.Manifest.Permission.ReadExternalStorage, Android.Manifest.Permission.WriteExternalStorage }, 0);

            var root = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.SetBackgroundColor(Color.ParseColor("#0e0e0e"));

            var top = new LinearLayout(this){ Orientation = Orientation.Horizontal };
            top.SetBackgroundColor(Color.ParseColor("#e10600"));
            top.SetPadding(40,25,40,25);
            var t = new TextView(this){ Text="RYUBING - SD865 | /sdcard/Ryubing/roms" };
            t.SetTextColor(Color.White); t.TextSize=14; t.SetTypeface(null, TypefaceStyle.Bold);
            var btn = new Button(this){ Text="ATUALIZAR" };
            btn.SetBackgroundColor(Color.White); btn.SetTextColor(Color.ParseColor("#e10600"));
            top.AddView(t, new LinearLayout.LayoutParams(0,-2,1f));
            top.AddView(btn);
            
            var scroll = new ScrollView(this);
            list = new LinearLayout(this){ Orientation=Orientation.Vertical };
            list.SetPadding(20,20,20,20);
            scroll.AddView(list);
            
            root.AddView(top);
            root.AddView(scroll);
            SetContentView(root);

            btn.Click += (s,e)=>Load();
            Load();
        }

        void Load()
        {
            list.RemoveAllViews();
            try{
                if(!Directory.Exists(romPath)) Directory.CreateDirectory(romPath);
                var files = Directory.GetFiles(romPath);
                if(files.Length==0){
                    var empty = new TextView(this);
                    empty.Text=$"Nenhum jogo encontrado\n\nCrie a pasta:\n{romPath}\n\nE coloque .nsp .xci .nro";
                    empty.SetTextColor(Color.ParseColor("#888")); empty.Gravity=GravityFlags.Center; empty.SetPadding(0,200,0,0); empty.TextSize=16;
                    list.AddView(empty); return;
                }
                foreach(var f in files){
                    var card = new LinearLayout(this){ Orientation=Orientation.Horizontal };
                    card.SetPadding(30,30,30,30);
                    card.SetBackgroundColor(Color.ParseColor("#1c1c1c"));
                    var lp = new LinearLayout.LayoutParams(-1,-2); lp.SetMargins(0,0,0,18); card.LayoutParameters=lp;

                    var name = new TextView(this){ Text=Path.GetFileName(f) };
                    name.SetTextColor(Color.White); name.TextSize=15;
                    var play = new Button(this){ Text="JOGAR" };
                    play.SetBackgroundColor(Color.ParseColor("#00c853")); play.SetTextColor(Color.White);

                    card.AddView(name, new LinearLayout.LayoutParams(0,-2,1f));
                    card.AddView(play);
                    play.Click+= (s,e)=> Toast.MakeText(this, $"Carregando {Path.GetFileName(f)} - Core vindo no #9", ToastLength.Short).Show();
                    list.AddView(card);
                }
            }catch(System.Exception ex){
                Toast.MakeText(this, ex.Message, ToastLength.Long).Show();
            }
        }
    }
}
