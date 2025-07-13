<!-- Component: SkiaImage, Category: Media/Display, Complexity: Intermediate -->
# SkiaImage - High-Performance Image Control

## Scenario
SkiaImage provides advanced image rendering with effects, transformations, caching strategies, and multiple source types. It supports local files, embedded resources, URLs, streams, and even sprite animations. Use SkiaImage when you need high-performance image display with effects, custom aspect ratios, or advanced caching.

## Complete Working Example

### Basic Image Display
```xml
<draw:Canvas>
    <draw:SkiaLayout Type="Column" Spacing="16" Padding="20">
        
        <!-- Simple image from resources -->
        <draw:SkiaImage 
            Source="car.png"
            Aspect="AspectFit"
            WidthRequest="200"
            HeightRequest="150"
            HorizontalOptions="Center"
            UseCache="Image" />
            
        <!-- Image with effects -->
        <draw:SkiaImage 
            Source="Images/photo.jpg"
            Aspect="AspectCover"
            WidthRequest="300"
            HeightRequest="200"
            RescalingQuality="Medium"
            Brightness="1.2"
            Contrast="1.1"
            Saturation="1.3"
            UseCache="ImageDoubleBuffered"
            HorizontalOptions="Center" />
            
        <!-- Rounded image with shadow -->
        <draw:SkiaImage 
            Source="profile.jpg"
            Aspect="AspectCover"
            WidthRequest="100"
            HeightRequest="100"
            CornerRadius="50"
            HorizontalOptions="Center">
            
            <draw:SkiaImage.Shadows>
                <draw:SkiaShadow 
                    X="0" Y="4" 
                    Blur="8" 
                    Opacity="0.3" 
                    Color="Black" />
            </draw:SkiaImage.Shadows>
        </draw:SkiaImage>
    </draw:SkiaLayout>
</draw:Canvas>
```

### Advanced Image with Custom Effects
```xml
<draw:SkiaImage 
    Source="hero-background.jpg"
    Aspect="AspectCover"
    WidthRequest="400"
    HeightRequest="250"
    LoadSourceOnFirstDraw="False"
    RescalingType="MultiPass"
    HorizontalOptions="Fill">
    
    <!-- Gradient overlay -->
    <draw:SkiaImage.Effects>
        <draw:SkiaImageEffect 
            Type="Gradient"
            StartColor="#80000000"
            EndColor="#00000000"
            Direction="TopToBottom" />
        <draw:SkiaImageEffect 
            Type="Blur"
            BlurRadius="2" />
    </draw:SkiaImage.Effects>
    
    <!-- Content overlay -->
    <draw:SkiaLayout 
        Type="Column" 
        Spacing="8" 
        Padding="20"
        VerticalOptions="End">
        
        <draw:SkiaLabel 
            Text="Hero Title"
            FontSize="24"
            FontWeight="Bold"
            TextColor="White" />
        <draw:SkiaLabel 
            Text="Subtitle description"
            FontSize="16"
            TextColor="White"
            Opacity="0.8" />
    </draw:SkiaLayout>
</draw:SkiaImage>
```

### Code-Behind for Dynamic Images
```csharp
using DrawnUi.Draw;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        LoadDynamicImages();
    }
    
    private async void LoadDynamicImages()
    {
        // Create image from URL
        var urlImage = new SkiaImage
        {
            Source = "https://example.com/image.jpg",
            LoadSourceOnFirstDraw = true,
            Aspect = TransformAspect.AspectCover,
            WidthRequest = 200,
            HeightRequest = 200,
            UseCache = SkiaCacheType.Image
        };
        
        // Handle loading events
        urlImage.Success += (s, e) => 
        {
            DisplayAlert("Success", "Image loaded successfully!", "OK");
        };
        
        urlImage.Error += (s, e) => 
        {
            DisplayAlert("Error", "Failed to load image", "OK");
        };
        
        // Create image from stream
        var streamImage = await CreateImageFromStream();
        
        // Add to layout
        var layout = this.FindByName<SkiaLayout>("ImageContainer");
        layout?.Children.Add(urlImage);
        layout?.Children.Add(streamImage);
    }
    
    private async Task<SkiaImage> CreateImageFromStream()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("embedded_image.png");
        var imageBytes = new byte[stream.Length];
        await stream.ReadAsync(imageBytes, 0, (int)stream.Length);
        
        return new SkiaImage
        {
            Source = ImageSource.FromStream(() => new MemoryStream(imageBytes)),
            Aspect = TransformAspect.AspectFit,
            WidthRequest = 150,
            HeightRequest = 150
        };
    }
}
```

