using System;
using Ryujinx.Common.Logging;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvHostChannelDeviceFile : NvDeviceFile
    {
        private static void FLogPresent(string s){ try{ var p="/storage/emulated/0/Download/Ryubing/ryubing_present.txt"; System.IO.File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }

        public NvHostChannelDeviceFile(ServiceCtx context, ulong owner) : base(context, owner)
        {
            Path = "/dev/nvhost-gpu";
            FLogPresent("[GPFIFO] NvHostChannelDeviceFile CREATED");
        }

        public override NvInternalResult Ioctl(NvIoctl command, Span<byte> arguments)
        {
            // command.Cmd contém o ID do ioctl (SubmitGpfifo = 0x1, etc)
            FLogPresent($"[GPFIFO] Ioctl START cmd=0x{command.Cmd:X} type=0x{command.Type:X} args={arguments.Length}");
            try
            {
                // Deixa o logger original do Ryujinx também mostrar
                Logger.Debug?.Print(LogClass.ServiceNv, $"NvHostChannel Ioctl 0x{command.Cmd:X}");

                // Por enquanto retorna Success pra não travar o jogo
                // Depois que compilar e aparecer esse log, a gente injeta o SubmitGpfifo real
                // chamando Context.Device.Gpu
                return NvInternalResult.Success;
            }
            catch (Exception ex)
            {
                FLogPresent($"[GPFIFO] Ioctl EXCEPTION {ex}");
                return NvInternalResult.InvalidState;
            }
            finally
            {
                FLogPresent("[GPFIFO] Ioctl END");
            }
        }

        public override NvInternalResult Ioctl2(NvIoctl command, Span<byte> arguments, Span<byte> inlineInBuffer)
        {
            FLogPresent($"[GPFIFO] Ioctl2 START cmd=0x{command.Cmd:X} args={arguments.Length} inline={inlineInBuffer.Length}");
            var r = Ioctl(command, arguments);
            FLogPresent("[GPFIFO] Ioctl2 END");
            return r;
        }

        public override NvInternalResult Ioctl3(NvIoctl command, Span<byte> arguments, Span<byte> inlineOutBuffer)
        {
            FLogPresent($"[GPFIFO] Ioctl3 START cmd=0x{command.Cmd:X} args={arguments.Length} out={inlineOutBuffer.Length}");
            var r = Ioctl(command, arguments);
            FLogPresent("[GPFIFO] Ioctl3 END");
            return r;
        }

        public override void Close()
        {
            FLogPresent("[GPFIFO] Close CALLED");
        }

        // Compatibilidade com código antigo que chamava Destroy()
        public void Destroy() { Close(); }
        public static void Destroy(object dummy = null) { }
    }
}
