using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Memory
{
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("android")]
    public static partial class MemoryManagerUnixHelper
    {
        [Flags]
        public enum MmapProts : uint
        {
            PROT_NONE = 0,
            PROT_READ = 1,
            PROT_WRITE = 2,
            PROT_EXEC = 4,
        }

        [Flags]
        public enum MmapFlags : uint
        {
            MAP_SHARED = 1,
            MAP_PRIVATE = 2,
            MAP_ANONYMOUS = 4,
            MAP_NORESERVE = 8,
            MAP_FIXED = 16,
            MAP_UNLOCKED = 32,
            MAP_JIT_DARWIN = 0x800,
        }

        public const nint MAP_FAILED = -1;

        private const int MAP_ANONYMOUS_LINUX_GENERIC = 0x20;
        private const int MAP_NORESERVE_LINUX_GENERIC = 0x4000;
        private const int MAP_UNLOCKED_LINUX_GENERIC = 0x80000;

        private const int MAP_NORESERVE_DARWIN = 0x40;
        private const int MAP_ANONYMOUS_DARWIN = 0x1000;

        public const int MADV_DONTNEED = 4;
        public const int MADV_REMOVE = 9;

        [LibraryImport("libc", EntryPoint = "mmap", SetLastError = true)]
        private static partial nint Internal_mmap(nint address, ulong length, MmapProts prot, int flags, int fd, long offset);

        [LibraryImport("libc", SetLastError = true)]
        public static partial int mprotect(nint address, ulong length, MmapProts prot);

        [LibraryImport("libc", SetLastError = true)]
        public static partial int munmap(nint address, ulong length);

        [LibraryImport("libc", SetLastError = true)]
        public static partial nint mremap(nint old_address, ulong old_size, ulong new_size, int flags, nint new_address);

        [LibraryImport("libc", SetLastError = true)]
        public static partial int madvise(nint address, ulong size, int advice);

        [LibraryImport("libc", SetLastError = true)]
        public static partial int mkstemp(nint template);

        [LibraryImport("libc", SetLastError = true)]
        public static partial int unlink(nint pathname);

        [LibraryImport("libc", SetLastError = true)]
        public static partial int ftruncate(int fildes, nint length);

        [LibraryImport("libc", SetLastError = true)]
        public static partial int close(int fd);

        [LibraryImport("libc", SetLastError = true)]
        public static partial int shm_open(nint name, int oflag, uint mode);

        [LibraryImport("libc", SetLastError = true)]
        public static partial int shm_unlink(nint name);

        private static int MmapFlagsToSystemFlags(MmapFlags flags)
        {
            int result = 0;

            if (flags.HasFlag(MmapFlags.MAP_SHARED))
                result |= 0x01; // MAP_SHARED

            if (flags.HasFlag(MmapFlags.MAP_PRIVATE))
                result |= 0x02; // MAP_PRIVATE

            if (flags.HasFlag(MmapFlags.MAP_FIXED))
                result |= 0x10; // MAP_FIXED

            if (flags.HasFlag(MmapFlags.MAP_ANONYMOUS))
            {
                if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
                    result |= MAP_ANONYMOUS_LINUX_GENERIC; // 0x20
                else if (OperatingSystem.IsMacOS())
                    result |= MAP_ANONYMOUS_DARWIN;
                else
                    result |= MAP_ANONYMOUS_LINUX_GENERIC;
            }

            if (flags.HasFlag(MmapFlags.MAP_NORESERVE))
            {
                if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
                    result |= MAP_NORESERVE_LINUX_GENERIC; // 0x4000
                else if (OperatingSystem.IsMacOS())
                    result |= MAP_NORESERVE_DARWIN;
            }

            if (flags.HasFlag(MmapFlags.MAP_UNLOCKED))
            {
                if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
                    result |= MAP_UNLOCKED_LINUX_GENERIC;
                // no Darwin
            }

            if (flags.HasFlag(MmapFlags.MAP_JIT_DARWIN) && OperatingSystem.IsMacOS() && OperatingSystem.IsMacOSVersionAtLeast(10, 14))
                result |= (int)MmapFlags.MAP_JIT_DARWIN;

            if (result == 0)
                result = 0x22; // PRIVATE | ANONYMOUS fallback

            return result;
        }

        public static nint Mmap(nint address, ulong length, MmapProts prot, MmapFlags flags, int fd, long offset)
        {
            return Internal_mmap(address, length, prot, MmapFlagsToSystemFlags(flags), fd, offset);
        }
    }
}
