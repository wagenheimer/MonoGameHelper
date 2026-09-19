using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Wagenheimer.MonoGameHelper.Common;

namespace Wagenheimer.MonoGameHelper.Input;

/// <summary>
/// Manages hybrid cursor rendering:
/// - In Mouse mode: uses native OS hardware cursor (SDL2/DirectX) for zero latency.
/// - In GamePad mode: hides OS cursor and displays a virtual in-game pointer.
/// - In Touch mode: hides all cursors for a clean mobile/tablet experience.
///
/// The custom hardware cursor registered with <see cref="SetCustomHardwareCursor"/> can be turned
/// on/off at runtime through <see cref="UseCustomHardwareCursor"/> — useful for a game setting that
/// lets the player choose between the game's cursor art and the default OS arrow.
/// </summary>
public class HybridCursorManager : IDisposable
{
    private readonly Game _game;
    private MouseCursor? _hardwareCursor;
    private bool _customHardwareCursorEnabled = true;

    /// <summary>
    /// Indicates whether the in-game virtual sprite cursor should be visible.
    /// </summary>
    public bool IsVirtualCursorVisible { get; private set; }

    /// <summary>
    /// Indicates whether the native operating system hardware cursor is visible.
    /// </summary>
    public bool IsHardwareCursorVisible => _game.IsMouseVisible;

    /// <summary>
    /// Gets or sets whether the custom hardware cursor (registered via
    /// <see cref="SetCustomHardwareCursor"/>) is used while in Mouse mode. When <c>false</c>, the
    /// default OS arrow is shown instead. GamePad and Touch modes are unaffected.
    /// <para>Changing this applies immediately when the hardware cursor is currently visible (i.e.
    /// in Mouse mode); in GamePad/Touch mode the new value takes effect on the next switch back to
    /// the mouse.</para>
    /// </summary>
    public bool UseCustomHardwareCursor
    {
        get => _customHardwareCursorEnabled;
        set
        {
            if (_customHardwareCursorEnabled == value)
                return;

            _customHardwareCursorEnabled = value;
            ApplyHardwareCursor();
        }
    }

    public HybridCursorManager(Game game)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
    }

    /// <summary>
    /// Sets a custom texture to be used as the operating system's native hardware cursor.
    /// </summary>
    /// <param name="texture">RGBA Texture2D with cursor art.</param>
    /// <param name="hotspotX">Horizontal click hotspot (usually 0 or arrow tip).</param>
    /// <param name="hotspotY">Vertical click hotspot (usually 0 or arrow tip).</param>
    public void SetCustomHardwareCursor(Texture2D texture, int hotspotX = 0, int hotspotY = 0)
    {
        _hardwareCursor?.Dispose();
        _hardwareCursor = MouseCursor.FromTexture2D(texture, hotspotX, hotspotY);
        ApplyHardwareCursor();
    }

    /// <summary>
    /// Updates cursor visibility based on the active input device.
    /// </summary>
    public void UpdateForDevice(InputDeviceType device)
    {
        switch (device)
        {
            case InputDeviceType.MouseKeyboard:
                IsVirtualCursorVisible = false;
                _game.IsMouseVisible = true;
                ApplyHardwareCursor();
                break;

            case InputDeviceType.GamePad:
                _game.IsMouseVisible = false;
                IsVirtualCursorVisible = true;
                break;

            case InputDeviceType.Touch:
                _game.IsMouseVisible = false;
                IsVirtualCursorVisible = false;
                break;
        }
    }

    /// <summary>
    /// Applies the effective hardware cursor to the OS: the custom texture when enabled and
    /// available, otherwise the default arrow. No-op while the hardware cursor is hidden
    /// (GamePad/Touch modes).
    /// </summary>
    private void ApplyHardwareCursor()
    {
        if (!_game.IsMouseVisible)
            return;

        Mouse.SetCursor(_hardwareCursor != null && _customHardwareCursorEnabled
            ? _hardwareCursor
            : MouseCursor.Arrow);
    }

    public void Dispose()
    {
        _hardwareCursor?.Dispose();
        _hardwareCursor = null;
    }
}
