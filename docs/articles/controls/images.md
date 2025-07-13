# Image Controls

DrawnUi.Maui provides powerful image controls for high-performance image rendering with advanced features like effects, transformations, and sophisticated caching. This article covers the image components available in the framework.

## SkiaImage

SkiaImage is the core image control in DrawnUi.Maui, providing efficient image loading, rendering, and manipulation capabilities with direct SkiaSharp rendering. It supports multiple image sources, advanced rescaling algorithms, built-in effects, and comprehensive caching strategies.

### Basic Usage

```xml
<draw:SkiaImage
    Source="image.png"
    Aspect="AspectCover"
    HorizontalOptions="Center"
    VerticalOptions="Center"
    WidthRequest="200"
    HeightRequest="200" />
```

> **Note:** The default `Aspect` is `AspectCover`, which maintains aspect ratio while filling the entire space.

### Key Properties

#### Core Properties
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Source` | ImageSource | null | Source of the image (URL, file, resource, stream) |
| `Aspect` | TransformAspect | AspectCover | How the image scales to fit (AspectFit, AspectFill, etc.) |
| `HorizontalAlignment` | DrawImageAlignment | Center | Horizontal positioning of the image |
| `VerticalAlignment` | DrawImageAlignment | Center | Vertical positioning of the image |
| `UseAssembly` | object | null | Assembly to load embedded resources from |

#### Loading & Performance
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `LoadSourceOnFirstDraw` | bool | false | Whether to defer loading until first render |
| `PreviewBase64` | string | null | Base64 encoded preview image to show while loading |
| `RescalingQuality` | SKFilterQuality | None | Quality of image rescaling (None, Low, Medium, High) |
| `RescalingType` | RescalingType | Default | Rescaling algorithm (Default, MultiPass, GammaCorrection, EdgePreserving) |
| `EraseChangedContent` | bool | false | Erase existing image when new source is set but not loaded yet |
| `DrawWhenEmpty` | bool | true | Whether to draw when no source is set |

#### Effects & Adjustments
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AddEffect` | SkiaImageEffect | None | Built-in image effect (None, Sepia, Tint, BlackAndWhite, etc.) |
| `ColorTint` | Color | Transparent | Tint color for image effect |
| `EffectBlendMode` | SKBlendMode | SrcIn | Blend mode for effects |
| `Brightness` | double | 1.0 | Adjusts image brightness (≥1.0) |
| `Contrast` | double | 1.0 | Adjusts image contrast (≥1.0) |
| `Saturation` | double | 0.0 | Adjusts image saturation (≥0) |
| `Blur` | double | 0.0 | Applies blur effect |
| `Gamma` | double | 1.0 | Adjusts gamma (≥0) |
| `Darken` | double | 0.0 | Darkens the image |
| `Lighten` | double | 0.0 | Lightens the image |

#### Transformations
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ZoomX`/`ZoomY` | double | 1.0 | Zoom/scaling factors |
| `HorizontalOffset`/`VerticalOffset` | double | 0.0 | Offset for image position |

#### Sprite Sheets
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SpriteWidth`/`SpriteHeight` | double | 0.0 | Sprite sheet cell size |
| `SpriteIndex` | int | -1 | Index of sprite to display |

#### Gradient Overlay
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `UseGradient` | bool | false | Whether to apply gradient overlay |
| `StartColor` | Color | DarkGray | Start color for gradient |
| `EndColor` | Color | Gray | End color for gradient |

#### State Properties (Read-only)
| Property | Type | Description |
|----------|------|-------------|
| `LoadedSource` | LoadedImageSource | Currently loaded image source |
| `IsLoading` | bool | Whether image is currently loading |
| `HasError` | bool | Whether last load attempt failed |

> **Note:** Caching is handled by the SkiaControl base class. You can set `Cache` on SkiaImage for caching strategies (e.g., `Cache="Image"`).

> **Note:** The `VisualEffects` property is inherited from SkiaControl. You can use `<draw:SkiaControl.VisualEffects>` in XAML to apply effects like drop shadow or color presets.

