#nullable disable
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Android.Views;
using Android.Database;
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
    MainLauncher = true,
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

    LinearLayout layout;
    string selectedRom = "";
    Button btnJogar;
    bool permissionRequested = false;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if(Window!=null) Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        layout = new LinearLayout(this);
        layout.Orientation = Orientation.Vertical;
        layout.SetGravity(GravityFlags.Center);
        layout.SetBackgroundColor(Android.Graphics.Color.Black);
        layout.SetPadding(40, 20, 40, 20);

        var title = new TextView(this) { Text = "Ryubing - S20 FE Edition" };
        title.SetTextColor(Android.Graphics.Color.White);
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
        btnImport.SetBackgroundColor(Android.Graphics.Color.Yellow);
        btnImport.Click += (s,e)=>{
            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
            StartActivityForResult(intent, 1001);
        };
        topRow.AddView(btnImport);

        btnJogar = new Button(this) { Text = "JOGAR" };
        btnJogar.SetBackgroundColor(Android.Graphics.Color.Green);
        btnJogar.Enabled = false;
        btnJogar.Click += (s, e) =>
        {
            if(string.IsNullOrEmpty(selectedRom) || !File.Exists(selectedRom)){
                Toast.MakeText(this, "ROM nao encontrada", ToastLength.Short).Show(); return;
            }
            var internalProd = Path.Combine(FilesDir.AbsolutePath, "Ryujinx", "keys", "prod.keys");
            var internalProd2 = Path.Combine(FilesDir.AbsolutePath, "Ryujinx", "system", "keys", "prod.keys");
            if(!File.Exists(internalProd) && !File.Exists(internalProd2) && !File.Exists(Path.Combine(KeysPath,"prod.keys"))){
                Toast.MakeText(this, "prod.keys faltando - usa Importar Keys", ToastLength.Long).Show(); return;
            }
            // trava binario 16025
            var fi = File.Exists(internalProd) ? new FileInfo(internalProd) : (File.Exists(internalProd2) ? new FileInfo(internalProd2) : null);
            if(fi!=null && fi.Length>10000){
                Toast.MakeText(this, $"prod.keys BINARIO {fi.Length}b! Importe o TXT de 5kb", ToastLength.Long).Show(); return;
            }
            var intentGame = new Intent(this, typeof(GameActivity));
            intentGame.PutExtra("rom_path", selectedRom);
            StartActivity(intentGame);
        };
        topRow.AddView(btnJogar);
        layout.AddView(topRow);
        SetContentView(layout);
    }

    string GetFileNameFromUri(Android.Net.Uri uri){
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
                        // trava binario 16025
                        if(targetName=="prod.keys" && bytes.Length>10000){
                            Toast.MakeText(this, $"ARQUIVO BINARIO {bytes.Length}b ignorado! Pegue o TXT de 5kb", ToastLength.Long).Show();
                            return;
                        }
                        File.WriteAllBytes(Path.Combine(keysDir, targetName), bytes);
                        File.WriteAllBytes(Path.Combine(sysKeysDir, targetName), bytes);
                        File.WriteAllBytes(Path.Combine(KeysPath, targetName), bytes);
                        string msg = targetName=="prod.keys" ? $"TXT OK {bytes.Length}b" : $"{targetName} {bytes.Length}b";
                        Toast.MakeText(this, $"{targetName} importada: {msg}", ToastLength.Long).Show();
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
            var keysDir = Path.Combine(baseDir, "keys");
            var sysKeysDir = Path.Combine(baseDir, "system", "keys");
            Directory.CreateDirectory(keysDir);
            Directory.CreateDirectory(sysKeysDir);
            foreach (var k in new[] { "prod.keys", "title.keys" }){
                var src = Path.Combine(KeysPath, k);
                if (!File.Exists(src)) continue;
                var len = new FileInfo(src).Length;
                if(k=="prod.keys" && len>10000) continue; // nunca sobrescreve TXT bom com binario
                try{
                    File.Copy(src, Path.Combine(keysDir, k), true);
                    File.Copy(src, Path.Combine(sysKeysDir, k), true);
                }catch{}
            }
            // FIX CRITICO: seta BaseDir pro VFS não criar pasta temporaria vazia
            try{
                var admType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>{try{return a.GetTypes();}catch{return new Type[0];}}).FirstOrDefault(t=>t.Name=="AppDataManager");
                admType?.GetProperty("BaseDirPath")?.SetValue(null, baseDir);
            }catch{}
        }catch(Exception ex){
            Android.Util.Log.Error("Ryubing", "EnsureDirectories: " + ex.Message);
        }
    }

    void UpdateInfo(){
        if (layout.ChildCount < 2) return;
        var info = layout.GetChildAt(1) as TextView;
        if (info == null) return;
        var prodExt = new FileInfo(Path.Combine(KeysPath, "prod.keys"));
        var prodInt = new FileInfo(Path.Combine(FilesDir.AbsolutePath, "Ryujinx", "keys", "prod.keys"));
        var titleInt = new FileInfo(Path.Combine(FilesDir.AbsolutePath, "Ryujinx", "keys", "title.keys"));

        if(!prodInt.Exists && !prodExt.Exists){
            info.Text = "keys NAO encontradas - usa Importar Keys (TXT 5kb)";
            info.SetTextColor(Android.Graphics.Color.Red);
        }else{
            string txt = "";
            if(prodInt.Exists) txt += $"interno {prodInt.Length}b ";
            if(prodExt.Exists) txt += $"ext {prodExt.Length}b ";
            txt += titleInt.Exists ? $"| title {titleInt.Length}b" : "| title FALTA";
            info.Text = txt;
            bool isTxt = prodInt.Exists ? prodInt.Length<10000 : prodExt.Length<10000;
            info.SetTextColor(isTxt ? Android.Graphics.Color.Green : Android.Graphics.Color.Yellow);
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
        container.SetGravity(GravityFlags.Center);
        try{
            var allFiles = Directory.EnumerateFiles(GamesPath, "*.*", SearchOption.AllDirectories)
                .Where(f => !IgnoredDirs.Any(ig => f.Contains(ig, StringComparison.OrdinalIgnoreCase)))
                .Where(f => f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f).Take(100).ToList();
            if (allFiles.Count == 0){
                var empty = new TextView(this) { Text = "Nenhum jogo em " + GamesPath }; empty.SetTextColor(Android.Graphics.Color.Red); empty.Gravity = GravityFlags.Center;
                layout.AddView(empty); return;
            }
            foreach (var romPath in allFiles){
                var row = new LinearLayout(this){ Orientation = Orientation.Horizontal }; row.SetGravity(GravityFlags.CenterVertical); row.SetPadding(10,8,10,8);
                var fi = new FileInfo(romPath);
                var name = new TextView(this) { Text = Path.GetFileName(romPath) + $" [{fi.Length/1024/1024}MB]" };
                name.SetTextColor(Android.Graphics.Color.White); name.TextSize = 11;
                name.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
                row.AddView(name);
                var localPath = romPath;
                var btn = new Button(this) { Text = "Selecionar" };
                btn.Click += (s, e) =>{
                    selectedRom = localPath;
                    btnJogar.Enabled = true;
                    btnJogar.Text = "JOGAR " + Path.GetFileName(localPath);
                    Toast.MakeText(this, "Selecionado: " + Path.GetFileName(localPath), ToastLength.Short).Show();
                };
                row.AddView(btn);
                container.AddView(row);
            }
        }catch(Exception ex){
            var err = new TextView(this) { Text = "Erro scan: " + ex.Message }; err.SetTextColor(Android.Graphics.Color.Red); container.AddView(err);
        }
        layout.AddView(container);
    }
}
