using System;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel.Types;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvHostGpuDeviceFile : NvDeviceFile
    {
        private static void FLogPresent(string s){ try{ var p="/storage/emulated/0/Download/Ryubing/ryubing_present.txt"; System.IO.File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }

        private readonly NvChannel _channel;

        public NvHostGpuDeviceFile(ServiceCtx context, ulong owner) : base(context, owner)
        {
            Path = "/dev/nvhost-ctrl-gpu";
            _channel = new NvChannel(context);
            FLogPresent("[GPU] NvHostGpuDeviceFile CREATED - canal real");
        }

        public override NvInternalResult Ioctl(NvIoctl command, Span<byte> arguments)
        {
            FLogPresent($"[GPU] Ioctl START cmd=0x{command.Cmd:X} args={arguments.Length}");
            // Delega pro channel real, não return 0
            var result = _channel.Ioctl(command, arguments);
            FLogPresent($"[GPU] Ioctl END result={result}");
            return (NvInternalResult)result;
        }

        public override NvInternalResult Ioctl2(NvIoctl command, Span<byte> arguments, Span<byte> inlineInBuffer)
        {
            FLogPresent($"[GPU] Ioctl2 START cmd=0x{command.Cmd:X}");
            var result = _channel.Ioctl2(command, arguments, inlineInBuffer);
            FLogPresent($"[GPU] Ioctl2 END result={result}");
            return (NvInternalResult)result;
        }

        public override NvInternalResult QueryEvent(out int eventHandle, uint eventId)
        {
            FLogPresent($"[SYNC] QueryEvent REAL id={eventId}");
            var result = _channel.QueryEvent(out eventHandle, eventId);
            FLogPresent($"[SYNC] QueryEvent handle={eventHandle} result={result}");
            return (NvInternalResult)result;
        }

        public override void Close()
        {
            FLogPresent("[GPU] Close");
            _channel?.Dispose();
        }
    }
}
