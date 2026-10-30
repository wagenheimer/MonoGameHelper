using Microsoft.Xna.Framework;
namespace Wagenheimer.MonoGameHelper.Input;
/// <summary>Device-independent hover/hold arbitration. A held or dragged touch consumes its release.</summary>
public sealed class PointerHintGesture
{
 public float HoverDelay { get; set; } = .35f;
 public float HoldDelay { get; set; } = .5f;
 public float MoveTolerance { get; set; } = 12f;
 public string? HintTarget { get; private set; }
 public bool SuppressClick { get; private set; }
 public bool IsCaptured => _down && _pressed != null;
 private bool _down, _cancelled, _held, _lastTouch;
 private string? _pressed, _hover;
 private Vector2 _start;
 private float _elapsed;
 /// <summary>Call once per frame before Forms dispatches clicks. Target is a stable application key.</summary>
 public void Update(string? target, Vector2 position, bool down, bool touch, float dt)
 {
  if(touch!=_lastTouch && _down) { Reset();_lastTouch=touch;_down=down;SuppressClick=true;return; }
  _lastTouch=touch;
  SuppressClick=false;dt=Math.Clamp(dt,0,.1f+Math.Max(HoldDelay,HoverDelay));
  if(touch)
  {
   if(down && !_down) { _pressed=target;_start=position;_elapsed=0;_cancelled=false;_held=false;HintTarget=null; }
   if(down) {
    if(Vector2.DistanceSquared(position,_start)>MoveTolerance*MoveTolerance || target!=_pressed) _cancelled=true;
    _elapsed+=dt;
    if(!_cancelled && _pressed!=null && _elapsed>=HoldDelay) { _held=true;HintTarget=_pressed; }
   }
   if(!down && _down) SuppressClick=_held||_cancelled||target!=_pressed||Vector2.DistanceSquared(position,_start)>MoveTolerance*MoveTolerance;
  }
  else {
   if(down && !_down)_pressed=target;
   if(_hover!=target || down) { _elapsed=0;HintTarget=null; }
   _hover=target;
   if(!down && target!=null) { _elapsed+=dt;if(_elapsed>=HoverDelay)HintTarget=target; }
   if(target==null)HintTarget=null;
  }
  _down=down;
 }
 /// <summary>Show help from an explicit information tap.</summary>
 public void Show(string target) => HintTarget=target;
 /// <summary>Cancel on screen disposal or a modal opening.</summary>
 public void Reset() { _down=false;_pressed=null;_hover=null;_elapsed=0;_held=false;_cancelled=false;HintTarget=null;SuppressClick=false; }
}
