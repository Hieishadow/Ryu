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
        public dynamic Channel { get; } = new FakeChannel();
        public void Destroy() { }
        public static void Destroy(object dummy = null) { }
        public void Close() { }
        public NvHostChannelDeviceFile() { }
    }
}
