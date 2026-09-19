using System;
using System.IO;
using Microsoft.Xna.Framework.Audio;

namespace Wagenheimer.MonoGameHelper.Audio;

/// <summary>
/// Universal audio file loader supporting WAV, MP3, and OGG formats into MonoGame SoundEffect instances.
/// </summary>
public static class AudioLoader
{
    /// <summary>
    /// Loads a SoundEffect from a file path (.wav, .mp3, or .ogg).
    /// </summary>
    /// <param name="filePath">Absolute or relative file path to the audio file.</param>
    /// <returns>A new SoundEffect containing the decoded audio.</returns>
    public static SoundEffect LoadSoundEffect(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Audio file not found: {filePath}", filePath);

        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".wav" => LoadWav(filePath),
            ".mp3" => LoadMp3(filePath),
            ".ogg" => LoadOgg(filePath),
            _ => throw tropicalNotSupportedException(ext)
        };
    }

    private static NotSupportedException tropicalNotSupportedException(string ext) =>
        new($"Audio format '{ext}' is not supported. Use .wav, .mp3, or .ogg.");

    private static SoundEffect LoadWav(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return SoundEffect.FromStream(stream);
    }

    private static SoundEffect LoadMp3(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var mpeg = new NLayer.MpegFile(stream);

        int sampleRate = mpeg.SampleRate;
        int channels = mpeg.Channels;

        var floatBuffer = new float[8192];
        using var ms = new MemoryStream();

        int read;
        while ((read = mpeg.ReadSamples(floatBuffer, 0, floatBuffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                short pcm = (short)Math.Clamp(floatBuffer[i] * 32767f, short.MinValue, short.MaxValue);
                ms.WriteByte((byte)(pcm & 0xFF));
                ms.WriteByte((byte)((pcm >> 8) & 0xFF));
            }
        }

        return new SoundEffect(ms.ToArray(), sampleRate, (AudioChannels)channels);
    }

    private static SoundEffect LoadOgg(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new NVorbis.VorbisReader(stream, closeOnDispose: false);

        int sampleRate = reader.SampleRate;
        int channels = reader.Channels;

        var floatBuffer = new float[8192];
        using var ms = new MemoryStream();

        int read;
        while ((read = reader.ReadSamples(floatBuffer, 0, floatBuffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                short pcm = (short)Math.Clamp(floatBuffer[i] * 32767f, short.MinValue, short.MaxValue);
                ms.WriteByte((byte)(pcm & 0xFF));
                ms.WriteByte((byte)((pcm >> 8) & 0xFF));
            }
        }

        return new SoundEffect(ms.ToArray(), sampleRate, (AudioChannels)channels);
    }
}
