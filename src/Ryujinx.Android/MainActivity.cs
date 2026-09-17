using System;
using System.IO;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Content.PM;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.Common.Configuration;
using Ryujinx.Graphics.Vulkan;

namespace RyujinxAndroid
{
    [Activity(Label = "DragoNX", MainLauncher = true, ScreenOrientation = ScreenOrientation.Landscape, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
    public class MainActivity : Activity, ISurfaceHolderCallback
    {
        SurfaceView view;
        Switch emu;
        string game;

        protected override void OnCreate(Bundle b)
        {
            base.OnCreate(b);
            Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
            view = new SurfaceView(this);
            view.Holder.AddCallback(this);
            SetContentView(view);

            // ACHA JOGO
            var dir = "/storage/emulated/0/Download/DragoNX/";
            if (Directory.Exists(dir))
            {
                foreach(var f in Directory.GetFiles(dir))
                {
                    if(f.EndsWith(".nsp") || f.EndsWith(".xci") || f.EndsWith(".nca") || f.EndsWith(".nro"))
                    { game = f; break; }
                }
            }
        }

        public void SurfaceCreated(ISurfaceHolder h)
        {
            if (game == null) 
            {
                Toast.MakeText(this, "Coloca jogo em /Download/DragoNX/", ToastLength.Long).Show();
                return;
            }

            try
            {
                // File system + keys
                var vfs = new VirtualFileSystem();
                var keyPath = "/storage/emulated/0/Download/DragoNX/prod.keys";
                if (File.Exists(keyPath))
                {
                    vfs.ImportKeys(File.ReadAllBytes(keyPath));
                }

                var renderer = new VulkanRenderer(h.Surface);
                var fsClient = new FileSystemClient(vfs);

                emu = new Switch(renderer, fsClient, fsClient);
                emu.LoadApplication(game);

                new System.Threading.Thread(() => emu.Run()).Start();
                Toast.MakeText(this, "ZELDA RODANDO JIT!", ToastLength.Short).Show();
            }
            catch(Exception e)
            {
                Toast.MakeText(this, "ERRO: " + e.Message, ToastLength.Long).Show();
            }
        }
        public void SurfaceChanged(ISurfaceHolder h, Android.Graphics.Format f, int w, int ht) {}
        public void SurfaceDestroyed(ISurfaceHolder h) { emu?.Stop(); }
    }
}
