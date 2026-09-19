using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Wagenheimer.MonoGameHelper.Localization;

/// <summary>
/// JSON-backed <see cref="IStringLocalizer"/> that loads one <c>&lt;culture&gt;.json</c> file per
/// language from a directory at runtime, without any Content Pipeline dependency.
/// </summary>
/// <remarks>
/// <para>
/// The culture list comes from an optional <c>manifest.json</c>. When the manifest is absent the
/// directory is scanned and every <c>*.json</c> file name (without extension) becomes a culture,
/// with <c>defaultCulture</c> used as the default.
/// </para>
/// <para>
/// Keys are matched with <see cref="StringComparer.Ordinal"/> (case-sensitive). Reads are lock-free
/// through atomic reference swaps; <see cref="SetCulture"/> loads and caches culture files under a
/// private lock, so disk is only touched when a culture is loaded for the first time.
/// </para>
/// </remarks>
public sealed class JsonStringLocalizer : IStringLocalizer
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMap =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly string _directory;
    private readonly string _defaultCulture;
    private readonly string[] _availableCultures;
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _cache =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _missingKeys =
        new(StringComparer.Ordinal);

    private volatile IReadOnlyDictionary<string, string> _activeMap;
    private volatile IReadOnlyDictionary<string, string> _defaultMap;
    private volatile string _culture;

    /// <summary>
    /// Initializes a new localizer over the translation files found in <paramref name="directory"/>.
    /// </summary>
    /// <param name="directory">Directory containing the culture JSON files and optional manifest.json.</param>
    /// <param name="defaultCulture">
    /// Default culture code, used as the fallback and when no manifest is present.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="directory"/> or <paramref name="defaultCulture"/> is null, empty, or whitespace.
    /// </exception>
    public JsonStringLocalizer(string directory, string defaultCulture = "en")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCulture);

        _directory = directory;

        string manifestPath = Path.Combine(directory, "manifest.json");
        LocalizationManifest? manifest = null;
        string resolvedDefault = defaultCulture;

        if (File.Exists(manifestPath))
        {
            manifest = JsonSerializer.Deserialize<LocalizationManifest>(File.ReadAllText(manifestPath));

            string[] codes = manifest?.Languages?
                .Select(language => language.Code)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();

            _availableCultures = codes.Length > 0 ? codes : DiscoverCultures(directory);

            string? manifestDefault = manifest?.Default;
            resolvedDefault = string.IsNullOrWhiteSpace(manifestDefault) ? defaultCulture : manifestDefault!;
        }
        else
        {
            _availableCultures = DiscoverCultures(directory);
        }

        _defaultCulture = Canonicalize(resolvedDefault) ?? resolvedDefault;
        _culture = _defaultCulture;
        _defaultMap = GetOrLoad(_defaultCulture);
        _activeMap = _defaultMap;
    }

    /// <inheritdoc />
    public string this[string key]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (_activeMap.TryGetValue(key, out string? active) && !string.IsNullOrEmpty(active))
                return active;

            if (_defaultMap.TryGetValue(key, out string? fallback) && !string.IsNullOrEmpty(fallback))
                return fallback;

            _missingKeys.TryAdd(key, 0);
            return key;
        }
    }

    /// <inheritdoc />
    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, this[key], args);

    /// <inheritdoc />
    public string Culture => _culture;

    /// <inheritdoc />
    public string DefaultCulture => _defaultCulture;

    /// <inheritdoc />
    public IReadOnlyCollection<string> AvailableCultures => _availableCultures;

    /// <inheritdoc />
    public IReadOnlyCollection<string> MissingKeys => _missingKeys.Keys.ToArray();

    /// <inheritdoc />
    public void SetCulture(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            culture = _defaultCulture;

        string canonical = Canonicalize(culture) ?? _defaultCulture;

        IReadOnlyDictionary<string, string> active = GetOrLoad(canonical);
        IReadOnlyDictionary<string, string> fallback = GetOrLoad(_defaultCulture);

        _defaultMap = fallback;
        _activeMap = active;
        _culture = canonical;
    }

    private string? Canonicalize(string culture)
    {
        foreach (string available in _availableCultures)
        {
            if (string.Equals(available, culture, StringComparison.OrdinalIgnoreCase))
                return available;
        }

        return null;
    }

    private IReadOnlyDictionary<string, string> GetOrLoad(string culture)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(culture, out IReadOnlyDictionary<string, string>? cached))
                return cached;

            IReadOnlyDictionary<string, string> map = LoadCultureFile(culture);
            _cache[culture] = map;
            return map;
        }
    }

    private IReadOnlyDictionary<string, string> LoadCultureFile(string culture)
    {
        string path = Path.Combine(_directory, culture + ".json");
        if (!File.Exists(path))
            return EmptyMap;

        string json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
            return EmptyMap;

        Dictionary<string, string>? parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (parsed is null || parsed.Count == 0)
            return EmptyMap;

        Dictionary<string, string> map = new(parsed.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in parsed)
            map[pair.Key] = pair.Value;

        return map;
    }

    private static string[] DiscoverCultures(string directory)
    {
        if (!Directory.Exists(directory))
            return Array.Empty<string>();

        return Directory.GetFiles(directory, "*.json")
            .Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty)
            .Where(name => name.Length > 0 &&
                           !name.Equals("manifest", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }
}
