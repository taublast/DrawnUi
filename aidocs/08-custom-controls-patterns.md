<!-- Component: Custom Controls, Category: Advanced/Architecture, Complexity: Advanced -->
# Custom Controls - Building Reusable Components

## Scenario
Creating custom controls in DrawnUI allows you to build reusable, encapsulated UI components with their own properties, events, and behaviors. Custom controls can inherit from SkiaControl, SkiaLayout, or other base classes to create specialized functionality. Use custom controls when you need reusable components, complex business logic, or specialized rendering.

## Complete Working Example

### Custom Rating Control
```csharp
using DrawnUi.Draw;

public class StarRating : SkiaLayout
{
    public static readonly BindableProperty RatingProperty = 
        BindableProperty.Create(nameof(Rating), typeof(double), typeof(StarRating), 0.0, 
            propertyChanged: OnRatingChanged);
    
    public static readonly BindableProperty MaxRatingProperty = 
        BindableProperty.Create(nameof(MaxRating), typeof(int), typeof(StarRating), 5,
            propertyChanged: OnMaxRatingChanged);
    
    public static readonly BindableProperty StarSizeProperty = 
        BindableProperty.Create(nameof(StarSize), typeof(double), typeof(StarRating), 24.0,
            propertyChanged: OnStarSizeChanged);
    
    public static readonly BindableProperty IsReadOnlyProperty = 
        BindableProperty.Create(nameof(IsReadOnly), typeof(bool), typeof(StarRating), false);
    
    public double Rating
    {
        get => (double)GetValue(RatingProperty);
        set => SetValue(RatingProperty, value);
    }
    
    public int MaxRating
    {
        get => (int)GetValue(MaxRatingProperty);
        set => SetValue(MaxRatingProperty, value);
    }
    
    public double StarSize
    {
        get => (double)GetValue(StarSizeProperty);
        set => SetValue(StarSizeProperty, value);
    }
    
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }
    
    public event EventHandler<RatingChangedEventArgs> RatingChanged;
    
    private List<SkiaShape> _stars = new();
    
    public StarRating()
    {
        Type = LayoutType.Row;
        Spacing = 4;
        CreateStars();
    }
    
    private void CreateStars()
    {
        Children.Clear();
        _stars.Clear();
        
        for (int i = 0; i < MaxRating; i++)
        {
            var star = new SkiaShape
            {
                Type = ShapeType.Path,
                PathData = "M12,2L15.09,8.26L22,9L17,14L18.18,21L12,17.77L5.82,21L7,14L2,9L8.91,8.26L12,2Z",
                WidthRequest = StarSize,
                HeightRequest = StarSize,
                FillColor = Colors.LightGray,
                Tag = i
            };
            
            if (!IsReadOnly)
            {
                star.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command<int>(OnStarTapped),
                    CommandParameter = i + 1
                });
            }
            
            _stars.Add(star);
            Children.Add(star);
        }
        
        UpdateStarColors();
    }
    
    private void OnStarTapped(int rating)
    {
        if (IsReadOnly) return;
        
        var oldRating = Rating;
        Rating = rating;
        RatingChanged?.Invoke(this, new RatingChangedEventArgs(oldRating, rating));
    }
    
    private void UpdateStarColors()
    {
        for (int i = 0; i < _stars.Count; i++)
        {
            var star = _stars[i];
            var starValue = i + 1;
            
            if (starValue <= Rating)
            {
                star.FillColor = Colors.Gold;
            }
            else if (starValue - 0.5 <= Rating)
            {
                // Half star - could implement gradient or different icon
                star.FillColor = Colors.Orange;
            }
            else
            {
                star.FillColor = Colors.LightGray;
            }
        }
    }
    
    private static void OnRatingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is StarRating control)
        {
            control.UpdateStarColors();
        }
    }
    
    private static void OnMaxRatingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is StarRating control)
        {
            control.CreateStars();
        }
    }
    
    private static void OnStarSizeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is StarRating control)
        {
            foreach (var star in control._stars)
            {
                star.WidthRequest = control.StarSize;
                star.HeightRequest = control.StarSize;
            }
        }
    }
}

public class RatingChangedEventArgs : EventArgs
{
    public double OldRating { get; }
    public double NewRating { get; }
    
    public RatingChangedEventArgs(double oldRating, double newRating)
    {
        OldRating = oldRating;
        NewRating = newRating;
    }
}
```

### XAML Usage of Custom Control
```xml
<draw:Canvas>
    <draw:SkiaLayout Type="Column" Spacing="20" Padding="20">
        
        <!-- Basic rating control -->
        <local:StarRating 
            Rating="3.5"
            MaxRating="5"
            StarSize="32"
            HorizontalOptions="Center" />
            
        <!-- Interactive rating -->
        <local:StarRating 
            x:Name="UserRating"
            Rating="{Binding UserRating, Mode=TwoWay}"
            MaxRating="5"
            StarSize="28"
            IsReadOnly="False"
            RatingChanged="OnUserRatingChanged"
            HorizontalOptions="Center" />
            
        <!-- Read-only rating -->
        <local:StarRating 
            Rating="{Binding AverageRating}"
            MaxRating="5"
            StarSize="20"
            IsReadOnly="True"
            HorizontalOptions="Center" />
    </draw:SkiaLayout>
</draw:Canvas>
```

