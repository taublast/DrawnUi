<!-- Component: Canvas, Category: Core/Setup, Complexity: Basic -->
# Canvas - DrawnUI Root Container

## Scenario
Canvas is the root container that hosts all DrawnUI controls. It acts as the bridge between MAUI and the DrawnUI rendering system, providing hardware-accelerated rendering and gesture handling. Use Canvas when you want to include DrawnUI controls in your MAUI app or create a fully drawn UI.

## Complete Working Example

### XAML Setup
```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw"
             x:Class="MyApp.MainPage">
    
    <draw:Canvas 
        HorizontalOptions="Fill" 
        VerticalOptions="Fill"
        Gestures="Enabled"
        RenderingMode="Accelerated"
        BackgroundColor="White">
        
        <draw:SkiaLayout Type="Column" Spacing="16" Padding="32">
            <draw:SkiaLabel 
                Text="Hello DrawnUI!" 
                FontSize="24" 
                TextColor="Black"
                HorizontalOptions="Center" />
                
            <draw:SkiaButton 
                Text="Click Me" 
                WidthRequest="120" 
                HeightRequest="40"
                BackgroundColor="Blue" 
                TextColor="White"
                HorizontalOptions="Center" />
        </draw:SkiaLayout>
    </draw:Canvas>
</ContentPage>
```

### MauiProgram.cs Setup
```csharp
using DrawnUi.Draw;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseDrawnUi(new()
            {
                DesktopWindow = new()
                {
                    Width = 500,
                    Height = 800
                }
            });
            
        return builder.Build();
    }
}
```

### Code-Behind Setup
```csharp
using DrawnUi.Draw;

namespace MyApp;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }
}
```

## Result
- Hardware-accelerated rendering of all DrawnUI controls
- Gesture handling enabled for touch interactions
- Pixel-perfect cross-platform UI rendering
- High performance with caching strategies

## Variations

### 1. Programmatic Canvas Creation
```csharp
var canvas = new Canvas()
{
    Gestures = GesturesMode.Enabled,
    RenderingMode = RenderingModeType.Accelerated,
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    Content = new SkiaLayout()
    {
        Type = LayoutType.Column,
        HorizontalOptions = LayoutOptions.Fill,
        Spacing = 16,
        Padding = 32,
        Children = new List<SkiaControl>()
        {
            new SkiaLabel() { Text = "Hello from Code!" },
            new SkiaButton() { Text = "Code Button" }
        }
    }
};

this.Content = canvas;
```

## Related Components
- **Also see**: SkiaLayout, SkiaControl, DrawnUiBasePage
- **Requires**: UseDrawnUi() in MauiProgram.cs
- **Child containers**: SkiaLayout, SkiaScroll, SkiaGrid

## Common Mistakes

### ❌ Missing UseDrawnUi() initialization
```csharp
// Wrong - DrawnUI not initialized
builder.UseMauiApp<App>();
```

### ✅ Correct initialization
```csharp
// Correct - DrawnUI properly initialized
builder.UseMauiApp<App>().UseDrawnUi();
```

### ❌ Mixing MAUI and DrawnUI controls directly
```xml
<!-- Wrong - MAUI controls inside Canvas -->
<draw:Canvas>
    <Button Text="MAUI Button" />  <!-- This won't work -->
</draw:Canvas>
```

### ✅ Use DrawnUI controls only
```xml
<!-- Correct - DrawnUI controls only -->
<draw:Canvas>
    <draw:SkiaButton Text="DrawnUI Button" />
</draw:Canvas>
```

### ❌ Missing hardware acceleration for animated scenarios
```csharp
// Wrong - canvas will be good enough to static content only
```csharp
var canvas = new Canvas()
{
};
```

### ✅ Correct creation
```csharp
// Correct - accelerated canvas for animations and fast shader graphics
```csharp
var canvas = new Canvas()
{
    RenderingMode = RenderingModeType.Accelerated,
};
```

### ❌ Missing gestures support for interactive scenarios
```csharp
// Wrong - canvas will be good enough to static content only
```csharp
var canvas = new Canvas()
{
};
```

### ✅ Correct creation
```csharp
// Correct - gestures enabled for interactive controls
```csharp
var canvas = new Canvas()
{
    Gestures = GesturesMode.Lock
};
```


### ❌ Missing layout fill for top containers for single-canvas screens
```csharp
// Wrong - container will calculate auto-size and drop in performance
```csharp
var canvas = new Canvas()
{
    Gestures = GesturesMode.Enabled,
    RenderingMode = RenderingModeType.Accelerated,
    Content = new SkiaLayout()
    {
        Type = LayoutType.Column,
        Children = new List<SkiaControl>()
        {
            new SkiaLabel() { Text = "Hello from Code!" },
            new SkiaButton() { Text = "Code Button" }
        }
    }
};
```

### ✅ Correct creation
```csharp
// Correct - filling out available space, faster layout pass
```csharp
var canvas = new Canvas()
{
    Gestures = GesturesMode.Enabled,
    RenderingMode = RenderingModeType.Accelerated,
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    Content = new SkiaLayout()
    {
        Type = LayoutType.Column,
        HorizontalOptions = LayoutOptions.Fill,
        Children = new List<SkiaControl>()
        {
            new SkiaLabel() { Text = "Hello from Code!" },
            new SkiaButton() { Text = "Code Button" }
        }
    }
};
```

## Tags
#canvas #setup #core #basic #rendering #gestures #hardware-acceleration #root-container #maui-integration
