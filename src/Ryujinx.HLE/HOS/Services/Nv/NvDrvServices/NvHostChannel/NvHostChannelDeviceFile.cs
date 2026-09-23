using System;
using System.Runtime.InteropServices;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel.Types;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvHostChannelDeviceFile : NvDeviceFile
    {
        // #558 LOG
        private static void FLogPresent(string s){ try{ var p="/storage/emulated/0/Download/Ryubing/ryubing_present.txt"; System.IO.File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }

        private readonly NvHostChannelDevice _device;
        private int _refCount;

        public NvHostChannelDeviceFile(NvHostChannelDevice device)
        {
            _device = device;
            _refCount = 1;
        }

        public override NvResult Ioctl(int cmd, Span<byte> input, Span<byte> output)
        {
            FLogPresent($"[GPFIFO] Ioctl START cmd=0x{cmd:X} in={input.Length} out={output.Length}");
            try
            {
                // Cmds do NvHostChannel - valores padrão Ryujinx
                // 0x00010001 = SubmitGpfifo
                // 0x00010002 = AllocGpfifoEx
                // 0x00010008 = SubmitGpfifoMultiprocess
                // Deixa o device decidir - é ele que chama GpfifoProcessor.Process

                NvResult result = _device.Ioctl(cmd, input, output);

                FLogPresent($"[GPFIFO] Ioctl END result={result}");
                return result;
            }
            catch (Exception ex)
            {
                FLogPresent($"[GPFIFO] Ioctl EXCEPTION {ex.Message}");
                Logger.Error?.Print(LogClass.ServiceNv, $"NvHostChannelDeviceFile Ioctl 0x{cmd:X} failed: {ex}");
                return NvResult.InvalidState;
            }
        }

        public override NvResult Open()
        {
            _refCount++;
            return NvResult.Success;
        }

        public override NvResult Close()
        {
            _refCount--;
            if (_refCount <= 0)
            {
                _device.Close();
            }
            return NvResult.Success;
        }

        public void Destroy() => _device.Destroy();
        public static void Destroy(object dummy = null) { }

        // Métodos que o Ryujinx original expõe via reflection/dynamic
        // Mantemos como passthrough para compatibilidade com seu Ryubing
        public NvResult SubmitGpfifo(Span<byte> input, Span<byte> output)
        {
            FLogPresent("[GPFIFO] SubmitGpfifo START");
            try
            {
                var r = _device.Ioctl(0x00010001, input, output);
                FLogPresent($"[GPFIFO] SubmitGpfifo END r={r}");
                return r;
            }
            finally { }
        }

        public NvResult SubmitGpfifoMultiprocess(Span<byte> input, Span<byte> output)
        {
            FLogPresent("[GPFIFO] SubmitGpfifoMultiprocess START");
            try
            {
                var r = _device.Ioctl(0x00010008, input, output);
                FLogPresent($"[GPFIFO] SubmitGpfifoMultiprocess END r={r}");
                return r;
            }
            finally { }
        }
    }
}
