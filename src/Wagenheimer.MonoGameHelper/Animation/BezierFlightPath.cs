using Microsoft.Xna.Framework;

namespace Wagenheimer.MonoGameHelper.Animation;

/// <summary>
/// A cubic Bézier path intended for short "fly to target" effects (objective pickups, reward
/// counters, item coins). It exposes the interpolated point, tangent velocity and a rotation
/// envelope that keeps the sprite upright at both ends.
/// </summary>
/// <remarks>
/// Pure value type: no per-update allocations and no engine dependency beyond
/// <see cref="Vector2"/>. The arch is defined by a lateral offset and a vertical lift, so the
/// caller can vary the feel without authoring control points by hand.
/// </remarks>
public readonly record struct BezierFlightPath(
    Vector2 Start,
    Vector2 FirstControl,
    Vector2 SecondControl,
    Vector2 End)
{
    /// <summary>
    /// Builds a flight path between <paramref name="start"/> and <paramref name="end"/>.
    /// </summary>
    /// <param name="lateralOffset">
    /// Sideways bow of the curve, in the same units as the points. Positive and negative values
    /// move the curve to opposite sides of the straight line.
    /// </param>
    /// <param name="arcHeight">
    /// Vertical lift of the first control point. Positive values lift the curve against screen Y,
    /// producing a visible arch.
    /// </param>
    public static BezierFlightPath Create(
        Vector2 start,
        Vector2 end,
        float lateralOffset,
        float arcHeight)
    {
        Vector2 delta = end - start;
        if (delta.LengthSquared() < 0.0001f)
            return new BezierFlightPath(start, start, end, end);

        Vector2 perpendicular = new(-delta.Y, delta.X);
        perpendicular = Vector2.Normalize(perpendicular);

        Vector2 firstControl = start
            + delta * 0.22f
            + perpendicular * lateralOffset
            + new Vector2(0f, -arcHeight);
        Vector2 secondControl = end
            - delta * 0.22f
            + perpendicular * lateralOffset * 0.45f;

        return new BezierFlightPath(start, firstControl, secondControl, end);
    }

    /// <summary>
    /// Eases the normalized time so the object starts moving on the first update instead of
    /// waiting at the origin (quadratic ease-out).
    /// </summary>
    public float EasedTime(float time)
    {
        float t = MathHelper.Clamp(time, 0f, 1f);
        return 1f - (1f - t) * (1f - t);
    }

    /// <summary>Point on the curve for a normalized time in the 0..1 range.</summary>
    public Vector2 PointAt(float time)
        => Evaluate(Start, FirstControl, SecondControl, End, EasedTime(time));

    /// <summary>First derivative (tangent) for a normalized time in the 0..1 range.</summary>
    public Vector2 VelocityAt(float time)
        => CubicFirstDerivative(Start, FirstControl, SecondControl, End, EasedTime(time));

    /// <summary>
    /// Rotation aligned with the curve tangent, multiplied by a smooth bell envelope so it is
    /// exactly zero at both ends and never snaps into place.
    /// </summary>
    /// <param name="spin">Extra spin amount (radians) applied at the top of the arc.</param>
    public float RotationAt(float time, float spin)
    {
        float t = MathHelper.Clamp(time, 0f, 1f);
        float envelope = MathF.Sin(MathF.PI * t);
        envelope *= envelope;

        Vector2 velocity = VelocityAt(t);
        float tangent = MathF.Atan2(velocity.Y, velocity.X);
        return (tangent + spin) * envelope;
    }

    /// <summary>Evaluates a cubic Bézier at raw <paramref name="t"/> (no easing).</summary>
    public static Vector2 Evaluate(Vector2 start, Vector2 firstControl, Vector2 secondControl, Vector2 end, float t)
    {
        float clamped = MathHelper.Clamp(t, 0f, 1f);
        float inverse = 1f - clamped;

        return inverse * inverse * inverse * start
            + 3f * inverse * inverse * clamped * firstControl
            + 3f * inverse * clamped * clamped * secondControl
            + clamped * clamped * clamped * end;
    }

    /// <summary>First derivative of a cubic Bézier at raw <paramref name="t"/>.</summary>
    public static Vector2 CubicFirstDerivative(Vector2 start, Vector2 firstControl, Vector2 secondControl, Vector2 end, float t)
    {
        float clamped = MathHelper.Clamp(t, 0f, 1f);
        float inverse = 1f - clamped;

        return 3f * inverse * inverse * (firstControl - start)
            + 6f * inverse * clamped * (secondControl - firstControl)
            + 3f * clamped * clamped * (end - secondControl);
    }
}
