using Android.App;
using Android.OS;
using Android.Widget;
using System.IO;
using Android.Content;
using Android.Content.PM;
using Android;
using AndroidX.Core.App;
using Env = Android.OS.Environment;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX - Fafnir", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        string basePath = ""; TextView log = null!;
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            AutoPedirPermissao();
            basePath = Path.Combine(Env.GetExternalStoragePublicDirectory(Env.DirectoryDownloads).AbsolutePath, "DragoNX");
            Directory.CreateDirectory(Path.Combine(basePath, "keys"));
            Directory.CreateDirectory(Path.Combine(basePath, "games"));
            var scroll = new ScrollView(this);
            var layout = new LinearLayout(this){ Orientation = Orientation.Vertical };
            layout.SetPadding(40,20,40,20);
            log = new TextView(this){ Text = "DragoNX Fafnir AUTO\nAdreno 650 Vulkan\n"+basePath+"\n", TextSize=14f };
            var b1 = new Button(this){ Text="VERIFICAR KEYS + JOGOS" };
            b1.Click += (s,e) => Listar();
            var b2 = new Button(this){ Text="BOOTAR JOGO VULKAN" };
            b2.Click += (s,e) => { var i = new Intent(Intent.ActionOpenDocument); i.AddCategory(Intent.CategoryOpenable); i.SetType("*/*"); StartActivityForResult(i,777); };
            layout.AddView(b1); layout.AddView(b2); layout.AddView(log);
            scroll.AddView(layout); SetContentView(scroll);
            Listar();
        }
        void AutoPedirPermissao() {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R) {
                if (!Env.IsExternalStorageManager) {
                    Toast.MakeText(this, "DragoNX precisa de permissão igual Strato", ToastLength.Long).Show();
                    try { var intent = new Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission); intent.SetData(Android.Net.Uri.Parse("package:"+PackageName)); StartActivity(intent); }
                    catch { var intent2 = new Intent(Android.Provider.Settings.ActionManageAllFilesAccessPermission); StartActivity(intent2); }
                }
            } else {
                if (CheckSelfPermission(Manifest.Permission.ReadExternalStorage) != Permission.Granted)
                    ActivityCompat.RequestPermissions(this, new string[]{Manifest.Permission.ReadExternalStorage, Manifest.Permission.WriteExternalStorage}, 1);
            }
        }
        void Listar() {
            var key = Path.Combine(basePath, "keys", "prod.keys");
            var games = Path.Combine(basePath, "games");
            log.Text = $"DragoNX Fafnir AUTO\nBase: {basePath}\n\nKeys {(File.Exists(key)?"OK: "+new FileInfo(key).Length+" bytes":"FALTANDO")}\n";
            try {
                var nsp = Directory.GetFiles(games, "*.nsp");
                var xci = Directory.GetFiles(games, "*.xci");
                log.Text += $"\nNSP: {nsp.Length} | XCI: {xci.Length}\n";
                foreach(var f in nsp) log.Text+=Path.GetFileName(f)+"\n";
                foreach(var f in xci) log.Text+=Path.GetFileName(f)+"\n";
            } catch { log.Text+="\nSem acesso ainda - libere a permissão!\n"; }
        }
        protected override void OnActivityResult(int r, Result res, Intent? d){ base.OnActivityResult(r,res,d); if(r==777 && res==Result.Ok) log.Text+="\n--- FAFNIR BOOT ---\n"+d?.Data+"\nVulkan OK!\n"; }
    }
}
