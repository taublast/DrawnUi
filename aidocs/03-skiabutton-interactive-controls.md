<!-- Component: SkiaButton, Category: Controls/Interactive, Complexity: Basic -->
# SkiaButton - Interactive Button Control

## Scenario
SkiaButton provides platform-styled button controls with advanced customization options, gesture handling, and visual states. It supports different platform styles (iOS, Android, Windows), custom content, icons, and animations. Use SkiaButton when you need interactive buttons with consistent cross-platform appearance or custom styling.

## Complete Working Example

### Basic Button with Platform Styling
```xml
<draw:Canvas>
    <draw:SkiaLayout Type="Column" Spacing="16" Padding="20">
        
        <!-- Platform-styled button -->
        <draw:SkiaButton 
            Text="Platform Button"
            ControlStyle="Platform"
            WidthRequest="200"
            HeightRequest="44"
            HorizontalOptions="Center"
            CommandTapped="{Binding ButtonClickedCommand}" />
            
        <!-- Custom styled button -->
        <draw:SkiaButton 
            Text="Custom Button"
            BackgroundColor="Blue"
            TextColor="White"
            CornerRadius="8"
            FontSize="16"
            FontWeight="Bold"
            WidthRequest="200"
            HeightRequest="44"
            HorizontalOptions="Center"
            Clicked="OnCustomButtonClicked" />
            
        <!-- Button with icon -->
        <draw:SkiaButton 
            Text="Download"
            IconText="⬇"
            IconPosition="Left"
            IconSpacing="8"
            BackgroundColor="Green"
            TextColor="White"
            CornerRadius="12"
            WidthRequest="200"
            HeightRequest="44"
            HorizontalOptions="Center" />
    </draw:SkiaLayout>
</draw:Canvas>
```

### Code-Behind Event Handling
```csharp
using DrawnUi.Draw;
using System.Windows.Input;

public partial class MainPage : ContentPage
{
    public ICommand ButtonClickedCommand { get; }
    
    public MainPage()
    {
        InitializeComponent();
        
        ButtonClickedCommand = new Command(() => 
        {
            DisplayAlert("Button", "Platform button clicked!", "OK");
        });
        
        BindingContext = this;
    }
    
    private void OnCustomButtonClicked(object sender, EventArgs e)
    {
        if (sender is SkiaButton button)
        {
            DisplayAlert("Button", $"Custom button '{button.Text}' clicked!", "OK");
        }
    }
}
```

### Advanced Button with Custom Content
```xml
<draw:SkiaButton 
    WidthRequest="250" 
    HeightRequest="60"
    BackgroundColor="Transparent"
    CornerRadius="16"
    StrokeColor="Blue"
    StrokeWidth="2"
    HorizontalOptions="Center">
    
    <!-- Custom button content -->
    <draw:SkiaLayout Type="Row" Spacing="12" Padding="16">
        <draw:SkiaShape 
            Type="Circle" 
            WidthRequest="32" 
            HeightRequest="32"
            BackgroundColor="Blue" />
            
        <draw:SkiaLayout Type="Column" Spacing="2">
            <draw:SkiaLabel 
                Text="Custom Button" 
                FontSize="16" 
                FontWeight="Bold"
                TextColor="Blue" />
            <draw:SkiaLabel 
                Text="With subtitle" 
                FontSize="12" 
                TextColor="Gray" />
        </draw:SkiaLayout>
    </draw:SkiaLayout>
</draw:SkiaButton>
```

## Result
- Platform-consistent button appearance and behavior
- Touch feedback with visual state changes
- Command binding support for MVVM patterns
- Customizable styling and content

## Variations

### 1. Material Design Button
```xml
<draw:SkiaButton 
    Text="Material Button"
    ControlStyle="Material"
    BackgroundColor="#2196F3"
    TextColor="White"
    CornerRadius="4"
    WidthRequest="160"
    HeightRequest="36" />
```

### 2. iOS Style Button
```xml
<draw:SkiaButton 
    Text="iOS Button"
    ControlStyle="iOS"
    BackgroundColor="#007AFF"
    TextColor="White"
    CornerRadius="8"
    FontSize="17"
    WidthRequest="160"
    HeightRequest="44" />
```

### 3. Toggle Button
```xml
<draw:SkiaButton 
    x:Name="ToggleButton"
    Text="Toggle Me"
    IsToggled="{Binding IsToggled}"
    BackgroundColor="{Binding IsToggled, Converter={StaticResource BoolToColorConverter}}"
    TextColor="White"
    CornerRadius="8" />
```

