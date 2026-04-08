# DrawnUI Cache System — Complete Reference

## Overview

DrawnUI caches rendered output of controls to avoid re-executing paint logic every frame. The cache system offers multiple strategies ranging from lightweight SKPicture recording to GPU-backed texture surfaces. Each control can independently select its cache type via the `UseCache` property on `SkiaControl`.

---

## Cache Types (`SkiaCacheType` enum)

**File:** `src/Shared/Internals/Enums/SkiaCacheType.cs`

| Type | Backing | Description |
|------|---------|-------------|
| `None` | — | No caching. Control is re-painted every frame. Default for most controls. |
| `Operations` | `SKPicture` | Records draw commands into an `SKPictureRecorder`. Replayed on each frame. Lightweight, no extra bitmap allocation. Best for shapes, SVGs, text. **Cannot capture shadow/blur effects** (those need rasterized output). **PROHIBITED for controls with GPU-surface shaders** — SKPicture cannot replay shader programs that require a live GPU surface. **PROHIBITED as parent cache for children using GPU-backed cache types** (`GPU`, `ImageCompositeGPU`) — SKPicture recording cannot capture GPU-surface output. |
| `OperationsFull` | `SKPicture` | Same as `Operations` but records from the full canvas bounds, not just the control's drawing area. Used internally when double-buffering promotes `None` → `OperationsFull` so the cache can be offset (e.g., during scroll animation). |
| `Image` | `SKSurface` → `SKImage` | Renders into a CPU-backed `SKSurface`, then snapshots to `SKImage`. Works for any size including larger-than-VRAM. Supports opacity, shadows, effects. Good for static controls that are expensive to paint. |
| `ImageDoubleBuffered` | `SKSurface` → `SKImage` × 2 | Same as `Image` but with double-buffering: shows the previous cached frame while the new one renders on a background thread. Prevents visual stutter during updates. Best for animated controls (sliders, lottie, progress bars). |
| `ImageComposite` | `SKSurface` (reused) | Partial redraw: tracks which children changed ("dirty"), erases their regions on the existing cached surface, and repaints only those children. The surface is **not cleared** between frames — old content persists. Best for large scroll content layouts with many mostly-static children (PDF pages, forms, markdown). |
| `ImageCompositeGPU` | `SKSurface` (GPU-backed, reused) | GPU-accelerated version of `ImageComposite`. Same partial-redraw dirty-tracking logic but the backing surface uses the canvas `GRContext`. Combines the partial-redraw benefit of composite with zero CPU→GPU upload cost. Falls back to `ImageComposite` if `!Super.GpuCacheEnabled`. Best for large GPU-accelerated layouts with many mostly-static children. |
| `GPU` | `SKSurface` (GPU-backed) | Creates a surface on the same `GRContext` as the accelerated canvas. Zero CPU→GPU upload cost. **Does NOT apply opacity.** Falls back to `Image` if no hardware acceleration or `Super.GpuCacheEnabled = false`. Best for high-perf game elements. **PROHIBITED for controls with GPU-surface shaders** — the GPU cache surface conflicts with the shader's own surface requirements. |

---

## Key Properties

