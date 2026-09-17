using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using System;
using System.IO;
using Ryujinx.Memory;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.Audio.Backends.Dummy;
using Ryujinx.Common.Configuration;
using Ryujinx.HLE.UI;

namespace Ryujinx.Android
{
    [Activity(Label = "DragoNX JIT", MainLauncher = true, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = ScreenOrientation.Landscape, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
            var view = new SurfaceView(this);
            SetContentView(view);

            var baseDir = "/storage/emulated/0/Download/DragoNX/";
            string gamePath = null;

            try { 
                if(Directory.Exists(baseDir))
                    foreach(var f in Directory.GetFiles(baseDir, "*", SearchOption.AllDirectories))
                        if(f.EndsWith(".nsp") || f.EndsWith(".xci") || f.EndsWith(".nro") || f.EndsWith(".nca"))
                        { gamePath = f; break; }
            } catch {}

            if(gamePath == null)
            {
                Toast.MakeText(this,"Coloque o jogo em /Download/DragoNX/", ToastLength.Long).Show();
                return;
            }

            try
            {
                // ATIVA JIT RWX PARA ANDROID
                MemoryBlock.EnableForcedRwx = true;

                var vfs = new VirtualFileSystem();
                var prodKeys = Path.Combine(baseDir, "prod.keys");
                if(File.Exists(prodKeys))
                    vfs.ImportKeys(File.ReadAllBytes(prodKeys), "prod.keys");

                var gpu = new VulkanRenderer();
                var audio = new DummyHardwareDeviceDriver();
                
                // O DragoNX original usa o AppHost, mas esse modo direto funciona pra build mínima
                Toast.MakeText(this, "JIT ON - " + Path.GetFileName(gamePath), ToastLength.Short).Show();
                
                // Chama o loader oficial do Ryujinx.Android
                var intent = new Android.Content.Intent(this, typeof(GameActivity));
                intent.PutExtra("gamePath", gamePath);
                StartActivity(intent);
            }
            catch(Exception e)
            {
                Toast.MakeText(this, "JIT ERRO: " + e.Message + "\n" + e.StackTrace, ToastLength.Long).Show();
            }
        }
    }
}
