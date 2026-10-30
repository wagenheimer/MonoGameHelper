using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Gum.GueDeriving;
using Gum.Wireframe;
using Wagenheimer.MonoGameHelper.Input;
namespace Wagenheimer.MonoGameHelper.Gum;
/// <summary>Reusable hints for authored Gum panels. Mouse hover and touch hold share one gesture policy.</summary>
public sealed class GumHintController
{
 private sealed record Target(string Key,GraphicalUiElement Visual,Func<(string Title,string Body)> Content,bool InformationOnly);
 private readonly List<Target> _targets=new();
 private readonly GraphicalUiElement _panel;
 private readonly TextRuntime _title,_body;
 private readonly PointerHintGesture _gesture=new();
 /// <summary>Optional top edge of a reserved bottom HUD; hints for its targets stay above it.</summary>
 public float? BottomReservedTop { get; set; }
 public bool IsCaptured => _gesture.IsCaptured;
 public bool SuppressClick => _gesture.SuppressClick;
 public string? HintTarget => _gesture.HintTarget;
 public GumHintController(GraphicalUiElement panel,TextRuntime title,TextRuntime body) { _panel=panel;_title=title;_body=body;_panel.Visible=false; }
 /// <summary>Register a stable named target. Disabled actions may still expose explanatory help.</summary>
 public void Register(string key,GraphicalUiElement visual,Func<(string Title,string Body)> content,bool informationOnly=false) => _targets.Add(new(key,visual,content,informationOnly));
 public static bool Contains(GraphicalUiElement v,Vector2 p) => v.Visible && p.X>=v.AbsoluteLeft && p.X<v.AbsoluteLeft+v.AbsoluteWidth && p.Y>=v.AbsoluteTop && p.Y<v.AbsoluteTop+v.AbsoluteHeight;
 /// <summary>Call before Forms click dispatch. Coordinates and viewport size are design units.</summary>
 public void Update(Vector2 pointer,bool down,bool touch,float dt,float viewportWidth,float viewportHeight)
 {
  Target? hit=null;
  for(int i=_targets.Count-1;i>=0;i--)if(Contains(_targets[i].Visual,pointer)) { hit=_targets[i];break; }
  _gesture.Update(hit?.Key,pointer,down,touch,dt);
  DrawHint(viewportWidth,viewportHeight);
 }
 /// <summary>Gate action callbacks; information-only targets show help on a short tap too.</summary>
 public bool HandleClick(string key)
 {
  if(_gesture.SuppressClick)return false;
  var target=_targets.Find(t=>t.Key==key);
  if(target?.InformationOnly==true) { _gesture.Show(key);return false; }
  return true;
 }
 private void DrawHint(float viewportWidth,float viewportHeight)
 {
  var target=_targets.Find(t=>t.Key==_gesture.HintTarget);
  _panel.Visible=target!=null;
  if(target==null)return;
  var content=target.Content();_title.Text=content.Title;_body.Text=content.Body;
  _panel.X=Math.Clamp(target.Visual.AbsoluteLeft+target.Visual.AbsoluteWidth/2-_panel.Width/2,12,Math.Max(12,viewportWidth-_panel.Width-12));
  float y=target.Visual.AbsoluteTop-_panel.Height-16;
  if(BottomReservedTop is float bottom && target.Visual.AbsoluteTop>=bottom)y=Math.Min(y,bottom-_panel.Height-16);
  if(y<12)y=target.Visual.AbsoluteTop+target.Visual.AbsoluteHeight+16;
  _panel.Y=Math.Clamp(y,12,Math.Max(12,viewportHeight-_panel.Height-12));
 }
 public bool ContainsHint(Vector2 position) => Contains(_panel,position);
 public void Reset() { _gesture.Reset();_panel.Visible=false; }
}
