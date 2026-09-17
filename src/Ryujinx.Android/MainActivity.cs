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
            title.Text = "RYU + CORE - Zelda Ready";
            title.TextSize = 22;
            layout.AddView(title);

            var btn = new global::Android.Widget.Button(this);
            btn.Text = "🎮 RODAR ZELDA LINKS AWAKENING";
            layout.AddView(btn);

            var log = new global::Android.Widget.TextView(this);
            log.Text = "Aguardando...\n";
            layout.AddView(log);

            btn.Click += (s,e) => {
                string nsp = "/storage/emulated/0/Switch/Games/The Legend of Zelda Links Awakening.nsp";
                string keys = "/storage/emulated/0/Switch/prod.keys";
                
                if(!File.Exists(keys)) { log.Text = "FALTA: /Switch/prod.keys"; return; }
                if(!File.Exists(nsp)) { log.Text = $"FALTA: {nsp}"; return; }
                
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
            status.Text = $"Iniciando CORE...\n{GamePath}\n";
            status.SetTextColor(new global::Android.Graphics.Color(255,255,255));
            SetContentView(status);

            // Roda o core em thread separada
            new System.Threading.Thread(() => {
                try {
                    UpdateStatus("1/4 Lendo prod.keys...");
                    var keySet = KeySet.FromFile("/storage/emulated/0/Switch/prod.keys");
                    
                    UpdateStatus("2/4 Criando VFS...");
                    var vfs = VirtualFileSystem.Create();
                    
                    UpdateStatus("3/4 Inicializando Switch Device...");
                    var device = new Switch(vfs, keySet, null, null);
                    // O Ryujinx precisa de um renderer, no Android usa Vulkan
                    // var gpu = new Ryujinx.Graphics.Vulkan.VulkanRenderer(...)
                    
                    UpdateStatus($"4/4 Carregando NSP...\n{GamePath}");
                    device.LoadApplication(GamePath);

                    UpdateStatus("RODANDO! Se chegou aqui, o jogo iniciou o boot!");
                    // device.Run() - aqui que inicia o loop do jogo
                }
                catch(Exception ex) {
                    UpdateStatus($"ERRO NO CORE:\n{ex.Message}\n\n{ex.StackTrace}");
                }
            }).Start();
        }

        void UpdateStatus(string msg) {
            RunOnUiThread(() => {
                status.Text += "\n" + msg;
            });
        }
    }
}
