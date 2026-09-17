using System;
using System.Dynamic;
namespace Ryujinx.HLE.HOS.Services.Nv
{
    public class FakeHost1xDevice : DynamicObject
    {
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) { result = 0; return true; }
        public void RegisterDevice(object a, object b) { }
        public void Dispose() { }
        public object CreateContext(object a) => 0;
        public object DestroyContext(object a) => 0;
        public object Map(object a, object b, object c) => 0;
        public object Unmap(object a, object b) => 0;
        public object AllocateRange(object a, object b) => 0;
        public object GetFreeAddress(object a) => 0;
        public object Submit(object a) => 0;
    }

    internal class Host1xContext : IDisposable
    {
        public dynamic Smmu { get; } = new FakeHost1xDevice();
        public dynamic MemoryAllocator { get; } = new FakeHost1xDevice();
        public dynamic Host1x { get; } = new FakeHost1xDevice();
        public Host1xContext(dynamic gpu, ulong pid) { }
        public void Dispose() { }
    }
}
