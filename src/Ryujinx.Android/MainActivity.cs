#nullable disable
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Android.Views;
using Android.Provider;
using Android.Runtime;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace Ryujinx.Android;

[Activity(
    Name = "com.ryubing.android.MainActivity",
    Label = "Ryubing",
    Exported = true,
    MainLauncher = true,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public class MainActivity : Activity
{
    public const string BasePath = "/storage/emulated/0/Download/Ryubing";
    const string GamesPath = BasePath + "/games";
    const string KeysPath = BasePath + "/keys";
    const string FirmwarePath = BasePath + "/firmware";
    static readonly string[] IgnoredDirs = { ".thumbnails", "System Volume Information", ".trashed", "LOST.DIR", "Android" };

    LinearLayout layout;
    string selectedRom = "";
    Button btnJogar;
    bool permissionRequested = false;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        try {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                try { File.WriteAllText(BasePath + "/crash.txt", $"CRASH {DateTime.Now}\n{e.ExceptionObject}\n"); } catch {}
            };
        } catch {}

        base.OnCreate(savedInstanceState);
        if(Window!=null) Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
        layout = new LinearLayout(this);
        layout.Orientation = Orientation.Vertical;
        layout.SetGravity(GravityFlags.Center);
        layout.SetBackgroundColor(global::Android.Graphics.Color.Black);
        layout.SetPadding(40, 20, 40, 20);
        var title = new TextView(this) { Text = "Ryubing - S20 FE Edition #529" };
        title.SetTextColor(global::Android.Graphics.Color.White);
        title.TextSize = 20; title.Gravity = GravityFlags.Center;
        layout.AddView(title);
        var info = new TextView(this){ Gravity = GravityFlags.Center, TextSize = 11f };
        layout.AddView(info);
        var topRow = new LinearLayout(this){ Orientation = Orientation.Horizontal };
        topRow.SetGravity(GravityFlags.Center);
        var btnPerm = new Button(this) { Text = "1 - Permissao" };
        btnPerm.Click += (s, e) => RequestAllFilesPermission();
        topRow.AddView(btnPerm);
        var btnScan = new Button(this) { Text = "2 - Listar Jogos" };
        btnScan.Click += (s, e) => ScanGames();
        topRow.AddView(btnScan);
        var btnImport = new Button(this) { Text = "Importar Keys" };
        btnImport.SetBackgroundColor(global::Android.Graphics.Color.Yellow);
        btnImport.Click += (s,e)=>{
            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
            StartActivityForResult(intent, 1001);
        };
        topRow.AddView(btnImport);
        var btnClear = new Button(this) { Text = "Limpar Log" };
        btnClear.Click += (s,e)=>{ try{ if(File.Exists(BasePath+"/ryubing_log.txt")) File.Delete(BasePath+"/ryubing_log.txt"); Toast.MakeText(this,"Logs limpos",ToastLength.Short).Show(); UpdateInfo(); }catch{} };
        topRow.AddView(btnClear);
        btnJogar = new Button(this) { Text = "JOGAR" };
        btnJogar.SetBackgroundColor(global::Android.Graphics.Color.Green);
        btnJogar.Enabled = false;
        btnJogar.Click += (s, e) =>
        {
            if(string.IsNullOrEmpty(selectedRom) || !File.Exists(selectedRom)){
                Toast.MakeText(this, "ROM nao encontrada", ToastLength.Short).Show(); return;
            }
            var intentGame = new Intent(this, typeof(global::Ryujinx.Android.GameActivity));
            intentGame.PutExtra("rom_path", selectedRom);
            StartActivity(intentGame);
        };
        topRow.AddView(btnJogar);
        layout.AddView(topRow);
        SetContentView(layout);
    }

    string GetFileNameFromUri(global::Android.Net.Uri uri){
        try{
            using(var c = ContentResolver.Query(uri, null, null, null, null)){
                if(c!=null && c.MoveToFirst()){
                    int idx = c.GetColumnIndex(OpenableColumns.DisplayName);
                    if(idx>=0) return c.GetString(idx);
                }
            }
        }catch{}
        return uri.LastPathSegment ?? "prod.keys";
    }

    protected override void OnActivityResult(int requestCode, Result result, Intent data)
    {
        base.OnActivityResult(requestCode, result, data);
        if(requestCode==1001 && result==Result.Ok && data?.Data!=null){
            try{
                var uri = data.Data;
                string fileName = GetFileNameFromUri(uri).ToLower();
                string targetName = fileName.Contains("title") ? "title.keys" : "prod.keys";
                ContentResolver.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission);
                using(var input = ContentResolver.OpenInputStream(uri)){
                    if(input==null) return;
                    var baseDir = Path.Combine(FilesDir.AbsolutePath, "Ryujinx");
                    var keysDir = Path.Combine(baseDir, "keys");
                    var sysKeysDir = Path.Combine(baseDir, "system", "keys");
                    Directory.CreateDirectory(keysDir);
                    Directory.CreateDirectory(sysKeysDir);
                    Directory.CreateDirectory(KeysPath);
                    using(var ms = new MemoryStream()){
                        input.CopyTo(ms);
                        var bytes = ms.ToArray();
                        File.WriteAllBytes(Path.Combine(keysDir, targetName), bytes);
                        File.WriteAllBytes(Path.Combine(sysKeysDir, targetName), bytes);
                        File.WriteAllBytes(Path.Combine(KeysPath, targetName), bytes);
                        Toast.MakeText(this, $"{targetName} importada: {bytes.Length}b OK em 3 lugares", ToastLength.Long).Show();
                    }
                    UpdateInfo();
                }
            }catch(Exception ex){
                Toast.MakeText(this, "Erro import: "+ex.Message, ToastLength.Long).Show();
            }
        }
    }

    protected override void OnResume(){
        base.OnResume();
        if (HasAllFilesPermission()){
            EnsureDirectories();
            UpdateInfo();
            ScanGames();
        }else if (!permissionRequested){
            permissionRequested = true;
            RequestAllFilesPermission();
        }else UpdateInfo();
    }

    bool HasAllFilesPermission(){
        if (Build.VERSION.SdkInt < BuildVersionCodes.R) return true;
        return global::Android.OS.Environment.IsExternalStorageManager;
    }

    void EnsureDirectories(){
        try{
            Directory.CreateDirectory(GamesPath);
            Directory.CreateDirectory(KeysPath);
            Directory.CreateDirectory(FirmwarePath);
            if (FilesDir == null) return;
            var baseDir = Path.Combine(FilesDir.AbsolutePath, "Ryujinx");
            Directory.CreateDirectory(Path.Combine(baseDir, "keys"));
            Directory.CreateDirectory(Path.Combine(baseDir, "system"));
            Directory.CreateDirectory(Path.Combine(baseDir, "system", "keys"));
            Directory.CreateDirectory(Path.Combine(baseDir, "system", "Contents", "registered"));
            Directory.CreateDirectory(Path.Combine(baseDir, "bis", "system", "Contents", "registered"));

            foreach (var k in new[] { "prod.keys", "title.keys" }){
                var src = Path.Combine(KeysPath, k);
                if (!File.Exists(src)) continue;
                try{
                    File.Copy(src, Path.Combine(baseDir, "keys", k), true);
                    File.Copy(src, Path.Combine(baseDir, "system", "keys", k), true);
                    File.Copy(src, Path.Combine(baseDir, "system", k), true);
                }catch{}
            }
        }catch{}
    }

    void UpdateInfo(){
        if (layout.ChildCount < 2) return;
        var info = layout.GetChildAt(1) as TextView;
        if (info == null) return;
        var prodInt = new FileInfo(Path.Combine(FilesDir.AbsolutePath, "Ryujinx", "keys", "prod.keys"));
        int firmIntCount = 0;
        try{ var p = Path.Combine(FilesDir.AbsolutePath, "Ryujinx", "system", "Contents", "registered"); if(Directory.Exists(p)) firmIntCount = Directory.GetFiles(p, "*.nca").Length; }catch{}
        
        if(!prodInt.Exists){
            info.Text = "keys NAO encontradas - usa Importar Keys";
            info.SetTextColor(global::Android.Graphics.Color.Red);
        }else{
            info.Text = $"keys OK {prodInt.Length/1024}KB | Firm {firmIntCount} NCAs | Pronto pra jogar";
            info.SetTextColor(global::Android.Graphics.Color.Green);
        }
    }

    void RequestAllFilesPermission(){
        try{
            var intent = new Intent(global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
            intent.SetData(global::Android.Net.Uri.Parse("package:" + PackageName));
            StartActivity(intent);
        }catch{
            StartActivity(new Intent(global::Android.Provider.Settings.ActionManageAllFilesAccessPermission));
        }
    }

    void ScanGames(){
        if (!HasAllFilesPermission()){
            Toast.MakeText(this, "Concede a permissao primeiro", ToastLength.Long).Show(); return;
        }
        while (layout.ChildCount > 3) layout.RemoveViewAt(layout.ChildCount - 1);
        selectedRom = "";
        btnJogar.Enabled = false; btnJogar.Text = "JOGAR";
        var container = new LinearLayout(this){ Orientation = Orientation.Vertical };
        try{
            var allFiles = Directory.EnumerateFiles(GamesPath, "*.*", SearchOption.AllDirectories)
                .Where(f => !IgnoredDirs.Any(ig => f.Contains(ig, StringComparison.OrdinalIgnoreCase)))
                .Where(f => f.EndsWith(".nsp") || f.EndsWith(".xci") || f.EndsWith(".nsz") || f.EndsWith(".xcz"))
                .OrderBy(f => f).Take(100).ToList();
            if (allFiles.Count == 0){
                var empty = new TextView(this) { Text = "Nenhum jogo em " + GamesPath }; empty.SetTextColor(global::Android.Graphics.Color.Red); layout.AddView(empty); return;
            }
            foreach (var romPath in allFiles){
                var row = new LinearLayout(this){ Orientation = Orientation.Horizontal }; row.SetPadding(10,8,10,8);
                var fi = new FileInfo(romPath);
                var name = new TextView(this) { Text = Path.GetFileName(romPath) + $" [{fi.Length/1024/1024}MB]" };
                name.SetTextColor(global::Android.Graphics.Color.White); name.LayoutParameters = new LinearLayout.LayoutParams(0, -2, 1f);
                row.AddView(name);
                var localPath = romPath;
                var btn = new Button(this) { Text = "Selecionar" };
                btn.Click += (s, e) =>{ selectedRom = localPath; btnJogar.Enabled = true; btnJogar.Text = "JOGAR " + Path.GetFileName(localPath); };
                row.AddView(btn);
                container.AddView(row);
            }
        }catch(Exception ex){
            container.AddView(new TextView(this) { Text = "Erro scan: " + ex.Message });
        }
        layout.AddView(container);
    }
}
