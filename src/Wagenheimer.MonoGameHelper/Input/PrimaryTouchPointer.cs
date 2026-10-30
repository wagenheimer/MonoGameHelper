using Microsoft.Xna.Framework;
namespace Wagenheimer.MonoGameHelper.Input;
/// <summary>A sampled physical touch; position is in the coordinate space chosen by the caller.</summary>
public readonly record struct TouchContact(int Id, Vector2 Position, bool Down);
/// <summary>Owns one finger until release. Other fingers cannot steal a captured gesture.</summary>
public sealed class PrimaryTouchPointer
{
 public Vector2 Position { get; private set; }
 public bool IsDown { get; private set; }
 public bool JustPressed { get; private set; }
 public bool JustReleased { get; private set; }
 private int? _owner;
 private bool _waitForClear;
 /// <summary>Sample once per frame, including frames with no contacts.</summary>
 public void Update(ReadOnlySpan<TouchContact> contacts)
 {
  JustPressed=JustReleased=false;
  if(_owner is int id) {
   foreach(var c in contacts)if(c.Id==id && c.Down) { Position=c.Position;return; }
   foreach(var c in contacts)if(c.Id==id) { Position=c.Position;break; }
   IsDown=false;JustReleased=true;_owner=null;_waitForClear=true;
  }
  bool any=false;foreach(var c in contacts)any|=c.Down;
  if(_waitForClear) { if(!any)_waitForClear=false;return; }
  foreach(var c in contacts)if(c.Down) { _owner=c.Id;Position=c.Position;IsDown=true;JustPressed=true;return; }
 }
}
