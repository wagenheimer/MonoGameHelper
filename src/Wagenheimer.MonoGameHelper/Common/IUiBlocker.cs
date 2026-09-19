namespace Wagenheimer.MonoGameHelper.Common;

/// <summary>
/// Provides an abstraction to check whether player input should be blocked
/// during transitions, cutscenes, or loading screens.
/// </summary>
public interface IUiBlocker
{
    /// <summary>
    /// Returns true if player input is temporarily blocked.
    /// </summary>
    bool IsInputBlocked { get; }
}
