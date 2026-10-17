using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Wagenheimer.MonoGameHelper.Graphics;

/// <summary>
/// Represents a sub-region (single sprite) within a <see cref="TextureAtlas"/>.
/// </summary>
public sealed class AtlasRegion
{
    /// <summary>Sprite name without file extension.</summary>
    public string Name { get; }

    /// <summary>Cropped rectangle bounds in the atlas texture (pixels).</summary>
    public Rectangle Bounds { get; }

    /// <summary>Original size before whitespace trimming (or equal to Bounds if untrimmed).</summary>
    public Point OriginalSize { get; }

    /// <summary>Offset from the top-left corner of the original untrimmed image.</summary>
    public Point Offset { get; }

    /// <summary>Frame index (e.g. for numbered animation sequences, or -1 if unindexed).</summary>
    public int Index { get; }

    /// <summary>Whether the sprite was rotated 90 degrees clockwise during packing.</summary>
    public bool Rotate { get; }

    /// <summary>The atlas texture page this region belongs to, or null if parsed without texture.</summary>
    public Texture2D? Texture { get; }

    public AtlasRegion(
        string name,
        Rectangle bounds,
        Point originalSize,
        Point offset,
        int index,
        bool rotate,
        Texture2D? texture = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Bounds = bounds;
        OriginalSize = originalSize;
        Offset = offset;
        Index = index;
        Rotate = rotate;
        Texture = texture;
    }

    public override string ToString() => $"{Name} [{Bounds.X},{Bounds.Y},{Bounds.Width}x{Bounds.Height}]";
}

/// <summary>
/// A fast, pure MonoGame parser and container for <c>libGDX</c>-style texture atlases (<c>.atlas</c>).
/// Compatible with Nez SpriteAtlasPacker, TexturePacker (libGDX exporter), and standard GDX atlases.
/// </summary>
public sealed class TextureAtlas : IDisposable
{
    private readonly Dictionary<string, AtlasRegion> _regions;
    private readonly bool _ownsTexture;

    /// <summary>The underlying texture for this atlas page (or null if parsed headlessly).</summary>
    public Texture2D? Texture { get; }

    /// <summary>The art resolution scale of this atlas (e.g. 1x, 2x, 4x).</summary>
    public ArtScale ArtScale { get; }

    /// <summary>All parsed regions indexed by sprite name.</summary>
    public IReadOnlyDictionary<string, AtlasRegion> Regions => _regions;

    /// <summary>Gets a sprite region by name. Throws <see cref="KeyNotFoundException"/> if not found.</summary>
    public AtlasRegion this[string name]
    {
        get
        {
            if (_regions.TryGetValue(name, out var region))
                return region;
            throw new KeyNotFoundException($"Sprite region '{name}' was not found in texture atlas.");
        }
    }

    public TextureAtlas(Texture2D? texture, Dictionary<string, AtlasRegion> regions, ArtScale artScale, bool ownsTexture = false)
    {
        Texture = texture;
        _regions = regions ?? throw new ArgumentNullException(nameof(regions));
        ArtScale = artScale;
        _ownsTexture = ownsTexture;
    }

    /// <summary>Attempts to retrieve a region by name without throwing.</summary>
    public bool TryGetRegion(string name, out AtlasRegion? region)
    {
        if (_regions.TryGetValue(name, out var found))
        {
            region = found;
            return true;
        }

        region = null;
        return false;
    }

    /// <summary>Returns true if the atlas contains a region with the given name.</summary>
    public bool ContainsRegion(string name) => _regions.ContainsKey(name);

    /// <summary>
    /// Parses a libGDX-style atlas description from string content and binds it to an optional texture.
    /// </summary>
    public static TextureAtlas Parse(string atlasContent, Texture2D? texture = null, float defaultScale = 1f)
    {
        using var reader = new StringReader(atlasContent);
        return Parse(reader, texture, defaultScale);
    }

