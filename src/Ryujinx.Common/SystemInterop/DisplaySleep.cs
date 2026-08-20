using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Common.SystemInterop
{
    public partial class DisplaySleep
    {
        [Flags]
        enum EXECUTION_STATE : uint
        {
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001,
        }

        [LibraryImport("kernel32.dll", SetLastError = true)]
        private static partial EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        private const string CoreFoundationLibraryName = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const string IOKitLibraryName = "/System/Library/Frameworks/IOKit.framework/IOKit";

        private const uint CFStringEncodingUTF8 = 0x08000100;
        private const uint AssertionLevelOn = 255;

        // Keeping the display awake also keeps the system awake, so this covers both flags used on Windows.
        private const string AssertionType = "PreventUserIdleDisplaySleep";
        private const string AssertionName = "Ryujinx is running a game";

        [LibraryImport(CoreFoundationLibraryName, StringMarshalling = StringMarshalling.Utf8)]
        private static partial nint CFStringCreateWithCString(nint allocator, string str, uint encoding);

        [LibraryImport(CoreFoundationLibraryName)]
        private static partial void CFRelease(nint cf);

        [LibraryImport(IOKitLibraryName)]
        private static partial int IOPMAssertionCreateWithName(nint assertionType, uint assertionLevel, nint assertionName, out uint assertionId);

        [LibraryImport(IOKitLibraryName)]
        private static partial int IOPMAssertionRelease(uint assertionId);

        // kIOPMNullAssertionID, meaning no assertion is currently held.
        private static uint _assertionId;

        static public void Prevent()
        {
            if (OperatingSystem.IsWindows())
            {
                SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_DISPLAY_REQUIRED);
            }
            else if (OperatingSystem.IsMacOS() && _assertionId == 0)
            {
                nint assertionType = CFStringCreateWithCString(nint.Zero, AssertionType, CFStringEncodingUTF8);
                nint assertionName = CFStringCreateWithCString(nint.Zero, AssertionName, CFStringEncodingUTF8);

                if (IOPMAssertionCreateWithName(assertionType, AssertionLevelOn, assertionName, out uint assertionId) == 0)
                {
                    _assertionId = assertionId;
                }

                CFRelease(assertionType);
                CFRelease(assertionName);
            }
        }

        static public void Restore()
        {
            if (OperatingSystem.IsWindows())
            {
                SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
            }
            else if (OperatingSystem.IsMacOS() && _assertionId != 0)
            {
                IOPMAssertionRelease(_assertionId);

                _assertionId = 0;
            }
        }
    }
}
