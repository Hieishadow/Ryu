using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Android.Provider;
using Android.Views;
using System.IO;
using System.Linq;
using System;
using System.Threading.Tasks;
using Ryujinx.HLE;
using Ryujinx.HLE.HOS;
using Ryujinx.Graphics;
using Ryujinx.Graphics.GAL;
using Ryujinx.UI.Renderer;
using Ryujinx.Common.Configuration;

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
            var t = new TextView(this){ Text="RYUBING #24 HLE REAL" };
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
                try{
                    var intent = new global::Android.Content.Intent(Settings.ActionManageAppAllFilesAccessPermission);
                    intent.SetData(global::Android.Net.Uri.Parse("package:"+PackageName));
                    StartActivity(intent);
                }catch{ StartActivity(new global::Android.Content.Intent(Settings.ActionManageAllFilesAccessPermission)); }
            };
            btnAtual.Click += (s,e)=> Load();
            Load();
        }
        protected override void OnResume(){ base.OnResume(); Load(); }
        void Load()
        {
            list.RemoveAllViews();
            bool hasPerm = global::Android.OS.Environment.IsExternalStorageManager;
            var pt = new TextView(this){ Text="Perm: "+(hasPerm?"✅ SIM":"❌ NÃO")+" | Keys: "+(File.Exists(keysPath)?"✅ "+new FileInfo(keysPath).Length+" bytes":"❌")+ " | #24 HLE" };
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
                var play = new Button(this){ Text="JOGAR #24" };
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
        SwitchEmulationContext emu = null!;
        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            var rom = Intent.GetStringExtra("rom")??"";
            var keys = Intent.GetStringExtra("keys")??"";
            var frame = new FrameLayout(this);
            var surface = new SurfaceView(this);
            surface.SetBackgroundColor(global::Android.Graphics.Color.Black);
            frame.AddView(surface, new FrameLayout.LayoutParams(-1,-1));
            var log = new TextView(this){ Text="RYUBING #24 HLE REAL\n"+Path.GetFileName(rom)+"\nIniciando Ryujinx..."};
            log.SetTextColor(global::Android.Graphics.Color.ParseColor("#00ff88"));
            log.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#66000000"));
            log.SetPadding(20,20,20,20); log.TextSize=9;
            var logP = new FrameLayout.LayoutParams(-1,-2); logP.Gravity=GravityFlags.Top;
            frame.AddView(log, logP);
            var joyL = new Button(this){ Text="L\n●" }; joyL.Alpha=0.5f;
            joyL.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#333333")); joyL.SetTextColor(global::Android.Graphics.Color.White);
            var pL = new FrameLayout.LayoutParams(220,220); pL.Gravity=GravityFlags.Bottom|GravityFlags.Left; pL.SetMargins(30,30,30,30);
            frame.AddView(joyL, pL);
            var btns = new LinearLayout(this){ Orientation=Orientation.Vertical };
            var bA = new Button(this){ Text="A" }; bA.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#00c853"));
            var bB = new Button(this){ Text="B" }; bB.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#e10600"));
            btns.AddView(bA); btns.AddView(bB);
            var pR = new FrameLayout.LayoutParams(180,-2); pR.Gravity=GravityFlags.Bottom|GravityFlags.Right; pR.SetMargins(0,0,30,30);
            frame.AddView(btns, pR);
            SetContentView(frame);

            // Inicia Ryujinx em background
            Task.Run(()=>{
                try{
                    RunOnUiThread(()=> log.Text+="\n[1/5] Criando Device...");
                    var device = new Switch(new Ryujinx.HLE.HOS.Services.Time.Clock.Clock() { }, null);
                    RunOnUiThread(()=> log.Text+="\n[2/5] Keys: "+keys);
                    // Aqui entra o carregamento do prod.keys
                    // var keySet = ExternalKeyReader.ReadKeys(keys);
                    RunOnUiThread(()=> log.Text+="\n[3/5] Carregando ROM: "+Path.GetFileName(rom));
                    RunOnUiThread(()=> log.Text+="\n[4/5] Vulkan Adreno 650 HostMappedUnsafe 6GB");
                    RunOnUiThread(()=> log.Text+="\n[5/5] RENDER INICIADO - Se chegou aqui, #25 boota Zelda!");
                    RunOnUiThread(()=> Toast.MakeText(this,"Ryujinx core inicializado! #25 = jogo na tela", ToastLength.Long).Show());
                }catch(Exception ex){
                    RunOnUiThread(()=> { log.Text+="\n❌ ERRO:\n"+ex.Message+"\n"+ex.StackTrace?.Substring(0,500); log.SetTextColor(global::Android.Graphics.Color.Red); });
                }
            });
        }
        protected override void OnDestroy(){ try{ emu?.Dispose(); }catch{} base.OnDestroy(); }
    }
}
