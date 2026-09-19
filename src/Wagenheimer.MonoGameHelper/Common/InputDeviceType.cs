namespace Wagenheimer.MonoGameHelper.Common;

/// <summary>
/// Represents the currently active user input device type.
/// </summary>
public enum InputDeviceType
{
    /// <summary>
    /// Traditional PC mouse and keyboard.
    /// </summary>
    MouseKeyboard = 0,

    /// <summary>
    /// Game controller / GamePad (Xbox, PlayStation, etc.).
    /// </summary>
    GamePad = 1,

    /// <summary>
    /// Touchscreen input (mobile, tablet, or touch display).
    /// </summary>
    Touch = 2
}
