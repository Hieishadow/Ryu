using System;
namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    class NvHostChannelDeviceFile
    {
        public object Channel { get; } = null;
        public void Destroy() { }
        public virtual void Close() { }
        public virtual int QueryEvent(out int h, uint id) { h = 0; return 0; }
        public virtual int Ioctl2(object a, Span<byte> b, Span<byte> c) => 0;
        public NvHostChannelDeviceFile() { }
    }
}
