# Advanced Gesture Handling in DrawnUi.Maui

DrawnUi.Maui provides a robust and extensible gesture system for building interactive, touch-driven UIs. This article covers all available gesture handling approaches, from simple taps to complex multi-touch interactions.

## Gesture System Overview
- **Unified gesture model** for tap, drag, swipe, pinch, long-press, and multi-touch
- **Multiple handling approaches** to fit different scenarios and coding styles
- **ConsumeGestures event handler** for code-behind gesture processing (latest approach)
- **AddGestures attached properties** for MVVM command binding
- **ISkiaGestureListener** interface for complex custom gesture logic
- **SkiaHotspot** for simple tap/click detection
- **Gesture locking and propagation control** for complex UI hierarchies

## Gesture Handling Approaches Comparison

### 1. ConsumeGestures Event Handler (Recommended for Code-Behind)

**Best for:** Code-behind gesture processing, complex animations, immediate response scenarios

```xml
<draw:SkiaShape 
    ConsumeGestures="OnShapeGestures"
    Type="Rectangle" 
    BackgroundColor="Blue" />
```

```csharp
private void OnShapeGestures(object sender, SkiaGesturesInfo e)
{
    if (e.Args.Type == TouchActionResult.Tapped)
    {
        e.Consumed = true; // Must be synchronous!
        
        // Start animations on background thread
        Task.Run(async () =>
        {
            await ((SkiaControl)sender).AnimateScaleTo(1.2, 150);
            await ((SkiaControl)sender).AnimateScaleTo(1.0, 150);
        });
    }
    
    if (e.Args.Type == TouchActionResult.Panning)
    {
        e.Consumed = true;
        var control = (SkiaControl)sender;
        control.TranslationX += e.Args.Event.Distance.Delta.X / control.RenderingScale;
        control.TranslationY += e.Args.Event.Distance.Delta.Y / control.RenderingScale;
    }
}
```

**Key Features:**
- Works on **any SkiaControl** without subclassing
- Event handler **must stay synchronous** for proper gesture consumption
- Use `Task.Run` for animations to avoid blocking gesture processing
- `e.Consumed = true` prevents gesture bubbling

### 2. AddGestures Attached Properties (Best for MVVM)

**Best for:** MVVM scenarios, binding to ViewModel commands, declarative gesture handling

```xml
<draw:SkiaShape
    draw:AddGestures.AnimationTapped="Ripple"
    draw:AddGestures.CommandTapped="{Binding TapCommand}"
    draw:AddGestures.CommandTappedParameter="{Binding CurrentItem}"
    draw:AddGestures.CommandLongPressing="{Binding LongPressCommand}"
    Type="Rectangle" 
    BackgroundColor="Green" />
```

**Available attached properties:**
- `CommandTapped` / `CommandTappedParameter`
- `CommandLongPressing` / `CommandLongPressingParameter` 
- `CommandPressed` / `CommandPressedParameter`
- `AnimationTapped` - Built-in animations: "Ripple", "Scale", "Fade"

**Key Features:**
- Perfect for **MVVM binding** to ViewModel commands
- Built-in **animation effects** without code
- Declarative approach keeps XAML clean
- Automatic parameter passing to commands

### 3. SkiaHotspot (Simple Tap Detection)

**Best for:** Simple tap/click scenarios, button-like behavior

```xml
<draw:SkiaHotspot 
    Tapped="OnTapped"
    CommandTapped="{Binding TapCommand}">
    <draw:SkiaShape Type="Circle" BackgroundColor="Red" />
</draw:SkiaHotspot>
```

```csharp
private void OnTapped(object sender, EventArgs e)
{
    // Simple tap handling
}
```

**Key Features:**
- Wraps content in a tappable area
- Both **event and command** support
- Simplest approach for basic interactions

### 4. ISkiaGestureListener Interface (Advanced Custom Logic)

**Best for:** Complex custom gesture logic, subclassed controls, performance-critical scenarios

Implement `ISkiaGestureListener` for advanced gestures:

```csharp
public class DraggableShape : SkiaShape, ISkiaGestureListener
{
    public override void OnParentChanged()
    {
        base.OnParentChanged();
        RegisterGestureListener(this);
    }
    
    public bool OnGestureEvent(TouchActionType type, TouchActionEventArgs args, TouchActionResult result)
    {
        if (result == TouchActionResult.Panning)
        {
            TranslationX += args.Distance.Delta.X / RenderingScale;
            TranslationY += args.Distance.Delta.Y / RenderingScale;
            return true; // Consume gesture
        }
        
        if (result == TouchActionResult.Tapped)
        {
            // Handle tap
            return true;
        }
        
        return false; // Don't consume
    }
}
```

