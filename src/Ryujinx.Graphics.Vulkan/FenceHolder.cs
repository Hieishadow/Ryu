using Silk.NET.Vulkan;
using System;
using System.IO;
using System.Threading;

namespace Ryujinx.Graphics.Vulkan
{
    class FenceHolder : IDisposable
    {
        private static void FLog(string s){ try{ var p="/storage/emulated/0/Download/Ryubing/ryubing_log.txt"; var d=Path.GetDirectoryName(p); if(d!=null){ try{ if(!Directory.Exists(d)) Directory.CreateDirectory(d); }catch{} } File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss.fff")+" [FENCE] "+s+"\n"); }catch{} }

        private readonly Vk _api;
        private readonly Device _device;
        private Fence _fence;
        private int _referenceCount;
        private int _lock;
        private readonly bool _concurrentWaitUnsupported;
        private bool _disposed;

        public unsafe FenceHolder(Vk api, Device device, bool concurrentWaitUnsupported)
        {
            _api = api;
            _device = device;
            _concurrentWaitUnsupported = concurrentWaitUnsupported;
            FenceCreateInfo fenceCreateInfo = new() { SType = StructureType.FenceCreateInfo, };
            api.CreateFence(device, in fenceCreateInfo, null, out _fence).ThrowOnError();
            _referenceCount = 1;
        }

        public Fence GetUnsafe() => _fence;

        public bool TryGet(out Fence fence)
        {
            int lastValue;
            do
            {
                lastValue = _referenceCount;
                if (lastValue == 0) { fence = default; return false; }
            }
            while (Interlocked.CompareExchange(ref _referenceCount, lastValue + 1, lastValue) != lastValue);
            if (_concurrentWaitUnsupported) AcquireLock();
            fence = _fence;
            return true;
        }

        public Fence Get() { Interlocked.Increment(ref _referenceCount); return _fence; }

        public void PutLock() { Put(); if (_concurrentWaitUnsupported) ReleaseLock(); }

        public void Put()
        {
            if (Interlocked.Decrement(ref _referenceCount) == 0)
            {
                _api.DestroyFence(_device, _fence, Span<AllocationCallbacks>.Empty);
                _fence = default;
            }
        }

        private void AcquireLock() { while (!TryAcquireLock()) { Thread.SpinWait(32); } }
        private bool TryAcquireLock() => Interlocked.Exchange(ref _lock, 1) == 0;
        private void ReleaseLock() => Interlocked.Exchange(ref _lock, 0);

        public void Wait()
        {
            if (_concurrentWaitUnsupported)
            {
                AcquireLock();
                try { WaitWithTimeout(1_000_000_000); } // #554: 1s max, não infinito
                finally { ReleaseLock(); }
            }
            else
            {
                WaitWithTimeout(1_000_000_000);
            }
        }

        // #554: NOVO - usado pelo CommandBufferPool #553
        public unsafe bool WaitWithTimeout(ulong timeout)
        {
            if (_concurrentWaitUnsupported)
            {
                if (!TryAcquireLock()) return false;
                try
                {
                    fixed (Fence* p = &_fence)
                    {
                        Result r = _api.WaitForFences(_device, 1, p, true, timeout);
                        bool ok = r == Result.Success;
                        if (!ok) FLog($"WaitWithTimeout timeout={timeout} result={r} fence={_fence.Handle.ToString("X")}");
                        return ok;
                    }
                }
                finally { ReleaseLock(); }
            }
            else
            {
                fixed (Fence* p = &_fence)
                {
                    Result r = _api.WaitForFences(_device, 1, p, true, timeout);
                    bool ok = r == Result.Success;
                    if (!ok) FLog($"WaitWithTimeout timeout={timeout} result={r} fence={_fence.Handle.ToString("X")}");
                    return ok;
                }
            }
        }

        public bool IsSignaled()
        {
            if (_concurrentWaitUnsupported)
            {
                if (!TryAcquireLock()) return false;
                try { return FenceHelper.AllSignaled(_api, _device, [_fence]); }
                finally { ReleaseLock(); }
            }
            else
            {
                return FenceHelper.AllSignaled(_api, _device, [_fence]);
            }
        }

        public void Dispose()
        {
            if (!_disposed) { Put(); _disposed = true; }
        }
    }
}
