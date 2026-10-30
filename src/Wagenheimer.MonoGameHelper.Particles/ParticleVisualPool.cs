namespace Wagenheimer.MonoGameHelper.Particles;

/// <summary>Projects particle snapshots onto pre-authored UI visuals without allocating
/// controls per frame. The caller owns the visuals, their atlas bindings and clipping.</summary>
public sealed class ParticleVisualPool<TVisual>
{
    private readonly IReadOnlyList<TVisual> _visuals;
    private readonly Action<TVisual,ParticleDraw> _apply;
    private readonly Action<TVisual> _hide;
    private int _used;
    public int Dropped { get; private set; }
    public int Presented => _used;
    public ParticleVisualPool(IReadOnlyList<TVisual> visuals,Action<TVisual,ParticleDraw> apply,Action<TVisual> hide)
    {
        ArgumentNullException.ThrowIfNull(visuals);ArgumentNullException.ThrowIfNull(apply);ArgumentNullException.ThrowIfNull(hide);
        _visuals=visuals;_apply=apply;_hide=hide;
    }
    public void Begin() { _used=0;Dropped=0; }
    public void Submit(ParticleDraw particle)
    {
        if(_used>=_visuals.Count){Dropped++;return;}
        _apply(_visuals[_used++],particle);
    }
    public void End()
    {
        for(int i=_used;i<_visuals.Count;i++)_hide(_visuals[i]);
    }
}