    /// <summary>
    /// Parses a libGDX-style atlas description from a <see cref="TextReader"/> and binds it to an optional texture.
    /// </summary>
    public static TextureAtlas Parse(TextReader reader, Texture2D? texture = null, float defaultScale = 1f)
    {
        var regions = new Dictionary<string, AtlasRegion>(StringComparer.OrdinalIgnoreCase);
        float scale = defaultScale;

        string? line;
        // Skip any leading blank lines
        while ((line = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
                break;
        }

        // The first non-empty line is the texture page image name (e.g. "menu.png")
        // string? textureFileName = line?.Trim();

        // Read page properties (size, format, filter, repeat, scale) until blank line or first sprite name
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                break;

            if (trimmed.Contains(':'))
            {
                if (AtlasScaleReader.TryParseScaleLine(trimmed, out var parsedScale))
                {
                    scale = parsedScale;
                }
            }
            else
            {
                // Reached a sprite name without an empty line separator
                break;
            }
        }

        // Parse sprite records
        string? currentName = null;
        if (line != null && !string.IsNullOrWhiteSpace(line) && !line.Contains(':'))
        {
            currentName = line.Trim();
        }

        bool rotate = false;
        int x = 0, y = 0, w = 0, h = 0;
        int origW = 0, origH = 0;
        int offsetX = 0, offsetY = 0;
        int index = -1;

        void SaveCurrentRegion()
        {
            if (currentName != null)
            {
                regions[currentName] = new AtlasRegion(
                    currentName,
                    new Rectangle(x, y, w, h),
                    new Point(origW > 0 ? origW : w, origH > 0 ? origH : h),
                    new Point(offsetX, offsetY),
                    index,
                    rotate,
                    texture);
                currentName = null;
            }
        }

        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                SaveCurrentRegion();
                continue;
            }

            if (!line.StartsWith(" ") && !line.StartsWith("\t") && !trimmed.Contains(':'))
            {
                // New sprite name
                SaveCurrentRegion();
                currentName = trimmed;
                rotate = false;
                x = y = w = h = 0;
                origW = origH = 0;
                offsetX = offsetY = 0;
                index = -1;
                continue;
            }

            int colon = trimmed.IndexOf(':');
            if (colon < 0) continue;

            string key = trimmed[..colon].Trim().ToLowerInvariant();
            string value = trimmed[(colon + 1)..].Trim();

            switch (key)
            {
                case "rotate":
                    rotate = bool.TryParse(value, out var r) && r;
                    break;
                case "xy":
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 2)
                    {
                        int.TryParse(parts[0].Trim(), out x);
                        int.TryParse(parts[1].Trim(), out y);
                    }
                    break;
                }
                case "size":
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 2)
                    {
                        int.TryParse(parts[0].Trim(), out w);
                        int.TryParse(parts[1].Trim(), out h);
                    }
                    break;
                }
                case "orig":
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 2)
                    {
                        int.TryParse(parts[0].Trim(), out origW);
                        int.TryParse(parts[1].Trim(), out origH);
                    }
                    break;
                }
                case "offset":
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 2)
                    {
                        int.TryParse(parts[0].Trim(), out offsetX);
                        int.TryParse(parts[1].Trim(), out offsetY);
                    }
                    break;
                }
                case "index":
                    int.TryParse(value, out index);
                    break;
            }
        }

        SaveCurrentRegion();

        return new TextureAtlas(texture, regions, new ArtScale(scale));
    }

    /// <summary>
    /// Loads a texture atlas and its matching PNG image from disk.
    /// </summary>
    /// <param name="graphicsDevice">The active GraphicsDevice.</param>
    /// <param name="atlasPath">Path to the .atlas metadata file.</param>
    /// <param name="imagePath">Path to the .png file (if null, uses same base path as atlas with .png extension).</param>
    /// <param name="premultiplyAlpha">Whether to premultiply alpha on the loaded texture.</param>
    public static TextureAtlas FromFile(
        GraphicsDevice graphicsDevice,
        string atlasPath,
        string? imagePath = null,
        bool premultiplyAlpha = false)
    {
        if (graphicsDevice == null) throw new ArgumentNullException(nameof(graphicsDevice));
        if (!File.Exists(atlasPath)) throw new FileNotFoundException("Atlas file not found.", atlasPath);

        imagePath ??= Path.ChangeExtension(atlasPath, ".png");
        if (!File.Exists(imagePath)) throw new FileNotFoundException("Atlas image file not found.", imagePath);

        Texture2D texture;
        using (var stream = File.OpenRead(imagePath))
        {
            texture = Texture2D.FromStream(graphicsDevice, stream);
        }

        if (premultiplyAlpha)
        {
            PremultiplyAlpha(texture);
        }

        string atlasText = File.ReadAllText(atlasPath);
        var atlas = Parse(atlasText, texture);
        return new TextureAtlas(texture, (Dictionary<string, AtlasRegion>)atlas.Regions, atlas.ArtScale, ownsTexture: true);
    }

    private static void PremultiplyAlpha(Texture2D texture)
    {
        var data = new Color[texture.Width * texture.Height];
        texture.GetData(data);
        for (int i = 0; i < data.Length; i++)
        {
            var c = data[i];
            float a = c.A / 255f;
            data[i] = new Color((byte)(c.R * a), (byte)(c.G * a), (byte)(c.B * a), c.A);
        }
        texture.SetData(data);
    }

    public void Dispose()
    {
        if (_ownsTexture)
        {
            Texture?.Dispose();
        }
    }
}
