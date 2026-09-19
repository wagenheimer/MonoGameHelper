using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Wagenheimer.MonoGameHelper.Graphics;

/// <summary>
/// Utility for rendering ultrawide or adaptive backgrounds in fixed-height or dynamic aspect ratio viewports.
/// Guarantees that the viewport is fully covered without black bars or texture distortion.
/// </summary>
public static class FitHeightBackground
{
    /// <summary>
    /// Computes the destination rectangle to draw a background texture scaled to match the viewport height,
    /// horizontally centered.
    /// </summary>
    /// <param name="textureWidth">Width of the source texture in pixels.</param>
    /// <param name="textureHeight">Height of the source texture in pixels.</param>
    /// <param name="virtualWidth">Target virtual viewport width.</param>
    /// <param name="virtualHeight">Target virtual viewport height.</param>
    /// <returns>The calculated destination rectangle.</returns>
    public static Rectangle CalculateFitHeight(int textureWidth, int textureHeight, int virtualWidth, int virtualHeight)
    {
        if (textureHeight <= 0 || textureWidth <= 0 || virtualHeight <= 0)
            return new Rectangle(0, 0, virtualWidth, virtualHeight);

        float scale = (float)virtualHeight / textureHeight;
        int scaledWidth = (int)Math.Round(textureWidth * scale);

        // If the scaled width is smaller than virtualWidth (e.g. on super ultrawide monitors),
        // scale by width instead to prevent black pillarbox borders.
        if (scaledWidth < virtualWidth)
        {
            scale = (float)virtualWidth / textureWidth;
            scaledWidth = virtualWidth;
            int scaledHeight = (int)Math.Round(textureHeight * scale);
            int yOffset = (virtualHeight - scaledHeight) / 2;
            return new Rectangle(0, yOffset, scaledWidth, scaledHeight);
        }

        int xOffset = (virtualWidth - scaledWidth) / 2;
        return new Rectangle(xOffset, 0, scaledWidth, virtualHeight);
    }

    /// <summary>
    /// Draws the texture scaled and centered to cover the viewport.
    /// </summary>
    /// <param name="spriteBatch">Active SpriteBatch.</param>
    /// <param name="texture">Texture to draw.</param>
    /// <param name="virtualWidth">Target virtual viewport width.</param>
    /// <param name="virtualHeight">Target virtual viewport height.</param>
    /// <param name="tintColor">Optional color tint (default: White).</param>
    public static void Draw(SpriteBatch spriteBatch, Texture2D texture, int virtualWidth, int virtualHeight, Color? tintColor = null)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(texture);

        Rectangle dest = CalculateFitHeight(texture.Width, texture.Height, virtualWidth, virtualHeight);
        spriteBatch.Draw(texture, dest, tintColor ?? Color.White);
    }
}
