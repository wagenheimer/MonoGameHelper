#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Gum.GueDeriving;
using Gum.Wireframe;
using KernSmith;
using KernSmith.Gum;
using KernSmith.Output;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary.Graphics;
using RenderingLibrary.Graphics.Fonts;

namespace Wagenheimer.MonoGameHelper.Gum;

/// <summary>
/// Provides advanced font material generation for Gum UI in MonoGame:
/// vertical color gradients and soft baked drop shadows rendered via KernSmith / FreeType.
/// </summary>
public static class GumFontMaterials
{
    private static GraphicsDevice? _graphicsDevice;
    private static readonly ConcurrentDictionary<string, FontMaterial> RegisteredMaterials = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, BitmapFont> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<AppliedRecord> Applied = new();
    private static readonly object SyncLock = new();

    private sealed record AppliedRecord(WeakReference<TextRuntime> TextRef, FontMaterial Material, string FontFilePath, int FontSize, bool Bold);

    static GumFontMaterials()
    {
        // Pre-register standard presets
        RegisterMaterial("Gold", FontMaterial.Gold);
        RegisterMaterial("Silver", FontMaterial.Silver);
        RegisterMaterial("Bronze", FontMaterial.Bronze);
        RegisterMaterial("Emerald", FontMaterial.Emerald);
        RegisterMaterial("Ruby", FontMaterial.Ruby);
        RegisterMaterial("Amber", FontMaterial.Amber);
        RegisterMaterial("Sapphire", FontMaterial.Sapphire);
        RegisterMaterial("Amethyst", FontMaterial.Amethyst);
        RegisterMaterial("WhiteShadow", FontMaterial.WhiteShadow);
    }

    /// <summary>
    /// Configures the active GraphicsDevice used to allocate font textures.
    /// Typically called once in Game.Initialize right after GumUI.Initialize.
    /// </summary>
    public static void SetGraphicsDevice(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    /// <summary>Registers a named custom font material for convenient reuse.</summary>
    public static void RegisterMaterial(string name, FontMaterial material)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Material name cannot be null or empty.", nameof(name));
        RegisteredMaterials[name] = material ?? throw new ArgumentNullException(nameof(material));
    }

    /// <summary>Retrieves a registered font material by name.</summary>
    public static FontMaterial? GetMaterial(string name)
    {
        return RegisteredMaterials.TryGetValue(name, out var mat) ? mat : null;
    }

    /// <summary>
    /// Applies a registered material by name to a Gum TextRuntime element.
    /// </summary>
    public static bool Apply(TextRuntime text, string materialName, string fontFilePath, int fontSize, bool bold = false)
    {
        if (!RegisteredMaterials.TryGetValue(materialName, out var material))
        {
            Console.WriteLine($"[GumFontMaterials] Aviso: Material '{materialName}' nao encontrado.");
            return false;
        }

        return Apply(text, material, fontFilePath, fontSize, bold);
    }

    /// <summary>
    /// Generates (or retrieves from cache) a BitmapFont with baked gradient and shadow and assigns it to text.Typeface.
    /// </summary>
    public static bool Apply(TextRuntime text, FontMaterial material, string fontFilePath, int fontSize, bool bold = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(material);

        var device = _graphicsDevice;
        if (device == null)
        {
            Console.WriteLine("[GumFontMaterials] GraphicsDevice nao inicializado. Chame GumFontMaterials.SetGraphicsDevice primeiro.");
            return false;
        }

        var font = GetOrCreate(device, material, fontFilePath, fontSize, bold);
        if (font == null)
            return false;

        // In Gum.MonoGame, assigning text.Typeface applies the baked font texture
        text.Typeface = font;

        lock (SyncLock)
        {
            // Remove garbage-collected or duplicate entries
            Applied.RemoveAll(r => !r.TextRef.TryGetTarget(out var target) || ReferenceEquals(target, text));
            Applied.Add(new AppliedRecord(new WeakReference<TextRuntime>(text), material, fontFilePath, fontSize, bold));
        }

        return true;
    }

