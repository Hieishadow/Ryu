using System;
using Ryujinx.Common.Logging;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvHostChannelDeviceFile : NvDeviceFile
    {
        private static void FLog(string s){ try{ System.IO.File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_present.txt", DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }
        private readonly NvChannel _channel;

        public NvChannel Channel => _channel; // FIX pro NvHostAsGpuDeviceFile L114

        public NvHostChannelDeviceFile(ServiceCtx context, ulong owner) : base(context, owner)
        {
            Path = "/dev/nvhost-gpu";
            _channel = new NvChannel(context);
            FLog("[GPFIFO] DeviceFile CREATED");
        }

        public override NvInternalResult Ioctl(NvIoctl command, Span<byte> arguments)
        {
            FLog($"[GPFIFO] Ioctl Number={command.Number} Raw=0x{command.RawValue:X8}");
            try{
                if (command.Number == 1)
                    return (NvInternalResult)CallIoctlMethod<Types.SubmitGpfifoArguments>(_channel.SubmitGpfifo, arguments);
                if (command.Number == 2 || command.Number == 9) // AllocGpfifoEx e Ex2
                    return (NvInternalResult)CallIoctlMethod<Types.AllocGpfifoExArguments>(_channel.AllocGpfifoEx, arguments);
            }catch(Exception ex){ FLog($"Ioctl EX {ex}"); }
            
            return NvInternalResult.NotImplemented;
        }

        public override NvInternalResult Ioctl2(NvIoctl command, Span<byte> arguments, Span<byte> inlineInBuffer) => Ioctl(command, arguments);
        public override NvInternalResult Ioctl3(NvIoctl command, Span<byte> arguments, Span<byte> inlineOutBuffer) => Ioctl(command, arguments);
        public override void Close(){ FLog("[GPFIFO] Close"); }
        
        // FIX L597 - tem que ter static e instance
        public void Destroy(){ Close(); }
        public static void DestroyStatic(){ }
    }
}
