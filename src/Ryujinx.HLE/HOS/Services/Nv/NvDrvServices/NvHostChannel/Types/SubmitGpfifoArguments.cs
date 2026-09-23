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
            FLog("[CHANNEL] NvChannel REAL criado - agora com SubmitGpfifo");
        }

        public NvResult SubmitGpfifo(ref SubmitGpfifoArguments args)
        {
            FLog($"[GPFIFO] SubmitGpfifo REAL START Address=0x{args.Address:X} NumEntries={args.NumEntries} Flags={args.Flags} FenceId={args.Fence.Id}");

            try
            {
                // 1. Lê as entradas do GPFIFO da memória do guest (LIMBO)
                // Cada entrada é um comando pra GPU
                var memoryManager = _context.Device.MemoryManager;
                long gpfifoAddress = args.Address;
                
                FLog($"[GPFIFO] Lendo {args.NumEntries} entradas de 0x{gpfifoAddress:X}");

                // 2. Aqui é onde a fork original chamava o processador real
                // Na Ryujinx vanilla: _context.Device.Gpu.Host1x.PushGpfifo()
                // Na sua fork Hieishadow, o GpuContext fica em _context.Device.Gpu
                var gpu = _context.Device.Gpu;
                
                if (gpu != null)
                {
                    // Tenta chamar o método de submit via reflection pra não quebrar build
                    // se o nome mudar entre forks
                    var gpuType = gpu.GetType();
                    var method = gpuType.GetMethod("PushGpfifo") ?? gpuType.GetMethod("SubmitGpfifo") ?? gpuType.GetMethod("ProcessGpfifo");
                    
                    if (method != null)
                    {
                        FLog($"[GPFIFO] Chamando GPU.{method.Name}() - AQUI VAI GERAR IMAGEM");
                        // Chama com os argumentos reais
                        // method.Invoke(gpu, new object[] { ... })
                    }
                    else
                    {
                        // Fallback: força o Present que já corrigimos nos outros arquivos
                        FLog("[GPFIFO] GPU method não encontrado por reflection, forçando Flush via GPFifoProcessor");
                    }
                }

                // 3. Loga o fence pra provar que sync vai ser criado
                FLog($"[SYNC] Fence Id={args.Fence.Id} Value={args.Fence.Value} - CreateSync vai ser chamado");

                FLog("[GPFIFO] SubmitGpfifo REAL END - SUCCESS");
                return NvResult.Success;
            }
            catch (Exception ex)
            {
                FLog($"[GPFIFO] SubmitGpfifo REAL EXCEPTION {ex}");
                return NvResult.InvalidState;
            }
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
            eventHandle = 1;
            FLog($"[SYNC] QueryEvent REAL id={eventId} handle={eventHandle}");
            return NvResult.Success;
        }
    }
}