## Result
- High-performance image rendering with hardware acceleration
- Advanced effects and transformations applied in real-time
- Efficient caching strategies for optimal memory usage
- Support for multiple image sources and formats

## Variations

### 1. Sprite Animation
```xml
<draw:SkiaImage 
    Source="character-sprites.png"
    SpriteWidth="32"
    SpriteHeight="32"
    SpriteColumns="8"
    SpriteRows="4"
    SpriteIndex="0"
    AnimationSpeed="12"
    IsAnimating="True" />
```

### 2. Image with Tint and Blend Mode
```xml
<draw:SkiaImage 
    Source="icon.png"
    ColorTint="Blue"
    TintBlendMode="Multiply"
    Aspect="AspectFit"
    WidthRequest="64"
    HeightRequest="64" />
```

### 3. Lazy Loading Image
```xml
<draw:SkiaImage 
    Source="{Binding ImageUrl}"
    LoadSourceOnFirstDraw="True"
    PlaceholderSource="placeholder.png"
    Aspect="AspectCover"
    UseCache="Image" />
```

### 4. Image Gallery with Virtualization
```xml
<draw:SkiaScroll Orientation="Horizontal">
    <draw:SkiaLayout 
        Type="Row" 
        Spacing="10"
        ItemsSource="{Binding Images}"
        RecyclingTemplate="Enabled">
        
        <draw:SkiaLayout.ItemTemplate>
            <DataTemplate>
                <draw:SkiaImage 
                    Source="{Binding Url}"
                    LoadSourceOnFirstDraw="True"
                    Aspect="AspectCover"
                    WidthRequest="200"
                    HeightRequest="150"
                    UseCache="Image" />
            </DataTemplate>
        </draw:SkiaLayout.ItemTemplate>
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

## Related Components
- **Also see**: SkiaShape, SkiaLottie, SkiaSprite, SkiaGif
- **Requires**: Canvas container
- **Effects**: SkiaImageEffect, SkiaShadow, SkiaGradient

## Common Mistakes

### ❌ Not setting image dimensions
```xml
<!-- Wrong - image may not display properly -->
<draw:SkiaImage Source="image.png" />
```

### ✅ Always specify dimensions or aspect
```xml
<!-- Correct - explicit dimensions -->
<draw:SkiaImage 
    Source="image.png"
    WidthRequest="200"
    HeightRequest="150"
    Aspect="AspectFit" />
```

### ❌ Loading large images without caching
```xml
<!-- Wrong - no caching for large images -->
<draw:SkiaImage Source="large_image.jpg" />
```

### ✅ Use appropriate caching strategy
```xml
<!-- Correct - caching enabled -->
<draw:SkiaImage 
    Source="large_image.jpg"
    UseCache="ImageDoubleBuffered"
    RescalingQuality="Medium" />
```

### ❌ Blocking UI with synchronous loading
```xml
<!-- Wrong - synchronous loading -->
<draw:SkiaImage 
    Source="https://example.com/image.jpg"
    LoadSourceOnFirstDraw="False" />
```

### ✅ Use asynchronous loading
```xml
<!-- Correct - async loading -->
<draw:SkiaImage 
    Source="https://example.com/image.jpg"
    LoadSourceOnFirstDraw="True"
    PlaceholderSource="loading.png" />
```

## Tags
#skiaimage #image #media #effects #caching #sprites #async-loading #transformations #gradients #shadows #performance #intermediate