### Custom Progress Ring Control
```csharp
public class ProgressRing : SkiaControl
{
    public static readonly BindableProperty ProgressProperty = 
        BindableProperty.Create(nameof(Progress), typeof(double), typeof(ProgressRing), 0.0,
            propertyChanged: OnProgressChanged);
    
    public static readonly BindableProperty RingThicknessProperty = 
        BindableProperty.Create(nameof(RingThickness), typeof(double), typeof(ProgressRing), 8.0);
    
    public static readonly BindableProperty ProgressColorProperty = 
        BindableProperty.Create(nameof(ProgressColor), typeof(Color), typeof(ProgressRing), Colors.Blue);
    
    public static readonly BindableProperty BackgroundRingColorProperty = 
        BindableProperty.Create(nameof(BackgroundRingColor), typeof(Color), typeof(ProgressRing), Colors.LightGray);
    
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, Math.Clamp(value, 0, 1));
    }
    
    public double RingThickness
    {
        get => (double)GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }
    
    public Color ProgressColor
    {
        get => (Color)GetValue(ProgressColorProperty);
        set => SetValue(ProgressColorProperty, value);
    }
    
    public Color BackgroundRingColor
    {
        get => (Color)GetValue(BackgroundRingColorProperty);
        set => SetValue(BackgroundRingColorProperty, value);
    }
    
    private SkiaShape _backgroundRing;
    private SkiaShape _progressRing;
    private SkiaLabel _percentageLabel;
    
    public ProgressRing()
    {
        CreateRings();
    }
    
    private void CreateRings()
    {
        // Background ring
        _backgroundRing = new SkiaShape
        {
            Type = ShapeType.Arc,
            StartAngle = 0,
            SweepAngle = 360,
            StrokeWidth = RingThickness,
            StrokeColor = BackgroundRingColor,
            FillColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        
        // Progress ring
        _progressRing = new SkiaShape
        {
            Type = ShapeType.Arc,
            StartAngle = -90, // Start from top
            SweepAngle = 0,
            StrokeWidth = RingThickness,
            StrokeColor = ProgressColor,
            FillColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        
        // Percentage label
        _percentageLabel = new SkiaLabel
        {
            Text = "0%",
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            TextColor = Colors.Black,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        
        // Add to layout
        var container = new SkiaLayout
        {
            Type = LayoutType.Absolute,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        
        container.Children.Add(_backgroundRing);
        container.Children.Add(_progressRing);
        container.Children.Add(_percentageLabel);
        
        Content = container;
    }
    
    private static void OnProgressChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ProgressRing control)
        {
            control.UpdateProgress();
        }
    }
    
    private void UpdateProgress()
    {
        var sweepAngle = Progress * 360;
        _progressRing.SweepAngle = sweepAngle;
        _percentageLabel.Text = $"{Progress:P0}";
    }
    
    public async Task AnimateToProgress(double targetProgress, uint duration = 1000)
    {
        var startProgress = Progress;
        var animation = new Animation(v => Progress = v, startProgress, targetProgress);
        animation.Commit(this, "ProgressAnimation", length: duration, easing: Easing.CubicOut);
    }
}
```

## Result
- Reusable, encapsulated UI components
- Custom properties with data binding support
- Event handling and business logic encapsulation
- Consistent API and behavior across applications

## Variations

### 1. Custom Input Control
```csharp
public class CustomTextInput : SkiaLayout
{
    public static readonly BindableProperty TextProperty = 
        BindableProperty.Create(nameof(Text), typeof(string), typeof(CustomTextInput), string.Empty);
    
    public static readonly BindableProperty PlaceholderProperty = 
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(CustomTextInput), string.Empty);
    
    // Implementation with validation, formatting, etc.
}
```

### 2. Custom Chart Control
```csharp
public class SimpleBarChart : SkiaControl
{
    public static readonly BindableProperty DataProperty = 
        BindableProperty.Create(nameof(Data), typeof(IEnumerable<double>), typeof(SimpleBarChart), null);
    
    // Custom drawing logic for chart rendering
    protected override void OnDraw(SKCanvas canvas, SKRect rect)
    {
        // Custom Skia drawing code
        base.OnDraw(canvas, rect);
    }
}
```

## Related Components
- **Also see**: SkiaControl, SkiaLayout, BindableProperty
- **Requires**: Understanding of MAUI binding system
- **Inheritance**: SkiaControl, SkiaLayout, or specialized base classes

## Common Mistakes

### ❌ Not implementing proper property change handling
```csharp
// Wrong - no property change notification
public double MyProperty { get; set; }
```

### ✅ Use BindableProperty with change handlers
```csharp
// Correct - proper bindable property
public static readonly BindableProperty MyPropertyProperty = 
    BindableProperty.Create(nameof(MyProperty), typeof(double), typeof(MyControl), 0.0,
        propertyChanged: OnMyPropertyChanged);

public double MyProperty
{
    get => (double)GetValue(MyPropertyProperty);
    set => SetValue(MyPropertyProperty, value);
}
```

### ❌ Creating controls without proper cleanup
```csharp
// Wrong - no disposal of resources
public class MyControl : SkiaControl
{
    private Timer _timer = new Timer();
    // No cleanup
}
```

### ✅ Implement proper disposal
```csharp
// Correct - proper resource management
public class MyControl : SkiaControl, IDisposable
{
    private Timer _timer;
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

## Tags
#custom-controls #reusable-components #bindable-properties #encapsulation #architecture #advanced #inheritance #events #data-binding
