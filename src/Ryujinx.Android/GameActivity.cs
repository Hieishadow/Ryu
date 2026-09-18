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
using System.Runtime.InteropServices;
using VkResult = Silk.NET.Vulkan.Result;

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
        
        logView = new Android.Widget.TextView(this){ Text = $"DragoNX #255 REAL VULKAN\n{Path.GetFileName(romPath)}" };
        logView.Gravity = GravityFlags.Center;
        surfaceView = new SurfaceView(this);
        
        var layout = new Android.Widget.FrameLayout(this);
        layout.AddView(surfaceView, new Android.Widget.FrameLayout.LayoutParams(-1,-1));
        layout.AddView(logView, new Android.Widget.FrameLayout.LayoutParams(-1,-1));
        
        SetContentView(layout);
        surfaceView.Holder!.AddCallback(new SurfaceCallback(this));
    }

    class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
    {
        GameActivity act;
        public SurfaceCallback(GameActivity a){ act = a; }
        unsafe delegate VkResult CreateAndroidSurfaceDelegate(Instance instance, AndroidSurfaceCreateInfoKHR* pCreateInfo, AllocationCallbacks* pAllocator, SurfaceKHR* pSurface);

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            act.RunOnUiThread(() => act.logView!.Text = "Surface criado, Vulkan REAL #255...");
            new System.Threading.Thread(() => {
                try {
                    var vfs = VirtualFileSystem.CreateInstance();
                    var gpu = VulkanRenderer.Create("DragoNX", (instance, vk) => {
                        unsafe {
                            SurfaceKHR surface;
                            var createInfo = new AndroidSurfaceCreateInfoKHR{
                                SType = StructureType.AndroidSurfaceCreateInfoKhr,
                                Window = (nint*)holder.Surface!.Handle
                            };
                            var funcPtr = vk.GetInstanceProcAddr(instance, "vkCreateAndroidSurfaceKHR");
                            if (funcPtr == IntPtr.Zero) throw new Exception("vkCreateAndroidSurfaceKHR not found");
                            var func = Marshal.GetDelegateForFunctionPointer<CreateAndroidSurfaceDelegate>(funcPtr);
                            VkResult res = func(instance, &createInfo, null, &surface);
                            if (res != VkResult.Success) throw new Exception($"vkCreateAndroidSurfaceKHR falhou: {res}");
                            return surface;
                        }
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
                        act.logView!.Visibility = ViewStates.Gone;
                    });
                    if (ok) { while (true) { device.ProcessFrame(); device.PresentFrame(() => {}); } }
                } catch (Exception ex) {
                    act.RunOnUiThread(() => {
                        var tv = new Android.Widget.TextView(act);
                        tv.Text = "CRASH #255 REAL:\n" + ex.ToString();
                        act.SetContentView(tv);
                    });
                }
            }).Start();
        }
        public void SurfaceChanged(ISurfaceHolder holder, AFormat format, int width, int height) {}
        public void SurfaceDestroyed(ISurfaceHolder holder) {}
    }
}
