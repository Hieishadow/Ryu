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
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            var layout = new global::Android.Widget.LinearLayout(this);
            layout.Orientation = global::Android.Widget.Orientation.Vertical;
            layout.SetPadding(40,60,40,40);

            var title = new global::Android.Widget.TextView(this);
            title.Text = "RYU HARDCORE #148 - Zelda";
            title.TextSize = 20;
            layout.AddView(title);

            var btn = new global::Android.Widget.Button(this);
            btn.Text = "RODAR ZELDA REAL";
            layout.AddView(btn);

            var log = new global::Android.Widget.TextView(this);
            log.Text = "Aguardando...\n";
            layout.AddView(log);

            btn.Click += (s,e) => {
                string nsp = "/storage/emulated/0/Switch/Games/The Legend of Zelda Links Awakening.nsp";
                string keys = "/storage/emulated/0/Switch/prod.keys";
                if(!File.Exists(keys)) { log.Text = "FALTA prod.keys"; return; }
                if(!File.Exists(nsp)) { log.Text = "FALTA " + nsp; return; }
                GameActivity.GamePath = nsp;
                StartActivity(new global::Android.Content.Intent(this, typeof(GameActivity)));
            };
            SetContentView(layout);
        }
    }

    [Activity(Label = "Game", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
    public class GameActivity : Activity
    {
        public static string GamePath;
        TextView status;

        protected override void OnCreate(Bundle b)
        {
            base.OnCreate(b);
            Window.AddFlags(global::Android.Views.WindowManagerFlags.KeepScreenOn | global::Android.Views.WindowManagerFlags.Fullscreen);
            status = new global::Android.Widget.TextView(this);
            status.Text = "HARDCORE TEST\n" + GamePath + "\n";
            SetContentView(status);

            new System.Threading.Thread(() => {
                try {
                    Update("1/3 Verificando arquivos reais...");
                    var nspInfo = new FileInfo(GamePath);
                    var keyInfo = new FileInfo("/storage/emulated/0/Switch/prod.keys");
                    Update("NSP: " + (nspInfo.Length / 1024 / 1024) + " MB OK");
                    Update("KEYS: " + keyInfo.Length + " bytes OK");

                    Update("2/3 Criando VFS...");
                    var vfs = new VirtualFileSystem();
                    Update("VFS criado!");

                    Update("3/3 Criando Switch Device...");
                    var device = new Switch(vfs, null, 0, 0);
                    Update("Device criado: " + device.GetType().Name);

                    using(var fs = new FileStream(GamePath, FileMode.Open, FileAccess.Read)){
                        byte[] header = new byte[4];
                        fs.Read(header, 0, 4);
                        string magic = System.Text.Encoding.ASCII.GetString(header);
                        Update("MAGIC: " + magic);
                        bool ok = magic == "PFS0";
                        if(ok) {
                            Update("NSP VALIDO - PFS0 OK");
                        } else {
                            Update("Arquivo invalido, magic diferente de PFS0");
                        }
                    }

                    Update("TESTE REAL PASSOU! Proximo passo Vulkan");
                }
                catch(Exception ex){
                    Update("ERRO: " + ex.Message + "\n" + ex.StackTrace);
                }
            }).Start();
        }

        void Update(string msg){
            RunOnUiThread(() => { status.Text += "\n" + msg; });
        }
    }
}
