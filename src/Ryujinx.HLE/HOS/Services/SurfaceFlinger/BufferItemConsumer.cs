using Ryujinx.Graphics.Gpu;
using Ryujinx.Common.Logging;
using System;
using System.IO;

namespace Ryujinx.HLE.HOS.Services.SurfaceFlinger
{
    class BufferItemConsumer : ConsumerBase
    {
        private readonly GpuContext _gpuContext;

        public BufferItemConsumer(Switch device,
            BufferQueueConsumer consumer,
            uint consumerUsage,
            int bufferCount,
            bool controlledByApp,
            IConsumerListener listener = null) : base(consumer, controlledByApp, listener)
        {
            _gpuContext = device.Gpu;
            Status status = Consumer.SetConsumerUsageBits(consumerUsage);
            if (status!= Status.Success) throw new InvalidOperationException();
            if (bufferCount!= -1)
            {
                status = Consumer.SetMaxAcquiredBufferCount(bufferCount);
                if (status!= Status.Success) throw new InvalidOperationException();
            }
        }

        // FECHA O CAMINHO DO FRAME - override obrigatório
        public override void OnFrameAvailable(ref BufferItem item)
        {
            // Mantém comportamento original se houver listener
            base.OnFrameAvailable(ref item);

            // FORÇA o fluxo que faltava: Acquire -> EnqueueFrame -> Present
            try
            {
                if (AcquireBuffer(out BufferItem bufferItem, 0, true) == Status.Success)
                {
                    try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [FILE] [VI] OnFrameAvailable -> Acquire OK slot={bufferItem.Slot} frame={bufferItem.FrameNumber} -> EnqueueFrame\n"); } catch {}
                    _gpuContext.Window.EnqueueFrameThreadSafe(bufferItem);
                }
            }
            catch (Exception ex)
            {
                try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now:HH:mm:ss.fff} [FILE] [VI] OnFrameAvailable EX {ex}\n"); } catch {}
            }
        }

        public override void OnFrameReplaced(ref BufferItem item)
        {
            base.OnFrameReplaced(ref item);
            // Mesmo fluxo pro replace (Celeste usa muito)
            try
            {
                if (AcquireBuffer(out BufferItem bufferItem, 0, true) == Status.Success)
                {
                    _gpuContext.Window.EnqueueFrameThreadSafe(bufferItem);
                }
            }
            catch {}
        }

        public Status AcquireBuffer(out BufferItem bufferItem, ulong expectedPresent, bool waitForFence = false)
        {
            lock (Lock)
            {
                Status status = AcquireBufferLocked(out BufferItem tmp, expectedPresent);
                if (status!= Status.Success)
                {
                    bufferItem = null;
                    return status;
                }
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
