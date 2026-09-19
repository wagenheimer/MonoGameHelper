using System;
using Microsoft.Xna.Framework;

namespace Wagenheimer.MonoGameHelper.Graphics;

/// <summary>
/// Describes how much an atlas was scaled when it was packed, so sprites can be drawn at the
/// same **on-screen size** regardless of the art resolution.
///
/// ## The problem
///
/// If you ship 2x art, every sprite is twice as large in pixels. Drawing it at its natural size
/// would double everything on screen and break every layout.
///
/// ## The rule
///
/// The *design size* of a sprite is simply **its size in the 1x art**. Therefore the correction
/// factor is **uniform per atlas**:
///
/// <code>
/// BaseScale = 1 / Scale        // 1x -> 1.0 | 2x -> 0.5 | 4x -> 0.25
/// </code>
///
/// > ⚠️ **Never** derive a "design size" per sprite (e.g. `cellWidth / sprite.SourceRect.Width`).
/// > Atlases routinely contain sprites of many different natural sizes; such a formula would
/// > stretch each one to the cell size and destroy the layout.
///
/// ## Usage
///
/// <code>
/// var art = new ArtScale(2f);              // atlas packed at 2x
/// entity.Transform.SetScale(art.ToAbsolute());        // "1x look", half the pixels
/// entity.Transform.SetScale(art.ToAbsolute(1.35f));   // relative punch animation
/// </code>
///
/// Scale animations must always be expressed **relative** to <see cref="BaseScale"/>:
/// absolute values (`Vector2.One`, `1.35f`, `0.78f`) would reset a sprite to the wrong size as
/// soon as the art resolution changes.
/// </summary>
public readonly struct ArtScale : IEquatable<ArtScale>
{
    /// <summary>Packing scale of the art: `1` = 1x, `2` = 2x, `4` = 4x.</summary>
    public float Scale { get; }

    /// <summary>Factor that keeps the on-screen size stable: `1 / Scale`.</summary>
    public float BaseScale { get; }

    /// <summary>Creates an art scale from the atlas packing factor (must be greater than zero).</summary>
    public ArtScale(float scale)
    {
        if (scale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "Art scale must be greater than zero.");

        Scale = scale;
        BaseScale = 1f / scale;
    }

    /// <summary>1x art (no correction).</summary>
    public static ArtScale One => new(1f);

    /// <summary>Creates an art scale from an atlas packing factor.</summary>
    public static ArtScale FromAtlasScale(float atlasScale) => new(atlasScale);

    /// <summary>
    /// Converts a **relative** scale (as authored for 1x art) into the absolute scale to apply
    /// to a transform. `ToAbsolute()` returns the neutral size.
    /// </summary>
    public Vector2 ToAbsolute(float relative = 1f) => new(BaseScale * relative);

    /// <summary>Non-uniform variant of <see cref="ToAbsolute(float)"/>.</summary>
    public Vector2 ToAbsolute(Vector2 relative) => new(BaseScale * relative.X, BaseScale * relative.Y);

    /// <summary>
    /// Converts a pixel measurement taken from the packed art back into design units.
    /// `Unscale(148)` returns `74` for 2x art.
    /// </summary>
    public float Unscale(float artPixels) => artPixels / Scale;

    /// <inheritdoc />
    public bool Equals(ArtScale other) => Scale.Equals(other.Scale);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ArtScale other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Scale.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => $"{Scale:0.##}x (base {BaseScale:0.###})";

    public static bool operator ==(ArtScale left, ArtScale right) => left.Equals(right);

    public static bool operator !=(ArtScale left, ArtScale right) => !left.Equals(right);
}

/// <summary>
/// Reads the multi-resolution metadata of a <c>libGDX</c>-style <c>.atlas</c> file.
///
/// The libGDX atlas format records the packing scale in a <c>scale:</c> header line:
///
/// <code>
/// tokens@2x.png
/// size: 1024, 1024
/// format: RGBA8888
/// filter: Linear, Linear
/// repeat: none
/// scale: 2
/// </code>
///
/// Packing the same source at several scales (`scale: [1,2,4]`, `scaleSuffix: ["","@2x","@4x"]`)
/// yields atlases with identical layouts that differ only by this factor — which is exactly what
/// <see cref="ArtScale"/> consumes.
/// </summary>
public static class AtlasScaleReader
{
    /// <summary>Default packing scale when the atlas carries no <c>scale:</c> metadata.</summary>
    public const float DefaultScale = 1f;

    /// <summary>
    /// Parses a single atlas header line such as <c>scale: 2</c> or <c>scale:2</c>.
    /// </summary>
    /// <returns><c>true</c> when the line is a valid, positive scale declaration.</returns>
    public static bool TryParseScaleLine(string? line, out float scale)
    {
        scale = DefaultScale;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        var trimmed = line.Trim();
        if (!trimmed.StartsWith("scale", StringComparison.OrdinalIgnoreCase))
            return false;

        int separator = trimmed.IndexOf(':');
        if (separator < 0)
            return false;

        var value = trimmed[(separator + 1)..].Trim();
        if (!float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed <= 0f)
            return false;

        scale = parsed;
        return true;
    }

    /// <summary>
    /// Reads the packing scale from an atlas file's header. Returns <see cref="DefaultScale"/>
    /// (1x) when the file is missing, unreadable, or has no <c>scale:</c> line.
    /// </summary>
    /// <remarks>
    /// Only the header is scanned (up to the first blank line or the first sprite record),
    /// so this is cheap enough to call at load time.
    /// </remarks>
    public static float ReadScaleFromFile(string atlasPath)
    {
        if (string.IsNullOrWhiteSpace(atlasPath) || !System.IO.File.Exists(atlasPath))
            return DefaultScale;

        try
        {
            foreach (var rawLine in System.IO.File.ReadLines(atlasPath))
            {
                var line = rawLine.Trim();

                // The sprite records start right after the header; a record is "name\nx,y,w,h\nox,oy".
                // Headers always contain a colon, sprite names never do.
                if (line.Length == 0 || !line.Contains(':'))
                {
                    if (TryParseScaleLine(line, out var scale))
                        return scale;

                    // Reached the sprite list without finding a scale header.
                    break;
                }

                if (TryParseScaleLine(line, out var headerScale))
                    return headerScale;
            }
        }
        catch (System.IO.IOException)
        {
            // Fall back to 1x rather than failing asset loading over optional metadata.
        }

        return DefaultScale;
    }

    /// <summary>Reads the packing scale and returns it as an <see cref="ArtScale"/>.</summary>
    public static ArtScale ReadArtScaleFromFile(string atlasPath) => new(ReadScaleFromFile(atlasPath));
}
