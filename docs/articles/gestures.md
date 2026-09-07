# Handling Gestures in DrawnUI

DrawnUI gesture docs have two layers:

1. the canvas host must be configured to accept interaction
2. the control tree decides what to do with taps, pans, long presses, and commands

If you skip the first layer, control handlers can be correct and still never fire.

## Start with the canvas host

### MAUI `Canvas`

In MAUI, set the `Gestures` property on the `Canvas` that hosts your DrawnUI tree.

Common values:

- `Enabled` for normal interactive UI
- `SoftLock` when the canvas lives inside a native `ScrollView` and you need DrawnUI panning controls to cooperate with native scrolling
- `Lock` when the DrawnUI surface should fully capture the input stream

Minimal MAUI example:

```xml
<draw:Canvas
    Gestures="Enabled"
    HorizontalOptions="Fill"
    VerticalOptions="Fill">
    <draw:SkiaLayout Type="Column" Padding="24" Spacing="12">
        <draw:SkiaLabel Text="Canvas gestures enabled" FontSize="22" />
        <draw:SkiaButton Text="Tap me" Clicked="OnButtonClicked" />
    </draw:SkiaLayout>
</draw:Canvas>
```

Without `Gestures="Enabled"` or another non-disabled mode, your DrawnUI controls do not receive touch interaction through that canvas.

### Blazor WebAssembly `Canvas`

Browser-side Blazor uses the same idea, but the property is passed as `GesturesMode`.

Minimal Blazor example:

```razor
@using DrawnUi.Draw
@using DrawnUi.Views

<Canvas RootControl="@RootControl"
        WidthRequest="400"
        HeightRequest="220"
        Gestures="@GesturesMode.Enabled" />

@code {
    private readonly SkiaControl RootControl = new SkiaLayout()
    {
        Margin = new Thickness(16),
        Type = LayoutType.Column,
        Spacing = 12,
        Children =
        {
            new SkiaLabel { Text = "Blazor canvas gestures enabled", FontSize = 22 },
            new SkiaButton("Increment").OnTapped(_ => clickCount++)
        }
    };

    private int clickCount;
}
```

For browser-side `Canvas`, the default is effectively disabled until you opt in with `GesturesMode.Enabled` or `GesturesMode.Lock`.

### Blazor Server `Canvas`

Blazor Server is different.

The server-backed `Canvas` does not expose the same `Gestures` parameter as browser-side `Canvas`. The surface is rendered on the server and interactions are routed back to the server through the server runtime.

That means:

- you do not enable gestures with a `Gestures` property on the server `Canvas`
- you still wire gesture-aware controls and handlers inside the DrawnUI tree
- the same control-level concepts apply, but the host model is the server-backed `Canvas`, not browser `Canvas`

Minimal server-side example:

```razor
<Canvas Content="@BuildCanvasContent()"
    WidthRequest="400"
    HeightRequest="240"
    Alt="DrawnUI server-rendered sample" />

@code {
    private int clickCount;

    private SkiaControl BuildCanvasContent()
    {
        return new SkiaLayout()
        {
            Type = LayoutType.Column,
            Spacing = 12,
            Children =
            {
                new SkiaLabel { Text = "Server Canvas sample", FontSize = 24 },
                new SkiaButton("Increment").OnTapped(_ => clickCount++)
            }
        };
    }
}
```

So yes, the same control-level gesture patterns still apply to Blazor Server, but the canvas-level enablement step is specific to MAUI `Canvas` and browser-side Blazor `Canvas`.

### `DrawnUi.Net`

`DrawnUi.Net` is different again because it is not a UI framework host by itself.

There is no MAUI `Canvas`, browser `Canvas`, or server-backed Blazor `Canvas` that automatically captures pointer input for you. `DrawnUi.Net` gives you the DrawnUI rendering and layout model, but the outer host or harness is responsible for delivering input.

That means:

- there is no canvas-level `Gestures` property to enable in `DrawnUi.Net`
- control-level gesture logic still works once your host forwards pointer or touch input into the DrawnUI tree
- in a headless rendering harness, there may be no live gesture stream at all unless you explicitly simulate or inject it

