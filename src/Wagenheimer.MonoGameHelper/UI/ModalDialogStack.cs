using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace Wagenheimer.MonoGameHelper.UI;

/// <summary>
/// Manages a stack of active modal screens with automatic back-navigation handling (Escape / GamePad B).
/// </summary>
public class ModalDialogStack
{
    private readonly List<IModalDialog> _stack = new();
    private KeyboardState _previousKeyboardState;
    private GamePadState _previousGamePadState;

    public int Count => _stack.Count;
    public bool HasActiveModal => _stack.Count > 0;
    public IModalDialog? ActiveModal => _stack.Count > 0 ? _stack[^1] : null;

    /// <summary>
    /// Event triggered whenever a dialog is pushed or removed from the stack.
    /// </summary>
    public event Action? OnStackChanged;

    public void Push(IModalDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        _stack.Add(dialog);
        OnStackChanged?.Invoke();
    }

    public IModalDialog? Pop()
    {
        if (_stack.Count == 0) return null;

        var top = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        OnStackChanged?.Invoke();
        return top;
    }

    public bool Remove(IModalDialog dialog)
    {
        bool removed = _stack.Remove(dialog);
        if (removed) OnStackChanged?.Invoke();
        return removed;
    }

    public void Clear()
    {
        if (_stack.Count > 0)
        {
            _stack.Clear();
            OnStackChanged?.Invoke();
        }
    }

    /// <summary>
    /// Attempts to close the top dialog on the stack. Returns true if a modal was handled and closed.
    /// </summary>
    public bool TryHandleEscape()
    {
        if (_stack.Count == 0) return false;

        var top = _stack[^1];
        if (top.CanCloseWithEscape && top.OnCloseRequested())
        {
            _stack.Remove(top);
            OnStackChanged?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Monitors back/cancel input keys (Escape and GamePad B button) and closes the active modal if applicable.
    /// </summary>
    public void Update(KeyboardState keyboardState, GamePadState gamePadState)
    {
        bool escapeJustPressed = keyboardState.IsKeyDown(Keys.Escape) && !_previousKeyboardState.IsKeyDown(Keys.Escape);
        bool bJustPressed = gamePadState.IsConnected &&
                            gamePadState.Buttons.B == ButtonState.Pressed &&
                            _previousGamePadState.Buttons.B == ButtonState.Released;

        if (escapeJustPressed || bJustPressed)
        {
            TryHandleEscape();
        }

        _previousKeyboardState = keyboardState;
        _previousGamePadState = gamePadState;
    }
}
