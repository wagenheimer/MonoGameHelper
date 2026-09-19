using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Audio;

namespace Wagenheimer.MonoGameHelper.Audio;

/// <summary>
/// MasterAudio-inspired central audio controller providing clean sound playback,
/// volume bus management, voice limiting / polyphony control, retrigger debouncing,
/// and smooth background music crossfading.
/// </summary>
public static class AudioManager
{
    private static float _masterVolume = 1f;
    private static float _musicVolume = 0.8f;
    private static float _sfxVolume = 1f;
    private static bool _isMuted = false;

    public static int DefaultMaxVoicesPerSound { get; set; } = 3;
    public static float DefaultRetriggerLimitSeconds { get; set; } = 0.03f;
    public static VoiceStealRule DefaultVoiceStealRule { get; set; } = VoiceStealRule.StealOldest;

    private static readonly Dictionary<string, SoundEffect> _sounds = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _soundPaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _musicPaths = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, SoundConfig> _soundConfigs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<SoundEffectInstance>> _activeVoices = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, double> _lastPlayTimes = new(StringComparer.OrdinalIgnoreCase);

    private static double _totalAudioTime = 0;

    private static SoundEffectInstance? _currentMusicInstance;
    private static SoundEffect? _currentMusicEffect;
    private static string _currentMusicName = string.Empty;

    private static SoundEffectInstance? _fadingOutMusicInstance;
    private static SoundEffect? _fadingOutMusicEffect;
    private static float _currentMusicFadeTimer;
    private static float _currentMusicFadeDuration;
    private static float _currentMusicTargetVolume;

    private static float _fadeOutMusicTimer;
    private static float _fadeOutMusicDuration;
    private static float _fadeOutMusicStartVolume;

