using System;
using System.IO;
using Wagenheimer.MonoGameHelper.Audio;
using Xunit;

namespace Wagenheimer.MonoGameHelper.Tests;

public class AudioLoadTests
{
    [Fact]
    public void TestAudioFormatsSupported()
    {
        // Check supported extensions validation
        Assert.Throws<FileNotFoundException>(() => AudioLoader.LoadSoundEffect("non_existent_file.mp3"));

        string tempFlac = Path.GetTempFileName() + ".flac";
        File.WriteAllText(tempFlac, "dummy");
        try
        {
            Assert.Throws<NotSupportedException>(() => AudioLoader.LoadSoundEffect(tempFlac));
        }
        finally
        {
            File.Delete(tempFlac);
        }
    }

    [Fact]
    public void TestConfigureSoundAndVolumeBuses()
    {
        AudioManager.MasterVolume = 0.9f;
        AudioManager.MusicVolume = 0.7f;
        AudioManager.SfxVolume = 0.8f;

        Assert.Equal(0.9f, AudioManager.MasterVolume);
        Assert.Equal(0.7f, AudioManager.MusicVolume);
        Assert.Equal(0.8f, AudioManager.SfxVolume);

        AudioManager.ConfigureSound("sfx_explode", maxVoices: 2, retriggerLimitSeconds: 0.05f, VoiceStealRule.StealOldest);

        // Verify registration of non-existent sound gracefully returns null when played
        var instance = AudioManager.PlaySound("unknown_sound_id_xyz");
        Assert.Null(instance);
    }
}
