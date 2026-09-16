using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Android.Provider;
using Android.Views;
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
        string romPath = "/storage/emulated/0/Download/Ryubing";
        string keysPath = "";
        LinearLayout list = null!;
        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            keysPath = Path.Combine(romPath,"keys","prod.keys");
            var root = new LinearLayout(this){ Orientation=Orientation.Vertical };
            root.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#111111"));
            var top = new LinearLayout(this){ Orientation=Orientation.Horizontal };
            top.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#e10600"));
            top.SetPadding(40,25,40,25);
            var t = new TextView(this){ Text="RYUBING #22 VULKAN RENDER" };
            t.SetTextColor(global::Android.Graphics.Color.White); t.SetTypeface(null, global::Android.Graphics.TypefaceStyle.Bold);
            var btnPerm = new Button(this){ Text="DAR PERMISSÃO" };
            btnPerm.SetBackgroundColor(global::Android.Graphics.Color.Yellow); btnPerm.SetTextColor(global::Android.Graphics.Color.Black);
            var btnAtual = new Button(this){ Text="ATUALIZAR" };
            btnAtual.SetBackgroundColor(global::Android.Graphics.Color.White); btnAtual.SetTextColor(global::Android.Graphics.Color.ParseColor("#e10600"));
            top.AddView(t, new LinearLayout.LayoutParams(0,-2,1f));
            top.AddView(btnPerm); top.AddView(btnAtual);
            var scroll = new ScrollView(this);
            list = new LinearLayout(this){ Orientation=Orientation.Vertical }; list.SetPadding(20,20,20,20);
            scroll.AddView(list);
            root.AddView(top); root.AddView(scroll);
            SetContentView(root);
            btnPerm.Click += (s,e)=>{
                var intent = new global::Android.Content.Intent(Settings.ActionManageAppAllFilesAccessPermission);
                intent.SetData(global::Android.Net.Uri.Parse("package:"+PackageName));
                StartActivity(intent);
            };
            btnAtual.Click += (s,e)=> Load();
            Load();
        }
        protected override void OnResume(){ base.OnResume(); Load(); }
        void Load()
        {
            list.RemoveAllViews();
            bool hasPerm = global::Android.OS.Environment.IsExternalStorageManager;
            var pt = new TextView(this){ Text="Perm: "+(hasPerm?"✅ SIM":"❌ NÃO")+" | Keys: "+(File.Exists(keysPath)?"✅":"❌")+" | #22 RENDER" };
            pt.SetTextColor(global::Android.Graphics.Color.ParseColor("#00ff88")); pt.SetPadding(10,10,10,20);
            list.AddView(pt);
            if(!hasPerm) return;
            var files = Directory.Exists(romPath)? Directory.GetFiles(romPath) : new string[0];
            var roms = files.Where(f=> f.EndsWith(".nsp")||f.EndsWith(".xci")).ToArray();
            foreach(var f in roms){
                var card = new LinearLayout(this){ Orientation=Orientation.Horizontal };
                card.SetPadding(30,30,30,30); card.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#1c1c1c"));
                var lp = new LinearLayout.LayoutParams(-1,-2); lp.SetMargins(0,0,0,18); card.LayoutParameters=lp;
                var name = new TextView(this){ Text=Path.GetFileName(f)+" | "+(new FileInfo(f).Length/1024/1024)+" MB" }; name.SetTextColor(global::Android.Graphics.Color.White); name.TextSize=10;
                var play = new Button(this){ Text="JOGAR VULKAN" };
                play.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#00c853")); play.SetTextColor(global::Android.Graphics.Color.White);
                string path = f;
                play.Click += (s,e)=>{
                    var intent = new global::Android.Content.Intent(this, typeof(GameActivity));
                    intent.PutExtra("rom", path); intent.PutExtra("keys", keysPath);
                    StartActivity(intent);
                };
                card.AddView(name, new LinearLayout.LayoutParams(0,-2,1f));
                card.AddView(play);
                list.AddView(card);
            }
        }
    }

    [Activity(Label="Zelda Vulkan", Theme="@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation=ScreenOrientation.Landscape)]
    public class GameActivity : Activity
    {
        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            var rom = Intent.GetStringExtra("rom")??"";
            var frame = new FrameLayout(this);

            // 1. Surface onde o Vulkan vai renderizar o Zelda
            var surface = new SurfaceView(this);
            surface.SetBackgroundColor(global::Android.Graphics.Color.Black);
            frame.AddView(surface, new FrameLayout.LayoutParams(-1,-1));

            // 2. Overlay de debug no topo
            var log = new TextView(this){
                Text="RYUBING #22 RENDER\n"+Path.GetFileName(rom)+"\nVulkan Adreno 650 | 6GB HostMappedUnsafe\nFPS: 0 | Mem: "+(GC.GetTotalMemory(false)/1024/1024)+"MB\n[Surface pronta pro Ryujinx Renderer]"
            };
            log.SetTextColor(global::Android.Graphics.Color.ParseColor("#00ff88"));
            log.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#66000000"));
            log.SetPadding(20,20,20,20); log.TextSize=9;
            var logP = new FrameLayout.LayoutParams(-1,-2); logP.Gravity=GravityFlags.Top;
            frame.AddView(log, logP);

            // 3. Controles - analógico esq
            var joyL = new Button(this){ Text="L\n●\n" }; joyL.Alpha=0.5f;
            joyL.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#333333")); joyL.SetTextColor(global::Android.Graphics.Color.White);
            var pL = new FrameLayout.LayoutParams(220,220); pL.Gravity=GravityFlags.Bottom|GravityFlags.Left; pL.SetMargins(30,0,30);
            frame.AddView(joyL, pL);

            // 4. Botões ABXY dir
            var btns = new LinearLayout(this){ Orientation=Orientation.Vertical };
            var bA = new Button(this){ Text="A" }; bA.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#00c853"));
            var bB = new Button(this){ Text="B" }; bB.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#e10600"));
            btns.AddView(bA); btns.AddView(bB);
            var pR = new FrameLayout.LayoutParams(180,-2); pR.Gravity=GravityFlags.Bottom|GravityFlags.Right; pR.SetMargins(0,0,30,30);
            frame.AddView(btns, pR);

            // 5. L R ZL ZR topo
            var topBar = new LinearLayout(this){ Orientation=Orientation.Horizontal };
            var bl = new Button(this){ Text="L" }; var br = new Button(this){ Text="R" }; bl.Alpha=0.6f; br.Alpha=0.6f;
            topBar.AddView(bl, new LinearLayout.LayoutParams(0,-2,1f)); topBar.AddView(br, new LinearLayout.LayoutParams(0,-2,1f));
            var pTop = new FrameLayout.LayoutParams(300,120); pTop.Gravity=GravityFlags.Top|GravityFlags.Right;
            frame.AddView(topBar, pTop);

            SetContentView(frame);
            Toast.MakeText(this,"#22 Surface Vulkan pronta! #23 linka Ryujinx HLE aqui", ToastLength.Long).Show();
        }
    }
}
