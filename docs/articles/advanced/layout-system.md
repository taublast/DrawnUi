# Layout System Architecture

This article covers the internal architecture of DrawnUi.Maui's layout system, designed for developers who want to understand how layouts work under the hood or extend the system with custom layout types.

## Layout System Overview

DrawnUi.Maui's layout system is built on a core principle: direct rendering to canvas with optimizations for mobile and desktop platforms. Unlike traditional MAUI layouts that create native UI elements, DrawnUi.Maui renders everything using SkiaSharp, enabling consistent cross-platform visuals and better performance for complex UIs.

## Core Components

### SkiaControl

`SkiaControl` is the foundation of the entire UI system. It provides core capabilities for:

- Position tracking in the rendering tree
- Coordinate transformation for touch and rendering
- Efficient invalidation system
- Support for effects and transforms
- Hit testing and touch input handling
- Visibility management

Its key methods include:
- `OnMeasure`: Determines the size requirements of the control
- `OnArrange`: Positions the control within its parent
- `OnDraw`: Renders the control using a SkiaSharp canvas
- `InvalidateInternal`: Manages rendering invalidation

### SkiaLayout

`SkiaLayout` extends `SkiaControl` to provide layout functionality. It's implemented as a partial class with functionality split across files by layout type:

- **SkiaLayout.cs**: Core layout mechanisms
- **SkiaLayout.Grid.cs**: Grid layout implementation 
- **SkiaLayout.ColumnRow.cs**: Stack-like layouts
- **SkiaLayout.BuildWrapLayout.cs**: Wrap layout implementation
- **SkiaLayout.ListView.cs**: Virtualized list rendering
- **SkiaLayout.IList.cs**: List-specific optimization
- **SkiaLayout.ViewsAdapter.cs**: Template management

This approach allows specialized handling for each layout type while sharing common infrastructure.

### Layout Structures

The system uses specialized structures to efficiently track and manage layout calculations:

- **LayoutStructure**: Tracks arranged controls in stack layouts
- **GridStructure**: Manages grid-specific layout information
- **ControlInStack**: Contains information about a control's position 

## Advanced Concepts

### Virtualization

Virtualization is a key performance optimization that only renders items currently visible in the viewport. This enables efficient rendering of large collections.

The `VirtualizationMode` enum defines several strategies:
- **None**: All items are rendered
- **Enabled**: Only visible items are rendered and measured
- **Smart**: Renders visible items plus a buffer
- **Managed**: Uses a managed renderer for advanced cases

Virtualization works alongside template recycling to minimize both CPU and memory usage.

### Template Recycling

The `RecyclingTemplate` property determines how templates are reused across items:
- **None**: New instance created for each item
- **Enabled**: Templates are reused as items scroll out of view
- **Smart**: Reuses templates with additional optimizations

The `ViewsAdapter` class manages template instantiation, recycling, and state management.

### Measurement Strategies

The layout system supports different strategies for measuring item sizes:

- **MeasureFirst**: Measures all items before rendering
- **MeasureAll**: Continuously measures all items
- **MeasureVisible**: Only measures visible items

These strategies let you balance between layout accuracy and performance.

## Extending the Layout System

### Creating a Custom Layout Type

To create a custom layout type, you'll typically:

1. Create a new class inheriting from `SkiaLayout`
2. Override the `OnMeasure` and `OnArrange` methods
3. Implement custom measurement and arrangement logic
4. Optionally create custom properties for layout configuration

Here's a simplified example of a circular layout implementation:

