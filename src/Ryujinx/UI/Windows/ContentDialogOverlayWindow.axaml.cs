using Avalonia.Controls;
using Avalonia.Media;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class ContentDialogOverlayWindow : StyleableWindow
    {
        public ContentDialogOverlayWindow() : base(false)
        {
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
            Background = Brushes.Transparent;

            InitializeComponent();

            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowDecorations = WindowDecorations.None;
            ExtendClientAreaTitleBarHeightHint = 0;
            CanResize = false;
        }
    }
}
