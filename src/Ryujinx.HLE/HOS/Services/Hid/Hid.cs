using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Kernel.Memory;
using Ryujinx.HLE.HOS.Services.Hid.Types.SharedMemory;
using System;
using System.IO;

namespace Ryujinx.HLE.HOS.Services.Hid
{
    public class Hid
    {
        private const string LogPath = "/data/user/0/com.ryubing.android/files/Ryujinx/hid_debug.log";

        private static void L(string msg)
        {
            try { File.AppendAllText(LogPath, $"{DateTime.Now}: {msg}\n"); } catch {}
            try { Console.WriteLine($"[HID] {msg}"); } catch {}
            try { Android.Util.Log.Info("RYUBING_HID", msg); } catch {}
        }

        private readonly Switch _device;
        private readonly SharedMemoryStorage _storage;

        internal const int SharedMemEntryCount = 17;

        internal ref SharedMemory SharedMemory
        {
            get
            {
                return ref _storage.GetRef<SharedMemory>(0);
            }
        }

        public DebugPadDevice DebugPad;
        public TouchDevice Touchscreen;
        public MouseDevice Mouse;
        public DebugMouseDevice DebugMouse;
        public KeyboardDevice Keyboard;
        public NpadDevices Npads;

        static Hid()
        {
        }

        internal Hid(in Switch device, SharedMemoryStorage storage)
        {
            L("ctor ENTER 394 NO FIELD - internal log");
            
            _device = device;
            _storage = storage;

            L("before devices");

            DebugPad = new DebugPadDevice(_device, true);
            L("DebugPad OK");
            Touchscreen = new TouchDevice(_device, true);
            L("Touchscreen OK");
            Mouse = new MouseDevice(_device, false);
            L("Mouse OK");
            DebugMouse = new DebugMouseDevice(_device, false);
            L("DebugMouse OK");
            Keyboard = new KeyboardDevice(_device, false);
            L("Keyboard OK");
            Npads = new NpadDevices(_device, true);
            L("Npads OK");

            L("ctor EXIT OK 394");
        }

        public void RefreshInputConfig(System.Collections.Generic.List<InputConfig> inputConfig) {}
        public ControllerKeys UpdateStickButtons(JoystickPosition leftStick, JoystickPosition rightStick) => 0;
        internal ulong GetTimestampTicks() => (ulong)Environment.TickCount64;
    }
}
