using System;
using System.IO;
using System.Text;
using Wagenheimer.MonoGameHelper.Localization;
using Xunit;

namespace Wagenheimer.MonoGameHelper.Tests;

public class LocalizationTests
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "MonoGameHelperLoc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteJson(string dir, string fileName, string json) =>
        File.WriteAllText(Path.Combine(dir, fileName), json, Utf8NoBom);

    private static void Cleanup(string dir)
    {
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
    }

    [Fact]
    public void Indexer_FallsBackToDefaultCulture()
    {
        string dir = NewTempDir();
        try
        {
            WriteJson(dir, "en.json", """{ "back": "Back", "cancel": "Cancel" }""");
            WriteJson(dir, "pt-BR.json", """{ "back": "Voltar" }""");

            var localizer = new JsonStringLocalizer(dir, "en");
            localizer.SetCulture("pt-BR");

            Assert.Equal("Voltar", localizer["back"]);
            Assert.Equal("Cancel", localizer["cancel"]);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void Indexer_UnknownKey_ReturnsKeyAndTracksItAsMissing()
    {
        string dir = NewTempDir();
        try
        {
            WriteJson(dir, "en.json", """{ "back": "Back" }""");

            var localizer = new JsonStringLocalizer(dir, "en");

            Assert.Equal("ghost_key", localizer["ghost_key"]);
            Assert.Contains("ghost_key", localizer.MissingKeys);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void Format_SubstitutesArguments()
    {
        string dir = NewTempDir();
        try
        {
            WriteJson(dir, "en.json", """{ "levelcompleted": "Order {0} completed!" }""");

            var localizer = new JsonStringLocalizer(dir, "en");

            Assert.Equal("Order 3 completed!", localizer.Format("levelcompleted", 3));
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void Manifest_ExposesDefaultAndAvailableCultures()
    {
        string dir = NewTempDir();
        try
        {
            WriteJson(dir, "manifest.json",
                """{ "default": "en", "languages": [ { "code": "en", "name": "English" }, { "code": "pt-BR", "name": "Português (Brasil)" } ] }""");
            WriteJson(dir, "en.json", """{ "back": "Back" }""");
            WriteJson(dir, "pt-BR.json", """{ "back": "Voltar" }""");

            var localizer = new JsonStringLocalizer(dir, "en");

            Assert.Equal("en", localizer.DefaultCulture);
            Assert.Contains("pt-BR", localizer.AvailableCultures);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void SetCulture_UnknownCultureFallsBackToDefault()
    {
        string dir = NewTempDir();
        try
        {
            WriteJson(dir, "en.json", """{ "back": "Back", "cancel": "Cancel" }""");
            WriteJson(dir, "pt-BR.json", """{ "back": "Voltar" }""");

            var localizer = new JsonStringLocalizer(dir, "en");
            localizer.SetCulture("xx-XX");

            Assert.Equal("en", localizer.Culture);
            Assert.Equal("Cancel", localizer["cancel"]);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void WithoutManifest_DiscoversCulturesFromFiles()
    {
        string dir = NewTempDir();
        try
        {
            WriteJson(dir, "en.json", """{ "back": "Back" }""");
            WriteJson(dir, "pt-BR.json", """{ "back": "Voltar" }""");
            WriteJson(dir, "ja.json", """{ "back": "戻る" }""");

            var localizer = new JsonStringLocalizer(dir, "en");

            Assert.Equal("en", localizer.DefaultCulture);
            Assert.Contains("en", localizer.AvailableCultures);
            Assert.Contains("pt-BR", localizer.AvailableCultures);
            Assert.Contains("ja", localizer.AvailableCultures);
            Assert.DoesNotContain("manifest", localizer.AvailableCultures);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void SetCulture_IsCaseInsensitive()
    {
        string dir = NewTempDir();
        try
        {
            WriteJson(dir, "en.json", """{ "back": "Back" }""");
            WriteJson(dir, "pt-BR.json", """{ "back": "Voltar" }""");

            var localizer = new JsonStringLocalizer(dir, "en");
            localizer.SetCulture("pt-br");

            Assert.Equal("pt-BR", localizer.Culture);
            Assert.Equal("Voltar", localizer["back"]);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void Utf8_ReadsAccentedAndCjkValues()
    {
        string dir = NewTempDir();
        try
        {
            WriteJson(dir, "en.json", """{ "cancel": "Cancel" }""");
            WriteJson(dir, "ja.json", """{ "cancel": "キャンセル", "greeting": "Olá ção" }""");

            var localizer = new JsonStringLocalizer(dir, "en");
            localizer.SetCulture("ja");

            Assert.Equal("キャンセル", localizer["cancel"]);
            Assert.Equal("Olá ção", localizer["greeting"]);
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void SetCulture_SwitchingBackAndForth_ReusesCachedDictionaries()
    {
        string dir = NewTempDir();
        try
        {
            WriteJson(dir, "en.json", """{ "back": "Back", "cancel": "Cancel" }""");
            string ptBrPath = Path.Combine(dir, "pt-BR.json");
            WriteJson(dir, "pt-BR.json", """{ "back": "Voltar" }""");

            var localizer = new JsonStringLocalizer(dir, "en");
            Assert.Equal("Back", localizer["back"]);

            localizer.SetCulture("pt-BR");
            Assert.Equal("Voltar", localizer["back"]);

            // Removing the file proves the B -> A -> B cycle serves the cached A dictionary.
            File.Delete(ptBrPath);

            localizer.SetCulture("en");
            Assert.Equal("Back", localizer["back"]);
            Assert.Equal("Cancel", localizer["cancel"]);

            localizer.SetCulture("pt-BR");
            Assert.Equal("Voltar", localizer["back"]);
            Assert.Equal("pt-BR", localizer.Culture);
        }
        finally
        {
            Cleanup(dir);
        }
    }
}
