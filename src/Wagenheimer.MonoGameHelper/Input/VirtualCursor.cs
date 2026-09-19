using System;
using Microsoft.Xna.Framework;

namespace Wagenheimer.MonoGameHelper.Input;

/// <summary>
/// Simulates physics and input behavior for a virtual cursor controlled by an analog stick or D-Pad.
/// </summary>
public class VirtualCursor
{
    public Vector2 Position { get; set; }

    /// <summary>
    /// Base movement speed in pixels per second.
    /// </summary>
    public float BaseSpeed { get; set; } = 800f;

    /// <summary>
    /// Maximum speed allowed when accelerating continuously.
    /// </summary>
    public float MaxSpeed { get; set; } = 1600f;

    /// <summary>
    /// Acceleration rate per second while holding the stick.
    /// </summary>
    public float AccelerationRate { get; set; } = 1200f;

    /// <summary>
    /// Deadzone threshold to ignore minor analog stick jitter.
    /// </summary>
    public float DeadZone { get; set; } = 0.15f;

    /// <summary>
    /// Screen or virtual resolution width and height to clamp cursor bounds.
    /// </summary>
    public int ScreenWidth { get; set; } = 1430;
    public int ScreenHeight { get; set; } = 768;

    /// <summary>
    /// State of simulated left mouse click (GamePad action button, e.g. A).
    /// </summary>
    public bool IsLeftDown { get; private set; }
    public bool IsLeftJustPressed { get; private set; }
    public bool IsLeftJustReleased { get; private set; }

    private float _currentSpeed;
    private bool _wasLeftDown;

    public VirtualCursor(int screenWidth = 1430, int screenHeight = 768)
    {
        ScreenWidth = screenWidth;
        ScreenHeight = screenHeight;
        Position = new Vector2(screenWidth / 2f, screenHeight / 2f);
        _currentSpeed = BaseSpeed;
    }

    /// <summary>
    /// Updates the virtual cursor position and action state based on stick input and button states.
    /// </summary>
    /// <param name="stick">Analog stick vector (X from -1 to 1, Y from -1 to 1 where positive is up).</param>
    /// <param name="actionButtonDown">True if the click action button (e.g. A) is pressed.</param>
    /// <param name="deltaTime">Elapsed frame time in seconds.</param>
    public void Update(Vector2 stick, bool actionButtonDown, float deltaTime)
    {
        // 1. Click state tracking
        IsLeftJustPressed = actionButtonDown && !_wasLeftDown;
        IsLeftJustReleased = !actionButtonDown && _wasLeftDown;
        IsLeftDown = actionButtonDown;
        _wasLeftDown = actionButtonDown;

        // 2. Analog stick movement
        float magnitude = stick.Length();
        if (magnitude < DeadZone)
        {
            _currentSpeed = BaseSpeed;
            return;
        }

        // Progressive acceleration while pushing the stick
        _currentSpeed = Math.Min(MaxSpeed, _currentSpeed + AccelerationRate * deltaTime);

        // In MonoGame, thumbstick.Y is positive UP.
        // In screen space, Y grows downwards, so we invert Y.
        var direction = new Vector2(stick.X, -stick.Y);
        direction.Normalize();

        // Scale distance by normalized intensity beyond the deadzone
        float normalizedIntensity = (magnitude - DeadZone) / (1f - DeadZone);
        float moveDistance = _currentSpeed * normalizedIntensity * deltaTime;

        float newX = Math.Clamp(Position.X + direction.X * moveDistance, 0, ScreenWidth);
        float newY = Math.Clamp(Position.Y + direction.Y * moveDistance, 0, ScreenHeight);

        Position = new Vector2(newX, newY);
    }

    /// <summary>
    /// Sets the virtual cursor position directly (e.g. when syncing from physical mouse position).
    /// </summary>
    public void SetPosition(Vector2 position)
    {
        Position = new Vector2(
            Math.Clamp(position.X, 0, ScreenWidth),
            Math.Clamp(position.Y, 0, ScreenHeight)
        );
        _currentSpeed = BaseSpeed;
    }
}
