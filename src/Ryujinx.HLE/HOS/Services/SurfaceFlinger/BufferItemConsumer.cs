using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvMap;
using System;
using System.IO;

namespace Ryujinx.HLE.HOS.Services.SurfaceFlinger
{
    class BufferItemConsumer : ConsumerBase
    {
        private readonly GpuContext _gpuContext;

        public BufferItemConsumer(Switch device, BufferQueueConsumer consumer, uint consumerUsage, int bufferCount, bool controlledByApp, IConsumerListener listener = null) : base(consumer, controlledByApp, listener)
        {
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
                if (gb.Buffer.Surfaces == null || gb.Buffer.Surfaces.Length == 0) return;

                var surf = gb.Buffer.Surfaces[0];
                int nvHandle = surf.NvMapHandle;
                if (nvHandle == 0) nvHandle = gb.Buffer.NvMapId;
                ulong offset = (ulong)surf.Offset;

                var owner = Consumer.Core.Owner;
                NvMapHandle map = NvMapDeviceFile.GetMapFromHandle(owner, nvHandle);
                if (map == null) return;
                ulong address = (ulong)(map.Address + (long)offset);
                if (address == 0) return;

                int width = gb.Width;
                int height = gb.Height;
                int stride = width;
                bool isLinear = true;
                int gobBlocks = 1 << surf.BlockHeightLog2;
                if (gobBlocks == 0) gobBlocks = 1;

                Format fmt = Format.R8G8B8A8Unorm;
                byte bpp = 4;

                var crop = new ImageCrop(bi.Crop.Left, bi.Crop.Right, bi.Crop.Top, bi.Crop.Bottom, false, false, false, 1, 1);
                BufferItem copy = bi;
                Action<GpuContext, object> acq = (ctx, obj) => { try { ((BufferItem)obj).Fence.WaitForever(ctx); } catch {} };
                Action<object> rel = (obj) => { try { var b = (BufferItem)obj; AndroidFence f = AndroidFence.NoFence; ReleaseBuffer(b, ref f); } catch {} };

                if (_gpuContext.Window.EnqueueFrameThreadSafe(address, width, height, stride, isLinear, gobBlocks, fmt, bpp, crop, acq, rel, copy))
                    _gpuContext.Window.SignalFrameReady();
            }
            catch { }
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
