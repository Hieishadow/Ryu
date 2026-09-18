using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Android.Views;
using System;
using System.IO;
using System.Linq;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace DragoNX;

[Activity(
    Name = "com.ryubing.android.MainActivity",
    Label = "Ryubing",
    Exported = true,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public class MainActivity : Activity
{
    const string BasePath = "/storage/emulated/0/Download/Ryubing";
    const string GamesPath = BasePath + "/games";
    const string KeysPath = BasePath + "/keys";
    const string FirmwarePath = BasePath + "/firmware";

    static readonly string[] IgnoredDirs = { ".thumbnails", "System Volume Information", ".trashed", "LOST.DIR", "Android" };

    LinearLayout layout = null!;
    string? selectedRom = null;
    Button? btnJogar;
    bool permissionRequested = false;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        layout = new LinearLayout(this);
        layout.Orientation = Orientation.Vertical;
        layout.SetGravity(GravityFlags.Center);
        layout.SetBackgroundColor(Android.Graphics.Color.Black);
        layout.SetPadding(40, 20, 40, 20);

        var title = new TextView(this) { Text = "Ryubing - S20 FE Edition" };
        title.SetTextColor(Android.Graphics.Color.White);
        title.TextSize = 20;
        title.Gravity = GravityFlags.Center;
        layout.AddView(title);

        var info = new TextView(this);
        info.Gravity = GravityFlags.Center;
        info.TextSize = 11f;
        layout.AddView(info);

        var topRow = new LinearLayout(this);
        topRow.Orientation = Orientation.Horizontal;
        topRow.SetGravity(GravityFlags.Center);

        var btnPerm = new Button(this) { Text = "1 - Permissão" };
        btnPerm.Click += (s, e) => RequestAllFilesPermission();
        topRow.AddView(btnPerm);

        var btnScan = new Button(this) { Text = "2 - Listar Jogos" };
        btnScan.Click += (s, e) => ScanGames();
        topRow.AddView(btnScan);

        btnJogar = new Button(this) { Text = "▶ JOGAR" };
        btnJogar.SetBackgroundColor(Android.Graphics.Color.Green);
        btnJogar.Enabled = false;
        btnJogar.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(selectedRom))
            {
                var intent = new Intent(this, typeof(GameActivity));
                intent.PutExtra("rom_path", selectedRom);
                StartActivity(intent);
            }
        };
        topRow.AddView(btnJogar);

        layout.AddView(topRow);
        SetContentView(layout);
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (HasAllFilesPermission())
        {
            EnsureDirectories();
            UpdateInfo();
            ScanGames();
        }
        else if (!permissionRequested)
        {
            permissionRequested = true;
            RequestAllFilesPermission();
        }
        else
        {
            UpdateInfo();
        }
    }

    bool HasAllFilesPermission()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.R) return true;
        return global::Android.OS.Environment.IsExternalStorageManager;
    }

    void EnsureDirectories()
    {
        try
        {
            Directory.CreateDirectory(GamesPath);
            Directory.CreateDirectory(KeysPath);
            Directory.CreateDirectory(FirmwarePath);
            
            if (FilesDir == null) return;
            var internalSystem = Path.Combine(FilesDir.AbsolutePath, "Ryujinx", "system");
            Directory.CreateDirectory(internalSystem);

            foreach (var k in new[] { "prod.keys", "title.keys" })
            {
                var src = Path.Combine(KeysPath, k);
                var dst = Path.Combine(internalSystem, k);
                if (File.Exists(src)) 
                {
                    File.Copy(src, dst, true);
                    Android.Util.Log.Info("Ryubing", $"Key copiada: {k}");
                }
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("Ryubing", "EnsureDirectories: " + ex.Message);
        }
    }

    void UpdateInfo()
    {
        if (layout.ChildCount < 2) return;
        if (layout.GetChildAt(1) is not TextView info) return;
        var prod = new FileInfo(Path.Combine(KeysPath, "prod.keys"));
        var title = new FileInfo(Path.Combine(KeysPath, "title.keys"));
        if (prod.Exists)
        {
            info.Text = $"✓ prod.keys {prod.Length} bytes | {(title.Exists ? $"title.keys {title.Length} bytes" : "title.keys FALTANDO")}";
            info.SetTextColor(title.Exists ? Android.Graphics.Color.Green : Android.Graphics.Color.Yellow);
        }
        else
        {
            info.Text = $"✗ keys NAO encontradas em {KeysPath}";
            info.SetTextColor(Android.Graphics.Color.Red);
        }
    }

    void RequestAllFilesPermission()
    {
        try
        {
            var intent = new Intent(global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
            intent.SetData(global::Android.Net.Uri.Parse("package:" + PackageName));
            StartActivity(intent);
        }
        catch
        {
            StartActivity(new Intent(global::Android.Provider.Settings.ActionManageAllFilesAccessPermission));
        }
    }

    void ScanGames()
    {
        if (!HasAllFilesPermission())
        {
            Toast.MakeText(this, "Concede a permissão primeiro", ToastLength.Long)?.Show();
            return;
        }

        while (layout.ChildCount > 3)
            layout.RemoveViewAt(layout.ChildCount - 1);

        selectedRom = null;
        if (btnJogar != null)
        {
            btnJogar.Enabled = false;
            btnJogar.Text = "▶ JOGAR";
        }

        var container = new LinearLayout(this);
        container.Orientation = Orientation.Vertical;
        container.SetGravity(GravityFlags.Center);

        try
        {
            var allFiles = Directory.EnumerateFiles(GamesPath, "*.*", SearchOption.AllDirectories)
                .Where(f => !IgnoredDirs.Any(ig => f.Contains(ig, StringComparison.OrdinalIgnoreCase)))
                .Where(f => f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
                .Take(100).ToList();

            if (allFiles.Count == 0)
            {
                var empty = new TextView(this) { Text = $"Nenhum jogo em {GamesPath} (recursivo)" };
                empty.SetTextColor(Android.Graphics.Color.Red);
                empty.Gravity = GravityFlags.Center;
                layout.AddView(empty);
                return;
            }

            foreach (var romPath in allFiles)
            {
                var row = new LinearLayout(this);
                row.Orientation = Orientation.Horizontal;
                row.SetGravity(GravityFlags.CenterVertical);
                row.SetPadding(10, 8, 10, 8);

                var name = new TextView(this) { Text = Path.GetFileName(romPath) + $" [{Path.GetFileName(Path.GetDirectoryName(romPath))}]" };
                name.SetTextColor(Android.Graphics.Color.White);
                name.TextSize = 11;
                name.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
                row.AddView(name);

                var btn = new Button(this) { Text = "Selecionar" };
                btn.Click += (s, e) =>
                {
                    selectedRom = romPath;
                    if (btnJogar != null)
                    {
                        btnJogar.Enabled = true;
                        btnJogar.Text = $"▶ JOGAR {Path.GetFileName(romPath)}";
                    }
                };
                row.AddView(btn);
                container.AddView(row);
            }
        }
        catch (Exception ex)
        {
            var err = new TextView(this) { Text = "Erro scan: " + ex.Message };
            err.SetTextColor(Android.Graphics.Color.Red);
            container.AddView(err);
        }

        layout.AddView(container);
    }
}
