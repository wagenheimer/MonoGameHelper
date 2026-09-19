using System;
using System.IO;
using System.Threading.Tasks;
using Wagenheimer.MonoGameHelper.Storage;
using Xunit;

namespace Wagenheimer.MonoGameHelper.Tests;

public class StorageTests
{
    private record TestPlayerProfile(string Name, int Level, int Coins);

    [Fact]
    public void TestAtomicSaveAndLoad()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "MonoGameHelperTests_" + Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(testDir, "savegame.json");

        try
        {
            var profile = new TestPlayerProfile("Cezar", 15, 2500);
            SafeJsonStorage.Save(filePath, profile);

            Assert.True(File.Exists(filePath));

            var loaded = SafeJsonStorage.Load<TestPlayerProfile>(filePath);
            Assert.NotNull(loaded);
            Assert.Equal("Cezar", loaded.Name);
            Assert.Equal(15, loaded.Level);
            Assert.Equal(2500, loaded.Coins);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public async Task TestAsyncSaveAndCorruptFallbackRecovery()
    {
        string testDir = Path.Combine(Path.GetTempPath(), "MonoGameHelperTests_" + Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(testDir, "savegame_backup.json");

        try
        {
            // Initial save
            var profile1 = new TestPlayerProfile("InitialPlayer", 1, 100);
            await SafeJsonStorage.SaveAsync(filePath, profile1);

            // Second save -> creates .bak with initial save
            var profile2 = new TestPlayerProfile("UpdatedPlayer", 2, 200);
            await SafeJsonStorage.SaveAsync(filePath, profile2);

            Assert.True(File.Exists(filePath));
            Assert.True(File.Exists(filePath + ".bak"));

            // Now corrupt the primary file
            await File.WriteAllTextAsync(filePath, "{ corrupt json ... invalid syntax [[[ ");

            // Load should catch the corruption and safely fallback to .bak
            var recovered = await SafeJsonStorage.LoadAsync<TestPlayerProfile>(filePath);
            Assert.NotNull(recovered);
            Assert.Equal("InitialPlayer", recovered.Name);
            Assert.Equal(1, recovered.Level);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public void TestNonExistentFileReturnsDefault()
    {
        var fallback = new TestPlayerProfile("Default", 0, 0);
        var loaded = SafeJsonStorage.Load<TestPlayerProfile>("non_existent_file_12345.json", fallback);

        Assert.NotNull(loaded);
        Assert.Equal("Default", loaded.Name);
    }
}
