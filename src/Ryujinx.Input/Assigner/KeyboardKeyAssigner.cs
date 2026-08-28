using Ryujinx.Common.Logging;
using PhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;

namespace Ryujinx.Input.Assigner
{
    /// <summary>
    /// <see cref="IButtonAssigner"/> implementation for <see cref="IKeyboard"/>.
    /// </summary>
    public class KeyboardKeyAssigner : IButtonAssigner
    {
        private readonly IKeyboard _keyboard;

        private KeyboardStateSnapshot _keyboardState;
        private Button? _pressedButton;

        public KeyboardKeyAssigner(IKeyboard keyboard)
        {
            _keyboard = keyboard;
        }

        public void Initialize()
        {
            _pressedButton = null;
        }

        public void ReadInput()
        {
            _keyboardState = _keyboard.GetKeyboardStateSnapshot();

            if (_pressedButton is null)
            {
                Button? buttonFromState = GetPressedButtonFromState();
                Button? buttonFromBufferedPress = buttonFromState is null ? GetPressedButtonFromBufferedPress() : null;

                _pressedButton = buttonFromState ?? buttonFromBufferedPress;
            }

            if (_pressedButton is not null)
            {
                string source = _pressedButton.HasValue && GetPressedButtonFromState() is not null ? "state" : "buffered-press";
                Logger.Debug?.Print(LogClass.UI, $"Keyboard assigner registered key={_pressedButton.Value.AsHidType<PhysicalKey>()}, source={source}, cancelPressed={ShouldCancel()}");
            }
        }

        public bool IsAnyButtonPressed()
        {
            return _pressedButton is not null;
        }

        public bool ShouldCancel()
        {
            return _keyboardState.IsPressed(PhysicalKey.Escape);
        }

        public Button? GetPressedButton()
        {
            return !ShouldCancel() ? _pressedButton : null;
        }

        private Button? GetPressedButtonFromState()
        {
            PhysicalKey aliasedKey = GetAliasedPressedKey();

            if (aliasedKey != PhysicalKey.Unknown)
            {
                return new Button(aliasedKey);
            }

            for (PhysicalKey key = PhysicalKey.Unknown; key < PhysicalKey.Count; key++)
            {
                if (_keyboardState.IsPressed(key))
                {
                    return new Button(key);
                }
            }

            return null;
        }

        private Button? GetPressedButtonFromBufferedPress()
        {
            return _keyboard.TryConsumePressedKey(out PhysicalKey key) ? new Button(key) : null;
        }

        private PhysicalKey GetAliasedPressedKey()
        {
            // On some layouts (for example AltGr on Windows), Right Alt is reported as Ctrl+Alt.
            // Prefer AltRight in that case so the binding reflects the physical key used.
            if (_keyboardState.IsPressed(PhysicalKey.ControlLeft) && _keyboardState.IsPressed(PhysicalKey.AltRight))
            {
                return PhysicalKey.AltRight;
            }

            // On some Copilot keyboards, the key in the right-control position is reported as
            // ShiftLeft+Win+F23. Prefer ControlRight so the binding reflects that physical key.
            if (_keyboardState.IsPressed(PhysicalKey.ShiftLeft) &&
                _keyboardState.IsPressed(PhysicalKey.F23) &&
                (_keyboardState.IsPressed(PhysicalKey.WinLeft) || _keyboardState.IsPressed(PhysicalKey.WinRight)))
            {
                return PhysicalKey.ControlRight;
            }

            return PhysicalKey.Unknown;
        }
    }
}
