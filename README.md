# Wagenheimer.MonoGameHelper

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![MonoGame](https://img.shields.io/badge/MonoGame-3.8-E73C00.svg)](https://monogame.net/)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey.svg)](#)

A modern, production-ready utility library for **MonoGame** (.NET 8/9/10), designed to provide cross-platform input management, hybrid hardware/virtual mouse cursors, modal dialog stacks, robust audio playback (MP3, OGG, WAV without MGCB friction), adaptive ultrawide background scaling, **native-resolution rendering with design-space layout**, **multi-resolution sprite art**, **drawing above the UI**, **layered sprite rigs with animation curves**, CI screenshot gates, and atomic crash-safe JSON storage.

---

## Features

### 1. Hybrid Cursor & Multi-Input System
Seamlessly switches between input modes without cumbersome boilerplate:
- **Mouse & Keyboard Mode**: Uses native hardware cursor (`MouseCursor.FromTexture2D`) for zero latency and smooth operating-system level responsiveness. The custom cursor can be toggled at runtime with `CursorManager.UseCustomHardwareCursor` (the default OS arrow is used when `false`) — ideal for a "Custom Cursor" game setting.
- **Gamepad Virtual Cursor**: Simulates a virtual mouse cursor powered by the GamePad left thumbstick with customizable speed, acceleration curves, deadzones, and viewport bounding. Button A triggers a simulated left-click, and Button B triggers cancel/back.
- **Touch Input Auto-Detection**: Whenever touch contact is detected via `TouchPanel`, hardware and virtual cursors are automatically hidden, and touch coordinates trigger direct clicks and gestures. When the mouse moves or a gamepad stick is moved, the cursor smoothly reappears.

### 2. Universal Audio Engine (MasterAudio Style)
Play sound effects and background music without depending on external codecs or complex MGCB configuration:
- Built-in pure C# decoders for **MP3** (via `NLayer`), **OGG** (via `NVorbis`), and native **WAV**.
- Centralized audio bus management with `MasterVolume`, `MusicVolume`, and `SfxVolume`.
- Sample-accurate looping and smooth **crossfades** for background music.
- Fire-and-forget or trackable sound instances (`PlaySound`, `PlaySoundAndForget`, `PlayMusic`, `StopMusic`).
- **Streamed music** via `MusicPlayer` (`Song`/`MediaPlayer`) — plays a long track without keeping its whole PCM in RAM; falls back gracefully where streaming isn't supported. Use it for music; use `AudioManager` for SFX.

### 3. Modal Dialog & Escape Stack
Manage stacked UI menus, popups, and confirmation dialogs:
- Register `IModalDialog` instances with `Push`, `Pop`, and `Clear` (plus `Remove` for one dialog).
- Automatically responds to the **Escape** key and GamePad **B** button to close the topmost modal.
- Integrates with `IUiBlocker` to prevent background clicks or gameplay interactions while modals or transitions are active.

### 4. Safe Atomic JSON Storage
Crash-resilient local data persistence for save files, player profiles, and settings:
- Atomic writes: Writes data to a `.tmp` file before replacing the target file, preventing corruption during sudden crashes or power cuts.
- Automatic backups: Creates a `.bak` copy of the previous state on save.
- Failover recovery: If the primary JSON is corrupted or invalid, automatically recovers and deserializes the `.bak` file.
- Both synchronous and asynchronous APIs (`Save`, `SaveAsync`, `Load`, `LoadAsync`).

### 5. Adaptive Ultrawide Background Layout
- `FitHeightBackground`: Dynamically scales backgrounds to fit fixed-height viewports (e.g., 768p, 1080p, 1440p) across any aspect ratio (4:3, 16:9, 16:10, 21:9, 32:9) while keeping the image centered and eliminating letterbox/pillarbox black bars.

### 6. Resolution Independence (Design Viewport)
- `DesignViewport`: Maps a **design space** (the resolution your game was authored for) onto the **native back buffer pixels**, so the game keeps authoring in design units while the GPU rasterizes at full quality.

```csharp
using Wagenheimer.MonoGameHelper.Layout;

// Once at startup
DesignViewport.Configure(1430, 768);
DesignViewport.Update(GraphicsDevice);   // call every frame (or on resize)

camera.RawZoom = DesignViewport.Scale;              // world camera
var pixels = DesignViewport.ToScreen(designPos);    // design units -> pixels
var design = DesignViewport.ToDesign(mousePoint);   // pixels -> design units
```

| Member | Meaning |
| :--- | :--- |
| `Scale` | `BackBufferHeight / DesignHeight` — the uniform factor |
| `DesignWidth` | Visible width in design units (dynamic, ultrawide-friendly) |
| `DesignHeight` | Fixed height of the design space |
| `Center` / `DesignSize` | Visible area in design units |
| `ToScreen` / `ToDesign` | Unit conversions |
| `ScaleChanged` | Event raised when the scale changes (window resize) |

> **Why not render into a reduced RenderTarget and stretch it?** Because that blurs the whole image. Rendering natively and bridging the gap with a camera scale keeps text, particles and shaders razor sharp. It also removes letterboxing entirely.

### 7. Multi-Resolution Art (`ArtScale`)
- `ArtScale`: Keeps sprites at the same **on-screen size** no matter how large the source art is.

```csharp
using Wagenheimer.MonoGameHelper.Graphics;

var art = new ArtScale(2f);                          // atlas packed at 2x
entity.Transform.SetScale(art.ToAbsolute());          // neutral size
entity.Transform.SetScale(art.ToAbsolute(1.35f));     // relative punch animation
```

| Member | Meaning |
| :--- | :--- |
| `Scale` | Packing factor: `1` = 1x, `2` = 2x, `4` = 4x |
| `BaseScale` | `1 / Scale` (1x -> 1.0, 2x -> 0.5, 4x -> 0.25) |
| `ToAbsolute(relative)` | Converts an authored-for-1x scale into the absolute one |
| `Unscale(artPixels)` | Converts packed pixels back into design units |

- `AtlasScaleReader`: Reads the `scale:` header of a **libGDX**-style `.atlas` file, so packed atlases carry their own resolution metadata.

```csharp
float scale = AtlasScaleReader.ReadScaleFromFile("Content/Atlases/tokens@2x.atlas"); // 2
```

> ⚠️ **Never** derive a "design size" per sprite (e.g. `cellWidth / sprite.SourceRect.Width`). Atlases routinely hold sprites of many different natural sizes, and such a formula would stretch each one to the cell size and destroy the layout. The correction is **uniform per atlas**.
>
> ⚠️ Scale animations must be **relative** to `BaseScale`. Absolute values (`Vector2.One`, `1.35f`, `0.78f`) reset a sprite to the wrong size as soon as the art resolution changes.

### 8. Draw Above the UI (`PostUiOverlay`)
- `PostUiOverlay` + `IPostUiDrawable` + `PostUiLayers`: A drawing layer that sits **above** the UI. A game object drawn by the world renderer can never appear above the UI, no matter which sorting layer it uses — this is where cursors, screen flashes, floating text and debug markers go.

```csharp
using Wagenheimer.MonoGameHelper.UI;

// Startup
PostUiOverlay.Initialize(GraphicsDevice);

public class MyCursor : IPostUiDrawable
{
    public int PostUiLayer => PostUiLayers.Pointer;   // higher = in front
    public bool IsPostUiVisible => true;

    public void DrawPostUi(SpriteBatch batch)
        => SpriteBatchDrawing.FillRectangle(batch, new Rectangle(10, 10, 20, 20), Color.Red);
}

PostUiOverlay.Register(myCursor);        // on creation
PostUiOverlay.Unregister(myCursor);      // on disposal

// Each frame, AFTER the UI has been drawn
PostUiOverlay.Draw();
```

| Layer | Use |
| :--- | :--- |
| `PostUiLayers.WorldEffect` | Full-screen flashes, vignettes |
| `PostUiLayers.FloatingText` | Damage numbers, rewards, labels |
| `PostUiLayers.Pointer` | Cursors — above everything |

Elements draw in **design units** (the batch already carries the `DesignViewport` scale matrix), so world, UI and overlay share one coordinate system.

### 9. Back Buffer Screenshot (CI / fidelity gates)
- `BackBufferScreenshot`: Captures the **final frame** (including UI and overlays) to a PNG. Engine-provided screenshot helpers usually capture an intermediate render target taken *before* the UI is drawn, so they miss it.

```csharp
// Startup
_screenshot = ScreenshotRequest.FromEnvironment("MYGAME_SCREENSHOT", "MYGAME_SCREENSHOT_FRAME");

// End of Draw(), after every layer
if (_screenshot != null && _screenshot.TryCapture(GraphicsDevice))
    Exit();
```

```bash
$env:MYGAME_SCREENSHOT = "C:\temp\gate.png"
$env:MYGAME_SCREENSHOT_FRAME = "45"
dotnet run
```

### 10. Primitive Drawing (`SpriteBatchDrawing`)
- `SpriteBatchDrawing`: `Pixel` (owned 1x1 white texture), `FillRectangle`, `DrawRectangleOutline`. Never depends on a framework-provided white pixel that may or may not exist.

> ⚠️ Prefer `FillRectangle` over SpriteBatch's "destination rectangle" overload when your batching layer does not support it — some third-party batchers silently fail to render that path.

### 11. Screen Fade (scene transitions)
- `ScreenFade`: Full-screen colour overlay for transitions — fade to black, swap the scene, fade back.

**Where it draws:** between the game scene and the UI, using its own `SpriteBatch`:

```csharp
base.Draw(gameTime);          // 1. game scene
ScreenFade.Instance.Draw();   // 2. curtain — covers the WORLD only
GumUI.Draw();                 // 3. UI stays visible on top
PostUiOverlay.Draw();         // 4. overlays above the UI (e.g. the cursor)
```

That is deliberate: a loading screen (or a HUD) must stay readable while the world is covered, and
the virtual cursor must not disappear behind a transition curtain.

> If you need a blackout that covers **everything** — including the UI and the overlay — draw the
> curtain through `PostUiOverlay` at `PostUiLayers.ScreenFade` instead.

```csharp
using Wagenheimer.MonoGameHelper.UI;

// Once at startup
ScreenFade.Initialize(GraphicsDevice);

// Every frame
ScreenFade.Instance.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

// Transition: cover -> swap -> reveal
ScreenFade.Instance.FadeToBlack(0.25f, () =>
{
    Core.Scene = CreateNextScene();
    ScreenFade.Instance.FadeFromBlack(0.25f);
});
```

| Member | Meaning |
| :--- | :--- |
| `Initialize(GraphicsDevice)` | Creates the internal `SpriteBatch` (call once at startup) |
| `Draw()` | Draws the curtain. Call **after** the scene and **before** the UI |
| `Dispose()` | Releases the internal `SpriteBatch` |
| `FadeToBlack(duration, onComplete)` | Fades to fully opaque (screen covered) |
| `FadeFromBlack(duration, onComplete)` | Fades to fully transparent (screen revealed) |
| `FadeTo(alpha, duration, onComplete)` | Fades to an arbitrary opacity |
| `SetAlpha(alpha)` | Jumps to an opacity without animating |
| `Alpha` / `IsFading` | Current state |
| `Color` | Overlay colour (default black) |

> It is a **singleton** on purpose: only one transition can be in flight, and it must survive scene
> swaps. Because it draws outside `PostUiOverlay`, clearing the overlay between scenes does **not**
> affect the fade.

### 12. Layered Sprite Rigs (`SpriteRig`)

- `SpriteRig`: replays a **hierarchy of sprites** (positions, rotations, draw order) driven by
  **animation curves** — built for composite logos/emblems coming from a sprite-based authoring tool
  (e.g. a Unity prefab of `SpriteRenderer`s driven by `Animator` clips).

A composite logo is not one sprite: it is a tree of sprites where each part can be posed and animated
on its own clock. `SpriteRig` is **data-driven** — a small JSON (or plain C#) describes the nodes and
the curves, and the runtime only evaluates and draws. Nothing is allocated per frame.

```csharp
using Wagenheimer.MonoGameHelper.Graphics;

var rig = SpriteRig.FromJson(File.ReadAllText("Content/Data/Logo/logo_frame.json"));

// Preferred: sprites packed in an ATLAS (one texture, premultiplied by the loader, fewer swaps).
rig.AssignImages(key => new SpriteRigImage(atlasTexture, atlasRects[key]));

// Loose PNGs also work (whole texture) — but see the atlas rule in your project's docs.
rig.AssignTextures(path => Texture2D.FromStream(GraphicsDevice, File.OpenRead(path)));

// per frame
rig.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
rig.Draw(spriteBatch, origin: new Vector2(715, 132), scale: 1f, opacity: 1f);
```

| Member | Meaning |
| :--- | :--- |
| `FromJson(json)` / `Load(stream)` | Parses the rig (throws `InvalidDataException` with a useful message) |
| `Create(nodes, clips)` | Builds a rig in code, no JSON |
| `Nodes` / `Clips` / `Duration` / `DrawOrder` | Structure of the rig (`DrawOrder` = node indices, back to front) |
| `SpritePaths` | Distinct sprite keys, in the order the nodes reference them |
| `AssignImages(key => image)` | **Atlas path**: texture + source rectangle per key |
| `AssignTextures(path => texture)` | Loose-PNG path: whole textures |
| `GetNodeImage(node)` | Image of a node, for custom renderers |
| `Update(deltaSeconds)` | Advances every clip and recomposes the hierarchy |
| `Draw(batch, origin, scale, opacity, showOptional)` | Draws in the rig's own space |
| `TimeScale` / `BaseScale` | Global speed; base scale for multi-resolution art (2x/4x atlases → `1/ArtScale`) |
| `GetClipTime` / `SetClipTime` (by name or index) | Read/seek a clip's clock (sync two layers) |
| `TryGetNodeIndex` / `GetWorldPosition` / `GetWorldRotationZ` / `IsNodeVisible` | Evaluated pose, to anchor other things to a bone or for tests |
| `Reset()` | Restores the rest pose and rewinds the clocks |

#### JSON schema

```json
{
  "nodes": [
    { "name": "root",  "parent": -1, "pos": [0, 0],   "rotZ": 0, "order": 0, "sprite": null,        "optional": false },
    { "name": "leaf",  "parent": 0,  "pos": [10, 0],  "rotZ": 90, "order": 1, "sprite": "leaf.png", "optional": false }
  ],
  "clips": [
    { "name": "idle", "root": 0, "duration": 5.65, "loop": true, "curves": [
      { "node": 1, "channel": "posY", "keys": [[0, 0], [2.5, 12], [5.65, 0]] }
    ]}
  ]
}
```

- `nodes` — **depth-first**, so a node's `parent` always comes before it (which is also what rules out
  cycles). `sprite: null` makes the node a pure pivot ("bone").
- `clips[].root` — informational: where the animator sat in the authoring tool.
- `channels` — `posX`, `posY`, `rotZ` (degrees), `active` (on/off pose).
- `optional` — variant art (special edition, language…). Hidden unless
  `Draw(..., showOptional: true)`. The legacy key `ceOnly` is also accepted.

#### Conventions worth knowing

- The rig works in **its own space**: **Y up** and counter-clockwise Z rotation, like Unity. The flip
  to screen space (Y down, clockwise) happens inside `Draw`, from `origin`.
- Each sprite's pivot is its **centre**.
- Draw order: higher `order` draws later (on top); ties are broken by hierarchy (a child draws after
  its parent — that is what puts a blinking eyelid over the face).
- `posX`/`posY`/`rotZ` interpolate with **smoothstep** (the ease-in/out of Unity's auto tangents),
  while `active` uses a **step** — an eye blink must switch, not fade.

#### Validation (fail early, with a useful message)

`Create`/`FromJson` throw `InvalidDataException` for: no nodes, a parent that does not exist or comes
*after* the child, a curve pointing at a missing node, keys with mismatched `times`/`values` lengths,
an empty key list, non-finite values, a missing/unknown channel, or malformed JSON. Keys are sorted by
time on load, so hand-written JSON out of order still works, and a clip with `duration: 0` derives its
duration from the last key instead of freezing.

#### What it is NOT

It does **not** deform meshes (Unity's *SpriteSkin* / 2D Animation): there are no vertices and no bone
weights, so a sprite is always drawn rigid. If you need deformation, use a skeletal runtime (Spine,
DragonBones) — those have MonoGame runtimes, but they need the animation authored in their own editor.

> The same rig can be drawn from `PostUiOverlay` (above the UI, e.g. a loading screen) or from a
> scene component (behind the UI, e.g. a menu).

### 13. JSON Localization

- `IStringLocalizer` + `JsonStringLocalizer` + `LocalizationManifest`: runtime localization from a flat `key -> value` JSON file per culture, parsed with `System.Text.Json`. There is **no** Content Pipeline / MGCB step — the files are plain data copied raw to the output directory.

```csharp
using System.Globalization;
using Wagenheimer.MonoGameHelper.Localization;

var loc = new JsonStringLocalizer("Content/Localization", defaultCulture: "en");
loc.SetCulture(CultureInfo.CurrentUICulture.Name);   // unknown code -> falls back to "en"

Console.WriteLine(loc["menu.play"]);
Console.WriteLine(loc.Format("hud.score", 42));
// missing keys: loc.MissingKeys  (falls back to the key itself)
```

| Member | Meaning |
| :--- | :--- |
| `this[key]` | Active-culture value, else default-culture value, else the key itself |
| `Format(key, args)` | Resolves then formats with the invariant culture |
| `Culture` / `DefaultCulture` | Active and fallback culture codes (e.g. `"pt-BR"`, `"en"`) |
| `AvailableCultures` | Every culture declared by the manifest (or discovered on disk) |
| `MissingKeys` | Keys unresolved in both cultures — accumulates for QA |
| `SetCulture(code)` | Switches culture at runtime (case-insensitive; unknown -> default) |

#### File format

```
Content/Localization/
  manifest.json
  en.json
  pt-BR.json
```

`en.json` — a flat object of `key -> value`:

```json
{
  "menu.play": "Play",
  "hud.score": "Score: {0}"
}
```

`manifest.json` — the default culture and the language-picker entries:

```json
{
  "default": "en",
  "languages": [
    { "code": "en", "name": "English" },
    { "code": "pt-BR", "name": "Portuguese (Brazil)" }
  ]
}
```

> ⚠️ The `.json` files are **data, not content**: add them with `CopyToOutputDirectory` and **never** to the MGCB. The `manifest.json` is optional — without it the directory is scanned and every `*.json` file name (minus the extension) becomes a culture.

#### Fallback & missing keys

Resolution walks three steps: the **active culture**, then the **default culture**, then **the key itself**. Every key that reaches the third step is recorded in `MissingKeys` (a thread-safe, accumulating set), so a QA pass can dump the untranslated strings.

#### Keeping the JSON in sync with the source (Google Sheets)

The recommended workflow treats a spreadsheet as the **single source of truth** and the committed JSON files as a **snapshot**: translators edit the sheet, a small script (`tools/fetch_localization.ps1`) exports a published CSV and regenerates one `<culture>.json` per language, and that snapshot is committed alongside the code. The game never reads the sheet at runtime — only the JSON — so builds stay offline, deterministic and diff-friendly.

#### Thread safety

The indexer and `Format` read from maps that are swapped by **atomic reference** — there is no lock on the read path. `SetCulture` may be called at runtime (e.g. from a settings menu); culture files are loaded and cached once under a private lock, so disk is only touched the first time a culture is activated.

---

## Installation

The library is not published to NuGet yet. Reference it as a **project** or a **local package**:

```bash
# 1) Clone
git clone https://github.com/wagenheimer/MonoGameHelper.git
```

```xml
<!-- 2) Add a project reference from your game -->
<ItemGroup>
  <ProjectReference Include="..\MonoGameHelper\src\Wagenheimer.MonoGameHelper\Wagenheimer.MonoGameHelper.csproj" />
</ItemGroup>
```

```bash
# or build a local NuGet package and add it
dotnet pack src/Wagenheimer.MonoGameHelper/Wagenheimer.MonoGameHelper.csproj -c Release
dotnet add package Wagenheimer.MonoGameHelper --source ./src/Wagenheimer.MonoGameHelper/bin/Release
```

**Requirements:** .NET 10 SDK, MonoGame 3.8 (DesktopGL). Dependencies: `NLayer` (MP3) and `NVorbis` (OGG).

### 14. Bézier Flight Paths (`BezierFlightPath`)

- `BezierFlightPath`: a pure MonoGame cubic Bézier for short "fly to target" effects (objective
  pickups, reward counters, item coins). It returns the interpolated point, the tangent velocity and
  a rotation envelope that is exactly zero at both ends.

```csharp
using Wagenheimer.MonoGameHelper.Animation;

var path = BezierFlightPath.Create(
    start: worldPos,
    end:   targetPos,
    lateralOffset: 90f,   // sideways bow; sign flips the side
    arcHeight: 120f);     // vertical lift against screen Y

var position = path.PointAt(normalizedTime);          // 0..1
var rotation = path.RotationAt(normalizedTime, 0.2f); // 0 at both ends
var tangent  = path.VelocityAt(normalizedTime);       // for trails / alignment
```

Key properties:

| Member | Meaning |
| :--- | :--- |
| `Create(start, end, lateralOffset, arcHeight)` | Builds the control points from an arch description |
| `PointAt(t)` / `VelocityAt(t)` | Eased cubic position and first derivative |
| `RotationAt(t, spin)` | Tangent rotation multiplied by a `sin(pi*t)^2` envelope |
| `Evaluate(...)` / `CubicFirstDerivative(...)` | Raw static evaluation helpers |

> The value type allocates nothing per update and does not depend on Nez or Gum: it only needs
> `Microsoft.Xna.Framework.Vector2`.

### 15. Pure MonoGame Texture Atlas & Gum UI Synchronization (`TextureAtlas`)

- `TextureAtlas` + `AtlasRegion`: A pure MonoGame parser and container for **libGDX**-style `.atlas` files (produced by Nez's `SpriteAtlasPacker`, TexturePacker, or GDX tools).
- **Zero third-party runtime dependencies**: works in vanilla MonoGame without requiring Nez, Gum, or external native DLLs.
- Includes **headless parsing** support for unit tests and CI pipelines (parses regions without requiring an initialized GPU device).

```csharp
using Wagenheimer.MonoGameHelper.Graphics;

// Load atlas and texture page from disk
using var atlas = TextureAtlas.FromFile(GraphicsDevice, "Content/Atlases/menu.atlas");

// Access regions by sprite name (case-insensitive)
AtlasRegion buttonRegion = atlas["btn_play"];
Rectangle srcRect = buttonRegion.Bounds; // source rect in pixels
Point originalSize = buttonRegion.OriginalSize; // size before whitespace trim

// Multi-resolution art scale
float baseScale = atlas.ArtScale.BaseScale; // 1.0 (1x), 0.5 (2x), 0.25 (4x)

// Draw via standard MonoGame SpriteBatch
spriteBatch.Draw(
    atlas.Texture,
    position: new Vector2(100, 100),
    sourceRectangle: buttonRegion.Bounds,
    color: Color.White,
    rotation: 0f,
    origin: Vector2.Zero,
    scale: atlas.ArtScale.ToAbsolute(),
    effects: SpriteEffects.None,
    layerDepth: 0f);
```

| Member | Meaning |
| :--- | :--- |
| `FromFile(graphicsDevice, atlasPath, imagePath?, premultiplyAlpha?)` | Loads `.atlas` + `.png` from disk (supports automatic alpha premultiplication) |
| `Parse(atlasContent, texture?, defaultScale?)` | Parses atlas text format (can run headlessly without a texture) |
| `Regions` | Read-only dictionary of all parsed sprite regions |
| `this[name]` / `TryGetRegion(name, out region)` | Fast sprite lookup by name (case-insensitive) |
| `ArtScale` | Art packing factor (`1x`, `2x`, `4x`) and scale conversion utilities |
| `Bounds` / `OriginalSize` / `Offset` / `Index` | Region properties matching standard libGDX specification |

---

#### The Texture Atlas & Gum UI Pipeline

When building games with modern UI engines like **Gum** in MonoGame, two conflicting requirements arise:
1. **At authoring time (Gum Tool)**: The visual editor needs sprite coordinates (`TextureLeft`, `TextureTop`, `TextureWidth`, `TextureHeight`) written inside `.gusx`/`.gucx` XML files to display live previews.
2. **At build and runtime**: Sprites must be packed into texture sheets (`.atlas` + `.png`) with bleed extrusion to prevent pixel seams, eliminate draw calls, and support multi-resolution scaling (`1x`, `2x`, `4x`). Manually updating XML coordinates whenever an atlas is re-packed is impractical.

`Wagenheimer.MonoGameHelper` provides a clean, automated 3-tier pipeline located in `tools/`:

```
┌───────────────────────────────────────────────┐
│  1. Source PNGs (tools/AssetsSource/<Atlas>/)  │
└───────────────────────┬───────────────────────┘
                        │
                        ▼ (MSBuild incremental build or tools/pack-atlases.ps1)
┌───────────────────────────────────────────────┐
│     2. Atlas Packing (.atlas + .png)          │
│        - Extrusion bleed & padding >= 2       │
│        - Scale metadata (scale: 1, 2, 4)      │
└───────────────┬───────────────────────────────┘
                │
        ┌───────┴────────────────────────┐
        ▼                                ▼
┌───────────────────────────────┐ ┌───────────────────────────────────────────┐
│ 3a. sync-gum-preview-coords   │ │ 3b. Runtime Binding                       │
│    Injects exact coordinates  │ │    - TextureAtlas (pure MonoGame)         │
│    into Gum .gusx / .gucx     │ │    - GumAtlasBinder (by sprite name)      │
│    (Gum Tool preview works!)  │ │    - Immune to coordinate changes!        │
└───────────────────────────────┘ └───────────────────────────────────────────┘
```

#### 1. Automatic Incremental Build via MSBuild Target

Drop this target into your game's `.csproj`. It runs only when PNGs in `AssetsSource` are modified:

```xml
<!-- Incremental Atlas Packing & Gum Preview Sync on Build -->
<ItemGroup>
  <AtlasSourceFiles Include="$(MSBuildThisFileDirectory)..\..\tools\AssetsSource\**\*.*" />
  <AtlasOutputFiles Include="$(MSBuildThisFileDirectory)Content\Atlases\menu.atlas;$(MSBuildThisFileDirectory)Content\Atlases\game.atlas" />
</ItemGroup>

<Target Name="AutoPackAtlases" BeforeTargets="BeforeBuild" Inputs="@(AtlasSourceFiles)" Outputs="@(AtlasOutputFiles)">
  <Message Importance="high" Text="==> [AutoPackAtlases] Changes detected in AssetsSource. Packing atlases..." />
  <Exec Command="powershell -ExecutionPolicy Bypass -File &quot;$(MSBuildThisFileDirectory)..\..\tools\pack_atlases.ps1&quot;" />
</Target>
```

#### 2. Standalone Utility Scripts (`tools/`)

The repository includes ready-to-use PowerShell scripts:
- `tools/pack-atlases.ps1`: Parameterized script that loops over folders in `AssetsSource`, calls the atlas packer, and invokes the Gum preview synchronizer.
- `tools/sync-gum-preview-coords.ps1`: Reads any generated `.atlas` file and injects the exact pixel coordinates into matching `SourceFile` properties in Gum XML files.

---

### 16. Gum UI Extensions (`Wagenheimer.MonoGameHelper.Gum`)

The companion package **`Wagenheimer.MonoGameHelper.Gum`** provides specialized extensions for games using **Gum** (`Gum.MonoGame` + `KernSmith.MonoGameGum`):
- **Casual & Commercial Grade Font Materials (`GumFontMaterials`)**: Vertical two-color gradients and soft blurred drop shadows baked directly into font textures via FreeType/KernSmith.
- **Dynamic Atlas Binding (`GumAtlasBinder`)**: Binds Gum visual elements and container buttons to texture atlas regions automatically without manual C# assignment loops.

#### Why Font Materials?
Standard Gum text renders as a single flat solid color with an optional hard 1-pixel black outline (`OutlineThickness`). In commercial casual games (such as Unity TextMeshPro SDF materials or classic ZenGL games), titles and HUD numbers always feature **rich vertical gradients** (e.g., top golden yellow `#FFD800` into bottom amber orange `#FF8E00`) and **soft, blurred drop shadows**.

`GumFontMaterials` brings this visual quality to Gum by generating an RGBA `BitmapFont` texture via KernSmith and applying it to `TextRuntime.Typeface`:

```csharp
using Wagenheimer.MonoGameHelper.Gum;

// 1. Once at startup (in Game.Initialize, right after GumUI.Initialize):
GumFontMaterials.SetGraphicsDevice(GraphicsDevice);

// 2. In your scene or screen:
// Apply a built-in designer preset:
GumFontMaterials.Apply(titleLabel, FontMaterial.Gold, "Content/Gum/Fonts/TitleFont.ttf", fontSize: 36);

// Or create custom materials with specific colors and shadow blur:
var royalMaterial = new FontMaterial(
    GradientTop: new Color(255, 230, 100),
    GradientBottom: new Color(200, 130, 20),
    GradientAngle: 90f,
    ShadowOpacity: 0.65f,
    ShadowBlur: 4f,
    ShadowOffsetY: 3);

GumFontMaterials.Apply(scoreLabel, royalMaterial, "Content/Gum/Fonts/UiFont.ttf", fontSize: 24);

// 3. Or apply by element name in a Gum container hierarchy:
GumFontMaterials.ApplyTo(screenContainer, "txtPlayerName", FontMaterial.Silver, fontPath, fontSize: 18);
```

#### Designer Presets Available Out of the Box:
| Preset | Colors (Top ➔ Bottom) | Typical Use |
| :--- | :--- | :--- |
| `FontMaterial.Gold` | `#FFD800` (Yellow) ➔ `#FF8E00` (Amber) | Main titles, coins, primary buttons |
| `FontMaterial.Silver` | `#FFFFFF` (White) ➔ `#B0B0B0` (Silver) | Subtitles, rank labels, descriptions |
| `FontMaterial.Bronze` | `#E6A15C` (Bronze) ➔ `#8C4E1A` (Dark Bronze) | Medieval menus, rustic badges |
| `FontMaterial.Emerald` | `#90FF00` (Lime) ➔ `#3A8F24` (Forest Green) | Level clear, victory messages, success |
| `FontMaterial.Ruby` | `#FF4B4B` (Coral) ➔ `#9E0000` (Crimson) | Danger, warnings, loss banners |
| `FontMaterial.Amber` | `#FFB900` (Gold) ➔ `#F76A07` (Deep Orange) | Energy, objective counters |
| `FontMaterial.Sapphire`| `#00D2FF` (Cyan) ➔ `#0055D4` (Ocean Blue) | Mana, water, cold effects |
| `FontMaterial.Amethyst`| `#E084FF` (Lilac) ➔ `#6818A5` (Deep Purple) | Magic powers, mysterious rewards |
| `FontMaterial.WhiteShadow`| `#FFFFFF` (White) ➔ `#FFFFFF` (White) | Solid white text with a soft shadow (no gradient) |

#### Window Resize Handling:
When the game window is resized, call `GumFontMaterials.ReapplyAll()` to automatically reconstruct active materials at the new scale.

---

#### Automatic Atlas Binding (`GumAtlasBinder`)

Allows binding Gum UI elements directly to a libGDX texture atlas:

```csharp
// Single element binding:
GumAtlasBinder.ApplyRegion(myIconSprite, atlas, "icon_sword");

// Or data-driven binding from a JSON manifest (Content/Gum/atlas-bindings.json):
GumAtlasBinder.BindFromJson(
    screenRoot,
    "Content/Gum/atlas-bindings.json",
    spriteKey => atlas.TryGetRegion(spriteKey, out var r) ? (atlas.Texture, r.Bounds) : null);
```

---

### 17. Game Window Icon (`WindowIcon`)

Applies a Windows `.ico` to the **game window** (title bar, taskbar and Alt-Tab):
- **One call** from `Game.Initialize` — no SDL2 interop and no reflection into the backend.
- Covers what the project's `<ApplicationIcon>` **cannot**: hosting the game in another process (e.g. `dotnet App.dll` under a debugger, where the app manifest — and therefore the exe icon — is ignored) or a backend that never sets a window icon.
- Loads the **small and big** sizes with `LoadImage`, so each surface gets the icon resolution it wants.
- Cross-platform safe: a no-op outside Windows, and every failure path returns `false` (an icon never brings a game down).

```csharp
using Wagenheimer.MonoGameHelper.Graphics;

protected override void Initialize()
{
    base.Initialize(); // the native window now exists

    WindowIcon.Apply(Window.Handle, "Content/Icon.ico");
}
```

> Pair it with `<ApplicationIcon>Content\Icon.ico</ApplicationIcon>` in the `.csproj` for the **executable's** icon (Explorer, shortcuts, and the taskbar when launched from the `.exe`). The helper keeps the loaded `HICON`s alive internally, so it should be called once, after the window exists.

---

## Quick Start

### Hybrid Input & Cursor Setup

```csharp
using Wagenheimer.MonoGameHelper.Input;

public class MyGame : Game
{
    private GraphicsDeviceManager _graphics;

    public MyGame()
    {
        _graphics = new GraphicsDeviceManager(this);
    }

    protected override void Initialize()
    {
        base.Initialize();
        
        // Initialize with your virtual viewport resolution (e.g. 1430 x 768)
        InputHelperManager.Initialize(this, virtualWidth: 1430, virtualHeight: 768);
    }

    protected override void LoadContent()
    {
        // Set custom hardware cursor for mouse mode
        using var cursorStream = File.OpenRead("Content/cursor_hardware.png");
        var cursorTexture = Texture2D.FromStream(GraphicsDevice, cursorStream);
        InputHelperManager.Instance.CursorManager.SetCustomHardwareCursor(cursorTexture, hotspotX: 0, hotspotY: 0);

        // Optional: honour a "Custom Cursor" setting (false = default OS arrow).
        // Takes effect immediately in Mouse mode; GamePad/Touch are unaffected.
        InputHelperManager.Instance.CursorManager.UseCustomHardwareCursor = true;
    }

    protected override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Updates active device (Mouse, GamePad, Touch) and cursor coordinates
        InputHelperManager.Instance.Update(gameTime);

        base.Update(gameTime);
    }
}
```

### Audio Playback

```csharp
using Wagenheimer.MonoGameHelper.Audio;

// Register audio folder
AudioManager.RegisterAudioDirectory("Content/Audio");

// Set bus volumes
AudioManager.MasterVolume = 1.0f;
AudioManager.MusicVolume = 0.8f;
AudioManager.SfxVolume = 1.0f;

// Play background music with a 1.5s fade-in
AudioManager.PlayMusic("farm", fadeDuration: 1.5f);

// Play a sound effect
AudioManager.PlaySound("sfx-click");

// In your main Game.Update loop:
AudioManager.Update(deltaTime);
```

Long music tracks: decode-free **streaming** (no full PCM in RAM). Prefer this for background music;
`AudioManager.PlayMusic` decodes the whole track into a `SoundEffect` (better for short loops / crossfades):

```csharp
// Starts streaming; returns false if the platform doesn't support it (then fall back to AudioManager).
if (!MusicPlayer.Play("menu", "/abs/path/Content/Audio/music/menu.ogg", loop: true, volume: 0.7f))
    AudioManager.PlayMusic("menu", fadeDuration: 1.5f);

MusicPlayer.Volume = 0.7f;   // 0..1
MusicPlayer.Stop();
```

### Safe Storage

```csharp
using Wagenheimer.MonoGameHelper.Storage;

public record PlayerSave(string Name, int Level, int Coins);

// Save atomically with backup
var save = new PlayerSave("Player1", 5, 1200);
SafeJsonStorage.Save("savegame.json", save);

// Load with safe fallback if file is missing or corrupted
var loaded = SafeJsonStorage.Load<PlayerSave>("savegame.json", defaultValue: new PlayerSave("Default", 1, 0));
```

### Localization

```csharp
using System.Globalization;
using Wagenheimer.MonoGameHelper.Localization;

// Load every Content/Localization/<culture>.json (plus the optional manifest.json)
var loc = new JsonStringLocalizer("Content/Localization", defaultCulture: "en");

// Follow the OS language; unknown codes fall back to the default culture
loc.SetCulture(CultureInfo.CurrentUICulture.Name);

var title = loc["menu.title"];              // "Play"
var score = loc.Format("hud.score", 1200);  // "Score: 1200"
```

---

## Target Framework & Dependencies
- **Target**: `.NET 10.0` (compatible with modern MonoGame DesktopGL projects)
- **Dependencies**:
  - `MonoGame.Framework.DesktopGL` (>= 3.8.0)
  - `NLayer` (3.0.0) - MP3 Decoding
  - `NVorbis` (0.10.5) - OGG Decoding

## Repository layout

```
MonoGameHelper/
├─ src/
│  ├─ Wagenheimer.MonoGameHelper/       # Core library (pure MonoGame, zero UI dependencies)
│  └─ Wagenheimer.MonoGameHelper.Gum/   # Gum UI extensions (GumFontMaterials, GumAtlasBinder)
├─ tests/Wagenheimer.MonoGameHelper.Tests/  # xUnit test suite
├─ tools/                               # automation scripts (pack-atlases.ps1, sync-gum-preview-coords.ps1)
├─ README.md
└─ Wagenheimer.MonoGameHelper.slnx
```

## Building & testing

```bash
dotnet build Wagenheimer.MonoGameHelper.slnx
dotnet test
```

## Contributing

Issues and pull requests are welcome. Please keep the library **engine-agnostic** (MonoGame only —
no Nez/Gum/Unity types), add XML docs for public APIs, and cover new behavior with tests.

## License
MIT License. Created by Cezar Wagenheimer / Green Sauce Games.
