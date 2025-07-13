<!-- Component: SkiaShape, Category: Graphics/Custom, Complexity: Intermediate -->
# SkiaShape - Custom Graphics and Shapes

## Scenario
SkiaShape enables creation of custom graphics, geometric shapes, and complex visual elements using vector graphics. It supports various shape types (Rectangle, Circle, Ellipse, Path), gradients, strokes, and custom drawing. Use SkiaShape when you need custom visual elements, icons, decorative graphics, or complex geometric shapes.

## Complete Working Example

### Basic Shapes Collection
```xml
<draw:Canvas>
    <draw:SkiaLayout Type="Grid" ColumnDefinitions="*,*" RowDefinitions="*,*,*" Spacing="16" Padding="20">
        
        <!-- Rectangle with gradient -->
        <draw:SkiaShape 
            Type="Rectangle"
            WidthRequest="120"
            HeightRequest="80"
            CornerRadius="8"
            Grid.Row="0" Grid.Column="0">
            
            <draw:SkiaShape.Fill>
                <draw:SkiaGradient 
                    Type="Linear"
                    StartColor="Blue"
                    EndColor="Purple"
                    Angle="45" />
            </draw:SkiaShape.Fill>
        </draw:SkiaShape>
        
        <!-- Circle with stroke -->
        <draw:SkiaShape 
            Type="Circle"
            WidthRequest="100"
            HeightRequest="100"
            StrokeColor="Red"
            StrokeWidth="4"
            FillColor="Transparent"
            Grid.Row="0" Grid.Column="1" />
            
        <!-- Custom path shape -->
        <draw:SkiaShape 
            Type="Path"
            PathData="M 10,30 A 20,20 0,0,1 50,30 A 20,20 0,0,1 90,30 Q 90,60 50,90 Q 10,60 10,30 z"
            FillColor="Pink"
            WidthRequest="100"
            HeightRequest="100"
            Grid.Row="1" Grid.Column="0" />
            
        <!-- Star shape with shadow -->
        <draw:SkiaShape 
            Type="Path"
            PathData="M50,5 L61,35 L95,35 L68,57 L79,91 L50,70 L21,91 L32,57 L5,35 L39,35 Z"
            FillColor="Gold"
            WidthRequest="100"
            HeightRequest="100"
            Grid.Row="1" Grid.Column="1">
            
            <draw:SkiaShape.Shadows>
                <draw:SkiaShadow 
                    X="2" Y="2" 
                    Blur="4" 
                    Color="Orange" 
                    Opacity="0.6" />
            </draw:SkiaShape.Shadows>
        </draw:SkiaShape>
        
        <!-- Progress ring -->
        <draw:SkiaShape 
            Type="Arc"
            StartAngle="0"
            SweepAngle="270"
            StrokeColor="Green"
            StrokeWidth="8"
            FillColor="Transparent"
            WidthRequest="80"
            HeightRequest="80"
            Grid.Row="2" Grid.Column="0" />
            
        <!-- Custom icon -->
        <draw:SkiaShape 
            Type="Path"
            PathData="M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M11,16.5L6.5,12L7.91,10.59L11,13.67L16.59,8.09L18,9.5L11,16.5Z"
            FillColor="Green"
            WidthRequest="60"
            HeightRequest="60"
            Grid.Row="2" Grid.Column="1" />
    </draw:SkiaLayout>
</draw:Canvas>
```

### Interactive Custom Control
```xml
<draw:SkiaLayout Type="Column" Spacing="20" Padding="20">
    
    <!-- Custom toggle switch -->
    <draw:SkiaShape 
        x:Name="ToggleBackground"
        Type="Rectangle"
        WidthRequest="60"
        HeightRequest="30"
        CornerRadius="15"
        FillColor="LightGray"
        HorizontalOptions="Center">
        
        <draw:SkiaShape.GestureRecognizers>
            <TapGestureRecognizer Tapped="OnToggleTapped" />
        </draw:SkiaShape.GestureRecognizers>
        
        <!-- Toggle thumb -->
        <draw:SkiaShape 
            x:Name="ToggleThumb"
            Type="Circle"
            WidthRequest="26"
            HeightRequest="26"
            FillColor="White"
            TranslationX="-15"
            VerticalOptions="Center"
            HorizontalOptions="Center">
            
            <draw:SkiaShape.Shadows>
                <draw:SkiaShadow 
                    X="0" Y="1" 
                    Blur="3" 
                    Color="Black" 
                    Opacity="0.3" />
            </draw:SkiaShape.Shadows>
        </draw:SkiaShape>
    </draw:SkiaShape>
    
    <!-- Custom progress bar -->
    <draw:SkiaLayout Type="Absolute" WidthRequest="200" HeightRequest="8">
        <!-- Background track -->
        <draw:SkiaShape 
            Type="Rectangle"
            WidthRequest="200"
            HeightRequest="8"
            CornerRadius="4"
            FillColor="LightGray"
            AbsoluteLayout.LayoutBounds="0,0,200,8" />
            
        <!-- Progress fill -->
        <draw:SkiaShape 
            x:Name="ProgressFill"
            Type="Rectangle"
            WidthRequest="120"
            HeightRequest="8"
            CornerRadius="4"
            FillColor="Blue"
            AbsoluteLayout.LayoutBounds="0,0,120,8" />
    </draw:SkiaLayout>
</draw:SkiaLayout>
```

