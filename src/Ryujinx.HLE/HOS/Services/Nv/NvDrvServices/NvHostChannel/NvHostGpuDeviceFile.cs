using System;
namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvHostGpuDeviceFile
    {
        public NvHostGpuDeviceFile() { }
        public void Close() { }
        public int QueryEvent(out int handle, uint id) { handle = 0; return 0; }
        public int Ioctl2(object a, Span<byte> b, Span<byte> c) => 0;
    }
}
