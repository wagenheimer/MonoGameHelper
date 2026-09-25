using Microsoft.Xna.Framework;
using Wagenheimer.MonoGameHelper.Animation;
using Xunit;

namespace Wagenheimer.MonoGameHelper.Tests;

public class AnimationTests
{
    [Fact]
    public void BezierFlightPath_StartsAtOriginMovesImmediatelyAndEndsAtTarget()
    {
        var start = new Vector2(100f, 400f);
        var end = new Vector2(1200f, 180f);
        var path = BezierFlightPath.Create(start, end, lateralOffset: 90f, arcHeight: 120f);

        Assert.Equal(start, path.PointAt(0f));
        Assert.NotEqual(start, path.PointAt(0.02f));
        Assert.Equal(end, path.PointAt(1f));
        Assert.True(path.VelocityAt(0.5f).LengthSquared() > 0f);
    }

    [Fact]
    public void BezierFlightPath_RotationStartsAndEndsAtZero()
    {
        var path = BezierFlightPath.Create(
            new Vector2(100f, 400f),
            new Vector2(1200f, 180f),
            lateralOffset: 90f,
            arcHeight: 120f);

        Assert.Equal(0f, path.RotationAt(0f, 0.2f), 5);
        Assert.Equal(0f, path.RotationAt(1f, 0.2f), 5);
        Assert.NotEqual(0f, path.RotationAt(0.5f, 0.2f));
    }

    [Fact]
    public void BezierFlightPath_HandlesCoincidentPointsWithoutNaN()
    {
        var point = new Vector2(50f, 60f);
        var path = BezierFlightPath.Create(point, point, lateralOffset: 90f, arcHeight: 120f);

        Assert.Equal(point, path.PointAt(0f));
        Assert.Equal(point, path.PointAt(1f));
        Assert.Equal(Vector2.Zero, path.VelocityAt(0.5f));
    }
}