Use `DrawnUi.Net` for gesture-related work when you want to:

- validate shared gesture state transitions in a controlled harness
- reproduce a gesture bug outside MAUI or Blazor noise
- test the DrawnUI control logic after input has already been normalized by your own host

So the rule is:

- MAUI and browser Blazor need host-level gesture enablement on the canvas surface
- Blazor Server uses its server-backed `Canvas` host behavior instead of a `Gestures` property
- `DrawnUi.Net` depends on whatever outer host or test harness you build around it to feed interaction into DrawnUI

## Then wire control-level gestures

Once the host surface is configured correctly, choose the control-level pattern that matches the job.

## `ConsumeGestures` for raw gesture handling

Use `ConsumeGestures` when you want to inspect tap, pan, long press, or release events directly.

```xml
<draw:SkiaShape
    x:Name="MyCard"
    Type="Rectangle"
    CornerRadius="20"
    ConsumeGestures="OnCardGestures">

    <!-- Your content here -->
</draw:SkiaShape>
```

```csharp
private void OnCardGestures(object sender, SkiaGesturesInfo e)
{
    if (sender is not SkiaControl control)
    {
        return;
    }

    switch (e.Args.Type)
    {
        case TouchActionResult.Tapped:
            e.Consumed = true;
            Task.Run(async () =>
            {
                await control.ScaleTo(1.05, 80);
                await control.ScaleTo(1.0, 80);
            });
            break;

        case TouchActionResult.Panning:
            e.Consumed = true;
            control.TranslationX += e.Args.Event.Distance.Delta.X / control.RenderingScale;
            control.TranslationY += e.Args.Event.Distance.Delta.Y / control.RenderingScale;
            break;

        case TouchActionResult.Up:
            e.Consumed = true;
            Task.Run(async () => await control.TranslateToAsync(0, 0, 180));
            break;
    }
}
```

Key points:

- `ConsumeGestures` gives you raw gesture events on the control
- check `e.Args.Type` for `Tapped`, `Panning`, `Up`, `LongPressing`, and related states
- set `e.Consumed = true` when you want to stop propagation
- keep the handler synchronous; if you animate, start async work from inside it

## `SkiaButton` for button-like taps

For button-style interaction, prefer the higher-level button APIs.

### XAML event handler

```xml
<draw:SkiaButton
    Text="Click Me"
    Tapped="OnButtonTapped" />
```

```csharp
private void OnButtonTapped(object sender, ControlTappedEventArgs e)
{
    // Handle button click
}
```

### Commands with `AddGestures`

```xml
<draw:SkiaButton
    Text="Click Me"
    draw:AddGestures.CommandTapped="{Binding MyCommand}" />
```

## MVVM shape or layout gestures

For non-button surfaces, attach commands to shapes and layouts:

```xml
<draw:SkiaShape
    Type="Rectangle"
    draw:AddGestures.CommandTapped="{Binding SelectItemCommand}"
    draw:AddGestures.CommandTappedParameter="{Binding .}"
    draw:AddGestures.AnimationTapped="Scale">

    <draw:SkiaLabel Text="{Binding Name}" />
</draw:SkiaShape>
```

Built-in `AnimationTapped` values include:

- `Scale`
- `Ripple`
- `Fade`

## Mouse buttons and the browser context menu

Every mouse button goes through the gesture pipeline: a right or middle click is a normal `Down` / `Up` / `Tapped`
with the button in `e.Parameters.Event.Pointer` (`Button`, `ButtonNumber`, `DeviceType`, `PressedButtons`), on every
platform, so games and custom controls keep their button data. Nothing in DrawnUI filters buttons; the app decides.

On the web heads (Blazor, `DrawnUi.Wasm`) a right click does two more things a desktop app never sees: the browser
opens its own menu over the canvas ("Save image as…", "Copy image"), and on touch a long press raises the same
request. Two habits cover it:

### 1. Act on the primary button only where that is what you mean

A `Tapped` handler that opens a page or submits a form should ignore the other buttons:

```csharp
new SkiaShape { ... }
    .OnTapped((shape, e) =>
    {
        var button = e.Parameters.Event.Pointer?.Button ?? MouseButton.Left; // touch and pen have no button
        if (button != MouseButton.Left)
            return;

        OpenDetails();
    });
```

