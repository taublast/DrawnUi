# Interactive Button Tutorial: Game-Style UI Elements

Learn how to create stunning interactive buttons with visual effects, just like those found in modern games! This tutorial shows you how to build a button with bevel effects, gradients, and smooth press animations.

## 🎯 What We'll Build

A beautiful interactive button featuring:
- **✨ Bevel/Emboss effects** with depth and lighting
- **🌈 Gradient backgrounds** with smooth color transitions
- **🎮 Press animations** with realistic visual feedback
- **⚡ Optimized performance** with smart caching

<img src="../images/interactive-button.png" alt="Interactive Button" width="200" style="margin: 16px 0;" />

## 🛠️ Step-by-Step Implementation

### 1. 📱 Setup Your MAUI Page

First, let's create a basic MAUI page with a DrawnUI Canvas:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage x:Class="ButtonDemo.MainPage"
             xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw">

    <draw:Canvas x:Name="MainCanvas" 
                 BackgroundColor="DarkSlateBlue">
        
        <!-- Button container -->
        <draw:SkiaLayout x:Name="ButtonContainer"
                         HorizontalOptions="Center"
                         VerticalOptions="Center" />
                         
    </draw:Canvas>

</ContentPage>
```

### 2. 🏗️ Create the Button Factory

Now let's create our button factory method with all the visual effects:

```csharp
using DrawnUi.Maui.Draw;

namespace ButtonDemo;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        CreateAwesomeButton();
    }

    void CreateAwesomeButton()
    {
        var button = CreateInteractiveButton("PLAY GAME", () => 
        {
            DisplayAlert("Success!", "Button was pressed! 🎉", "OK");
        });
        
        ButtonContainer.Children.Add(button);
    }

    public static SkiaShape CreateInteractiveButton(string caption, Action action)
    {
        return new SkiaShape()
        {
            UseCache = SkiaCacheType.Image,
            CornerRadius = 8,
            MinimumWidthRequest = 120,
            BackgroundColor = Colors.Black,
            BevelType = BevelType.Bevel,
            Bevel = new SkiaBevel()
            {
                Depth = 2,
                LightColor = Colors.White,
                ShadowColor = Colors.DarkBlue,
                Opacity = 0.33,
            },
            Children =
            {
                new SkiaRichLabel()
                {
                    Text = caption,
                    Margin = new Thickness(16, 8, 16, 10),
                    UseCache = SkiaCacheType.Operations,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                }
            },
            FillGradient = new SkiaGradient()
            {
                StartXRatio = 0,
                EndXRatio = 1,
                StartYRatio = 0,
                EndYRatio = 0.5f,
                Colors = new Color[]
                {
                    Colors.HotPink,
                    Colors.DeepPink,
                }
            },
        }.WithGestures((me, args, b) =>
        {
            if (args.Type == TouchActionResult.Tapped)
            {
                action?.Invoke();
            }
            else if (args.Type == TouchActionResult.Down)
            {
                SetButtonPressed(me);
            }
            else if (args.Type == TouchActionResult.Up)
            {
                SetButtonReleased(me);
                return null;
            }
            return me;
        });
    }

    // Visual feedback for button press
    public static void SetButtonPressed(SkiaShape btn)
    {
        btn.Children[0].TranslationX = 1;
        btn.Children[0].TranslationY = 1;
        btn.BevelType = BevelType.Emboss;
    }

    public static void SetButtonReleased(SkiaShape btn)
    {
        btn.Children[0].TranslationX = 0;
        btn.Children[0].TranslationY = 0;
        btn.BevelType = BevelType.Bevel;
    }
}
```

Now let's wrap it inside a Canvas to be used inside a usual MAUI app - that's all you need to integrate this awesome button into any existing MAUI project!

## 🎨 Understanding the Visual Effects

### Bevel Effects
The `SkiaBevel` creates a 3D appearance:
- **Depth**: Controls how pronounced the effect is
- **LightColor**: Simulates light hitting the surface
- **ShadowColor**: Creates depth with shadows
- **BevelType**: Switches between raised (Bevel) and pressed (Emboss)

### Gradient Backgrounds
The `SkiaGradient` adds visual appeal:
- **StartXRatio/EndXRatio**: Horizontal gradient direction
- **StartYRatio/EndYRatio**: Vertical gradient direction
- **Colors**: Array of colors for smooth transitions

### Press Animation
The gesture handling provides tactile feedback:
- **Translation**: Moves the text slightly when pressed
- **Bevel switching**: Changes from raised to pressed appearance

## 🚀 Advanced Customization

### Different Color Schemes

```csharp
// Blue theme
FillGradient = new SkiaGradient()
{
    Colors = new Color[] { Colors.CornflowerBlue, Colors.DarkBlue }
}

// Green theme  
FillGradient = new SkiaGradient()
{
    Colors = new Color[] { Colors.LimeGreen, Colors.DarkGreen }
}

// Orange theme
FillGradient = new SkiaGradient()
{
    Colors = new Color[] { Colors.Orange, Colors.DarkOrange }
}
```

### Custom Shapes

```csharp
// Rounded button
CornerRadius = 25,

// Pill-shaped button
CornerRadius = 50,

// Sharp edges
CornerRadius = 0,
```

### Different Sizes

```csharp
// Large button
MinimumWidthRequest = 200,
Margin = new Thickness(24, 12, 24, 14),

// Small button  
MinimumWidthRequest = 80,
Margin = new Thickness(12, 6, 12, 8),
```

## 💡 Performance Tips

1. **Use Caching**: `UseCache = SkiaCacheType.Image` for the button shape
2. **Cache Text**: `UseCache = SkiaCacheType.Operations` for labels
3. **Minimize Redraws**: Only change properties that need visual updates
4. **Batch Updates**: Group multiple property changes together

## 🎯 What You've Learned

- **Visual Effects**: How to create depth with bevel effects
- **Gradients**: Adding beautiful color transitions
- **Gesture Handling**: Implementing interactive press states
- **Performance**: Using caching for smooth animations
- **Customization**: Adapting the button for different themes

## 🚀 Next Steps

Try experimenting with:
- **Different gradient directions** (diagonal, radial)
- **Multiple buttons** with different themes
- **Icon integration** using SkiaSvg
- **Animation effects** with SkiaLottie

This button technique can be used throughout your app for a consistent, professional game-like interface! 🎮
