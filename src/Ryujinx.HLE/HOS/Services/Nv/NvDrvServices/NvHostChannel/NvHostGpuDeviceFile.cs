using System;
using Ryujinx.Memory;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvHostGpuDeviceFile : NvDeviceFile
    {
        private readonly NvChannel _channel;
        public NvChannel Channel => _channel;

        public NvHostGpuDeviceFile(ServiceCtx context, IVirtualMemoryManager mem, ulong owner) : base(context, owner)
        {
            Path = "/dev/nvhost-gpu";
            _channel = new NvChannel(context);
        }
        public NvHostGpuDeviceFile(ServiceCtx context, ulong owner) : this(context, null, owner) { }

        public override NvInternalResult Ioctl(NvIoctl command, Span<byte> arguments)
        {
            if (command.Number == 1)
                return CallIoctlMethod<Types.SubmitGpfifoArguments>(_channel.SubmitGpfifo, arguments);
            if (command.Number == 2 || command.Number == 9)
                return CallIoctlMethod<Types.AllocGpfifoExArguments>(_channel.AllocGpfifoEx, arguments);
            return NvInternalResult.Success;
        }
        public override NvInternalResult Ioctl2(NvIoctl command, Span<byte> arguments, Span<byte> inlineInBuffer) => Ioctl(command, arguments);
        public override NvInternalResult Ioctl3(NvIoctl command, Span<byte> arguments, Span<byte> inlineOutBuffer) => Ioctl(command, arguments);
        public override void Close(){ }
        public static void Destroy(){ }
    }
}
