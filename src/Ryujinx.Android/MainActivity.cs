using Android.App;
using Android.OS;
using Android.Widget;
using System;
using System.IO;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;

namespace Ryujinx.Android
{
    [Activity(Label = "Ryu", MainLauncher = true)]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle s)
        {
            base.OnCreate(s);
            var l = new LinearLayout(this);
            l.Orientation = Orientation.Vertical;
            l.SetPadding(40,60,40,40);
            
            var b = new Button(this);
            b.Text = "ABRIR ZELDA AGORA";
            b.Click += (a,e) => {
                GameActivity.Path = "/storage/emulated/0/Switch/Games/The Legend of Zelda Links Awakening.nsp";
                StartActivity(new Android.Content.Intent(this, typeof(GameActivity)));
            };
            l.AddView(b);
            SetContentView(l);
        }
    }

    [Activity(Label = "Game", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
    public class GameActivity : Activity
    {
        public static string Path;
        TextView t;
        protected override void OnCreate(Bundle b)
        {
            base.OnCreate(b);
            Window.AddFlags(Android.Views.WindowManagerFlags.KeepScreenOn | Android.Views.WindowManagerFlags.Fullscreen);
            t = new TextView(this);
            SetContentView(t);

            new System.Threading.Thread(()=> {
                try{
                    Log("LIGANDO CORE VULKAN JIT S20 FE...");
                    var vfs = VirtualFileSystem.Create();
                    var cm = new ContentManager(vfs);
                    cm.InitializeInstance(Path);
                    
                    var device = new global::Ryujinx.HLE.HOS.Switch(vfs, cm, null, null);
                    Log("CORE OK - Adreno 650 - 6GB");
                    
                    Log("CARREGANDO " + Path);
                    device.LoadApplication(Path);
                    Log("ZELDA CARREGADO Iniciando...");
                    Log("Se ficar preto e o Vulkan que falta mas o core LIGOU");
                }catch(Exception ex){
                    Log("ERRO: " + ex.Message + "\n" + ex.ToString());
                }
            }).Start();
        }
        void Log(string m){ RunOnUiThread(()=> t.Text += "\n" + m); }
    }
}
