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
public class GameActivity : Activity, ISurfaceHolderCallback
{
    string romPath = "";
    ISurfaceHolder? holder;
    SurfaceView? surfaceView;
    Android.Widget.TextView? log;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

        romPath = Intent?.GetStringExtra("rom_path") ?? "/storage/emulated/0/Download/DragoNX/games/game.nsp";

        log = new Android.Widget.TextView(this){ Text = $"DragoNX #245\n{Path.GetFileName(romPath)}" };
        log.Gravity = GravityFlags.Center;
        SetContentView(log);

        surfaceView = new SurfaceView(this);
        holder = surfaceView.Holder;
        holder.AddCallback(this);
    }

    public void SurfaceCreated(ISurfaceHolder holder)
    {
        log!.Text = "Surface criado, iniciando Vulkan...";
        
        new System.Threading.Thread(() => {
            try {
                var vfs = VirtualFileSystem.CreateInstance();
                
                // CRIA VULKAN COM A JANELA REAL DO ANDROID
                var gpu = VulkanRenderer.Create("DragoNX", (instance, vk) => {
                    var ci = new AndroidSurfaceCreateInfoKHR{
                        SType = StructureType.AndroidSurfaceCreateInfoKhr,
                        Window = holder.Surface!.Handle
                    };
                    vk.CreateAndroidSurface(instance, ci, null, out var surf).CheckResult();
                    return surf;
                }, () => new[] { "VK_KHR_surface", "VK_KHR_android_surface" });

                var audio = new DummyHardwareDeviceDriver();
                var prop = typeof(HleConfiguration).GetProperty("UserChannelPersistence")!;
                var userChannel = Activator.CreateInstance(prop.PropertyType, true)!;
                var memConfig = (MemoryConfiguration)Activator.CreateInstance(typeof(MemoryConfiguration), true)!;

                var hleConfigObj = new HleConfiguration(
                    memConfig, SystemLanguage.AmericanEnglish, RegionCode.USA, VSyncMode.Switch,
                    true, true, 1, true, IntegrityCheckLevel.None, 0, 0, "UTC",
                    MemoryManagerMode.SoftwarePageTable, true, AspectRatio.Fixed16x9, 1f, false, "",
                    MultiplayerMode.Disabled, false, "", "", false, 0, false, 60, null
                );
                var configure = typeof(HleConfiguration).GetMethod("Configure")!;
                var hleConfig = (HleConfiguration)configure.Invoke(hleConfigObj, new object[]{ vfs, null!, null!, null!, userChannel, gpu, audio, null! })!;

                var device = new Ryujinx.HLE.Switch(hleConfig);
                bool ok = device.LoadNsp(romPath);
                
                RunOnUiThread(() => {
                    if (!ok) { log!.Text = $"LoadNsp falhou\n{romPath}"; return; }
                    SetContentView(surfaceView);
                });

                if (ok) {
                    while (true) {
                        device.ProcessFrame();
                        device.PresentFrame(() => {});
                    }
                }
            } catch (Exception ex) {
                RunOnUiThread(() => {
                    var tv = new Android.Widget.TextView(this);
                    tv.Text = "CRASH RENDER:\n" + ex.ToString();
                    SetContentView(tv);
                });
            }
        }).Start();
    }

    public void SurfaceChanged(ISurfaceHolder h, Format f, int w, int ht) {}
    public void SurfaceDestroyed(ISurfaceHolder h) {}
}
