using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using System.IO;
using System.Linq;
using System;

[assembly: UsesPermission(Android.Manifest.Permission.ReadExternalStorage)]
[assembly: UsesPermission(Android.Manifest.Permission.WriteExternalStorage)]
[assembly: UsesPermission(Android.Manifest.Permission.ManageExternalStorage)]

namespace Ryujinx.Android
{
    [Activity(Label="Ryubing", MainLauncher=true, Theme="@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation=ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        string romPath1 = null!;
        string romPath2 = null!;
        string keysPath1 = null!;
        string keysPath2 = null!;
        LinearLayout list = null!;

        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            romPath1 = System.IO.Path.Combine("/storage/emulated/0", "Download", "Ryubing");
            keysPath1 = System.IO.Path.Combine(romPath1, "keys", "prod.keys");
            var appFiles = GetExternalFilesDir(null)!.AbsolutePath;
            romPath2 = System.IO.Path.Combine(appFiles, "roms");
            keysPath2 = System.IO.Path.Combine(appFiles, "prod.keys");
            try{ Directory.CreateDirectory(romPath1); Directory.CreateDirectory(System.IO.Path.Combine(romPath1,"keys")); }catch{}
            try{ Directory.CreateDirectory(romPath2); }catch{}

            if(Build.VERSION.SdkInt >= BuildVersionCodes.R){
                try{
                    if(!global::Android.OS.Environment.IsExternalStorageManager){
                        var intent = new global::Android.Content.Intent(global::Android.Provider.Settings.ActionManageAllFilesAccessPermission);
                        StartActivity(intent);
                    }
                }catch{
                    try{
                        var intent2 = new global::Android.Content.Intent(global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                        intent2.SetData(global::Android.Net.Uri.Parse("package:"+PackageName));
                        StartActivity(intent2);
                    }catch{}
                }
            }

            var root = new LinearLayout(this){ Orientation = Orientation.Vertical };
            root.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#111111"));
            var top = new LinearLayout(this){ Orientation = Orientation.Horizontal };
            top.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#e10600"));
            top.SetPadding(40,25,40,25);
            var t = new TextView(this){ Text="RYUBING #16.2 FIX PERM" };
            t.SetTextColor(global::Android.Graphics.Color.White); t.TextSize=11; t.SetTypeface(null, global::Android.Graphics.TypefaceStyle.Bold);
            var btn = new Button(this){ Text="ATUALIZAR" };
            btn.SetBackgroundColor(global::Android.Graphics.Color.White); btn.SetTextColor(global::Android.Graphics.Color.ParseColor("#e10600"));
            var btnPerm = new Button(this){ Text="PERMISSÃO" };
            btnPerm.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#ffaa00")); btnPerm.SetTextColor(global::Android.Graphics.Color.Black);
            top.AddView(t, new LinearLayout.LayoutParams(0,-2,1f));
            top.AddView(btnPerm);
            top.AddView(btn);
            var scroll = new ScrollView(this);
            list = new LinearLayout(this){ Orientation=Orientation.Vertical };
            list.SetPadding(20,20,20,20);
            scroll.AddView(list);
            root.AddView(top);
            root.AddView(scroll);
            SetContentView(root);
            btn.Click += (s,e)=>{ Load(); };
            btnPerm.Click += (s,e)=>{
                try{
                    var intent = new global::Android.Content.Intent(global::Android.Provider.Settings.ActionManageAllFilesAccessPermission);
                    StartActivity(intent);
                }catch{
                    var intent = new global::Android.Content.Intent(global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                    intent.SetData(global::Android.Net.Uri.Parse("package:"+PackageName));
                    StartActivity(intent);
                }
            };
            Load();
        }

        void Load()
        {
            list.RemoveAllViews();
            try{
                bool isManager = false;
                if(Build.VERSION.SdkInt >= BuildVersionCodes.R) isManager = global::Android.OS.Environment.IsExternalStorageManager;

                var permTxt = new TextView(this){ Text="Permissão Todos Arquivos: "+(isManager?"✅ SIM":"❌ NÃO - Clique PERMISSÃO") };
                permTxt.SetTextColor(global::Android.Graphics.Color.ParseColor(isManager?"#00ff88":"#ffaa00"));
                permTxt.SetPadding(10,10,10,10);
                list.AddView(permTxt);

                bool hasKeys1 = File.Exists(keysPath1);
                bool hasKeys2 = File.Exists(keysPath2);
                bool hasKeys = hasKeys1 || hasKeys2;

                var keysInfo = new TextView(this){ Text=(hasKeys?"✅ prod.keys OK":"❌ prod.keys falta")+"\n"+romPath1 };
                keysInfo.SetTextColor(global::Android.Graphics.Color.ParseColor(hasKeys?"#00ff88":"#ff4444"));
                keysInfo.TextSize=10; keysInfo.SetPadding(10,10,10,20);
                keysInfo.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#222222"));
                list.AddView(keysInfo);

                var path1Files = Directory.Exists(romPath1)? Directory.GetFiles(romPath1) : new string[0];
                var path2Files = Directory.Exists(romPath2)? Directory.GetFiles(romPath2) : new string[0];
                var roms = path1Files.Concat(path2Files).Where(f=> f.ToLower().EndsWith(".nsp")||f.ToLower().EndsWith(".xci")||f.ToLower().EndsWith(".nsz")||f.ToLower().EndsWith(".xcz")).ToArray();

                var info = new TextView(this){ Text="SCAN: "+romPath1+" => "+path1Files.Length+" files\n"+romPath2+" => "+path2Files.Length+" files\nJOGOS: "+roms.Length };
                info.SetTextColor(global::Android.Graphics.Color.ParseColor("#aaaaaa")); info.TextSize=10; info.SetPadding(10,10,10,20);
                list.AddView(info);

                foreach(var f in roms){
                    var card = new LinearLayout(this){ Orientation=Orientation.Horizontal };
                    card.SetPadding(30,30,30,30); card.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#1c1c1c"));
                    var lp = new LinearLayout.LayoutParams(-1,-2); lp.SetMargins(0,0,0,18); card.LayoutParameters=lp;
                    var name = new TextView(this){ Text=System.IO.Path.GetFileName(f) };
                    name.SetTextColor(global::Android.Graphics.Color.White); name.TextSize=13;
                    var play = new Button(this){ Text="JOGAR" };
                    play.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#00c853")); play.SetTextColor(global::Android.Graphics.Color.White);
                    card.AddView(name, new LinearLayout.LayoutParams(0,-2,1f));
                    card.AddView(play);
                    list.AddView(card);
                }
                if(roms.Length==0){
                    var empty = new TextView(this){ Text="Mova.nsp para:\n"+romPath1+"\n\nE clique PERMISSÃO > ative Ryubing > voltar > ATUALIZAR" };
                    empty.SetTextColor(global::Android.Graphics.Color.White); empty.TextSize=11;
                    list.AddView(empty);
                }
            }catch(Exception ex){
                var err = new TextView(this){ Text="ERRO: "+ex.Message };
                err.SetTextColor(global::Android.Graphics.Color.ParseColor("#ff4444"));
                list.AddView(err);
            }
        }
    }
}
