using System;
using Ryujinx.Memory;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvHostChannelDeviceFile : NvDeviceFile
    {
        private readonly NvChannel _channel;
        public NvChannel Channel => _channel;

        public NvHostChannelDeviceFile(ServiceCtx context, IVirtualMemoryManager mem, ulong owner) : base(context, owner)
        {
            Path = "/dev/nvhost-gpu";
            _channel = new NvChannel(context);
        }
        public NvHostChannelDeviceFile(ServiceCtx context, ulong owner) : this(context, null, owner) { }

        public override NvInternalResult Ioctl(NvIoctl command, Span<byte> arguments)
        {
            if (command.Number == 1)
                return CallIoctlMethod<Types.SubmitGpfifoArguments>(_channel.SubmitGpfifo, arguments);
            if (command.Number == 2 || command.Number == 9)
                return CallIoctlMethod<Types.AllocGpfifoExArguments>(_channel.AllocGpfifoEx, arguments);
            if (command.Number == 8)
                return CallIoctlMethod<Types.MapCommandBufferArguments>(_channel.MapCommandBuffer, arguments);
            return NvInternalResult.NotImplemented;
        }
        public override NvInternalResult Ioctl2(NvIoctl command, Span<byte> arguments, Span<byte> inlineInBuffer) => Ioctl(command, arguments);
        public override NvInternalResult Ioctl3(NvIoctl command, Span<byte> arguments, Span<byte> inlineOutBuffer) => Ioctl(command, arguments);
        public override void Close(){ }
        public static void Destroy(){ }
    }
}
