using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Wagenheimer.MonoGameHelper.Layout;

/// <summary>
/// Resolution-independent layout: maps a **design space** (the resolution the game was authored
/// for) onto the **native pixels** of the current back buffer.
///
/// ## Why
///
/// Rendering the whole scene into a reduced RenderTarget and stretching it up blurs everything.
/// The recommended approach is to render at **native resolution** and bridge the gap with a
/// **uniform scale factor** — so the game keeps authoring coordinates in design units while the
/// GPU rasterizes at full quality.
///
/// <code>
/// scale           = BackBufferHeight / DesignHeight
/// visible width   = BackBufferWidth  / scale      (design units, dynamic)
/// visible height  = DesignHeight                  (always)
/// </code>
///
/// This is intentionally free of any engine dependency: it works with raw MonoGame, Nez, or
/// anything else. Configure it once and call <see cref="Update(GraphicsDevice)"/> whenever the
/// window may have changed size (or every frame — it is cheap).
///
/// ## Usage
///
/// <code>
/// DesignViewport.Configure(1430, 768);          // once, at startup
/// DesignViewport.Update(GraphicsDevice);        // every frame (or on resize)
///
/// camera.RawZoom = DesignViewport.Scale;        // world camera
/// var pos = DesignViewport.ToScreen(designPos); // design -> pixels
/// </code>
/// </summary>
public static class DesignViewport
{
    /// <summary>Width of the original design (used only as reference/aspect hint).</summary>
    public static int DesignWidthReference { get; private set; } = 1920;

    /// <summary>Height of the design space. This is the fixed axis of the layout.</summary>
    public static int DesignHeight { get; private set; } = 1080;

    /// <summary>Width of the back buffer in real pixels.</summary>
    public static int PixelWidth { get; private set; } = 1920;

    /// <summary>Height of the back buffer in real pixels.</summary>
    public static int PixelHeight { get; private set; } = 1080;

    /// <summary>True once <see cref="Configure"/> has been called.</summary>
    public static bool IsConfigured { get; private set; }

    /// <summary>
    /// Raises the configuration/canvas change so consumers (UI systems, cameras) can react.
    /// Useful for syncing an external UI camera without polling.
    /// </summary>
    public static event Action<float>? ScaleChanged;

    /// <summary>Sets the design resolution. Call once at startup.</summary>
    /// <param name="designWidth">Reference width from the original design (e.g. 1430).</param>
    /// <param name="designHeight">Fixed height of the design space (e.g. 768).</param>
    public static void Configure(int designWidth, int designHeight)
    {
        if (designWidth <= 0) throw new ArgumentOutOfRangeException(nameof(designWidth));
        if (designHeight <= 0) throw new ArgumentOutOfRangeException(nameof(designHeight));

        DesignWidthReference = designWidth;
        DesignHeight = designHeight;
        IsConfigured = true;
    }

    /// <summary>Updates the known back buffer size from a <see cref="GraphicsDevice"/>.</summary>
    public static void Update(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        Update(graphicsDevice.PresentationParameters.BackBufferWidth,
               graphicsDevice.PresentationParameters.BackBufferHeight);
    }

    /// <summary>
    /// Updates the known back buffer size. Call on resize (or every frame).
    /// Raises <see cref="ScaleChanged"/> only when the resulting scale actually changed.
    /// </summary>
    public static void Update(int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
            return;

        float previousScale = PixelHeight > 0 && DesignHeight > 0 ? Scale : float.NaN;

        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;

        if (!IsConfigured)
            return;

        float currentScale = Scale;
        if (float.IsNaN(previousScale) || Math.Abs(currentScale - previousScale) > 0.000001f)
            ScaleChanged?.Invoke(currentScale);
    }

    /// <summary>Uniform factor that converts design units into back buffer pixels.</summary>
    public static float Scale => DesignHeight <= 0 ? 1f : PixelHeight / (float)DesignHeight;

    /// <summary>
    /// Visible width in design units. Dynamic: grows on wider windows (ultrawide) and shrinks on
    /// narrower ones, matching a "fit height" policy without black bars.
    /// </summary>
    public static float DesignWidth => PixelWidth <= 0 ? DesignWidthReference : PixelWidth / Scale;

    /// <summary>Centre of the visible area, in design units.</summary>
    public static Vector2 Center => new(DesignWidth * 0.5f, DesignHeight * 0.5f);

    /// <summary>Size of the visible area, in design units.</summary>
    public static Vector2 DesignSize => new(DesignWidth, DesignHeight);

    /// <summary>Converts a point from design units to back buffer pixels.</summary>
    public static Vector2 ToScreen(Vector2 designPoint) => designPoint * Scale;

    /// <summary>Converts a point from back buffer pixels to design units.</summary>
    public static Vector2 ToDesign(Vector2 screenPoint) => screenPoint / Scale;

    /// <summary>Converts a point from back buffer pixels to design units.</summary>
    public static Vector2 ToDesign(Point screenPoint) => new(screenPoint.X / Scale, screenPoint.Y / Scale);
}
