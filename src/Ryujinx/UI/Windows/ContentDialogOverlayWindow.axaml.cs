using Avalonia.Controls;
using Avalonia.Media;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class ContentDialogOverlayWindow : StyleableWindow
    {
        public ContentDialogOverlayWindow()
        {
            InitializeComponent();

            WindowBackdrop.Disable(this);
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowDecorations = WindowDecorations.None;
            ExtendClientAreaTitleBarHeightHint = 0;
            Background = Brushes.Transparent;
            CanResize = false;
        }
    }
}