### Aspect Modes

The `Aspect` property controls how the image is sized and positioned within its container. This is a critical property for ensuring that your images display correctly while maintaining their proportions when appropriate.

#### Available Aspect Modes

| Aspect Mode | Description | Visual Effect |
|-------------|-------------|---------------|
| `None` | No scaling or positioning | Image displayed at original size |
| `Fill` | Enlarges to fill the viewport without maintaining aspect ratio if smaller, but does not scale down if larger | May distort proportions |
| `Fit` | Fit without maintaining aspect ratio and without enlarging if smaller | May distort proportions |
| `AspectFit` | Fit inside viewport respecting aspect without enlarging if smaller | May leave empty space around |
| `AspectFill` | Covers viewport respecting aspect without scaling down if bigger | May crop portions of the image |
| `FitFill` | Enlarges to fill the viewport if smaller and reduces size if larger, all without respecting aspect ratio | May distort proportions |
| `AspectFitFill` | Enlarges to fit the viewport if smaller and reduces size if larger, all while respecting aspect ratio | Maintains proportions |
| `Cover` | Enlarges to cover the viewport if smaller and reduces size if larger, all without respecting aspect ratio. Same as AspectFitFill but will crop the image to fill entire viewport | May crop image |
| `AspectCover` | **Default.** Covers viewport respecting aspect, scales both up and down as needed | May crop portions, maintains aspect |
| `Tile` | Tiles the image to fill the viewport | Repeats image pattern |

#### Rescaling Types

The `RescalingType` property determines the algorithm used for image rescaling:

| Rescaling Type | Description | Best For |
|----------------|-------------|----------|
| `Default` | Standard SkiaSharp rescaling: "I just need it to work fast" | Minor size adjustments, performance-focused |
| `MultiPass` | Multi-pass progressive rescaling for superior quality on significant size changes: "I want good quality" | Icons, logos, transparent graphics with sharp edges, large size reductions (2x smaller) |
| `GammaCorrection` | Gamma-corrected rescaling that processes in linear color space for photographic quality: "I'm working with photos professionally" | Professional photography and color-critical work, color accuracy, gradients and smooth color transitions |
| `EdgePreserving` | Edge-preserving rescaling optimized for sharp graphics and pixel-perfect content: "I'm working with pixel art/UI graphics" | Pixel art, very small icons (16x16, 32x32), screenshots, text graphics |

> **Note:** `GammaCorrection` is slower than other methods but provides the best quality for photographic content.

#### Examples and Visual Guide

```xml
<!-- Maintain aspect ratio, fit within bounds -->
<draw:SkiaImage Source="image.png" Aspect="AspectFit" />
```
This ensures the entire image is visible, possibly with letterboxing (empty space) on the sides or top/bottom.

```xml
<!-- Maintain aspect ratio, fill bounds (may crop) - DEFAULT -->
<draw:SkiaImage Source="image.png" Aspect="AspectCover" />
```
This fills the entire control with the image, possibly cropping parts that don't fit. Great for background images or thumbnails. This is the default behavior.

```xml
<!-- Stretch to fill bounds (may distort) -->
<draw:SkiaImage Source="image.png" Aspect="Fill" />
```
This stretches the image to fill the control exactly, potentially distorting the image proportions.

```xml
<!-- Tile the image -->
<draw:SkiaImage Source="pattern.png" Aspect="Tile" />
```
This repeats the image to fill the entire control. Perfect for background patterns.

```xml
<!-- High-quality rescaling for photos -->
<draw:SkiaImage
    Source="photo.jpg"
    Aspect="AspectCover"
    RescalingType="GammaCorrection"
    RescalingQuality="Medium" />
```
This provides the best quality for photographic content with gamma-corrected rescaling.

#### Combining Aspect and Alignment

You can combine `Aspect` with `HorizontalAlignment` and `VerticalAlignment` for precise control:

