using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Ryujinx.Ava.Input;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.HLE.UI;
using System;
using System.Threading;
using AvaKey = Avalonia.Input.Key;
using PhysicalKey = Ryujinx.Common.Configuration.Hid.PhysicalKey;

namespace Ryujinx.Ava.UI.Applet
{
    class AvaloniaDynamicTextInputHandler : IDynamicTextInputHandler
    {
        private MainWindow _parent;
        private AvaloniaKeyboardDriver _avaloniaKeyboardDriver;
        private readonly OffscreenTextBox _hiddenTextBox;
        private bool _canProcessInput;
        private IDisposable _textChangedSubscription;
        private IDisposable _selectionStartChangedSubscription;
        private IDisposable _selectionEndtextChangedSubscription;

        public AvaloniaDynamicTextInputHandler(MainWindow parent)
        {
            _parent = parent;

            if (_parent.InputManager.KeyboardDriver is AvaloniaKeyboardDriver avaloniaKeyboardDriver)
            {
                _avaloniaKeyboardDriver = avaloniaKeyboardDriver;
                avaloniaKeyboardDriver.KeyPressed += AvaloniaDynamicTextInputHandler_KeyPressed;
                avaloniaKeyboardDriver.KeyRelease += AvaloniaDynamicTextInputHandler_KeyRelease;
                avaloniaKeyboardDriver.TextInput += AvaloniaDynamicTextInputHandler_TextInput;
            }

            _hiddenTextBox = _parent.HiddenTextBox;

            Dispatcher.UIThread.Post(() =>
            {
                _textChangedSubscription = _hiddenTextBox.GetObservable(TextBox.TextProperty).Subscribe(TextChanged);
                _selectionStartChangedSubscription = _hiddenTextBox.GetObservable(TextBox.SelectionStartProperty).Subscribe(SelectionChanged);
                _selectionEndtextChangedSubscription = _hiddenTextBox.GetObservable(TextBox.SelectionEndProperty).Subscribe(SelectionChanged);
            });
        }

        private void TextChanged(string text)
        {
            TextChangedEvent?.Invoke(text ?? string.Empty, _hiddenTextBox.SelectionStart, _hiddenTextBox.SelectionEnd, false);
        }

        private void SelectionChanged(int _)
        {
            TextChangedEvent?.Invoke(_hiddenTextBox.Text ?? string.Empty, _hiddenTextBox.SelectionStart, _hiddenTextBox.SelectionEnd, false);
        }

