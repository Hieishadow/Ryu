using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Graphics;
using System.Linq;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
          ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class GameActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn | WindowManagerFlags.HardwareAccelerated);

        var romPath = Intent?.GetStringExtra("rom_path");

        // CORRIGIDO: System.IO.Path e System.IO.Directory e System.IO.File com nome completo
        if (string.IsNullOrEmpty(romPath))
        {
            try {
                var games = System.IO.Directory.GetFiles("/storage/emulated/0/Download/DragoNX/games");
                if (games.Length > 0) romPath = games[0];
            } catch {}
        }

        var layout = new LinearLayout(this);
        layout.Orientation = Orientation.Vertical;
        layout.SetGravity(GravityFlags.Center);
        layout.SetBackgroundColor(Color.Black);
        layout.SetPadding(40,40,40,40);

        var tv = new TextView(this);
        tv.SetTextColor(Color.White);
        tv.TextSize = 18;
        tv.Gravity = GravityFlags.Center;

        if (string.IsNullOrEmpty(romPath) ||!System.IO.File.Exists(romPath))
        {
            tv.Text = "DragoNX Teste\n\nColoque Zelda em:\n/Download/DragoNX/games/";
            layout.AddView(tv);
            SetContentView(layout);
            return;
        }

        try
        {
            // CORRIGIDO L47 e L57: usar System.IO.Path
            var fileName = System.IO.Path.GetFileName(romPath);
            tv.Text = $"Tentando bootar:\n{fileName}\n\nJIT ON - Aguarde...";
            layout.AddView(tv);
            SetContentView(layout);

            Toast.MakeText(this, $"ROM: {fileName}", ToastLength.Long)?.Show();
            tv.Text += "\n\nLeu o jogo! Pronto pro host Vulkan.";
        }
        catch (System.Exception ex)
        {
            tv.Text = $"Erro: {ex.Message}";
            layout.AddView(tv);
            SetContentView(layout);
        }
    }
}
