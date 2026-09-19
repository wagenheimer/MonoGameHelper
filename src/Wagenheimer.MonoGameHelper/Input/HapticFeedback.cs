using System;
using Microsoft.Xna.Framework;

namespace Wagenheimer.MonoGameHelper.Input;

/// <summary>
/// Static cross-platform haptic feedback dispatcher.
/// Dispatches tactile vibration events to the registered <see cref="IHapticFeedbackProvider"/>
/// (e.g. GamePad on desktop/console, or platform native vibrators on mobile).
/// </summary>
public static class HapticFeedback
{
    private static IHapticFeedbackProvider _provider = new GamePadHapticProvider();

    /// <summary>
    /// Master toggle for haptic vibrations.
    /// </summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>
    /// Global intensity multiplier (0.0 to 1.0).
    /// </summary>
    public static float GlobalIntensity { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the active haptic provider implementation.
    /// Allows plugging in Android Vibrator or iOS UIFeedbackGenerator on mobile builds.
    /// </summary>
    public static IHapticFeedbackProvider Provider
    {
        get => _provider;
        set => _provider = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Plays a predefined tactile haptic pattern.
    /// </summary>
    public static void Play(HapticPattern pattern, PlayerIndex player = PlayerIndex.One)
    {
        if (!Enabled) return;
        _provider.Play(pattern, player);
    }

    /// <summary>
    /// Plays custom raw dual-motor vibration.
    /// </summary>
    /// <param name="leftMotor">Low-frequency heavy motor intensity (0.0 to 1.0).</param>
    /// <param name="rightMotor">High-frequency crisp motor intensity (0.0 to 1.0).</param>
    /// <param name="durationSeconds">Duration in seconds.</param>
    /// <param name="player">Controller player index.</param>
    public static void Vibrate(float leftMotor, float rightMotor, float durationSeconds, PlayerIndex player = PlayerIndex.One)
    {
        if (!Enabled) return;
        _provider.Vibrate(leftMotor, rightMotor, durationSeconds, player);
    }

    /// <summary>
    /// Immediately silences all active vibrations.
    /// </summary>
    public static void Stop(PlayerIndex player = PlayerIndex.One)
    {
        _provider.Stop(player);
    }

    /// <summary>
    /// Updates active vibration envelopes. Automatically called by <see cref="InputHelperManager.Update"/>.
    /// </summary>
    public static void Update(float deltaTime)
    {
        _provider.Update(deltaTime);
    }
}
