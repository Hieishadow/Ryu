using System;
using Ryujinx.Common.Logging;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvHostChannelDeviceFile : NvDeviceFile
    {
        private static void FLog(string s){ try{ System.IO.File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_present.txt", DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }
        private readonly NvChannel _channel;

        public NvHostChannelDeviceFile(ServiceCtx context, ulong owner) : base(context, owner)
        {
            Path = "/dev/nvhost-gpu";
            _channel = new NvChannel(context); // agora existe construtor
            FLog("[GPFIFO] DeviceFile CREATED com canal real");
        }

        public override NvInternalResult Ioctl(NvIoctl command, Span<byte> arguments)
        {
            FLog($"[GPFIFO] Ioctl Raw=0x{command.RawValue:X8} Number={command.Number} Type={command.Type}");
            
            if (command.Number == 1) // SubmitGpfifo
            {
                return (NvInternalResult)CallIoctlMethod<Types.SubmitGpfifoArguments>(_channel.SubmitGpfifo, arguments);
            }
            else if (command.Number == 2)
            {
                return (NvInternalResult)CallIoctlMethod<Types.AllocGpfifoExArguments>(_channel.AllocGpfifoEx, arguments);
            }
            
            FLog($"[GPFIFO] Ioctl Number={command.Number} não implementado");
            return NvInternalResult.NotImplemented;
        }

        public override NvInternalResult Ioctl2(NvIoctl command, Span<byte> arguments, Span<byte> inlineInBuffer) => Ioctl(command, arguments);
        public override NvInternalResult Ioctl3(NvIoctl command, Span<byte> arguments, Span<byte> inlineOutBuffer) => Ioctl(command, arguments);
        public override void Close() { FLog("[GPFIFO] Close"); }
        public void Destroy() => Close(); // corrige L597
    }
}
