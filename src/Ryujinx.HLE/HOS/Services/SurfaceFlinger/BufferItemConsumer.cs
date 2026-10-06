using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.HLE.HOS.Services.Nv.NvMap;
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
            Status s = Consumer.SetConsumerUsageBits(consumerUsage);
            if (s!= Status.Success) throw new InvalidOperationException();
            if (bufferCount!= -1)
            {
                s = Consumer.SetMaxAcquiredBufferCount(bufferCount);
                if (s!= Status.Success) throw new InvalidOperationException();
            }
        }

        public override void OnFrameAvailable(ref BufferItem item) { base.OnFrameAvailable(ref item); TryEnqueue(); }
        public override void OnFrameReplaced(ref BufferItem item) { base.OnFrameReplaced(ref item); TryEnqueue(); }

        private void TryEnqueue()
        {
            try
            {
                if (AcquireBuffer(out BufferItem bi, 0, true)!= Status.Success) return;

                var gb = bi.GraphicBuffer.Object;
                if (gb == null || gb.Buffer.Surfaces.Length == 0) return;

                var surf = gb.Buffer.Surfaces[0];
                int nvHandle = surf.NvMapHandle;
                if (nvHandle == 0) nvHandle = gb.Buffer.NvMapId;
                ulong offset = (ulong)surf.Offset;

                // Pega o owner do layer - é KProcess
                var owner = Consumer.Core.Owner;
                NvMapHandle map = NvMapDeviceFile.GetMapFromHandle(owner, nvHandle);
                ulong address = (ulong)(map.Address + (long)offset);

                int width = gb.Width;
                int height = gb.Height;
                int stride = width; // fallback, v17 não tem stride
                bool isLinear = true;
                int gobBlocks = 1 << surf.BlockHeightLog2;
                if (gobBlocks == 0) gobBlocks = 1;

                Format fmt = ConvertColorFormat(surf.ColorFormat);
                byte bpp = (fmt == Format.B5G6R5Unorm || fmt == Format.R4G4B4A4Unorm)? (byte)2 : (byte)4;

                var crop = new ImageCrop(bi.Crop.Left, bi.Crop.Right, bi.Crop.Top, bi.Crop.Bottom, false, false, false, 1, 1);

                BufferItem copy = bi;
                Action<GpuContext, object> acq = (ctx, obj) => { try { ((BufferItem)obj).Fence.WaitForever(ctx); } catch {} };
                Action<object> rel = (obj) => { try { var b = (BufferItem)obj; AndroidFence f = AndroidFence.NoFence; ReleaseBuffer(b, ref f); } catch {} };

                try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] Enqueue addr=0x{address:X} {width}x{height} fmt={fmt}\n"); } catch {}

                if (_gpuContext.Window.EnqueueFrameThreadSafe(address, width, height, stride, isLinear, gobBlocks, fmt, bpp, crop, acq, rel, copy))
                    _gpuContext.Window.SignalFrameReady();
            }
            catch (Exception ex)
            {
                try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] EX {ex}\n"); } catch {}
            }
        }

        private static Format ConvertColorFormat(int cf)
        {
            // ColorFormat enum da v17: 1=R5G6B5, etc - mapeia simples
            return cf switch
            {
                4 => Format.R8G8B8A8Unorm,
                3 => Format.B5G6R5Unorm,
                _ => Format.R8G8B8A8Unorm,
            };
        }

        public Status AcquireBuffer(out BufferItem bufferItem, ulong expectedPresent, bool waitForFence = false)
        {
            lock (Lock)
            {
                Status st = AcquireBufferLocked(out BufferItem tmp, expectedPresent);
                if (st!= Status.Success) { bufferItem = null; return st; }
                bufferItem = (BufferItem)tmp.Clone();
                if (waitForFence) bufferItem.Fence.WaitForever(_gpuContext);
                if (!Slots[bufferItem.Slot].GraphicBuffer.IsNull)
                    bufferItem.GraphicBuffer.Set(Slots[bufferItem.Slot].GraphicBuffer);
                return Status.Success;
            }
        }

        public Status ReleaseBuffer(BufferItem bufferItem, ref AndroidFence fence)
        {
            lock (Lock)
            {
                Status r = AddReleaseFenceLocked(bufferItem.Slot, ref bufferItem.GraphicBuffer, ref fence);
                if (r == Status.Success) r = ReleaseBufferLocked(bufferItem.Slot, ref bufferItem.GraphicBuffer);
                return r;
            }
        }

        public Status SetDefaultBufferSize(uint w, uint h) { lock (Lock) { return Consumer.SetDefaultBufferSize(w, h); } }
        public Status SetDefaultBufferFormat(PixelFormat f) { lock (Lock) { return Consumer.SetDefaultBufferFormat(f); } }
    }
}
