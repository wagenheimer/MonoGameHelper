using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Particles;

namespace Wagenheimer.MonoGameHelper.Particles;

/// <summary>Renderer-neutral snapshot; scale is relative to the borrowed atlas region.</summary>
public readonly record struct ParticleDraw(Texture2D? Texture, Rectangle Source, Vector2 Position,
    Color Color, float Rotation, Vector2 Scale, float Depth)
{
    /// <summary>Use with straight-alpha UI renderers; Color itself targets AlphaBlend.</summary>
    public Color StraightColor => Color.A==0 ? Microsoft.Xna.Framework.Color.Transparent : new Color(
        Math.Clamp((Color.R*255+Color.A/2)/Color.A,0,255),
        Math.Clamp((Color.G*255+Color.A/2)/Color.A,0,255),
        Math.Clamp((Color.B*255+Color.A/2)/Color.A,0,255),Color.A);
}

/// <summary>
/// Owns an Extended effect and its bounded native buffers, never its atlas textures.
/// Supports code-created or Ember-created effects. Supply resolved atlas regions before use.
/// Single-threaded: update and visit on the graphics thread. No SpriteBatch Begin/End here.
/// </summary>
public sealed class ParticleEffectInstance : IDisposable
{
    private readonly ParticleEffect _effect;
    private bool _disposed;
    private readonly IReadOnlyDictionary<ParticleEmitter,Vector2>? _atlasScale;
    public ParticleEffect Effect => _effect;
    public int ActiveParticles => _effect.ActiveParticles;
    public int Capacity { get; }
    public ParticleEffectInstance(ParticleEffect effect, int maximumCapacity = 8192, IReadOnlyDictionary<ParticleEmitter,Vector2>? atlasScale = null)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if(effect.IsDisposed) throw new ObjectDisposedException(nameof(effect));
        if(maximumCapacity < 1) throw new ArgumentOutOfRangeException(nameof(maximumCapacity));
        long capacity=0;
        foreach(var emitter in effect.Emitters) capacity+=emitter.Capacity;
        if(capacity>maximumCapacity) throw new ArgumentException("Effect exceeds the particle capacity budget.",nameof(effect));
        Capacity=(int)capacity;
        _effect=effect;
        _atlasScale=atlasScale;
    }
    public void Trigger(Vector2 position)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(!float.IsFinite(position.X) || !float.IsFinite(position.Y)) throw new ArgumentOutOfRangeException(nameof(position));
        _effect.Trigger(position);
    }
    /// <summary>Count-only burst accent; existing particles and authored defaults are untouched.</summary>
    public void Trigger(Vector2 position,float intensity)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(!float.IsFinite(intensity) || intensity<=0)throw new ArgumentOutOfRangeException(nameof(intensity));
        if(!float.IsFinite(position.X) || !float.IsFinite(position.Y))throw new ArgumentOutOfRangeException(nameof(position));
        intensity=Math.Clamp(intensity,.25f,2f);
        foreach(var emitter in _effect.Emitters)
        {
            var original=emitter.Parameters.Quantity;
            int count=original.Value;
            try
            {
                // A single impact remains single: accent the surrounding sparks, not a stacked flash.
                emitter.Parameters.Quantity=new MonoGame.Extended.Particles.Data.ParticleInt32Parameter(
                    count<=1?count:Math.Min(emitter.Capacity,Math.Max(1,(int)MathF.Round(count*intensity))));
                emitter.Trigger(position);
            }
            finally { emitter.Parameters.Quantity=original; }
        }
    }
    public void Update(float elapsedSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(!float.IsFinite(elapsedSeconds) || elapsedSeconds<0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if(elapsedSeconds>0) _effect.Update(elapsedSeconds);
    }
    /// <summary>Reuse the visitor delegate. No per-particle managed objects are created.</summary>
    public unsafe void Visit(Action<ParticleDraw> visitor)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        ArgumentNullException.ThrowIfNull(visitor);
        float cosine=MathF.Cos(_effect.Rotation),sine=MathF.Sin(_effect.Rotation);
        foreach(var emitter in _effect.Emitters)
        {
            if(!emitter.Visible) continue;
            var region=emitter.TextureRegion;
            var correction=_atlasScale!=null && _atlasScale.TryGetValue(emitter,out var value)?value:Vector2.One;
            var iterator=emitter.Buffer.Iterator;
            while(iterator.HasNext)
            {
                var particle=iterator.Next();
                var color=HslColor.ToRgb(new HslColor(particle->Color[0],particle->Color[1],particle->Color[2]));
                var anchor=new Vector2(particle->TriggeredPos[0],particle->TriggeredPos[1]);
                var delta=(new Vector2(particle->Position[0],particle->Position[1])-anchor)*_effect.Scale;
                var position=anchor+new Vector2(delta.X*cosine-delta.Y*sine,delta.X*sine+delta.Y*cosine);
                visitor(new ParticleDraw(region?.Texture,region?.Bounds??Rectangle.Empty,
                    position,color*Math.Clamp(particle->Opacity,0,1),
                    particle->Rotation+_effect.Rotation,new Vector2(particle->Scale[0],particle->Scale[1])*correction*_effect.Scale,particle->LayerDepth));
            }
        }
    }
    public void Dispose()
    {
        if(_disposed)return;
        _effect.Dispose();
        _disposed=true;
    }
}
