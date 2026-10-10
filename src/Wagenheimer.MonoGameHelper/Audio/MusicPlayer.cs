using System;
using Microsoft.Xna.Framework.Media;

namespace Wagenheimer.MonoGameHelper.Audio;

/// <summary>
/// Toca uma faixa de música **em streaming** a partir de um arquivo, sem manter a faixa inteira em
/// RAM — a contraparte de <see cref="AudioManager"/> (que decodifica o arquivo inteiro num
/// <see cref="Microsoft.Xna.Framework.Audio.SoundEffect"/>).
///
/// Use este caminho para **música** (faixas longas: ~30 MB de PCM por minuto estéreo a 44,1 kHz) e o
/// <see cref="AudioManager"/> para **SFX** (curtos, precisam de sobreposição/polifonia).
///
/// Usa <see cref="Song"/> + <see cref="MediaPlayer"/> (Ogg Vorbis no DesktopGL). Como o suporte a
/// streaming varia por plataforma, <see cref="Play"/> **devolve <c>false</c>** (em vez de lançar)
/// quando o backend não suporta — o chamador pode cair para o <see cref="AudioManager"/>.
/// </summary>
public static class MusicPlayer
{
    private static Song? _current;
    private static string _currentName = string.Empty;

    /// <summary>Nome da faixa em reprodução (ou vazio).</summary>
    public static string CurrentName => _currentName;

    /// <summary>Verdadeiro quando há música tocando.</summary>
    public static bool IsPlaying => MediaPlayer.State == MediaState.Playing;

    /// <summary>Volume da música (0..1), aplicado ao mixer global de mídia.</summary>
    public static float Volume
    {
        get => MediaPlayer.Volume;
        set => MediaPlayer.Volume = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Muta/desmuta a música.</summary>
    public static bool IsMuted
    {
        get => MediaPlayer.IsMuted;
        set => MediaPlayer.IsMuted = value;
    }

    /// <summary>
    /// Começa a tocar <paramref name="filePath"/> em streaming. Idempotente para a mesma faixa.
    /// </summary>
    /// <returns><c>true</c> se começou a tocar; <c>false</c> se o streaming não é suportado.</returns>
    public static bool Play(string name, string filePath, bool loop = true, float volume = 1f)
    {
        try
        {
            if (_currentName.Equals(name, StringComparison.OrdinalIgnoreCase) && IsPlaying)
                return true;

            Stop();

            // `Song.FromUri` não passa pelo Content Pipeline: mantém a faixa em disco/stream.
            var song = Song.FromUri(name, new Uri(filePath, UriKind.Absolute));
            MediaPlayer.IsRepeating = loop;
            MediaPlayer.Volume = Math.Clamp(volume, 0f, 1f);
            MediaPlayer.Play(song);

            _current = song;
            _currentName = name;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MusicPlayer] streaming indisponivel para '{name}': {ex.Message}");
            return false;
        }
    }

    /// <summary>Para a música atual.</summary>
    public static void Stop()
    {
        try
        {
            if (MediaPlayer.State != MediaState.Stopped)
                MediaPlayer.Stop();
        }
        catch
        {
            // O mixer de mídia pode não estar disponível em alguns backends — ignorar.
        }

        _current = null;
        _currentName = string.Empty;
    }

    /// <summary>Pausa a música.</summary>
    public static void Pause() => MediaPlayer.Pause();

    /// <summary>Retoma a música.</summary>
    public static void Resume() => MediaPlayer.Resume();
}
