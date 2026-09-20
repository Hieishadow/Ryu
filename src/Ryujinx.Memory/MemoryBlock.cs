using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Ryujinx.Memory
{
    public sealed class MemoryBlock : IWritableBlock, IDisposable
    {
        private readonly bool _usesSharedMemory;
        private readonly bool _isMirror;
        private readonly bool _viewCompatible;
        private readonly bool _forJit;
        private nint _sharedMemory;
        private nint _pointer;
        public nint Pointer => _pointer;
        public ulong Size { get; }

        private static void Log(string s){
            try{
                var p="/storage/emulated/0/Download/Ryubing/ryubing_log.txt";
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.AppendAllText(p, $"{DateTime.Now:HH:mm:ss.fff} [MEMBLOCK] {s}\n");
            }catch{}
        }

        public MemoryBlock(ulong size, MemoryAllocationFlags flags = MemoryAllocationFlags.None)
        {
            Log($"ctor START size={size} flags={flags} Reserve={flags.HasFlag(MemoryAllocationFlags.Reserve)} Mirrorable={flags.HasFlag(MemoryAllocationFlags.Mirrorable)}");

            if (flags.HasFlag(MemoryAllocationFlags.Mirrorable))
            {
                _sharedMemory = MemoryManagement.CreateSharedMemory(size, flags.HasFlag(MemoryAllocationFlags.Reserve));
                if (!flags.HasFlag(MemoryAllocationFlags.NoMap))
                {
                    _pointer = MemoryManagement.MapSharedMemory(_sharedMemory, size);
                }
                _usesSharedMemory = true;
            }
            else if (flags.HasFlag(MemoryAllocationFlags.Reserve))
            {
                _viewCompatible = flags.HasFlag(MemoryAllocationFlags.ViewCompatible);
                _forJit = flags.HasFlag(MemoryAllocationFlags.Jit);
                try{
                    _pointer = MemoryManagement.Reserve(size, _forJit, _viewCompatible);
                    Log($"Reserve OK ptr=0x{_pointer:X} size={size}");
                }catch(Exception ex){
                    Log($"Reserve FAIL size={size} ex={ex}");
                    throw;
                }
            }
            else
            {
                _forJit = flags.HasFlag(MemoryAllocationFlags.Jit);
                _pointer = MemoryManagement.Allocate(size, _forJit);
            }
            Size = size;
            Log($"ctor END OK Size={Size}");
        }

        private MemoryBlock(ulong size, nint sharedMemory)
        {
            _pointer = MemoryManagement.MapSharedMemory(sharedMemory, size);
            Size = size;
            _usesSharedMemory = true;
            _isMirror = true;
        }

        public MemoryBlock CreateMirror()
        {
            if (_sharedMemory == nint.Zero) throw new NotSupportedException("Mirroring is not supported on the memory block because the Mirrorable flag was not set.");
            return new MemoryBlock(Size, _sharedMemory);
        }

        public void Commit(ulong offset, ulong size)
        {
            Log($"Commit START offset=0x{offset:X} size=0x{size:X} ({size/1024/1024}MB) TotalSize={Size}");
            try{
                MemoryManagement.Commit(GetPointerInternal(offset, size), size, _forJit);
                Log($"Commit END OK offset=0x{offset:X}");
            }catch(Exception ex){
                Log($"Commit FAIL offset=0x{offset:X} size=0x{size:X} ex={ex}");
                throw;
            }
        }

        public void Decommit(ulong offset, ulong size)
        {
            Log($"Decommit offset=0x{offset:X} size=0x{size:X}");
            MemoryManagement.Decommit(GetPointerInternal(offset, size), size);
        }

        public void MapView(MemoryBlock srcBlock, ulong srcOffset, ulong dstOffset, ulong size)
        {
            if (srcBlock._sharedMemory == nint.Zero) throw new ArgumentException("The source memory block is not mirrorable, and thus cannot be mapped on the current block.");
            MemoryManagement.MapView(srcBlock._sharedMemory, srcOffset, GetPointerInternal(dstOffset, size), size, this);
        }
        public void UnmapView(MemoryBlock srcBlock, ulong offset, ulong size) => MemoryManagement.UnmapView(srcBlock._sharedMemory, GetPointerInternal(offset, size), size, this);
        public void Reprotect(ulong offset, ulong size, MemoryPermission permission, bool throwOnFail = true) => MemoryManagement.Reprotect(GetPointerInternal(offset, size), size, permission, _viewCompatible, throwOnFail);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public void Read(ulong offset, Span<byte> data) => GetSpan(offset, data.Length).CopyTo(data);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public T Read<T>(ulong offset) where T : unmanaged => GetRef<T>(offset);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public void Write(ulong offset, ReadOnlySpan<byte> data) => data.CopyTo(GetSpan(offset, data.Length));
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public void Write<T>(ulong offset, T data) where T : unmanaged => GetRef<T>(offset) = data;
        public void Copy(ulong dstOffset, ulong srcOffset, ulong size){ const int MaxChunkSize = 1 << 24; for (ulong offset = 0; offset < size; offset += MaxChunkSize){ int copySize = (int)Math.Min(MaxChunkSize, size - offset); Write(dstOffset + offset, GetSpan(srcOffset + offset, copySize)); } }
        public void Fill(ulong offset, ulong size, byte value){ const int MaxChunkSize = 1 << 24; for (ulong subOffset = 0; subOffset < size; subOffset += MaxChunkSize){ int copySize = (int)Math.Min(MaxChunkSize, size - subOffset); GetSpan(offset + subOffset, copySize).Fill(value); } }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public unsafe ref T GetRef<T>(ulong offset) where T : unmanaged { nint ptr = _pointer; ObjectDisposedException.ThrowIf(ptr == nint.Zero, this); int sz = Unsafe.SizeOf<T>(); ulong endOffset = offset + (ulong)sz; if (endOffset > Size || endOffset < offset) ThrowInvalidMemoryRegionException(); return ref Unsafe.AsRef<T>((void*)PtrAddr(ptr, offset)); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public nint GetPointer(ulong offset, ulong size) => GetPointerInternal(offset, size);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private nint GetPointerInternal(ulong offset, ulong size){ nint ptr = _pointer; ObjectDisposedException.ThrowIf(ptr == nint.Zero, this); ulong endOffset = offset + size; if (endOffset > Size || endOffset < offset) ThrowInvalidMemoryRegionException(); return PtrAddr(ptr, offset); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public unsafe Span<byte> GetSpan(ulong offset, int size) => new Span<byte>((void*)GetPointerInternal(offset, (ulong)size), size);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public unsafe Memory<byte> GetMemory(ulong offset, int size) => new NativeMemoryManager<byte>((byte*)GetPointerInternal(offset, (ulong)size), size).Memory;
        public WritableRegion GetWritableRegion(ulong offset, int size) => new WritableRegion(null, offset, GetMemory(offset, size));
        private static nint PtrAddr(nint pointer, ulong offset) => new nint(pointer.ToInt64() + (long)offset);
        public void Dispose(){ FreeMemory(); GC.SuppressFinalize(this); }
        ~MemoryBlock() => FreeMemory();
        private void FreeMemory(){ nint ptr = Interlocked.Exchange(ref _pointer, nint.Zero); if (ptr != nint.Zero){ if (_usesSharedMemory) MemoryManagement.UnmapSharedMemory(ptr, Size); else MemoryManagement.Free(ptr, Size); } if (!_isMirror){ nint sharedMemory = Interlocked.Exchange(ref _sharedMemory, nint.Zero); if (sharedMemory != nint.Zero) MemoryManagement.DestroySharedMemory(sharedMemory); } }
        public static bool SupportsFlags(MemoryAllocationFlags flags){ if (flags.HasFlag(MemoryAllocationFlags.ViewCompatible)){ if (OperatingSystem.IsWindows()) return OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17134); return OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(); } return true; }
        public static ulong GetPageSize() => (ulong)Environment.SystemPageSize;
        private static void ThrowInvalidMemoryRegionException() => throw new InvalidMemoryRegionException();
    }
}
