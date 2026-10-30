using Microsoft.Xna.Framework;
namespace Wagenheimer.MonoGameHelper.Particles;

/// <summary>Bounded, renderer-neutral expanding impulses. Units belong to the caller.
/// No GPU objects; update with the gameplay clock. Newest replaces oldest at capacity.</summary>
public sealed class RadialImpulsePool
{
    public readonly record struct Impulse(Vector2 Center,float Radius,float Amplitude,float Width,float Progress);
    private struct Wave { public Vector2 Center; public float Age,Duration,Radius,Amplitude,Width; }
    private readonly Wave[] _waves;
    private int _next;
    public int ActiveCount {get;private set;}
    public RadialImpulsePool(int capacity=4) { if(capacity<1)throw new ArgumentOutOfRangeException(nameof(capacity));_waves=new Wave[capacity]; }
    public void Trigger(Vector2 center,float radius,float amplitude,float duration=.5f,float width=12)
    {
        if(!float.IsFinite(center.X)||!float.IsFinite(center.Y)||!float.IsFinite(radius)||radius<=0||!float.IsFinite(amplitude)||amplitude<0||!float.IsFinite(duration)||duration<=0||!float.IsFinite(width)||width<=0)throw new ArgumentOutOfRangeException(nameof(radius));
        // Prefer expired slots before replacing a living wave.
        int at=_next;
        for(int i=0;i<_waves.Length;i++){int k=(_next+i)%_waves.Length;if(_waves[k].Duration<=0){at=k;break;}}
        if(_waves[at].Duration<=0)ActiveCount++;
        _waves[at]=new Wave{Center=center,Duration=duration,Radius=radius,Amplitude=amplitude,Width=width};
        _next=(at+1)%_waves.Length;
    }
    public void Update(float dt)
    {
        if(!float.IsFinite(dt)||dt<0)throw new ArgumentOutOfRangeException(nameof(dt));
        for(int i=0;i<_waves.Length;i++)
        {
            ref var w=ref _waves[i];if(w.Duration<=0)continue;w.Age+=dt;
            if(w.Age>=w.Duration){w=default;ActiveCount--;}
        }
    }
    public int CopyTo(Span<Impulse> destination)
    {
        int count=0;
        foreach(var w in _waves)
        {
            if(w.Duration<=0)continue;if(count==destination.Length)break;
            float t=w.Age/w.Duration;
            destination[count++]=new(w.Center,w.Radius*(1-MathF.Pow(1-t,2)),w.Amplitude*(1-t)*(1-t),w.Width,t);
        }
        return count;
    }
    public void Translate(Vector2 delta)
    {
        if(!float.IsFinite(delta.X)||!float.IsFinite(delta.Y))throw new ArgumentOutOfRangeException(nameof(delta));
        for(int i=0;i<_waves.Length;i++)if(_waves[i].Duration>0)_waves[i].Center+=delta;
    }
    public void Clear(){Array.Clear(_waves);ActiveCount=0;_next=0;}
}
