using Microsoft.Xna.Framework;
using Wagenheimer.MonoGameHelper.Particles;
using Xunit;
public class RadialImpulseTests
{
 [Fact] public void BoundedReplacementExpiresAndClears()
 {
  var pool=new RadialImpulsePool(2);
  for(int i=0;i<100;i++)pool.Trigger(new Vector2(i,0),200,8);
  Assert.Equal(2,pool.ActiveCount);
  var copy=new RadialImpulsePool.Impulse[2];Assert.Equal(2,pool.CopyTo(copy));
  Assert.All(copy,x=>Assert.InRange(x.Center.X,98,99));
  pool.Translate(new Vector2(100,20));pool.CopyTo(copy);Assert.All(copy,x=>Assert.Equal(20,x.Center.Y));Assert.All(copy,x=>Assert.InRange(x.Center.X,198,199));
  pool.Update(.2f);pool.CopyTo(copy);Assert.All(copy,x=>{Assert.InRange(x.Radius,1,200);Assert.InRange(x.Amplitude,0,8);});
  pool.Update(1);Assert.Equal(0,pool.ActiveCount);
  pool.Trigger(Vector2.Zero,100,5);pool.Clear();Assert.Equal(0,pool.CopyTo(copy));
 }
 [Fact] public void RejectsInvalidAndPauseDoesNotAdvance()
 {
  var pool=new RadialImpulsePool();Assert.Throws<ArgumentOutOfRangeException>(()=>pool.Trigger(Vector2.Zero,0,1));
  Assert.Throws<ArgumentOutOfRangeException>(()=>pool.Update(float.NaN));pool.Trigger(Vector2.Zero,100,5);
  var a=new RadialImpulsePool.Impulse[4];pool.Update(0);pool.CopyTo(a);Assert.Equal(0,a[0].Progress);
 }
}