    public static float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Clamp(value, 0f, 1f);
            ApplyMusicVolume();
        }
    }

    public static float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Math.Clamp(value, 0f, 1f);
            ApplyMusicVolume();
        }
    }

    public static float SfxVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = Math.Clamp(value, 0f, 1f);
    }

    public static bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            ApplyMusicVolume();
        }
    }

    public static string CurrentMusicName => _currentMusicName;
    public static bool IsMusicPlaying => _currentMusicInstance?.State == SoundState.Playing;

    /// <summary>
    /// Configures polyphony, max simultaneous voices, and retrigger debounce for a specific sound effect.
    /// </summary>
    public static void ConfigureSound(
        string soundName,
        int maxVoices,
        float retriggerLimitSeconds = 0.03f,
        VoiceStealRule stealRule = VoiceStealRule.StealOldest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(soundName);

        _soundConfigs[soundName] = new SoundConfig
        {
            MaxVoices = Math.Max(1, maxVoices),
            RetriggerLimitSeconds = Math.Max(0f, retriggerLimitSeconds),
            StealRule = stealRule
        };
    }

    /// <summary>
    /// Registers a sound effect by name directly from an audio file (.wav, .mp3, or .ogg).
    /// </summary>
    public static void RegisterSound(string soundName, string filePath, bool preload = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(soundName);
        _soundPaths[soundName] = filePath;

        if (preload && File.Exists(filePath))
        {
            try
            {
                var sfx = AudioLoader.LoadSoundEffect(filePath);
                _sounds[soundName] = sfx;
            }
            catch
            {
                // Defer loading on demand if immediate loading fails
            }
        }
    }

    /// <summary>
    /// Registers a sound effect instance directly.
    /// </summary>
    public static void RegisterSound(string soundName, SoundEffect soundEffect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(soundName);
        _sounds[soundName] = soundEffect ?? throw new ArgumentNullException(nameof(soundEffect));
    }

    /// <summary>
    /// Registers a sound effects directory, discovering all .wav, .mp3, and .ogg files.
    /// </summary>
    public static void RegisterSoundDirectory(string directoryPath, bool preload = false)
    {
        if (!Directory.Exists(directoryPath)) return;

        foreach (var file in Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is not (".mp3" or ".ogg" or ".wav")) continue;

            string soundName = Path.GetFileNameWithoutExtension(file);
            RegisterSound(soundName, file, preload);
        }
    }

    /// <summary>
    /// Registers a music track path for on-demand streaming or playback.
    /// </summary>
    public static void RegisterMusic(string musicName, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(musicName);
        _musicPaths[musicName] = filePath;
    }

    /// <summary>
    /// Registers a music directory, discovering all .wav, .mp3, and .ogg tracks for on-demand playback.
    /// </summary>
    public static void RegisterMusicDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        foreach (var file in Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is not (".mp3" or ".ogg" or ".wav")) continue;

            string trackName = Path.GetFileNameWithoutExtension(file);
            RegisterMusic(trackName, file);
        }
    }

    /// <summary>
    /// Legacy alias for directory registration.
    /// </summary>
    public static void RegisterDirectory(string directoryPath, bool isMusic = false)
    {
        if (isMusic)
            RegisterMusicDirectory(directoryPath);
        else
            RegisterSoundDirectory(directoryPath);
    }

    /// <summary>
    /// Legacy alias for RegisterDirectory.
    /// </summary>
    public static void RegisterAudioDirectory(string directoryPath, bool isMusic = false) =>
        RegisterDirectory(directoryPath, isMusic);

    /// <summary>
    /// Plays a registered sound effect with MasterAudio-style voice limiting, pooling, and retrigger throttling.
    /// </summary>
    public static SoundEffectInstance? PlaySound(string soundName, float volume = 1f, float pitch = 0f, float pan = 0f)
    {
        if (_isMuted) return null;

        // 1. Get sound configuration (or fallback to defaults)
        if (!_soundConfigs.TryGetValue(soundName, out var config))
        {
            config = new SoundConfig
            {
                MaxVoices = DefaultMaxVoicesPerSound,
                RetriggerLimitSeconds = DefaultRetriggerLimitSeconds,
                StealRule = DefaultVoiceStealRule
            };
        }

        // 2. Retrigger debouncing check (prevents audio burst / ear-clipping)
        if (_lastPlayTimes.TryGetValue(soundName, out double lastPlayTime))
        {
            if ((_totalAudioTime - lastPlayTime) < config.RetriggerLimitSeconds)
            {
                return null;
            }
        }

        // 3. Resolve or load SoundEffect instance
        if (!_sounds.TryGetValue(soundName, out var sfx))
        {
            if (_soundPaths.TryGetValue(soundName, out var filePath) && File.Exists(filePath))
            {
                sfx = AudioLoader.LoadSoundEffect(filePath);
                _sounds[soundName] = sfx;
            }
            else if (_musicPaths.TryGetValue(soundName, out var musicPath) && File.Exists(musicPath))
            {
                sfx = AudioLoader.LoadSoundEffect(musicPath);
                _sounds[soundName] = sfx;
            }
            else
            {
                return null;
            }
        }

        // 4. Voice Limiting & Polyphony Management
        if (!_activeVoices.TryGetValue(soundName, out var activeList))
        {
            activeList = new List<SoundEffectInstance>();
            _activeVoices[soundName] = activeList;
        }

        // Clean up stopped instances
        activeList.RemoveAll(i => i.State == SoundState.Stopped);

        if (activeList.Count >= config.MaxVoices)
        {
            if (config.StealRule == VoiceStealRule.IgnoreNewest)
            {
                return null;
            }

            // Steal Oldest
            var oldest = activeList[0];
            oldest.Stop();
            activeList.RemoveAt(0);
        }

        // 5. Create and trigger new instance
        float effectiveVolume = Math.Clamp(volume * _sfxVolume * _masterVolume, 0f, 1f);
        var instance = sfx.CreateInstance();
        instance.Volume = effectiveVolume;
        instance.Pitch = Math.Clamp(pitch, -1f, 1f);
        instance.Pan = Math.Clamp(pan, -1f, 1f);
        instance.Play();

        activeList.Add(instance);
        _lastPlayTimes[soundName] = _totalAudioTime;

        return instance;
    }

    /// <summary>
    /// Plays a sound effect fire-and-forget style.
    /// </summary>
    public static void PlaySoundAndForget(string soundName, float volume = 1f)
    {
        PlaySound(soundName, volume);
    }

    /// <summary>
    /// Plays a background music track with optional smooth fade-in and automatic crossfade.
    /// Only the active track is retained in memory.
    /// </summary>
    public static void PlayMusic(string musicName, float fadeDuration = 1f, bool loop = true)
    {
        if (_currentMusicName.Equals(musicName, StringComparison.OrdinalIgnoreCase) && IsMusicPlaying)
            return;

        if (!_musicPaths.TryGetValue(musicName, out var path) && !_sounds.ContainsKey(musicName))
            return;

        // Prepare fade out for current music if playing
        if (_currentMusicInstance != null && _currentMusicInstance.State == SoundState.Playing)
        {
            _fadingOutMusicInstance = _currentMusicInstance;
            _fadingOutMusicEffect = _currentMusicEffect;
            _fadeOutMusicTimer = 0f;
            _fadeOutMusicDuration = Math.Max(0.01f, fadeDuration);
            _fadeOutMusicStartVolume = _fadingOutMusicInstance.Volume;
        }

        // Load new music sound effect on-demand (streaming/uncompressed only while playing)
        SoundEffect? newEffect = null;
        if (_sounds.TryGetValue(musicName, out var existingEffect))
        {
            newEffect = existingEffect;
        }
        else if (path != null)
        {
            newEffect = AudioLoader.LoadSoundEffect(path);
        }

        if (newEffect == null) return;

        _currentMusicEffect = newEffect;
        _currentMusicInstance = newEffect.CreateInstance();
        _currentMusicInstance.IsLooped = loop;
        _currentMusicName = musicName;

        float targetVol = _isMuted ? 0f : _musicVolume * _masterVolume;

        if (fadeDuration > 0.05f)
        {
            _currentMusicInstance.Volume = 0f;
            _currentMusicFadeTimer = 0f;
            _currentMusicFadeDuration = fadeDuration;
            _currentMusicTargetVolume = targetVol;
        }
        else
        {
            _currentMusicInstance.Volume = targetVol;
            _currentMusicFadeDuration = 0f;
        }

        _currentMusicInstance.Play();
    }

    /// <summary>
    /// Smoothly stops the currently playing music.
    /// </summary>
    public static void StopMusic(float fadeDuration = 1f)
    {
        if (_currentMusicInstance == null || _currentMusicInstance.State != SoundState.Playing)
            return;

        _fadingOutMusicInstance = _currentMusicInstance;
        _fadingOutMusicEffect = _currentMusicEffect;
        _fadeOutMusicTimer = 0f;
        _fadeOutMusicDuration = Math.Max(0.01f, fadeDuration);
        _fadeOutMusicStartVolume = _fadingOutMusicInstance.Volume;

        _currentMusicInstance = null;
        _currentMusicEffect = null;
        _currentMusicName = string.Empty;
    }

    /// <summary>
    /// Pauses currently playing music.
    /// </summary>
    public static void PauseMusic()
    {
        _currentMusicInstance?.Pause();
    }

    /// <summary>
    /// Resumes paused music.
    /// </summary>
    public static void ResumeMusic()
    {
        _currentMusicInstance?.Resume();
    }

    /// <summary>
    /// Updates audio volume fades and internal timers. Call this inside your game's Update loop.
    /// </summary>
    public static void Update(float deltaTime)
    {
        _totalAudioTime += deltaTime;

        // 1. Process Fade-In for Current Music
        if (_currentMusicInstance != null && _currentMusicFadeDuration > 0f)
        {
            _currentMusicFadeTimer += deltaTime;
            float progress = Math.Clamp(_currentMusicFadeTimer / _currentMusicFadeDuration, 0f, 1f);
            float targetVol = _isMuted ? 0f : _musicVolume * _masterVolume;
            _currentMusicInstance.Volume = progress * targetVol;

            if (progress >= 1f)
            {
                _currentMusicFadeDuration = 0f;
            }
        }

        // 2. Process Fade-Out for Previous Music
        if (_fadingOutMusicInstance != null)
        {
            _fadeOutMusicTimer += deltaTime;
            float progress = Math.Clamp(_fadeOutMusicTimer / _fadeOutMusicDuration, 0f, 1f);
            _fadingOutMusicInstance.Volume = (1f - progress) * _fadeOutMusicStartVolume;

            if (progress >= 1f)
            {
                _fadingOutMusicInstance.Stop();
                _fadingOutMusicInstance.Dispose();
                _fadingOutMusicInstance = null;

                // Dispose old music audio data to free RAM
                if (_fadingOutMusicEffect != null && _fadingOutMusicEffect != _currentMusicEffect)
                {
                    _fadingOutMusicEffect.Dispose();
                    _fadingOutMusicEffect = null;
                }
            }
        }
    }

    private static void ApplyMusicVolume()
    {
        if (_currentMusicInstance != null && _currentMusicFadeDuration <= 0f)
        {
            _currentMusicInstance.Volume = _isMuted ? 0f : _musicVolume * _masterVolume;
        }
    }
}
