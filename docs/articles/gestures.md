# Handling Gestures in DrawnUI

DrawnUI provides multiple ways to handle touch gestures. This guide shows real-world patterns used in the tutorials and sandbox apps.

## Quick Start: The ConsumeGestures Pattern

The simplest and most common pattern for handling gestures:

```xml
<draw:SkiaShape 
    x:Name="MyCard"
    Type="Rectangle"
    CornerRadius="20"
    ConsumeGestures="OnCardTapped">
    
    <!-- Your content here -->
</draw:SkiaShape>
```

```csharp
private void OnCardTapped(object sender, SkiaGesturesInfo e)
{
    if (e.Args.Type == TouchActionResult.Tapped)
    {
        e.Consumed = true; // Consume the gesture
        
        // Animations run async to avoid blocking
        Task.Run(async () =>
        {
            var control = (SkiaControl)sender;
            await control.ScaleTo(1.1, 100);
            await control.ScaleTo(1.0, 100);
        });
    }
}
```

**Key points:**
- `ConsumeGestures` attribute specifies the handler method
- Check `e.Args.Type` for gesture type: `Tapped`, `Panning`, `Up`, etc.
- Set `e.Consumed = true` to prevent gesture propagation
- **Always** use `Task.Run` for async animations - don't await in the handler
- Handler **must be synchronous** to consume properly

## Real-World Example: Interactive Card

From the Tutorials app:

```xml
<draw:SkiaShape
    x:Name="Card1"
    Type="Rectangle"
    CornerRadius="20"
    WidthRequest="300"
    HeightRequest="180"
    ConsumeGestures="OnCardGestures">
    
    <draw:SkiaControl.FillGradient>
        <draw:SkiaGradient Type="Linear" Angle="45">
            <draw:SkiaGradient.Colors>
                <Color>#667eea</Color>
                <Color>#764ba2</Color>
            </draw:SkiaGradient.Colors>
        </draw:SkiaGradient>
    </draw:SkiaControl.FillGradient>
    
    <draw:SkiaLayout Type="Column" Margin="24" Spacing="12">
        <draw:SkiaRichLabel
            Text="🎨 Gradient Card"
            FontSize="20"
            FontAttributes="Bold"
            TextColor="White" />
        <draw:SkiaLabel
            Text="Tap to animate!"
            FontSize="12"
            TextColor="#ccccff" />
    </draw:SkiaLayout>
</draw:SkiaShape>
```

```csharp
private void OnCardGestures(object sender, SkiaGesturesInfo e)
{
    if (sender is SkiaControl control)
    {
        if (e.Args.Type == TouchActionResult.Tapped)
        {
            e.Consumed = true;
            
            Task.Run(async () =>
            {
                // Brighten gradient colors on tap
                if (control is SkiaShape shape && shape.FillGradient is SkiaGradient gradient)
                {
                    var original = gradient.Colors[0];
                    var lighter = Color.FromRgba(
                        Math.Min(1, original.Red * 1.5),
                        Math.Min(1, original.Green * 1.5),
                        Math.Min(1, original.Blue * 1.5),
                        original.Alpha);
                    
                    gradient.Colors = new List<Color> { lighter, lighter };
                    await Task.Delay(200);
                    gradient.Colors = new List<Color> { original, original };
                }
            });
        }
    }
}
```

## Gesture Types

The `e.Args.Type` field tells you what happened:

- **`Tapped`**: Single tap/click
- **`DoubleTapped`**: Double tap
- **`LongPressing`**: Long press detected
- **`Panning`**: Dragging/swiping
- **`Up`**: Touch released (end of interaction)
- **`Holding`**: Touch held down

Example with multiple gesture types:

```csharp
private void OnGestures(object sender, SkiaGesturesInfo e)
{
    var control = (SkiaControl)sender;
    
    switch (e.Args.Type)
    {
        case TouchActionResult.Tapped:
            e.Consumed = true;
            Task.Run(async () => await control.AnimateScaleTo(1.1, 100));
            break;
            
        case TouchActionResult.LongPressing:
            e.Consumed = true;
            // Show context menu
            break;
            
        case TouchActionResult.Panning:
            e.Consumed = true;
            // Drag the control
            control.TranslationX += e.Args.Event.Distance.Delta.X / control.RenderingScale;
            control.TranslationY += e.Args.Event.Distance.Delta.Y / control.RenderingScale;
            break;
            
        case TouchActionResult.Up:
            e.Consumed = true;
            // End gesture - snap back to position
            Task.Run(async () => await control.TranslateToAsync(0, 0, 200));
            break;
    }
}
```

## Button Taps: Using SkiaButton

For buttons, you can use the built-in `Tapped` event:

```xml
<draw:SkiaButton 
    Text="Click Me"
    Tapped="OnButtonTapped" />
```

```csharp
private void OnButtonTapped(object sender, ControlTappedEventArgs e)
{
    // Handle button click
    DisplayAlert("Tapped", "Button was tapped!", "OK");
}
```

Or use MVVM binding with `AddGestures`:

