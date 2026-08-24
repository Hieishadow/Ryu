using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using FluentAvalonia.UI.Windowing;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Systems.Configuration;
using Ryujinx.Ava.UI.Controls;
using Microsoft.Win32;
using System;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Windows
{
    public abstract class StyleableAppWindow : FAAppWindow
    {
        public static async Task ShowAsync(StyleableAppWindow appWindow, Window owner = null)
        {
/*
#if DEBUG
        appWindow.AttachDevTools(new KeyGesture(Key.F12, KeyModifiers.Control));
#endif
*/
        await appWindow.ShowDialog(owner ?? RyujinxApp.MainWindow);
    }

        protected StyleableAppWindow(bool useCustomTitleBar = false, double? titleBarHeight = null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowBackdrop.Configure(this);

            LocaleManager.Instance.LocaleChanged += LocaleChanged;
            LocaleChanged();

            if (useCustomTitleBar)
            {
                TitleBar.ExtendsContentIntoTitleBar = !ConfigurationState.Instance.ShowOldUI;

                if (TitleBar.ExtendsContentIntoTitleBar && titleBarHeight != null)
                    TitleBar.Height = titleBarHeight.Value;
            }

            Icon = RyujinxLogo.Bitmap;
        }

        private void LocaleChanged()
        {
            FlowDirection = LocaleManager.Instance.IsRTL() ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        /* protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.SystemChrome | ExtendClientAreaChromeHints.OSXThickTitleBar;
        } */
    }

    public abstract class StyleableWindow : Window
    {
        public static async Task ShowAsync(StyleableWindow window, Window owner = null)
        {
/*
#if DEBUG
            window.AttachDevTools(new KeyGesture(Key.F12, KeyModifiers.Control));
#endif
*/
            await window.ShowDialog(owner ?? RyujinxApp.MainWindow);
        }

        protected StyleableWindow(bool useBackdrop = true)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (useBackdrop)
            {
                WindowBackdrop.Configure(this);
            }

            LocaleManager.Instance.LocaleChanged += LocaleChanged;
            LocaleChanged();

            Icon = new WindowIcon(RyujinxLogo.Bitmap);
        }

        private void LocaleChanged()
        {
            FlowDirection = LocaleManager.Instance.IsRTL() ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        /* protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.SystemChrome | ExtendClientAreaChromeHints.OSXThickTitleBar;
        } */
    }

    static class WindowBackdrop
    {
        public static void Configure(Window window)
        {
            if (!OperatingSystem.IsWindows())
            {
                window.TransparencyLevelHint = [WindowTransparencyLevel.None];
                ApplyBackground(window, true);

                return;
            }

            Update(window, false);

            window.Transitions =
            [
                new BrushTransition
                {
                    Property = Window.BackgroundProperty,
                    Duration = TimeSpan.FromMilliseconds(220),
                },
            ];

            window.Activated += (_, _) => Update(window, false);
            window.Deactivated += (_, _) => Update(window, true);
        }

        private static void Update(Window window, bool inactive)
        {
            bool transparencyEnabled = IsWindowsTransparencyEnabled();

            window.TransparencyLevelHint = transparencyEnabled
                ? [WindowTransparencyLevel.Mica, WindowTransparencyLevel.None]
                : [WindowTransparencyLevel.None];

            ApplyBackground(window, inactive || !transparencyEnabled);
        }

        private static void ApplyBackground(Window window, bool opaque)
        {
            ThemeVariant themeVariant = window.IsVisible
                ? window.ActualThemeVariant
                : Application.Current?.ActualThemeVariant ?? window.ActualThemeVariant;
            bool isDark = themeVariant == ThemeVariant.Dark;

            byte alpha = opaque ? byte.MaxValue : (byte)0x80;
            byte channel = isDark ? (byte)0x20 : (byte)0xF3;

            window.Background = new SolidColorBrush(Color.FromArgb(alpha, channel, channel, channel));
        }

        private static bool IsWindowsTransparencyEnabled()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            const string personalizeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            const string enableTransparencyValue = "EnableTransparency";

            return Registry.GetValue(personalizeKey, enableTransparencyValue, 1) is not int enableTransparency ||
                   enableTransparency != 0;
        }
    }
}
