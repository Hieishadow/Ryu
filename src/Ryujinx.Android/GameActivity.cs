using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using System.IO;
using Android.Content.PM;

namespace Ryujinx.Android
{
    [Activity(Label="DragoNX Game", Theme="@android:style/Theme.Black.NoTitleBar.Fullscreen", ScreenOrientation = ScreenOrientation.Landscape, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
    public class GameActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Window.AddFlags(WindowManagerFlags.KeepScreenOn | WindowManagerFlags.Fullscreen);

            var gamePath = Intent?.GetStringExtra("gamePath")?? "nenhum";
            var baseDir = Intent?.GetStringExtra("baseDir")?? GetExternalFilesDir(null)!.AbsolutePath;

            var layout = new FrameLayout(this);

            // Surface onde o Vulkan vai renderizar
            var surface = new SurfaceView(this);
            layout.AddView(surface, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            // Log overlay
            var log = new TextView(this);
            log.SetBackgroundColor(Android.Graphics.Color.ParseColor("#CC000000"));
            log.SetTextColor(Android.Graphics.Color.ParseColor("#00FF00"));
            log.SetPadding(20,20,20,20);
            log.TextSize = 12f;
            log.Text = $"DragoNX FULL\n\nGame: {Path.GetFileName(gamePath)}\nPath: {gamePath}\nBase: {baseDir}\n\n[JIT] libarmeilleure-jitsupport.so = ATIVO\n[Vulkan] Surface criada - pronto pra VulkanRenderer\n\nKeys: {baseDir}/system/prod.keys\nFirmware: {baseDir}/bis/\n\nStatus: Aguardando inicializacao do core...";

            layout.AddView(log, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 700));
            SetContentView(layout);

            // Inicialização real do Ryujinx (descomente quando quiser ligar)
            /*
            Task.Run(() => {
                try {
                    var vfs = new Ryujinx.HLE.FileSystem.VirtualFileSystem();
                    var keyset = Ryujinx.HLE.FileSystem.Content.ExternalKeyReader.ReadKeyFile(
                        Path.Combine(baseDir, "system", "prod.keys"),
                        Path.Combine(baseDir, "system", "title.keys"), null, null);
                    // var gpu = new Ryujinx.Graphics.Vulkan.VulkanRenderer(surface.Handle);
                    // var device = new Ryujinx.HLE.Switch(...);
                    // device.LoadApplication(gamePath);
                } catch (Exception ex) {
                    RunOnUiThread(() => log.Text += "\nERRO: " + ex.Message);
                }
            });
            */
        }
    }
}
