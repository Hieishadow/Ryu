
using System;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel.Types;
using Ryujinx.Common.Logging;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvChannel
    {
        public int Timeout;
        public int SubmitTimeout;
        public int Timeslice;

        private readonly ServiceCtx _context;
        private static void FLog(string s){ try{ System.IO.File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_present.txt", DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }

        public NvChannel(ServiceCtx context)
        {
            _context = context;
            FLog("[CHANNEL] NvChannel REAL criado");
        }

        // Chamado pelo NvHostChannelDeviceFile Number=1 (SubmitGpfifo)
        public NvResult SubmitGpfifo(ref SubmitGpfifoArguments args)
        {
            FLog($"[GPFIFO] SubmitGpfifo REAL Address=0x{args.Address:X} NumEntries={args.NumEntries} Flags=0x{args.Flags:X}");
            
            // Aqui é onde o Hieishadow original chamava a GPU
            // _context.Device.Gpu.GPFifo.Submit(...)
            // Por enquanto loga pra provar que chegou - no próximo passo injetamos o Process real
            
            return NvResult.Success;
        }

        public NvResult AllocGpfifoEx(ref AllocGpfifoExArguments args)
        {
            FLog("[GPFIFO] AllocGpfifoEx REAL");
            return NvResult.Success;
        }

        public NvResult MapCommandBuffer(ref MapCommandBufferArguments args)
        {
            FLog("[GPFIFO] MapCommandBuffer REAL");
            return NvResult.Success;
        }

        public NvResult QueryEvent(out int eventHandle, uint eventId)
        {
            eventHandle = 0;
            FLog($"[SYNC] QueryEvent REAL id={eventId}");
            return NvResult.Success;
        }
    }
}
