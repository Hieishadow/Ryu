using System;
using PhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;

namespace Ryujinx.Input
{
    public readonly struct Button
    {
        public readonly ButtonType Type;
        private readonly uint _rawValue;

        public Button(PhysicalKey key)
        {
            Type = ButtonType.PhysicalKey;
            _rawValue = (uint)key;
        }

        public Button(GamepadButtonInputId gamepad)
        {
            Type = ButtonType.GamepadButtonInputId;
            _rawValue = (uint)gamepad;
        }

        public Button(StickInputId stick)
        {
            Type = ButtonType.StickId;
            _rawValue = (uint)stick;
        }

        public T AsHidType<T>() where T : Enum
        {
            return (T)Enum.ToObject(typeof(T), _rawValue);
        }
    }
}
