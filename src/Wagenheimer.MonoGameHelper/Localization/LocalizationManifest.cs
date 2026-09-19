using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Wagenheimer.MonoGameHelper.Localization;

/// <summary>
/// Describes the languages available to a <see cref="JsonStringLocalizer"/> through a
/// <c>manifest.json</c> file located next to the translation files.
/// </summary>
public sealed class LocalizationManifest
{
    /// <summary>
    /// Default culture code used when the active culture is missing a key (e.g. "en").
    /// </summary>
    [JsonPropertyName("default")]
    public string Default { get; set; } = "en";

    /// <summary>Languages declared by the manifest.</summary>
    [JsonPropertyName("languages")]
    public List<LanguageInfo> Languages { get; set; } = new();
}

/// <summary>A single language entry of a <see cref="LocalizationManifest"/>.</summary>
public sealed class LanguageInfo
{
    /// <summary>Culture code, e.g. "pt-BR".</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable language name, e.g. "Português (Brasil)".</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
