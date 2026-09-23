using System;
using Ryujinx.Memory;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvHostChannelDeviceFile : NvDeviceFile
    {
        private static void FLog(string s){ try{ System.IO.File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_present.txt", DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }
        private readonly NvChannel _channel;
        public NvChannel Channel => _channel;

        public NvHostChannelDeviceFile(ServiceCtx context, IVirtualMemoryManager mem, ulong owner) : base(context, owner)
        {
            Path = "/dev/nvhost-gpu";
            _channel = new NvChannel(context);
            FLog("[GPFIFO] DeviceFile CREATED");
        }

        public NvHostChannelDeviceFile(ServiceCtx context, ulong owner) : this(context, null, owner) { }

        public override NvInternalResult Ioctl(NvIoctl command, Span<byte> arguments)
        {
            FLog($"[GPFIFO] Ioctl Number={command.Number}");
            if (command.Number == 1)
                return (NvInternalResult)CallIoctlMethod<Types.SubmitGpfifoArguments>(_channel.SubmitGpfifo, arguments);
            if (command.Number == 2 || command.Number == 9)
                return (NvInternalResult)CallIoctlMethod<Types.AllocGpfifoExArguments>(_channel.AllocGpfifoEx, arguments);
            if (command.Number == 8)
                return (NvInternalResult)CallIoctlMethod<Types.MapCommandBufferArguments>(_channel.MapCommandBuffer, arguments);
            return NvInternalResult.NotImplemented;
        }

        public override NvInternalResult Ioctl2(NvIoctl command, Span<byte> arguments, Span<byte> inlineInBuffer) => Ioctl(command, arguments);
        public override NvInternalResult Ioctl3(NvIoctl command, Span<byte> arguments, Span<byte> inlineOutBuffer) => Ioctl(command, arguments);
        public override void Close(){ FLog("[GPFIFO] Close"); }

        // FIX do erro L597 - tem que ser static
        public static void Destroy(){ }
    }
}
