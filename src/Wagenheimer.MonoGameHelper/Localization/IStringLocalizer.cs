using System;
using System.Collections.Generic;

namespace Wagenheimer.MonoGameHelper.Localization;

/// <summary>
/// Resolves localized strings for an active culture with fallback to a default culture.
/// Implementations are expected to be safe for concurrent reads.
/// </summary>
public interface IStringLocalizer
{
    /// <summary>
    /// Gets the value for <paramref name="key"/> from the active culture, falling back to the
    /// default culture and finally to the key itself.
    /// </summary>
    /// <param name="key">Case-sensitive translation key.</param>
    /// <returns>The active-culture value, the default-culture value, or <paramref name="key"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="key"/> is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    string this[string key] { get; }

    /// <summary>
    /// Resolves <paramref name="key"/> and formats it using the invariant culture.
    /// </summary>
    /// <param name="key">Case-sensitive translation key.</param>
    /// <param name="args">Values substituted into the resolved format string.</param>
    /// <returns>The formatted, localized string.</returns>
    string Format(string key, params object[] args);

    /// <summary>Gets the canonical code of the active culture (e.g. "pt-BR").</summary>
    string Culture { get; }

    /// <summary>Gets the canonical code of the default culture (e.g. "en").</summary>
    string DefaultCulture { get; }

    /// <summary>Gets every culture code known to the localizer.</summary>
    IReadOnlyCollection<string> AvailableCultures { get; }

    /// <summary>
    /// Gets the keys that could not be resolved in either the active or the default culture.
    /// The collection accumulates over the lifetime of the localizer.
    /// </summary>
    IReadOnlyCollection<string> MissingKeys { get; }

    /// <summary>
    /// Switches the active culture. Unknown codes fall back to <see cref="DefaultCulture"/>.
    /// Comparison is case-insensitive, so "pt-br" activates the "pt-BR" culture.
    /// </summary>
    /// <param name="culture">Culture code to activate.</param>
    void SetCulture(string culture);
}
