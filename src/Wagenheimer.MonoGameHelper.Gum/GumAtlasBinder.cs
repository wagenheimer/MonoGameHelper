#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Wagenheimer.MonoGameHelper.Graphics;

namespace Wagenheimer.MonoGameHelper.Gum;

/// <summary>
/// Universal binder that connects Gum UI elements and interactive button components
/// to MonoGame/libGDX texture atlas regions dynamically by name, eliminating manual C# wiring loops.
/// </summary>
public static class GumAtlasBinder
{
    /// <summary>
    /// Applies an atlas region to a Gum SpriteRuntime element.
    /// </summary>
    public static void ApplyRegion(SpriteRuntime sprite, Texture2D texture, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        ArgumentNullException.ThrowIfNull(texture);

        sprite.Texture = texture;
        sprite.TextureAddress = global::Gum.Managers.TextureAddress.Custom;
        sprite.TextureLeft = bounds.X;
        sprite.TextureTop = bounds.Y;
        sprite.TextureWidth = bounds.Width;
        sprite.TextureHeight = bounds.Height;
    }

    /// <summary>
    /// Applies a named region from a <see cref="TextureAtlas"/> to a Gum SpriteRuntime.
    /// </summary>
    public static bool ApplyRegion(SpriteRuntime sprite, TextureAtlas atlas, string regionName)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        ArgumentNullException.ThrowIfNull(atlas);

        if (atlas.Texture != null && atlas.TryGetRegion(regionName, out var region) && region != null)
        {
            ApplyRegion(sprite, atlas.Texture, region.Bounds);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Traverses a Gum element hierarchy and automatically binds all sprites and container buttons
    /// declared in a JSON manifest (e.g. Content/Gum/atlas-bindings.json).
    /// </summary>
    public static void BindFromJson(
        GraphicalUiElement root,
        string jsonManifestPath,
        Func<string, (Texture2D Texture, Rectangle Bounds)?> resolveRegion)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(resolveRegion);

        if (!File.Exists(jsonManifestPath))
            return;

        try
        {
            string json = File.ReadAllText(jsonManifestPath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("elements", out var elements))
                return;

            foreach (var prop in elements.EnumerateObject())
            {
                string elementName = prop.Name;
                string spriteKey = prop.Value.GetString() ?? elementName;

                var resolved = resolveRegion(spriteKey);
                if (resolved == null)
                    continue;

                var (tex, rect) = resolved.Value;

                var target = root.GetGraphicalUiElementByName(elementName);
                if (target == null)
                    continue;

                if (target is SpriteRuntime spriteTarget)
                {
                    ApplyRegion(spriteTarget, tex, rect);
                }
                else if (target.GetGraphicalUiElementByName("Art") is SpriteRuntime art)
                {
                    ApplyRegion(art, tex, rect);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GumAtlasBinder] Erro ao processar manifest de binding '{jsonManifestPath}': {ex.Message}");
        }
    }
}
