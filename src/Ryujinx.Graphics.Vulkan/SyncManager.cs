using Ryujinx.Common.Logging;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Ryujinx.Graphics.Vulkan
{
    class SyncManager
    {
        private static void FLog(string s){ try{ var p="/storage/emulated/0/Download/Ryubing/ryubing_present.txt"; var d=Path.GetDirectoryName(p); if(d!=null){ try{ if(!Directory.Exists(d)) Directory.CreateDirectory(d); }catch{} } File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss.fff")+" "+s+"\n"); }catch{} }

        private class SyncHandle
        {
            public ulong ID;
            public MultiFenceHolder Waitable;
            public ulong FlushId;
            public bool Signalled;

            public bool NeedsFlush(ulong currentFlushId)
            {
                return (long)(FlushId - currentFlushId) >= 0;
            }
        }

        private ulong _firstHandle;

        private readonly VulkanRenderer _gd;
        private readonly Device _device;
        private readonly List<SyncHandle> _handles;
        private ulong _flushId;
        private long _waitTicks;

        public SyncManager(VulkanRenderer gd, Device device)
        {
            _gd = gd;
            _device = device;
            _handles = [];
            FLog($"[SYNC] SyncManager ctor");
        }

        public void RegisterFlush()
        {
            _flushId++;
            // FLog($"[SYNC] RegisterFlush flushId={_flushId}"); // muito verboso, deixa comentado
        }

        public void Create(ulong id, bool strict)
        {
            FLog($"[SYNC] Create START id={id} strict={strict} curFlush={_flushId} cur={_firstHandle}");
            ulong flushId = _flushId;
            MultiFenceHolder waitable = new();
            if (strict || _gd.InterruptAction == null)
            {
                FLog($"[SYNC] Create BEFORE FlushAllCommands id={id}");
                _gd.FlushAllCommands();
                FLog($"[SYNC] Create AFTER FlushAllCommands id={id} BEFORE AddWaitable");
                _gd.CommandBufferPool.AddWaitable(waitable);
                FLog($"[SYNC] Create AFTER AddWaitable id={id}");
            }
            else
            {
                FLog($"[SYNC] Create AddInUseWaitable id={id}");
                _gd.CommandBufferPool.AddInUseWaitable(waitable);
            }

            SyncHandle handle = new()
            {
                ID = id,
                Waitable = waitable,
                FlushId = flushId,
            };

            lock (_handles)
            {
                _handles.Add(handle);
            }
            FLog($"[SYNC] Create END id={id} totalHandles={_handles.Count}");
        }

        public ulong GetCurrent()
        {
            FLog($"[SYNC] GetCurrent START first={_firstHandle} count={_handles.Count}");
            lock (_handles)
            {
                ulong lastHandle = _firstHandle;

                foreach (SyncHandle handle in _handles)
                {
                    lock (handle)
                    {
                        if (handle.Waitable == null)
                        {
                            continue;
                        }

                        if (handle.ID > lastHandle)
                        {
                            bool signaled = handle.Signalled || handle.Waitable.WaitForFences(_gd.Api, _device, 0);
                            if (signaled)
                            {
                                lastHandle = handle.ID;
                                handle.Signalled = true;
                            }
                        }
                    }
                }

                FLog($"[SYNC] GetCurrent END last={lastHandle}");
                return lastHandle;
            }
        }

        public void Wait(ulong id)
        {
            FLog($"[SYNC] Wait START id={id} first={_firstHandle} flushId={_flushId}");
            SyncHandle result = null;

            lock (_handles)
            {
                if ((long)(_firstHandle - id) > 0)
                {
                    FLog($"[SYNC] Wait EARLY RETURN already signaled id={id} first={_firstHandle}");
                    return;
                }

                foreach (SyncHandle handle in _handles)
                {
                    if (handle.ID == id)
                    {
                        result = handle;
                        break;
                    }
                }
            }

            if (result != null)
            {
                if (result.Waitable == null)
                {
                    FLog($"[SYNC] Wait NULL Waitable id={id}");
                    return;
                }

                long beforeTicks = Stopwatch.GetTimestamp();

                if (result.NeedsFlush(_flushId))
                {
                    FLog($"[SYNC] Wait BEFORE InterruptAction Flush id={id} needFlush={result.FlushId} curFlush={_flushId}");
                    _gd.InterruptAction(() =>
                    {
                        if (result.NeedsFlush(_flushId))
                        {
                            FLog($"[SYNC] Wait INSIDE InterruptAction FlushAllCommands id={id}");
                            _gd.FlushAllCommands();
                        }
                    });
                    FLog($"[SYNC] Wait AFTER InterruptAction id={id}");
                }

                lock (result)
                {
                    if (result.Waitable == null)
                    {
                        FLog($"[SYNC] Wait NULL after lock id={id}");
                        return;
                    }

                    FLog($"[SYNC] Wait BEFORE WaitForFences 1000ms id={id}");
                    bool signaled = result.Signalled || result.Waitable.WaitForFences(_gd.Api, _device, 1000000000);
                    FLog($"[SYNC] Wait AFTER WaitForFences id={id} signaled={signaled} prevSignalled={result.Signalled}");

                    if (!signaled)
                    {
                        Logger.Error?.PrintMsg(LogClass.Gpu, $"VK Sync Object {result.ID} failed to signal within 1000ms. Continuing...");
                        FLog($"[SYNC] Wait TIMEOUT 1000ms id={id}");
                    }
                    else
                    {
                        _waitTicks += Stopwatch.GetTimestamp() - beforeTicks;
                        result.Signalled = true;
                        FLog($"[SYNC] Wait signaled OK id={id}");
                    }
                }
            }
            else
            {
                FLog($"[SYNC] Wait NOT FOUND id={id}");
            }

            FLog($"[SYNC] Wait END id={id}");
        }

        public void Cleanup()
        {
            while (true)
            {
                SyncHandle first = null;
                lock (_handles)
                {
                    first = _handles.FirstOrDefault();
                }

                if (first == null || first.NeedsFlush(_flushId))
                {
                    break;
                }

                bool signaled = first.Waitable.WaitForFences(_gd.Api, _device, 0);
                if (signaled)
                {
                    lock (_handles)
                    {
                        lock (first)
                        {
                            _firstHandle = first.ID + 1;
                            _handles.RemoveAt(0);
                            Array.Clear(first.Waitable.Fences);
                            MultiFenceHolder.FencePool.Release(first.Waitable.Fences);
                            first.Waitable = null;
                        }
                    }
                }
                else
                {
                    break;
                }
            }
        }

        public long GetAndResetWaitTicks()
        {
            long result = _waitTicks;
            _waitTicks = 0;

            return result;
        }
    }
}
