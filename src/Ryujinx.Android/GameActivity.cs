using Android.App;
using Android.OS;
using Android.Views;
using AFormat = Android.Graphics.Format;
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
    string romPath = "";
    SurfaceView? surfaceView;
    Android.Widget.TextView? logView;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
        romPath = Intent?.GetStringExtra("rom_path") ?? "/storage/emulated/0/Download/DragoNX/games/game.nsp";
        logView = new Android.Widget.TextView(this){ Text = $"DragoNX #247\n{Path.GetFileName(romPath)}" };
        logView.Gravity = GravityFlags.Center;
        SetContentView(logView);
        surfaceView = new SurfaceView(this);
        surfaceView.Holder.AddCallback(new SurfaceCallback(this));
    }

    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
    {
        GameActivity act;
        public SurfaceCallback(GameActivity a){ act = a; }
        public void SurfaceCreated(ISurfaceHolder holder)
        {
            act.RunOnUiThread(() => act.logView!.Text = "Surface criado, iniciando Vulkan...");
            new System.Threading.Thread(() => {
                try {
                    var vfs = VirtualFileSystem.CreateInstance();
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
                    bool ok = device.LoadNsp(act.romPath);
                    act.RunOnUiThread(() => {
                        if (!ok) { act.logView!.Text = $"LoadNsp falhou\n{act.romPath}"; return; }
                        act.SetContentView(act.surfaceView);
                    });
                    if (ok) { while (true) { device.ProcessFrame(); device.PresentFrame(() => {}); } }
                } catch (System.Exception ex) {
                    act.RunOnUiThread(() => {
                        var tv = new Android.Widget.TextView(act);
                        tv.Text = "CRASH:\n" + ex.ToString();
                        act.SetContentView(tv);
                    });
                }
            }).Start();
        }
        public void SurfaceChanged(ISurfaceHolder holder, AFormat format, int width, int height) {}
        public void SurfaceDestroyed(ISurfaceHolder holder) {}
    }
}
