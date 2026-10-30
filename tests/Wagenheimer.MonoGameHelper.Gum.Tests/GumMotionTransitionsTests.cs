using global::Gum.GueDeriving;
using Microsoft.Xna.Framework;
using Xunit;

namespace Wagenheimer.MonoGameHelper.Gum.Tests;

public class GumMotionTransitionsTests
{
    private static void Finish(GumMotionTransitions motion) { for (int i = 0; i < 60; i++) motion.Update(1f / 60f); }

    [Fact]
    public void ShowPreservesOpacityAndScaleAndRestoresAuthoredPosition()
    {
        var motion = new GumMotionTransitions();
        var form = new ContainerRuntime { X = 11, Y = 20, Width = 500, Height = 200 };
        var hidden = new SpriteRuntime { Parent = form, Alpha = 0 };
        var hover = new SpriteRuntime { Parent = form, Alpha = 80 };
        motion.Show(form, new Vector2(0, 320));
        Assert.Equal(340, form.Y);
        motion.Update(1f / 60f);
        Assert.InRange(form.Y, 21, 339);
        Finish(motion);
        Assert.Equal(11, form.X); Assert.Equal(20, form.Y);
        Assert.Equal(500, form.Width); Assert.Equal(200, form.Height);
        Assert.Equal(0, hidden.Alpha); Assert.Equal(80, hover.Alpha);
        Assert.True(form.Visible); Assert.False(motion.IsAnimating(form));
    }

    [Fact]
    public void HideWaitsForMotionAndCallsCompletionOnce()
    {
        var motion = new GumMotionTransitions(); var form = new ContainerRuntime(); int done = 0;
        motion.Hide(form, new Vector2(0, 320), onCompleted: () => done++);
        motion.Update(0.016f); Assert.Equal(0, done); Assert.True(form.Visible);
        Finish(motion); Assert.Equal(1, done); Assert.False(form.Visible);
        Finish(motion); Assert.Equal(1, done);
    }

    [Fact]
    public void ReversingHideKeepsCurrentPositionAndDropsOldCallback()
    {
        var motion = new GumMotionTransitions(); var form = new ContainerRuntime { Y = 12 }; int oldDone = 0;
        motion.Hide(form, new Vector2(0, 320), onCompleted: () => oldDone++);
        motion.Update(.03f); float current = form.Y;
        motion.Show(form, new Vector2(0, 320)); Assert.Equal(current, form.Y);
        Finish(motion); Assert.Equal(12, form.Y); Assert.Equal(0, oldDone);
    }

    [Fact]
    public void CancelRestoresPositionAndDoesNotInvokeCompletion()
    {
        var motion = new GumMotionTransitions(); var form = new ContainerRuntime { X = 15 }; int done = 0;
        motion.Show(form, new Vector2(-300, 0), onCompleted: () => done++);
        motion.Cancel(form); Finish(motion);
        Assert.Equal(15, form.X); Assert.Equal(0, done); Assert.Equal(0, motion.ActiveCount);
    }

    [Fact]
    public void StationaryBackdropStaysInPlaceWithoutChangingItsAlpha()
    {
        var motion = new GumMotionTransitions(); var form = new ContainerRuntime { Y = 20 };
        var dim = new SpriteRuntime { Parent = form, Y = 4, Alpha = 170 };
        motion.Show(form, new Vector2(0, 800), stationaryChildren: new[] { dim });
        Assert.Equal(24, form.Y + dim.Y); motion.Update(.02f);
        Assert.Equal(24, form.Y + dim.Y); Assert.Equal(170, dim.Alpha);
        Finish(motion); Assert.Equal(4, dim.Y);
    }

    [Fact]
    public void InitialTextureUploadCannotSkipEntireEntrance()
    {
        var motion = new GumMotionTransitions(); var form = new ContainerRuntime();
        motion.Show(form, new Vector2(0, 320)); motion.Update(2f);
        Assert.True(motion.IsAnimating(form)); Assert.InRange(form.Y, 1, 319);
    }

    [Fact]
    public void ShowCanReopenAFormHiddenExternally()
    {
        var motion = new GumMotionTransitions(); var form = new ContainerRuntime { Y = 10 };
        motion.Show(form, new Vector2(0, 320)); Finish(motion);
        form.Visible = false;
        motion.Show(form, new Vector2(0, 320));
        Assert.True(form.Visible); Finish(motion); Assert.Equal(10, form.Y);
    }

    [Fact]
    public void CompletionCanStartAnotherMotionSafely()
    {
        var motion = new GumMotionTransitions(); var first = new ContainerRuntime(); var next = new ContainerRuntime();
        motion.Hide(first, new Vector2(0, 320), onCompleted: () => motion.Show(next, new Vector2(100, 0)));
        Finish(motion); Assert.True(next.Visible); Assert.Equal(0, next.X); Assert.Equal(0, motion.ActiveCount);
    }
}
