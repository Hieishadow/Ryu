using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Android.Views;
using Android.Graphics;
using System.IO;
using System.Linq;

namespace DragoNX;

[Activity(Label = "DragoNX", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
public class MainActivity : Activity
{
    const string BasePath = "/storage/emulated/0/Download/DragoNX";
    const string GamesPath = BasePath + "/games";
    const string SystemPath = BasePath + "/system";
    const string JitPath = BasePath + "/cache/jit";

    LinearLayout layout = null!;
    TextView status = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        Directory.CreateDirectory(GamesPath);
        Directory.CreateDirectory(SystemPath);
        Directory.CreateDirectory(JitPath);

        layout = new LinearLayout(this);
        layout.Orientation = Orientation.Vertical;
        layout.SetGravity(GravityFlags.Center);
        layout.SetBackgroundColor(Color.Black);
        layout.SetPadding(40, 40, 40, 40);

        var title = new TextView(this);
        title.Text = "DragoNX - Teste Debug";
        title.SetTextColor(Color.White);
        title.TextSize = 22;
        title.Gravity = GravityFlags.Center;
        layout.AddView(title);

        status = new TextView(this);
        status.SetTextColor(Color.Gray);
        status.TextSize = 14;
        status.Gravity = GravityFlags.Center;
        status.Text = $"Games: {GamesPath}\nJIT: {JitPath}";
        layout.AddView(status);

        var btnPerm = new Button(this);
        btnPerm.Text = "1 - Dar Permissão de Arquivos";
        btnPerm.Click += (s, e) => RequestAllFilesPermission();
        layout.AddView(btnPerm);

        var btnScan = new Button(this);
        btnScan.Text = "2 - Procurar Zelda Links Awakening";
        btnScan.Click += (s, e) => ScanGames();
        layout.AddView(btnScan);

        SetContentView(layout);

        // Auto pede permissão se não tiver
        if (!HasAllFilesPermission())
        {
            RequestAllFilesPermission();
        }
    }

    bool HasAllFilesPermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            return Android.OS.Environment.IsExternalStorageManager;
        }
        return true;
    }

    void RequestAllFilesPermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            try
            {
                var intent = new Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                intent.SetData(Android.Net.Uri.Parse("package:" + PackageName));
                StartActivity(intent);
                Toast.MakeText(this, "Ative: Permitir acesso a todos os arquivos", ToastLength.Long)?.Show();
            }
            catch
            {
                var intent = new Intent(Android.Provider.Settings.ActionManageAllFilesAccessPermission);
                StartActivity(intent);
            }
        }
    }

    void ScanGames()
    {
        layout.RemoveAllViews();
        
        var title = new TextView(this);
        title.Text = "Jogos encontrados:";
        title.SetTextColor(Color.White);
        title.TextSize = 20;
        title.Gravity = GravityFlags.Center;
        layout.AddView(title);

        // CORRIGIDO: Usa Java.IO.File pra não dar ambiguidade no build 36MB
        var dir = new Java.IO.File(GamesPath);
        var files = dir.ListFiles();

        if (files == null || files.Length == 0)
        {
            var empty = new TextView(this);
            empty.SetTextColor(Color.Red);
            empty.Text = $"\nNenhum jogo em:\n{GamesPath}\n\nColoque o Links Awakening.nsp lá e clica de novo.";
            empty.Gravity = GravityFlags.Center;
            layout.AddView(empty);

            var back = new Button(this);
            back.Text = "Voltar";
            back.Click += (s,e) => Recreate();
            layout.AddView(back);
            return;
        }

        foreach (var file in files.Where(f =>!f.IsDirectory))
        {
            var btn = new Button(this);
            btn.Text = file.Name;
            btn.SetBackgroundColor(Color.DarkGray);
            var path = file.AbsolutePath;
            btn.Click += (s, e) =>
            {
                var intent = new Intent(this, typeof(GameActivity));
                intent.PutExtra("rom_path", path);
                StartActivity(intent);
            };
            layout.AddView(btn);
        }
    }
}
