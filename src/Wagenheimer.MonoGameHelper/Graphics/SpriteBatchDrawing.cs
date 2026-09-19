using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Wagenheimer.MonoGameHelper.Graphics;

/// <summary>
/// Small, engine-agnostic primitive drawing helpers for <see cref="SpriteBatch"/>.
///
/// The pixel texture is created lazily and owned by this class, so it never depends on a
/// framework-provided "white pixel" that may or may not exist.
///
/// > ⚠️ Prefer <see cref="FillRectangle(SpriteBatch, Rectangle, Color)"/> over SpriteBatch's
/// > "destination rectangle" overload when your batching layer does not support it (some
/// > third-party batchers silently fail to render that path).
/// </summary>
public static class SpriteBatchDrawing
{
    private static Texture2D? _pixel;

    /// <summary>Texture used to paint solid rectangles.</summary>
    public static Texture2D Pixel
    {
        get
        {
            if (_pixel == null || _pixel.IsDisposed)
                throw new InvalidOperationException(
                    "SpriteBatchDrawing has not been initialized. Call SpriteBatchDrawing.Initialize(GraphicsDevice) first.");

            return _pixel;
        }
    }

    /// <summary>Creates the 1x1 white pixel texture. Safe to call more than once.</summary>
    public static void Initialize(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        if (_pixel != null && !_pixel.IsDisposed)
            return;

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    /// <summary>Releases the pixel texture (e.g. on device reset or shutdown).</summary>
    public static void Dispose()
    {
        _pixel?.Dispose();
        _pixel = null;
    }

    /// <summary>Ensures the pixel texture exists for the given device.</summary>
    private static Texture2D ResolvePixel(SpriteBatch spriteBatch)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);

        if (_pixel == null || _pixel.IsDisposed)
            Initialize(spriteBatch.GraphicsDevice);

        return _pixel!;
    }

    /// <summary>Fills a rectangle with a solid color.</summary>
    public static void FillRectangle(SpriteBatch spriteBatch, Rectangle rectangle, Color color)
    {
        var pixel = ResolvePixel(spriteBatch);

        spriteBatch.Draw(
            pixel,
            new Vector2(rectangle.X, rectangle.Y),
            null,
            color,
            0f,
            Vector2.Zero,
            new Vector2(rectangle.Width, rectangle.Height),
            SpriteEffects.None,
            0f);
    }

    /// <summary>Fills a rectangle defined by a position and a size.</summary>
    public static void FillRectangle(SpriteBatch spriteBatch, Vector2 position, Vector2 size, Color color)
        => FillRectangle(spriteBatch, new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), color);

    /// <summary>Draws a hollow rectangle (outline). Thickness is in the current batch units.</summary>
    public static void DrawRectangleOutline(SpriteBatch spriteBatch, Rectangle rectangle, Color color, int thickness = 1)
    {
        if (thickness <= 0)
            return;

        FillRectangle(spriteBatch, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        FillRectangle(spriteBatch, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        FillRectangle(spriteBatch, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        FillRectangle(spriteBatch, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
}