```xml
<draw:SkiaButton 
    Text="Click Me"
    draw:AddGestures.CommandTapped="{Binding MyCommand}" />
```

## Drag and Pan Operations

For dragging/panning touch:

```csharp
private void OnPan(object sender, SkiaGesturesInfo e)
{
    var control = (SkiaControl)sender;
    
    if (e.Args.Type == TouchActionResult.Panning)
    {
        e.Consumed = true;
        
        // Update position in real-time
        var deltaX = e.Args.Event.Distance.Delta.X / control.RenderingScale;
        var deltaY = e.Args.Event.Distance.Delta.Y / control.RenderingScale;
        
        control.TranslationX += deltaX;
        control.TranslationY += deltaY;
    }
    else if (e.Args.Type == TouchActionResult.Up)
    {
        e.Consumed = true;
        
        // Animate back to rest position
        Task.Run(async () =>
        {
            await control.TranslateToAsync(0, 0, 300, Easing.SpringOut);
        });
    }
}
```

## MVVM Pattern with Commands

For MVVM applications:

```xml
<draw:SkiaShape 
    Type="Rectangle"
    draw:AddGestures.CommandTapped="{Binding SelectItemCommand}"
    draw:AddGestures.CommandTappedParameter="{Binding .}"
    draw:AddGestures.AnimationTapped="Scale">
    
    <draw:SkiaLabel Text="{Binding Name}" />
</draw:SkiaShape>
```

```csharp
// In your ViewModel
public Command<ItemModel> SelectItemCommand { get; }

public MyViewModel()
{
    SelectItemCommand = new Command<ItemModel>(item =>
    {
        SelectedItem = item;
        // Navigate or perform action
    });
}
```

**Built-in animations for `AnimationTapped`:**
- `"Scale"` - Scale up then down
- `"Ripple"` - Ripple effect
- `"Fade"` - Fade in/out

## Gesture Locking and Propagation

Use `LockChildrenGestures` to control which gestures reach nested controls:

```xml
<draw:SkiaLayout Type="Column" LockChildrenGestures="PassTap">
    <!-- Only tap gestures reach children, pan/swipe don't -->
    <draw:SkiaShape Type="Rectangle" ConsumeGestures="OnTap" />
</draw:SkiaLayout>
```

Options:
- `Enabled`: Children can't receive gestures
- `Disabled`: All gestures pass through (default)
- `PassTap`: Only tap/click events reach children
- `PassTapAndLongPress`: Tap and long-press pass through

## Common Patterns

### Tap Feedback (Scale Animation)

```csharp
private void OnTap(object sender, SkiaGesturesInfo e)
{
    if (e.Args.Type == TouchActionResult.Tapped)
    {
        e.Consumed = true;
        
        Task.Run(async () =>
        {
            var control = (SkiaControl)sender;
            await control.ScaleTo(0.95, 100);
            await control.ScaleTo(1.0, 100);
        });
    }
}
```

### Swipe Detection

```csharp
private void OnSwipe(object sender, SkiaGesturesInfo e)
{
    if (e.Args.Type == TouchActionResult.Up)
    {
        e.Consumed = true;
        
        // Check swipe distance and direction
        var totalDistance = e.Args.Event.Distance.Total;
        
        if (Math.Abs(totalDistance.X) > 100 && Math.Abs(totalDistance.X) > Math.Abs(totalDistance.Y))
        {
            // Horizontal swipe
            if (totalDistance.X > 0)
            {
                // Swiped right
            }
            else
            {
                // Swiped left
            }
        }
    }
}
```

### Long Press Menu

```csharp
private void OnLongPress(object sender, SkiaGesturesInfo e)
{
    if (e.Args.Type == TouchActionResult.LongPressing)
    {
        e.Consumed = true;
        
        // Show context menu at gesture location
        var position = new Point(e.Args.Event.Location.X, e.Args.Event.Location.Y);
        ShowContextMenu(position);
    }
}
```

## Key Takeaways

1. **Use `ConsumeGestures` for most UI interactions** - It's simple, clean, and no subclassing required
2. **Keep handlers synchronous** - Always use `Task.Run()` for async work like animations
3. **Check gesture type with `e.Args.Type`** - This tells you what action occurred (Tapped, Panning, Up, etc.)
4. **Set `e.Consumed = true`** - This prevents the gesture from bubbling to parent controls
5. **Access gesture data from `e.Args.Event`** - Location, distance, pinch scale all available here
6. **Use `AddGestures` for MVVM** - When you need command binding instead of code-behind
7. **Use `LockChildrenGestures` to manage propagation** - Control which gestures reach nested controls

For additional gesture utilities, see the helper methods in [Canvas.cs](../../src/Maui/DrawnUi/Views/Canvas.cs#L1) and [SkiaControl.Shared.cs](../../src/Shared/Draw/Base/SkiaControl.Shared.cs#L1) for `GetGesturePositionInsideControl()`, `GetGesturePositionInsideChild()`, and `CheckChildGestureHit()`.