using Microsoft.Xna.Framework;
using Wagenheimer.MonoGameHelper.Input;
using Xunit;
public class PointerHintGestureTests
{
 [Fact] public void HoverWaitsThenShowsAndClearsOnLeave() { var g=new PointerHintGesture();g.Update("goal",Vector2.Zero,false,false,.2f);Assert.Null(g.HintTarget);g.Update("goal",Vector2.Zero,false,false,.2f);Assert.Equal("goal",g.HintTarget);g.Update(null,Vector2.Zero,false,false,.01f);Assert.Null(g.HintTarget); }
 [Fact] public void TouchHoldConsumesRelease() { var g=new PointerHintGesture();g.Update("power",Vector2.Zero,true,true,.01f);g.Update("power",Vector2.Zero,true,true,.6f);Assert.Equal("power",g.HintTarget);g.Update("power",Vector2.Zero,false,true,.01f);Assert.True(g.SuppressClick);Assert.Equal("power",g.HintTarget);g.Update(null,Vector2.Zero,true,true,.01f);Assert.Null(g.HintTarget); }
 [Fact] public void ShortTouchDoesNotSuppressClick() { var g=new PointerHintGesture();g.Update("power",Vector2.Zero,true,true,.01f);g.Update("power",Vector2.Zero,false,true,.1f);Assert.False(g.SuppressClick); }
 [Fact] public void DragCancelsHoldAndRelease() { var g=new PointerHintGesture();g.Update("power",Vector2.Zero,true,true,.01f);g.Update("power",new Vector2(40,0),true,true,.6f);Assert.Null(g.HintTarget);g.Update("power",new Vector2(40,0),false,true,.01f);Assert.True(g.SuppressClick); }
 [Fact] public void ChangedTargetCannotActivateOnRelease() { var g=new PointerHintGesture();g.Update("power",Vector2.Zero,true,true,.01f);g.Update("goal",Vector2.Zero,false,true,.1f);Assert.True(g.SuppressClick); }
 [Fact] public void SwitchingDeviceCancelsHeldRelease() { var g=new PointerHintGesture();g.Update("power",Vector2.Zero,true,true,.6f);g.Update("power",Vector2.Zero,false,false,.01f);Assert.True(g.SuppressClick);Assert.Null(g.HintTarget); }
 [Fact] public void MousePressOwnsGestureUntilRelease() { var g=new PointerHintGesture();g.Update("power",Vector2.Zero,true,false,.01f);g.Update(null,new Vector2(100,0),true,false,.01f);Assert.True(g.IsCaptured);g.Update(null,new Vector2(100,0),false,false,.01f);Assert.False(g.IsCaptured); }
 [Fact] public void ReleaseMovementCancelsFastDrag() { var g=new PointerHintGesture();g.Update("power",Vector2.Zero,true,true,.01f);g.Update("power",new Vector2(40,0),false,true,.01f);Assert.True(g.SuppressClick); }
 [Fact] public void ResetReleasesCaptureAndHint() { var g=new PointerHintGesture();g.Update("power",Vector2.Zero,true,true,.6f);g.Reset();Assert.False(g.IsCaptured);Assert.Null(g.HintTarget); }
}
