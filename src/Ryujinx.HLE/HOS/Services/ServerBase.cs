using Microsoft.IO;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel;
using Ryujinx.HLE.HOS.Kernel.Ipc;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.Horizon;
using Ryujinx.Horizon.Common;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services
{
    class ServerBase : IDisposable
    {
        // Must be the maximum value used by services (highest one know is the one used by nvservices = 0x8000).
        // Having a size that is too low will cause failures as data copy will fail if the receiving buffer is
        // not large enough.
        private const int PointerBufferSize = 0x8000;

        private static uint[] DefaultCapabilities => [
            (((uint)KScheduler.CpuCoresCount - 1) << 24) + (((uint)KScheduler.CpuCoresCount - 1) << 16) + 0x63F7u,
            0x1FFFFFCF,
            0x207FFFEF,
            0x47E0060F,
            0x0048BFFF,
            0x01007FFF,
        ];

        // The amount of time Dispose() will wait to Join() the thread executing the ServerLoop()
        private static readonly TimeSpan _threadJoinTimeout = TimeSpan.FromSeconds(3);

        private readonly KernelContext _context;
        private KProcess _selfProcess;
        private KEvent _wakeEvent;
        private int _wakeHandle = 0;
        private ulong _heapAddr;

        private readonly ReaderWriterLockSlim _handleLock = new();
        private readonly Dictionary<int, IpcService> _sessions = new();
        private readonly Dictionary<int, Func<IpcService>> _ports = new();
        private readonly List<KThread> _serverThreads = [];

        private readonly int _threadCount;

        private sealed class ServerLoopState : IDisposable
        {
            public readonly KThread Thread;
            public readonly ulong RecvListAddr;
            public readonly RecyclableMemoryStream RequestDataStream;
            public readonly BinaryReader RequestDataReader;
            public readonly RecyclableMemoryStream ResponseDataStream;
            public readonly BinaryWriter ResponseDataWriter;

            public ServerLoopState(KThread thread, ulong recvListAddr)
            {
                Thread = thread;
                RecvListAddr = recvListAddr;
                RequestDataStream = MemoryStreamManager.Shared.GetStream();
                RequestDataReader = new BinaryReader(RequestDataStream);
                ResponseDataStream = MemoryStreamManager.Shared.GetStream();
                ResponseDataWriter = new BinaryWriter(ResponseDataStream);
            }

            public void Dispose()
            {
                RequestDataReader.Dispose();
                RequestDataStream.Dispose();
                ResponseDataWriter.Dispose();
                ResponseDataStream.Dispose();
            }
        }

        private int _isDisposed = 0;

        public ManualResetEvent InitDone { get; }
        public string Name { get; }
        public Func<IpcService> SmObjectFactory { get; }

        public ServerBase(KernelContext context, string name, Func<IpcService> smObjectFactory = null, int threadCount = 1)
        {
            _context = context;
            _threadCount = Math.Max(threadCount, 1);

            InitDone = new ManualResetEvent(false);
            Name = name;
            SmObjectFactory = smObjectFactory;

            const ProcessCreationFlags Flags =
                ProcessCreationFlags.EnableAslr |
                ProcessCreationFlags.AddressSpace64Bit |
                ProcessCreationFlags.Is64Bit |
                ProcessCreationFlags.PoolPartitionSystem;

            ProcessCreationInfo creationInfo = new(Name, 1, 0, 0x8000000, 1, Flags, 0, 0);

            KernelStatic.StartInitialProcess(context, creationInfo, DefaultCapabilities, 44, () =>
            {
                var currentThread = KernelStatic.GetCurrentThread();
                currentThread.HostThread.Name = $"{{{Name}}}";

                Main();
            });
        }

        private void AddPort(int serverPortHandle, Func<IpcService> objectFactory)
        {
            bool lockTaken = false;
            try
            {
                lockTaken = _handleLock.TryEnterWriteLock(Timeout.Infinite);

                _ports.Add(serverPortHandle, objectFactory);
            }
            finally
            {
                if (lockTaken)
                {
                    _handleLock.ExitWriteLock();
                }
            }
        }

        public void AddSessionObj(KServerSession serverSession, IpcService obj)
        {
            // Ensure that the sever loop is running.
            InitDone.WaitOne();

            _selfProcess.HandleTable.GenerateHandle(serverSession, out int serverSessionHandle);

            AddSessionObj(serverSessionHandle, obj);
        }

        public void AddSessionObj(int serverSessionHandle, IpcService obj)
        {
            bool lockTaken = false;
            try
            {
                lockTaken = _handleLock.TryEnterWriteLock(Timeout.Infinite);

                _sessions.Add(serverSessionHandle, obj);
            }
            finally
            {
                if (lockTaken)
                {
                    _handleLock.ExitWriteLock();
                }
            }

            _wakeEvent.WritableEvent.Signal();
        }

        private IpcService GetSessionObj(int serverSessionHandle)
        {
            bool lockTaken = false;
            try
            {
                lockTaken = _handleLock.TryEnterReadLock(Timeout.Infinite);

                return _sessions[serverSessionHandle];
            }
            finally
            {
                if (lockTaken)
                {
                    _handleLock.ExitReadLock();
                }
            }
        }

        private bool RemoveSessionObj(int serverSessionHandle, out IpcService obj)
        {
            bool lockTaken = false;
            try
            {
                lockTaken = _handleLock.TryEnterWriteLock(Timeout.Infinite);

                return _sessions.Remove(serverSessionHandle, out obj);
            }
            finally
            {
                if (lockTaken)
                {
                    _handleLock.ExitWriteLock();
                }
            }
        }

        private void Main()
        {
            _selfProcess = KernelStatic.GetCurrentProcess();

            if (SmObjectFactory != null)
            {
                _context.Syscall.ManageNamedPort(out int serverPortHandle, "sm:", 50);

                AddPort(serverPortHandle, SmObjectFactory);
            }

            _wakeEvent = new KEvent(_context);
            Result result = _selfProcess.HandleTable.GenerateHandle(_wakeEvent.ReadableEvent, out _wakeHandle);

            if (result != Result.Success)
            {
                throw new InvalidOperationException($"Failed to create wake event handle for {Name}: {result}");
            }

            _context.Syscall.SetHeapSize(out _heapAddr, Math.Max(0x200000UL, (ulong)PointerBufferSize * (ulong)_threadCount));

            KThread currentThread = KernelStatic.GetCurrentThread();

            lock (_serverThreads)
            {
                _serverThreads.Add(currentThread);
            }

            InitDone.Set();

            for (int i = 1; i < _threadCount; i++)
            {
                int workerIndex = i;

                result = _context.Syscall.CreateThread(
                    out int handle,
                    0,
                    0,
                    0,
                    currentThread.DynamicPriority,
                    _selfProcess.DefaultCpuCore,
                    () =>
                    {
                        KernelStatic.GetCurrentThread().HostThread.Name = $"{{{Name}.{workerIndex}}}";
                        ServerLoop(workerIndex);
                    });

                if (result != Result.Success)
                {
                    Logger.Warning?.Print(LogClass.Service, $"Failed to create {Name} service worker {workerIndex}: {result}");
                    continue;
                }

                KThread workerThread = _selfProcess.HandleTable.GetKThread(handle);

                lock (_serverThreads)
                {
                    _serverThreads.Add(workerThread);
                }

                result = _context.Syscall.StartThread(handle);
                _context.Syscall.CloseHandle(handle);

                if (result != Result.Success)
                {
                    Logger.Warning?.Print(LogClass.Service, $"Failed to start {Name} service worker {workerIndex}: {result}");
                }
            }

            ServerLoop(0);
        }

        private void ServerLoop(int workerIndex)
        {
            KThread selfThread = KernelStatic.GetCurrentThread();

            HorizonStatic.Register(
                default,
                _context.Syscall,
                _selfProcess.CpuMemory,
                selfThread.ThreadContext,
                (int)selfThread.ThreadContext.GetX(1));

            using ServerLoopState state = new(selfThread, _heapAddr + (ulong)(PointerBufferSize * workerIndex));

            ulong messagePtr = selfThread.TlsAddress;
            _selfProcess.CpuMemory.Write(messagePtr + 0x0, 0);
            _selfProcess.CpuMemory.Write(messagePtr + 0x4, 2 << 10);
            _selfProcess.CpuMemory.Write(messagePtr + 0x8, state.RecvListAddr | ((ulong)PointerBufferSize << 48));
            int replyTargetHandle = 0;

            while (true)
            {
                int portHandleCount;
                int handleCount;
                int[] handles;

                bool handleLockTaken = false;
                try
                {
                    handleLockTaken = _handleLock.TryEnterReadLock(Timeout.Infinite);

                    portHandleCount = _ports.Count;

                    handleCount = portHandleCount + _sessions.Count + 1;

                    handles = ArrayPool<int>.Shared.Rent(handleCount);

                    handles[0] = _wakeHandle;

                    _ports.Keys.CopyTo(handles, 1);

                    _sessions.Keys.CopyTo(handles, portHandleCount + 1);
                }
                finally
                {
                    if (handleLockTaken)
                    {
                        _handleLock.ExitReadLock();
                    }
                }

                Result rc = _context.Syscall.ReplyAndReceive(out int signaledIndex, handles.AsSpan(0, handleCount), replyTargetHandle, -1);

                selfThread.HandlePostSyscall();

                if (!selfThread.Context.Running)
                {
                    break;
                }

                replyTargetHandle = 0;

                if (rc == Result.Success && signaledIndex >= portHandleCount + 1)
                {
                    // We got a IPC request, process it, pass to the appropriate service if needed.
                    int signaledHandle = handles[signaledIndex];

                    if (Process(state, signaledHandle))
                    {
                        replyTargetHandle = signaledHandle;
                    }
                }
                else
                {
                    if (rc == Result.Success)
                    {
                        if (signaledIndex > 0)
                        {
                            // We got a new connection, accept the session to allow servicing future requests.
                            if (_context.Syscall.AcceptSession(out int serverSessionHandle, handles[signaledIndex]) == Result.Success)
                            {
                                bool handleWriteLockTaken = false;
                                try
                                {
                                    handleWriteLockTaken = _handleLock.TryEnterWriteLock(Timeout.Infinite);
                                    IpcService obj = _ports[handles[signaledIndex]].Invoke();
                                    _sessions.Add(serverSessionHandle, obj);
                                }
                                finally
                                {
                                    if (handleWriteLockTaken)
                                    {
                                        _handleLock.ExitWriteLock();
                                    }
                                }
                            }
                        }
                        else
                        {
                            // The _wakeEvent signalled, which means we have a new session.
                            _wakeEvent.WritableEvent.Clear();
                        }
                    }
                    else if (rc == KernelResult.PortRemoteClosed && signaledIndex >= 0 && SmObjectFactory != null)
                    {
                        DestroySession(handles[signaledIndex]);
                    }

                    _selfProcess.CpuMemory.Write(messagePtr + 0x0, 0);
                    _selfProcess.CpuMemory.Write(messagePtr + 0x4, 2 << 10);
                    _selfProcess.CpuMemory.Write(messagePtr + 0x8, state.RecvListAddr | ((ulong)PointerBufferSize << 48));
                }

                ArrayPool<int>.Shared.Return(handles);
            }

            Dispose();
        }

        private void DestroySession(int serverSessionHandle)
        {
            _context.Syscall.CloseHandle(serverSessionHandle);

            if (RemoveSessionObj(serverSessionHandle, out IpcService session))
            {
                (session as IDisposable)?.Dispose();
            }
        }

        private bool Process(ServerLoopState state, int serverSessionHandle)
        {
            IpcMessage request = ReadRequest(state);

            IpcMessage response = new();

            ulong tempAddr = state.RecvListAddr;
            int sizesOffset = request.RawData.Length - ((request.RecvListBuff.Count * 2 + 3) & ~3);

            bool noReceive = true;

            for (int i = 0; i < request.ReceiveBuff.Count; i++)
            {
                noReceive &= (request.ReceiveBuff[i].Position == 0);
            }

            if (noReceive)
            {
                response.PtrBuff.EnsureCapacity(request.RecvListBuff.Count);

                for (int i = 0; i < request.RecvListBuff.Count; i++)
                {
                    ulong size = (ulong)BinaryPrimitives.ReadInt16LittleEndian(request.RawData.AsSpan(sizesOffset + i * 2, 2));

                    response.PtrBuff.Add(new IpcPtrBuffDesc(tempAddr, (uint)i, size));

                    request.RecvListBuff[i] = new IpcRecvListBuffDesc(tempAddr, size);

                    tempAddr += size;
                }
            }

            bool shouldReply = true;
            bool isTipcCommunication = false;

            state.RequestDataStream.SetLength(0);
            state.RequestDataStream.Write(request.RawData);
            state.RequestDataStream.Position = 0;

            if (request.Type is IpcMessageType.CmifRequest or
                IpcMessageType.CmifRequestWithContext)
            {
                response.Type = IpcMessageType.CmifResponse;

                state.ResponseDataStream.SetLength(0);

                ServiceCtx context = new(
                    _context.Device,
                    _selfProcess,
                    _selfProcess.CpuMemory,
                    state.Thread,
                    request,
                    response,
                    state.RequestDataReader,
                    state.ResponseDataWriter);

                GetSessionObj(serverSessionHandle).CallCmifMethod(context);

                response.RawData = state.ResponseDataStream.ToArray();
            }
            else if (request.Type is IpcMessageType.CmifControl or
                     IpcMessageType.CmifControlWithContext)
            {
#pragma warning disable IDE0059 // Remove unnecessary value assignment
                uint magic = (uint)state.RequestDataReader.ReadUInt64();
#pragma warning restore IDE0059
                uint cmdId = (uint)state.RequestDataReader.ReadUInt64();

                switch (cmdId)
                {
                    case 0:
                        FillHipcResponse(state, response, 0, GetSessionObj(serverSessionHandle).ConvertToDomain());
                        break;

                    case 3:
                        FillHipcResponse(state, response, 0, PointerBufferSize);
                        break;

                    // TODO: Whats the difference between IpcDuplicateSession/Ex?
                    case 2:
                    case 4:
                        {
                            _ = state.RequestDataReader.ReadInt32();

                            _context.Syscall.CreateSession(out int dupServerSessionHandle, out int dupClientSessionHandle, false, 0);

                            bool writeLockTaken = false;
                            try
                            {
                                writeLockTaken = _handleLock.TryEnterWriteLock(Timeout.Infinite);
                                _sessions[dupServerSessionHandle] = _sessions[serverSessionHandle];
                            }
                            finally
                            {
                                if (writeLockTaken)
                                {
                                    _handleLock.ExitWriteLock();
                                }
                            }

                            response.HandleDesc = IpcHandleDesc.MakeMove(dupClientSessionHandle);

                            FillHipcResponse(state, response, 0);

                            break;
                        }

                    default:
                        throw new NotImplementedException(cmdId.ToString());
                }
            }
            else if (request.Type is IpcMessageType.CmifCloseSession or IpcMessageType.TipcCloseSession)
            {
                DestroySession(serverSessionHandle);
                shouldReply = false;
            }
            // If the type is past 0xF, we are using TIPC
            else if (request.Type > IpcMessageType.TipcCloseSession)
            {
                isTipcCommunication = true;

                // Response type is always the same as request on TIPC.
                response.Type = request.Type;

                state.ResponseDataStream.SetLength(0);

                ServiceCtx context = new(
                    _context.Device,
                    _selfProcess,
                    _selfProcess.CpuMemory,
                    state.Thread,
                    request,
                    response,
                    state.RequestDataReader,
                    state.ResponseDataWriter);

                GetSessionObj(serverSessionHandle).CallTipcMethod(context);

                response.RawData = state.ResponseDataStream.ToArray();

                RecyclableMemoryStream responseStream = response.GetStreamTipc();
                _selfProcess.CpuMemory.Write(state.Thread.TlsAddress, responseStream.GetReadOnlySequence());
                MemoryStreamManager.Shared.ReleaseStream(responseStream);
            }
            else
            {
                throw new NotImplementedException(request.Type.ToString());
            }

            if (!isTipcCommunication)
            {
                RecyclableMemoryStream responseStream = response.GetStream((long)state.Thread.TlsAddress, state.RecvListAddr | ((ulong)PointerBufferSize << 48));
                _selfProcess.CpuMemory.Write(state.Thread.TlsAddress, responseStream.GetReadOnlySequence());
                MemoryStreamManager.Shared.ReleaseStream(responseStream);
            }

            return shouldReply;
        }

        private IpcMessage ReadRequest(ServerLoopState state)
        {
            const int MessageSize = 0x100;

            using SpanOwner<byte> reqDataOwner = SpanOwner<byte>.Rent(MessageSize);

            Span<byte> reqDataSpan = reqDataOwner.Span;

            _selfProcess.CpuMemory.Read(state.Thread.TlsAddress, reqDataSpan);

            IpcMessage request = new(reqDataSpan, (long)state.Thread.TlsAddress);

            return request;
        }

        private void FillHipcResponse(ServerLoopState state, IpcMessage response, long result)
        {
            FillHipcResponse(state, response, result, ReadOnlySpan<byte>.Empty);
        }

        private void FillHipcResponse(ServerLoopState state, IpcMessage response, long result, int value)
        {
            Span<byte> span = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(span, value);
            FillHipcResponse(state, response, result, span);
        }

        private void FillHipcResponse(ServerLoopState state, IpcMessage response, long result, ReadOnlySpan<byte> data)
        {
            response.Type = IpcMessageType.CmifResponse;

            state.ResponseDataStream.SetLength(0);

            state.ResponseDataStream.Write(IpcMagic.Sfco);
            state.ResponseDataStream.Write(result);

            state.ResponseDataStream.Write(data);

            response.RawData = state.ResponseDataStream.ToArray();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _isDisposed, 1) == 0)
            {
                List<KThread> serverThreads;

                lock (_serverThreads)
                {
                    serverThreads = [.. _serverThreads];
                }

                foreach (KThread thread in serverThreads)
                {
                    if (thread?.HostThread == null || thread.HostThread.ManagedThreadId == Environment.CurrentManagedThreadId)
                    {
                        continue;
                    }

                    if (thread.HostThread.Join(_threadJoinTimeout) == false)
                    {
                        Logger.Warning?.Print(LogClass.Service, $"The ServerBase thread didn't terminate within {_threadJoinTimeout:g}, waiting longer.");

                        thread.HostThread.Join(Timeout.Infinite);
                    }
                }

                _selfProcess.HandleTable.CloseHandle(_wakeHandle);

                foreach (IpcService service in _sessions.Values)
                {
                    (service as IDisposable)?.Dispose();

                    service.DestroyAtExit();
                }

                _sessions.Clear();
                _ports.Clear();
                _handleLock.Dispose();

                InitDone.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
