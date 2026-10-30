using global::Gum.GueDeriving;
using Microsoft.Xna.Framework;
using Xunit;
namespace Wagenheimer.MonoGameHelper.Gum.Tests;
public class GumHintControllerTests
{
 [Fact] public void HoverHintStaysInViewportAndClosesOnReset()
 {
  var panel=new ContainerRuntime { Width=460,Height=188 };var title=new TextRuntime();var body=new TextRuntime();
  var target=new ContainerRuntime { X=1850,Y=1000,Width=70,Height=70 };
  var hints=new GumHintController(panel,title,body);hints.Register("power",target,()=>("Power","Ready"));
  hints.Update(new Vector2(1880,1020),false,false,.4f,1920,1080);
  Assert.True(panel.Visible);Assert.InRange(panel.X,12,1448);Assert.True(panel.Y+panel.Height<=target.Y);hints.Reset();Assert.False(panel.Visible);
 }
 [Fact] public void HeldActionDoesNotActivateOnRelease()
 {
  var panel=new ContainerRuntime { Width=460,Height=188 };var target=new ContainerRuntime { Width=100,Height=100 };
  var hints=new GumHintController(panel,new TextRuntime(),new TextRuntime());hints.Register("power",target,()=>("Power","Ready"));
  hints.Update(new Vector2(50,50),true,true,.01f,1920,1080);hints.Update(new Vector2(50,50),true,true,.6f,1920,1080);hints.Update(new Vector2(50,50),false,true,.01f,1920,1080);Assert.False(hints.HandleClick("power"));
 }
 [Fact] public void TopTargetPlacesHintBelowInsteadOfCoveringControl()
 {
  var panel=new ContainerRuntime {Width=460,Height=188};var target=new ContainerRuntime {X=20,Y=0,Width=100,Height=80};var hints=new GumHintController(panel,new TextRuntime(),new TextRuntime());hints.Register("menu",target,()=>("Menu","Pause"));hints.Update(new Vector2(50,40),false,false,.4f,1920,1080);Assert.True(panel.Y>=target.Y+target.Height);
 }
 [Fact] public void BottomHudHintDoesNotCoverNeighboringStatusLabel()
 {
  var panel=new ContainerRuntime {Width=460,Height=188};var target=new ContainerRuntime {X=400,Y=964,Width=240,Height=90};var hints=new GumHintController(panel,new TextRuntime(),new TextRuntime()) {BottomReservedTop=910};hints.Register("goal",target,()=>("Goal","13/50"));hints.Update(new Vector2(500,990),false,false,.4f,1920,1080);Assert.True(panel.Y+panel.Height<=894);
 }
 [Fact] public void InformationTapShowsHelpInsteadOfAction()
 {
  var hints=new GumHintController(new ContainerRuntime(),new TextRuntime(),new TextRuntime());var target=new ContainerRuntime { Width=100,Height=100 };hints.Register("goal",target,()=>("Goal","12/50"),true);Assert.False(hints.HandleClick("goal"));Assert.Equal("goal",hints.HintTarget);
 }
 [Theory] [InlineData(1485f)] [InlineData(1920f)] [InlineData(2520f)]
 public void ExpandedHintIsSizedBeforePositioningAndRespectsBottomHud(float width)
 {
  var panel=new ContainerRuntime{Width=460,Height=188};var target=new ContainerRuntime{X=width/2-702,Y=939,Width=164,Height=170};
  var hints=new GumHintController(panel,new TextRuntime(),new TextRuntime()){BottomReservedTop=910};
  hints.Register("power",target,()=>{panel.Width=720;panel.Height=720;return("Powers","Four stages");});
  hints.Update(new Vector2(target.X+40,1000),false,false,.4f,width,1080);
  Assert.True(panel.Visible);Assert.InRange(panel.X,12,width-panel.Width-12);Assert.True(panel.Y+panel.Height<=894);
 }
 [Fact] public void SwitchingFromDetailedHintRestoresCompactPresentation()
 {
  var panel=new ContainerRuntime();var power=new ContainerRuntime{X=10,Y=940,Width=100,Height=100};var goal=new ContainerRuntime{X=200,Y=940,Width=100,Height=100};
  var hints=new GumHintController(panel,new TextRuntime(),new TextRuntime()){BottomReservedTop=910};
  hints.Register("power",power,()=>{panel.Width=720;panel.Height=720;return("Power","Stages");});
  hints.Register("goal",goal,()=>{panel.Width=460;panel.Height=188;return("Goal","Progress");});
  hints.Update(new Vector2(50,960),false,false,.4f,1920,1080);Assert.Equal(720,panel.Height);
  hints.Update(new Vector2(230,960),false,false,.4f,1920,1080);Assert.Equal(188,panel.Height);Assert.Equal(460,panel.Width);Assert.True(panel.Y+panel.Height<=894);
 }
}
