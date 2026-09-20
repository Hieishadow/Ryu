using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static Ryujinx.Memory.MemoryManagerUnixHelper;

namespace Ryujinx.Memory
{
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("android")]
    static class MemoryManagementUnix
    {
        private static readonly ConcurrentDictionary<nint, ulong> _allocations = new();
        private static void Log(string s){
            try{
                var p="/storage/emulated/0/Download/Ryubing/ryubing_log.txt";
                Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                File.AppendAllText(p, $"{DateTime.Now:HH:mm:ss.fff} [UNIX] {s}\n");
            }catch{}
        }

        public static nint Allocate(ulong size, bool forJit)
        {
            MmapProts prot = MmapProts.PROT_READ | MmapProts.PROT_WRITE;
            if (OperatingSystem.IsAndroid() && forJit)
            {
                prot |= MmapProts.PROT_EXEC;
                Log($"Allocate JIT RWX size={size} ({size/1024/1024}MB) forJit={forJit} prot={prot}");
            }
            return AllocateInternal(size, prot, forJit);
        }

        public static nint Reserve(ulong size, bool forJit)
        {
            Log($"Reserve size={size} ({size/1024/1024}MB) forJit={forJit}");
            return AllocateInternal(size, MmapProts.PROT_NONE, forJit);
        }

        private static nint AllocateInternal(ulong size, MmapProts prot, bool forJit, bool shared = false)
        {
            MmapFlags flags = MmapFlags.MAP_ANONYMOUS;
            if (shared) flags |= MmapFlags.MAP_SHARED | MmapFlags.MAP_UNLOCKED;
            else flags |= MmapFlags.MAP_PRIVATE;
            if (prot == MmapProts.PROT_NONE) flags |= MmapFlags.MAP_NORESERVE;

            if (OperatingSystem.IsAndroid() && forJit)
            {
                prot |= MmapProts.PROT_EXEC;
            }

            if (OperatingSystem.IsMacOS() && OperatingSystem.IsMacOSVersionAtLeast(10, 14) && forJit)
            {
                flags |= MmapFlags.MAP_JIT_DARWIN;
                if (prot == (MmapProts.PROT_READ | MmapProts.PROT_WRITE)) prot |= MmapProts.PROT_EXEC;
            }

            nint ptr = Mmap(nint.Zero, size, prot, flags, -1, 0);
            if (ptr == MAP_FAILED) throw new SystemException($"mmap fail size={size} prot={prot} flags={flags} err={Marshal.GetLastPInvokeErrorMessage()}");
            _allocations.TryAdd(ptr, size);
            Log($"mmap OK ptr=0x{ptr:X} size={size} prot={prot} forJit={forJit}");
            return ptr;
        }

        public static void Commit(nint address, ulong size, bool forJit)
        {
            Log($"Commit addr=0x{address:X} size={size} ({size/1024/1024}MB) forJit={forJit}");
            
            // FIX S20 FE #495: só pede EXEC se for JIT, senão RW puro
            MmapProts prot = forJit 
                ? MmapProts.PROT_READ | MmapProts.PROT_WRITE | MmapProts.PROT_EXEC
                : MmapProts.PROT_READ | MmapProts.PROT_WRITE;

            int result = mprotect(address, size, prot);
            if (result != 0)
            {
                Log($"mprotect FAIL {Marshal.GetLastPInvokeErrorMessage()} -> MAP_FIXED {prot}");
                nint mapped = Mmap(address, size, prot, MmapFlags.MAP_FIXED | MmapFlags.MAP_PRIVATE | MmapFlags.MAP_ANONYMOUS, -1, 0);
                if (mapped == MAP_FAILED)
                {
                    Log($"MAP_FIXED FAIL {Marshal.GetLastPInvokeErrorMessage()}");
                    throw new SystemException($"Commit fail 0x{address:X} {Marshal.GetLastPInvokeErrorMessage()}");
                }
                Log($"MAP_FIXED OK addr=0x{mapped:X}");
                return;
            }
            Log($"Commit OK addr=0x{address:X}");
        }

        public static void Decommit(nint address, ulong size)
        {
            Log($"Decommit addr=0x{address:X} size={size}");
            madvise(address, size, 4);
            mprotect(address, size, MmapProts.PROT_NONE);
        }

        public static bool Reprotect(nint address, ulong size, MemoryPermission permission)
        {
            MmapProts prot = GetProtection(permission);
            if (OperatingSystem.IsAndroid() && prot.HasFlag(MmapProts.PROT_EXEC))
            {
                if (mprotect(address, size, prot) != 0)
                {
                    var fallback = prot & ~MmapProts.PROT_EXEC;
                    if (fallback == MmapProts.PROT_NONE) fallback = MmapProts.PROT_READ | MmapProts.PROT_WRITE;
                    Log($"Reprotect EXEC FAIL -> fallback {fallback}");
                    return mprotect(address, size, fallback) == 0;
                }
                return true;
            }
            return mprotect(address, size, prot) == 0;
        }

        private static MmapProts GetProtection(MemoryPermission permission) => permission switch
        {
            MemoryPermission.None => MmapProts.PROT_NONE,
            MemoryPermission.Read => MmapProts.PROT_READ,
            MemoryPermission.ReadAndWrite => MmapProts.PROT_READ | MmapProts.PROT_WRITE,
            MemoryPermission.ReadAndExecute => MmapProts.PROT_READ | MmapProts.PROT_EXEC,
            MemoryPermission.ReadWriteExecute => MmapProts.PROT_READ | MmapProts.PROT_WRITE | MmapProts.PROT_EXEC,
            MemoryPermission.Execute => MmapProts.PROT_EXEC,
            _ => throw new MemoryProtectionException(permission),
        };

        public static bool Free(nint address){ if (_allocations.TryRemove(address, out ulong size)) return munmap(address, size) == 0; return munmap(address, 0) == 0; }
        public static bool Unmap(nint address, ulong size) => munmap(address, size) == 0;
        public unsafe static nint CreateSharedMemory(ulong size, bool reserve){ int fd; if (OperatingSystem.IsMacOS()){ byte[] memName = "Ryujinx-XXXXXX"u8.ToArray(); fixed (byte* pMemName = memName){ fd = shm_open((nint)pMemName, 0x2 | 0x200 | 0x800 | 0x400, 384); if (fd == -1) throw new SystemException(Marshal.GetLastPInvokeErrorMessage()); shm_unlink((nint)pMemName); } } else { byte[] fileName = "/dev/shm/Ryujinx-XXXXXX"u8.ToArray(); fixed (byte* pFileName = fileName){ fd = mkstemp((nint)pFileName); if (fd == -1) throw new SystemException(Marshal.GetLastPInvokeErrorMessage()); unlink((nint)pFileName); } } if (ftruncate(fd, (nint)size) != 0) throw new SystemException(Marshal.GetLastPInvokeErrorMessage()); return fd; }
        public static void DestroySharedMemory(nint handle) => close(handle.ToInt32());
        public static nint MapSharedMemory(nint handle, ulong size) => Mmap(nint.Zero, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE, MmapFlags.MAP_SHARED, handle.ToInt32(), 0);
        public static void UnmapSharedMemory(nint address, ulong size) => munmap(address, size);
        public static void MapView(nint sharedMemory, ulong srcOffset, nint location, ulong size) => Mmap(location, size, MmapProts.PROT_READ | MmapProts.PROT_WRITE, MmapFlags.MAP_FIXED | MmapFlags.MAP_SHARED, sharedMemory.ToInt32(), (long)srcOffset);
        public static void UnmapView(nint location, ulong size) => Mmap(location, size, MmapProts.PROT_NONE, MmapFlags.MAP_FIXED | MmapFlags.MAP_PRIVATE | MmapFlags.MAP_ANONYMOUS | MmapFlags.MAP_NORESERVE, -1, 0);
    }
}
