using System;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel.Types;
using Ryujinx.HLE.HOS.Services.Nv.Types;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvChannel
    {
        public int Timeout;
        public int SubmitTimeout;
        public int Timeslice;
        private readonly ServiceCtx _context;

        private static void FLog(string s){
            try{ System.IO.File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_present.txt", DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{}
        }

        public NvChannel(ServiceCtx context){ _context = context; FLog("[CHANNEL] NvChannel REAL CRIADO"); }

        public int SubmitGpfifo(ref SubmitGpfifoArguments args)
        {
            FLog($"[GPFIFO] SubmitGpfifo Addr=0x{args.Address:X} Num={args.NumEntries} Fence={args.Fence.Id}:{args.Fence.Value}");
            try{
                if ((args.Flags & SubmitGpfifoFlags.FenceWait) != 0 && args.Fence.IsValid())
                    args.Fence.Wait(_context.Device.Gpu, TimeSpan.FromSeconds(1));

                if ((args.Flags & SubmitGpfifoFlags.FenceIncrement) != 0 && args.Fence.IsValid()){
                    FLog($"[SYNC] FenceIncrement ANTES {args.Fence.Value}");
                    args.Fence.Increment(_context.Device.Gpu);
                    FLog($"[SYNC] FenceIncrement DEPOIS {args.Fence.Value}");
                }
                return 0;
            }catch(Exception ex){ FLog($"EX {ex}"); return -1; }
        }

        public int AllocGpfifoEx(ref AllocGpfifoExArguments args){ return 0; }
        public int MapCommandBuffer(ref MapCommandBufferArguments args){ return 0; }

        public int QueryEvent(out int eventHandle, uint eventId)
        {
            eventHandle = 1;
            FLog($"[SYNC] QueryEvent id={eventId}");
            return 0;
        }
    }
}
