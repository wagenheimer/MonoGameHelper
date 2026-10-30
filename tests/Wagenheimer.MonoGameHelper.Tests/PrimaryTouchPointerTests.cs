using Microsoft.Xna.Framework;
using Wagenheimer.MonoGameHelper.Input;
using Xunit;
public class PrimaryTouchPointerTests
{
 [Fact] public void TracksPressMoveAndReleaseOnce() { var p=new PrimaryTouchPointer();p.Update([new(1,new Vector2(10,20),true)]);Assert.True(p.JustPressed);p.Update([new(1,new Vector2(20,30),true)]);Assert.False(p.JustPressed);Assert.Equal(new Vector2(20,30),p.Position);p.Update([]);Assert.True(p.JustReleased);p.Update([]);Assert.False(p.JustReleased); }
 [Fact] public void FinalReleasePositionIsPreserved() { var p=new PrimaryTouchPointer();p.Update([new(1,Vector2.Zero,true)]);p.Update([new(1,new Vector2(80,0),false)]);Assert.Equal(new Vector2(80,0),p.Position);Assert.True(p.JustReleased); }
 [Fact] public void SecondFingerCannotTakeOverUntilAllLifted() { var p=new PrimaryTouchPointer();p.Update([new(1,Vector2.Zero,true),new(2,Vector2.One,true)]);p.Update([new(2,Vector2.One,true)]);Assert.True(p.JustReleased);Assert.False(p.IsDown);p.Update([new(2,Vector2.One,true)]);Assert.False(p.JustPressed);p.Update([]);p.Update([new(2,Vector2.One,true)]);Assert.True(p.JustPressed); }
}
