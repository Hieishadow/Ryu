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
        string keysPath = null!;
        LinearLayout list = null!;

        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            var baseDownload = System.IO.Path.Combine("/storage/emulated/0", "Download", "Ryubing");
            var keysFolder = System.IO.Path.Combine(baseDownload, "keys");
            try{
                Directory.CreateDirectory(baseDownload);
                Directory.CreateDirectory(keysFolder);
                romPath = baseDownload;
                keysPath = System.IO.Path.Combine(keysFolder, "prod.keys");
            }catch{
                romPath = System.IO.Path.Combine(GetExternalFilesDir(null)!.AbsolutePath, "roms");
                keysPath = System.IO.Path.Combine(GetExternalFilesDir(null)!.AbsolutePath, "prod.keys");
            }

            var root = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#111111"));
            var top = new LinearLayout(this){ Orientation = Orientation.Horizontal };
            top.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#e10600"));
            top.SetPadding(40,25,40,25);
            var t = new TextView(this){ Text="RYUBING #14.1 FIX" };
            t.SetTextColor(global::Android.Graphics.Color.White); t.TextSize=12; t.SetTypeface(null, global::Android.Graphics.TypefaceStyle.Bold);
            var btn = new Button(this){ Text="ATUALIZAR" };
            btn.SetBackgroundColor(global::Android.Graphics.Color.White); btn.SetTextColor(global::Android.Graphics.Color.ParseColor("#e10600"));
            top.AddView(t, new LinearLayout.LayoutParams(0,-2,1f));
            top.AddView(btn);
            var scroll = new ScrollView(this);
            list = new LinearLayout(this){ Orientation=Orientation.Vertical };
            list.SetPadding(20,20,20,20);
            scroll.AddView(list);
            root.AddView(top);
            root.AddView(scroll);
            SetContentView(root);
            btn.Click += (s,e)=>{ Toast.MakeText(this, "Recarregando...", ToastLength.Short).Show(); Load(); };
            Load();
        }

        void Load()
        {
            list.RemoveAllViews();
            try{
                bool hasKeys = File.Exists(keysPath);
                var statusColor = hasKeys? "#00c853" : "#ff4444";
                var statusText = hasKeys? "✅ prod.keys OK" : "❌ prod.keys FALTA\n"+keysPath;
                var keysInfo = new TextView(this){ Text=statusText+"\nPasta: "+romPath };
                keysInfo.SetTextColor(global::Android.Graphics.Color.ParseColor(statusColor));
                keysInfo.TextSize=11; keysInfo.SetPadding(10,10,10,20);
                keysInfo.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#222222"));
                list.AddView(keysInfo);

                var allFiles = Directory.Exists(romPath)? Directory.GetFiles(romPath) : new string[0];
                var roms = allFiles.Where(f=> f.ToLower().EndsWith(".nsp")||f.ToLower().EndsWith(".xci")||f.ToLower().EndsWith(".nsz")||f.ToLower().EndsWith(".xcz")).ToArray();

                if(roms.Length==0){
                    var empty = new TextView(this){ Text="\nNenhum jogo em:\n"+romPath+"\nTotal: "+allFiles.Length };
                    empty.SetTextColor(global::Android.Graphics.Color.White); empty.Gravity=GravityFlags.Center; empty.TextSize=13;
                    list.AddView(empty); return;
                }
                foreach(var f in roms){
                    var card = new LinearLayout(this){ Orientation=Orientation.Vertical };
                    card.SetPadding(30,25,30,25); card.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#1c1c1c"));
                    var lp = new LinearLayout.LayoutParams(-1,-2); lp.SetMargins(0,0,0,18); card.LayoutParameters=lp;
                    var row1 = new LinearLayout(this){ Orientation=Orientation.Horizontal };
                    var name = new TextView(this){ Text=System.IO.Path.GetFileName(f) };
                    name.SetTextColor(global::Android.Graphics.Color.White); name.TextSize=14;
                    var size = new FileInfo(f).Length/1024/1024;
                    var sizeTxt = new TextView(this){ Text=size+" MB" };
                    sizeTxt.SetTextColor(global::Android.Graphics.Color.ParseColor("#888888")); sizeTxt.TextSize=10;
                    row1.AddView(name, new LinearLayout.LayoutParams(0,-2,1f));
                    row1.AddView(sizeTxt);
                    var row2 = new LinearLayout(this){ Orientation=Orientation.Horizontal };
                    row2.SetPadding(0,15,0,0);
                    var play = new Button(this){ Text= hasKeys? "JOGAR #14.1" : "PRECISA KEYS" };
                    play.SetBackgroundColor(global::Android.Graphics.Color.ParseColor(hasKeys? "#00c853" : "#555555"));
                    play.SetTextColor(global::Android.Graphics.Color.White);
                    row2.AddView(play, new LinearLayout.LayoutParams(-1,-2));
                    card.AddView(row1);
                    card.AddView(row2);
                    list.AddView(card);
                }
            }catch(Exception ex){
                var err = new TextView(this){ Text="ERRO: "+ex.Message };
                err.SetTextColor(global::Android.Graphics.Color.ParseColor("#ff4444"));
                list.AddView(err);
            }
        }
    }
}
