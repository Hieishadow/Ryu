using System;
using Android.App;
using Android.OS;
using Android.Content.PM;
using Android.Widget;
using Android.Graphics;
using Ryujinx.Graphics.GAL;

namespace RyujinxAndroid
{
    [Activity(Label = "Ryujinx", MainLauncher = true, ScreenOrientation = ScreenOrientation.Landscape, Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Window.AddFlags(Android.Views.WindowManagerFlags.Fullscreen | Android.Views.WindowManagerFlags.KeepScreenOn);
            var tv = new TextView(this);
            tv.SetTextColor(Color.Lime);
            tv.TextSize = 12f;
            tv.SetPadding(30,30,30,30);
            tv.SetTextIsSelectable(true);
            try
            {
                var r = new FakeRenderer();
                tv.Text = $"V18 MASTER - Build 72\nSWITCH OK!!!\n{r.GetType().FullName}\n{typeof(IRenderer).FullName}";
            }
            catch(Exception ex){ tv.Text = $"V18 FAIL\n{ex}"; }
            SetContentView(tv);
        }
        class FakeRenderer : IRenderer
        {
            public event EventHandler<ScreenCaptureImageInfo> ScreenCaptured { add{} remove{} }
            public bool PreferThreading => false;
            public IPipeline Pipeline => null!;
            public IWindow Window => null!;
            public uint ProgramCount => 0;
            public void BackgroundContextAction(Action action, bool alwaysBackground = false) => action();
            public BufferHandle CreateBuffer(int size, BufferAccess access = BufferAccess.Default) => BufferHandle.Null;
            public BufferHandle CreateBuffer(nint pointer, int size) => BufferHandle.Null;
            public BufferHandle CreateBufferSparse(ReadOnlySpan<BufferRange> storageBuffers) => BufferHandle.Null;
            public IImageArray CreateImageArray(int size, bool isBuffer) => null!;
            public IProgram CreateProgram(ShaderSource[] shaders, ShaderInfo info) => null!;
            public ISampler CreateSampler(SamplerCreateInfo info) => null!;
            public ITexture CreateTexture(TextureCreateInfo info) => null!;
            public ITextureArray CreateTextureArray(int size, bool isBuffer) => null!;
            public bool PrepareHostMapping(nint address, ulong size) => false;
            public void CreateSync(ulong id, bool strict) {}
            public void DeleteBuffer(BufferHandle buffer) {}
            public PinnedSpan<byte> GetBufferData(BufferHandle buffer, int offset, int size) => new PinnedSpan<byte>(new byte[size]);
            public Capabilities GetCapabilities() => default;
            public ulong GetCurrentSync() => 0;
            public HardwareInfo GetHardwareInfo() => default;
            public IProgram LoadProgramBinary(byte[] programBinary, bool hasFragmentShader, ShaderInfo info) => null!;
            public void SetBufferData(BufferHandle buffer, int offset, ReadOnlySpan<byte> data) {}
            public void UpdateCounters() {}
            public void PreFrame() {}
            public ICounterEvent ReportCounter(CounterType type, EventHandler<ulong> resultHandler, float divisor, bool hostReserved) => null!;
            public void ResetCounter(CounterType type) {}
            public void WaitSync(ulong id) {}
            public void Initialize(GraphicsDebugLevel logLevel) {}
            public void SetInterruptAction(Action<Action> interruptAction) {}
            public void Screenshot() {}
            public void Dispose() {}
        }
    }
}
