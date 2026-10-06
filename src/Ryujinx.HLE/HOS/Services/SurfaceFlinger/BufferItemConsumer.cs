using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.HLE.HOS;
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
            Consumer.SetConsumerUsageBits(consumerUsage);
            if (bufferCount != -1) Consumer.SetMaxAcquiredBufferCount(bufferCount);
        }

        public override void OnFrameAvailable(ref BufferItem item)
        {
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] OnFrameAvailable slot={item.Slot}\n"); } catch {}
            base.OnFrameAvailable(ref item);
            TryEnqueue();
        }

        public override void OnFrameReplaced(ref BufferItem item)
        {
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] OnFrameReplaced slot={item.Slot}\n"); } catch {}
            base.OnFrameReplaced(ref item);
            TryEnqueue();
        }

        private void TryEnqueue()
        {
            try
            {
                Status st = AcquireBuffer(out BufferItem bi, 0, true);
                try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [VI] Acquire={st}\n"); } catch {}
                if (st != Status.Success) return;

                var gb = bi.GraphicBuffer.Object;
                var surf = gb.Buffer.Surfaces[0];
                int nvHandle = surf.NvMapHandle != 0 ? surf.NvMapHandle : gb.Buffer.NvMapId;
                try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"[VI] gb={gb.Width}x{gb.Height} h={nvHandle} off={surf.Offset} cf={surf.ColorFormat}\n"); } catch {}

                AndroidFence f = AndroidFence.NoFence;
                ReleaseBuffer(bi, ref f);
            }
            catch (Exception ex)
            {
                try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"[VI] EX TryEnqueue {ex}\n"); } catch {}
            }
        }

        public Status AcquireBuffer(out BufferItem bufferItem, ulong expectedPresent, bool waitForFence = false)
        {
            lock (Lock)
            {
                Status st = AcquireBufferLocked(out BufferItem tmp, expectedPresent);
                if (st != Status.Success) { bufferItem = null; return st; }
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
