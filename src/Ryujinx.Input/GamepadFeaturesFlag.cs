using System;

namespace Ryujinx.Input
{
    /// <summary>
    /// Represent features supported by a <see cref="IGamepad"/>.
    /// </summary>
    [Flags]
    public enum GamepadFeaturesFlag
    {
        /// <summary>
        /// No features are supported
        /// </summary>
        None = 0,

        /// <summary>
        /// Rumble
        /// </summary>
        /// <remarks>Also named haptic</remarks>
        Rumble = 1,
        
        /// <summary>
        /// HD Rumble
        /// </summary>
        /// <remarks>Also named haptic</remarks>
        HdRumble = 2,

        /// <summary>
        /// Motion
        /// <remarks>Also named sixaxis</remarks>
        /// </summary>
        Motion = 4,

        /// <summary>
        ///     The LED on the back of modern PlayStation controllers (DualSense &amp; DualShock 4).
        /// </summary>
        Led = 8,
    }
}