```csharp
public class CircularLayout : SkiaLayout
{
    public static readonly BindableProperty RadiusProperty = 
        BindableProperty.Create(nameof(Radius), typeof(float), typeof(CircularLayout), 100f,
        propertyChanged: (b, o, n) => ((CircularLayout)b).InvalidateMeasure());
        
    public float Radius
    {
        get => (float)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }
    
    protected override SKSize OnMeasure(float widthConstraint, float heightConstraint)
    {
        // Need enough space for a circle with our radius
        return new SKSize(Radius * 2, Radius * 2);
    }
    
    protected override void OnArrange(SKRect destination)
    {
        base.OnArrange(destination);
        
        // Skip if no children
        if (Children.Count == 0) return;
        
        // Calculate center point
        SKPoint center = new SKPoint(destination.MidX, destination.MidY);
        float angleStep = 360f / Children.Count;
        
        // Position each child around the circle
        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.IsVisible) continue;
            
            // Calculate position on circle
            float angle = i * angleStep * (float)Math.PI / 180f;
            float x = center.X + Radius * (float)Math.Cos(angle) - child.MeasuredSize.Width / 2;
            float y = center.Y + Radius * (float)Math.Sin(angle) - child.MeasuredSize.Height / 2;
            
            // Arrange child at calculated position
            child.Arrange(new SKRect(x, y, x + child.MeasuredSize.Width, y + child.MeasuredSize.Height));
        }
    }
}
```

### Best Practices for Custom Layouts

1. **Minimize Measure Calls**: Measure operations are expensive. Cache results when possible.

2. **Implement Proper Invalidation**: Ensure your layout properly invalidates when properties affecting layout change.

3. **Consider Virtualization**: For layouts with many items, implement virtualization to only render visible content.

4. **Optimize Arrangement Logic**: Keep arrangement logic simple and efficient, especially for layouts that update frequently.

5. **Respect Constraints**: Always respect the width and height constraints passed to OnMeasure.

6. **Cache Layout Calculations**: For complex layouts, consider caching calculations that don't need to be redone every frame.

7. **Extend SkiaLayout**: Instead of creating entirely new layout types, consider extending SkiaLayout and creating a new LayoutType enum value if needed.

## Layout System Internals

### The Layout Process

The layout process follows these steps:

1. **Parent Invalidates Layout**: When a change requires remeasurement
2. **OnMeasure Called**: Layout determines its size requirements
3. **Parent Determines Size**: Parent decides actual size allocation
4. **OnArrange Called**: Layout positions itself and its children
5. **OnDraw Called**: Layout renders itself and its children

### Coordinate Spaces

The layout system deals with multiple coordinate spaces:

- **Local Space**: Relative to the control itself (0,0 is top-left of control)
- **Parent Space**: Relative to the parent control
- **Canvas Space**: Relative to the drawing canvas
- **Screen Space**: Relative to the screen (used for touch input)

The system provides methods for converting between these spaces, making it easier to handle positioning and hit testing.

### Layout-Specific Properties

Layout controls have unique bindable properties that affect their behavior:

- **ColumnDefinitions/RowDefinitions**: Define grid structure
- **Spacing**: Controls space between items
- **Padding**: Controls space inside the layout edges
- **LayoutType**: Determines layout strategy
- **ItemsSource/ItemTemplate**: For data-driven layouts

## Performance Considerations

### Rendering Optimization

The rendering system is optimized using several techniques:

1. **Clipping**: Only renders content within visible bounds
2. **Caching**: Different caching strategies for balancing performance
3. **Background Processing**: Template initialization on background threads
4. **Incremental Loading**: Loading and measuring items incrementally

### When to Use Each Layout Type

- **Absolute**: When precise positioning is needed (graphs, custom visualizations)
- **Grid**: For tabular data and form layouts
- **Column/Row**: For sequential content in one direction
- **Wrap**: For content that should flow naturally across lines (tags, flow layouts)

## Debugging Layouts

For debugging layout issues, use these built-in features:

- Set `IsDebugRenderBounds` to `true` to visualize layout boundaries
- Use `SkiaLabelFps` to monitor rendering performance
- Add the `DebugRenderGraph` control to visualize the rendering tree

## Summary

DrawnUi.Maui's layout system provides a foundation for creating high-performance, visually consistent UIs across platforms. By understanding its architecture, you can leverage its capabilities to create custom layouts and optimize your application's performance.