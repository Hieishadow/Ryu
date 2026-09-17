using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using Android.Content;
using System;
using System.IO;
using System.Linq;

namespace Ryujinx.Android
{
    [Activity(Label = "Ryu", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar")]
    public class MainActivity : Activity
    {
        TextView logView;
        string gamesPath = "/storage/emulated/0/Switch/Games";

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            var layout = new LinearLayout(this);
            layout.Orientation = Orientation.Vertical;
            layout.SetPadding(40, 80, 40, 40);
            layout.SetBackgroundColor(Android.Graphics.Color.Rgb(15, 15, 15));

            var title = new TextView(this);
            title.Text = "Ryu - Build 142 OK ✓";
            title.SetTextSize(Android.Util.ComplexUnitType.Sp, 22);
            title.SetTextColor(Android.Graphics.Color.White);
            layout.AddView(title);

            var subtitle = new TextView(this);
            subtitle.Text = "Pipeline compilando! Agora vamos adicionar a UI";
            subtitle.SetTextColor(Android.Graphics.Color.Gray);
            layout.AddView(subtitle);

            var btnGames = new Button(this);
            btnGames.Text = "📁 SELECIONAR PASTA DE JOGOS";
            btnGames.SetBackgroundColor(Android.Graphics.Color.Rgb(0, 120, 215));
            layout.AddView(btnGames);

            var btnKeys = new Button(this);
            btnKeys.Text = "🔑 IMPORTAR prod.keys";
            btnKeys.SetBackgroundColor(Android.Graphics.Color.Rgb(0, 180, 90));
            layout.AddView(btnKeys);

            logView = new TextView(this);
            logView.Text = $"\nLog:\n- APK instalado com sucesso\n- Path: {gamesPath}\n- Aguardando keys...\n";
            logView.SetTextColor(Android.Graphics.Color.White);
            logView.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
            layout.AddView(logView);

            var btnList = new Button(this);
            btnList.Text = "🎮 LISTAR JOGOS (TESTE)";
            layout.AddView(btnList);

            btnGames.Click += (s, e) => {
                try {
                    var intent = new Intent(Intent.ActionOpenDocumentTree);
                    StartActivityForResult(intent, 1);
                } catch (Exception ex) {
                    logView.Text += $"\nErro: {ex.Message}";
                }
            };

            btnList.Click += (s, e) => {
                try {
                    if (Directory.Exists(gamesPath)) {
                        var files = Directory.GetFiles(gamesPath, "*.nsp").Concat(Directory.GetFiles(gamesPath, "*.xci")).ToArray();
                        logView.Text += $"\nEncontrados {files.Length} jogos em {gamesPath}\n";
                        foreach(var f in files.Take(5)) logView.Text += $"- {Path.GetFileName(f)}\n";
                    } else {
                        logView.Text += $"\nPasta não existe: {gamesPath}\nCrie /Switch/Games no celular";
                    }
                } catch (Exception ex) {
                    logView.Text += $"\nErro lista: {ex.Message}";
                }
            };

            btnKeys.Click += (s, e) => {
                logView.Text += "\n> Coloque prod.keys em /storage/emulated/0/Switch/prod.keys\n";
                Toast.MakeText(this, "Coloque prod.keys na pasta Switch", ToastLength.Long).Show();
            };

            SetContentView(layout);
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            if (data != null) {
                gamesPath = data.Data.ToString();
                logView.Text += $"\nPasta selecionada: {gamesPath}\n";
            }
        }
    }
}
