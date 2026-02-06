# DrawnUI Gesture System

---

# Part 1: General Overview

## Why We Need Our Own Gesture System

DrawnUI doesn't use native controls - everything is drawn on a Skia canvas. 
When you tap the screen, the platform says "someone touched at (150, 300)" but has no idea what's there. It's just pixels. 
We need to figure out which control should receive that tap.
For .NET MAUI we use `AppoMobi.Maui.Gestures` package to get gestures for all platforms.

## The Basic Flow

```
Touch Event → Canvas → Find Control at Point → Deliver Gesture
```

1. **Canvas** receives touch from the platform
2. **RenderTree** tells us where each control was drawn
3. **Hit test** checks if touch point is inside a control
4. **Deliver** the gesture to the winning control

## RenderTree: The "What's Where" Map

Every time we draw, we record where each control ended up:

```csharp
tree.Add(new SkiaControlWithRect(
    control,             // the control
    destinationRect,     // where it was drawn
    control.DrawingRect, // its actual bounds
    index                // z-order (front to back)
));
```

This list is our map for gesture processing. We walk through it (top to bottom, front to back) looking for the control that contains the touch point.

## Hit Testing

When a gesture arrives, we need to check if the touch point is inside each control. This happens in two places:

### 1. Canvas Level
Canvas iterates through registered gesture listeners and checks:
```csharp
forChild = ((SkiaControl)listener).HitIsInside(args.Event.StartingLocation.X, args.Event.StartingLocation.Y);
```

### 2. Control Level
Each control checks its own bounds:
```csharp
public virtual bool HitIsInside(float x, float y)
{
    return HitBoxAuto.ContainsInclusive(x, y);
}
```

`HitBoxAuto` is typically the control's `DrawingRect` - where it actually rendered on screen.

### How It Works Together
- Canvas finds top-level controls that contain the point
- Each control's `ProcessGestures` walks its RenderTree to find children at that point
- First control that contains the point AND wants to handle the gesture wins
- Controls can pass gestures to children or consume them themselves

### BlockGesturesBelow

Sometimes you want a control to block gestures from reaching controls underneath, even if it doesn't actively handle them. Example: a drawer panel that covers part of the screen - you don't want taps to pass through to the content behind it.

```csharp
public bool BlockGesturesBelow { get; set; }
```

When `true`, the control returns itself as the "consumer" even when it doesn't process the gesture:
```csharp
var consumedDefault = BlockGesturesBelow ? this : null;
```

This stops gesture propagation to controls below in the z-order.

### InputTransparent

The opposite of BlockGesturesBelow. When `true`, the control is completely ignored for gesture processing - touches pass right through it as if it wasn't there.

```csharp
if (listener.InputTransparent)
    continue; // skip this control entirely
```

Useful for overlay decorations, visual effects, or any control that should be visible but not interactive.

## Gesture Listeners

Controls that want gestures implement `ISkiaGestureListener`:

```csharp
public interface ISkiaGestureListener
{
    ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply);
    bool HitIsInside(float x, float y);
}
```

They register with the Canvas when visible, unregister when hidden/disposed.

## That's It For Basics

Touch comes in → we check RenderTree → find control at that point → deliver gesture.

Simple.

---

# Part 2: In Deep - Challenges & Edge Cases

## The Caching Problem

DrawnUI caches controls for performance. A control renders to an off-screen bitmap, then we just draw that bitmap instead of re-rendering.

**The catch:** cache was created at position A, but might be drawn at position B.

Example: A list item is cached when at Y=500. You scroll. Now it's drawn at Y=300. But its internal RenderTree still thinks children are at Y=500.

Touch comes in at Y=320. We need to translate that to Y=520 to match the cache's coordinate system.

### LastDestination

Each `CachedObject` tracks:
- `Bounds` - where it was when cache was created
- `LastDestination` - where it was actually drawn last time

```csharp
public SKPoint TranslateInputCoords(SKRect drawingRect)
{
    var current = LastDestination.IsEmpty ? drawingRect : LastDestination;
    var offsetX = current.Left - Bounds.Left;
    var offsetY = current.Top - Bounds.Top;
    return new SKPoint(-offsetX, -offsetY);
}
```

## Nested Caches

When caches are nested (cached scroll → cached layout → button), each level needs its own coordinate translation. ProcessGestures walks down the tree, translating at each cached level.

## Parent RenderTree Parameter

```csharp
ProcessGestures(args, apply, IReadOnlyList<SkiaControlWithRect> parentRenderTree = null)
```

Why? A cached control that didn't redraw has stale/empty RenderTree. But the parent knows where it drew the cached child. Parent's RenderTree fills the gap.

## ImageComposite Special Case

ImageComposite is smart caching - only redraws "dirty" children, composites onto previous frame.

**Problem:** Non-dirty children don't draw, so their cache's `LastDestination` never updates.

**Solution:** `ArrangeCache()` - arranges the control AND updates cache position:

```csharp
public virtual void ArrangeCache(SKRect destination, float widthRequest, float heightRequest, float scale)
{
    Arrange(destination, widthRequest, heightRequest, scale);

    var cache = RenderObject ?? RenderObjectPrevious;
    if (cache != null)
    {
        cache.LastDestination = DrawingRect;
    }
}
```

Used in ImageComposite composition mode for non-dirty children.

## Files Involved

- `Canvas.cs` - receives platform touches, dispatches to controls
- `SkiaControl.Shared.cs` - `ProcessGestures`, `HitIsInside`, `ArrangeCache`
- `SkiaLayout.*.cs` - builds RenderTree during drawing
- `SkiaRenderObject.cs` - `CachedObject` with `LastDestination`, `TranslateInputCoords`
