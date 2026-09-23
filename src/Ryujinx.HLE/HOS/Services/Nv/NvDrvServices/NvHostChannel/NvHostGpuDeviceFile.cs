using System;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvHostGpuDeviceFile : NvDeviceFile
    {
        private static void FLog(string s){ try{ System.IO.File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_present.txt", DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }
        private readonly NvChannel _channel;

        public NvHostGpuDeviceFile(ServiceCtx context, ulong owner) : base(context, owner)
        {
            Path = "/dev/nvhost-ctrl-gpu";
            _channel = new NvChannel(context);
            FLog("[GPU] DeviceFile CREATED com canal real");
        }

        public override NvInternalResult Ioctl(NvIoctl command, Span<byte> arguments)
        {
            FLog($"[GPU] Ioctl Number={command.Number}");
            return NvInternalResult.Success;
        }
        public override NvInternalResult Ioctl2(NvIoctl command, Span<byte> arguments, Span<byte> inlineInBuffer) => Ioctl(command, arguments);
        public override NvInternalResult Ioctl3(NvIoctl command, Span<byte> arguments, Span<byte> inlineOutBuffer) => Ioctl(command, arguments);
        public override NvInternalResult QueryEvent(out int eventHandle, uint eventId) { eventHandle = 0; FLog($"[SYNC] QueryEvent id={eventId}"); return NvInternalResult.Success; }
        public override void Close() { FLog("[GPU] Close"); }
        public void Destroy() => Close();
    }
}
