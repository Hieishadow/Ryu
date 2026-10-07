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
    const string DriversPath = BasePath + "/drivers";
    static readonly string[] IgnoredDirs = { ".thumbnails", "System Volume Information", ".trashed", "LOST.DIR", "Android" };

    LinearLayout layout;
    string selectedRom = "";
    string selectedDriver = "system";
    string selectedBackend = "vulkan";
    Button btnJogar;
    Button btnSystem;
    Button btnTurnip;
    Button btnVulkan;
    Button btnOpenGl;
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
        var title = new TextView(this) { Text = "Ryubing - S20 FE v10.8.1 FIX" };
        title.SetTextColor(global::Android.Graphics.Color.White);
        title.TextSize = 20; title.Gravity = GravityFlags.Center;
        layout.AddView(title);
        var info = new TextView(this){ Gravity = GravityFlags.Center, TextSize = 11f };
        layout.AddView(info);

        var driverRow = new LinearLayout(this){ Orientation = Orientation.Horizontal };
        driverRow.SetGravity(GravityFlags.Center);
        var lblDriver = new TextView(this){ Text = " Driver: " };
        lblDriver.SetTextColor(global::Android.Graphics.Color.White);
        btnSystem = new Button(this){ Text = "SYSTEM [ATUAL]" };
        btnTurnip = new Button(this){ Text = "TURNIP" };
        btnSystem.Click+= (s,e)=>{
            selectedDriver="system";
            btnSystem.Text="SYSTEM [ATUAL]";
            btnTurnip.Text="TURNIP";
            Toast.MakeText(this,"Driver SYSTEM (recomendado)",ToastLength.Short).Show();
            UpdateInfo();
        };
        btnTurnip.Click+= (s,e)=>{
            var so1 = Path.Combine(DriversPath,"libvulkan_freedreno.so");
            var so2 = Path.Combine(DriversPath,"libvulkan.so");
            var so3 = Path.Combine(DriversPath,"vulkan.adreno.so");
            bool exists = File.Exists(so1) || File.Exists(so2) || File.Exists(so3);
            if(!exists){
                Toast.MakeText(this,"Coloque driver em /Download/Ryubing/drivers/",ToastLength.Long).Show();
                return;
            }
            long size = 0;
            try{ if(File.Exists(so1)) size = new FileInfo(so1).Length; else if(File.Exists(so2)) size = new FileInfo(so2).Length; }catch{}
            if(size > 15000000){
                Toast.MakeText(this,"AVISO: esse Turnip precisa libhardware.so e não funciona no Android 13+. Use system.",ToastLength.Long).Show();
                selectedDriver="system";
                btnSystem.Text="SYSTEM [ATUAL]";
                btnTurnip.Text="TURNIP [INCOMPATIVEL]";
            } else {
                selectedDriver="turnip";
                btnSystem.Text="SYSTEM";
                btnTurnip.Text="TURNIP [ATUAL]";
                Toast.MakeText(this,"Turnip selecionado",ToastLength.Short).Show();
            }
            UpdateInfo();
        };
        driverRow.AddView(lblDriver);
        driverRow.AddView(btnSystem);
        driverRow.AddView(btnTurnip);
        layout.AddView(driverRow);

        var backendRow = new LinearLayout(this){ Orientation = Orientation.Horizontal };
        backendRow.SetGravity(GravityFlags.Center);
        var lblBackend = new TextView(this){ Text = " Backend: " };
        lblBackend.SetTextColor(global::Android.Graphics.Color.White);
        btnVulkan = new Button(this){ Text = "VULKAN [ATUAL]" };
        btnOpenGl = new Button(this){ Text = "OPENGL [NAO TEM]" };
        btnVulkan.Click+= (s,e)=>{
            selectedBackend="vulkan";
            btnVulkan.Text="VULKAN [ATUAL]";
            btnOpenGl.Text="OPENGL [NAO TEM]";
            UpdateInfo();
        };
        btnOpenGl.Click+= (s,e)=>{
            Toast.MakeText(this,"Esse fork é Vulkan-only. OpenGL não existe aqui",ToastLength.Long).Show();
            selectedBackend="vulkan";
            btnVulkan.Text="VULKAN [ATUAL]";
            btnOpenGl.Text="OPENGL [NAO TEM]";
            UpdateInfo();
        };
        backendRow.AddView(lblBackend);
        backendRow.AddView(btnVulkan);
        backendRow.AddView(btnOpenGl);
        layout.AddView(backendRow);

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
        btnClear.Click += (s,e)=>{ 
            try{ 
                if(File.Exists(BasePath+"/ryubing_log.txt")) File.Delete(BasePath+"/ryubing_log.txt");
                if(File.Exists(BasePath+"/crash_emu.txt")) File.Delete(BasePath+"/crash_emu.txt");
                var drvCache = Path.Combine(FilesDir.AbsolutePath,"drivers");
                if(Directory.Exists(drvCache)) Directory.Delete(drvCache,true);
                Toast.MakeText(this,"Logs e cache driver limpos",ToastLength.Short).Show(); 
                UpdateInfo(); 
            }catch{} 
        };
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
            intentGame.PutExtra("vulkan_driver", selectedDriver);
            intentGame.PutExtra("graphics_backend", selectedBackend);
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
                    Directory.CreateDirectory(keysDir);
                    Directory.CreateDirectory(KeysPath);
                    using(var ms = new MemoryStream()){
                        input.CopyTo(ms);
                        var bytes = ms.ToArray();
                        File.WriteAllBytes(Path.Combine(keysDir, targetName), bytes);
                        File.WriteAllBytes(Path.Combine(KeysPath, targetName), bytes);
                        Toast.MakeText(this, $"{targetName} importada: {bytes.Length}b OK", ToastLength.Long).Show();
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
            Directory.CreateDirectory(DriversPath);
            if (FilesDir == null) return;
            var baseDir = Path.Combine(FilesDir.AbsolutePath, "Ryujinx");
            var keysDir = Path.Combine(baseDir, "keys");
            var bisRegistered = Path.Combine(baseDir, "bis", "system", "Contents", "registered");
            var sysRegistered = Path.Combine(baseDir, "system", "Contents", "registered");
            Directory.CreateDirectory(keysDir);
            Directory.CreateDirectory(Path.Combine(baseDir, "system"));
            Directory.CreateDirectory(sysRegistered);
            Directory.CreateDirectory(bisRegistered);
            foreach (var k in new[] { "prod.keys", "title.keys" }){
                var src = Path.Combine(KeysPath, k);
                if (!File.Exists(src)) continue;
                try{ File.Copy(src, Path.Combine(keysDir, k), true); }catch{}
            }
            try {
                if (Directory.Exists(FirmwarePath)) {
                    var ncas = Directory.GetFiles(FirmwarePath, "*.nca", SearchOption.AllDirectories);
                    foreach(var nca in ncas) {
                        try {
                            var name = Path.GetFileName(nca);
                            var dest1 = Path.Combine(bisRegistered, name);
                            var dest2 = Path.Combine(sysRegistered, name);
                            if (!File.Exists(dest1)) File.Copy(nca, dest1, true);
                            if (!File.Exists(dest2)) File.Copy(nca, dest2, true);
                        } catch {}
                    }
                }
            } catch {}
            try {
                var dupFolder = Path.Combine(baseDir, "system", "keys");
                if(Directory.Exists(dupFolder)) Directory.Delete(dupFolder, true);
                foreach(var k in new[] { "prod.keys", "title.keys" }){
                    var dupFile = Path.Combine(baseDir, "system", k);
                    if(File.Exists(dupFile)) File.Delete(dupFile);
                }
            } catch {}
        }catch{}
    }

    void UpdateInfo(){
        if (layout.ChildCount < 2) return;
        var info = layout.GetChildAt(1) as TextView;
        if (info == null) return;
        var prodInt = new FileInfo(Path.Combine(FilesDir.AbsolutePath, "Ryujinx", "keys", "prod.keys"));
        int firmIntCount = 0;
        try{ 
            var p1 = Path.Combine(FilesDir.AbsolutePath, "Ryujinx", "bis", "system", "Contents", "registered"); 
            var p2 = Path.Combine(FilesDir.AbsolutePath, "Ryujinx", "system", "Contents", "registered");
            if(Directory.Exists(p1)) firmIntCount = Directory.GetFiles(p1, "*.nca").Length;
            if(firmIntCount==0 && Directory.Exists(p2)) firmIntCount = Directory.GetFiles(p2, "*.nca").Length;
        }catch{}
        
        if(!prodInt.Exists){
            info.Text = "keys NAO encontradas - usa Importar Keys";
            info.SetTextColor(global::Android.Graphics.Color.Red);
        }else{
            info.Text = $"keys OK {prodInt.Length/1024}KB | Firm {firmIntCount} NCAs | {selectedBackend.ToUpper()} | Driver: {selectedDriver} | S20 FE v10.8.1";
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
        while (layout.ChildCount > 5) layout.RemoveViewAt(layout.ChildCount - 1);
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
                btn.Click += (s, e) =>{ selectedRom = localPath; btnJogar.Enabled = true; btnJogar.Text = "JOGAR " + Path.GetFileName(localPath) + $" [{selectedBackend}/{selectedDriver}]"; };
                row.AddView(btn);
                container.AddView(row);
            }
        }catch(Exception ex){
            container.AddView(new TextView(this) { Text = "Erro scan: " + ex.Message });
        }
        layout.AddView(container);
    }
}
