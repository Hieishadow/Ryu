using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu;
using Ryujinx.Graphics.Gpu.Image;
using System;
using System.IO;

namespace Ryujinx.HLE.HOS.Services.SurfaceFlinger
{
    class BufferItemConsumer : ConsumerBase
    {
        private readonly GpuContext _gpuContext;
        private readonly Switch _device;

        public BufferItemConsumer(Switch device,
            BufferQueueConsumer consumer,
            uint consumerUsage,
            int bufferCount,
            bool controlledByApp,
            IConsumerListener listener = null) : base(consumer, controlledByApp, listener)
        {
            _device = device;
            _gpuContext = device.Gpu;
            Status status = Consumer.SetConsumerUsageBits(consumerUsage);
            if (status!= Status.Success) throw new InvalidOperationException();
            if (bufferCount!= -1)
            {
                status = Consumer.SetMaxAcquiredBufferCount(bufferCount);
                if (status!= Status.Success) throw new InvalidOperationException();
            }
        }

        public override void OnFrameAvailable(ref BufferItem item)
        {
            base.OnFrameAvailable(ref item);
            TryAcquireAndEnqueue();
        }

        public override void OnFrameReplaced(ref BufferItem item)
        {
            base.OnFrameReplaced(ref item);
            TryAcquireAndEnqueue();
        }

        private void TryAcquireAndEnqueue()
        {
            try
            {
                if (AcquireBuffer(out BufferItem bufferItem, 0, true)!= Status.Success)
                    return;

                var gb = bufferItem.GraphicBuffer.Object;
                if (gb == null) return;

                // Pega a superfície NvMap
                var surface = gb.Buffer.Surfaces[0];
                ulong address = surface.Address;
                // Na v17, pid é o Owner do Core (processo dono do buffer)
                ulong pid = 0;
                try { pid = Consumer.Core.Owner; }
                catch {
                    try { pid = (ulong)gb.Buffer.Surfaces[0].NvMapHandle; } catch {}
                }

                // Se não conseguir pid, tenta pegar o primeiro registrado (Celeste só tem 1 processo)
                if (pid == 0 && _gpuContext.PhysicalMemoryRegistry.Count > 0)
                {
                    foreach(var k in _gpuContext.PhysicalMemoryRegistry.Keys) { pid = k; break; }
                }

                int width = gb.Width;
                int height = gb.Height;
                int stride = surface.Stride!= 0? surface.Stride : width;
                bool isLinear = surface.Kind == 0 || surface.Kind == 1; // Generic ou Pitch
                int gobBlocksInY = surface.BlockHeightLog2!= 0? (1 << surface.BlockHeightLog2) : 1;
                if (gobBlocksInY == 0) gobBlocksInY = 1;

                // Converte PixelFormat -> GAL Format
                Format format = ConvertFormat(gb.Format);
                byte bpp = GetBytesPerPixel(gb.Format);

                var crop = new ImageCrop(
                    bufferItem.Crop.Left,
                    bufferItem.Crop.Right,
                    bufferItem.Crop.Top,
                    bufferItem.Crop.Bottom,
                    false, false, false, 1, 1
                );

                // Callbacks que o Window espera
                BufferItem biCopy = bufferItem; // captura pra lambda
                Action<GpuContext, object> acquireCallback = (ctx, obj) => {
                    try { ((BufferItem)obj).Fence.WaitForever(ctx); } catch {}
                };
                Action<object> releaseCallback = (obj) => {
                    try {
                        BufferItem bi = (BufferItem)obj;
                        AndroidFence fence = AndroidFence.NoFence;
                        ReleaseBuffer(bi, ref fence);
                    } catch {}
                };

                try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] OnFrameAvailable -> Enqueue pid={pid} addr=0x{address:X} {width}x{height} fmt={format}\n"); } catch {}

                bool ok = _gpuContext.Window.EnqueueFrameThreadSafe(
                    pid, address, width, height, stride, isLinear, gobBlocksInY,
                    format, bpp, crop,
                    acquireCallback, releaseCallback, biCopy
                );

                if (ok) _gpuContext.Window.SignalFrameReady();
            }
            catch (Exception ex)
            {
                try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] TryEnqueue EX {ex}\n"); } catch {}
            }
        }

        private static Format ConvertFormat(PixelFormat pf)
        {
            return pf switch
            {
                PixelFormat.Rgba8888 => Format.R8G8B8A8Unorm,
                PixelFormat.Rgbx8888 => Format.R8G8B8A8Unorm,
                PixelFormat.Rgb565 => Format.R5G6B5Unorm,
                PixelFormat.Rgba5551 => Format.R5G5B5A1Unorm,
                PixelFormat.Rgba4444 => Format.R4G4B4A4Unorm,
                _ => Format.R8G8B8A8Unorm,
            };
        }

        private static byte GetBytesPerPixel(PixelFormat pf)
        {
            return pf switch
            {
                PixelFormat.Rgb565 => 2,
                PixelFormat.Rgba5551 => 2,
                PixelFormat.Rgba4444 => 2,
                _ => 4,
            };
        }

        public Status AcquireBuffer(out BufferItem bufferItem, ulong expectedPresent, bool waitForFence = false)
        {
            lock (Lock)
            {
                Status status = AcquireBufferLocked(out BufferItem tmp, expectedPresent);
                if (status!= Status.Success) { bufferItem = null; return status; }
                bufferItem = (BufferItem)tmp.Clone();
                if (waitForFence) bufferItem.Fence.WaitForever(_gpuContext);
                bufferItem.GraphicBuffer.Set(Slots[bufferItem.Slot].GraphicBuffer);
                return Status.Success;
            }
        }

        public Status ReleaseBuffer(BufferItem bufferItem, ref AndroidFence fence)
        {
            lock (Lock)
            {
                Status result = AddReleaseFenceLocked(bufferItem.Slot, ref bufferItem.GraphicBuffer, ref fence);
                if (result == Status.Success) result = ReleaseBufferLocked(bufferItem.Slot, ref bufferItem.GraphicBuffer);
                return result;
            }
        }

        public Status SetDefaultBufferSize(uint width, uint height) { lock (Lock) { return Consumer.SetDefaultBufferSize(width, height); } }
        public Status SetDefaultBufferFormat(PixelFormat defaultFormat) { lock (Lock) { return Consumer.SetDefaultBufferFormat(defaultFormat); } }
    }
}