**Key Features:**
- **Maximum performance** - Direct gesture handling without event overhead
- **Full control** over gesture processing and consumption
- **Requires subclassing** - Need to create custom control classes
- **Manual registration** required via `RegisterGestureListener()`

## When to Use Each Approach

| Scenario | Recommended Approach | Why |
|----------|---------------------|-----|
| Simple button tap | SkiaHotspot | Simplest, built-in command support |
| MVVM with commands | AddGestures | Clean XAML, automatic binding |
| Complex animations | ConsumeGestures | Code-behind flexibility, no subclassing |
| Drag-and-drop | ConsumeGestures | Real-time updates, easy implementation |
| Custom control | ISkiaGestureListener | Maximum performance, full control |
| List item interactions | AddGestures | MVVM-friendly, parameter binding |

## Advanced Gesture Patterns

### Multi-Touch and Pan Gestures with ConsumeGestures

```xml
<draw:SkiaShape 
    x:Name="PannableCard"
    ConsumeGestures="OnPanGestures"
    Type="Rectangle" 
    CornerRadius="12"
    BackgroundColor="Purple" />
```

```csharp
private void OnPanGestures(object sender, SkiaGesturesInfo e)
{
    var control = (SkiaControl)sender;
    
    switch (e.Args.Type)
    {
        case TouchActionResult.Panning:
            e.Consumed = true;
            // Real-time drag
            control.TranslationX += e.Args.Event.Distance.Delta.X / control.RenderingScale;
            control.TranslationY += e.Args.Event.Distance.Delta.Y / control.RenderingScale;
            
            // Add rotation based on pan direction
            var deltaX = e.Args.Event.Distance.Total.X / control.RenderingScale;
            control.Rotation = Math.Max(-15, Math.Min(15, deltaX * 0.1));
            break;
            
        case TouchActionResult.Up:
            e.Consumed = true;
            // Snap back animation
            Task.Run(async () =>
            {
                await control.TranslateToAsync(0, 0, 200, Easing.SpringOut);
                await control.RotateToAsync(0, 150, Easing.SpringOut);
            });
            break;
    }
}
```

### MVVM Command Binding with AddGestures

```xml
<!-- In a list item or collection -->
<draw:SkiaDrawnCell
    draw:AddGestures.CommandTapped="{Binding Source={x:Reference ParentList}, Path=BindingContext.SelectItemCommand}"
    draw:AddGestures.CommandTappedParameter="{Binding .}"
    draw:AddGestures.CommandLongPressing="{Binding Source={x:Reference ParentList}, Path=BindingContext.ShowContextMenuCommand}"
    draw:AddGestures.AnimationTapped="Scale">
    
    <draw:SkiaLayout Type="Row" Spacing="12">
        <draw:SkiaImage Source="{Binding ImageUrl}" />
        <draw:SkiaLabel Text="{Binding Title}" />
    </draw:SkiaLayout>
</draw:SkiaDrawnCell>
```

```csharp
// In your ViewModel
public ICommand SelectItemCommand => new Command<ItemModel>(item => 
{
    SelectedItem = item;
    // Navigate or perform action
});

public ICommand ShowContextMenuCommand => new Command<ItemModel>(item =>
{
    // Show context menu for item
});
```

## Gesture Locking and Propagation

Use the `LockChildrenGestures` property to control gesture propagation:

- `LockTouch.Enabled`: Prevents children from receiving gestures
- `LockTouch.Disabled`: Allows all gestures to propagate
- `LockTouch.PassTap`: Only tap events pass through
- `LockTouch.PassTapAndLongPress`: Tap and long-press pass through

Example:

```xml
<draw:SkiaLayout LockChildrenGestures="PassTap">
    <!-- Only tap gestures reach children -->
</draw:SkiaLayout>
```

## Custom Gesture Handling in Code

Override `OnGestureEvent` for fine-grained control:

```csharp
public override ISkiaGestureListener OnGestureEvent(TouchActionType type, TouchActionEventArgs args, TouchActionResult result, SKPoint childOffset, SKPoint childOffsetDirect)
{
    // Custom logic for gesture routing or handling
    return base.OnGestureEvent(type, args, result, childOffset, childOffsetDirect);
}
```

