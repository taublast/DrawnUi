# Understanding the Drawing Pipeline

DrawnUI's drawing pipeline transforms your logical UI controls into pixel-perfect drawings on a Skia canvas. 
This article explains how the pipeline works, from initial layout calculations to final rendering.  
DrawnUI is rather a rendering engine, not a UI-framework, and is not designed for an "unaware" usage.  
In order to use it effectively, one needs to understand how it works in order to achive the best performance and results.  
At the same time it's possible to create abstractional wrappers around performance-oriented controls, to automaticaly set caching types, layout options etc. that could be used without deep understanding of internals. 

## Overview

The DrawnUI drawing pipeline consists of several key stages:

1. **Executing pre-draw actions** - including gestures processing, animations, etc.
2. **Measuring and arranging** - measure and arrange self and children if layout was invalidated
3. **Drawing and caching**   - painting and caching for future fast re-draws of recorded SKPicture or rasterized SKImage
4. **Executing post-draw actions** - like post-animations for overlays, etc.

## Pipeline Flow

### 1. Executing Pre-Draw Actions

Before any drawing occurs, DrawnUI executes several pre-draw actions including gestures processing, animations, etc.

#### Invalidation Triggers

The pipeline begins when a control needs to be redrawn. This can happen due to:

- Some controls property changed (color, size, text, etc.)
- Layout changes (adding/removing children)
- Animation updates
- User interactions
- Canvas received a "redraw" request from the engine (app went foreground, graphics context changed etc).
- The top framework decided to re-draw our Canvas

```csharp
// Most commonly used invalidation methods
control.Update();            // Mark for redraw (Update), invalidates cache
control.Repaint();           // Mark parent for redraw (Update), to repaint without destroying cache, at new positions, transformed etc
control.Invalidate();        // Invalidate and (maybe, depending on this control  logic) update
control.InvalidateMeasure(); // Just recalculate size and layout and update
control.Parent?.Invalidate() // When the above doesn't work if parent refuses to invalidate due to its internal logic
```

#### Pre-Draw Operations

**Gesture Processing:**
- Process pending touch events and gestures
- Update gesture states and animations
- Handle user input through the control hierarchy

**Animation Updates:**
- Execute frame-based animations
- Update animated properties
- Calculate interpolated values for smooth transitions

**Layout Validation:**
- Check if measure/arrange is needed
- Process layout invalidations
- Prepare for drawing operations

### 2. Measuring and Arranging

This stage handles the layout system - measure and arrange self and children if layout was invalidated.

#### Measure Stage

Controls calculate their desired size based on available space and content requirements.

```csharp
public virtual ScaledSize Measure(float widthConstraint, float heightConstraint, float scale)
{
    // Create measure request with constraints
    var request = CreateMeasureRequest(widthConstraint, heightConstraint, scale);

    // Measure content and return desired size
    return MeasureLayout(request, false);
}
```

**Measure Process:**
1. **Constraints Calculation** - Determine available space considering margins and padding
2. **Content Measurement** - Measure child controls based on layout type
3. **Size Request** - Calculate final desired size

**Layout Types:**
- `Absolute` - Children positioned at specific coordinates
- `Column/Row` - Stack children vertically or horizontally
- `Grid` - Arrange children in rows and columns
- `Wrap` - Flow children with wrapping

#### Arrange Stage

The arrange stage positions controls within their allocated space and calculates final drawing rectangles.

```csharp
public virtual void Arrange(SKRect destination, float widthRequest, float heightRequest, float scale)
{
    // Pre-arrange validation
    if (!PreArrange(destination, widthRequest, heightRequest, scale))
        return;

    // Calculate final layout
    var layout = CalculateLayout(arrangingFor, widthRequest, heightRequest, scale);

    // Set drawing rectangle
    DrawingRect = layout;

    // Post-arrange processing
    PostArrange(destination, widthRequest, heightRequest, scale);
}
```

