using Microsoft.Xna.Framework;
using MonoGame.Extended.Particles;
using MonoGame.Extended.Particles.Data;
using Wagenheimer.MonoGameHelper.Particles;
using Xunit;
public class ParticleInstanceTests
{
    static ParticleEffect Create()
    {
        var effect = new ParticleEffect("test");
        effect.Emitters.Add(new ParticleEmitter(12) { LifeSpan = .2f, Parameters = new ParticleReleaseParameters { Quantity = new ParticleInt32Parameter(8), Speed = new ParticleFloatParameter(0) } });
        return effect;
    }
    [Fact] public void RepeatedBurstsStayBoundedAndExpire()
    {
        using var instance = new ParticleEffectInstance(Create());
        for(int i=0;i<100;i++) instance.Trigger(new Vector2(i, 0));
        Assert.InRange(instance.ActiveParticles, 1, 12);
        instance.Update(1f);
        Assert.Equal(0, instance.ActiveParticles);
    }
    [Fact] public void PauseDoesNotAdvanceAndInvalidDeltaRejected()
    {
        using var instance = new ParticleEffectInstance(Create());
        instance.Trigger(Vector2.Zero);
        instance.Update(0);
        Assert.Equal(8, instance.ActiveParticles);
        Assert.Throws<ArgumentOutOfRangeException>(()=>instance.Update(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(()=>instance.Update(-1));
    }
    [Fact] public void DisposalIsIdempotentAndPreventsReuse()
    {
        var effect=Create(); var instance=new ParticleEffectInstance(effect);
        instance.Dispose(); instance.Dispose();
        Assert.True(effect.IsDisposed);
        Assert.Throws<ObjectDisposedException>(()=>instance.Trigger(Vector2.Zero));
    }
    [Fact] public void RendererReceivesIndependentBurstPositionsWithoutTexture()
    {
        using var instance=new ParticleEffectInstance(Create());
        instance.Trigger(new Vector2(10,20));
        instance.Trigger(new Vector2(30,40));
        var positions=new List<Vector2>();
        instance.Visit(p=>positions.Add(p.Position));
        Assert.Contains(new Vector2(10,20),positions);
        Assert.Contains(new Vector2(30,40),positions);
    }
    [Fact] public void RejectsCapacityAboveBudgetWithoutTakingOwnership()
    {
        using var effect=Create();
        Assert.Throws<ArgumentException>(()=>new ParticleEffectInstance(effect,8));
        Assert.False(effect.IsDisposed);
    }
    [Fact] public void InvalidPositionDoesNotEmit()
    {
        using var instance=new ParticleEffectInstance(Create());
        Assert.Throws<ArgumentOutOfRangeException>(()=>instance.Trigger(new Vector2(float.NaN,0)));
        Assert.Equal(0,instance.ActiveParticles);
    }
    [Fact] public void UpdatingOneInstanceDoesNotAdvanceAnother()
    {
        using var first=new ParticleEffectInstance(Create());
        using var second=new ParticleEffectInstance(Create());
        first.Trigger(Vector2.Zero);second.Trigger(Vector2.Zero);
        first.Update(1);
        Assert.Equal(0,first.ActiveParticles);
        Assert.Equal(8,second.ActiveParticles);
    }
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("999999999")]
    public void EmberBudgetRejectedBeforeTextureResolution(string capacity)
    {
        using var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"<ParticleEffect><Emitters><ParticleEmitter Capacity='{capacity}'/></Emitters></ParticleEffect>"));
        Assert.Throws<InvalidDataException>(()=>AtlasParticleEffectLoader.Load(stream,_=>throw new Exception("Resolver must not run"),64));
    }
    [Fact] public void GlobalScaleAndRotationReachTheRenderer()
    {
        var effect=Create();effect.Scale=new Vector2(2,3);effect.Rotation=.7f;
        effect.Emitters[0].Parameters.Scale=new ParticleVector2Parameter(Vector2.One);
        effect.Emitters[0].Parameters.Rotation=new ParticleFloatParameter(0);
        using var instance=new ParticleEffectInstance(effect);
        instance.Trigger(Vector2.Zero);
        ParticleDraw sample=default;instance.Visit(p=>sample=p);
        Assert.Equal(new Vector2(2,3),sample.Scale);
        Assert.Equal(.7f,sample.Rotation,3);
    }
    [Fact] public void BurstAccentIsBoundedAndDoesNotChangeNextBurst()
    {
        using var instance=new ParticleEffectInstance(Create());
        instance.Trigger(Vector2.Zero,1.5f);
        Assert.Equal(12,instance.ActiveParticles);
        instance.Update(1);
        instance.Trigger(Vector2.Zero);
        Assert.Equal(8,instance.ActiveParticles);
    }
    [Fact] public void StraightColorConvertsPremultipliedSnapshotForUi()
    {
        var draw=new ParticleDraw(null,Rectangle.Empty,Vector2.Zero,new Color(64,32,16,128),0,Vector2.One,0);
        Assert.Equal(new Color(128,64,32,128),draw.StraightColor);
        Assert.Equal(Color.Transparent,(draw with { Color=Color.Transparent }).StraightColor);
    }
    private sealed class Visual { public bool Visible; }
    [Fact] public void VisualPoolCapsAndHidesUnusedSlotsOnNextFrame()
    {
        var visuals=new[]{new Visual(),new Visual()};
        var pool=new ParticleVisualPool<Visual>(visuals,(v,p)=>v.Visible=true,v=>v.Visible=false);
        pool.Begin();pool.Submit(default);pool.Submit(default);pool.Submit(default);pool.End();
        Assert.Equal(1,pool.Dropped);Assert.All(visuals,v=>Assert.True(v.Visible));
        pool.Begin();pool.Submit(default);pool.End();
        Assert.True(visuals[0].Visible);Assert.False(visuals[1].Visible);
    }
}
