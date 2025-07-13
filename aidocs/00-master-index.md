# DrawnUI AI-Optimized Documentation Index

## Quick Navigation

### Core Components
- [Canvas & Basic Setup](01-canvas-and-basic-setup.md) - Root container and initialization
- [SkiaLayout Container Patterns](02-skialayout-container-patterns.md) - Layout management and data binding
- [SkiaButton Interactive Controls](03-skiabutton-interactive-controls.md) - Button controls and interactions
- [SkiaImage Advanced Imaging](04-skiaimage-advanced-imaging.md) - Image display with effects and caching
- [SkiaScroll Virtualization](05-skiascroll-virtualization-patterns.md) - High-performance scrolling
- [SkiaShape Custom Graphics](06-skiashape-custom-graphics.md) - Vector graphics and custom shapes
- [Animations & Gestures](07-animations-and-gestures.md) - Interactive animations and touch handling
- [Custom Controls Patterns](08-custom-controls-patterns.md) - Building reusable components

### Advanced Programming Patterns
- [Code-Behind & Fluent Patterns](11-code-behind-and-fluent-patterns.md) - Real-world code-behind usage
- [Fluent API Comprehensive Guide](12-fluent-api-comprehensive-guide.md) - Advanced fluent API patterns



## Component Hierarchy

```
Canvas (Root)
├── SkiaLayout (Container)
│   ├── SkiaButton (Interactive)
│   ├── SkiaLabel (Text)
│   ├── SkiaImage (Media)
│   ├── SkiaShape (Graphics)
│   └── SkiaScroll (Scrolling)
│       └── SkiaLayout (Virtualized Content)
└── Custom Controls (Advanced)
```

## Tag Categories

### Component Tags
- `#canvas` - Root container setup and configuration
- `#skialayout` - Layout containers and arrangement
- `#skiabutton` - Interactive button controls
- `#skiaimage` - Image display and media handling
- `#skiascroll` - Scrolling and virtualization
- `#skiashape` - Vector graphics and custom shapes
- `#custom-controls` - Reusable component creation

### Complexity Levels
- `#basic` - Simple usage patterns, getting started
- `#intermediate` - Common scenarios with some complexity
- `#advanced` - Complex patterns requiring deep understanding

### Use Case Tags
- `#setup` - Initial configuration and project setup
- `#layout` - UI arrangement and positioning
- `#interactive` - User interactions and gestures
- `#media` - Images, graphics, and visual content
- `#performance` - Optimization and efficient rendering
- `#databinding` - MVVM patterns and data connection
- `#animations` - Motion and visual effects
- `#gestures` - Touch and input handling
- `#virtualization` - Large dataset handling
- `#caching` - Memory and performance optimization
- `#code-behind` - Code-behind patterns and event handling
- `#fluent-api` - Fluent API and method chaining patterns
- `#programmatic-creation` - Dynamic control creation
- `#property-observation` - Property watching and updates

### Platform Tags
- `#cross-platform` - Works on all platforms
- `#mobile-first` - Optimized for mobile devices
- `#desktop` - Desktop-specific features
- `#hardware-acceleration` - GPU-accelerated rendering

## Common Patterns Quick Reference

### Basic Setup Pattern
```csharp
// MauiProgram.cs
builder.UseDrawnUi(new() { MobileIsFullscreen = true });

// XAML
<draw:Canvas Gestures="Enabled" RenderingMode="Accelerated">
    <draw:SkiaLayout Type="Column">
        <!-- Content -->
    </draw:SkiaLayout>
</draw:Canvas>
```

### Data Binding Pattern
```xml
<draw:SkiaLayout 
    ItemsSource="{Binding Items}"
    RecyclingTemplate="Enabled">
    <draw:SkiaLayout.ItemTemplate>
        <DataTemplate>
            <!-- Template content -->
        </DataTemplate>
    </draw:SkiaLayout.ItemTemplate>
</draw:SkiaLayout>
```

