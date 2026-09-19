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
        private readonly Switch _device;
        private readonly SharedMemoryStorage _storage;
        private SharedMemory _localSharedMemory;
        private bool _useLocal = false;

        internal const int SharedMemEntryCount = 17;

        internal ref SharedMemory SharedMemory
        {
            get
            {
                if (_useLocal) return ref _localSharedMemory;
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
            if (Environment.OSVersion.Platform == PlatformID.Unix) return;
        }

        internal Hid(in Switch device, SharedMemoryStorage storage)
        {
            _device = device;
            _storage = storage;
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now}: [HID] ctor ENTER\n"); } catch {}
            _useLocal = true;
            _localSharedMemory = default;
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now}: [HID] local default OK\n"); } catch {}
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now}: [HID] BEFORE InitDevices\n"); } catch {}
            DebugPad = new DebugPadDevice(_device, true);
            Touchscreen = new TouchDevice(_device, true);
            Mouse = new MouseDevice(_device, false);
            DebugMouse = new DebugMouseDevice(_device, false);
            Keyboard = new KeyboardDevice(_device, false);
            Npads = new NpadDevices(_device, true);
            try { File.AppendAllText("/storage/emulated/0/Download/Ryubing/ryubing_log.txt", $"{DateTime.Now}: [HID] InitDevices OK\n"); } catch {}
        }

        public void RefreshInputConfig(System.Collections.Generic.List<InputConfig> inputConfig) {}
        public ControllerKeys UpdateStickButtons(JoystickPosition leftStick, JoystickPosition rightStick) => 0;
        internal ulong GetTimestampTicks() => 0;
    }
}
