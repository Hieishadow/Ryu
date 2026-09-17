using System;
using System.IO;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Content.PM;
using Android.Widget;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.Memory;
using RyujinxSwitch = Ryujinx.HLE.Switch;

namespace RyujinxAndroid
{
    [Activity(Label = "DragoNX JIT", MainLauncher = true, ScreenOrientation = ScreenOrientation.Landscape, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
    public class MainActivity : Activity, ISurfaceHolderCallback
    {
        SurfaceView view;
        RyujinxSwitch emu;
        string game;

        protected override void OnCreate(Bundle? b)
        {
            base.OnCreate(b);
            Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
            view = new SurfaceView(this);
            view.Holder.AddCallback(this);
            SetContentView(view);

            var dir = "/storage/emulated/0/Download/DragoNX/";
            if (Directory.Exists(dir))
            {
                foreach(var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    if(f.EndsWith(".nsp") || f.EndsWith(".xci") || f.EndsWith(".nca"))
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
                // Libera JIT no Android
                MemoryBlock.EnableForcedRwx = true;

                var vfs = VirtualFileSystem.Create();
                var keyPath = "/storage/emulated/0/Download/DragoNX/prod.keys";
                if (File.Exists(keyPath)) vfs.ImportKeys(File.ReadAllBytes(keyPath));

                var contentManager = new ContentManager(vfs);
                var userChannel = new UserChannelPersistence();
                var gpu = new VulkanRenderer();
                Ryujinx.Audio.IAalOutput audio = null;

                emu = new RyujinxSwitch(vfs, contentManager, userChannel, gpu, audio);
                emu.LoadApplication(game);
                
                new System.Threading.Thread(() => emu.Run())
                { IsBackground = true, Priority = System.Threading.ThreadPriority.Highest }.Start();

                Toast.MakeText(this, "JIT ON - " + Path.GetFileName(game), ToastLength.Short).Show();
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
