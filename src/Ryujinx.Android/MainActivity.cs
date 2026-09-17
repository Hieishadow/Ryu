using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Java.IO;

namespace DragoNX;

[Activity(Label = "DragoNX", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
public class MainActivity : Activity
{
    const int REQ_ALL_FILES = 190;
    const string BasePath = "/storage/emulated/0/Download/DragoNX";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.R &&!Android.OS.Environment.IsExternalStorageManager)
        {
            try
            {
                var intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission);
                intent.SetData(Android.Net.Uri.Parse("package:" + PackageName));
                StartActivityForResult(intent, REQ_ALL_FILES);
            }
            catch
            {
                StartActivityForResult(new Intent(Settings.ActionManageAllFilesAccessPermission), REQ_ALL_FILES);
            }
            return;
        }

        LaunchDrago();
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == REQ_ALL_FILES)
        {
            if (Android.OS.Environment.IsExternalStorageManager)
                LaunchDrago();
            else
                FinishAffinity();
        }
    }

    void LaunchDrago()
    {
        new File(BasePath + "/system").Mkdirs();
        new File(BasePath + "/games").Mkdirs();
        new File(BasePath + "/cache").Mkdirs();

        var intent = new Intent(this, typeof(GameActivity));

        try
        {
            var gamesDir = new File(BasePath + "/games");
            var files = gamesDir.ListFiles();
            if (files!= null && files.Length > 0)
            {
                intent.PutExtra("rom_path", files[0].AbsolutePath);
            }
        }
        catch { }

        StartActivity(intent);
        Finish();
    }
}
