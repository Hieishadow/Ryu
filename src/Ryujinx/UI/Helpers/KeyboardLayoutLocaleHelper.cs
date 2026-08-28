using Ryujinx.Ava.Common.Locale;
using System;
using System.Collections.Generic;
using ConfigPhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;
using InputKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;

namespace Ryujinx.Ava.UI.Helpers
{
    internal static class KeyboardLayoutLocaleHelper
    {
        private static readonly Dictionary<InputKey, LocaleKeys> _sharedLocalizedKeysMap = new()
        {
            [InputKey.Unknown] = LocaleKeys.KeyboardLayout_KeyUnknown,
            [InputKey.ShiftLeft] = LocaleKeys.KeyboardLayout_KeyShiftLeft,
            [InputKey.ShiftRight] = LocaleKeys.KeyboardLayout_KeyShiftRight,
            [InputKey.ControlLeft] = LocaleKeys.KeyboardLayout_KeyControlLeft,
            [InputKey.ControlRight] = LocaleKeys.KeyboardLayout_KeyControlRight,
            [InputKey.AltLeft] = LocaleKeys.KeyboardLayout_KeyAltLeft,
            [InputKey.AltRight] = LocaleKeys.KeyboardLayout_KeyAltRight,
            [InputKey.WinLeft] = LocaleKeys.KeyboardLayout_KeyWinLeft,
            [InputKey.WinRight] = LocaleKeys.KeyboardLayout_KeyWinRight,
            [InputKey.Up] = LocaleKeys.KeyboardLayout_KeyUp,
            [InputKey.Down] = LocaleKeys.KeyboardLayout_KeyDown,
            [InputKey.Left] = LocaleKeys.KeyboardLayout_KeyLeft,
            [InputKey.Right] = LocaleKeys.KeyboardLayout_KeyRight,
            [InputKey.Enter] = LocaleKeys.KeyboardLayout_KeyEnter,
            [InputKey.Escape] = LocaleKeys.KeyboardLayout_KeyEscape,
            [InputKey.Space] = LocaleKeys.KeyboardLayout_KeySpace,
            [InputKey.Tab] = LocaleKeys.KeyboardLayout_KeyTab,
            [InputKey.BackSpace] = LocaleKeys.KeyboardLayout_KeyBackSpace,
            [InputKey.Insert] = LocaleKeys.KeyboardLayout_KeyInsert,
            [InputKey.Delete] = LocaleKeys.KeyboardLayout_KeyDelete,
            [InputKey.PageUp] = LocaleKeys.KeyboardLayout_KeyPageUp,
            [InputKey.PageDown] = LocaleKeys.KeyboardLayout_KeyPageDown,
            [InputKey.Home] = LocaleKeys.KeyboardLayout_KeyHome,
            [InputKey.End] = LocaleKeys.KeyboardLayout_KeyEnd,
            [InputKey.CapsLock] = LocaleKeys.KeyboardLayout_KeyCapsLock,
            [InputKey.ScrollLock] = LocaleKeys.KeyboardLayout_KeyScrollLock,
            [InputKey.PrintScreen] = LocaleKeys.KeyboardLayout_KeyPrintScreen,
            [InputKey.Pause] = LocaleKeys.KeyboardLayout_KeyPause,
            [InputKey.NumLock] = LocaleKeys.KeyboardLayout_KeyNumLock,
            [InputKey.Clear] = LocaleKeys.KeyboardLayout_KeyClear,
            [InputKey.Keypad0] = LocaleKeys.KeyboardLayout_KeyKeypad0,
            [InputKey.Keypad1] = LocaleKeys.KeyboardLayout_KeyKeypad1,
            [InputKey.Keypad2] = LocaleKeys.KeyboardLayout_KeyKeypad2,
            [InputKey.Keypad3] = LocaleKeys.KeyboardLayout_KeyKeypad3,
            [InputKey.Keypad4] = LocaleKeys.KeyboardLayout_KeyKeypad4,
            [InputKey.Keypad5] = LocaleKeys.KeyboardLayout_KeyKeypad5,
            [InputKey.Keypad6] = LocaleKeys.KeyboardLayout_KeyKeypad6,
            [InputKey.Keypad7] = LocaleKeys.KeyboardLayout_KeyKeypad7,
            [InputKey.Keypad8] = LocaleKeys.KeyboardLayout_KeyKeypad8,
            [InputKey.Keypad9] = LocaleKeys.KeyboardLayout_KeyKeypad9,
            [InputKey.KeypadDivide] = LocaleKeys.KeyboardLayout_KeyKeypadDivide,
            [InputKey.KeypadMultiply] = LocaleKeys.KeyboardLayout_KeyKeypadMultiply,
            [InputKey.KeypadSubtract] = LocaleKeys.KeyboardLayout_KeyKeypadSubtract,
            [InputKey.KeypadAdd] = LocaleKeys.KeyboardLayout_KeyKeypadAdd,
            [InputKey.KeypadDecimal] = LocaleKeys.KeyboardLayout_KeyKeypadDecimal,
            [InputKey.KeypadEnter] = LocaleKeys.KeyboardLayout_KeyKeypadEnter,
            [InputKey.Unbound] = LocaleKeys.KeyboardLayout_KeyUnbound,
        };

        public static bool TryGetPhysicalLabel(ConfigPhysicalKey key, out string label)
        {
            if (TryGetPhysicalLocaleKey(key, out LocaleKeys localeKey))
            {
                label = GetLocalizedString(localeKey);
                return true;
            }

            label = string.Empty;
            return false;
        }

        public static bool TryGetPhysicalLocaleKey(ConfigPhysicalKey key, out LocaleKeys localeKey)
        {
            return _sharedLocalizedKeysMap.TryGetValue(key, out localeKey);
        }

        private static string GetLocalizedString(LocaleKeys localeKey)
        {
            if (OperatingSystem.IsMacOS())
            {
                localeKey = localeKey switch
                {
                    LocaleKeys.KeyboardLayout_KeyControlLeft => LocaleKeys.KeyboardLayout_KeyMacControlLeft,
                    LocaleKeys.KeyboardLayout_KeyControlRight => LocaleKeys.KeyboardLayout_KeyMacControlRight,
                    LocaleKeys.KeyboardLayout_KeyAltLeft => LocaleKeys.KeyboardLayout_KeyMacAltLeft,
                    LocaleKeys.KeyboardLayout_KeyAltRight => LocaleKeys.KeyboardLayout_KeyMacAltRight,
                    LocaleKeys.KeyboardLayout_KeyWinLeft => LocaleKeys.KeyboardLayout_KeyMacWinLeft,
                    LocaleKeys.KeyboardLayout_KeyWinRight => LocaleKeys.KeyboardLayout_KeyMacWinRight,
                    _ => localeKey
                };
            }

            return LocaleManager.Instance[localeKey];
        }
    }
}
