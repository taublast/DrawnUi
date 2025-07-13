# Using Gradients in DrawnUi.Maui

DrawnUi.Maui provides powerful gradient support for shapes, text, and images, enabling visually rich and modern UI designs. This article covers the types of gradients available, how to apply them, and practical examples for common scenarios.

## Gradient Types

DrawnUi.Maui supports several gradient types:

- **Linear Gradient**: Colors transition along a straight line.
- **Radial Gradient**: Colors radiate outward from a center point.
- **Sweep Gradient**: Colors sweep around a center point in a circular fashion.

## Applying Gradients to Shapes

You can apply gradients to the background or stroke of any `SkiaShape` using the `BackgroundGradient` and `StrokeGradient` properties.

### Linear Gradient Example

```xml
<DrawUi:SkiaShape Type="Rectangle" CornerRadius="16" WidthRequest="200" HeightRequest="100">
    <DrawUi:SkiaShape.BackgroundGradient>
        <DrawUi:SkiaGradient 
            Type="Linear" 
            StartColor="#FF6A00" 
            EndColor="#FFD800" 
            StartPoint="0,0" 
            EndPoint="1,1" />
    </DrawUi:SkiaShape.BackgroundGradient>
</DrawUi:SkiaShape>
```

Maybe you have colors defined in a static class?

```xml
<draw:SkiaFrame
    AnimationTapped="Ripple"
    Tapped="OnTapped_Item"
    WidthRequest="60"
    StrokeWidth="1"
    HeightRequest="28"
    StrokeColor="{Binding SelectionColor}"
    CornerRadius="4">
    <draw:SkiaControl.FillGradient>
        <draw:SkiaGradient
            Opacity="0.1"
            EndXRatio="0"
            EndYRatio="1"
            StartXRatio="0"
            StartYRatio="0"
            Type="Linear">
            <draw:SkiaGradient.Colors>
                <x:Static Member="xam:BackColors.GradientStartNav"/>
                <x:Static Member="xam:BackColors.GradientEndNav"/>
            </draw:SkiaGradient.Colors>
        </draw:SkiaGradient>
    </draw:SkiaControl.FillGradient>
    <draw:SkiaRichLabel
        HorizontalOptions="Center"
        HorizontalTextAlignment="Center"
        Text="{Binding Title}"
        TextColor="{Binding SelectionColor}"
        VerticalOptions="Center" />
</draw:SkiaFrame>
```

### Radial Gradient Example

```xml
<DrawUi:SkiaShape Type="Circle" WidthRequest="120" HeightRequest="120">
    <DrawUi:SkiaShape.BackgroundGradient>
        <DrawUi:SkiaGradient 
            Type="Radial" 
            StartColor="#00C3FF" 
            EndColor="#FFFF1C" 
            Center="0.5,0.5" 
            Radius="0.5" />
    </DrawUi:SkiaShape.BackgroundGradient>
</DrawUi:SkiaShape>
```

### Sweep Gradient Example

```xml
<DrawUi:SkiaShape Type="Ellipse" WidthRequest="180" HeightRequest="100">
    <DrawUi:SkiaShape.BackgroundGradient>
        <DrawUi:SkiaGradient 
            Type="Sweep" 
            StartColor="#FF0080" 
            EndColor="#7928CA" 
            Center="0.5,0.5" />
    </DrawUi:SkiaShape.BackgroundGradient>
</DrawUi:SkiaShape>
```

### Multi-Stop Gradients

You can define gradients with multiple color stops:

```xml
<DrawUi:SkiaShape Type="Rectangle" WidthRequest="220" HeightRequest="60">
    <DrawUi:SkiaShape.BackgroundGradient>
        <DrawUi:SkiaGradient Type="Linear" StartPoint="0,0" EndPoint="1,0">
            <DrawUi:SkiaGradient.Stops>
                <DrawUi:GradientStop Color="#FF6A00" Offset="0.0" />
                <DrawUi:GradientStop Color="#FFD800" Offset="0.5" />
                <DrawUi:GradientStop Color="#00FFB4" Offset="1.0" />
            </DrawUi:SkiaGradient.Stops>
        </DrawUi:SkiaGradient>
    </DrawUi:SkiaShape.BackgroundGradient>
</DrawUi:SkiaShape>
```

## Applying Gradients to Text

You can apply gradients to text using the `FillGradient` property on `SkiaLabel`:

```xml
<DrawUi:SkiaLabel 
    Text="Gradient Text" 
    FontSize="32" 
    FillGradient="{StaticResource MyGradient}" />
```

Or define inline:

```xml
<DrawUi:SkiaLabel Text="Sunset" FontSize="40">
    <DrawUi:SkiaLabel.FillGradient>
        <DrawUi:SkiaGradient Type="Linear" StartColor="#FF6A00" EndColor="#FFD800" StartPoint="0,0" EndPoint="1,0" />
    </DrawUi:SkiaLabel.FillGradient>
</DrawUi:SkiaLabel>
```

## Applying Gradients to SVG, code behind

You can overlay gradients on images using the `UseGradient`, `StartColor`, and `EndColor` properties on `SkiaImage`:

```csharp
new SkiaSvg()
{
    HorizontalOptions = LayoutOptions.Center,
    HeightRequest = 20,
    LockRatio = 1,
    UseCache = SkiaCacheType.Image,
    FillGradient =
        new SkiaGradient()
        {
            StartXRatio = 1,
            EndXRatio = 0,
            StartYRatio = 0,
            EndYRatio = 1,
            Colors =
                new Color[] { BackColors.GradientStartNav,
                    BackColors.GradientEndNav }
        },
}
```

This creates a fade effect from transparent to black over the image.

## Defining Gradients as Resources

For reuse, define gradients as resources:

```xml
<ContentPage.Resources>
    <DrawUi:SkiaGradient x:Key="MyGradient" Type="Linear" StartColor="#FF6A00" EndColor="#FFD800" StartPoint="0,0" EndPoint="1,1" />
</ContentPage.Resources>
```

Then reference with:

```xml
<DrawUi:SkiaLabel Text="Reusable Gradient" FillGradient="{StaticResource MyGradient}" />
```

## C# Example: Creating a Gradient in Code

```csharp
var gradient = new SkiaGradient
{
    Type = SkiaGradientType.Linear,
    StartColor = Colors.Red,
    EndColor = Colors.Yellow,
    StartPoint = new Point(0, 0),
    EndPoint = new Point(1, 1)
};

control.FillGradient = gradient;
```

## Tips and Best Practices

- Use gradients to add depth and visual interest to your UI.
- For performance, prefer simple gradients or reuse gradient resources.
- Gradients can be animated by changing their properties 
- Combine gradients with shadows for modern card and button designs.

