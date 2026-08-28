using System;
using System.Collections.Generic;
using AvaPhysicalKey = Avalonia.Input.PhysicalKey;
using PhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;

namespace Ryujinx.Ava.Input
{
    internal static class AvaloniaKeyboardMappingHelper
    {
        private static readonly AvaPhysicalKey[] _physicalKeyMapping =
        [
            AvaPhysicalKey.None,
            AvaPhysicalKey.ShiftLeft,
            AvaPhysicalKey.ShiftRight,
            AvaPhysicalKey.ControlLeft,
            AvaPhysicalKey.ControlRight,
            AvaPhysicalKey.AltLeft,
            AvaPhysicalKey.AltRight,
            AvaPhysicalKey.MetaLeft,
            AvaPhysicalKey.MetaRight,
            AvaPhysicalKey.ContextMenu,
            AvaPhysicalKey.F1,
            AvaPhysicalKey.F2,
            AvaPhysicalKey.F3,
            AvaPhysicalKey.F4,
            AvaPhysicalKey.F5,
            AvaPhysicalKey.F6,
            AvaPhysicalKey.F7,
            AvaPhysicalKey.F8,
            AvaPhysicalKey.F9,
            AvaPhysicalKey.F10,
            AvaPhysicalKey.F11,
            AvaPhysicalKey.F12,
            AvaPhysicalKey.F13,
            AvaPhysicalKey.F14,
            AvaPhysicalKey.F15,
            AvaPhysicalKey.F16,
            AvaPhysicalKey.F17,
            AvaPhysicalKey.F18,
            AvaPhysicalKey.F19,
            AvaPhysicalKey.F20,
            AvaPhysicalKey.F21,
            AvaPhysicalKey.F22,
            AvaPhysicalKey.F23,
            AvaPhysicalKey.F24,
            AvaPhysicalKey.None,
            AvaPhysicalKey.None,
            AvaPhysicalKey.None,
            AvaPhysicalKey.None,
            AvaPhysicalKey.None,
            AvaPhysicalKey.None,
            AvaPhysicalKey.None,
            AvaPhysicalKey.None,
            AvaPhysicalKey.None,
            AvaPhysicalKey.None,
            AvaPhysicalKey.None,
            AvaPhysicalKey.ArrowUp,
            AvaPhysicalKey.ArrowDown,
            AvaPhysicalKey.ArrowLeft,
            AvaPhysicalKey.ArrowRight,
            AvaPhysicalKey.Enter,
            AvaPhysicalKey.Escape,
            AvaPhysicalKey.Space,
            AvaPhysicalKey.Tab,
            AvaPhysicalKey.Backspace,
            AvaPhysicalKey.Insert,
            AvaPhysicalKey.Delete,
            AvaPhysicalKey.PageUp,
            AvaPhysicalKey.PageDown,
            AvaPhysicalKey.Home,
            AvaPhysicalKey.End,
            AvaPhysicalKey.CapsLock,
            AvaPhysicalKey.ScrollLock,
            AvaPhysicalKey.PrintScreen,
            AvaPhysicalKey.Pause,
            AvaPhysicalKey.NumLock,
            AvaPhysicalKey.NumPadClear,
            AvaPhysicalKey.NumPad0,
            AvaPhysicalKey.NumPad1,
            AvaPhysicalKey.NumPad2,
            AvaPhysicalKey.NumPad3,
            AvaPhysicalKey.NumPad4,
            AvaPhysicalKey.NumPad5,
            AvaPhysicalKey.NumPad6,
            AvaPhysicalKey.NumPad7,
            AvaPhysicalKey.NumPad8,
            AvaPhysicalKey.NumPad9,
            AvaPhysicalKey.NumPadDivide,
            AvaPhysicalKey.NumPadMultiply,
            AvaPhysicalKey.NumPadSubtract,
            AvaPhysicalKey.NumPadAdd,
            AvaPhysicalKey.NumPadDecimal,
            AvaPhysicalKey.NumPadEnter,
            AvaPhysicalKey.A,
            AvaPhysicalKey.B,
            AvaPhysicalKey.C,
            AvaPhysicalKey.D,
            AvaPhysicalKey.E,
            AvaPhysicalKey.F,
            AvaPhysicalKey.G,
            AvaPhysicalKey.H,
            AvaPhysicalKey.I,
            AvaPhysicalKey.J,
            AvaPhysicalKey.K,
            AvaPhysicalKey.L,
            AvaPhysicalKey.M,
            AvaPhysicalKey.N,
            AvaPhysicalKey.O,
            AvaPhysicalKey.P,
            AvaPhysicalKey.Q,
            AvaPhysicalKey.R,
            AvaPhysicalKey.S,
            AvaPhysicalKey.T,
            AvaPhysicalKey.U,
            AvaPhysicalKey.V,
            AvaPhysicalKey.W,
            AvaPhysicalKey.X,
            AvaPhysicalKey.Y,
            AvaPhysicalKey.Z,
            AvaPhysicalKey.Digit0,
            AvaPhysicalKey.Digit1,
            AvaPhysicalKey.Digit2,
            AvaPhysicalKey.Digit3,
            AvaPhysicalKey.Digit4,
            AvaPhysicalKey.Digit5,
            AvaPhysicalKey.Digit6,
            AvaPhysicalKey.Digit7,
            AvaPhysicalKey.Digit8,
            AvaPhysicalKey.Digit9,
            AvaPhysicalKey.Backquote,
            AvaPhysicalKey.IntlBackslash,
            AvaPhysicalKey.Minus,
            AvaPhysicalKey.Equal,
            AvaPhysicalKey.BracketLeft,
            AvaPhysicalKey.BracketRight,
            AvaPhysicalKey.Semicolon,
            AvaPhysicalKey.Quote,
            AvaPhysicalKey.Comma,
            AvaPhysicalKey.Period,
            AvaPhysicalKey.Slash,
            AvaPhysicalKey.Backslash,
            AvaPhysicalKey.None,
        ];

        private static readonly Dictionary<AvaPhysicalKey, PhysicalKey> _reversePhysicalKeyMapping;

        static AvaloniaKeyboardMappingHelper()
        {
            _reversePhysicalKeyMapping = [];

            foreach (PhysicalKey key in Enum.GetValues<PhysicalKey>())
            {
                if (TryGetAvaPhysicalKey(key, out AvaPhysicalKey avaloniaKey))
                {
                    _reversePhysicalKeyMapping[avaloniaKey] = key;
                }
            }

            _reversePhysicalKeyMapping[AvaPhysicalKey.IntlRo] = PhysicalKey.BackSlash;
            _reversePhysicalKeyMapping[AvaPhysicalKey.IntlYen] = PhysicalKey.BackSlash;
        }

        public static bool TryGetAvaPhysicalKey(PhysicalKey key, out AvaPhysicalKey avaloniaKey)
        {
            avaloniaKey = AvaPhysicalKey.None;

            bool keyExists = key < PhysicalKey.Count && (int)key < _physicalKeyMapping.Length;
            if (keyExists)
            {
                avaloniaKey = _physicalKeyMapping[(int)key];
            }

            return keyExists && avaloniaKey != AvaPhysicalKey.None;
        }

        public static PhysicalKey ToPhysicalKey(AvaPhysicalKey key)
        {
            return _reversePhysicalKeyMapping.GetValueOrDefault(key, PhysicalKey.Unknown);
        }
    }
}
