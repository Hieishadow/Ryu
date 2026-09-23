using System;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel.Types;
using Ryujinx.HLE.HOS.Services.Nv.Types;
using Ryujinx.Common.Logging;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvChannel
    {
        public int Timeout;
        public int SubmitTimeout;
        public int Timeslice;

        private readonly ServiceCtx _context;

        private static void FLog(string s)
        {
            try
            {
                System.IO.File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_present.txt",
                DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\n");
            }
            catch { }
        }

        public NvChannel(ServiceCtx context)
        {
            _context = context;
            FLog("[CHANNEL] NvChannel REAL");
        }

        public NvResult SubmitGpfifo(ref SubmitGpfifoArguments args)
        {
            FLog($"[GPFIFO] SubmitGpfifo REAL Addr=0x{args.Address:X} Num={args.NumEntries} Flags={args.Flags} FenceId={args.Fence.Id} Val={args.Fence.Value}");

            var gpu = _context.Device.Gpu;

            try
            {
                if ((args.Flags & SubmitGpfifoFlags.FenceWait) != 0 && args.Fence.IsValid())
                {
                    FLog($"[SYNC] FenceWait Id={args.Fence.Id}");
                    args.Fence.Wait(gpu, TimeSpan.FromSeconds(1));
                }

                FLog($"[GPFIFO] Processando {args.NumEntries} entradas");

                if ((args.Flags & SubmitGpfifoFlags.FenceIncrement) != 0 && args.Fence.IsValid())
                {
                    FLog($"[SYNC] FenceIncrement ANTES Id={args.Fence.Id} Val={args.Fence.Value}");
                    args.Fence.Increment(gpu);
                    FLog($"[SYNC] FenceIncrement DEPOIS Id={args.Fence.Id} Val={args.Fence.Value}");
                }

                return NvResult.Success;
            }
            catch (Exception ex)
            {
                FLog($"[GPFIFO] EXCEPTION {ex}");
                return NvResult.InvalidState;
            }
        }

        public NvResult AllocGpfifoEx(ref AllocGpfifoExArguments args)
        {
            FLog($"[GPFIFO] AllocGpfifoEx Num={args.NumEntries}");
            return NvResult.Success;
        }

        public NvResult MapCommandBuffer(ref MapCommandBufferArguments args)
        {
            FLog("[GPFIFO] MapCommandBuffer");
            return NvResult.Success;
        }

        public NvResult QueryEvent(out int eventHandle, uint eventId)
        {
            eventHandle = _context.Device.Gpu.Synchronization.CreateSyncpointEvent(eventId);
            FLog($"[SYNC] QueryEvent id={eventId} handle={eventHandle}");
            return NvResult.Success;
        }
    }
}
