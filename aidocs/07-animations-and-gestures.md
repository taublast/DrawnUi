<!-- Component: Animations, Category: Animation/Interaction, Complexity: Advanced -->
# Animations and Gestures - Interactive UI Patterns

## Scenario
DrawnUI provides powerful animation and gesture systems for creating fluid, interactive user experiences. It supports property animations, transforms, gesture recognizers, and physics-based animations. Use animations and gestures when you need smooth transitions, interactive feedback, or complex user interactions.

## Complete Working Example

### Basic Property Animations
```xml
<draw:Canvas>
    <draw:SkiaLayout Type="Column" Spacing="20" Padding="20">
        
        <!-- Animated button with hover effects -->
        <draw:SkiaButton 
            x:Name="AnimatedButton"
            Text="Hover Me"
            WidthRequest="200"
            HeightRequest="50"
            BackgroundColor="Blue"
            TextColor="White"
            CornerRadius="8"
            HorizontalOptions="Center">
            
            <draw:SkiaButton.Triggers>
                <!-- Hover animation -->
                <EventTrigger Event="MouseEntered">
                    <draw:BeginAnimation>
                        <draw:ColorAnimation 
                            TargetProperty="BackgroundColor"
                            To="DarkBlue"
                            Duration="200" />
                        <draw:DoubleAnimation 
                            TargetProperty="Scale"
                            To="1.05"
                            Duration="200" />
                    </draw:BeginAnimation>
                </EventTrigger>
                
                <EventTrigger Event="MouseLeft">
                    <draw:BeginAnimation>
                        <draw:ColorAnimation 
                            TargetProperty="BackgroundColor"
                            To="Blue"
                            Duration="200" />
                        <draw:DoubleAnimation 
                            TargetProperty="Scale"
                            To="1.0"
                            Duration="200" />
                    </draw:BeginAnimation>
                </EventTrigger>
            </draw:SkiaButton.Triggers>
        </draw:SkiaButton>
        
        <!-- Animated loading indicator -->
        <draw:SkiaShape 
            x:Name="LoadingIndicator"
            Type="Arc"
            StartAngle="0"
            SweepAngle="90"
            StrokeColor="Orange"
            StrokeWidth="4"
            FillColor="Transparent"
            WidthRequest="40"
            HeightRequest="40"
            HorizontalOptions="Center">
            
            <draw:SkiaShape.Triggers>
                <DataTrigger TargetType="draw:SkiaShape" Binding="{Binding IsLoading}" Value="True">
                    <DataTrigger.EnterActions>
                        <draw:BeginAnimation>
                            <draw:DoubleAnimation 
                                TargetProperty="Rotation"
                                From="0" 
                                To="360" 
                                Duration="1000" 
                                RepeatBehavior="Forever" />
                        </draw:BeginAnimation>
                    </DataTrigger.EnterActions>
                    <DataTrigger.ExitActions>
                        <draw:StopAnimation TargetProperty="Rotation" />
                    </DataTrigger.ExitActions>
                </DataTrigger>
            </draw:SkiaShape.Triggers>
        </draw:SkiaShape>
        
        <!-- Gesture-controlled card -->
        <draw:SkiaLayout 
            x:Name="SwipeCard"
            Type="Column"
            Spacing="12"
            Padding="20"
            BackgroundColor="White"
            CornerRadius="12"
            WidthRequest="300"
            HeightRequest="200"
            HorizontalOptions="Center">
            
            <draw:SkiaLayout.Shadows>
                <draw:SkiaShadow X="0" Y="4" Blur="8" Color="Black" Opacity="0.1" />
            </draw:SkiaLayout.Shadows>
            
            <draw:SkiaLayout.GestureRecognizers>
                <PanGestureRecognizer PanUpdated="OnCardPanned" />
                <TapGestureRecognizer Tapped="OnCardTapped" />
            </draw:SkiaLayout.GestureRecognizers>
            
            <draw:SkiaLabel 
                Text="Swipe or Tap Me"
                FontSize="18"
                FontWeight="Bold"
                HorizontalOptions="Center" />
            <draw:SkiaLabel 
                Text="Interactive card with gestures"
                FontSize="14"
                TextColor="Gray"
                HorizontalOptions="Center" />
        </draw:SkiaLayout>
    </draw:SkiaLayout>
</draw:Canvas>
```

