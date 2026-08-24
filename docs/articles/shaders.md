# Shaders

DrawnUI ships with a thin, reusable SKSL shader layer on top of SkiaSharp's
`SKRuntimeEffect`. It is designed for **hot-path rendering** — applying custom
fragment shaders every frame at 60+ FPS without generating garbage or churning
GPU handles.

See a [live example](https://drawnui.net/sandbox/effects-shader) running on Blazor WebAssembly.

There are two entry points:

| Type | Namespace | Purpose |
|---|---|---|
| `SkiaShader` | `DrawnUi.Infrastructure` | Lightweight reusable engine. Use directly for manual rendering or as a building block. |
| `SkiaShaderEffect` | `DrawnUi.Draw` | XAML-bindable `IPostRendererEffect` that wraps `SkiaShader`. Attach to any `SkiaControl` via `.FX` or the `Effects` collection. |

## Quick start — effect on a control

```xml
<draw:SkiaImage Source="photo.jpg"
                Aspect="AspectCover"
                UseCache="ImageComposite">
    <draw:SkiaImage.VisualEffects>
        <draw:SkiaShaderEffect ShaderSource="Shaders/ripples.sksl" />
    </draw:SkiaImage.VisualEffects>
</draw:SkiaImage>
```

The shader receives the control's rendered output as `iImage1` and draws the
result back onto the canvas. No code-behind required.

## Standard uniforms

`SkiaShader` provides the Shadertoy-style uniform set out of the box. Declare
**all of them** in your `.sksl` file, even the ones you never read:

```glsl
uniform shader iImage1;          // input texture (the control's output)
uniform float2 iResolution;      // viewport size in pixels
uniform float2 iImageResolution; // texture size in pixels
uniform float  iTime;            // elapsed seconds
uniform float2 iOffset;          // top-left of the draw rect
uniform float4 iMouse;           // xy = current, zw = down position
```

You do not need to populate them manually — the base class writes every one of
them on every frame. That is also why they are all mandatory: writing a uniform
the compiled shader does not declare throws, and the throw aborts shader
creation, so the whole effect silently stops rendering. An unused declared
uniform costs nothing.

Shaders assembled from a template (`//script-goes-here`) declare them once in
the template, not per snippet.

## Resource loading

Shader files go in `Resources/Raw/Shaders/` (lowercase filenames — iOS is
case-sensitive). Load with `ShaderSource="Shaders/myeffect.sksl"`. Compiled
effects are cached by filename, so the same `.sksl` file is only compiled once
per process even if used on many controls.

For inline code use `ShaderCode="..."` instead.

## Code-behind rendering

`SkiaShader` can be used directly inside any control's `Paint` override:

```csharp
private readonly SkiaShader _shader =
    SkiaShader.FromResource("Shaders/noise.sksl");

protected override void Paint(DrawingContext ctx)
{
    base.Paint(ctx);

    _shader.Time = (float)(ctx.Context.FrameTimeNanos * 1e-9);
    _shader.DrawRect(ctx.Context.Canvas, ctx.Destination);
}

protected override void OnDisposing()
{
    _shader.Dispose();
    base.OnDisposing();
}
```

## Custom uniforms — subclass pattern

Subclass either `SkiaShader` (engine-level) or `SkiaShaderEffect` (XAML-level).
Override `CreateUniforms` and set extra keys on the returned instance:

```csharp
public class RippleEffect : SkiaShaderEffect
{
    public float Intensity { get; set; } = 1f;

    // Pre-allocated buffer — see "Performance contract" below
    private readonly float[] _bufCenter = new float[2];
    public SKPoint Center { get; set; } = new(0.5f, 0.5f);

    protected override SKRuntimeEffectUniforms CreateUniforms(SKRect destination)
    {
        var uniforms = base.CreateUniforms(destination);

        uniforms["intensity"] = Intensity;

        _bufCenter[0] = Center.X;
        _bufCenter[1] = Center.Y;
        uniforms["iCenter"] = _bufCenter;

        return uniforms;
    }
}
```

Additional input textures work the same way via `CreateTexturesUniforms` /
`SkiaShader.CreateChildren`:

```csharp
protected override SKRuntimeEffectChildren CreateTexturesUniforms(
    SkiaDrawingContext ctx, SKRect destination, SKShader primaryTexture)
{
    var children = base.CreateTexturesUniforms(ctx, destination, primaryTexture);
    children["iImage2"] = _secondaryTextureShader;
    return children;
}
```

## `UseBackground` modes

`SkiaShaderEffect.UseBackground` controls how the input texture (`iImage1`) is
sourced:

| Mode | Behavior | When to use |
|---|---|---|
| `Always` | Captures a fresh snapshot of the parent every frame (or reuses the parent's cached image if present). | Live effects over animated content. |
| `Once` | Snapshots on first render, freezes, and keeps feeding the same image. Reset with `AquiredBackground = false`. | One-shot transitions, reveal animations. |
| `Never` | Passes no texture. Your shader must not declare `iImage1`. | Generative shaders (noise, gradients, procedural patterns). |

> **Tip:** For `Always` mode, set a cache type on the parent (`ImageComposite`
> works well) so the snapshot path reuses the already-rasterised parent image
> instead of re-snapshotting the canvas every frame.

## Performance contract

Shaders run on the hot path — every frame, on the render thread. `SkiaShader`
is carefully allocation-free in steady state:

- `SKRuntimeEffectUniforms`, `SKRuntimeEffectChildren`, and the texture
  `SKShader` are **cached on the instance** and reused across frames. They are
  rebuilt automatically only when the compiled effect changes, the source
  image handle changes, or sampling options change.
- The only unavoidable per-frame allocation is the final `SKShader` returned
  by `SKRuntimeEffect.ToShader(...)` — SkiaSharp snapshots uniforms at that
  call, so it has to be recreated each frame.
- All standard uniform float arrays (`iResolution`, `iMouse`, `iOffset`, …)
  are stored in pre-allocated `float[]` fields on the base class.

**Rules when subclassing:**

1. **Do not dispose** the object returned from `base.CreateUniforms(...)` or
   `base.CreateChildren(...)`. It is owned by the engine and reused.
2. **Do not return a different instance** — mutate the one returned by `base`
   and return it.
3. **Do not allocate per frame inside `CreateUniforms`.** If you need a
   `float[]` uniform, store it as a field and overwrite its slots each call
   (see `_bufCenter` above).
4. **Do not dispose** the shader returned by
   `SkiaShader.CreateTextureShader(source)` — it is cached per source handle.
5. **Never cache** a layer that hosts a shader effect in `SkiaScroll`,
   `SkiaDrawer`, `SkiaCarousel`, or any layout that virtualizes — follow the
   standard DrawnUI caching rules for dynamic content.
6. **PROHIBITED: Do NOT cache controls with GPU-surface shaders using
   `Operations` or `GPU` cache types.** `Operations` records draw commands into
   an `SKPicture` which cannot replay GPU-surface shader programs. `GPU` cache
   creates its own GPU surface that conflicts with the shader's surface
   requirements. Use `Image`, `ImageDoubleBuffered`, or `ImageComposite`
   instead.
7. **PROHIBITED: Do NOT nest children that use GPU-backed cache types (`GPU`,
   `ImageCompositeGPU`) inside a parent cached with `Operations`** —
   `SKPicture` recording cannot capture GPU-surface output from children.

Breaking these rules turns a 60 FPS render loop into a GC-thrashing one —
every disposed-then-rebuilt uniforms/children pair is a native handle round
trip and a managed allocation.

## Lifecycle and disposal

- `SkiaShader.DisposeCompiled()` tears down the compiled effect **and** its
  cached uniforms/children/texture shader (they are all bound to the effect).
  Call this before recompiling.
- `SkiaShader.Dispose()` releases everything including the owned paint.
- `SkiaShaderEffect.OnDisposing()` disposes the engine automatically. If you
  attach an effect and later remove it, dispose it explicitly:

  ```csharp
  _image.VisualEffects.Remove(_effect);
  _effect.Dispose();
  ```

## Debugging

- Hook `SkiaShaderEffect.OnCompilationError` to surface SKSL compile errors
  instead of letting them throw.
- If the effect renders black, check `IsCompiled` on the engine and verify
  your shader declares `iImage1` when `UseBackground != Never`.
- Line endings in `.sksl` files are normalised automatically via
  `SkiaShader.NormalizeLineEndings` — no need to worry about CRLF/LF mixing.

## Slide transitions — SkiaShaderCarousel

`SkiaShaderCarousel` (`DrawnUi.Controls`) renders slide changes with an SkSL transition
instead of translating slides: an attached `ShaderTransitionEffect` (a
`ShaderDoubleTexturesEffect` adding `progress` and `ratio` uniforms) blends the cached
images of the outgoing and incoming cells.

```xml
<draw:SkiaShaderCarousel
    IsLooped="True"
    LinearSpeedMs="750"
    TransitionShader="Shaders/Transitions/cube.sksl"
    ItemsSource="{Binding Items}">
    <!-- cell template MUST use UseCache="Image" — the effect samples cell caches -->
</draw:SkiaShaderCarousel>
```

The shader is a gl-transitions style `transition(vec2 uv)` function using
`getFromColor` / `getToColor` — any transition from
[gl-transitions](https://github.com/gl-transitions/gl-transitions) ported to SkSL works.
Provide it via:

- `TransitionShader` — path inside Resources/Raw
- `TransitionShaderCode` — raw SkSL string (required on OpenTK/`DRAWNUI_NET`)
- `TransitionTemplate` — replace the built-in adapter template
  (`ShaderTransitionEffect.DefaultTemplate`) when you need custom uniforms or sampling

Behavior notes:

- One gesture moves at most ONE slide; direction comes from velocity or displacement,
  the target from the gesture-origin snap point.
- A swipe during a running transition wraps it up within `InterruptedTransitionMs`
  (default 50) and then plays the next transition.
- Override `CreateTransitionEffect()` to plug a custom `ShaderTransitionEffect`
  subclass (extra uniforms, render-area clipping).

### Letterboxed slides (AspectFit)

When slides letterbox their content (photo viewer), the transition spans the whole
cell — a cube's reflection projects at screen bottom instead of under the photo. Fix
with a custom effect: clip `ctx.Destination` in `Render` to the union of the from/to
images' `DisplayRect`, re-anchor `GetEngine().Offset` to the clipped origin in
`SyncEngineState`, and remap `getFromColor`/`getToColor` sampling into that band via
custom uniforms (cell textures stay full-size). For a single-texture effect the clip
alone is enough — with `AutoCreateInputTexture` the snapshot is taken from the clipped
destination.

## See also

- [Drawing Pipeline](drawing-pipeline.md) — where effects fit in the render loop
- [Fluent C# Extensions](fluent-extensions.md) — attaching effects from code
- [Carousels](controls/carousels.md) — `SkiaShaderCarousel` details and gallery patterns