**Arrange Process:**
1. **Pre-Arrange** - Validate and prepare for layout
2. **Layout Calculation** - Determine final position and size
3. **Drawing Rectangle** - Set the area where control will be drawn
4. **Post-Arrange** - Cache layout information and handle changes

### 3. Drawing and Caching

This is the core rendering stage - painting and caching for future fast re-draws of recorded SKPicture or rasterized SKImage.

#### Paint Stage

The paint stage renders the actual visual content to the Skia canvas.

```csharp
protected virtual void Paint(DrawingContext ctx)
{
    // Paint background
    PaintTintBackground(ctx.Context.Canvas, ctx.Destination);

    // Execute custom paint operations
    foreach (var action in ExecuteOnPaint.Values)
    {
        action?.Invoke(this, ctx);
    }
}
```

**Drawing Context:**
- `SKCanvas` - The Skia drawing surface
- `SKRect Destination` - Where to draw in pixels
- `float Scale` - Pixel density scaling factor
- `object Parameters` - Optional custom parameters

#### Caching System

DrawnUI uses sophisticated caching to optimize rendering performance through render objects. This is crucial for achieving smooth 60fps animations and responsive UI.

##### Cache Types

```csharp
public enum SkiaCacheType
{
    None,                    // No caching, direct drawing every frame
    Operations,              // Cache drawing operations as SKPicture  
    OperationsFull,          // Cache operations ignoring clipping bounds
    Image,                   // Cache as rasterized SKBitmap  
    ImageComposite,          // Advanced bitmap caching with composition
    ImageDoubleBuffered,     // Background thread rendering of cache of same same, while showing previous cache
    GPU                      // Hardware-accelerated GPU memory caching
}
```

**Choosing the Right Cache Type:**
- **None** - Do not cache scrolls, drawers etc, native views and their containers.
- **Operations** - For anything, but maybe best for static vector content like text, icons, SVG.
- **Image** - Rasterize anything once and then just draw the bitmap on every frame.
- **ImageDoubleBuffered** - Perfect for recycled cells of same size
- **GPU** - Use GPU memory for storing overlays, avoid large sizes.

##### Render Object Pipeline

```csharp
public virtual bool DrawUsingRenderObject(DrawingContext context,
    float widthRequest, float heightRequest)
{
    // 1. Arrange the control
    Arrange(context.Destination, widthRequest, heightRequest, context.Scale);

    // 2. Check if we can use cached render object
    if (RenderObject != null && CheckCachedObjectValid(RenderObject))
    {
        DrawRenderObjectInternal(context, RenderObject);
        return true;
    }

    // 3. Create new render object if needed
    var cache = CreateRenderingObject(context, recordArea, oldObject, UsingCacheType,
        (ctx) => { PaintWithEffects(ctx); });

    // 4. Draw using the render object
    DrawRenderObjectInternal(context, cache);

    return true;
}
```

##### Cache Validation

Render objects are invalidated when:
- Control size or position changes
- Visual properties change (colors, effects, transforms)
- Child controls are added, removed, or modified
- Animation state updates require re-rendering
- Hardware context changes (e.g., returning from background)

### 4. Executing Post-Draw Actions

After the main drawing is complete, DrawnUI executes post-draw operations like post-animations for overlays, etc.

**Post-Animations:**
- Overlay effects and animations
- Particle systems and visual effects
- UI feedback animations (ripples, highlights)

**Smart Resource Management:**
- Frame-based disposal through DisposeManager
- Update animation states
- Prepare for next frame

#### DisposeManager

The **DisposeManager** is a resource management system that prevents crashes disposing resources in the middle of the drawing and ensures smooth performance by disposing packs of objects at once at the end of the frame. It is concurrent usage safe

**Why It's Needed:**
In high-performance rendering, resources like SKBitmap, SKPicture, and render objects might still be referenced by background threads, GPU operations, or cached render objects even after they're "logically" no longer needed. Immediate disposal can cause crashes or visual glitches.

**How to use:**
```csharp
//call this
control.DisposeObject(resource);
```