```xml
<!-- AspectFit with custom alignment -->
<draw:SkiaImage
    Source="image.png"
    Aspect="AspectFit"
    HorizontalAlignment="Start"
    VerticalAlignment="End" />
```
This would fit the image within bounds while aligning it to the bottom-left corner of the available space.

#### Choosing the Right Aspect Mode

- **For user photos or content images**: `AspectFit` ensures the entire image is visible
- **For backgrounds or covers**: `AspectCover` (default) ensures no empty space is visible
- **For thumbnails and cards**: `AspectCover` provides consistent sizing
- **For patterns and textures**: `Tile` repeats the image seamlessly
- **For icons that need exact sizing**: `Fill` stretches to exact dimensions
- **For pixel-perfect graphics**: `None` maintains original size and quality

#### Choosing the Right Rescaling Type

- **For general use**: `Default` provides good performance
- **For icons and logos**: `MultiPass` provides superior quality for significant size changes
- **For professional photography**: `GammaCorrection` provides color-accurate results
- **For pixel art and UI graphics**: `EdgePreserving` maintains sharp edges

### Image Alignment

Control the alignment of the image within its container:

```xml
<draw:SkiaImage
    Source="image.png"
    Aspect="AspectFit"
    HorizontalAlignment="End"
    VerticalAlignment="Start" />
```

This will position the image at the top-right of its container.

### Image Effects

SkiaImage supports various built-in effects through the `AddEffect` property. Effects can be combined with blend modes for advanced visual results.

#### Available Effects

| Effect | Description | Additional Properties |
|--------|-------------|----------------------|
| `None` | No effect applied | - |
| `BlackAndWhite` | Converts to grayscale | - |
| `Pastel` | Applies pastel color effect | - |
| `Tint` | Applies color tint | `ColorTint`, `EffectBlendMode` |
| `Darken` | Darkens the image | `Darken` (amount) |
| `Lighten` | Lightens the image | `Lighten` (amount) |
| `Sepia` | Applies sepia tone | - |
| `InvertColors` | Inverts all colors | - |
| `Contrast` | Adjusts contrast | `Contrast` (≥1.0) |
| `Saturation` | Adjusts saturation | `Saturation` (≥0) |
| `Brightness` | Adjusts brightness | `Brightness` (≥1.0) |
| `Gamma` | Adjusts gamma correction | `Gamma` (≥0) |
| `TSL` | Tint with Saturation and Lightness | `BackgroundColor`, `Saturation`, `Brightness`, `EffectBlendMode` |
| `HSL` | Hue, Saturation, Lightness adjustment | `Gamma` (hue), `Saturation`, `Brightness`, `EffectBlendMode` |
| `Custom` | Use custom effects via VisualEffects | - |

#### Basic Effects Examples

```xml
<!-- Apply a sepia effect -->
<draw:SkiaImage
    Source="image.png"
    AddEffect="Sepia" />

<!-- Apply a tint effect with custom blend mode -->
<draw:SkiaImage
    Source="image.png"
    AddEffect="Tint"
    ColorTint="Red"
    EffectBlendMode="Multiply" />

<!-- Apply grayscale effect -->
<draw:SkiaImage
    Source="image.png"
    AddEffect="BlackAndWhite" />

<!-- Invert colors -->
<draw:SkiaImage
    Source="image.png"
    AddEffect="InvertColors" />
```

### Image Adjustments

Fine-tune image appearance with various adjustment properties. These work independently of the `AddEffect` property:

```xml
<draw:SkiaImage
    Source="image.png"
    Brightness="1.2"
    Contrast="1.1"
    Saturation="0.8"
    Blur="2"
    Gamma="1.1" />
```

#### Advanced Effect Combinations

```xml
<!-- HSL effect with custom values -->
<draw:SkiaImage
    Source="image.png"
    AddEffect="HSL"
    Gamma="0.8"
    Saturation="1.2"
    Brightness="1.1"
    EffectBlendMode="Overlay" />

<!-- TSL effect with background color -->
<draw:SkiaImage
    Source="image.png"
    AddEffect="TSL"
    BackgroundColor="Blue"
    Saturation="0.7"
    Brightness="1.3"
    EffectBlendMode="SoftLight" />
```

