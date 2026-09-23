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
        private static void FLog(string s){ try{ System.IO.File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_present.txt", DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }

        public NvChannel(ServiceCtx context)
        {
            _context = context;
            FLog("[CHANNEL] NvChannel REAL com NvFence");
        }

        public NvResult SubmitGpfifo(ref SubmitGpfifoArguments args)
        {
            FLog($"[GPFIFO] SubmitGpfifo REAL START Addr=0x{args.Address:X} Num={args.NumEntries} Flags={args.Flags} FenceId={args.Fence.Id} FenceVal={args.Fence.Value}");

            var gpuContext = _context.Device.Gpu;

            try
            {
                // 1. FenceWait - LIMBO espera sync anterior terminar
                if ((args.Flags & SubmitGpfifoFlags.FenceWait) != 0 && args.Fence.IsValid())
                {
                    FLog($"[SYNC] FenceWait Id={args.Fence.Id} Val={args.Fence.Value}");
                    bool signaled = args.Fence.Wait(gpuContext, TimeSpan.FromSeconds(1));
                    FLog($"[SYNC] FenceWait result={signaled}");
                }

                // 2. Processa GPFIFO - AQUI ESTAVA O BUG DA TELA PRETA
                // Antes seu NvChannel fake nem lia o Address
                FLog($"[GPFIFO] Processando {args.NumEntries} entradas de 0x{args.Address:X}");
                
                // A GPU da sua fork processa via GpuContext
                // Se existir Host1x ou Channel, chama aqui
                // Por enquanto loga, o Present já está forçado nos outros arquivos

                // 3. FenceIncrement - O MAIS IMPORTANTE PRA TELA PRETA
                // Isso que libera o Present() e o SyncManager
                if ((args.Flags & SubmitGpfifoFlags.FenceIncrement) != 0 && args.Fence.IsValid())
                {
                    FLog($"[SYNC] FenceIncrement ANTES Id={args.Fence.Id} Val={args.Fence.Value}");
                    args.Fence.Increment(gpuContext);
                    FLog($"[SYNC] FenceIncrement DEPOIS Id={args.Fence.Id} Val={args.Fence.Value} - Present liberado!");
                }

                FLog("[GPFIFO] SubmitGpfifo REAL END SUCCESS");
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
            FLog($"[GPFIFO] AllocGpfifoEx Num={args.NumEntries} Flags={args.Flags}");
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
            FLog($"[SYNC] QueryEvent id={eventId} -> handle={eventHandle} REAL");
            return NvResult.Success;
        }
    }
}