### On `SkiaControl` (file: `src/Shared/Draw/Base/SkiaControl.Cache.cs`)

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `UseCache` | `SkiaCacheType` | `None` | User-facing desired cache type |
| `UsingCacheType` | `SkiaCacheType` (virtual, computed) | — | Effective cache type after overrides, fallbacks, and global settings |
| `AutoCache` | `bool` | `false` | Lets the control dynamically manage its own (or content's) cache strategy |
| `AllowCaching` | `bool` | `true` | Kill-switch; when `false`, forces `UsingCacheType → None` |
| `RenderObject` | `CachedObject` | `null` | Current front-buffer cache |
| `RenderObjectPrevious` | `CachedObject` | `null` | Back buffer (for double-buffered/composite) |
| `RenderObjectPreparing` | `CachedObject` | `null` | Being built on background thread |
| `RenderObjectNeedsUpdate` | `bool` | `true` | Dirty flag — set by `InvalidateCache()` |
| `ExpandDirtyRegion` | `Thickness` | `Zero` | Enlarges the cached/dirty area to include shadows or content that overflows bounds |

### Global Settings (on `Super` class, file: `src/Shared/Super.cs`)

| Setting | Default | Effect |
|---------|---------|--------|
| `Super.CacheEnabled` | `true` | Global cache kill switch. `false` → all controls render as `None`. |
| `Super.GpuCacheEnabled` | `true` | GPU cache enabled. `false` → `GPU` falls back to `Image`. |
| `Super.Multithreaded` | `false` | Enables double-buffering (background thread render). When on, `UsingCacheType` auto-promotes `None` → `OperationsFull` and downgrades `ImageDoubleBuffered`/`GPU` → `Image`, `ImageComposite` → `Operations`. |
| `Super.OffscreenRenderingAtCanvasLevel` | `false` | Routes background rendering through canvas-level queue instead of per-control tasks. |

---

## `UsingCacheType` Resolution Logic

The virtual `UsingCacheType` getter applies this cascade:

1. `!AllowCaching || !Super.CacheEnabled` → **`None`**
2. `CanUseCacheDoubleBuffering && Super.Multithreaded`:
   - `None` → `OperationsFull` (auto-promote)
   - `ImageDoubleBuffered` or `GPU` → `Image` (downgrade)
   - `ImageComposite` or `ImageCompositeGPU` → `Operations` (downgrade)
3. `GPU` requested but `!Super.GpuCacheEnabled` → **`Image`** (fallback)
4. `ImageCompositeGPU` requested but `!Super.GpuCacheEnabled` → **`ImageComposite`** (fallback)
5. `None` + `CanUseCacheDoubleBuffering` + `Super.Multithreaded` + has Parent → **`Operations`**
6. Otherwise → **`UseCache` as-is**

No derived classes override `UsingCacheType`. Customization is done via `CanUseCacheDoubleBuffering` and `AllowCaching`.

### `IsCacheComposite` / `IsCacheImage` Helper Properties

```
IsCacheComposite = UsingCacheType == ImageComposite || UsingCacheType == ImageCompositeGPU
IsCacheImage     = Image || GPU || ImageComposite || ImageCompositeGPU || ImageDoubleBuffered
```

These are used throughout the code instead of checking individual enum values, so new composite/image types are properly handled.

### `CanUseCacheDoubleBuffering` Overrides

| Control | Value | Reason |
|---------|-------|--------|
| `SkiaControl` (base) | `true` | Default |
| `SkiaCamera` | `false` | Streaming camera feed can't snapshot on background |
| `SkiaBackdrop` | `false` | Cannot create surface snapshots on background thread |

---

## CachedObject (file: `src/Shared/Internals/Models/CachedObject.cs`)

The cached rendering result. Holds **either** an `SKPicture` (Operations) or `SKImage` + `SKSurface` (Image/GPU):

```
CachedObject
├── Type: SkiaCacheType
├── Picture: SKPicture           // for Operations/OperationsFull
├── Image: SKImage               // for Image/GPU/ImageDoubleBuffered/ImageComposite
├── Surface: SKSurface           // backing surface (recycled via pool for CPU)
├── Bounds: SKRect               // control bounds at capture time
├── RecordingArea: SKRect        // canvas area that was recorded
├── PreserveSourceFromDispose    // true when surface is transferred to new CachedObject
├── SurfaceIsRecycled: bool      // surface came from pool
└── Draw(canvas, dest, paint)    // translate and render to canvas
```

Constructors:
- `CachedObject(type, SKPicture, bounds, recordingArea)` — for Operations
- `CachedObject(type, SKSurface, bounds, recordingArea)` — for Image/GPU/ImageCompositeGPU; calls `Image = surface.Snapshot()`

---

## Rendering Flow

### Main Entry: `DrawUsingRenderObject()` (SkiaControl.Cache.cs ~L905)

```
DrawUsingRenderObject()
├── Arrange()
├── if UsingCacheType != None:
│   ├── UseRenderingObject() → try drawing from existing cache
│   │   ├── CheckCachedObjectValid() → validate size, GPU context
│   │   ├── DrawRenderObjectInternal() → draw cache to canvas
│   │   └── if double-buffered && NeedUpdateFrontCache:
│   │       └── PushToOffscreenRendering(CreateRenderingObject)
│   ├── if UseRenderingObject returned false:
│   │   ├── if double-buffered:
│   │   │   └── PushToOffscreenRendering(CreateRenderingObject)
│   │   └── else:
│   │       └── CreateRenderingObjectAndPaint()
│   │           └── CreateRenderingObject() → DrawRenderObjectInternal()
├── else (None):
│   └── DrawDirectInternal() → PaintWithEffects()
└── FinalizeDrawingWithRenderObject()
```

### Cache Factory: `CreateRenderingObject()` (SkiaControl.Cache.cs ~L370)

**Operations / OperationsFull path (SKPicture):**
```csharp
using var recorder = new SKPictureRecorder();
var recordingContext = context.CreateForRecordingOperations(recorder, cacheRecordingArea);
action(recordingContext);            // calls PaintWithEffects
SKPicture picture = recorder.EndRecording();
return new CachedObject(type, picture, destination, cacheRecordingArea);
```

**Image / GPU / ImageComposite / ImageCompositeGPU / ImageDoubleBuffered path (SKSurface → SKImage):**
```csharp
bool isGpu = (usingCacheType == SkiaCacheType.GPU || usingCacheType == SkiaCacheType.ImageCompositeGPU);
SKSurface surface = CreateSurface(width, height, isGpu);
surface.Canvas.Translate(-recordArea.Left, -recordArea.Top);
action(recordingContext);            // calls PaintWithEffects
surface.Canvas.Flush();
return new CachedObject(type, surface, recordArea, recordArea);
// Constructor does: Image = surface.Snapshot();
```

**ImageComposite reuse:** When compositing, the existing `RenderObjectPrevious.Surface` is reused instead of creating a new one. The surface canvas is **not cleared** — only dirty regions are erased then repainted.

---

## ImageComposite: Partial Redraw Flow

1. **Child invalidates** → `UpdateByChild(child)` → `TrackChildAsDirty(child)` adds to `DirtyChildrenTracker` (a `ControlsTracker` backed by `Dictionary<Guid, SkiaControl>`)

2. **SetupRenderingWithComposition()** (on SkiaLayout):
   - Collects dirty children from tracker
   - Adds any children whose `DirtyRegion` intersects a dirty child
   - **Erases** dirty regions on previous surface using `SKBlendMode.Src` with transparent paint
   - Clips background painting to dirty regions only

3. **During child rendering**: `IsRenderingWithComposition` flag gates drawing:
   - Dirty children → full `Render(context)`
   - Clean children → `Arrange()` only (update position, no draw)

4. **Surface reuse**: Previous surface is reused; `PreserveSourceFromDispose = true` prevents the old CachedObject from disposing the transferred surface.

**When to use:** Large scroll content layouts with many mostly-static children. Avoid for layouts where children frequently change size (causes full rebuild via `InvalidateMeasure`).

---

## Double-Buffered Flow

### Three Buffers
- `RenderObject` — front buffer (displayed)
- `RenderObjectPrevious` — back buffer (shown while new one renders)
- `RenderObjectPreparing` — being built on background thread

### PushToOffscreenRendering

Two modes:
1. **Per-control** (default): `Task.Run` per control, `LimitedQueue<Action>(1)` — only latest action survives, older renders discarded.
2. **Canvas-level** (`Super.OffscreenRenderingAtCanvasLevel`): `ConcurrentQueue<OffscreenCommand>` → deduplicated per control via `Dictionary`, processed by single background task.

### Buffer Swap
```
Background thread:
  prepared = CreateRenderingObject(...)   // render to new surface
  RenderObjectPreparing = prepared
  RenderObject = prepared                 // atomic swap under lock(LockDraw)
  Repaint()                               // request frame to show new cache

RenderObject setter:
  lock(LockDraw) {
    old → RenderObjectPrevious (if double-buffered/composite) or DisposeObject(old)
    _renderObject = value
    Monitor.PulseAll(LockDraw)
  }
```

### Thread Safety
- `lock(LockDraw)` protects `RenderObject` read/write
- `SemaphoreSlim` prevents concurrent background renders per control
- `_processingOffscrenRendering` flag prevents duplicate Task.Run

---

## Cache Invalidation

### Methods (SkiaControl.Cache.cs)

| Method | What It Does |
|--------|-------------|
| `InvalidateCache()` | Sets `RenderObjectNeedsUpdate = true`. Checked at next draw. |
| `InvalidateCacheWithPrevious()` | + `RenderObjectPreviousNeedsUpdate = true` (destroys composite back buffer too) |
| `DestroyRenderingObject()` | Sets `RenderObject = null`, triggers disposal chain |

### Triggers

| Trigger | Path |
|---------|------|
| Property change (bindable) | `NeedDraw` callback → `Update()` → `InvalidateCache()` |
| `NeedUpdate = true` | Setter calls `InvalidateCache()` |
| Soft invalidation | `InvalidateInternal()` → `InvalidateCacheWithPrevious()` |
| Full invalidation | `Invalidate()` → `InvalidateInternal()` |
| Size change | `InvalidateMeasure()` → `DestroyRenderingObject()` |
| Child change | `InvalidateCacheWithPrevious()` on parent |

### Cache Validity Check: `CheckCachedObjectValid()`

Returns `false` (triggering rebuild) when:
1. Surface handle is 0 (disposed by OS/GC)
2. Recording area size mismatch vs current area (>1px tolerance)
3. For GPU: `GRContext.Handle` changed (e.g., app background/foreground)

---

## Surface Management

### SurfaceCacheManager (file: `src/Maui/DrawnUi/Views/SurfaceCacheManager.cs`)

Object pool for CPU `SKSurface` instances, keyed by `(width, height)`:

| Setting | Value |
|---------|-------|
| `_minRequestsForPooling` | 1 |
| `_maxPoolSize` | 10 surfaces per size |
| `_maxTrackedSizes` | 100 distinct sizes |

- `GetSurface(w, h)` → pooled or new `SKSurface.Create(...)`
- `ReturnSurface(surface)` → clears canvas, returns to pool; if full, disposes
- **GPU surfaces bypass the pool** — disposed via `DisposableManager`

### CreateSurface Decision Chain

1. Control calls `CreateSurface(width, height, isGpu)`
2. Delegates to `DrawnView.CreateSurface()`
3. If `isGpu` and `SkiaViewAccelerated.GRContext != null`:
   - `SKSurface.Create(GRContext, true, imageInfo)` — GPU-backed
4. Else: `SurfaceCacheManager.GetSurface(w, h)` — CPU, pooled

### DisposableManager (file: `src/Maui/DrawnUi/Views/DisposableManager.cs`)

Frame-delayed disposal. Waits 3 frames before disposing to avoid use-after-free:
```
EnqueueDisposable(disposable, frameNumber)
DisposeDisposables(currentFrame)  // called at frame start on DrawnView
```

---

## Memory Management / Disposal

### CachedObject.Dispose()
- Disposes `Surface` (unless `PreserveSourceFromDispose`), `Picture`, `Image`
- Sets all to null

### Control Disposal (SkiaControl.Shared.cs ~L5635)
```
RenderObjectPreparing?.Dispose()
clipPreviousCachePath?.Dispose()
PaintErase?.Dispose()
RenderObjectPrevious?.Dispose()
```

### Surface Return
- CPU surfaces: `SurfaceCacheManager.ReturnSurface()` (pool or dispose)
- GPU surfaces (GPU and ImageCompositeGPU): `DisposableManager.EnqueueDisposable()` (delayed 3 frames)

---

## AutoCache Behavior

`AutoCache` is a flag that specific parent controls use to dynamically manage their **content's** cache:

- **SkiaScroll**: While scrolling → `Content.UseCache = Operations`. When stopped → restores original.
- **SkiaDrawer**: Same pattern as SkiaScroll — `Operations` while animating, original at rest.

`AutoCache` does **not** auto-select a cache type on `SkiaControl` itself. It's purely a flag read by containers.

---

## Controls with Built-in Cache Defaults

| Control | Default Cache | Rationale |
|---------|--------------|-----------|
| `SkiaSvg` | `Operations` | SVG paths are draw-command-heavy, ideal for SKPicture |
| `SkiaProgress` | `ImageDoubleBuffered` | Animated bar needs smooth updates |
| `SkiaSlider` | `ImageDoubleBuffered` | Dragging needs smooth updates |
| `SkiaLottie` | `ImageDoubleBuffered` | Frame-by-frame animation |
| `SkiaGif` | `ImageDoubleBuffered` | Frame-by-frame animation |
| `SkiaMediaImage` | `ImageDoubleBuffered` | Streaming frames |
| `SkiaWheelPickerCell` | `Operations` | Small, static text cells |
| `SkiaWheelShape` | `Image` | Static shape, rarely changes |
| `SkiaRadioButton` | `Image` | Static icon, rarely changes |

---

## Controls That Must NOT Be Cached (as a containing layer)

Per project rules and code behavior, these controls should **never** have cache applied to a layer/layout wrapping them:

- **`SkiaScroll`** — Content scrolls by offset; caching the scroll itself would freeze content position.
- **`SkiaDrawer`** — Animated panel; same reason as scroll.
- **`SkiaCarousel`** — Swiping content; caching would freeze current page.
- **`SkiaMauiElement`** and derived (`SkiaMauiEntry`, etc.) — Native views overlaid; caching would render stale native content.

These controls manage their own internal caching (e.g., `AutoCache` on scroll/drawer) but the parent layout wrapping them should use `UseCache = None`.

---

## ExpandDirtyRegion

`Thickness` property that enlarges the rendered/dirty area beyond control bounds. Used when content overflows (e.g., drop shadows on a switch thumb):

```csharp
ExpandDirtyRegion = new Thickness(3, 6); // SkiaSwitch Cupertino style
ExpandDirtyRegion = new Thickness(5, 6); // SkiaSwitch Material style
```

Applied during dirty region calculation (SkiaControl.Shared.cs) and cache region calculation (SkiaControl.Cache.cs).

---

## Practical Cache Selection Guide

| Scenario | Recommended Cache | Reasoning |
|----------|-------------------|-----------|
| Static shape (rectangle, circle, path) | `Operations` | SKPicture recording is cheapest |
| SVG icon | `Operations` | Path-heavy, perfect for command replay |
| Static label | `Operations` | Text shaping is expensive, replay is cheap |
| Button with background | `Image` | Rasterized once including effects/shadows |
| Image/photo display | `Image` | Already a bitmap; just snapshot once |
| Slider thumb/track | `ImageDoubleBuffered` | Smooth animation during drag |
| Progress bar | `ImageDoubleBuffered` | Animated value changes |
| Lottie/GIF animation | `ImageDoubleBuffered` | Frame-by-frame rendering |
| Large scroll content (forms, PDF) | `ImageComposite` on content wrapper | Partial redraw avoids re-rendering all children |
| Large scroll content (GPU-accelerated canvas) | `ImageCompositeGPU` on content wrapper | Partial redraw + GPU-backed surface, zero CPU→GPU upload |
| Game background | `GPU` | Hardware-accelerated, minimal CPU overhead |
| Game sprite (small, simple) | `Operations` | Cheap, low memory |
| Game sprite (complex, animated) | `ImageDoubleBuffered` | Smooth frame updates |
| Container holding animated children | `None` | Container must re-render to reflect child changes |
| Container holding scroll/drawer/carousel | `None` | Dynamic content must not be frozen |
| Control with GPU-surface shader effect | `Image` or `ImageDoubleBuffered` | **PROHIBITED** with `Operations` or `GPU` — shader programs require a live rasterized surface, not command recording or a conflicting GPU surface |
| Parent of children using `GPU`/`ImageCompositeGPU` cache | `Image`, `ImageComposite`, or `None` | **PROHIBITED** with `Operations` — SKPicture cannot capture GPU-surface output from children |

### Architecture Pattern for Large Scrolls

Wrap the main content layout with `ImageComposite` cache and sub-layouts with `Operations` cache. Avoid layouts that change size in the scroll direction (forces full `InvalidateMeasure` → `DestroyRenderingObject`).

```xml
<draw:SkiaScroll>
  <draw:SkiaLayout Type="Column" UseCache="ImageComposite">
    <draw:SkiaLayout Type="Column" UseCache="Operations">
      <!-- static section 1 -->
    </draw:SkiaLayout>
    <draw:SkiaLayout Type="Column" UseCache="Operations">
      <!-- static section 2 -->
    </draw:SkiaLayout>
  </draw:SkiaLayout>
</draw:SkiaScroll>
```

---

## Key Source Files

| File | Content |
|------|---------|
| `src/Shared/Internals/Enums/SkiaCacheType.cs` | Cache type enum |
| `src/Shared/Draw/Base/SkiaControl.Cache.cs` | Main cache logic: properties, CreateRenderingObject, invalidation, offscreen rendering |
| `src/Shared/Draw/Base/SkiaControl.Shared.cs` | DrawRenderObject, PaintErase, rendering flow integration |
| `src/Shared/Draw/Base/SkiaControl.Invalidation.cs` | UpdateByChild, dirty tracking |
| `src/Shared/Internals/Models/CachedObject.cs` | CachedObject class (SKPicture/SKImage holder) |
| `src/Shared/Internals/Models/ControlsTracker.cs` | Dirty children tracker for ImageComposite |
| `src/Maui/DrawnUi/Views/SurfaceCacheManager.cs` | SKSurface object pool |
| `src/Maui/DrawnUi/Views/DisposableManager.cs` | Frame-delayed disposal |
| `src/Maui/DrawnUi/Views/DrawnView.cs` | CreateSurface (GPU/CPU), canvas-level offscreen queue |
| `src/Maui/DrawnUi/Draw/Layout/SkiaLayout.cs` | SetupRenderingWithComposition for ImageComposite |
| `src/Maui/DrawnUi/Draw/Scroll/SkiaScroll.cs` | AutoCache behavior for scroll |
| `src/Shared/Super.cs` | Global cache settings |

---

## Internal Cache Code — Working Notes

### When modifying cache code, verify:
1. **All `SkiaCacheType` switch/if branches** in `CreateRenderingObject()` — adding a new type requires handling in both the Operations path and Image path. Also update `IsCacheComposite` and `IsCacheImage` helper properties.
2. **Surface lifecycle** — CPU surfaces must go through `SurfaceCacheManager` for pooling. GPU surfaces (both `GPU` and `ImageCompositeGPU`) must go through `DisposableManager` for delayed disposal. Never dispose a surface that may still be referenced by a front/back buffer.
3. **`PreserveSourceFromDispose`** — critical for ImageComposite and double-buffer paths. When transferring a surface to a new `CachedObject`, set this on the old one to prevent double-dispose.
4. **Thread safety** — `RenderObject` setter is protected by `lock(LockDraw)`. All background rendering goes through the offscreen queue. Never modify `RenderObject` from outside the setter.
5. **`CheckCachedObjectValid()`** — any new cache-related state that could invalidate the cache must be checked here.
6. **`IsRenderingWithComposition`** — checked in SkiaLayout, Grid, and ColumnRow renderers. If adding a new layout type, add the composite guard.

### Common issues when working on cache:
- **Stale cache**: Control changes but cache isn't invalidated → verify `NeedDraw` callback is wired for the changed property.
- **Black/empty rendering**: Surface disposed too early → check `PreserveSourceFromDispose` and `DisposableManager` frame delay.
- **Memory leak**: Surfaces not returned to pool → ensure `CachedObject.Dispose()` is called and surfaces route through `SurfaceCacheManager.ReturnSurface()`.
- **GPU context mismatch**: App backgrounded/foregrounded → `CheckCachedObjectValid` handles via `GRContext.Handle` comparison, but GPU surfaces already drawn become invalid.
- **Composite artifacts**: Dirty region doesn't cover shadows → use `ExpandDirtyRegion`.
- **Composite full rebuild loop**: Child keeps changing size → causes `InvalidateMeasure` → `DestroyRenderingObject` every frame. Set fixed sizes.
