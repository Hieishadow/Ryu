using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.Graphics.Gpu.Memory;
using Ryujinx.Graphics.Texture;
using Ryujinx.Memory.Range;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Ryujinx.Graphics.Gpu
{
    public class Window
    {
        private readonly GpuContext _context;
        private readonly struct PresentationTexture
        {
            public TextureCache Cache { get; }
            public TextureInfo Info { get; }
            public MultiRange Range { get; }
            public ImageCrop Crop { get; }
            public Action<GpuContext, object> AcquireCallback { get; }
            public Action<object> ReleaseCallback { get; }
            public object UserObj { get; }
            public PresentationTexture(TextureCache cache, TextureInfo info, MultiRange range, ImageCrop crop, Action<GpuContext, object> acquireCallback, Action<object> releaseCallback, object userObj)
            { Cache=cache; Info=info; Range=range; Crop=crop; AcquireCallback=acquireCallback; ReleaseCallback=releaseCallback; UserObj=userObj; }
        }
        private readonly ConcurrentQueue<PresentationTexture> _frameQueue;
        private int _framesAvailable;
        public bool IsFrameAvailable => _framesAvailable != 0;
        public Window(GpuContext context) { _context = context; _frameQueue = new ConcurrentQueue<PresentationTexture>(); }

        // NOVO - chamado pelo BufferItemConsumer fix
        public bool EnqueueFrameThreadSafe(ulong address, int width, int height, int stride, bool isLinear, int gobBlocksInY, Format format, byte bytesPerPixel, ImageCrop crop, Action<GpuContext, object> acquireCallback, Action<object> releaseCallback, object userObj)
        {
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [GPU.WINDOW] Enqueue NEW addr=0x{address:X} {width}x{height}\n"); } catch {}
            TextureCache cache = null;
            foreach(var pm in _context.PhysicalMemoryRegistry.Values) { cache = pm.TextureCache; break; }
            if (cache == null) return false;
            FormatInfo fi = new(format,1,1,bytesPerPixel,4);
            TextureInfo info = new(0UL,width,height,1,1,1,1,stride,isLinear,gobBlocksInY,1,1,Target.Texture2D,fi);
            int size = SizeCalculator.GetBlockLinearTextureSize(width,height,1,1,1,1,1,bytesPerPixel,gobBlocksInY,1,1).TotalSize;
            MultiRange range = new(address,(ulong)size);
            _frameQueue.Enqueue(new PresentationTexture(cache,info,range,crop,acquireCallback,releaseCallback,userObj));
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [GPU.WINDOW] FRAME ENQUEUED queue={_frameQueue.Count}\n"); } catch {}
            return true;
        }

        // ANTIGO - mantido
        public bool EnqueueFrameThreadSafe(ulong pid, ulong address, int width, int height, int stride, bool isLinear, int gobBlocksInY, Format format, byte bytesPerPixel, ImageCrop crop, Action<GpuContext, object> acquireCallback, Action<object> releaseCallback, object userObj)
        {
            if (!_context.PhysicalMemoryRegistry.TryGetValue(pid, out PhysicalMemory pm)) return false;
            FormatInfo fi = new(format,1,1,bytesPerPixel,4);
            TextureInfo info = new(0UL,width,height,1,1,1,1,stride,isLinear,gobBlocksInY,1,1,Target.Texture2D,fi);
            int size = SizeCalculator.GetBlockLinearTextureSize(width,height,1,1,1,1,1,bytesPerPixel,gobBlocksInY,1,1).TotalSize;
            MultiRange range = new(address,(ulong)size);
            _frameQueue.Enqueue(new PresentationTexture(pm.TextureCache,info,range,crop,acquireCallback,releaseCallback,userObj));
            return true;
        }

        public void Present(Action swapBuffersCallback)
        {
            _context.AdvanceSequence();
            if (_frameQueue.TryDequeue(out PresentationTexture pt))
            {
                pt.AcquireCallback(_context, pt.UserObj);
                var tex = pt.Cache.FindOrCreateTexture(null, TextureSearchFlags.WithUpscale, pt.Info, 0, range: pt.Range);
                pt.Cache.Tick(); tex.SynchronizeMemory();
                var crop = new ImageCrop((int)(pt.Crop.Left*tex.ScaleFactor),(int)MathF.Ceiling(pt.Crop.Right*tex.ScaleFactor),(int)(pt.Crop.Top*tex.ScaleFactor),(int)MathF.Ceiling(pt.Crop.Bottom*tex.ScaleFactor),pt.Crop.FlipX,pt.Crop.FlipY,pt.Crop.IsStretched,pt.Crop.AspectRatioX,pt.Crop.AspectRatioY);
                _context.Renderer.Window.Present(tex.HostTexture,crop,swapBuffersCallback);
                pt.ReleaseCallback(pt.UserObj);
            }
        }
        public void SignalFrameReady() => Interlocked.Increment(ref _framesAvailable);
        public bool ConsumeFrameAvailable(){ if(Interlocked.CompareExchange(ref _framesAvailable,0,0)!=0){Interlocked.Decrement(ref _framesAvailable);return true;}return false;}
    }
}
