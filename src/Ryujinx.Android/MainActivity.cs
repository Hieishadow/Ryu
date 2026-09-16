using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using System.IO;
using System.Linq;
using System;

namespace Ryujinx.Android
{
    [Activity(Label="Ryubing", MainLauncher=true, Theme="@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation=ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        string romPath = null!;
        LinearLayout list = null!;

        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            var downloadPath = System.IO.Path.Combine("/storage/emulated/0", "Download", "Ryubing");
            try{
                if(!Directory.Exists(downloadPath)) Directory.CreateDirectory(downloadPath);
                romPath = downloadPath;
            }catch{ romPath = System.IO.Path.Combine(GetExternalFilesDir(null)!.AbsolutePath, "roms"); }

            var root = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.SetBackgroundColor(Android.Graphics.Color.ParseColor("#111111"));
            var top = new LinearLayout(this){ Orientation = Orientation.Horizontal };
            top.SetBackgroundColor(Android.Graphics.Color.ParseColor("#e10600"));
            top.SetPadding(40,25,40,25);
            var t = new TextView(this){ Text="RYUBING #13.2 FINAL" };
            t.SetTextColor(Android.Graphics.Color.White); t.TextSize=13; t.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
            var btn = new Button(this){ Text="ATUALIZAR" };
            btn.SetBackgroundColor(Android.Graphics.Color.White); btn.SetTextColor(Android.Graphics.Color.ParseColor("#e10600"));
            top.AddView(t, new LinearLayout.LayoutParams(0,-2,1f));
            top.AddView(btn);
            var scroll = new ScrollView(this);
            list = new LinearLayout(this){ Orientation=Orientation.Vertical };
            list.SetPadding(20,20,20,20);
            scroll.AddView(list);
            root.AddView(top);
            root.AddView(scroll);
            SetContentView(root);
            btn.Click += (s,e)=>{ Toast.MakeText(this, "Atualizando "+romPath, ToastLength.Short).Show(); Load(); };
            Load();
        }

        void Load()
        {
            list.RemoveAllViews();
            try{
                if(!Directory.Exists(romPath)) Directory.CreateDirectory(romPath);
                var allFiles = Directory.GetFiles(romPath);
                var roms = allFiles.Where(f=> f.ToLower().EndsWith(".nsp")||f.ToLower().EndsWith(".xci")||f.ToLower().EndsWith(".nsz")||f.ToLower().EndsWith(".xcz")).ToArray();
                var info = new TextView(this){ Text="Pasta: "+romPath+"\nTotal: "+allFiles.Length+" | Jogos: "+roms.Length };
                info.SetTextColor(Android.Graphics.Color.ParseColor("#888888")); info.TextSize=11; info.SetPadding(10,10,10,20);
                list.AddView(info);
                if(allFiles.Length>0){
                    var debug = new TextView(this){ Text=string.Join("\n", allFiles.Select(f=> System.IO.Path.GetFileName(f))) };
                    debug.SetTextColor(Android.Graphics.Color.ParseColor("#ffff00")); debug.TextSize=12;
                    list.AddView(debug);
                }
                if(roms.Length==0){
                    var empty = new TextView(this){ Text="\nVazio - coloque .nsp em:\n"+romPath };
                    empty.SetTextColor(Android.Graphics.Color.White); empty.Gravity=GravityFlags.Center;
                    list.AddView(empty); return;
                }
                foreach(var f in roms){
                    var card = new LinearLayout(this){ Orientation=Orientation.Horizontal };
                    card.SetPadding(30,30,30,30); card.SetBackgroundColor(Android.Graphics.Color.ParseColor("#1c1c1c"));
                    var lp = new LinearLayout.LayoutParams(-1,-2); lp.SetMargins(0,0,0,18); card.LayoutParameters=lp;
                    var name = new TextView(this){ Text=System.IO.Path.GetFileName(f) };
                    name.SetTextColor(Android.Graphics.Color.White);
                    var play = new Button(this){ Text="JOGAR" };
                    play.SetBackgroundColor(Android.Graphics.Color.ParseColor("#00c853")); play.SetTextColor(Android.Graphics.Color.White);
                    card.AddView(name, new LinearLayout.LayoutParams(0,-2,1f));
                    card.AddView(play);
                    list.AddView(card);
                }
            }catch(Exception ex){
                var err = new TextView(this){ Text="ERRO: "+ex.Message };
                err.SetTextColor(Android.Graphics.Color.ParseColor("#ff4444"));
                list.AddView(err);
            }
        }
    }
}
