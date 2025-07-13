# SkiaScroll.Planes - Managed Virtualization Technical Documentation

## Overview

The `SkiaScroll.Planes.cs` file implements a sophisticated **3-plane virtualization system** for handling infinite scrolling with recycled cell content when `VirtualisationType.Managed` is enabled. This system provides seamless scrolling performance by maintaining three rendering surfaces (planes) that are dynamically swapped and rendered as the user scrolls.

## Key Architecture Components

### Activation Requirements

The 3-plane system is activated when **ALL** conditions are met:
- `Content` is a `SkiaLayout` 
- `Orientation` is not `ScrollOrientation.Both`
- `Layout.Virtualisation == VirtualisationType.Managed`
- `UseVirtual` property returns `true`

### Core Data Structures

#### Plane Class
```csharp
public class Plane
{
    public float OffsetY;           // Source offset for positioning
    public float OffsetX;           // Source offset for positioning  
    public SKColor BackgroundColor; // Debug/visual identification
    public SKSurface Surface;       // Rendering surface
    public bool IsReady;           // Indicates if plane content is prepared
    public string Id;              // Identifier (Red, Green, Blue)
    public CachedObject CachedObject; // Cached rendered content
}
```

#### Three Planes System
- **PlaneCurrent** (Red): Currently visible content
- **PlaneForward** (Green): Next content in scroll direction
- **PlaneBackward** (Blue): Previous content in scroll direction

## Virtualization Strategy

### Plane Initialization
```csharp
_planeWidth = viewportWidth;
_planeHeight = viewportHeight * 2;  // Each plane is 2x viewport height
_planePrepareThreshold = _planeHeight / 2;
```

Each plane:
- Width = viewport width
- Height = 2× viewport height (provides buffer for smooth scrolling)
- Positioned with calculated offsets to create continuous coverage

### Plane Positioning Logic

**Vertical Scrolling Layout:**
```
PlaneBackward: OffsetY = -_planeHeight
PlaneCurrent:  OffsetY = 0
PlaneForward:  OffsetY = +_planeHeight
```

**Real-time Positioning:**
```csharp
// Applied during rendering
rectCurrent.Offset(0, currentScroll + PlaneCurrent.OffsetY);
rectForward.Offset(0, currentScroll + PlaneForward.OffsetY);
rectBackward.Offset(0, currentScroll + PlaneBackward.OffsetY);
```

## Plane Swapping Mechanism

### Swap Down (Scrolling Down)
**Trigger Condition:** `rectForward.MidY <= (Viewport.Height / 2)`

**Rotation Logic:**
```
PlaneForward  → PlaneCurrent
PlaneCurrent  → PlaneBackward  
PlaneBackward → PlaneForward (invalidated)
```

**Offset Updates:**
```csharp
PlaneForward.OffsetY = PlaneCurrent.OffsetY + _planeHeight;
PlaneBackward.OffsetY = PlaneCurrent.OffsetY - _planeHeight;
```

### Swap Up (Scrolling Up)
**Trigger Condition:** `rectBackward.MidY > Viewport.Height / 2`

**Rotation Logic:**
```
PlaneBackward → PlaneCurrent
PlaneCurrent  → PlaneForward
PlaneForward  → PlaneBackward (invalidated)
```

## Asynchronous Plane Preparation

### Background Rendering System
- **Thread Safety:** Uses `SemaphoreSlim` locks per plane
- **Cancellation:** Previous rendering jobs are cancelled when new ones start
- **Global Lock:** `_globalPlanePreparationLock` ensures single plane preparation at a time

### Plane Preparation Workflow
1. **Trigger Detection:** Scroll position indicates need for new content
2. **Cancellation:** Cancel any existing preparation for that plane
3. **Context Capture:** Capture current viewport state to avoid race conditions
4. **Background Task:** Render content asynchronously on background thread
5. **Cache Update:** Update plane's `CachedObject` with rendered content

### Preparation Conditions
**Forward Plane:**
- Not already building
- Plane exists and not ready
- `ViewportOffsetY != 0` (not at start)

**Backward Plane:**
- Not already building  
- Plane exists and not ready
- `ViewportOffsetY >= 0` (not at end)