**Practice:**
```csharp
// WUpdating a cached image
var oldBitmap = this.CachedBitmap;
this.CachedBitmap = newBitmap;

// Don't dispose immediately - let DisposeManager handle it
DisposeObject(oldBitmap); // Will be disposed safely after drawing few frames
```
 
**Benefits:**
- **Crash Prevention** - Resources are safely disposed after GPU/background operations complete
- **Performance** - No blocking waits or expensive synchronization
- **Automatic** - Works transparently without developer intervention

## Gesture Processing Integration

Gesture processing is integrated throughout the pipeline, primarily during pre-draw actions. Canvas asynchronously receives gesture events from the native platform and accumulates them to be passed through the control hierarchy at the start of a new frame. Gesture events are processed in the order they were received, to the concerned control's `ISkiaGestureListener.OnSkiaGestureEvent` implementation. This is a technical method that should not be used directly - it calls `SkiaControl.ProcessGestures` method that can be safely overridden.

### Gesture Parameters

#### SkiaGesturesParameters
Contains the core gesture information:

```csharp
public class SkiaGesturesParameters
{
    public TouchActionResult Type { get; set; }        // Down, Up, Tapped, Panning, etc.
    public TouchActionEventArgs Event { get; set; }    // Touch details and coordinates
}
```

**TouchActionResult Types:**
- `Down` - Initial touch contact
- `Up` - Touch release
- `Tapped` - Quick tap gesture
- `Panning` - Drag/swipe movement
- `LongPressing` - Extended press
- `Cancelled` - Gesture interrupted

**TouchActionEventArgs Properties:**
- `Location` - Current touch position
- `StartingLocation` - Initial touch position
- `Id` - Unique touch identifier for multi-touch
- `NumberOfTouches` - Count of simultaneous touches
- `Distance` - Movement delta and velocity information

#### GestureEventProcessingInfo
Manages coordinate transformations and gesture ownership:

```csharp
public struct GestureEventProcessingInfo
{
    public SKPoint MappedLocation { get; set; }        // Touch location with transforms applied
    public SKPoint ChildOffset { get; set; }           // Coordinate offset for child controls
    public SKPoint ChildOffsetDirect { get; set; }     // Direct offset without cached transforms
    public ISkiaGestureListener AlreadyConsumed { get; set; } // Tracks gesture ownership
}
```

### Hit Testing System

Hit testing determines which controls can receive touch input through a multi-stage process:

#### Primary Hit Testing
```csharp
public virtual bool HitIsInside(float x, float y)
{
    var hitbox = HitBoxAuto;  // Gets transformed drawing rectangle
    return hitbox.ContainsInclusive(x, y);
}

public virtual SKRect HitBoxAuto
{
    get
    {
        var moved = ApplyTransforms(DrawingRect);  // Apply all transforms
        return moved;
    }
}
```

#### Transform-Aware Hit Testing
The system accounts for control transformations (rotation, scale, translation):

```csharp
public virtual bool IsGestureForChild(SkiaControlWithRect child, SKPoint point)
{
    if (child.Control != null && !child.Control.InputTransparent && child.Control.CanDraw)
    {
        var transformed = child.Control.ApplyTransforms(child.HitRect);
        return transformed.ContainsInclusive(point.X, point.Y);
    }
    return false;
}
```

#### Coordinate Transformation
Touch coordinates are transformed through the control hierarchy:

```csharp
public SKPoint TransformPointToLocalSpace(SKPoint pointInParentSpace)
{
    // Apply inverse transformation to get point in local space
    if (RenderTransformMatrix != SKMatrix.Identity &&
        RenderTransformMatrix.TryInvert(out SKMatrix inverse))
    {
        return inverse.MapPoint(pointInParentSpace);
    }
    return pointInParentSpace;
}
```

### Gesture Processing Flow

#### Canvas-Level Processing
The Canvas manages the main gesture processing loop:

```csharp
protected virtual void ProcessGestures(SkiaGesturesParameters args)
{
    // Create initial processing info with touch location
    var adjust = new GestureEventProcessingInfo(
        args.Event.Location.ToSKPoint(),
        SKPoint.Empty,
        SKPoint.Empty,
        null);

    // First pass: Process controls that already had input
    foreach (var hadInput in HadInput.Values)
    {
        var consumed = hadInput.OnSkiaGestureEvent(args, adjust);
        if (consumed != null) break;
    }

    // Second pass: Hit test all gesture listeners
    foreach (var listener in GestureListeners.GetListeners())
    {
        if (listener.HitIsInside(args.Event.StartingLocation.X, args.Event.StartingLocation.Y))
        {
            var consumed = listener.OnSkiaGestureEvent(args, adjust);
            if (consumed != null) break;
        }
    }
}
```

#### Control-Level Processing
Individual controls process gestures with coordinate transformation:

```csharp
public ISkiaGestureListener OnSkiaGestureEvent(SkiaGesturesParameters args,
    GestureEventProcessingInfo apply)
{
    // Apply inverse transforms if control has transformations
    if (HasTransform && RenderTransformMatrix.TryInvert(out SKMatrix inverse))
    {
        apply = new GestureEventProcessingInfo(
            inverse.MapPoint(apply.MappedLocation),
            apply.ChildOffset,
            apply.ChildOffsetDirect,
            apply.AlreadyConsumed
        );
    }

    // Process the gesture
    var result = ProcessGestures(args, apply);
    return result; // Return consumer or null
}
```

#### Practical Usage Example
```csharp
public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args,
    GestureEventProcessingInfo apply)
{
    // Get local coordinates
    var point = TranslateInputOffsetToPixels(args.Event.Location, apply.ChildOffset);

    switch (args.Type)
    {
        case TouchActionResult.Down:
            IsPressed = true;
            return this; // Consume the gesture

        case TouchActionResult.Up:
            if (IsPressed)
            {
                IsPressed = false;
                OnClicked();
                return this;
            }
            break;
    }

    return null; // Don't consume, pass to other controls
}
```

### Key Concepts

**Gesture Consumption:**
- Return `this` to consume the gesture and prevent it from reaching other controls
- Return `null` to allow the gesture to continue through the hierarchy
- `BlockGesturesBelow` property can block all gestures from reaching lower controls

**Coordinate Spaces:**
- **Canvas Space** - Root coordinate system
- **Parent Space** - Coordinates relative to immediate parent
- **Local Space** - Coordinates relative to the control itself
- **Transformed Space** - Coordinates accounting for all applied transforms

**Multi-Touch Support:**
- Each touch has a unique `Id` for tracking
- `NumberOfTouches` indicates simultaneous touches
- Controls can handle complex multi-finger gestures

## Performance Optimizations

Understanding these optimizations is crucial for building high-performance DrawnUI applications.

### 1. Smart Caching Strategy

**Choose Cache Types Wisely:**
- **Static Content** - Use `Operations` for text, icons, simple shapes
- **Complex Graphics** - Use `Image` for content with effects, shadows, gradients
- **Animated Content** - Use `ImageDoubleBuffered` for smooth 60fps animations
- **High-Performance** - Use `GPU` caching when hardware acceleration is available

**Cache Invalidation Best Practices:**
- Batch property changes to minimize cache invalidations
- Use `Repaint()` instead of `Update()` when only position/transform changes
- Avoid frequent size changes that invalidate image caches

### 2. Layout System Optimization

**Efficient Invalidation:**
- **Layout Dirty Tracking** - Only re-layout when absolutely necessary
- **Measure Caching** - Reuse previous measurements when constraints haven't changed
- **Viewport Limiting** - Only process and measure visible content
- **Hierarchical Updates** - Invalidate only affected branches of the control tree

**Layout Performance Tips:**
- Prefer `Absolute` layout for static positioning
- Use `Column/Row` for simple stacking scenarios
- Reserve `Grid` for complex layouts that truly need it
- Minimize deep nesting of layout containers

