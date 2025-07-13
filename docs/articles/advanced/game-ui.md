# Building Game UIs and Interactive Games with DrawnUI

DrawnUi is not just for business apps — with direct SkiaSharp rendering, real-time animation, and flexible input handling, you can create everything from simple arcade games to rich, animated dashboards.

Mainly tested for  scenarios 

## Why Use DrawnUi for 2D-Games?

- **True cross-platform**  
   No more Windows-only, your game will look and run the same way on Apple and Android devices along with Windows and Mac desktops, with sound and all the goodies .NET MAUI provides for those platforms. 
- **High-performance canvas rendering**   
   On most platforms DrawnUi uses its own hardware-accelerated views handlers to ensure smooth display-synched multi-buffered rendering. They also provide an experimental "retained feature" (Canvas property `RenderingMode=RenderingModeType.AcceleratedRetained`) that on every frame returns to you the previous frame result for you to draw on top, instead of an empty surface. This can be useful for old-shool game engines that save/restore background for moving sprites, and other similar scenarios, for drawing only changed areas and obtain max performance.
- **Frame-based animation controls**  
   You can use built-in frame-based contols like SkiaSprite, SkiaLottie, SkiaGif (create your own) to easily use your existing art.
- **Drawing primitives**  
   You can use SkiaShape, SkiaLabel, SkiaImage, and other controls to build your game UI in a fast and declarative way.
- **Flexible input**  
   Canvas supports all multi-touch gestures, desktop mouse and keyboard. Controllers are on the roadmap.
- **Custom drawing**  
   You can override `Paint` inside any control and use all the power of SkiaSharp to draw directly on the canvas.
- **Easy integration**  
    with other .NET MAUI native controls and layouts to use-platform-specific features.

## Game Loop and Real-Time Updates

For interactive games, you need a game loop that updates the game state and redraws the UI at regular intervals.

### Example: Simple Game Loop

```csharp
public class GamePage : SkiaLayout
{
    private bool _running;
    private Timer _timer;
    private int _playerX = 100;
    private int _playerY = 100;

    public GamePage()
    {
        // Start the game loop
        _running = true;
        _timer = new Timer(OnTick, null, 0, 16); // ~60 FPS
    }

    private void OnTick(object state)
    {
        if (!_running) return;
        // Update game state
        _playerX += 1;
        // Redraw
        Invalidate();
    }

    protected override void OnDraw(SKCanvas canvas, SKRect destination, float scale)
    {
        base.OnDraw(canvas, destination, scale);
        // Draw player as a circle
        canvas.DrawCircle(_playerX, _playerY, 20, new SKPaint { Color = SKColors.Blue });
    }

    protected override void OnDisposing()
    {
        _running = false;
        _timer?.Dispose();
        base.OnDisposing();
    }
}
```

## Using SkiaSprite for Animated Characters

SkiaSprite makes it easy to animate sprite sheets:

```xml
<DrawUi:SkiaSprite
    x:Name="PlayerSprite"
    Source="character_run.png"
    Columns="8"
    Rows="1"
    FramesPerSecond="12"
    AutoPlay="True"
    WidthRequest="128"
    HeightRequest="128"
    HorizontalOptions="Center"
    VerticalOptions="Center" />
```

In code-behind, you can control animation state:

```csharp
PlayerSprite.Start(); // Start animation
PlayerSprite.Stop();  // Stop animation
PlayerSprite.CurrentFrame = 0; // Set frame
```

## Handling Input: Tap, Drag, and Gestures

DrawnUi.Maui supports rich gesture handling for interactive games:

```xml
<DrawUi:SkiaHotspot Tapped="OnPlayerTapped">
    <DrawUi:SkiaSprite ... />
</DrawUi:SkiaHotspot>
```

In code-behind:

```csharp
private void OnPlayerTapped(object sender, EventArgs e)
{
    // Respond to tap (e.g., jump, attack)
}
```

For drag or swipe, use gesture listeners or override touch methods in your control.

## Combining UI and Game Elements

You can mix game elements with standard DrawnUi controls:

```xml
<draw:SkiaLayout Type="Column">
    <draw:SkiaLabel Text="Score: 123" FontSize="24" />
    <draw:SkiaSprite ... />
    <draw:SkiaButton Text="Pause" Clicked="OnPause" />
</draw:SkiaLayout>
```

## Example: Simple Tap Game

```xml
<draw:SkiaLayout>
    <draw:SkiaHotspot Tapped="OnTap">
        <draw:SkiaShape Type="Circle" WidthRequest="100" HeightRequest="100" BackgroundColor="Red" />
    </draw:SkiaHotspot>
    <draw:SkiaLabel x:Name="ScoreLabel" Text="Score: 0" FontSize="24" />
</draw:SkiaLayout>
```

```csharp
private int _score = 0;
private void OnTap(object sender, SkiaGesturesParameters e)
{
    _score++;
    ScoreLabel.Text = $"Score: {_score}";
}
```

## Tips for Game UI Performance
- Use `Cache="Operations"` or `Cache="Image"` for static backgrounds or UI elements
- Minimize redraws: only call `Invalidate()` when needed
- Use SkiaLabelFps to monitor frame rate
- For complex games, manage game state and rendering in a dedicated class

## Integrating Addons (Camera, Maps, Charts)
- Use DrawnUi.Maui.Camera for AR or camera-based games
- Overlay charts or live data with DrawnUi.Maui.LiveCharts
- Add maps or location-based features with DrawnUi.Maui.MapsUi

## Summary
DrawnUi.Maui enables you to build interactive, animated, and performant game UIs on any platform. Combine sprites, custom drawing, and flexible input to create unique experiences—whether for games, dashboards, or playful business apps.
