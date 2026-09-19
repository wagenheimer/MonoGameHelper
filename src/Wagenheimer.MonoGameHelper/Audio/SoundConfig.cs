namespace Wagenheimer.MonoGameHelper.Audio;

/// <summary>
/// Configuration for polyphony, voice limits, and retrigger debounce for a sound effect.
/// </summary>
public class SoundConfig
{
    /// <summary>
    /// Maximum number of simultaneous playing instances allowed for this sound.
    /// Default is 3.
    /// </summary>
    public int MaxVoices { get; set; } = 3;

    /// <summary>
    /// Minimum time interval in seconds between consecutive triggers of this sound.
    /// Prevents audio clipping / burst distortion when multiple events happen simultaneously.
    /// Default is 0.03s (30ms).
    /// </summary>
    public float RetriggerLimitSeconds { get; set; } = 0.03f;

    /// <summary>
    /// The rule applied when the active voice limit is reached.
    /// Default is StealOldest.
    /// </summary>
    public VoiceStealRule StealRule { get; set; } = VoiceStealRule.StealOldest;
}
