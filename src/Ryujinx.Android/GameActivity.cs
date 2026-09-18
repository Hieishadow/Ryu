using Android.App;
using Android.OS;
using Android.Views;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.Audio.Backends.Dummy;
using LibHac.Tools.FsSystem;
using Silk.NET.Vulkan;
using System;
using System.IO;

namespace DragoNX;

[Activity(Label = "DragoNX", Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
public class GameActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        var romPath = Intent?.GetStringExtra("rom_path") ?? "/storage/emulated/0/Download/DragoNX/games/game.nsp";
        var log = new Android.Widget.TextView(this){ Text = $"DragoNX bootando:\n{Path.GetFileName(romPath)}..." };
        log.Gravity = GravityFlags.Center;
        SetContentView(log);

        new System.Threading.Thread(() => {
            try {
                var vfs = VirtualFileSystem.CreateInstance();
                var gpu = VulkanRenderer.Create("", (i, vk) => new SurfaceKHR(), () => new[] { "VK_KHR_surface", "VK_KHR_android_surface" });
                var audio = new DummyHardwareDeviceDriver();
                
                // Pega UserChannelPersistence por reflexão - não precisa do nome
                var prop = typeof(HleConfiguration).GetProperty("UserChannelPersistence")!;
                var userChannel = Activator.CreateInstance(prop.PropertyType, true)!;

                var memConfig = (MemoryConfiguration)Activator.CreateInstance(typeof(MemoryConfiguration), true)!;

                var hleConfigObj = new HleConfiguration(
                    memConfig,
                    SystemLanguage.AmericanEnglish,
                    RegionCode.USA,
                    VSyncMode.Switch,
                    true, true, 1, true,
                    IntegrityCheckLevel.None, 0, 0, "UTC",
                    MemoryManagerMode.SoftwarePageTable,
                    true, AspectRatio.Fixed16x9, 1f, false, "",
                    MultiplayerMode.Disabled, false, "", "",
                    false, 0, false, 60, null
                );
                
                // Chama Configure por reflexão também
                var configure = typeof(HleConfiguration).GetMethod("Configure")!;
                var hleConfig = (HleConfiguration)configure.Invoke(hleConfigObj, new object[]{ vfs, null!, null!, null!, userChannel, gpu, audio, null! })!;

                var device = new Ryujinx.HLE.Switch(hleConfig);

                RunOnUiThread(() => log.Text = $"LoadNsp: {Path.GetFileName(romPath)}");
                bool ok = device.LoadNsp(romPath);

                RunOnUiThread(() => {
                    if (!ok) { log.Text = $"LoadNsp falhou!\n{romPath}"; return; }
                    var sv = new SurfaceView(this);
                    SetContentView(sv);
                    Android.Widget.Toast.MakeText(this, "ZELDA LOADOU!", Android.Widget.ToastLength.Long)!.Show();
                });

                while (ok) {
                    device.ProcessFrame();
                    device.PresentFrame(() => {});
                }
            } catch (Exception ex) {
                RunOnUiThread(() => {
                    var tv = new Android.Widget.TextView(this);
                    tv.Text = "CRASH GAME:\n" + ex.ToString();
                    SetContentView(tv);
                });
            }
        }).Start();
    }
}