### 3. Drawing Pipeline Optimizations

**Rendering Efficiency:**
- **Clipping Optimization** - Skip drawing operations outside visible bounds
- **Transform Caching** - Reuse transformation matrices across frames
- **Effect Batching** - Group similar drawing operations to reduce state changes
- **Background Rendering** - Use double-buffered caching for complex animations

## Common Patterns

### Custom Control Drawing

```csharp
public class MyCustomControl : SkiaControl
{
    protected override void Paint(DrawingContext ctx)
    {
        base.Paint(ctx); // Paint background
        
        var canvas = ctx.Context.Canvas;
        var rect = ctx.Destination;
        
        // Custom drawing code here
        using var paint = new SKPaint
        {
            Color = SKColors.Blue,
            IsAntialias = true
        };
        
        canvas.DrawCircle(rect.MidX, rect.MidY, 
            Math.Min(rect.Width, rect.Height) / 2, paint);
    }
}
```

### Layout Container

```csharp
public class MyLayout : SkiaLayout
{
    protected override ScaledSize MeasureAbsolute(SKRect rectForChildrenPixels, float scale)
    {
        // Measure all children
        foreach (var child in Views)
        {
            var childSize = MeasureChild(child, 
                rectForChildrenPixels.Width, 
                rectForChildrenPixels.Height, scale);
        }
        
        // Return total size needed
        return ScaledSize.FromPixels(totalWidth, totalHeight, scale);
    }
    
    protected override void ArrangeChildren(SKRect rectForChildrenPixels, float scale)
    {
        // Position each child
        foreach (var child in Views)
        {
            var childRect = CalculateChildPosition(child, rectForChildrenPixels);
            child.Arrange(childRect, child.SizeRequest.Width, child.SizeRequest.Height, scale);
        }
    }
}
```

## Debugging the Pipeline

### Performance Monitoring

```csharp
// Enable performance tracking
Super.EnableRenderingStats = true;

// Monitor frame rates
var fps = canvasView.FPS;
var frameTime = canvasView.FrameTime;
```

### Visual Debugging

```csharp
// Show control boundaries
control.DebugShowBounds = true;

// Highlight invalidated areas
Super.ShowInvalidatedAreas = true;
```

## Best Practices for Performance

1. **Master Cache Types** - Choose the right `SkiaCacheType` based on content characteristics
2. **Understand Invalidation** - Use the most appropriate invalidation method for each scenario
3. **Optimize Paint Methods** - Keep custom `Paint()` implementations lightweight and efficient
4. **Profile Continuously** - Use built-in performance monitoring to identify bottlenecks
5. **Design for Caching** - Structure your UI to take advantage of render object caching
6. **Handle Gestures Smartly** - Return appropriate consumers to optimize hit-testing performance
7. **Batch Updates** - Group property changes to minimize pipeline overhead

## Debugging and Profiling

```csharp
// Enable performance tracking
Super.EnableRenderingStats = true;

// Monitor frame rates and timing
var fps = canvasView.FPS;
var frameTime = canvasView.FrameTime;

// Visual debugging
control.DebugShowBounds = true;
Super.ShowInvalidatedAreas = true;
```

## Conclusion

DrawnUI is a rendering engine that requires understanding its pipeline to achieve optimal results. Unlike traditional UI frameworks that hide rendering complexity, DrawnUI exposes these details to give you control over performance and visual quality.

**Key Takeaways:**

- **Pipeline Awareness** - Understanding the 4-stage pipeline helps you make informed decisions
- **Caching Strategy** - Proper cache type selection is crucial for performance
- **Invalidation Control** - Knowing when and how to invalidate prevents unnecessary work
- **Performance-First Design** - Design your UI architecture with the pipeline in mind

Understanding DrawnUI's internals enables applications that can achieve smooth 60fps animations, pixel-perfect custom controls, and responsive user experiences across all platforms.

Work with the pipeline design to create applications with smooth performance and visual quality.
