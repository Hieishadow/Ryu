using System;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel.Types;
using Ryujinx.HLE.HOS.Services.Nv.Types;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvChannel
    {
        private readonly ServiceCtx _context;
        private static void FLog(string s){ try{ System.IO.File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_present.txt", DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }
        public NvChannel(ServiceCtx context){ _context = context; FLog("[CHANNEL] CRIADO"); }

        // TEM QUE RETORNAR NvInternalResult (não int, não NvResult)
        public NvInternalResult SubmitGpfifo(ref SubmitGpfifoArguments args)
        {
            FLog($"SubmitGpfifo Fence {args.Fence.Id}:{args.Fence.Value} Flags={args.Flags}");
            try{
                if ((args.Flags & SubmitGpfifoFlags.FenceWait) != 0 && args.Fence.IsValid())
                    args.Fence.Wait(_context.Device.Gpu, TimeSpan.FromSeconds(1));
                if ((args.Flags & SubmitGpfifoFlags.FenceIncrement) != 0 && args.Fence.IsValid()){
                    FLog($"FenceIncrement ANTES {args.Fence.Value}");
                    args.Fence.Increment(_context.Device.Gpu);
                    FLog($"FenceIncrement DEPOIS {args.Fence.Value} <- LIBERA PRESENT");
                }
            }catch(Exception ex){ FLog(ex.ToString()); }
            return NvInternalResult.Success;
        }

        public NvInternalResult AllocGpfifoEx(ref AllocGpfifoExArguments args){ FLog("AllocGpfifoEx"); return NvInternalResult.Success; }
        public NvInternalResult AllocGpfifoEx2(ref AllocGpfifoExArguments args){ return NvInternalResult.Success; }
        public NvInternalResult MapCommandBuffer(ref MapCommandBufferArguments args){ return NvInternalResult.Success; }
        
        // FIX do erro CS1061 BindMemory do NvHostAsGpuDeviceFile.cs L114
        public NvInternalResult BindMemory(ref NvHostAsGpu.Types.BindChannelArguments args){ FLog("BindMemory"); return NvInternalResult.Success; }
    }
}
