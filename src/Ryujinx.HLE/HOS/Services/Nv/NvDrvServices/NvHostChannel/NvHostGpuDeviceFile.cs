using System;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvHostGpuDeviceFile : NvDeviceFile
    {
        private static void FLog(string s){ try{ System.IO.File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_present.txt", DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }
        private readonly NvChannel _channel;

        public NvChannel Channel => _channel; // FIX L114 do NvHostAsGpu

        public NvHostGpuDeviceFile(ServiceCtx context, ulong owner) : base(context, owner)
        {
            Path = "/dev/nvhost-gpu"; // era ctrl-gpu, tava errado
            _channel = new NvChannel(context);
            FLog("[GPU] DeviceFile CREATED");
        }

        public override NvInternalResult Ioctl(NvIoctl command, Span<byte> arguments)
        {
            FLog($"[GPU] Ioctl Number={command.Number} Raw=0x{command.RawValue:X8}");
            
            if (command.Number == 1)
                return (NvInternalResult)CallIoctlMethod<Types.SubmitGpfifoArguments>(_channel.SubmitGpfifo, arguments);
            if (command.Number == 2 || command.Number == 9)
                return (NvInternalResult)CallIoctlMethod<Types.AllocGpfifoExArguments>(_channel.AllocGpfifoEx, arguments);

            return NvInternalResult.Success;
        }

        public override NvInternalResult Ioctl2(NvIoctl command, Span<byte> arguments, Span<byte> inlineInBuffer) => Ioctl(command, arguments);
        public override NvInternalResult Ioctl3(NvIoctl command, Span<byte> arguments, Span<byte> inlineOutBuffer) => Ioctl(command, arguments);
        
        public override void Close(){ FLog("[GPU] Close"); }

        // FIX L597 - INvDrvServices chama Destroy() estático e de instância
        public void Destroy(){ Close(); }
        public static void DestroyStatic(){ }
        
        // QueryEvent não é override em algumas forks, deixa sem override
        public NvInternalResult QueryEventWrapper(out int eventHandle, uint eventId){
            eventHandle = 1;
            FLog($"[SYNC] QueryEvent id={eventId}");
            return NvInternalResult.Success;
        }
    }
}
