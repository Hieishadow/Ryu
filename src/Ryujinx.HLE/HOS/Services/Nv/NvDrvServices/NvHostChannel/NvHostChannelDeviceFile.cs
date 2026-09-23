using System;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel.Types;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvHostChannelDeviceFile : NvDeviceFile
    {
        private static void FLogPresent(string s){ try{ var p="/storage/emulated/0/Download/Ryubing/ryubing_present.txt"; System.IO.File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }

        private readonly NvChannel _channel;

        public NvHostChannelDeviceFile(ServiceCtx context, ulong owner) : base(context, owner)
        {
            Path = "/dev/nvhost-gpu";
            _channel = new NvChannel(context);
            FLogPresent("[GPFIFO] NvHostChannelDeviceFile CREATED");
        }

        public override NvInternalResult Ioctl(NvIoctl command, Span<byte> arguments)
        {
            FLogPresent($"[GPFIFO] Ioctl START cmd=0x{command.Cmd:X} args={arguments.Length}");
            var result = _channel.Ioctl(command, arguments);
            FLogPresent($"[GPFIFO] Ioctl END result={result}");
            return (NvInternalResult)result;
        }

        public override NvInternalResult Ioctl2(NvIoctl command, Span<byte> arguments, Span<byte> inlineInBuffer)
        {
            FLogPresent($"[GPFIFO] Ioctl2 START cmd=0x{command.Cmd:X}");
            var result = _channel.Ioctl2(command, arguments, inlineInBuffer);
            FLogPresent($"[GPFIFO] Ioctl2 END");
            return (NvInternalResult)result;
        }

        public override NvInternalResult Ioctl3(NvIoctl command, Span<byte> arguments, Span<byte> inlineOutBuffer)
        {
            FLogPresent($"[GPFIFO] Ioctl3 START cmd=0x{command.Cmd:X}");
            var result = _channel.Ioctl3(command, arguments, inlineOutBuffer);
            FLogPresent($"[GPFIFO] Ioctl3 END");
            return (NvInternalResult)result;
        }

        public override void Close()
        {
            FLogPresent("[GPFIFO] Close");
            _channel?.Dispose();
        }
    }
}