### Advanced Effects

For more complex effects, use the VisualEffects collection:

```xml
<draw:SkiaImage Source="image.png">
    <draw:SkiaControl.VisualEffects>
        <draw:DropShadowEffect
            Blur="8"
            X="2"
            Y="2"
            Color="#80000000" />
        <draw:ChainColorPresetEffect Preset="Sepia" />
    </draw:SkiaControl.VisualEffects>
</draw:SkiaImage>
```

### Gradient Overlays

Apply gradient overlays to images for enhanced visual effects:

```xml
<draw:SkiaImage
    Source="image.png"
    UseGradient="True"
    StartColor="#80000000"
    EndColor="#00000000" />
```

This applies a gradient from semi-transparent black to fully transparent, creating a fade effect.

### Sprite Sheets

SkiaImage supports sprite sheets for displaying a single sprite from a larger image:

```xml
<draw:SkiaImage
    Source="sprite-sheet.png"
    SpriteWidth="64"
    SpriteHeight="64"
    SpriteIndex="2" />
```

This shows the third sprite (index 2) from the sprite sheet, assuming each sprite is 64x64 pixels.

#### Animated Sprites

```xml
<!-- Animate through sprites -->
<draw:SkiaImage
    Source="character-walk.png"
    SpriteWidth="32"
    SpriteHeight="32"
    SpriteIndex="{Binding CurrentFrame}" />
```

### Preview Images

Show a low-resolution placeholder while loading the main image:

```xml
<DrawUi:SkiaImage
    Source="https://example.com/large-image.jpg"
    PreviewBase64="data:image/png;base64,iVBORw0KGgoAA..."
    Aspect="AspectFit" />
```

### Loading Options

Control how and when images are loaded:

```xml
<!-- Immediate loading (default) -->
<draw:SkiaImage
    Source="image.png"
    LoadSourceOnFirstDraw="False" />

<!-- Deferred loading (load when first rendered) -->
<draw:SkiaImage
    Source="image.png"
    LoadSourceOnFirstDraw="True" />

<!-- Erase content when source changes -->
<draw:SkiaImage
    Source="{Binding ImageUrl}"
    EraseChangedContent="True" />
```

#### Loading from Different Sources

```xml
<!-- From URL -->
<draw:SkiaImage Source="https://example.com/image.jpg" />

<!-- From file -->
<draw:SkiaImage Source="Images/local-image.png" />

<!-- From embedded resource -->
<draw:SkiaImage
    Source="MyApp.Images.embedded-image.png"
    UseAssembly="{x:Static local:App.CurrentAssembly}" />

<!-- From stream (in code-behind) -->
```

```csharp
// Load from stream
myImage.SetSource(async (cancellationToken) =>
{
    var stream = await GetImageStreamAsync();
    return stream;
});
```

### Caching Strategies

Optimize performance with various caching options:

```xml
<!-- Use double-buffered image caching (good for changing content) -->
<DrawUi:SkiaImage
    Source="image.png"
    UseCache="ImageDoubleBuffered" />

<!-- Use simple image caching (good for static content) -->
<DrawUi:SkiaImage
    Source="image.png"
    UseCache="Image" />

<!-- Cache drawing operations rather than bitmap (memory efficient) -->
<DrawUi:SkiaImage
    Source="image.png"
    UseCache="Operations" />

<!-- No caching (for frequently changing images) -->
<DrawUi:SkiaImage
    Source="image.png"
    UseCache="None" />
```

### Handling Load Events

You can respond to image load success or failure in code-behind:

```csharp
public MainPage()
{
    InitializeComponent();

    MyImage.Success += (sender, e) => {
        // Image loaded successfully
        Console.WriteLine($"Loaded: {e.Source}");
    };

    MyImage.Error += (sender, e) => {
        // Image failed to load
        Console.WriteLine($"Failed to load: {e.Source}");
    };

    MyImage.OnCleared += (sender, e) => {
        // Image was cleared/unloaded
    };
}
```

