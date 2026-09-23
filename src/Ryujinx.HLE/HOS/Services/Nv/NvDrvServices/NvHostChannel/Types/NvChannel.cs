using System;
using System.IO;
using Ryujinx.Graphics.Gpu.Memory;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel.Types;
using Ryujinx.HLE.HOS.Services.Nv.Types;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvChannel
    {
        private readonly ServiceCtx _context;
        private MemoryManager _gmm;

        private static void FLog(string s)
        {
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_present.txt", DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\n"); } catch { }
        }

        public NvChannel(ServiceCtx context)
        {
            _context = context;
            FLog("[CHANNEL] CRIADO");
        }

        public NvInternalResult BindMemory(MemoryManager gmm)
        {
            _gmm = gmm;
            FLog("[CHANNEL] BindMemory Gmm OK");
            return NvInternalResult.Success;
        }

        public NvInternalResult SubmitGpfifo(ref SubmitGpfifoArguments args)
        {
            FLog($"[CHANNEL] SubmitGpfifo Fence {args.Fence.Id}:{args.Fence.Value} Flags={args.Flags} Entries={args.NumEntries}");
            try
            {
                if ((args.Flags & SubmitGpfifoFlags.FenceWait) != 0 && args.Fence.IsValid())
                {
                    FLog($"[CHANNEL] FenceWait {args.Fence.Value}");
                    args.Fence.Wait(_context.Device.Gpu, TimeSpan.FromSeconds(1));
                }

                if ((args.Flags & SubmitGpfifoFlags.FenceIncrement) != 0 && args.Fence.IsValid())
                {
                    FLog($"[CHANNEL] FenceIncrement ANTES {args.Fence.Value}");
                    args.Fence.Increment(_context.Device.Gpu);
                    FLog($"[CHANNEL] FenceIncrement DEPOIS {args.Fence.Value} -> LIBEROU PRESENT");
                }
            }
            catch (Exception ex)
            {
                FLog("[CHANNEL] EX " + ex.Message);
            }
            return NvInternalResult.Success;
        }

        public NvInternalResult AllocGpfifoEx(ref AllocGpfifoExArguments args)
        {
            return NvInternalResult.Success;
        }

        public NvInternalResult MapCommandBuffer(ref MapCommandBufferArguments args)
        {
            return NvInternalResult.Success;
        }
    }
}
