using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Wagenheimer.MonoGameHelper.Common;

namespace Wagenheimer.MonoGameHelper.Input;

/// <summary>
/// Central input management service for Wagenheimer.MonoGameHelper.
/// Provides automatic tri-input detection (Mouse, GamePad, and Touch), hybrid cursor coordination,
/// and unified pointer positioning.
/// </summary>
public class InputHelperManager
{
    private static InputHelperManager? _instance;
    public static InputHelperManager Instance => _instance ?? throw new InvalidOperationException("InputHelperManager has not been initialized. Call InputHelperManager.Initialize() first.");

    public InputDeviceType ActiveDevice { get; private set; } = InputDeviceType.MouseKeyboard;
    public VirtualCursor VirtualCursor { get; }
    public HybridCursorManager CursorManager { get; }

    /// <summary>
    /// Event triggered whenever the active input device changes.
    /// </summary>
    public event Action<InputDeviceType>? OnActiveDeviceChanged;

    private MouseState _previousMouseState;
    private GamePadState _previousGamePadState;
    private Point _lastKnownMousePosition;

    public InputHelperManager(Game game, int virtualWidth = 1430, int virtualHeight = 768)
    {
        VirtualCursor = new VirtualCursor(virtualWidth, virtualHeight);
        CursorManager = new HybridCursorManager(game);
        _previousMouseState = Mouse.GetState();
        _lastKnownMousePosition = _previousMouseState.Position;
        _instance = this;
    }

    public static InputHelperManager Initialize(Game game, int virtualWidth = 1430, int virtualHeight = 768)
    {
        return new InputHelperManager(game, virtualWidth, virtualHeight);
    }

    /// <summary>
    /// Updates input device detection and cursor states. Should be called at the beginning of the game's Update loop.
    /// </summary>
    /// <param name="gameTime">Snapshot of game timing values.</param>
    /// <param name="scaleToVirtual">Optional function to transform physical window pixel coordinates to game virtual resolution.</param>
    public void Update(GameTime gameTime, Func<Point, Vector2>? scaleToVirtual = null)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        var currentMouseState = Mouse.GetState();
        var currentGamePadState = GamePad.GetState(PlayerIndex.One);
        var currentTouches = TouchPanel.GetState();

        // 1. TOUCH DETECTION
        bool hasActiveTouch = false;
        Vector2 firstTouchPos = Vector2.Zero;
        foreach (var touch in currentTouches)
        {
            if (touch.State is TouchLocationState.Pressed or TouchLocationState.Moved)
            {
                hasActiveTouch = true;
                firstTouchPos = touch.Position;
                break;
            }
        }

        if (hasActiveTouch)
        {
            SwitchDevice(InputDeviceType.Touch);
            if (scaleToVirtual != null)
            {
                VirtualCursor.SetPosition(scaleToVirtual(new Point((int)firstTouchPos.X, (int)firstTouchPos.Y)));
            }
            else
            {
                VirtualCursor.SetPosition(firstTouchPos);
            }
        }
        else
        {
            // 2. GAMEPAD DETECTION
            bool gamePadActive = false;
            if (currentGamePadState.IsConnected)
            {
                float stickMag = currentGamePadState.ThumbSticks.Left.Length();
                if (stickMag >= VirtualCursor.DeadZone ||
                    currentGamePadState.ThumbSticks.Right.Length() >= VirtualCursor.DeadZone ||
                    HasAnyButtonPressed(currentGamePadState))
                {
                    gamePadActive = true;
                }
            }

            // 3. MOUSE DETECTION
            int dx = currentMouseState.X - _lastKnownMousePosition.X;
            int dy = currentMouseState.Y - _lastKnownMousePosition.Y;
            bool mouseMoved = (dx * dx + dy * dy) > 4;
            bool mouseClicked = currentMouseState.LeftButton == ButtonState.Pressed ||
                                currentMouseState.RightButton == ButtonState.Pressed;

            if (mouseMoved || mouseClicked)
            {
                SwitchDevice(InputDeviceType.MouseKeyboard);
                _lastKnownMousePosition = currentMouseState.Position;

                Vector2 virtualPos = scaleToVirtual != null
                    ? scaleToVirtual(currentMouseState.Position)
                    : new Vector2(currentMouseState.X, currentMouseState.Y);

                VirtualCursor.SetPosition(virtualPos);
            }
            else if (gamePadActive)
            {
                SwitchDevice(InputDeviceType.GamePad);
            }
        }

        // 4. UPDATE VIRTUAL CURSOR IF IN GAMEPAD MODE
        if (ActiveDevice == InputDeviceType.GamePad && currentGamePadState.IsConnected)
        {
            bool aDown = currentGamePadState.Buttons.A == ButtonState.Pressed;
            VirtualCursor.Update(currentGamePadState.ThumbSticks.Left, aDown, dt);
        }

        // 5. UPDATE HYBRID CURSOR VISIBILITY
        CursorManager.UpdateForDevice(ActiveDevice);

        // 6. UPDATE HAPTIC VIBRATION ENVELOPES
        HapticFeedback.Update(dt);

        _previousMouseState = currentMouseState;
        _previousGamePadState = currentGamePadState;
    }

    private void SwitchDevice(InputDeviceType newDevice)
    {
        if (ActiveDevice == newDevice) return;

        ActiveDevice = newDevice;
        OnActiveDeviceChanged?.Invoke(newDevice);
    }

    private static bool HasAnyButtonPressed(GamePadState state)
    {
        return state.Buttons.A == ButtonState.Pressed ||
               state.Buttons.B == ButtonState.Pressed ||
               state.Buttons.X == ButtonState.Pressed ||
               state.Buttons.Y == ButtonState.Pressed ||
               state.Buttons.Start == ButtonState.Pressed ||
               state.Buttons.Back == ButtonState.Pressed ||
               state.Buttons.LeftShoulder == ButtonState.Pressed ||
               state.Buttons.RightShoulder == ButtonState.Pressed ||
               state.DPad.Up == ButtonState.Pressed ||
               state.DPad.Down == ButtonState.Pressed ||
               state.DPad.Left == ButtonState.Pressed ||
               state.DPad.Right == ButtonState.Pressed;
    }
}
