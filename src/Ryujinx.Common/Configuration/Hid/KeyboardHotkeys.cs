namespace Ryujinx.Common.Configuration.Hid
{
    public class KeyboardHotkeys
    {
        public PhysicalKey ToggleVSyncMode { get; set; }
        public PhysicalKey Screenshot { get; set; }
        public PhysicalKey ShowUI { get; set; }
        public PhysicalKey Pause { get; set; }
        public PhysicalKey ToggleMute { get; set; }
        public PhysicalKey ResScaleUp { get; set; }
        public PhysicalKey ResScaleDown { get; set; }
        public PhysicalKey VolumeUp { get; set; }
        public PhysicalKey VolumeDown { get; set; }
        public PhysicalKey CustomVSyncIntervalIncrement { get; set; }
        public PhysicalKey CustomVSyncIntervalDecrement { get; set; }
        public PhysicalKey TurboMode { get; set; }
        public bool TurboModeWhileHeld { get; set; }
    }
}
