using Android.App;
using Android.OS;
using Android.Views;
using Android.Graphics;
using Android.Widget;
using System.IO;

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
        if (string.IsNullOrEmpty(romPath))
        {
            // tenta achar manualmente
            var games = Directory.GetFiles("/storage/emulated/0/Download/DragoNX/games");
            if (games.Length > 0) romPath = games[0];
        }

        var layout = new LinearLayout(this);
        layout.SetBackgroundColor(Color.Black);
        layout.SetGravity(GravityFlags.Center);

        var tv = new TextView(this);
        tv.SetTextColor(Color.White);
        tv.TextSize = 16;
        tv.Gravity = GravityFlags.Center;

        if (string.IsNullOrEmpty(romPath) ||!File.Exists(romPath))
        {
            tv.Text = "DragoNX Debug - SEM JOGO\n\nColoque Links Awakening em:\n/Download/DragoNX/games/";
            layout.AddView(tv);
            SetContentView(layout);
            return;
        }

        // TENTA BOOTAR
        try
        {
            tv.Text = $"Tentando bootar:\n{Path.GetFileName(romPath)}\n\nJIT ON - Aguarde...";
            layout.AddView(tv);
            SetContentView(layout);

            // TODO: Aqui entra o Ryujinx
            // Se você já tem o Ryujinx.HLE referenciado:
            // var device = new Ryujinx.HLE.HOS.Horizon(...);
            // device.LoadApplication(romPath);

            // Por enquanto prova que achou o ROM
            Toast.MakeText(this, $"ROM encontrado: {Path.GetFileName(romPath)}", ToastLength.Long)?.Show();

            // Simula que ia iniciar Vulkan
            tv.Text += "\n\nSe chegou até aqui, o APK leu o jogo!\nAgora precisa do host Ryujinx.";
        }
        catch (System.Exception ex)
        {
            tv.Text = $"Erro ao bootar:\n{ex.Message}";
            layout.AddView(tv);
            SetContentView(layout);
        }
    }
}
