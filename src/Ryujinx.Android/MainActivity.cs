using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;

namespace DragoNX;

[Activity(Label = "DragoNX", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            if (!Android.OS.Environment.IsExternalStorageManager)
            {
                var intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission);
                intent.SetData(Android.Net.Uri.Parse("package:" + PackageName));
                StartActivity(intent);
                return;
            }
        }

        // FIX LINHA 51,52,53,59 - usando Java.IO.File completo
        var baseDir = new Java.IO.File("/storage/emulated/0/Download/DragoNX");
        if (!baseDir.Exists()) baseDir.Mkdirs();
        new Java.IO.File("/storage/emulated/0/Download/DragoNX/system").Mkdirs();
        new Java.IO.File("/storage/emulated/0/Download/DragoNX/games").Mkdirs();
        new Java.IO.File("/storage/emulated/0/Download/DragoNX/cache").Mkdirs();
        new Java.IO.File("/storage/emulated/0/Download/DragoNX/cache/jit").Mkdirs();

        var gamesDir = new Java.IO.File("/storage/emulated/0/Download/DragoNX/games");
        var files = gamesDir.ListFiles();
        var intent2 = new Intent(this, typeof(GameActivity));
        if (files!= null && files.Length > 0)
        {
            intent2.PutExtra("rom_path", files[0].AbsolutePath);
        }
        StartActivity(intent2);
        Finish();
    }
}
