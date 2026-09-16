using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Graphics;
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
            
            // TENTA /Download, SE NÃO CONSEGUIR USA A PASTA INTERNA
            var downloadPath = System.IO.Path.Combine("/storage/emulated/0", "Download", "Ryubing");
            var internalPath = System.IO.Path.Combine(GetExternalFilesDir(null)!.AbsolutePath, "roms");
            
            // Se /Download existe ou consegue criar, usa ele
            try{
                if(!Directory.Exists(downloadPath)) Directory.CreateDirectory(downloadPath);
                // testa se pode escrever
                File.WriteAllText(System.IO.Path.Combine(downloadPath, ".test"), "ok");
                File.Delete(System.IO.Path.Combine(downloadPath, ".test"));
                romPath = downloadPath;
            }catch{
                romPath = internalPath;
            }

            var root = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.SetBackgroundColor(Color.ParseColor("#111111"));

            var top = new LinearLayout(this){ Orientation = Orientation.Horizontal };
            top.SetBackgroundColor(Color.ParseColor("#e10600"));
            top.SetPadding(40,25,40,25);
            var t = new TextView(this){ Text="RYUBING - SD865" };
            t.SetTextColor(Color.White); t.TextSize=13; t.SetTypeface(null, TypefaceStyle.Bold);
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
                var files = Directory.GetFiles(romPath).Where(f=> f.EndsWith(".nsp")||f.EndsWith(".xci")||f.EndsWith(".nro")).ToArray();
                
                var info = new TextView(this){ Text="Pasta: "+romPath+"\nColoque seus .nsp/.xci aqui\n\nTotal: "+files.Length+" jogos" };
                info.SetTextColor(Color.ParseColor("#888888")); info.TextSize=11;
                info.SetPadding(10,10,10,20);
                list.AddView(info);

                if(files.Length==0){
                    var empty = new TextView(this);
                    empty.Text="Nenhum jogo encontrado\n\nCopie pra:\n"+romPath;
                    empty.SetTextColor(Color.White); empty.Gravity=GravityFlags.Center; empty.SetPadding(0,60,0,0); empty.TextSize=15;
                    list.AddView(empty); return;
                }
                foreach(var f in files){
                    var card = new LinearLayout(this){ Orientation=Orientation.Horizontal };
                    card.SetPadding(30,30,30,30);
                    card.SetBackgroundColor(Color.ParseColor("#1c1c1c"));
                    var lp = new LinearLayout.LayoutParams(-1,-2); lp.SetMargins(0,0,0,18); card.LayoutParameters=lp;
                    var name = new TextView(this){ Text=System.IO.Path.GetFileName(f) };
                    name.SetTextColor(Color.White); name.TextSize=15;
                    var play = new Button(this){ Text="JOGAR" };
                    play.SetBackgroundColor(Color.ParseColor("#00c853")); play.SetTextColor(Color.White);
                    card.AddView(name, new LinearLayout.LayoutParams(0,-2,1f));
                    card.AddView(play);
                    play.Click+= (s,e)=> Toast.MakeText(this, "Iniciando "+System.IO.Path.GetFileName(f), ToastLength.Short).Show();
                    list.AddView(card);
                }
            }catch(Exception ex){
                var err = new TextView(this){ Text="Erro: "+ex.Message };
                err.SetTextColor(Color.ParseColor("#ff4444"));
                list.AddView(err);
            }
        }
    }
}
