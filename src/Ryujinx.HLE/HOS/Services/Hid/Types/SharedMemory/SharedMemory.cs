using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.Common;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.DebugMouse;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.DebugPad;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.Keyboard;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.Mouse;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.Npad;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory.TouchScreen;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory
{
    [StructLayout(LayoutKind.Explicit, Size = 0x40000)]
    struct SharedMemory
    {
        [FieldOffset(0)]
        public RingLifo<DebugPadState> DebugPad;

        [FieldOffset(0x400)]
        public RingLifo<TouchScreenState> TouchScreen;

        [FieldOffset(0x3400)]
        public RingLifo<MouseState> Mouse;

        [FieldOffset(0x3800)]
        public RingLifo<KeyboardState> Keyboard;

        [FieldOffset(0x9A00)]
        public Array10<NpadState> Npads;

        [FieldOffset(0x3DC00)]
        public RingLifo<DebugMouseState> DebugMouse;

        [FieldOffset(0x3e200)]
        public NpadCondition Condition;

        public static SharedMemory Create()
        {
            // ANDROID FIX: não cria 262KB na stack
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                return default;
            }

            SharedMemory result = new()
            {
                DebugPad = RingLifo<DebugPadState>.Create(),
                TouchScreen = RingLifo<TouchScreenState>.Create(),
                Mouse = RingLifo<MouseState>.Create(),
                Keyboard = RingLifo<KeyboardState>.Create(),
                Condition = NpadCondition.Create(),
            };

            Span<NpadState> npadsSpan = result.Npads.AsSpan();

            for (int i = 0; i < npadsSpan.Length; i++)
            {
                npadsSpan[i] = NpadState.Create();
            }

            return result;
        }
    }
}
