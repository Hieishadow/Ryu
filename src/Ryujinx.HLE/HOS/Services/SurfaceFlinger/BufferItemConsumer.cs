using Ryujinx.Graphics.Gpu;
using Ryujinx.Common.Logging;
using System;

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

            if (status != Status.Success)
            {
                Logger.Error?.Print(LogClass.Gpu, $"[VI] SetConsumerUsageBits FAILED status={status}");
                throw new InvalidOperationException();
            }

            if (bufferCount != -1)
            {
                status = Consumer.SetMaxAcquiredBufferCount(bufferCount);

                if (status != Status.Success)
                {
                    Logger.Error?.Print(LogClass.Gpu, $"[VI] SetMaxAcquiredBufferCount FAILED status={status}");
                    throw new InvalidOperationException();
                }
            }
            
            Logger.Info?.Print(LogClass.Gpu, $"[VI] BufferItemConsumer created bufferCount={bufferCount} usage={consumerUsage}");
        }

        public Status AcquireBuffer(out BufferItem bufferItem, ulong expectedPresent, bool waitForFence = false)
        {
            lock (Lock)
            {
                Status status = AcquireBufferLocked(out BufferItem tmp, expectedPresent);

                if (status != Status.Success)
                {
                    // ESSE LOG VAI MOSTRAR POR QUE CELESTE CONGELA
                    Logger.Info?.Print(LogClass.Gpu, $"[VI] AcquireBuffer FAILED status={status} expectedPresent={expectedPresent} - QUEUE EMPTY!");
                    bufferItem = null;
                    return status;
                }

                // Make sure to clone the object to not temper the real instance.
                bufferItem = (BufferItem)tmp.Clone();

                Logger.Info?.Print(LogClass.Gpu, $"[VI] AcquireBuffer OK slot={bufferItem.Slot} frame={bufferItem.FrameNumber} expectedPresent={expectedPresent} isAuto={bufferItem.IsAutoTimestamp}");

                if (waitForFence)
                {
                    bufferItem.Fence.WaitForever(_gpuContext);
                }

                bufferItem.GraphicBuffer.Set(Slots[bufferItem.Slot].GraphicBuffer);

                return Status.Success;
            }
        }

        public Status ReleaseBuffer(BufferItem bufferItem, ref AndroidFence fence)
        {
            lock (Lock)
            {
                Logger.Info?.Print(LogClass.Gpu, $"[VI] ReleaseBuffer slot={bufferItem.Slot} frame={bufferItem.FrameNumber}");
                
                Status result = AddReleaseFenceLocked(bufferItem.Slot, ref bufferItem.GraphicBuffer, ref fence);

                if (result == Status.Success)
                {
                    result = ReleaseBufferLocked(bufferItem.Slot, ref bufferItem.GraphicBuffer);
                }

                if (result != Status.Success)
                {
                    Logger.Warning?.Print(LogClass.Gpu, $"[VI] ReleaseBuffer FAILED status={result} slot={bufferItem.Slot}");
                }

                return result;
            }
        }

        public Status SetDefaultBufferSize(uint width, uint height)
        {
            lock (Lock)
            {
                Logger.Info?.Print(LogClass.Gpu, $"[VI] SetDefaultBufferSize {width}x{height}");
                return Consumer.SetDefaultBufferSize(width, height);
            }
        }

        public Status SetDefaultBufferFormat(PixelFormat defaultFormat)
        {
            lock (Lock)
            {
                Logger.Info?.Print(LogClass.Gpu, $"[VI] SetDefaultBufferFormat {defaultFormat}");
                return Consumer.SetDefaultBufferFormat(defaultFormat);
            }
        }
    }
}