### Code-Behind Animation Logic
```csharp
using DrawnUi.Draw;

public partial class MainPage : ContentPage
{
    private bool _isLoading = false;
    private double _cardOriginalX;
    
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }
    
    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
        _cardOriginalX = SwipeCard.TranslationX;
    }
    
    private async void OnCardPanned(object sender, PanUpdatedEventArgs e)
    {
        var card = SwipeCard;
        
        switch (e.StatusType)
        {
            case GestureStatus.Running:
                // Follow finger movement
                card.TranslationX = _cardOriginalX + e.TotalX;
                
                // Add rotation based on swipe distance
                var rotation = Math.Max(-15, Math.Min(15, e.TotalX / 10));
                card.Rotation = rotation;
                
                // Change opacity based on distance
                var opacity = Math.Max(0.5, 1 - Math.Abs(e.TotalX) / 300);
                card.Opacity = opacity;
                break;
                
            case GestureStatus.Completed:
                // Determine if card should be dismissed or return
                if (Math.Abs(e.TotalX) > 100)
                {
                    await DismissCard(e.TotalX > 0);
                }
                else
                {
                    await ReturnCardToPosition();
                }
                break;
        }
    }
    
    private async Task DismissCard(bool swipeRight)
    {
        var card = SwipeCard;
        var targetX = swipeRight ? 400 : -400;
        
        // Animate card off screen
        var moveAnimation = card.TranslateTo(targetX, card.TranslationY, 300, Easing.CubicOut);
        var fadeAnimation = card.FadeTo(0, 300);
        var rotateAnimation = card.RotateTo(swipeRight ? 30 : -30, 300);
        
        await Task.WhenAll(moveAnimation, fadeAnimation, rotateAnimation);
        
        // Reset card position for next interaction
        await ResetCard();
    }
    
    private async Task ReturnCardToPosition()
    {
        var card = SwipeCard;
        
        // Animate back to original position
        var moveAnimation = card.TranslateTo(_cardOriginalX, card.TranslationY, 250, Easing.SpringOut);
        var fadeAnimation = card.FadeTo(1, 250);
        var rotateAnimation = card.RotateTo(0, 250);
        
        await Task.WhenAll(moveAnimation, fadeAnimation, rotateAnimation);
    }
    
    private async Task ResetCard()
    {
        var card = SwipeCard;
        
        // Reset all properties instantly
        card.TranslationX = _cardOriginalX;
        card.TranslationY = 0;
        card.Rotation = 0;
        card.Opacity = 0;
        
        // Animate back in
        await card.FadeTo(1, 300, Easing.CubicOut);
    }
    
    private async void OnCardTapped(object sender, EventArgs e)
    {
        var card = SwipeCard;
        
        // Pulse animation on tap
        await card.ScaleTo(1.1, 100, Easing.CubicOut);
        await card.ScaleTo(1.0, 100, Easing.CubicIn);
        
        // Toggle loading state
        IsLoading = !IsLoading;
    }
    
    // Programmatic animations
    private async Task AnimateButtonSequence()
    {
        var button = AnimatedButton;
        
        // Complex animation sequence
        await button.ScaleTo(1.2, 200);
        await button.RotateTo(360, 500);
        await Task.WhenAll(
            button.ScaleTo(1.0, 200),
            button.RotateTo(0, 200)
        );
    }
}
```

## Result
- Smooth 60fps animations with hardware acceleration
- Responsive gesture interactions with physics
- Complex animation sequences and state management
- Platform-optimized performance

## Variations

### 1. Staggered List Animation
```csharp
private async Task AnimateListItems(SkiaLayout listContainer)
{
    var children = listContainer.Children.ToList();
    var tasks = new List<Task>();
    
    for (int i = 0; i < children.Count; i++)
    {
        var child = children[i];
        var delay = i * 50; // Stagger by 50ms
        
        // Start from invisible and below
        child.Opacity = 0;
        child.TranslationY = 30;
        
        tasks.Add(Task.Run(async () =>
        {
            await Task.Delay(delay);
            await Task.WhenAll(
                child.FadeTo(1, 300),
                child.TranslateTo(0, 0, 300, Easing.CubicOut)
            );
        }));
    }
    
    await Task.WhenAll(tasks);
}
```

### 2. Physics-Based Spring Animation
```csharp
private async Task SpringAnimation(SkiaControl control)
{
    // Custom spring animation using multiple keyframes
    var keyframes = new[]
    {
        (0.0, 1.0),    // Start
        (0.3, 1.3),    // Overshoot
        (0.6, 0.9),    // Undershoot
        (0.8, 1.05),   // Small overshoot
        (1.0, 1.0)     // Final
    };
    
    foreach (var (time, scale) in keyframes)
    {
        var duration = (uint)(time * 500); // Total 500ms
        await control.ScaleTo(scale, duration, Easing.Linear);
    }
}
```

### 3. Parallax Scroll Effect
```xml
<draw:SkiaScroll x:Name="ParallaxScroll" Scrolled="OnParallaxScrolled">
    <draw:SkiaLayout Type="Column">
        <!-- Background layer (moves slower) -->
        <draw:SkiaImage 
            x:Name="BackgroundLayer"
            Source="background.jpg"
            Aspect="AspectCover"
            HeightRequest="300" />
            
        <!-- Foreground content (normal speed) -->
        <draw:SkiaLayout Type="Column" Spacing="20" Padding="20">
            <!-- Content items -->
        </draw:SkiaLayout>
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

```csharp
private void OnParallaxScrolled(object sender, ScrolledEventArgs e)
{
    // Move background at half speed for parallax effect
    BackgroundLayer.TranslationY = e.ScrollY * 0.5;
}
```

## Related Components
- **Also see**: SkiaControl, GestureRecognizers, Triggers
- **Requires**: Canvas container for hardware acceleration
- **Animation types**: Property, Transform, Color, Path animations

## Common Mistakes

### ❌ Blocking UI thread with animations
```csharp
// Wrong - blocking animation
for (int i = 0; i < 100; i++)
{
    control.TranslationX = i;
    Thread.Sleep(10); // Blocks UI
}
```

### ✅ Use async animations
```csharp
// Correct - async animation
await control.TranslateTo(100, 0, 1000);
```

### ❌ Not disposing animation resources
```csharp
// Wrong - animation keeps running
var animation = new DoubleAnimation();
// Never stopped or disposed
```

### ✅ Properly manage animation lifecycle
```csharp
// Correct - stop animations when done
private CancellationTokenSource _animationCts;

public async Task StartAnimation()
{
    _animationCts = new CancellationTokenSource();
    try
    {
        await AnimateWithCancellation(_animationCts.Token);
    }
    catch (OperationCanceledException) { }
}

public void StopAnimation()
{
    _animationCts?.Cancel();
}
```

## Tags
#animations #gestures #interactions #transforms #physics #spring #parallax #staggered #performance #advanced #60fps #hardware-acceleration
