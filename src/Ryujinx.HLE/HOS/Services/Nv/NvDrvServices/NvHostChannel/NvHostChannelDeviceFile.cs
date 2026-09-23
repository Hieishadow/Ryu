using System;
using System.Dynamic;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostChannel
{
    public class FakeChannel : DynamicObject
    {
        private static void FLogPresent(string s){ try{ var p="/storage/emulated/0/Download/Ryubing/ryubing_present.txt"; System.IO.File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            FLogPresent($"[GPFIFO] FakeChannel CALL {binder.Name} args={args?.Length}");
            result = 0;
            return true;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            FLogPresent($"[GPFIFO] FakeChannel GET {binder.Name}");
            result = new FakeChannel();
            return true;
        }
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