## Multi-Touch and Pinch-to-Zoom

Listen for pinch and multi-touch events:

```csharp
public bool OnGestureEvent(TouchActionType type, TouchActionEventArgs args, TouchActionResult result)
{
    if (type == TouchActionType.Pinch)
    {
        // args.PinchScale, args.Center, etc.
        // Handle zoom
        return true;
    }
    return false;
}
```

## Gesture Utilities and Best Practices
- Use `HadInput` to track which listeners have received input
- Use `InputTransparent` to make controls ignore gestures
- For performance, avoid deep gesture listener hierarchies
- Use debug logging to trace gesture flow

## Example: Swipe-to-Delete List Item

```xml
<draw:SkiaLayout ItemsSource="{Binding Items}">
    <draw:SkiaLayout.ItemTemplate>
        <DataTemplate>
            <local:SwipeToDeleteItem />
        </DataTemplate>
    </draw:SkiaLayout.ItemTemplate>
</draw:SkiaLayout>
```

```csharp
public class SwipeToDeleteItem : SkiaLayout, ISkiaGestureListener
{
    public override void OnParentChanged()
    {
        base.OnParentChanged();
        RegisterGestureListener(this);
    }
    public bool OnGestureEvent(TouchActionType type, TouchActionEventArgs args, TouchActionResult result)
    {
        if (type == TouchActionType.Pan && result == TouchActionResult.Panning)
        {
            // Move item horizontally
            this.TranslationX = args.Location.X;
            Invalidate();
            return true;
        }
        if (type == TouchActionType.Pan && result == TouchActionResult.Up)
        {
            if (Math.Abs(this.TranslationX) > 100)
            {
                // Trigger delete
                // ...
            }
            this.TranslationX = 0;
            Invalidate();
            return true;
        }
        return false;
    }
}
```

## Migration Guide

### From ISkiaGestureListener to ConsumeGestures

**Old approach (still works):**
```csharp
public class MyControl : SkiaShape, ISkiaGestureListener
{
    public override void OnParentChanged()
    {
        base.OnParentChanged();
        RegisterGestureListener(this);
    }
    
    public bool OnGestureEvent(TouchActionType type, TouchActionEventArgs args, TouchActionResult result)
    {
        // Handle gestures
        return true;
    }
}
```

**New approach (recommended):**
```xml
<draw:SkiaShape ConsumeGestures="OnGestures" />
```

```csharp
private void OnGestures(object sender, SkiaGesturesInfo e)
{
    // Handle gestures - no subclassing needed!
    e.Consumed = true;
}
```

## Performance Best Practices

1. **Use ConsumeGestures for most scenarios** - No subclassing overhead
2. **Keep gesture handlers synchronous** - Use `Task.Run` for animations
3. **Consume gestures early** - Set `e.Consumed = true` to prevent bubbling
4. **Use AddGestures for MVVM** - Cleaner than command parameters in code-behind
5. **Avoid deep gesture hierarchies** - Can impact performance in complex layouts
6. **Use gesture locking wisely** - Prevent unwanted gesture conflicts

## Troubleshooting

### Gestures Not Working
- ✅ Ensure Canvas has `Gestures="Enabled"`
- ✅ Check if parent controls are consuming gestures
- ✅ Verify `e.Consumed = true` is set when needed
- ✅ Use debug logging to trace gesture flow

### Performance Issues
- ✅ Avoid synchronous animations in gesture handlers
- ✅ Use appropriate caching (`UseCache="Image"` for complex animations)
- ✅ Minimize gesture listener count in lists
- ✅ Profile gesture handling with platform tools

### Gesture Conflicts
- ✅ Use `LockChildrenGestures` to control propagation
- ✅ Set `e.Consumed = true` to stop gesture bubbling
- ✅ Check gesture processing order (highest Z-index first)

## Summary

DrawnUi.Maui offers multiple gesture handling approaches to fit different scenarios:

- **ConsumeGestures** - Modern, flexible, no subclassing required
- **AddGestures** - Perfect for MVVM command binding
- **SkiaHotspot** - Simple tap detection
- **ISkiaGestureListener** - Maximum performance for custom controls

Choose the approach that best fits your architecture and requirements. The ConsumeGestures event handler is recommended for most new development due to its flexibility and ease of use.