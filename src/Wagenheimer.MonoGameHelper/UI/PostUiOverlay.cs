using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Wagenheimer.MonoGameHelper.Graphics;
using Wagenheimer.MonoGameHelper.Layout;

namespace Wagenheimer.MonoGameHelper.UI;

/// <summary>
/// Named ordering for <see cref="PostUiOverlay"/> elements.
///
/// **Higher value = drawn later = closer to the viewer.** (Opposite of the common
/// "lower = in front" convention used by some engines.)
/// </summary>
public static class PostUiLayers
{
    /// <summary>World effects that must pass over the UI (full-screen flashes, vignettes...).</summary>
    public const int WorldEffect = 0;

    /// <summary>Floating texts, damage numbers, reward popups.</summary>
    public const int FloatingText = 100;

    /// <summary>Cursors and pointers — above everything else, including the UI.</summary>
    public const int Pointer = 1000;

    /// <summary>
    /// Blackouts that must cover **everything**, including the UI and the pointer (e.g. a hard
    /// cut to black on suspend). Note that <see cref="ScreenFade"/> does **not** use this layer:
    /// it draws between the scene and the UI so a loading screen stays readable underneath.
    /// </summary>
    public const int ScreenFade = 10000;
}

/// <summary>
/// Something that must be drawn **after the UI**, on top of it.
///
/// Use it for cursors, screen flashes, floating text and debug markers. Do **not** use it for
/// regular UI: that belongs to the UI system.
/// </summary>
public interface IPostUiDrawable
{
    /// <summary>Ordering key. Higher draws later (in front). See <see cref="PostUiLayers"/>.</summary>
    int PostUiLayer { get; }

    /// <summary>When false nothing is drawn, but the item stays registered.</summary>
    bool IsPostUiVisible { get; }

    /// <summary>
    /// Draw here. The batch already carries the design-space scale matrix, so draw in the same
    /// **design units** used by the game world (not raw pixels).
    /// </summary>
    void DrawPostUi(SpriteBatch spriteBatch);
}

/// <summary>
/// A drawing layer that sits **above the UI**.
///
/// ## Rendering model
///
/// <code>
/// ┌─ PostUiOverlay ── cursors, episodic effects, floating text (above the UI)
/// ├─ UI ────────────── every interface element (above the game scene)
/// └─ Game scene ────── the world
/// </code>
///
/// A game object drawn by the world renderer can never appear above the UI, no matter which
/// sorting layer it uses. When something must be in front of the UI, register it here.
///
/// ## Coordinates
///
/// Elements draw in **design units**, using <see cref="DesignViewport"/> — the same coordinate
/// system as the world, which keeps world/UI/overlay perfectly aligned. When
/// <see cref="DesignViewport"/> is not configured the scale falls back to 1 (1 design unit = 1 pixel).
///
/// ## Usage
///
/// <code>
/// // Startup
/// PostUiOverlay.Initialize(GraphicsDevice);
///
/// // A component/drawable
/// public class MyCursor : IPostUiDrawable
/// {
///     public int PostUiLayer => PostUiLayers.Pointer;
///     public bool IsPostUiVisible => true;
///     public void DrawPostUi(SpriteBatch batch)
///         => SpriteBatchDrawing.FillRectangle(batch, new Rectangle(10, 10, 20, 20), Color.Red);
/// }
///
/// // Lifecycle
/// PostUiOverlay.Register(myCursor);
/// PostUiOverlay.Unregister(myCursor);
///
/// // Each frame, AFTER the UI has been drawn
/// PostUiOverlay.Draw();
/// </code>
///
/// Always register on creation and unregister on disposal so nothing leaks between scenes.
/// </summary>
public static class PostUiOverlay
{
    private static readonly List<IPostUiDrawable> _items = new();
    private static SpriteBatch? _spriteBatch;
    private static bool _orderDirty;
    private static bool _drawing;

    /// <summary>True once <see cref="Initialize"/> has been called.</summary>
    public static bool IsInitialized => _spriteBatch != null;

    /// <summary>True when at least one drawable is registered.</summary>
    public static bool HasDrawables => _items.Count > 0;

    /// <summary>Creates the internal SpriteBatch. Call once at startup.</summary>
    public static void Initialize(GraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);

        if (_spriteBatch != null)
            return;

        SpriteBatchDrawing.Initialize(graphicsDevice);
        _spriteBatch = new SpriteBatch(graphicsDevice);
    }

    /// <summary>Releases the internal resources.</summary>
    public static void Dispose()
    {
        _spriteBatch?.Dispose();
        _spriteBatch = null;
        _items.Clear();
        _orderDirty = false;
    }

    /// <summary>Registers a drawable. Registering the same instance twice is ignored.</summary>
    public static void Register(IPostUiDrawable? drawable)
    {
        if (drawable == null || _items.Contains(drawable))
            return;

        _items.Add(drawable);
        _orderDirty = true;
    }

    /// <summary>Removes a drawable. Safe to call even if it was never registered.</summary>
    public static void Unregister(IPostUiDrawable? drawable)
    {
        if (drawable == null)
            return;

        if (_items.Remove(drawable))
            _orderDirty = true;
    }

    /// <summary>
    /// Clears every registration. Use as a safety net when starting a new scene, in case some
    /// drawable was discarded without calling <see cref="Unregister"/>.
    /// </summary>
    public static void Clear()
    {
        _items.Clear();
        _orderDirty = false;
    }

    /// <summary>
    /// Draws every registered element. Must be called **after** the UI has been drawn for the
    /// frame (and after the world, obviously).
    /// </summary>
    public static void Draw()
    {
        if (_spriteBatch == null || _items.Count == 0 || _drawing)
            return;

        _drawing = true;
        try
        {
            if (_orderDirty)
            {
                _items.Sort(static (a, b) => a.PostUiLayer.CompareTo(b.PostUiLayer));
                _orderDirty = false;
            }

            // Bridges design units -> back buffer pixels, mirroring how the world is rendered.
            float scale = DesignViewport.Scale;

            _spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                Matrix.CreateScale(scale, scale, 1f));

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (!item.IsPostUiVisible)
                    continue;

                try
                {
                    item.DrawPostUi(_spriteBatch);
                }
                catch (Exception ex)
                {
                    // Um item com defeito não pode derrubar os outros NEM deixar o SpriteBatch
                    // aberto (sem End() nada é enviado e a camada inteira desaparece).
                    Console.WriteLine($"[PostUiOverlay] '{item.GetType().Name}' falhou ao desenhar: {ex}");
                }
            }

            _spriteBatch.End();
        }
        finally
        {
            _drawing = false;
        }
    }
}
