namespace Wagenheimer.MonoGameHelper.UI;

/// <summary>
/// Represents a modal dialog, popup, or overlay screen in the UI navigation stack.
/// </summary>
public interface IModalDialog
{
    /// <summary>
    /// Human-readable identifier or title for the modal dialog.
    /// </summary>
    string DialogName { get; }

    /// <summary>
    /// Whether this modal dialog consumes the Escape / Back action to close itself.
    /// </summary>
    bool CanCloseWithEscape { get; }

    /// <summary>
    /// Invoked when the player presses Escape (keyboard) or Button B (GamePad) to close the dialog.
    /// Returns true if the close request was accepted and consumed.
    /// </summary>
    bool OnCloseRequested();
}
