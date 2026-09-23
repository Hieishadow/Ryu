using System;
using Ryujinx.Graphics.Gpu.Memory;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel.Types;
using Ryujinx.HLE.HOS.Services.Nv.Types;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvChannel
    {
        private readonly ServiceCtx _context;
        private MemoryManager _gmm;
        private static void FLog(string s){ try{ System.IO.File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_present.txt", DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }

        public NvChannel(ServiceCtx context){ _context = context; FLog("[CHANNEL] CRIADO"); }

        // Chamado pelo NvHostAsGpuDeviceFile.cs L114
        public NvInternalResult BindMemory(MemoryManager gmm)
        {
            _gmm = gmm;
            FLog("[CHANNEL] BindMemory Gmm");
            return NvInternalResult.Success;
        }

        // Chamado pelo NvHostChannelDeviceFile
        public NvInternalResult SubmitGpfifo(ref SubmitGpfifoArguments args)
        {
            FLog($"SubmitGpfifo Fence {args.Fence.Id}:{args.Fence.Value} Flags={args.Flags}");
            try{
                if ((args.Flags & SubmitGpfifoFlags.FenceWait) != 0 && args.Fence.IsValid())
                    args.Fence.Wait(_context.Device.Gpu, TimeSpan.FromSeconds(1));
                if ((args.Flags & SubmitGpfifoFlags.FenceIncrement) != 0 && args.Fence.IsValid()){
                    FLog($"FenceIncrement ANTES {args.Fence.Value}");
                    args.Fence.Increment(_context.Device.Gpu);
                    FLog($"FenceIncrement DEPOIS {args.Fence.Value}");
                }
            }catch(Exception ex){ FLog(ex.ToString()); }
            return NvInternalResult.Success;
        }

        public NvInternalResult AllocGpfifoEx(ref AllocGpfifoExArguments args){ return NvInternalResult.Success; }
        public NvInternalResult MapCommandBuffer(ref MapCommandBufferArguments args){ return NvInternalResult.Success; }
    }
}
