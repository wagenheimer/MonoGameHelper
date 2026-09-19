using System.Numerics;
using Wagenheimer.MonoGameHelper.Input;
using Xunit;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Wagenheimer.MonoGameHelper.Tests;

public class VirtualCursorTests
{
    [Fact]
    public void VirtualCursor_InitialPosition_ShouldBeScreenCenter()
    {
        var cursor = new VirtualCursor(1430, 768);
        Assert.Equal(715f, cursor.Position.X);
        Assert.Equal(384f, cursor.Position.Y);
    }

    [Fact]
    public void VirtualCursor_InsideDeadZone_ShouldNotMove()
    {
        var cursor = new VirtualCursor(1430, 768);
        var initialPos = cursor.Position;

        // Stick com magnitude menor que deadzone (0.15f)
        cursor.Update(new XnaVector2(0.1f, 0.05f), false, 0.016f);

        Assert.Equal(initialPos, cursor.Position);
    }

    [Fact]
    public void VirtualCursor_MoveRight_ShouldIncreaseX()
    {
        var cursor = new VirtualCursor(1430, 768);
        var initialX = cursor.Position.X;

        // Stick totalmente para a direita
        cursor.Update(new XnaVector2(1f, 0f), false, 0.1f);

        Assert.True(cursor.Position.X > initialX);
    }

    [Fact]
    public void VirtualCursor_MoveUp_ShouldDecreaseYInScreenSpace()
    {
        var cursor = new VirtualCursor(1430, 768);
        var initialY = cursor.Position.Y;

        // No MonoGame, thumbstick Y > 0 é para cima (o que diminui Y na tela)
        cursor.Update(new XnaVector2(0f, 1f), false, 0.1f);

        Assert.True(cursor.Position.Y < initialY);
    }

    [Fact]
    public void VirtualCursor_Clamp_ShouldNotExceedScreenBounds()
    {
        var cursor = new VirtualCursor(100, 100);
        cursor.SetPosition(new XnaVector2(95, 95));

        // Tenta mover 10 segundos para baixo e direita
        cursor.Update(new XnaVector2(1f, -1f), false, 10f);

        Assert.Equal(100f, cursor.Position.X);
        Assert.Equal(100f, cursor.Position.Y);
    }

    [Fact]
    public void VirtualCursor_ClickSimulation_TracksEdgesCorrectly()
    {
        var cursor = new VirtualCursor(1000, 1000);

        // Frame 1: Botão A pressionado
        cursor.Update(XnaVector2.Zero, actionButtonDown: true, 0.016f);
        Assert.True(cursor.IsLeftDown);
        Assert.True(cursor.IsLeftJustPressed);
        Assert.False(cursor.IsLeftJustReleased);

        // Frame 2: Botão A continua segurado
        cursor.Update(XnaVector2.Zero, actionButtonDown: true, 0.016f);
        Assert.True(cursor.IsLeftDown);
        Assert.False(cursor.IsLeftJustPressed);
        Assert.False(cursor.IsLeftJustReleased);

        // Frame 3: Botão A solto
        cursor.Update(XnaVector2.Zero, actionButtonDown: false, 0.016f);
        Assert.False(cursor.IsLeftDown);
        Assert.False(cursor.IsLeftJustPressed);
        Assert.True(cursor.IsLeftJustReleased);
    }
}
