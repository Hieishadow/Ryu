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
using Ryujinx.Audio.OpenAL;

// FIX do Switch ambíguo
using RyujinxSwitch = Ryujinx.HLE.Switch;

namespace RyujinxAndroid
{
    [Activity(Label = "DragoNX", MainLauncher = true, ScreenOrientation = ScreenOrientation.Landscape, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
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
                // FIX 1: VirtualFileSystem não tem new(), é Create()
                var vfs = VirtualFileSystem.Create();
                var keyPath = "/storage/emulated/0/Download/DragoNX/prod.keys";
                if (File.Exists(keyPath)) 
                    vfs.ImportKeys(File.ReadAllBytes(keyPath));

                // FIX 2: ContentManager + UserChannel são obrigatórios agora
                var contentManager = new ContentManager(vfs);
                var userChannel = new UserChannelPersistence();

                // FIX 3: VulkanRenderer não recebe Surface no construtor no Ryujinx novo
                var renderer = new VulkanRenderer();
                var audio = new OpenALHardwareDeviceDriver();

                // FIX 4: Switch agora tem 5 parâmetros, não 3
                emu = new RyujinxSwitch(vfs, contentManager, userChannel, renderer, audio.GetHardwareDeviceDriver());

                emu.LoadApplication(game);
                new System.Threading.Thread(() => emu.Run()).Start();
            }
            catch(Exception e)
            {
                Toast.MakeText(this, "ERRO: " + e.ToString(), ToastLength.Long).Show();
            }
        }
        public void SurfaceChanged(ISurfaceHolder h, Android.Graphics.Format f, int w, int ht) {}
        public void SurfaceDestroyed(ISurfaceHolder h) { emu?.Stop(); }
    }
}