The one-argument `OnTapped(view => …)` overload has no args; use the `(view, e)` overload when the button matters.

### 2. Handle `ContextMenu` where you want your own menu

`ContextMenu` is a delegate that answers, like `OnGestures`, the same shape as in DrawnUi.React: return `true` to
take the request, the browser's menu is then suppressed; `false` or no handler and the browser menu shows as usual.

```csharp
new SkiaShape { ... }
    .OnContextMenu((shape, e) =>
    {
        ShowMyMenu(e.Local);   // point inside the control, in points
        return true;           // handled: the browser menu is suppressed
    });

// or directly
shape.ContextMenu = (sender, e) => { ShowMyMenu(e.Local); return true; };

// swallow the browser menu on a whole page: a handler on the root layout, nothing hit below it answers
page.ContextMenu = (_, _) => true;
```

It is a delegate property, not a C# event: one handler per control, set from code (no XAML attribute).

The request is routed like a tap: the deepest hit control first (through transforms and scroll offsets), then its
parents; the first handler returning `true` wins. `ContextMenuEventArgs` carries `Source` (`Mouse` for a right
click, `Touch` for a long press, `Keyboard` for the Menu key), `Local`, and the usual `Parameters` /
`ProcessingInfo` of a tap. Under the hood it is the `TouchActionResult.ContextMenu` gesture (AppoMobi.Gestures
3.11.1), so `ConsumeGestures` and `ProcessGestures` overrides see it too. A right click therefore arrives twice, as a
`Tapped` with `Button == MouseButton.Right` and as a `ContextMenu`; that is by design, the two answer different
questions. Other platforms never raise `ContextMenu`.

Per head:

- **Blazor** (`DrawnUi.Blazor.Wasm`): wired by `AppoMobi.Blazor.Gestures`. Up to 3.10.x the browser menu was always
  suppressed over the canvas; from 3.11.1 it shows unless a handler takes it. Blazor Server keeps suppressing it (the
  decision needs the synchronous JS → .NET call that WebAssembly has).
- **`DrawnUi.Wasm`**: `RunAsync` wires it; hand-written `main.js` files pass `onContextMenu: Input.OnContextMenu` in
  `setModuleExports`, see [DrawnUi.Wasm](web/index.md#gestures).
- **Blazor sandbox**: the Gestures page (`/canvas-gestures`) shows both habits, taps print their button and the pad
  takes the context menu while the card around it leaves the browser menu alone.

Selectable text in the accessibility overlay (when the app opts a label in) belongs to the browser: a right click on
selected text shows the browser's own copy menu, never a DrawnUI `ContextMenu`.

## Gesture locking and propagation

Use `LockChildrenGestures` when a parent layout should decide which gestures reach nested controls.

```xml
<draw:SkiaLayout Type="Column" LockChildrenGestures="PassTap">
    <draw:SkiaShape Type="Rectangle" ConsumeGestures="OnTap" />
</draw:SkiaLayout>
```

## Practical routing

Use this rule of thumb:

- configure the host first: `Canvas.Gestures` in MAUI or `GesturesMode` in browser Blazor
- use `SkiaButton` events or commands for button-like actions
- use `ConsumeGestures` when you need low-level gesture state such as pan, long press, or release
- on Blazor Server, use `Canvas` plus the same control-level handlers inside the DrawnUI tree

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

On the web heads a long press on touch also raises `ContextMenu` (see [Mouse buttons and the browser context menu](#mouse-buttons-and-the-browser-context-menu)); handle that one there so the browser's menu does not open over yours.

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

For additional gesture utilities, see the helper methods in [Canvas.cs](https://github.com/DrawnUi/DrawnUi.Net.Maui/blob/main/src/Maui/DrawnUi/Views/Canvas.cs#L1) and [SkiaControl.Shared.cs](https://github.com/DrawnUi/DrawnUi.Net.Maui/blob/main/src/Shared/Draw/Base/SkiaControl.Shared.cs#L1) for `GetGesturePositionInsideControl()`, `GetGesturePositionInsideChild()`, and `CheckChildGestureHit()`.