#### Monitoring Load State

```xml
<draw:SkiaImage
    Source="{Binding ImageUrl}"
    IsLoading="{Binding IsImageLoading, Mode=OneWayToSource}"
    HasError="{Binding HasImageError, Mode=OneWayToSource}" />
```

#### Manual Loading Control

```csharp
// Stop current loading
myImage.StopLoading();

// Reload the current source
myImage.ReloadSource();

// Clear the current image
myImage.ClearBitmap();
```

## Image Management

### SkiaImageManager

DrawnUi.Maui includes a powerful image management system through the `SkiaImageManager` class. This provides centralized image loading, caching, and resource management.

#### Preloading Images

Preload images to ensure they're ready when needed:

```csharp
// Preload a single image
await SkiaImageManager.Instance.PreloadImage("Images/my-image.jpg");

// Preload multiple images
await SkiaImageManager.Instance.PreloadImages(new List<string> 
{
    "Images/image1.jpg",
    "Images/image2.jpg",
    "Images/image3.jpg"
});
```

#### Managing Memory Usage

Configure the image manager for optimal memory usage:

```csharp
// Enable bitmap reuse for better memory usage
SkiaImageManager.ReuseBitmaps = true;

// Set cache longevity (in seconds)
SkiaImageManager.CacheLongevitySecs = 1800; // 30 minutes

// Enable async loading for local images
SkiaImageManager.LoadLocalAsync = true;

// Clear unused cached images
SkiaImageManager.Instance.ClearUnusedImages();

// Clear all cached images
SkiaImageManager.Instance.ClearAll();

// Add image to cache manually
SkiaImageManager.Instance.AddToCache("my-key", bitmap, 3600); // 1 hour

// Get image from cache
var cachedBitmap = SkiaImageManager.Instance.GetFromCache("my-key");
```

## Advanced Usage

### Loading from Base64

Load images directly from base64 strings:

```csharp
var base64String = "data:image/png;base64,iVBORw0KGgoAA...";
myImage.SetFromBase64(base64String);
```

### Applying Transformations

Apply transformations to the displayed image:

```xml
<DrawUi:SkiaImage
    Source="image.png"
    ZoomX="1.2"
    ZoomY="1.2"
    HorizontalOffset="10"
    VerticalOffset="-5" />
```

### Creating Images in Code

Create and configure SkiaImage controls programmatically:

```csharp
var image = new SkiaImage
{
    Source = "Images/my-image.jpg",
    LoadSourceOnFirstDraw = false,
    Aspect = TransformAspect.AspectCover,
    RescalingQuality = SKFilterQuality.Medium,
    RescalingType = RescalingType.MultiPass,
    AddEffect = SkiaImageEffect.Sepia,
    ColorTint = Colors.Brown,
    EffectBlendMode = SKBlendMode.Multiply,
    Brightness = 1.1,
    Contrast = 1.05,
    WidthRequest = 200,
    HeightRequest = 200,
    HorizontalOptions = LayoutOptions.Center,
    VerticalOptions = LayoutOptions.Center
};

// Subscribe to events
image.Success += (s, e) => Console.WriteLine("Image loaded");
image.Error += (s, e) => Console.WriteLine("Image failed to load");

myLayout.Children.Add(image);
```

#### Advanced Programmatic Usage

```csharp
// Create image with gradient overlay
var heroImage = new SkiaImage
{
    Source = "hero-background.jpg",
    Aspect = TransformAspect.AspectCover,
    UseGradient = true,
    StartColor = Color.FromArgb("#80000000"),
    EndColor = Color.FromArgb("#00000000"),
    RescalingType = RescalingType.GammaCorrection
};

// Create sprite animation
var spriteImage = new SkiaImage
{
    Source = "character-sprites.png",
    SpriteWidth = 32,
    SpriteHeight = 32,
    SpriteIndex = 0
};

// Animate sprites
var timer = new Timer(100);
timer.Elapsed += (s, e) =>
{
    spriteImage.SpriteIndex = (spriteImage.SpriteIndex + 1) % 8;
};
timer.Start();
```

