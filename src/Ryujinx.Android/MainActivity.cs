using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Android.Views;
using System.Linq;
// FIX DO BUILD - evita ambiguidade com Android.Graphics.Path
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;
using FileInfo = System.IO.FileInfo;

namespace DragoNX;

[Activity(Name = "com.ryubing.android.MainActivity", Label = "Ryubing", MainLauncher = true, Exported = true, ScreenOrientation = ScreenOrientation.Landscape, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
public class MainActivity : Activity
{
    const string BasePath = "/storage/emulated/0/Download/Ryubing";
    const string GamesPath = BasePath + "/games";
    const string KeysPath = BasePath + "/keys";
    LinearLayout layout = null!;
    string? selectedRom = null;
    Button? btnJogar;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
        Directory.CreateDirectory(GamesPath);
        Directory.CreateDirectory(KeysPath);

        try {
            var internalSystem = Path.Combine(FilesDir!.AbsolutePath, "Ryujinx", "system");
            Directory.CreateDirectory(internalSystem);
            var srcKey = Path.Combine(KeysPath, "prod.keys");
            var dstKey = Path.Combine(internalSystem, "prod.keys");
            if (File.Exists(srcKey)) File.Copy(srcKey, dstKey, true);
        } catch {}

        layout = new LinearLayout(this);
        layout.Orientation = Orientation.Vertical;
        layout.SetGravity(GravityFlags.Center);
        layout.SetBackgroundColor(Android.Graphics.Color.Black);
        layout.SetPadding(40,20,40,20);
        var title = new TextView(this){ Text = "Ryubing - S20 FE Edition" };
        title.SetTextColor(Android.Graphics.Color.White); title.TextSize = 20; title.Gravity = GravityFlags.Center;
        layout.AddView(title);
        string prodFile = KeysPath + "/prod.keys";
        bool prodOk = File.Exists(prodFile);
        var info = new TextView(this);
        info.Gravity = GravityFlags.Center; info.TextSize = 11f;
        info.Text = prodOk ? $"✓ prod.keys {new FileInfo(prodFile).Length} bytes" : $"✗ prod.keys NAO em {KeysPath}";
        info.SetTextColor(prodOk ? Android.Graphics.Color.Green : Android.Graphics.Color.Yellow);
        layout.AddView(info);
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
        btnJogar.SetBackgroundColor(Android.Graphics.Color.Green);
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
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R && !global::Android.OS.Environment.IsExternalStorageManager) RequestAllFilesPermission();
        else ScanGames();
    }
    void RequestAllFilesPermission()
    {
        try {
            var intent = new Intent(global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
            intent.SetData(global::Android.Net.Uri.Parse("package:" + PackageName));
            StartActivity(intent);
        } catch {
            StartActivity(new Intent(global::Android.Provider.Settings.ActionManageAllFilesAccessPermission));
        }
    }
    void ScanGames()
    {
        if(layout.ChildCount > 3) layout.RemoveViews(3, layout.ChildCount - 3);
        var container = new LinearLayout(this);
        container.Orientation = Orientation.Horizontal;
        container.SetGravity(GravityFlags.Center);
        var dir = new Java.IO.File(GamesPath);
        var files = dir.ListFiles();
        if (files == null || files.Length == 0) {
            var empty = new TextView(this){ Text = $"Nenhum jogo em {GamesPath}" };
            empty.SetTextColor(Android.Graphics.Color.Red); empty.Gravity = GravityFlags.Center;
            layout.AddView(empty);
            return;
        }
        foreach (var file in files.Where(f => !f.IsDirectory && (f.Name.EndsWith(".nsp") || f.Name.EndsWith(".xci")))) {
            var col = new LinearLayout(this);
            col.Orientation = Orientation.Vertical;
            col.SetPadding(10,10,10,10);
            col.SetBackgroundColor(Android.Graphics.Color.DarkGray);
            var name = new TextView(this){ Text = file.Name };
            name.SetTextColor(Android.Graphics.Color.White); name.TextSize = 10;
            col.AddView(name);
            var btn = new Button(this){ Text = "Selecionar" };
            btn.Click += (s,e) => {
                selectedRom = file.AbsolutePath;
                btnJogar!.Enabled = true;
                btnJogar.Text = $"▶ JOGAR {file.Name}";
            };
            col.AddView(btn);
            container.AddView(col);
        }
        layout.AddView(container);
    }
}
