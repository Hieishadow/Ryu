using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Android.Views;
using Android.Graphics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DragoNX;

[Activity(Label = "DragoNX", MainLauncher = true, Exported = true, ScreenOrientation = ScreenOrientation.Landscape, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
public class MainActivity : Activity
{
    const string BasePath = "/storage/emulated/0/Download/DragoNX";
    const string GamesPath = BasePath + "/games";
    const string KeysPath = BasePath + "/keys";
    const string FirmwarePath = BasePath + "/firmware";
    const string JitPath = BasePath + "/cache/jit";

    LinearLayout layout = null!;
    string? selectedRom = null;
    Button? btnJogar;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => {
            RunOnUiThread(() => {
                var tv = new TextView(this){ Text = "CRASH:\n" + e.ExceptionObject.ToString() };
                tv.SetTextColor(Color.Yellow); tv.SetBackgroundColor(Color.Black);
                SetContentView(tv);
            });
        };

        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        Directory.CreateDirectory(GamesPath);
        Directory.CreateDirectory(KeysPath);
        Directory.CreateDirectory(FirmwarePath);
        Directory.CreateDirectory(JitPath);

        layout = new LinearLayout(this);
        layout.Orientation = Orientation.Vertical;
        layout.SetGravity(GravityFlags.Center);
        layout.SetBackgroundColor(Color.Black);
        layout.SetPadding(40,20,40,20);

        var title = new TextView(this){ Text = "DragoNX - Horizontal" };
        title.SetTextColor(Color.White); title.TextSize = 20; title.Gravity = GravityFlags.Center;
        layout.AddView(title);

        // INFO PROD + FIRMWARE
        string prodFile = KeysPath + "/prod.keys";
        bool prodOk = File.Exists(prodFile);
        bool firmOk = Directory.Exists(FirmwarePath);
        var info = new TextView(this);
        info.Gravity = GravityFlags.Center; info.TextSize = 11f;
        info.Text = (prodOk ? $"✓ prod.keys {new FileInfo(prodFile).Length} bytes | " : $"✗ prod.keys NAO | ") + (firmOk ? $"✓ firmware {Directory.GetFiles(FirmwarePath, "*", SearchOption.AllDirectories).Length} arq" : "✗ firmware NAO");
        info.SetTextColor(prodOk && firmOk ? Color.Green : Color.Yellow);
        layout.AddView(info);

        // BOTOES TOPO HORIZONTAL
        var topRow = new LinearLayout(this);
        topRow.Orientation = Orientation.Horizontal;
        topRow.SetGravity(GravityFlags.Center);

        var btnPerm = new Button(this){ Text = "1 - Permissão" };
        btnPerm.Click += (s,e) => RequestAllFilesPermission();
        topRow.AddView(btnPerm);

        var btnScan = new Button(this){ Text = "2 - Listar Jogos" };
        btnScan.Click += (s,e) => ScanGames();
        topRow.AddView(btnScan);

        btnJogar = new Button(this){ Text = "▶ JOGAR" };
        btnJogar.SetBackgroundColor(Color.Green);
        btnJogar.Enabled = false;
        btnJogar.Click += (s,e) => {
            if(selectedRom != null){
                var intent = new Intent(this, typeof(GameActivity));
                intent.PutExtra("rom_path", selectedRom);
                StartActivity(intent);
            }
        };
        topRow.AddView(btnJogar);
        layout.AddView(topRow);

        SetContentView(layout);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R && !Android.OS.Environment.IsExternalStorageManager) RequestAllFilesPermission();
        else ScanGames();
    }

    void RequestAllFilesPermission()
    {
        try {
            var intent = new Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
            intent.SetData(Android.Net.Uri.Parse("package:" + PackageName));
            StartActivity(intent);
        } catch {
            StartActivity(new Intent(Android.Provider.Settings.ActionManageAllFilesAccessPermission));
        }
    }

    void ScanGames()
    {
        // LIMPA SO A LISTA
        if(layout.ChildCount > 3) layout.RemoveViews(3, layout.ChildCount - 3);

        var container = new LinearLayout(this);
        container.Orientation = Orientation.Horizontal;
        container.SetGravity(GravityFlags.Center);
        container.SetPadding(0,20,0,0);

        var dir = new Java.IO.File(GamesPath);
        var files = dir.ListFiles();
        if (files == null || files.Length == 0) {
            var empty = new TextView(this){ Text = $"Nenhum jogo em {GamesPath}" };
            empty.SetTextColor(Color.Red); empty.Gravity = GravityFlags.Center;
            layout.AddView(empty);
            return;
        }

        foreach (var file in files.Where(f => !f.IsDirectory)) {
            var col = new LinearLayout(this);
            col.Orientation = Orientation.Vertical;
            col.SetPadding(10,10,10,10);
            col.SetBackgroundColor(Color.DarkGray);

            var name = new TextView(this){ Text = file.Name };
            name.SetTextColor(Color.White); name.TextSize = 10; name.Gravity = GravityFlags.Center;
            col.AddView(name);

            var btn = new Button(this){ Text = "Selecionar" };
            btn.Click += (s,e) => {
                selectedRom = file.AbsolutePath;
                btnJogar!.Enabled = true;
                btnJogar.Text = $"▶ JOGAR {file.Name}";
                Toast.MakeText(this, $"Selecionado: {file.Name}", ToastLength.Short)?.Show();
            };
            col.AddView(btn);
            container.AddView(col);
        }
        layout.AddView(container);
    }
}
