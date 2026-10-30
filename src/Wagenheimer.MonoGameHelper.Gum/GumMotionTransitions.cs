#nullable enable
using System.Runtime.CompilerServices;
using global::Gum.Wireframe;
using Microsoft.Xna.Framework;

namespace Wagenheimer.MonoGameHelper.Gum;

/// <summary>
/// Reusable position-only show/hide transitions for Gum menus, panels and dialogs.
/// Never changes opacity, dimensions, scale, textures or Forms control states.
/// Call <see cref="Update"/> once per frame on the UI thread.
/// </summary>
public sealed class GumMotionTransitions
{
    private sealed class Layout
    {
        public Vector2 Rest;
        public bool Hidden;
        public readonly Dictionary<GraphicalUiElement, Vector2> Stationary = new();
    }

    private sealed class Motion
    {
        public required GraphicalUiElement Form;
        public required Layout Layout;
        public Vector2 From, To;
        public float Elapsed, Duration;
        public bool Showing;
        public Action? Completed;
    }

    private readonly ConditionalWeakTable<GraphicalUiElement, Layout> _layouts = new();
    private readonly List<Motion> _active = new();

    /// <summary>Number of animations still in progress.</summary>
    public int ActiveCount => _active.Count;

    /// <summary>Whether the given element is currently moving.</summary>
    public bool IsAnimating(GraphicalUiElement? form) => _active.Any(m => ReferenceEquals(m.Form, form));

    /// <summary>
    /// Shows an element from a design-unit offset relative to its authored X/Y. Reversing an
    /// unfinished hide starts at the current position. Repeated show requests are idempotent.
    /// Target X/Y must use pixel-based position units (percentage dimensions are fine).
    /// Optional stationary children must also be direct children using pixel-based position units;
    /// their parent-relative positions are counter-translated (useful for modal backdrops).
    /// </summary>
    public void Show(GraphicalUiElement? form, Vector2 hiddenOffset, float duration = .36f,
        Action? onCompleted = null, IEnumerable<GraphicalUiElement>? stationaryChildren = null)
        => Start(form, hiddenOffset, duration, true, onCompleted, stationaryChildren);

    /// <summary>
    /// Hides an element toward an offset from its authored position. Sets Visible=false and
    /// invokes onCompleted only after motion ends. Repeated hide requests do not restart it.
    /// </summary>
    public void Hide(GraphicalUiElement? form, Vector2 hiddenOffset, float duration = .22f,
        Action? onCompleted = null, IEnumerable<GraphicalUiElement>? stationaryChildren = null)
        => Start(form, hiddenOffset, duration, false, onCompleted, stationaryChildren);

    private void Start(GraphicalUiElement? form, Vector2 offset, float duration, bool showing,
        Action? completed, IEnumerable<GraphicalUiElement>? stationary)
    {
        if (form == null) { completed?.Invoke(); return; }
        if (!float.IsFinite(duration) || duration <= 0f || !float.IsFinite(offset.X) || !float.IsFinite(offset.Y))
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration and offset must be finite; duration must be positive.");
        var previous = _active.Find(m => ReferenceEquals(m.Form, form));
        if (previous != null && previous.Showing == showing) return;
        bool known = _layouts.TryGetValue(form, out var layout);
        if (!known)
        {
            layout = new Layout { Rest = new Vector2(form.X, form.Y) };
            _layouts.Add(form, layout);
        }
        if (stationary != null)
            foreach (var child in stationary)
            {
                if (!ReferenceEquals(child.Parent, form))
                    throw new ArgumentException("Stationary elements must be direct children.", nameof(stationary));
                if (!layout!.Stationary.ContainsKey(child))
                    layout.Stationary[child] = new Vector2(child.X, child.Y);
            }
        if (previous == null && known && layout!.Hidden == !showing && form.Visible == showing)
        {
            completed?.Invoke();
            return;
        }
        _active.RemoveAll(m => ReferenceEquals(m.Form, form));
        if (showing && previous == null) SetPosition(form, layout!, layout!.Rest + offset);
        form.Visible = true;
        _active.Add(new Motion
        {
            Form = form, Layout = layout!, From = new Vector2(form.X, form.Y),
            To = showing ? layout!.Rest : layout!.Rest + offset,
            Duration = duration, Showing = showing, Completed = completed,
        });
    }

    /// <summary>
    /// Cancels without invoking old completion callbacks. Restores authored positions by default
    /// and forgets the cached layout, allowing the caller to change layout before the next show.
    /// Call this before removing an element or unloading its screen.
    /// </summary>
    public void Cancel(GraphicalUiElement? form, bool restorePosition = true)
    {
        if (form == null) return;
        _active.RemoveAll(m => ReferenceEquals(m.Form, form));
        if (_layouts.TryGetValue(form, out var layout) && restorePosition)
            SetPosition(form, layout, layout.Rest);
        _layouts.Remove(form);
    }

    /// <summary>Cancels all active transitions without invoking navigation or removal callbacks.</summary>
    public void Clear()
    {
        foreach (var motion in _active.ToArray()) Cancel(motion.Form);
    }

    /// <summary>
    /// Advances motion, capped at 1/30 second per rendered update so a first-frame texture upload
    /// cannot skip the entire entrance. Completion callbacks run after all active positions update.
    /// </summary>
    public void Update(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        float step = Math.Min(elapsedSeconds, 1f / 30f);
        List<Action>? completed = null;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var motion = _active[i]; motion.Elapsed += step;
            float t = Math.Clamp(motion.Elapsed / motion.Duration, 0f, 1f);
            float eased = motion.Showing ? 1f - MathF.Pow(1f - t, 3f) : t * t * t;
            SetPosition(motion.Form, motion.Layout, Vector2.Lerp(motion.From, motion.To, eased));
            if (t < 1f) continue;
            motion.Form.Visible = motion.Showing;
            motion.Layout.Hidden = !motion.Showing;
            _active.RemoveAt(i);
            if (motion.Completed != null) (completed ??= new()).Add(motion.Completed);
        }
        if (completed != null) foreach (var action in completed) action();
    }

    private static void SetPosition(GraphicalUiElement form, Layout layout, Vector2 position)
    {
        form.X = position.X; form.Y = position.Y;
        Vector2 displacement = position - layout.Rest;
        foreach (var (child, rest) in layout.Stationary)
        {
            child.X = rest.X - displacement.X;
            child.Y = rest.Y - displacement.Y;
        }
    }
}