### 4. Programmatic Button Creation
```csharp
var button = new SkiaButton()
{
    Text = "Dynamic Button",
    BackgroundColor = Colors.Purple,
    TextColor = Colors.White,
    CornerRadius = 10,
    WidthRequest = 180,
    HeightRequest = 40,
    HorizontalOptions = LayoutOptions.Center
};

button.Clicked += (s, e) =>
{
    // Handle click
};

// Or with fluent syntax
var fluentButton = new SkiaButton("Fluent Button")
    .OnTapped(btn => DisplayAlert("Info", "Fluent button clicked!", "OK"));
```

### 5. Advanced Fluent API Patterns
```csharp
// Real-world example from AppoMobi.Mobile project
SkiaButton actionButton;

var layout = new SkiaLayout()
{
    Type = LayoutType.Column,
    Spacing = 16,
    Children = new List<SkiaControl>
    {
        new SkiaButton("Save Changes")
        {
            BackgroundColor = Colors.Blue,
            TextColor = Colors.White,
            CornerRadius = 8
        }
        .Assign(out actionButton)
        .OnTapped(async btn =>
        {
            btn.IsEnabled = false;
            btn.Text = "Saving...";

            try
            {
                await SaveChangesAsync();
                btn.Text = "Saved!";
                await Task.Delay(1000);
                btn.Text = "Save Changes";
            }
            finally
            {
                btn.IsEnabled = true;
            }
        })
        .WithCache(SkiaCacheType.Operations)
        .Height(44)
        .CenterX(),

        new SkiaButton("Cancel")
            .OnTapped(btn => Navigation.PopAsync())
            .Adapt(btn =>
            {
                if (HasUnsavedChanges)
                {
                    btn.BackgroundColor = Colors.Orange;
                    btn.Text = "Discard Changes";
                }
            })
    }
}
.Initialize(layout =>
{
    // Access assigned variables after construction
    actionButton.CommandTapped = new Command(() =>
    {
        // Alternative command binding
    });
});
```

### 5. Button with Animation
```csharp
var animatedButton = new SkiaButton()
{
    Text = "Animated Button",
    BackgroundColor = Colors.Orange,
    TextColor = Colors.White
};

animatedButton.Down += (s, e) => 
{
    // Scale down animation on press
    animatedButton.ScaleTo(0.95, 100);
};

animatedButton.Up += (s, e) => 
{
    // Scale back up on release
    animatedButton.ScaleTo(1.0, 100);
};
```

## Related Components
- **Also see**: SkiaToggle, SkiaCheckbox, SkiaSwitch, SkiaShape
- **Requires**: Canvas container
- **Child content**: Any SkiaControl for custom button content

## Common Mistakes

### ❌ Not setting proper button dimensions
```xml
<!-- Wrong - button may not be visible or clickable -->
<draw:SkiaButton Text="Button" />
```

### ✅ Always specify button size
```xml
<!-- Correct - explicit dimensions -->
<draw:SkiaButton 
    Text="Button" 
    WidthRequest="120" 
    HeightRequest="40" />
```

### ❌ Mixing Click events and Commands
```xml
<!-- Wrong - using both Click and Command -->
<draw:SkiaButton 
    Text="Button"
    Clicked="OnClicked"
    CommandTapped="{Binding Command}" />
```

### ✅ Use either Click events OR Commands
```xml
<!-- Correct - using Command for MVVM -->
<draw:SkiaButton 
    Text="Button"
    CommandTapped="{Binding Command}" />

<!-- Or using Click event -->
<draw:SkiaButton 
    Text="Button"
    Clicked="OnClicked" />
```

### ❌ Forgetting to handle button states
```xml
<!-- Wrong - no visual feedback -->
<draw:SkiaButton 
    Text="Button"
    BackgroundColor="Blue" />
```

### ✅ Provide visual state feedback
```xml
<!-- Correct - visual states defined -->
<draw:SkiaButton 
    Text="Button"
    BackgroundColor="Blue"
    PressedBackgroundColor="DarkBlue"
    DisabledBackgroundColor="Gray" />
```

## Tags
#skiabutton #button #interactive #platform-styling #gestures #commands #mvvm #click-events #custom-content #material #ios #android #basic