        private void AvaloniaDynamicTextInputHandler_TextInput(object sender, string text)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_canProcessInput)
                {
                    _hiddenTextBox.SendText(text);
                }
            });
        }

        private void AvaloniaDynamicTextInputHandler_KeyRelease(object sender, KeyEventArgs e)
        {
            PhysicalKey key = AvaloniaKeyboardMappingHelper.ToPhysicalKey(e.PhysicalKey);

            if (!(KeyReleasedEvent?.Invoke(key)).GetValueOrDefault(true))
            {
                return;
            }

            KeyEventArgs textBoxEvent = CreateTextBoxKeyEvent(e, false);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_canProcessInput && textBoxEvent != null)
                {
                    _hiddenTextBox.SendKeyUpEvent(textBoxEvent);
                }
            });
        }

        private void AvaloniaDynamicTextInputHandler_KeyPressed(object sender, KeyEventArgs e)
        {
            PhysicalKey key = AvaloniaKeyboardMappingHelper.ToPhysicalKey(e.PhysicalKey);

            if (!(KeyPressedEvent?.Invoke(key)).GetValueOrDefault(true))
            {
                return;
            }

            KeyEventArgs textBoxEvent = CreateTextBoxKeyEvent(e, true);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_canProcessInput && textBoxEvent != null)
                {
                    _hiddenTextBox.SendKeyDownEvent(textBoxEvent);
                }
            });
        }

        private KeyEventArgs CreateTextBoxKeyEvent(KeyEventArgs sourceEvent, bool isPressed)
        {
            PhysicalKey physicalKey = AvaloniaKeyboardMappingHelper.ToPhysicalKey(sourceEvent.PhysicalKey);
            AvaKey key = ToTextBoxKey(physicalKey);

            if (key == AvaKey.None)
            {
                return null;
            }

            return new KeyEventArgs
            {
                Key = key,
                PhysicalKey = sourceEvent.PhysicalKey,
                KeyModifiers = GetPhysicalModifiers(),
                Source = _hiddenTextBox,
                RoutedEvent = isPressed ? OffscreenTextBox.GetKeyDownRoutedEvent() : OffscreenTextBox.GetKeyUpRoutedEvent(),
            };
        }

        private KeyModifiers GetPhysicalModifiers()
        {
            KeyModifiers modifiers = KeyModifiers.None;

            if (_avaloniaKeyboardDriver.IsPressed(PhysicalKey.ShiftLeft) || _avaloniaKeyboardDriver.IsPressed(PhysicalKey.ShiftRight))
            {
                modifiers |= KeyModifiers.Shift;
            }

            if (_avaloniaKeyboardDriver.IsPressed(PhysicalKey.ControlLeft) || _avaloniaKeyboardDriver.IsPressed(PhysicalKey.ControlRight))
            {
                modifiers |= KeyModifiers.Control;
            }

            if (_avaloniaKeyboardDriver.IsPressed(PhysicalKey.AltLeft) || _avaloniaKeyboardDriver.IsPressed(PhysicalKey.AltRight))
            {
                modifiers |= KeyModifiers.Alt;
            }

            if (_avaloniaKeyboardDriver.IsPressed(PhysicalKey.WinLeft) || _avaloniaKeyboardDriver.IsPressed(PhysicalKey.WinRight))
            {
                modifiers |= KeyModifiers.Meta;
            }

            return modifiers;
        }

        private static AvaKey ToTextBoxKey(PhysicalKey key)
        {
            // TextBox requires Avalonia key values for editing commands. Derive them only from
            // physical positions; printable characters arrive separately through TextInput.
            if (key is >= PhysicalKey.A and <= PhysicalKey.Z)
            {
                return (AvaKey)((int)AvaKey.A + (int)(key - PhysicalKey.A));
            }

            if (key is >= PhysicalKey.Number0 and <= PhysicalKey.Number9)
            {
                return (AvaKey)((int)AvaKey.D0 + (int)(key - PhysicalKey.Number0));
            }

            if (key is >= PhysicalKey.F1 and <= PhysicalKey.F24)
            {
                return (AvaKey)((int)AvaKey.F1 + (int)(key - PhysicalKey.F1));
            }

            return key switch
            {
                PhysicalKey.ShiftLeft => AvaKey.LeftShift,
                PhysicalKey.ShiftRight => AvaKey.RightShift,
                PhysicalKey.ControlLeft => AvaKey.LeftCtrl,
                PhysicalKey.ControlRight => AvaKey.RightCtrl,
                PhysicalKey.AltLeft => AvaKey.LeftAlt,
                PhysicalKey.AltRight => AvaKey.RightAlt,
                PhysicalKey.WinLeft => AvaKey.LWin,
                PhysicalKey.WinRight => AvaKey.RWin,
                PhysicalKey.Enter => AvaKey.Return,
                PhysicalKey.Escape => AvaKey.Escape,
                PhysicalKey.Space => AvaKey.Space,
                PhysicalKey.Tab => AvaKey.Tab,
                PhysicalKey.BackSpace => AvaKey.Back,
                PhysicalKey.Insert => AvaKey.Insert,
                PhysicalKey.Delete => AvaKey.Delete,
                PhysicalKey.PageUp => AvaKey.PageUp,
                PhysicalKey.PageDown => AvaKey.PageDown,
                PhysicalKey.Home => AvaKey.Home,
                PhysicalKey.End => AvaKey.End,
                PhysicalKey.Up => AvaKey.Up,
                PhysicalKey.Down => AvaKey.Down,
                PhysicalKey.Left => AvaKey.Left,
                PhysicalKey.Right => AvaKey.Right,
                _ => AvaKey.None,
            };
        }

        public bool TextProcessingEnabled
        {
            get => Volatile.Read(ref _canProcessInput);
            set => Volatile.Write(ref _canProcessInput, value);
        }

        public event DynamicTextChangedHandler TextChangedEvent;
        public event KeyPressedHandler KeyPressedEvent;
        public event KeyReleasedHandler KeyReleasedEvent;

        public void Dispose()
        {
            if (_avaloniaKeyboardDriver != null)
            {
                _avaloniaKeyboardDriver.KeyPressed -= AvaloniaDynamicTextInputHandler_KeyPressed;
                _avaloniaKeyboardDriver.KeyRelease -= AvaloniaDynamicTextInputHandler_KeyRelease;
                _avaloniaKeyboardDriver.TextInput -= AvaloniaDynamicTextInputHandler_TextInput;
            }

            _textChangedSubscription?.Dispose();
            _selectionStartChangedSubscription?.Dispose();
            _selectionEndtextChangedSubscription?.Dispose();

            Dispatcher.UIThread.Post(() =>
            {
                _hiddenTextBox.Clear();
                _parent.ViewModel.RendererHostControl.Focus();

                _parent = null;
            });
        }

        public void SetText(string text, int cursorBegin) =>
            Dispatcher.UIThread.Post(() =>
            {
                _hiddenTextBox.Text = text;
                _hiddenTextBox.CaretIndex = cursorBegin;
            });

        public void SetText(string text, int cursorBegin, int cursorEnd) =>
            Dispatcher.UIThread.Post(() =>
            {
                _hiddenTextBox.Text = text;
                _hiddenTextBox.SelectionStart = cursorBegin;
                _hiddenTextBox.SelectionEnd = cursorEnd;
            });
    }
}
