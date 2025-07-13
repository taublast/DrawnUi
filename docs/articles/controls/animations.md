# Animation Controls

DrawnUi.Maui provides powerful controls for displaying animations directly on the canvas with high performance. This article covers the animation controls available in the framework.

## Animation Basics

All animation controls in DrawnUi.Maui share common functionality through the `AnimatedFramesRenderer` base class. This provides consistent playback control and event handling across different animation types.

Common properties and methods include:

| Property | Type | Description |
|----------|------|-------------|
| `AutoPlay` | bool | Automatically start animation when loaded |
| `IsPlaying` | bool | Indicates if animation is currently playing |
| `Repeat` | int | Number of times to repeat (-1 for infinite looping) |
| `SpeedRatio` | double | Animation playback speed multiplier |
| `DefaultFrame` | int | Frame to display when not playing |

Common methods:
- `Start()` - Begin or resume the animation
- `Stop()` - Pause the animation
- `Seek(frame)` - Jump to a specific frame

Common events:
- `Started` - Fires when animation begins
- `Finished` - Fires when animation completes

## SkiaGif

SkiaGif is a control for displaying animated GIF images with precise frame timing and control.

### Basic Usage

```xml
<DrawUi:SkiaGif
    Source="animated.gif"
    WidthRequest="200"
    HeightRequest="200"
    AutoPlay="True"
    Repeat="-1" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Source` | string | Path or URL to the GIF file |
| `Animation` | GifAnimation | Internal animation data (automatically created) |

### Loading Sources

SkiaGif can load animations from various sources:

```csharp
// From app resources
myGif.Source = "embedded_resource.gif";

// From file system
myGif.Source = "file:///path/to/animation.gif";

// From URL
myGif.Source = "https://example.com/animation.gif";
```

### Controlling Playback

```csharp
// Start the animation
myGif.Start();

// Stop at current frame
myGif.Stop();

// Jump to specific frame
myGif.Seek(5);

// Get total frames
int total = myGif.Animation?.TotalFrames ?? 0;
```

## SkiaLottie

