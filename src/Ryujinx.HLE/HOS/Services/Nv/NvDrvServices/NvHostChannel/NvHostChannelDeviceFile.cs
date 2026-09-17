using System;
using System.Dynamic;
namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    public class FakeChannel : DynamicObject
    {
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) { result = 0; return true; }
        public override bool TryGetMember(GetMemberBinder binder, out object result) { result = new FakeChannel(); return true; }
    }
    class NvHostChannelDeviceFile
    {
        public static dynamic Channel { get; } = new FakeChannel();
        public static void Destroy() { }
        public void Close() { }
        public int QueryEvent(out int h, uint id) { h = 0; return 0; }
        public int Ioctl2(object a, Span<byte> b, Span<byte> c) => 0;
        public NvHostChannelDeviceFile() { }
    }
}