## Performance Considerations

### Optimization Tips

1. **Image Size**
   - Resize images to their display size before including in your app
   - Use compressed formats (WebP, optimized PNG/JPEG) when possible
   - Consider providing different image sizes for different screen densities

2. **Caching**
   - Use `UseCache="Image"` for static images that don't change
   - Use `UseCache="ImageDoubleBuffered"` for images that change occasionally
   - Use `UseCache="Operations"` for images with effects but static content
   - Use `UseCache="None"` only for frequently changing images

3. **Loading Strategy**
   - Use `LoadSourceOnFirstDraw="True"` for off-screen images
   - Preload important images with SkiaImageManager.PreloadImages()
   - Provide preview images with `PreviewBase64` for large remote images

4. **Rendering Quality**
   - Set appropriate `RescalingQuality` based on your needs:
     - `None`: Fastest but lowest quality (default)
     - `Low`: Good balance for scrolling content
     - `Medium`: Good for static content
     - `High`: Best quality but slowest (use sparingly)
   - Choose the right `RescalingType`:
     - `Default`: Standard performance
     - `MultiPass`: Better quality for significant size changes
     - `GammaCorrection`: Best for photos (slower)
     - `EdgePreserving`: Best for pixel art and UI graphics

5. **Memory Management**
   - Enable bitmap reuse with `SkiaImageManager.ReuseBitmaps = true`
   - Set reasonable cache longevity with `SkiaImageManager.CacheLongevitySecs`
   - Call `ClearUnusedImages()` when appropriate
   - Use `EraseChangedContent="True"` for dynamic image sources

### Examples of Optimized Image Loading

#### For Lists/Carousels

```xml
<draw:SkiaImage
    Source="{Binding ImageUrl}"
    LoadSourceOnFirstDraw="True"
    Cache="ImageDoubleBuffered"
    RescalingQuality="Low"
    RescalingType="Default"
    Aspect="AspectCover" />
```

#### For Hero/Cover Images

```xml
<draw:SkiaImage
    Source="{Binding CoverImage}"
    PreviewBase64="{Binding CoverImagePreview}"
    LoadSourceOnFirstDraw="False"
    Cache="Image"
    RescalingQuality="Medium"
    RescalingType="GammaCorrection"
    Aspect="AspectCover" />
```

#### For Professional Photography

```xml
<draw:SkiaImage
    Source="{Binding HighResPhoto}"
    RescalingType="GammaCorrection"
    RescalingQuality="High"
    Aspect="AspectFit"
    Cache="Image" />
```

#### For Icons and UI Graphics

```xml
<draw:SkiaImage
    Source="icon.png"
    RescalingType="EdgePreserving"
    RescalingQuality="None"
    Aspect="None"
    Cache="Operations" />
```

#### For Image Galleries

```xml
<draw:SkiaScroll Orientation="Horizontal">
    <draw:SkiaLayout LayoutType="Row" Spacing="10">
        <!-- Images that are initially visible -->
        <draw:SkiaImage
            Source="{Binding Images[0]}"
            LoadSourceOnFirstDraw="False"
            Cache="Image"
            RescalingType="MultiPass"
            Aspect="AspectCover"
            WidthRequest="300"
            HeightRequest="200" />

        <!-- Images that may be scrolled to -->
        <draw:SkiaImage
            Source="{Binding Images[1]}"
            LoadSourceOnFirstDraw="True"
            Cache="Image"
            RescalingType="MultiPass"
            Aspect="AspectCover"
            WidthRequest="300"
            HeightRequest="200" />

        <!-- More images... -->
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

## Advanced Features

### Custom Image Rendering

Get a rendered version of the image with all effects applied:

```csharp
// Get the image with all effects and transformations applied
var renderedImage = mySkiaImage.GetRenderedSource();
if (renderedImage != null)
{
    // Use the rendered image
    // Remember to dispose when done
    renderedImage.Dispose();
}
```

### Image Transformations and Offsets

```xml
<draw:SkiaImage
    Source="image.png"
    ZoomX="1.5"
    ZoomY="1.2"
    HorizontalOffset="20"
    VerticalOffset="-10"
    Aspect="AspectCover" />
