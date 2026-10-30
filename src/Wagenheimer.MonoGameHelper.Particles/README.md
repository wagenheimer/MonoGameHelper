# MonoGameHelper.Particles

Optional MonoGame.Extended 6.1.1 adapter. No Nez or Gum dependency.

Create or load an Extended ParticleEffect once, resolve each emitter TextureRegion from your atlas by sprite name, then wrap it in ParticleEffectInstance. Trigger(position) reuses existing buffers. Update(dt) once per frame. Visit(cachedDelegate) submits renderer-neutral ParticleDraw values to your Nez Batcher or an already active SpriteBatch. Premultiplied textures require AlphaBlend; ParticleDraw.Color includes opacity. Apply your camera transform exactly once.

Dispose on scene teardown. The adapter disposes effect/emitter buffers, not the borrowed atlas. Do not mutate emitter capacities or collections after wrapping. Default total capacity budget is 8192; choose smaller scene-specific limits. Saturation follows Extended behavior: emission is limited by available slots. Pause by not updating, resume normally. All calls are single-threaded. UI clipping and scene/PostUiOverlay placement belong to consumer adapters.

## Editable Ember effects

Use `AtlasParticleEffectLoader.Load(stream, resolveSprite, maximumCapacity)` to load a `.ember` file. `resolveSprite` receives the basename without extension and returns a borrowed `Texture2DRegion`. The loader validates the total capacity before native allocation, ignores editor atlas coordinates, binds current atlas regions directly and compensates scale for differences between editor image pixels and runtime region pixels. It never loads loose PNGs. Shared texture names are restored after the Extended serializer reads the effect. Numeric parsing uses invariant culture and restores the caller culture.

Keep `.ember` plus PNG previews in your art workspace. Copy only `.ember` to runtime Content; resolve from your packed atlas. Manual bursts must have AutoTrigger=False. The adapter exposes `Effect` for emitter configuration, but do not change capacities or the emitter collection after construction. Scene transforms/camera and UI clipping remain consumer responsibilities. This is not a GUI editor bundled into the package; install Ember separately. Reopen/preview in the installed Ember version and verify the in-game renderer too. The adapter's visitor currently emits buffer order; a renderer needing a different particle sort order must sort/submit accordingly.

Tests cover saturation/expiry, pause, independent burst positions, invalid deltas and disposal without creating a GPU device. No runtime particle texture generation or per-burst emitter construction.

Validated integration: JoanOrArc match emitter file, atlas source rectangles, native 4K capture. Unit tests do not require a GPU; rendering and atlas repack behavior additionally need a graphics integration gate.


## Burst accents

`Trigger(position, intensity)` accents emission count only (intensity clamped to 0.25..2). Single-particle impacts remain single. Each emitter stays within capacity; original Quantity parameters are restored immediately. Existing particles and effect transforms are not modified. Suitable for caller-defined combo/critical-hit feedback without constructing new emitters. Audio pitch and game combo policy belong to the consumer.


`ParticleVisualPool<TVisual>` presents snapshots through a fixed caller-owned visual list: Begin, Submit, End hides unused slots and counts overflow without allocating visuals. Caller owns clipping, atlas bindings and ordering. For straight-alpha UI renderers use `ParticleDraw.StraightColor`; `Color` targets premultiplied AlphaBlend. Dispose the effect instance when leaving the screen; borrowed atlas textures remain owned by the asset cache.


## Radial shockwaves

`RadialImpulsePool` is bounded CPU state, independent of scene/UI frameworks. Trigger an impulse in your own coordinate units, update with gameplay time, copy into a caller-owned Span, and translate on viewport recentering. Newest replaces oldest when full. `Shaders/RadialShockwave.fx` is the matching pixel shader (four waves per pass, one scene sample); compile it with your MGCB. Bind Resolution in native pixels, Waves as center normalized XY / radius normalized by height / amplitude normalized by height, and Widths normalized by height. Disable the pass with no active waves. Do not feed the destination as its own source. The consumer owns targets, resizing, camera conversion and effect disposal; draw UI after processing if it must remain undistorted. This introduces one native-size intermediate render target in a typical scene pipeline; it never lowers scene resolution.
