using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Java.IO;

namespace DragoNX;

[Activity(Label = "DragoNX", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
public class MainActivity : Activity
{
    const string BasePath = "/storage/emulated/0/Download/DragoNX";
    const string KeysPath = "/storage/emulated/0/Download/DragoNX/system/prod.keys";

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.R &&!Android.OS.Environment.IsExternalStorageManager)
        {
            StartActivity(new Intent(Settings.ActionManageAppAllFilesAccessPermission, Android.Net.Uri.Parse("package:" + PackageName)));
            return;
        }

        new File(BasePath + "/system").Mkdirs();
        new File(BasePath + "/games").Mkdirs();
        new File(BasePath + "/cache").Mkdirs();

        var intent = new Intent(this, typeof(GameActivity));

        // Pega o primeiro jogo que achar em /games
        var gamesDir = new File(BasePath + "/games");
        var files = gamesDir.ListFiles();
        if (files!= null && files.Length > 0)
        {
            intent.PutExtra("rom_path", files[0].AbsolutePath);
        }

        StartActivity(intent);
        Finish();
    }
}