```

### Working with Image Sources

```csharp
// Check if image is currently loading
if (myImage.IsLoading)
{
    // Show loading indicator
}

// Check for errors
if (myImage.HasError)
{
    // Show error state
}

// Access the loaded source
var loadedSource = myImage.LoadedSource;
if (loadedSource != null)
{
    var width = loadedSource.Width;
    var height = loadedSource.Height;
}
```

## Best Practices

### 1. Choose the Right Aspect Mode
- Use `AspectCover` (default) for most scenarios
- Use `AspectFit` when you need to see the entire image
- Use `Tile` for patterns and backgrounds
- Use `None` for pixel-perfect icons

### 2. Optimize Rescaling
- Use `Default` rescaling for general performance
- Use `MultiPass` for icons and graphics with significant size changes
- Use `GammaCorrection` for professional photography
- Use `EdgePreserving` for pixel art and small UI elements

### 3. Manage Memory Efficiently
- Enable `SkiaImageManager.ReuseBitmaps = true` for shared images
- Set appropriate cache longevity with `CacheLongevitySecs`
- Use `EraseChangedContent="True"` for dynamic content
- Clear unused images periodically

### 4. Handle Loading States
- Use `LoadSourceOnFirstDraw="True"` for off-screen images
- Provide preview images for large remote images
- Subscribe to `Success` and `Error` events for user feedback
- Monitor `IsLoading` and `HasError` properties

### 5. Apply Effects Wisely
- Use built-in effects for common adjustments
- Combine effects with appropriate blend modes
- Use `Custom` effect type with VisualEffects for complex scenarios
- Consider performance impact of multiple effects

This comprehensive guide covers all aspects of using SkiaImage in DrawnUi.Maui, from basic usage to advanced optimization techniques. The control provides powerful image handling capabilities while maintaining excellent performance through intelligent caching and rendering strategies.


## SkiaGif

SkiaGif is a dedicated control for displaying animated GIF files with playback control and optimization features.

### Basic Usage

```xml
<draw:SkiaGif
    Source="animation.gif"
    IsPlaying="True"
    RepeatCount="0"
    WidthRequest="200"
    HeightRequest="200" />
```

### Key Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsPlaying` | bool | true | Whether the animation is playing |
| `RepeatCount` | int | 0 | Number of times to repeat (0 = infinite) |
| `Speed` | double | 1.0 | Playback speed multiplier |
| `CurrentFrame` | int | 0 | Current frame index |
| `FrameCount` | int | 0 | Total number of frames (read-only) |

### Examples

```xml
<!-- Auto-playing GIF -->
<draw:SkiaGif
    Source="loading.gif"
    IsPlaying="True"
    RepeatCount="0" />

<!-- Controlled GIF playback -->
<draw:SkiaGif
    Source="animation.gif"
    IsPlaying="{Binding IsAnimating}"
    Speed="0.5"
    RepeatCount="3" />
```

## SkiaMediaImage

SkiaMediaImage is a versatile control that can display various types of media including static images, animated GIFs, and other supported formats. It automatically detects the media type and uses the appropriate rendering method.

### Basic Usage

```xml
<draw:SkiaMediaImage
    Source="{Binding MediaUrl}"
    AutoPlay="True"
    WidthRequest="300"
    HeightRequest="200" />
```

### Key Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AutoPlay` | bool | true | Whether to auto-play animated content |
| `MediaType` | MediaType | Auto | Force specific media type handling |

### Examples

```xml
<!-- Auto-detecting media type -->
<draw:SkiaMediaImage
    Source="{Binding MediaSource}"
    AutoPlay="True"
    Aspect="AspectCover" />

<!-- Force GIF handling -->
<draw:SkiaMediaImage
    Source="image.gif"
    MediaType="Gif"
    AutoPlay="False" />
```