SkiaLottie is a control for displaying [Lottie animations](https://airbnb.io/lottie/), which are vector-based animations exported from Adobe After Effects. It provides smooth, resolution-independent animations with additional customization options.

### Basic Usage

```xml
<draw:SkiaLottie
    Source="animation.json"
    WidthRequest="200"
    HeightRequest="200"
    AutoPlay="True"
    Repeat="-1" />
```

### Real-World Example: Robot Animation

From the demo project, here's a practical implementation showing a robot animation with proper styling:

```xml
<draw:SkiaLottie
    AutoPlay="True"
    HorizontalOptions="Center"
    LockRatio="1"
    Repeat="-1"
    Source="Lottie/robot.json"
    VerticalOptions="Start"
    WidthRequest="300" />
```

**Key Features:**
- `LockRatio="1"` maintains aspect ratio
- `Repeat="-1"` loops indefinitely
- Loads from `Resources/Raw/Lottie/robot.json`

### Toggle State Support

SkiaLottie includes special support for toggle/switch animations with the `IsOn` property:

```xml
<DrawUi:SkiaLottie
    Source="toggle_animation.json"
    IsOn="{Binding IsToggled}"
    DefaultFrame="0"
    DefaultFrameWhenOn="30"
    SpeedRatio="1.5"
    AutoPlay="True"
    Repeat="0" />
```

This is perfect for animated toggles, checkboxes, or any other animation with distinct on/off states.

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Source` | string | Path or URL to the Lottie JSON file |
| `ColorTint` | Color | Color tint applied to the entire animation |
| `Colors` | IList<Color> | Collection of replacement colors |
| `IsOn` | bool | Toggle state property |
| `DefaultFrameWhenOn` | int | Frame to display when IsOn = true |
| `ApplyIsOnWhenNotPlaying` | bool | Whether to apply IsOn state when not playing |

### Customizing Colors

One of the powerful features of SkiaLottie is color customization without needing external editors:

#### Single Color Tint
```xml
<!-- Apply a global tint with theme color -->
<draw:SkiaLottie
    InputTransparent="True"
    AutoPlay="True"
    ColorTint="{StaticResource ColorPrimary}"
    HorizontalOptions="Center"
    LockRatio="1"
    Opacity="0.85"
    Repeat="-1"
    Source="Lottie/Loader.json"
    VerticalOptions="Center"
    WidthRequest="56" />
```

#### Multiple Color Replacement
For more granular control, you can replace multiple specific colors:

```xml
<draw:SkiaLottie
    AutoPlay="True"
    HorizontalOptions="Center"
    WidthRequest="50"
    LockRatio="1"
    Repeat="-1"
    Source="Lottie/Loader.json"
    VerticalOptions="Center">

    <draw:SkiaLottie.Colors>
        <Color>#FF0000</Color>
        <Color>#FFFFFF</Color>
    </draw:SkiaLottie.Colors>

</draw:SkiaLottie>
```

#### Programmatic Color Changes
```csharp
// Replace specific colors in the animation
myLottie.Colors.Add(Colors.Blue);
myLottie.Colors.Add(Colors.Green);

// Apply changes
myLottie.ReloadSource();
```

**Color Replacement Logic:**
- Colors are replaced in the order they appear in the original animation
- First color in `Colors` collection replaces the first unique color found
- `ColorTint` applies a global tint over the entire animation
- Use `Colors` for precise color replacement, `ColorTint` for theme integration

## SkiaSprite

SkiaSprite is a high-performance control for displaying and animating sprite sheets. It loads sprite sheets (a single image containing multiple animation frames arranged in a grid) and renders individual frames with precise timing for smooth animations.

### Basic Usage

```xml
<DrawUi:SkiaSprite
    Source="sprites/explosion.png"
    Columns="8"
    Rows="4"
    FramesPerSecond="24"
    AutoPlay="True"
    Repeat="-1"
    WidthRequest="128"
    HeightRequest="128" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Source` | string | Path or URL of the sprite sheet image |
| `Columns` | int | Number of columns in the sprite sheet grid |
| `Rows` | int | Number of rows in the sprite sheet grid |
| `FramesPerSecond` | int | Animation speed in frames per second (default: 24) |
| `MaxFrames` | int | Maximum number of frames to use (0 means use all) |
| `CurrentFrame` | int | Current frame being displayed (0-based index) |
| `FrameSequence` | int[] | Custom sequence of frames to play |
| `AnimationName` | string | Name of a predefined animation sequence |

### Sprite Sheet Structure

A sprite sheet is a single image containing multiple frames arranged in a grid:

```
+---+---+---+---+
| 0 | 1 | 2 | 3 |
+---+---+---+---+
| 4 | 5 | 6 | 7 |
+---+---+---+---+
| 8 | 9 | 10| 11|
+---+---+---+---+
```

The `Columns` and `Rows` properties define the grid structure:
- In the example above, set `Columns="4"` and `Rows="3"`
- Frames are numbered left-to-right, top-to-bottom (0 to 11)
- Each frame must have the same dimensions

### Frame Sequences and Reusing Spritesheets

One of the key features of SkiaSprite is the ability to create multiple animations from a single spritesheet by defining frame sequences:

```csharp
// Register named animations for a character spritesheet
SkiaSprite.CreateAnimationSequence("Idle", new[] { 0, 1, 2, 1 });
SkiaSprite.CreateAnimationSequence("Walk", new[] { 3, 4, 5, 6, 7, 8 });
SkiaSprite.CreateAnimationSequence("Jump", new[] { 9, 10, 11 });
```

Then in XAML just reference by name:
```xml
<!-- Multiple sprites sharing the same spritesheet with different animations -->
<DrawUi:SkiaSprite Source="character.png" AnimationName="Walk" />
<DrawUi:SkiaSprite Source="character.png" AnimationName="Jump" />
```

Or use a direct frame sequence:
```csharp
// Define a specific frame sequence
mySprite.FrameSequence = new[] { 3, 4, 5, 4, 3 }; // Play frames in this exact order
```

### Spritesheet Caching

SkiaSprite includes an intelligent caching system to avoid reloading the same spritesheets multiple times:

```csharp
// Clear the entire spritesheet cache
SkiaSprite.ClearCache();

// Remove a specific spritesheet from cache
SkiaSprite.RemoveFromCache("character.png");
```

## Performance Considerations

### SkiaGif
- GIFs can consume significant memory, especially large ones
- For large animations, verify memory usage

### SkiaLottie
- Vector-based animations are more memory efficient than GIFs
- Complex Lottie animations may be CPU-intensive
- Use `ColorTint` for simple color changes rather than individual color replacements when possible

### SkiaSprite
- Spritesheets are cached automatically to avoid redundant loading
- For large or numerous sprite sheets, consider monitoring memory usage
- Use `ClearCache()` or `RemoveFromCache()` when spritesheets are no longer needed
- For complex animations, use frame sequences to avoid redundant frames

## Advanced Examples

### Code-Behind Lottie Implementation

For complex scenarios, you can create and control SkiaLottie entirely in code:

```csharp
public partial class AnimationPage : ContentPage
{
    private SkiaLottie crashAnimation;

    public AnimationPage()
    {
        InitializeComponent();
        CreateLottieAnimation();
    }

    private void CreateLottieAnimation()
    {
        crashAnimation = new SkiaLottie()
        {
            AutoPlay = false,
            WidthRequest = 110,
            LockRatio = 1,
            SpeedRatio = 0.6f,
            Repeat = 0,
            DefaultFrame = -1, // -1 means "last frame" when not playing
            UseCache = SkiaCacheType.ImageDoubleBuffered,
            Source = "Space/Lottie/crash.json"
        };

        // Add to your layout
        MainLayout.Children.Add(crashAnimation);
    }

    // Control animation programmatically
    private void PlayCrashAnimation()
    {
        crashAnimation.Play();
    }

    private void StopAndResetAnimation()
    {
        crashAnimation.Stop();
        // Will automatically seek to DefaultFrame (-1 = last frame)
    }
}
```

**Advanced Properties Explained:**
- `DefaultFrame = -1` - Shows last frame when stopped (useful for "completed" states)
- `SpeedRatio = 0.6f` - Plays at 60% of original speed
- `UseCache = ImageDoubleBuffered` - Optimizes for smooth playback
- `Repeat = 0` - Plays once (use -1 for infinite loop)

### Loading Animation with Theme Integration

```xml
<draw:SkiaLottie
    Source="loading_spinner.json"
    WidthRequest="48"
    HeightRequest="48"
    AutoPlay="True"
    Repeat="-1"
    ColorTint="{AppThemeBinding Light={StaticResource Primary}, Dark={StaticResource PrimaryDark}}"
    Opacity="0.8" />
```

### Animated Button with Sprite Sheet

```xml
<DrawUi:SkiaButton
    WidthRequest="200"
    HeightRequest="60"
    BackgroundColor="Transparent">
    
    <DrawUi:SkiaSprite
        x:Name="buttonAnimation"
        Source="button_animation.png"
        Columns="5"
        Rows="1"
        FramesPerSecond="30"
        AutoPlay="False"
        DefaultFrame="0" />
    
    <DrawUi:SkiaLabel
        Text="Animated Button"
        TextColor="White"
        FontSize="16"
        HorizontalOptions="Center"
        VerticalOptions="Center" />
</DrawUi:SkiaButton>
```

In code-behind:
```csharp
MyButton.Pressed += (s, e) => {
    buttonAnimation.Stop();
    buttonAnimation.CurrentFrame = 0;
    buttonAnimation.Start();
};
```

### Game Character Animation

```xml
<DrawUi:SkiaLayout
    WidthRequest="200"
    HeightRequest="200">
    
    <DrawUi:SkiaSprite
        x:Name="CharacterAnimation"
        Source="character_sprites.png"
        Columns="8"
        Rows="4"
        FramesPerSecond="12"
        AnimationName="Walk"
        AutoPlay="True"
        WidthRequest="128"
        HeightRequest="128"
        HorizontalOptions="Center"
        VerticalOptions="Center" />
        
</DrawUi:SkiaLayout>
```

In code-behind:
```csharp
// Setup animation sequences
void InitializeAnimations()
{
    SkiaSprite.CreateAnimationSequence("Idle", new[] { 0, 1, 2, 1 });
    SkiaSprite.CreateAnimationSequence("Walk", new[] { 8, 9, 10, 11, 12, 13, 14, 15 });
    SkiaSprite.CreateAnimationSequence("Jump", new[] { 16, 17, 18, 19, 20, 21 });
    SkiaSprite.CreateAnimationSequence("Attack", new[] { 24, 25, 26, 27, 28, 29, 30 });
}

// Change animation based on game state
void UpdateCharacterState(PlayerState state)
{
    CharacterAnimation.Stop();
    
    switch (state)
    {
        case PlayerState.Idle:
            CharacterAnimation.AnimationName = "Idle";
            break;
        case PlayerState.Walking:
            CharacterAnimation.AnimationName = "Walk";
            break;
        case PlayerState.Jumping:
            CharacterAnimation.AnimationName = "Jump";
            break;
        case PlayerState.Attacking:
            CharacterAnimation.AnimationName = "Attack";
            break;
    }
    
    CharacterAnimation.Start();
}
```

### GIF Avatar

```xml
<draw:SkiaShape
    Type="Circle"
    WidthRequest="100"
    LockRatio="1">
    
    <draw:SkiaGif
        Source="avatar.gif"
        WidthRequest="100"
        LockRatio="1"
        AutoPlay="True"
        Repeat="-1" />
</draw:SkiaShape>
```