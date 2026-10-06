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
                Console.WriteLine($"[FILE] [VI] SetConsumerUsageBits FAILED status={status}");
                throw new InvalidOperationException();
            }

            if (bufferCount != -1)
            {
                status = Consumer.SetMaxAcquiredBufferCount(bufferCount);

                if (status != Status.Success)
                {
                    Console.WriteLine($"[FILE] [VI] SetMaxAcquiredBufferCount FAILED status={status}");
                    throw new InvalidOperationException();
                }
            }
            
            Console.WriteLine($"[FILE] [VI] BufferItemConsumer created bufferCount={bufferCount} usage={consumerUsage}");
        }

        public Status AcquireBuffer(out BufferItem bufferItem, ulong expectedPresent, bool waitForFence = false)
        {
            lock (Lock)
            {
                Status status = AcquireBufferLocked(out BufferItem tmp, expectedPresent);

                if (status != Status.Success)
                {
                    Console.WriteLine($"[FILE] [VI] AcquireBuffer FAILED status={status} expectedPresent={expectedPresent} - QUEUE EMPTY!");
                    bufferItem = null;
                    return status;
                }

                bufferItem = (BufferItem)tmp.Clone();

                Console.WriteLine($"[FILE] [VI] AcquireBuffer OK slot={bufferItem.Slot} frame={bufferItem.FrameNumber} expectedPresent={expectedPresent} isAuto={bufferItem.IsAutoTimestamp}");

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
                Console.WriteLine($"[FILE] [VI] ReleaseBuffer slot={bufferItem.Slot} frame={bufferItem.FrameNumber}");
                
                Status result = AddReleaseFenceLocked(bufferItem.Slot, ref bufferItem.GraphicBuffer, ref fence);

                if (result == Status.Success)
                {
                    result = ReleaseBufferLocked(bufferItem.Slot, ref bufferItem.GraphicBuffer);
                }

                if (result != Status.Success)
                {
                    Console.WriteLine($"[FILE] [VI] ReleaseBuffer FAILED status={result} slot={bufferItem.Slot}");
                }

                return result;
            }
        }

        public Status SetDefaultBufferSize(uint width, uint height)
        {
            lock (Lock)
            {
                Console.WriteLine($"[FILE] [VI] SetDefaultBufferSize {width}x{height}");
                return Consumer.SetDefaultBufferSize(width, height);
            }
        }

        public Status SetDefaultBufferFormat(PixelFormat defaultFormat)
        {
            lock (Lock)
            {
                Console.WriteLine($"[FILE] [VI] SetDefaultBufferFormat {defaultFormat}");
                return Consumer.SetDefaultBufferFormat(defaultFormat);
            }
        }
    }
}