## Integration with MeasuringStrategy

### ⚠️ Important Note on MeasuringStrategy.MeasureVisible
> **The `MeasuringStrategy.MeasureVisible` case is actually totally disabled!** 
> Logic will be replaced to use `MeasureFirst` instead.

### Current Implementation
```csharp
// Lines 300-315 in SkiaScroll.Planes.cs
if (Content is SkiaLayout layout && layout.IsTemplated
    && layout.MeasureItemsStrategy == MeasuringStrategy.MeasureVisible
    && layout.LastMeasuredIndex < layout.ItemsSource.Count)
{
    // This code path is disabled - will use MeasureFirst strategy
    var measuredEnd = layout.GetMeasuredContentEnd();
    // ... incremental measurement logic
}
```

### Boundary Conditions
The system handles content boundaries through enhanced swap conditions:
- **Content End:** Detects when all content is measured and adjusts swap triggers
- **Content Start:** Handles backward scrolling at content beginning
- **Dynamic Triggers:** Adjusts swap points based on content availability

## Performance Optimizations

### Memory Management
- **SKSurface Reuse:** Each plane maintains its own rendering surface
- **Cached Objects:** Rendered content is cached as `CachedObject` for fast drawing
- **Disposal Management:** Proper cleanup of old cached objects

### Rendering Efficiency
- **Intersection Testing:** Only draw planes that intersect with viewport
- **Background Preparation:** Content rendering happens off the main thread
- **Multi-swap Logic:** Handles fast scrolling with multiple swaps per frame

### Throttling and Locks
- **Per-plane Locks:** Prevent concurrent rendering of same plane
- **Global Preparation Lock:** Ensures single plane preparation at a time
- **Cancellation Tokens:** Cancel outdated rendering requests

## Drawing Process

### Main Rendering Loop (`DrawVirtual`)
1. **Initialize Planes:** Create planes if not exist
2. **Prepare Current:** Ensure current plane has content
3. **Calculate Positions:** Apply scroll offsets to plane rectangles
4. **Draw Planes:** Render visible planes in order (Backward, Current, Forward)
5. **Multi-swap Logic:** Handle fast scrolling with multiple swaps
6. **Background Preparation:** Trigger preparation of needed planes

### Plane Content Rendering (`PreparePlane`)
1. **Create Recording Context:** Set up drawing context for plane surface
2. **Calculate Viewport:** Determine visible area for plane content
3. **Apply Transforms:** Position canvas for content rendering
4. **Paint Content:** Call `PaintOnPlane` to render actual content
5. **Cache Result:** Store rendered content as `CachedObject`

## Gesture Handling System

### Overview
The 3-plane system includes a sophisticated gesture handling mechanism that enables touch interactions to work correctly across all planes regardless of scroll position or plane swaps. This system solves the complex problem of coordinate transformation when content is rendered on different planes at different times.

### Core Challenge
When using managed virtualization with 3 planes:
1. **Planes are rendered at different scroll positions** - A plane's content is rendered when the scroll is at position X
2. **Planes move independently** - Each plane has its own `OffsetY` that changes during swaps
3. **Gesture coordinates are relative to current screen position** - But children's `HitRect` coordinates are relative to when the plane was rendered
4. **Multiple coordinate systems** - Need to transform between: screen coordinates → plane coordinates → child coordinates

### Rendering Tree Capture
Each plane captures its rendering tree when content is prepared:

```csharp
// Plane.cs
public IReadOnlyList<SkiaControlWithRect> RenderTree { get; private set; }
public SKPoint RenderTreeCaptureOffset { get; private set; }        // Scroll position when captured
public float RenderTreeCapturePlaneOffsetY { get; private set; }    // Plane position when captured

public void CaptureRenderTree(List<SkiaControlWithRect> tree, SKPoint captureOffset, float planeOffsetY)
{
    RenderTree = tree?.ToList().AsReadOnly();  // Immutable snapshot for thread safety
    RenderTreeCaptureOffset = captureOffset;
    RenderTreeCapturePlaneOffsetY = planeOffsetY;
}
```

### Coordinate Transformation Algorithm
The gesture processing uses a two-part coordinate transformation:

```csharp
// ProcessGesturesForPlane method
var currentScroll = InternalViewportOffset.Pixels.Y;
var planeOffsetY = currentScroll + plane.OffsetY;

// Calculate total movement since plane was rendered
var scrollMovement = currentScroll - plane.RenderTreeCaptureOffset.Y;        // How much scroll changed
var planeMovement = plane.OffsetY - plane.RenderTreeCapturePlaneOffsetY;     // How much plane moved
var totalMovement = scrollMovement + planeMovement;                          // Combined movement

// Transform child HitRect to current screen coordinates
var adjustedHitRect = child.HitRect;
adjustedHitRect.Offset(0, totalMovement);
```

### Plane-Aware Gesture Processing
Gestures are processed across all visible planes in Z-order:

```csharp
// ProcessGesturesForPlanes method - Z-order processing
1. Forward Plane (Green) - Top layer
2. Current Plane (Red) - Middle layer  
3. Backward Plane (Blue) - Bottom layer
```

For each plane:
1. **Plane Hit Test:** `IsGestureInPlane()` checks if gesture intersects plane's visible area
2. **Child Hit Test:** Transform coordinates and test against each child's `HitRect`
3. **Event Routing:** Forward gesture to appropriate child control
4. **Bubble Handling:** Route tapped events to `Content.OnChildTapped()`

### Thread Safety Considerations
- **Immutable Snapshots:** Render trees use `ToList().AsReadOnly()` to prevent race conditions
- **Capture Timing:** Rendering tree is captured after content painting completes
- **Gesture Processing:** Uses immutable snapshots so `Reset()` doesn't affect ongoing gesture processing

### Why This Solution Works
**Problem:** Original implementation only worked for Red plane (usually `OffsetY = 0`)
```csharp
// OLD - Only considered scroll movement
var totalMovement = planeOffsetY - plane.RenderTreeCaptureOffset.Y;
```

**Solution:** Account for both scroll changes AND plane position changes
```csharp
// NEW - Considers both scroll and plane movement
var scrollMovement = currentScroll - plane.RenderTreeCaptureOffset.Y;
var planeMovement = plane.OffsetY - plane.RenderTreeCapturePlaneOffsetY;
var totalMovement = scrollMovement + planeMovement;
```

### Integration with Standard Gesture Processing
The system seamlessly integrates with existing gesture processing:

```csharp
// SkiaScroll.Gestures.cs - ProcessGestures method
if (UseVirtual && PlaneCurrent != null)
{
    return ProcessGesturesForPlanes(args, apply);  // 3-plane aware processing
}
return base.ProcessGestures(args, apply);          // Standard processing
```

### Debugging Gesture Issues
The system includes comprehensive debug logging:
- Plane identification and current positions
- Capture offsets vs current offsets  
- Coordinate transformation calculations
- Hit test results for each child
- Gesture routing decisions

This gesture handling system enables complex scrollable UIs with recycled content to maintain full interactivity across all planes, providing the user with a seamless experience regardless of the underlying virtualization complexity.

## Debug and Development Features

### Visual Identification
- **Red Plane:** Current content
- **Green Plane:** Forward content  
- **Blue Plane:** Backward content

### Debugging Output
- Plane swap notifications
- Preparation status messages
- Content boundary detection
- Performance monitoring
- **Gesture Debug Info:** Coordinate transformations, hit tests, and routing decisions

### Test Implementation
Reference implementation available in `ThreePlaneVirtualizationTest.cs` demonstrating:
- Proper configuration setup
- Data binding with recycled templates
- Interactive testing controls
- Performance monitoring
- **Gesture Testing:** Tap interactions across all planes

## Configuration Example

```csharp
var virtualizedLayout = new SkiaLayout
{
    MeasureItemsStrategy = MeasuringStrategy.MeasureVisible, // Will use MeasureFirst
    Virtualisation = VirtualisationType.Managed,
    RecyclingTemplate = RecyclingTemplate.Enabled,
    ItemsSource = dataSource
};

var scroll = new SkiaScroll
{
    Orientation = ScrollOrientation.Vertical,
    Content = virtualizedLayout
};
```

This configuration enables the 3-plane virtualization system for smooth infinite scrolling with recycled cell content.