    /// <summary>
    /// Finds a child Text element in a Gum root by instance name and applies a font material to it.
    /// </summary>
    public static bool ApplyTo(GraphicalUiElement? root, string instanceName, FontMaterial material, string fontFilePath, int fontSize, bool bold = false)
    {
        if (root?.GetGraphicalUiElementByName(instanceName) is not TextRuntime label)
            return false;

        return Apply(label, material, fontFilePath, fontSize, bold);
    }

    /// <summary>
    /// Re-applies all active font materials. Call when window is resized or graphics device resets.
    /// </summary>
    public static void ReapplyAll()
    {
        AppliedRecord[] records;
        lock (SyncLock)
        {
            records = Applied.ToArray();
        }

        foreach (var record in records)
        {
            if (record.TextRef.TryGetTarget(out var text))
            {
                Apply(text, record.Material, record.FontFilePath, record.FontSize, record.Bold);
            }
        }
    }

    /// <summary>Generates or retrieves a cached BitmapFont for the given material and configuration.</summary>
    public static BitmapFont? GetOrCreate(GraphicsDevice graphicsDevice, FontMaterial material, string fontFilePath, int fontSize, bool bold = false)
    {
        string cacheKey = $"{fontFilePath}|{fontSize}|{bold}|{material.GetHashCode()}";
        if (Cache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            if (!File.Exists(fontFilePath))
            {
                Console.WriteLine($"[GumFontMaterials] Arquivo de fonte nao encontrado: {fontFilePath}");
                return null;
            }

            var save = new BmfcSave
            {
                FontName = Path.GetFileNameWithoutExtension(fontFilePath),
                FontSize = fontSize,
                IsBold = bold,
            };

            var options = GumFontGenerator.BuildOptions(save);

            // Vertical gradient
            options.GradientStartR = material.GradientTop.R;
            options.GradientStartG = material.GradientTop.G;
            options.GradientStartB = material.GradientTop.B;
            options.GradientEndR = material.GradientBottom.R;
            options.GradientEndG = material.GradientBottom.G;
            options.GradientEndB = material.GradientBottom.B;
            options.GradientAngle = material.GradientAngle;
            options.GradientMidpoint = 0.5f;

            // Soft drop shadow / blurred border
            var shadowColor = material.ShadowColor ?? Color.Black;
            options.Outline = 0;
            options.ShadowR = shadowColor.R;
            options.ShadowG = shadowColor.G;
            options.ShadowB = shadowColor.B;
            options.ShadowOffsetX = material.ShadowOffsetX;
            options.ShadowOffsetY = material.ShadowOffsetY;
            options.ShadowOpacity = material.ShadowOpacity;
            options.ShadowBlur = (int)material.ShadowBlur;
            options.HardShadow = false;

            // Maintain full RGBA channels for colors & shadows
            options.Channels = null;

            // Padding prevents glyph shadow clipping
            int p = material.Padding;
            options.Padding = new KernSmith.Padding(p, p, p, p);

            // Generate font via BmFont
            var result = BmFont.Generate(fontFilePath, options);
            var font = CreateBitmapFont(result, graphicsDevice);

            Cache[cacheKey] = font;
            return font;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GumFontMaterials] Falha ao gerar material de fonte: {ex.Message}");
            return null;
        }
    }

    private static BitmapFont CreateBitmapFont(BmFontResult result, GraphicsDevice graphicsDevice)
    {
        var textures = new Texture2D[result.Pages.Count];
        for (var i = 0; i < result.Pages.Count; i++)
        {
            var page = result.Pages[i];
            var texture = new Texture2D(graphicsDevice, page.Width, page.Height, false, SurfaceFormat.Color);
            texture.SetData(page.PixelData);
            textures[i] = texture;
        }

        return new BitmapFont(textures, result.FntText);
    }
}
