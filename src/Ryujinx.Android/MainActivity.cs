#nullable disable
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

        var btnPerm = new Button(this) { Text = "1 - Permissao" };
        btnPerm.Click += (s, e) => RequestAllFilesPermission();
        topRow.AddView(btnPerm);

        var btnScan = new Button(this) { Text = "2 - Listar Jogos" };
        btnScan.Click += (s, e) => ScanGames();
        topRow.AddView(btnScan);

        // NOVO BOTAO - NAO QUEBRA NADA
        var btnImport = new Button(this) { Text = "Importar Keys" };
        btnImport.SetBackgroundColor(Android.Graphics.Color.Yellow);
        btnImport.Click += (s,e)=>{
            try{
                var intent = new Intent(Intent.ActionOpenDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType("*/*");
                StartActivityForResult(intent, 1001);
            }catch(Exception ex){ Toast.MakeText(this, ex.Message, ToastLength.Long).Show(); }
        };
        topRow.AddView(btnImport);

        btnJogar = new Button(this) { Text = "JOGAR" };
        btnJogar.SetBackgroundColor(Android.Graphics.Color.Green);
        btnJogar.Enabled = false;
        btnJogar.Click += (s, e) =>
        {
            bool empty = string.IsNullOrEmpty(selectedRom);
            bool exists = false;
            if(empty==false) exists = File.Exists(selectedRom);
            if(empty || exists==false)
            {
                Toast.MakeText(this, "ROM nao encontrada", ToastLength.Short).Show();
                return;
            }
            // AGORA CHECA INTERNO TAMBEM
            var internalKeys = Path.Combine(FilesDir.AbsolutePath, "Ryujinx", "keys", "prod.keys");
            var externalProd = Path.Combine(KeysPath, "prod.keys");
            if(File.Exists(internalKeys)==false && File.Exists(externalProd)==false)
            {
                Toast.MakeText(this, "prod.keys faltando - usa Importar Keys", ToastLength.Long).Show();
                return;
            }
            var intent = new Intent(this, typeof(GameActivity));
            intent.PutExtra("rom_path", selectedRom);
            StartActivity(intent);
        };
        topRow.AddView(btnJogar);

        layout.AddView(topRow);
        SetContentView(layout);
    }

    protected override void OnActivityResult(int requestCode, Result result, Intent data)
    {
        base.OnActivityResult(requestCode, result, data);
        if(requestCode==1001 && result==Result.Ok && data!=null){
            try{
                var uri = data.Data;
                if(uri==null) return;
                using(var input = ContentResolver.OpenInputStream(uri)){
                    if(input==null) return;
                    var baseDir = Path.Combine(FilesDir.AbsolutePath, "Ryujinx");
                    var keysDir = Path.Combine(baseDir, "keys");
                    var sysKeysDir = Path.Combine(baseDir, "system", "keys");
                    Directory.CreateDirectory(keysDir);
                    Directory.CreateDirectory(sysKeysDir);
                    Directory.CreateDirectory(KeysPath);

                    string dstInternal1 = Path.Combine(keysDir, "prod.keys");
                    string dstInternal2 = Path.Combine(sysKeysDir, "prod.keys");
                    string dstExternal = Path.Combine(KeysPath, "prod.keys");

                    // salva nos 3 lugares
                    using(var ms = new MemoryStream()){
                        input.CopyTo(ms);
                        var bytes = ms.ToArray();
                        File.WriteAllBytes(dstInternal1, bytes);
                        File.WriteAllBytes(dstInternal2, bytes);
                        File.WriteAllBytes(dstExternal, bytes);
                        long len = bytes.Length;
                        string msg = len>3000 && len<10000 ? $"TXT OK {len} bytes" : $"BINARIO {len} bytes - precisa TXT 5kb";
                        Toast.MakeText(this, $"Keys importada: {msg}", ToastLength.Long).Show();
                    }
                    UpdateInfo();
                }
            }catch(Exception ex){
                Toast.MakeText(this, "Erro import: "+ex.Message, ToastLength.Long).Show();
            }
        }
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
        else if (permissionRequested==false)
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
            var baseDir = Path.Combine(FilesDir.AbsolutePath, "Ryujinx");
            var keysDir = Path.Combine(baseDir, "keys");
            var sysKeysDir = Path.Combine(baseDir, "system", "keys");
            Directory.CreateDirectory(keysDir);
            Directory.CreateDirectory(sysKeysDir);

            foreach (var k in new[] { "prod.keys", "title.keys" })
            {
                var src = Path.Combine(KeysPath, k);
                var dst1 = Path.Combine(keysDir, k);
                var dst2 = Path.Combine(sysKeysDir, k);
                if (File.Exists(src)) 
                {
                    try{
                        // só copia se for TXT pra não sobrescrever TXT bom com binário 16025
                        var len = new FileInfo(src).Length;
                        if(k=="prod.keys" && len>10000) continue;
                        File.Copy(src, dst1, true);
                        File.Copy(src, dst2, true);
                    }catch{}
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
        var v = layout.GetChildAt(1);
        var info = v as TextView;
        if (info == null) return;
        var prodExternal = new FileInfo(Path.Combine(KeysPath, "prod.keys"));
        var prodInternal = new FileInfo(Path.Combine(FilesDir.AbsolutePath, "Ryujinx", "keys", "prod.keys"));
        var titleKeys = new FileInfo(Path.Combine(KeysPath, "title.keys"));
        
        string txt = "";
        if (prodInternal.Exists) txt += $"interno {prodInternal.Length}b ";
        if (prodExternal.Exists) txt += $"externo {prodExternal.Length}b | ";
        if(titleKeys.Exists) txt += "title.keys " + titleKeys.Length + " bytes";
        else txt += "title.keys FALTANDO";

        if(prodInternal.Exists==false && prodExternal.Exists==false){
            info.Text = "keys NAO encontradas - usa Importar Keys";
            info.SetTextColor(Android.Graphics.Color.Red);
        }else{
            info.Text = txt;
            bool isTxt = prodInternal.Exists ? prodInternal.Length<10000 : prodExternal.Length<10000;
            info.SetTextColor(isTxt ? Android.Graphics.Color.Green : Android.Graphics.Color.Yellow);
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
        if (HasAllFilesPermission()==false)
        {
            Toast.MakeText(this, "Concede a permissao primeiro", ToastLength.Long).Show();
            return;
        }

        while (layout.ChildCount > 3)
            layout.RemoveViewAt(layout.ChildCount - 1);

        selectedRom = "";
        if (btnJogar != null)
        {
            btnJogar.Enabled = false;
            btnJogar.Text = "JOGAR";
        }

        var container = new LinearLayout(this);
        container.Orientation = Orientation.Vertical;
        container.SetGravity(GravityFlags.Center);

        try
        {
            var allFiles = Directory.EnumerateFiles(GamesPath, "*.*", SearchOption.AllDirectories)
                .Where(f => IgnoredDirs.Any(ig => f.Contains(ig, StringComparison.OrdinalIgnoreCase))==false)
                .Where(f => f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f)
                .Take(100).ToList();

            if (allFiles.Count == 0)
            {
                var empty = new TextView(this) { Text = "Nenhum jogo em " + GamesPath + " (recursivo)" };
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

                var fi = new FileInfo(romPath);
                var name = new TextView(this) { Text = Path.GetFileName(romPath) + " [" + (fi.Length / 1024 / 1024) + "MB] [" + Path.GetFileName(Path.GetDirectoryName(romPath)) + "]" };
                name.SetTextColor(Android.Graphics.Color.White);
                name.TextSize = 11;
                name.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
                row.AddView(name);

                var localPath = romPath;
                var btn = new Button(this) { Text = "Selecionar" };
                btn.Click += (s, e) =>
                {
                    selectedRom = localPath;
                    if (btnJogar != null)
                    {
                        btnJogar.Enabled = true;
                        btnJogar.Text = "JOGAR " + Path.GetFileName(localPath);
                    }
                    Toast.MakeText(this, "Selecionado: " + Path.GetFileName(localPath), ToastLength.Short).Show();
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
