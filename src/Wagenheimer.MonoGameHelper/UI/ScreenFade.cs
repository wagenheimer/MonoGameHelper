using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Wagenheimer.MonoGameHelper.Graphics;
using Wagenheimer.MonoGameHelper.Layout;

namespace Wagenheimer.MonoGameHelper.UI;

/// <summary>
/// Full-screen colour overlay used for scene transitions (fade out, swap the scene, fade in).
///
/// ## Where it draws (important)
///
/// It draws **between the game scene and the UI** — not above the UI. That is deliberate and
/// matches how these transitions work in shipped games: the curtain covers the *world*, while the
/// interface (a loading screen, a HUD) stays visible on top of it.
///
/// Call <see cref="Draw"/> right after the scene render and **before** the UI:
///
/// <code>
/// base.Draw(gameTime);      // scene
/// ScreenFade.Instance.Draw();   // curtain (covers the world)
/// GumUI.Draw();             // UI stays on top, as in a loading screen
/// PostUiOverlay.Draw();     // overlays above the UI
/// </code>
///
/// > If it drew above the UI, a fade would hide the loading screen itself.
///
/// ## Usage
///
/// <code>
/// ScreenFade.Initialize(GraphicsDevice);                       // once
/// ScreenFade.Instance.Update(dt);                              // every frame
/// ScreenFade.Instance.FadeToBlack(0.25f, () =>
/// {
///     Core.Scene = nextScene;
///     ScreenFade.Instance.FadeFromBlack(0.25f);
/// });
/// </code>
///
/// It is a **singleton** on purpose: only one transition can be in flight at a time, and it must
/// survive scene swaps.
/// </summary>
public sealed class ScreenFade
{
    private static SpriteBatch? _spriteBatch;

    private float _duration;
    private float _elapsed;
    private float _startAlpha;
    private float _targetAlpha;
    private Action? _onComplete;

    /// <summary>The single shared instance.</summary>
    public static ScreenFade Instance { get; } = new();

    private ScreenFade()
    {
    }

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
    }

    /// <summary>Current opacity: 0 = fully transparent, 1 = fully opaque.</summary>
    public float Alpha { get; private set; }

    /// <summary>True while a fade is in progress.</summary>
    public bool IsFading { get; private set; }

    /// <summary>Overlay colour. Defaults to black.</summary>
    public Color Color { get; set; } = Color.Black;

    /// <summary>Fades to fully opaque (screen covered).</summary>
    public void FadeToBlack(float duration, Action? onComplete = null) => FadeTo(1f, duration, onComplete);

    /// <summary>Fades to fully transparent (screen revealed).</summary>
    public void FadeFromBlack(float duration, Action? onComplete = null) => FadeTo(0f, duration, onComplete);

    /// <summary>Fades to an arbitrary opacity.</summary>
    public void FadeTo(float targetAlpha, float duration, Action? onComplete = null)
    {
        _startAlpha = Alpha;
        _targetAlpha = Math.Clamp(targetAlpha, 0f, 1f);
        _elapsed = 0f;
        _onComplete = onComplete;

        if (duration <= 0f)
        {
            Alpha = _targetAlpha;
            IsFading = false;
            _onComplete?.Invoke();
            _onComplete = null;
            return;
        }

        _duration = duration;
        IsFading = true;
    }

    /// <summary>Jumps straight to an opacity without animating.</summary>
    public void SetAlpha(float alpha)
    {
        Alpha = Math.Clamp(alpha, 0f, 1f);
        IsFading = false;
        _onComplete = null;
    }

    /// <summary>
    /// Advances the fade. If you skip this, the fade never completes.
    /// Use <c>(float)gameTime.ElapsedGameTime.TotalSeconds</c>.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        if (!IsFading)
            return;

        _elapsed += deltaSeconds;

        float t = Math.Clamp(_elapsed / _duration, 0f, 1f);
        // Smoothstep keeps the transition from feeling abrupt.
        float eased = t * t * (3f - 2f * t);
        Alpha = MathHelper.Lerp(_startAlpha, _targetAlpha, eased);

        if (t < 1f)
            return;

        Alpha = _targetAlpha;
        IsFading = false;

        var callback = _onComplete;
        _onComplete = null;
        callback?.Invoke();
    }

    /// <summary>
    /// Draws the curtain. Call **after** the game scene and **before** the UI.
    /// Draws in design units (see <see cref="DesignViewport"/>).
    /// </summary>
    public void Draw()
    {
        if (_spriteBatch == null || Alpha <= 0.001f)
            return;

        var size = DesignViewport.DesignSize;
        var full = new Rectangle(0, 0, (int)Math.Ceiling(size.X), (int)Math.Ceiling(size.Y));

        float scale = DesignViewport.Scale;
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullCounterClockwise,
            null,
            Matrix.CreateScale(scale, scale, 1f));

        SpriteBatchDrawing.FillRectangle(_spriteBatch, full, Color * Alpha);

        _spriteBatch.End();
    }
}
