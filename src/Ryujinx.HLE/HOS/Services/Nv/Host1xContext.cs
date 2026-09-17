using System;
namespace Ryujinx.HLE.HOS.Services.Nv
{
    class Host1xContext : IDisposable
    {
        public object Smmu { get; }
        public object MemoryAllocator { get; }
        public object Host1x { get; }
        public Host1xContext(object gpu, ulong pid) { }
        public void Dispose() { }
    }
}
