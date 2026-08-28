using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Systems.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Input;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using PhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;

namespace Ryujinx.Ava.Input
{
    internal class AvaloniaKeyboardDriver : IGamepadDriver
    {
        [Flags]
        private enum CGEventFlags : ulong
        {
            AlphaShift = 1UL << 16,
        }

        private enum CGEventSourceStateID : uint
        {
            HIDSystemState = 1,
        }

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern CGEventFlags CGEventSourceFlagsState(CGEventSourceStateID stateID);

        private static readonly string[] _keyboardIdentifers = ["0"];
        private readonly Control _control;
        private readonly Window _window;
        private readonly HashSet<PhysicalKey> _pressedKeys;
        private readonly Queue<PhysicalKey> _pressedKeyQueue;
        private readonly Lock _keyStateLock;

        public event EventHandler<KeyEventArgs> KeyPressed;
        public event EventHandler<KeyEventArgs> KeyRelease;
        public event EventHandler<string> TextInput;

        public string DriverName => "AvaloniaKeyboardDriver";
        public ReadOnlySpan<string> GamepadsIds => _keyboardIdentifers;

        public AvaloniaKeyboardDriver(Control control)
        {
            _control = control;
            _window = control as Window ?? TopLevel.GetTopLevel(control) as Window;
            _pressedKeys = [];
            _pressedKeyQueue = [];
            _keyStateLock = new();

            _control.AddHandler(InputElement.KeyDownEvent, OnKeyPress, RoutingStrategies.Tunnel, true);
            _control.AddHandler(InputElement.KeyUpEvent, OnKeyRelease, RoutingStrategies.Tunnel, true);
            _control.TextInput += Control_TextInput;
            _window?.Deactivated += Window_Deactivated;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Clear();
        }

        private void Control_TextInput(object sender, TextInputEventArgs e)
        {
            TextInput?.Invoke(this, e.Text);
        }

        public event Action<string> OnGamepadConnected
        {
            add { }
            remove { }
        }

        public event Action<string> OnGamepadDisconnected
        {
            add { }
            remove { }
        }

        public IGamepad GetGamepad(string id)
        {
            if (!_keyboardIdentifers[0].Equals(id))
            {
                return null;
            }

            return new AvaloniaKeyboard(this, _keyboardIdentifers[0], LocaleManager.Instance[LocaleKeys.KeyboardLayout_KeyboardInputMode]);
        }

        public IEnumerable<IGamepad> GetGamepads() => [GetGamepad("0")];

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _control.RemoveHandler(InputElement.KeyDownEvent, OnKeyPress);
                _control.RemoveHandler(InputElement.KeyUpEvent, OnKeyRelease);
                _control.TextInput -= Control_TextInput;

                if (_window != null)
                {
                    _window.Deactivated -= Window_Deactivated;
                }

                Clear();
            }
        }

        protected void OnKeyPress(object sender, KeyEventArgs args)
        {
            UpdateKeyState(args, true);
            KeyPressed?.Invoke(this, args);
        }

        protected void OnKeyRelease(object sender, KeyEventArgs args)
        {
            UpdateKeyState(args, false);
            KeyRelease?.Invoke(this, args);
        }

        internal bool IsPressed(PhysicalKey key)
        {
            if (key is PhysicalKey.Unbound or PhysicalKey.Unknown)
            {
                return false;
            }

            if (key == PhysicalKey.CapsLock && OperatingSystem.IsMacOS())
            {
                return IsCapsLockOnMacOS();
            }

            lock (_keyStateLock)
            {
                return _pressedKeys.Contains(key);
            }
        }

        private static bool IsCapsLockOnMacOS()
        {
            try
            {
                CGEventFlags flags = CGEventSourceFlagsState(CGEventSourceStateID.HIDSystemState);
                return (flags & CGEventFlags.AlphaShift) != 0;
            }
            catch (Exception ex)
            {
                Logger.Debug?.Print(LogClass.UI, $"Failed to query CapsLock state: {ex}");
                return false;
            }
        }

        public void Clear()
        {
            lock (_keyStateLock)
            {
                _pressedKeys.Clear();
                _pressedKeyQueue.Clear();
            }
        }

        internal bool TryConsumePressedKey(out PhysicalKey key)
        {
            lock (_keyStateLock)
            {
                if (_pressedKeyQueue.TryDequeue(out key))
                {
                    return true;
                }
            }

            key = PhysicalKey.Unknown;
            return false;
        }

        private void UpdateKeyState(KeyEventArgs args, bool isPressed)
        {
            PhysicalKey key = AvaloniaKeyboardMappingHelper.ToPhysicalKey(args.PhysicalKey);
            bool stateChanged = false;
            bool bufferedPress = false;

            if (key is not PhysicalKey.Unknown and not PhysicalKey.Unbound)
            {
                lock (_keyStateLock)
                {
                    bool wasPressed = _pressedKeys.Contains(key);
                    stateChanged = wasPressed != isPressed;

                    if (isPressed)
                    {
                        _pressedKeys.Add(key);

                        if (!wasPressed)
                        {
                            _pressedKeyQueue.Enqueue(key);
                            bufferedPress = true;
                        }
                    }
                    else
                    {
                        _pressedKeys.Remove(key);
                    }
                }
            }

            if (ConfigurationState.Instance.Logger.EnableAvaloniaLog && stateChanged)
            {
                Logger.Info?.Print(
                    LogClass.UI,
                    $"Keyboard {(isPressed ? "down" : "up")}: avaloniaPhysical={args.PhysicalKey}, keySymbol={FormatKeySymbol(args.KeySymbol)}, modifiers={args.KeyModifiers}, physical={key}, buffered={bufferedPress}");
            }
        }

        private static string FormatKeySymbol(string keySymbol)
        {
            return string.IsNullOrEmpty(keySymbol) ? "<none>" : keySymbol;
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