### Code-Behind for Interactive Shapes
```csharp
using DrawnUi.Draw;

public partial class MainPage : ContentPage
{
    private bool _isToggled = false;
    private double _progress = 0.6; // 60%
    
    public MainPage()
    {
        InitializeComponent();
        UpdateProgressBar();
    }
    
    private async void OnToggleTapped(object sender, EventArgs e)
    {
        _isToggled = !_isToggled;
        
        // Animate toggle switch
        var background = ToggleBackground;
        var thumb = ToggleThumb;
        
        var targetColor = _isToggled ? Colors.Green : Colors.LightGray;
        var targetX = _isToggled ? 15 : -15;
        
        // Animate background color and thumb position
        var colorAnimation = background.ColorTo(targetColor, 200);
        var positionAnimation = thumb.TranslateTo(targetX, thumb.TranslationY, 200);
        
        await Task.WhenAll(colorAnimation, positionAnimation);
    }
    
    private void UpdateProgressBar()
    {
        var progressWidth = 200 * _progress;
        ProgressFill.WidthRequest = progressWidth;
    }
    
    // Method to update progress programmatically
    public async Task SetProgress(double progress, bool animated = true)
    {
        _progress = Math.Clamp(progress, 0, 1);
        var targetWidth = 200 * _progress;
        
        if (animated)
        {
            await ProgressFill.LayoutTo(new Rect(0, 0, targetWidth, 8), 300);
        }
        else
        {
            ProgressFill.WidthRequest = targetWidth;
        }
    }
}
```

## Result
- Custom vector graphics with precise control
- Smooth animations and interactions
- Hardware-accelerated rendering
- Scalable graphics that work on all screen densities

## Variations

### 1. Animated Loading Spinner
```xml
<draw:SkiaShape 
    x:Name="LoadingSpinner"
    Type="Arc"
    StartAngle="0"
    SweepAngle="90"
    StrokeColor="Blue"
    StrokeWidth="4"
    FillColor="Transparent"
    WidthRequest="40"
    HeightRequest="40">
    
    <draw:SkiaShape.Triggers>
        <DataTrigger TargetType="draw:SkiaShape" Binding="{Binding IsLoading}" Value="True">
            <DataTrigger.EnterActions>
                <draw:BeginAnimation>
                    <draw:RotationAnimation 
                        From="0" 
                        To="360" 
                        Duration="1000" 
                        RepeatForever="True" />
                </draw:BeginAnimation>
            </DataTrigger.EnterActions>
        </DataTrigger>
    </draw:SkiaShape.Triggers>
</draw:SkiaShape>
```

### 2. Custom Chart Elements
```csharp
// Create pie chart segment
var pieSegment = new SkiaShape()
{
    Type = ShapeType.Arc,
    StartAngle = 0,
    SweepAngle = 120, // 1/3 of circle
    FillColor = Colors.Blue,
    StrokeColor = Colors.White,
    StrokeWidth = 2,
    WidthRequest = 100,
    HeightRequest = 100
};

// Create bar chart bar
var barChart = new SkiaShape()
{
    Type = ShapeType.Rectangle,
    WidthRequest = 30,
    HeightRequest = 80,
    CornerRadius = 4,
    Fill = new SkiaGradient()
    {
        Type = GradientType.Linear,
        StartColor = Colors.LightBlue,
        EndColor = Colors.DarkBlue,
        Angle = 90
    }
};
```

### 3. Custom Button with Shape
```xml
<draw:SkiaButton 
    WidthRequest="120" 
    HeightRequest="120"
    BackgroundColor="Transparent"
    CornerRadius="60">
    
    <!-- Custom circular button content -->
    <draw:SkiaShape 
        Type="Circle"
        WidthRequest="100"
        HeightRequest="100"
        FillColor="Orange"
        HorizontalOptions="Center"
        VerticalOptions="Center">
        
        <!-- Plus icon -->
        <draw:SkiaShape 
            Type="Path"
            PathData="M40,20 L60,20 L60,40 L80,40 L80,60 L60,60 L60,80 L40,80 L40,60 L20,60 L20,40 L40,40 Z"
            FillColor="White"
            WidthRequest="60"
            HeightRequest="60"
            HorizontalOptions="Center"
            VerticalOptions="Center" />
    </draw:SkiaShape>
</draw:SkiaButton>
```

## Related Components
- **Also see**: SkiaGradient, SkiaShadow, SkiaPath, SkiaImage
- **Requires**: Canvas container
- **Effects**: SkiaGradient, SkiaShadow, SkiaFilter

## Common Mistakes

### ❌ Complex paths without proper sizing
```xml
<!-- Wrong - no size specified for complex path -->
<draw:SkiaShape 
    Type="Path"
    PathData="M10,10 L50,50 L90,10 Z" />
```

### ✅ Always specify dimensions for paths
```xml
<!-- Correct - explicit dimensions -->
<draw:SkiaShape 
    Type="Path"
    PathData="M10,10 L50,50 L90,10 Z"
    WidthRequest="100"
    HeightRequest="60" />
```

### ❌ Using bitmap images for simple shapes
```xml
<!-- Wrong - using image for simple shape -->
<draw:SkiaImage Source="circle.png" />
```

### ✅ Use SkiaShape for vector graphics
```xml
<!-- Correct - vector shape -->
<draw:SkiaShape 
    Type="Circle"
    FillColor="Blue"
    WidthRequest="50"
    HeightRequest="50" />
```

### ❌ Not optimizing complex path data
```xml
<!-- Wrong - overly complex path -->
<draw:SkiaShape PathData="M1.1,2.2 L3.3333,4.4444 L5.55555,6.66666..." />
```

### ✅ Simplify and optimize paths
```xml
<!-- Correct - simplified path -->
<draw:SkiaShape PathData="M1,2 L3,4 L6,7 Z" />
```

## Tags
#skiashape #shapes #vector-graphics #custom-graphics #path #gradients #shadows #animations #interactive #charts #icons #intermediate
