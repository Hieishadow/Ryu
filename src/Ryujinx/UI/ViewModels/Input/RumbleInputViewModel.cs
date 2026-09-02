using CommunityToolkit.Mvvm.ComponentModel;

namespace Ryujinx.Ava.UI.ViewModels.Input
{
    public partial class RumbleInputViewModel : BaseModel
    {
        public RumbleInputViewModel(ControllerInputViewModel model)
        {
            ControllerModel = model;
        }
        
        [ObservableProperty]
        public partial float StrongRumble { get; set; }

        [ObservableProperty]
        public partial float WeakRumble { get; set; }

        [ObservableProperty]
        public partial bool EnableHDRumble { get; set; }
        
        public ControllerInputViewModel ControllerModel { get; }
    }
}
