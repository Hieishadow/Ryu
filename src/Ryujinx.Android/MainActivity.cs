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
        TextView infoText = null!;

        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            
            var downloadPath = Path.Combine("/storage/emulated/0", "Download", "Ryubing");
            try{
                if(!Directory.Exists(downloadPath)) Directory.CreateDirectory(downloadPath);
                romPath = downloadPath;
            }catch{ romPath = Path.Combine(GetExternalFilesDir(null)!.AbsolutePath, "roms"); }

            var root = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.SetBackgroundColor(Color.ParseColor("#111111"));

            var top = new LinearLayout(this){ Orientation = Orientation.Horizontal };
            top.SetBackgroundColor(Color.ParseColor("#e10600"));
            top.SetPadding(40,25,40,25);
            var t = new TextView(this){ Text="RYUBING - SD865 - #12.1" };
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

            // CORREÇÃO DO BOTÃO
            btn.Click += (s,e)=>{
                Toast.MakeText(this, "Atualizando... "+romPath, ToastLength.Short).Show();
                Load();
            };
            Load();
        }

        void Load()
        {
            list.RemoveAllViews();
            try{
                if(!Directory.Exists(romPath)) Directory.CreateDirectory(romPath);
                
                // LISTA TUDO MESMO, SEM FILTRO PRA DEBUGAR
                var allFiles = Directory.GetFiles(romPath, "*.*", SearchOption.TopDirectoryOnly);
                var roms = allFiles.Where(f=> f.ToLower().EndsWith(".nsp")||f.ToLower().EndsWith(".xci")||f.ToLower().EndsWith(".nsz")||f.ToLower().EndsWith(".xcz")).ToArray();
                
                var info = new TextView(this){ Text="Pasta: "+romPath+"\nArquivos totais na pasta: "+allFiles.Length+"\nJogos (.nsp/.xci): "+roms.Length+"\n\nSe tá 0, a pasta tá vazia ou o Android não liberou." };
                info.SetTextColor(Color.ParseColor("#888888")); info.TextSize=11;
                info.SetPadding(10,10,10,20);
                list.AddView(info);

                if(allFiles.Length>0){
                    var debug = new TextView(this){ Text="Arquivos encontrados:\n"+string.Join("\n", allFiles.Select(Path.GetFileName)) };
                    debug.SetTextColor(Color.ParseColor("#ffff00")); debug.TextSize=10;
                    list.AddView(debug);
                }

                if(roms.Length==0){
                    var empty = new TextView(this);
                    empty.Text="\nNenhum jogo .nsp/.xci encontrado\n\n1. Coloque em:\n"+romPath+"\n\n2. Prod.keys só precisa no #13 pra RODAR, não pra listar";
                    empty.SetTextColor(Color.White); empty.Gravity=GravityFlags.Center; empty.TextSize=13;
                    list.AddView(empty); return;
                }
                foreach(var f in roms){
                    var card = new LinearLayout(this){ Orientation=Orientation.Horizontal };
                    card.SetPadding(30,30,30,30);
                    card.SetBackgroundColor(Color.ParseColor("#1c1c1c"));
                    var lp = new LinearLayout.LayoutParams(-1,-2); lp.SetMargins(0,0,0,18); card.LayoutParameters=lp;
                    var name = new TextView(this){ Text=Path.GetFileName(f)+" ("+(new FileInfo(f).Length/1024/1024)+" MB)" };
                    name.SetTextColor(Color.White); name.TextSize=14;
                    var play = new Button(this){ Text="JOGAR" };
                    play.SetBackgroundColor(Color.ParseColor("#00c853")); play.SetTextColor(Color.White);
                    card.AddView(name, new LinearLayout.LayoutParams(0,-2,1f));
                    card.AddView(play);
                    play.Click+= (s,e)=> Toast.MakeText(this, "Vai precisar de prod.keys no #13", ToastLength.Long).Show();
                    list.AddView(card);
                }
            }catch(Exception ex){
                var err = new TextView(this){ Text="ERRO: "+ex.Message+"\n"+ex.StackTrace };
                err.SetTextColor(Color.ParseColor("#ff4444"));
                list.AddView(err);
            }
        }
    }
}
