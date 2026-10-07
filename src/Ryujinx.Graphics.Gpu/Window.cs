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
        private long _presentCounter = 0;
        public bool IsFrameAvailable => _framesAvailable != 0;
        public Window(GpuContext context) { _context = context; _frameQueue = new ConcurrentQueue<PresentationTexture>(); }

        void Log(string s){
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} {s}\n"); } catch {}
        }

        public bool EnqueueFrameThreadSafe(ulong address, int width, int height, int stride, bool isLinear, int gobBlocksInY, Format format, byte bytesPerPixel, ImageCrop crop, Action<GpuContext, object> acquireCallback, Action<object> releaseCallback, object userObj)
        {
            Log($"[GPU.WINDOW] Enqueue NEW addr=0x{address:X} {width}x{height} fmt={format} queue={_frameQueue.Count}");
            TextureCache cache = null;
            foreach(var pm in _context.PhysicalMemoryRegistry.Values) { cache = pm.TextureCache; break; }
            if (cache == null){ Log("[GPU.WINDOW] Enqueue FAIL cache null"); return false; }
            FormatInfo fi = new(format,1,1,bytesPerPixel,4);
            TextureInfo info = new(0UL,width,height,1,1,1,1,stride,isLinear,gobBlocksInY,1,1,Target.Texture2D,fi);
            int size = SizeCalculator.GetBlockLinearTextureSize(width,height,1,1,1,1,1,bytesPerPixel,gobBlocksInY,1,1).TotalSize;
            MultiRange range = new(address,(ulong)size);
            _frameQueue.Enqueue(new PresentationTexture(cache,info,range,crop,acquireCallback,releaseCallback,userObj));
            Log($"[GPU.WINDOW] FRAME ENQUEUED OK queue={_frameQueue.Count}");
            return true;
        }

        public bool EnqueueFrameThreadSafe(ulong pid, ulong address, int width, int height, int stride, bool isLinear, int gobBlocksInY, Format format, byte bytesPerPixel, ImageCrop crop, Action<GpuContext, object> acquireCallback, Action<object> releaseCallback, object userObj)
        {
            PhysicalMemory pm = null;
            if (!_context.PhysicalMemoryRegistry.TryGetValue(pid, out pm))
            {
                Log($"[GPU.WINDOW] Enqueue OLD pid=0x{pid:X} not found -> fallback to first cache");
                foreach (var p in _context.PhysicalMemoryRegistry.Values)
                {
                    pm = p;
                    break;
                }
            }
            if (pm == null)
            {
                Log("[GPU.WINDOW] Enqueue OLD FAIL no cache at all");
                return false;
            }
            FormatInfo fi = new(format, 1, 1, bytesPerPixel, 4);
            TextureInfo info = new(0UL, width, height, 1, 1, 1, 1, stride, isLinear, gobBlocksInY, 1, 1, Target.Texture2D, fi);
            int size = SizeCalculator.GetBlockLinearTextureSize(width, height, 1, 1, 1, 1, 1, bytesPerPixel, gobBlocksInY, 1, 1).TotalSize;
            MultiRange range = new(address, (ulong)size);
            _frameQueue.Enqueue(new PresentationTexture(pm.TextureCache, info, range, crop, acquireCallback, releaseCallback, userObj));
            Log($"[GPU.WINDOW] Enqueue OLD OK {width}x{height} pid=0x{pid:X} queue={_frameQueue.Count}");
            return true;
        }

        public void Present(Action swapBuffersCallback)
        {
            _context.AdvanceSequence();
            _presentCounter++;
            bool hasFrame = _frameQueue.TryDequeue(out PresentationTexture pt);

            if (!hasFrame)
            {
                if(_presentCounter % 60 == 1) Log($"[GPU.WINDOW] Present EMPTY queue=0 -> MAGENTA fallback #{_presentCounter}");
                try {
                    _context.Renderer.Window.Present(null, new ImageCrop(0,0,0,0,false,false,false,1,1), swapBuffersCallback);
                } catch {
                    swapBuffersCallback?.Invoke();
                }
                return;
            }

            try
            {
                Log($"[GPU.WINDOW] Present DEQUEUE addr=0x{pt.Range.Address:X} {pt.Info.Width}x{pt.Info.Height} queueLeft={_frameQueue.Count}");
                pt.AcquireCallback(_context, pt.UserObj);
                var tex = pt.Cache.FindOrCreateTexture(null, TextureSearchFlags.WithUpscale, pt.Info, 0, range: pt.Range);
                
                if(tex == null || tex.HostTexture == null)
                {
                    Log($"[GPU.WINDOW] FindOrCreateTexture FAIL null -> MAGENTA");
                    _context.Renderer.Window.Present(null, pt.Crop, swapBuffersCallback);
                    pt.ReleaseCallback(pt.UserObj);
                    return;
                }

                pt.Cache.Tick(); 
                tex.SynchronizeMemory();
                var crop = new ImageCrop((int)(pt.Crop.Left*tex.ScaleFactor),(int)MathF.Ceiling(pt.Crop.Right*tex.ScaleFactor),(int)(pt.Crop.Top*tex.ScaleFactor),(int)MathF.Ceiling(pt.Crop.Bottom*tex.ScaleFactor),pt.Crop.FlipX,pt.Crop.FlipY,pt.Crop.IsStretched,pt.Crop.AspectRatioX,pt.Crop.AspectRatioY);
                
                Log($"[GPU.WINDOW] Present OK host={tex.HostTexture} {tex.Info.Width}x{tex.Info.Height}");
                _context.Renderer.Window.Present(tex.HostTexture,crop,swapBuffersCallback);
                pt.ReleaseCallback(pt.UserObj);
            }
            catch(Exception ex)
            {
                Log($"[GPU.WINDOW] Present EX {ex.Message} -> MAGENTA");
                try { _context.Renderer.Window.Present(null, new ImageCrop(0,0,0,0,false,false,false,1,1), swapBuffersCallback); } catch { swapBuffersCallback?.Invoke(); }
            }
        }
        public void SignalFrameReady() => Interlocked.Increment(ref _framesAvailable);
        public bool ConsumeFrameAvailable(){ if(Interlocked.CompareExchange(ref _framesAvailable,0,0)!=0){Interlocked.Decrement(ref _framesAvailable);return true;}return false;}
    }
}
