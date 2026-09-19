namespace Wagenheimer.MonoGameHelper.Audio;

/// <summary>
/// Rule applied when the maximum number of simultaneous voices for a sound effect is reached.
/// </summary>
public enum VoiceStealRule
{
    /// <summary>
    /// Stops the oldest currently playing instance and replaces it with the new sound.
    /// </summary>
    StealOldest,

    /// <summary>
    /// Drops the new sound request and lets the currently playing instances finish.
    /// </summary>
    IgnoreNewest
}