### Performance Pattern
```xml
<draw:SkiaScroll Virtualisation="Enabled">
    <draw:SkiaLayout 
        Virtualisation="Enabled"
        UseCache="ImageComposite"
        RecyclingTemplate="Enabled">
        <!-- Large dataset content -->
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

### Animation Pattern
```csharp
await control.ScaleTo(1.1, 200);
await control.ScaleTo(1.0, 200);
```

### Fluent API Pattern
```csharp
SkiaButton actionButton;

var control = new SkiaButton("Click Me")
    .Assign(out actionButton)
    .OnTapped(async btn => await HandleClickAsync())
    .Height(44)
    .CenterX()
    .WithCache(SkiaCacheType.Operations);
```

### Property Observation Pattern
```csharp
new SkiaLabel()
    .Observe(viewModel, (label, prop) =>
    {
        if (prop.IsEither(nameof(BindingContext), nameof(ViewModel.Status)))
        {
            label.Text = viewModel.Status;
            label.TextColor = viewModel.IsOnline ? Colors.Green : Colors.Gray;
        }
    })
```

## Component Relationships

### Dependencies
- **Canvas** → Required for all DrawnUI controls
- **SkiaLayout** → Container for other controls
- **SkiaScroll** → Requires SkiaLayout for content
- **Custom Controls** → Inherit from SkiaControl or SkiaLayout

### Common Combinations
- **Canvas + SkiaLayout** → Basic page structure
- **SkiaScroll + SkiaLayout + ItemTemplate** → Lists and collections
- **SkiaButton + Animations** → Interactive feedback
- **SkiaImage + Effects** → Rich media display
- **SkiaShape + Gestures** → Custom interactive graphics

## Performance Guidelines

### Memory Optimization
- Use `RecyclingTemplate="Enabled"` for lists
- Enable `Virtualisation="Enabled"` for large datasets
- Set appropriate `UseCache` strategies
- Dispose custom controls properly

### Rendering Optimization
- Use `RenderingMode="Accelerated"` on Canvas
- Minimize complex layouts in ItemTemplates
- Use `MeasureItemsStrategy="MeasureAll"` for consistent items
- Cache complex graphics with `UseCache="ImageComposite"`

### Animation Performance
- Use hardware-accelerated properties (Transform, Opacity)
- Avoid animating layout-affecting properties
- Use `Easing` functions for smooth motion
- Cancel animations when not needed

## Common Anti-Patterns

### ❌ Performance Issues
```xml
<!-- Wrong: No virtualization for large lists -->
<draw:SkiaLayout ItemsSource="{Binding 10000Items}">
```

### ❌ Memory Leaks
```csharp
// Wrong: Not disposing resources
public class MyControl : SkiaControl
{
    private Timer _timer = new Timer(); // Never disposed
}
```

### ❌ Layout Problems
```xml
<!-- Wrong: No size specified -->
<draw:SkiaButton Text="Button" />
```

### ❌ Binding Issues
```xml
<!-- Wrong: Using both Click and Command -->
<draw:SkiaButton Clicked="OnClick" CommandTapped="{Binding Command}" />
```

## Getting Started Checklist

1. ✅ Add `UseDrawnUi()` to MauiProgram.cs
2. ✅ Create Canvas as root container
3. ✅ Use SkiaLayout for content arrangement
4. ✅ Specify dimensions for interactive controls
5. ✅ Enable virtualization for lists
6. ✅ Use appropriate caching strategies
7. ✅ Test on target platforms

## Advanced Topics

### Custom Drawing
- Inherit from SkiaControl
- Override OnDraw method
- Use SKCanvas for custom graphics

### Performance Profiling
- Monitor memory usage with large lists
- Profile animation frame rates
- Test scrolling performance

### Platform Integration
- Handle platform-specific gestures
- Adapt to different screen densities
- Consider platform UI guidelines

## Support and Resources

- **Documentation**: Check existing docs/ folder for detailed API reference
- **Samples**: Explore Sandbox project for working examples
- **Performance**: Use built-in profiling tools
- **Community**: Follow DrawnUI best practices and patterns
