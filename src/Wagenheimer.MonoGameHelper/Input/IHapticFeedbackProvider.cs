using Microsoft.Xna.Framework;

namespace Wagenheimer.MonoGameHelper.Input;

/// <summary>
/// Abstraction interface for hardware haptic and vibration output.
/// Implementations can drive desktop GamePads, Android Vibrators, or iOS UIFeedbackGenerators.
/// </summary>
public interface IHapticFeedbackProvider
{
    /// <summary>
    /// Plays a predefined pattern with calibrated amplitudes and durations.
    /// </summary>
    void Play(HapticPattern pattern, PlayerIndex player = PlayerIndex.One);

    /// <summary>
    /// Plays raw dual-motor vibration.
    /// </summary>
    /// <param name="leftMotor">Low-frequency heavy motor intensity (0.0 to 1.0).</param>
    /// <param name="rightMotor">High-frequency crisp motor intensity (0.0 to 1.0).</param>
    /// <param name="durationSeconds">Duration in seconds.</param>
    /// <param name="player">Controller player index.</param>
    void Vibrate(float leftMotor, float rightMotor, float durationSeconds, PlayerIndex player = PlayerIndex.One);

    /// <summary>
    /// Immediately stops all active vibrations.
    /// </summary>
    void Stop(PlayerIndex player = PlayerIndex.One);

    /// <summary>
    /// Updates ongoing vibration timers and decay.
    /// </summary>
    void Update(float deltaTime);
}
