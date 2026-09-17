using Android.App; using Android.OS; using Android.Widget; using Android.Views; using System.IO;
namespace Ryujinx.Android
{
    [Activity(Label="Game", Theme="@android:style/Theme.Black.NoTitleBar.Fullscreen")]
    public class GameActivity : Activity
    {
        protected override void OnCreate(Bundle? b){
            base.OnCreate(b);
            Window.AddFlags(WindowManagerFlags.KeepScreenOn | WindowManagerFlags.Fullscreen);
            var game=Intent?.GetStringExtra("gamePath")??"";
            var baseDir=Intent?.GetStringExtra("baseDir")??"";
            
            var layout=new FrameLayout(this);
            var surface=new SurfaceView(this); // Aqui entra VulkanRenderer
            layout.AddView(surface, new FrameLayout.LayoutParams(-1,-1));
            
            var log=new TextView(this);
            log.SetBackgroundColor(Android.Graphics.Color.ParseColor("#80000000"));
            log.SetTextColor(Android.Graphics.Color.Green);
            log.Text=$"DragoNX\nGame:{Path.GetFileName(game)}\nBase:{baseDir}\n\nJIT: Ativo (libarmeilleure-jitsupport.so)\nVulkan: Surface criada\n\nProximo passo: inicializar Switch context:\nvar vfs=new VirtualFileSystem();\nvar keys=ExternalKeyReader.ReadKeyFile(prod.keys);\nvar gpu=new VulkanRenderer(surface.Handle);\nvar emu=new Switch(gpu,...);\nemu.LoadApplication(game);";
            layout.AddView(log, new FrameLayout.LayoutParams(-1,600));
            SetContentView(layout);
        }
    }
}
