# SkiaShell

SkiaShell is a powerful navigation framework for DrawnUi applications that provides full navigation capabilities similar to MAUI's Shell, but with the performance and customization benefits of direct SkiaSharp rendering.

## Overview

SkiaShell acts as a replacement for the standard MAUI Shell, allowing for fully drawn UI with SkiaSharp while maintaining compatibility with MAUI's routing capabilities. It provides complete navigation stack management, modal presentations, popups, and toast notifications within a DrawnUi.Maui Canvas.

### Key Features

- **MAUI-compatible navigation**: Use familiar navigation patterns with `GoToAsync`
- **Navigation stack management**: Handle screen, modal, popup, and toast stacks
- **Routing with parameters**: Support for query parameters in navigation routes
- **Modal and popup systems**: Present overlays with customizable animations
- **Background freezing**: Capture and display screenshots of current views as backgrounds
- **Toast notifications**: Show temporary messages with automatic dismissal
- **Back button handling**: Handle hardware back button with customizable behavior

## Setup

### Basic Configuration

To use SkiaShell in your application, you need to:

1. Optiinal: create a page that derives from `DrawnUiBasePage`. This class provide support to track native keyboard to be able to adapt layout accordingly.
2. Add a Canvas to your page
3. Set up the required layout structure on the canvas
4. Initialize the shell to register elements present on the canvas that would serve for navigation

Here's a basic example:

```xml
<drawn:DrawnUiBasePage
    x:Class="MyApp.MainShellPage"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:drawn="clr-namespace:DrawnUi.Maui;assembly=DrawnUi.Maui">

    <drawn:Canvas
        x:Name="MainCanvas"
        HardwareAcceleration="Enabled"
        Gestures="Enabled"
        HorizontalOptions="Fill"
        VerticalOptions="Fill">
        
        <!-- Main content goes here -->
        <drawn:SkiaLayout
            Tag="ShellLayout"
            HorizontalOptions="Fill"
            VerticalOptions="Fill">
            
            <drawn:SkiaLayout
                Tag="RootLayout"
                HorizontalOptions="Fill"
                VerticalOptions="Fill">
                
                <drawn:SkiaViewSwitcher
                    Tag="NavigationLayout"
                    HorizontalOptions="Fill"
                    VerticalOptions="Fill" />
                    
            </drawn:SkiaLayout>
            
        </drawn:SkiaLayout>
    </drawn:Canvas>
    
</drawn:DrawnUiBasePage>
```

In your code-behind:

```csharp
public partial class MainShellPage : DrawnUiBasePage
{
    public MainShellPage()
    {
        InitializeComponent();
        
        // Initialize and register the shell
        Shell = new SkiaShell();
        Shell.Initialize(MainCanvas);
    }
    
    public SkiaShell Shell { get; private set; }
    
    // Register routes in OnAppearing or constructor
    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Register navigation routes
        Shell.RegisterRoute("home", typeof(HomePage));
        Shell.RegisterRoute("details", typeof(DetailsPage));
        
        // Navigate to the initial route
        Shell.GoToAsync("home");
    }
}
```

### Required Layout Tags

SkiaShell relies on specific tags to identify key components in your layout:

- `ShellLayout`: The outer container for all navigation elements (typically directly inside the Canvas)
- `RootLayout`: The main layout container (inside ShellLayout)
- `NavigationLayout`: A `SkiaViewSwitcher` that handles page transitions (inside RootLayout)

## Navigation

### Basic Navigation

```csharp
// Navigate to a registered route
await Shell.GoToAsync("details");

// Navigate with parameters
await Shell.GoToAsync("details?id=123&name=Product");

// Navigate back
bool handled = Shell.GoBack(true); // true to animate

// Check if can go back
bool canGoBack = Shell.CanGoBack();
```

### Push and Pop Pages

```csharp
// Push a page instance
var detailsPage = new DetailsPage();
await Shell.PushAsync(detailsPage, animated: true);

// Pop the current page
var poppedPage = await Shell.PopAsync(animated: true);

// Pop to the root page
await Shell.PopToRootAsync(animated: true);
```

### Route Registration

Routes need to be registered before navigation:

```csharp
// Register a route with a page type
Shell.RegisterRoute("details", typeof(DetailsPage));

// Register a route with a factory function
Shell.RegisterRoute("profile", () => new ProfilePage());
```

### Route Parameters

Extract parameters in the destination page:

```csharp
public class DetailsPage : SkiaControl
{
    protected override void OnParentChanged()
    {
        base.OnParentChanged();
        
        // Get query parameters from shell route
        var shell = AppShell; // Helper property to get the shell
        
        if (shell?.RouteParameters != null)
        {
            string id = shell.RouteParameters.GetValueOrDefault("id");
            string name = shell.RouteParameters.GetValueOrDefault("name");
            
            // Use the parameters
            LoadDetails(id, name);
        }
    }
}
```

## Modals and Popups

### Modal Presentation

```csharp
// Show a modal from a registered route
await Shell.PushModalAsync("details", useGestures: true, animated: true);

// Show a modal from a page instance
await Shell.PushModalAsync(new DetailsPage(), useGestures: true, animated: true);

// Dismiss the modal
await Shell.PopModalAsync(animated: true);
```

### Popup Presentation

```csharp
// Create a popup content
var popupContent = new SkiaLayout
{
    WidthRequest = 300,
    HeightRequest = 200,
    BackgroundColor = Colors.White,
    CornerRadius = 10
};

// Add content to the popup
popupContent.Add(new SkiaLabel 
{ 
    Text = "This is a popup",
    HorizontalOptions = LayoutOptions.Center,
    VerticalOptions = LayoutOptions.Center
});

// Show popup
await Shell.OpenPopupAsync(
    content: popupContent,
    animated: true,
    closeWhenBackgroundTapped: true,
    freezeBackground: true
);

// Close popup
await Shell.ClosePopupAsync(animated: true);
```

### Toast Notifications

```csharp
// Show a simple text toast
Shell.ShowToast("Operation completed successfully", msShowTime: 3000);

// Show a custom toast
Shell.ShowToast(new SkiaRichLabel
{
    Text = "**Important:** Your data has been saved.",
    TextColor = Colors.White
}, msShowTime: 3000);
```

## Customization

### Visual Customization

```csharp
// Set global appearance properties
SkiaShell.PopupBackgroundColor = new SKColor(0, 0, 0, 128); // 50% transparent black
SkiaShell.PopupsBackgroundBlur = 10; // Blur amount
SkiaShell.PopupsAnimationSpeed = 350; // Animation duration in ms
SkiaShell.ToastBackgroundColor = new SKColor(50, 50, 50, 230);
SkiaShell.ToastTextColor = Colors.White;
```

### Animation Control

Control the animation duration and timing:

```csharp
// Fast navigation with minimal animation
await Shell.GoToAsync("details", new NavigationParameters
{
    AnimationDuration = 150
});

// Slow modal presentation with specific animation
await Shell.PushModalAsync("settings", new NavigationParameters
{
    AnimationDuration = 500,
    AnimationType = NavigationType.SlideFromRight
});
```

### Navigation Events

```csharp
// Subscribe to navigation events
Shell.Navigated += OnNavigated;
Shell.Navigating += OnNavigating;
Shell.RouteChanged += OnRouteChanged;

// Handle the events
private void OnNavigating(object sender, SkiaShellNavigatingArgs e)
{
    // Access navigation details
    string source = e.Source.ToString();
    string destination = e.Destination;
    
    // Optionally cancel navigation
    if (HasUnsavedChanges)
    {
        e.Cancel = true;
        ShowSavePrompt();
    }
}

private void OnNavigated(object sender, SkiaShellNavigatedArgs e)
{
    // Navigation completed
    Debug.WriteLine($"Navigated from {e.Source} to {e.Destination}");
}
```

### Custom Back Navigation

Implement the `IHandleGoBack` interface to handle back navigation in view models:

```csharp
public class EditViewModel : IHandleGoBack
{
    public bool OnShellGoBack(bool animate)
    {
        // Check for unsaved changes
        if (HasUnsavedChanges)
        {
            // Show confirmation dialog
            ShowConfirmationDialog();
            
            // Return true to indicate we're handling the back navigation
            return true;
        }
        
        // Return false to let the default back navigation occur
        return false;
    }
}
```

## Advanced Features

### Background Freezing

When showing modals or popups, SkiaShell can freeze the background content by taking a screenshot:

```csharp
// Show a modal with frozen background
await Shell.PushModalAsync("details", new NavigationParameters
{
    FreezeBackground = true,
    FreezeBlur = 5,
    FreezeTint = new SKColor(0, 0, 0, 100)
});
```

### Custom Modal Presentation

Create a custom modal presentation style:

```csharp
// Subclass SkiaShell to customize modal presentation
public class CustomShell : SkiaShell
{
    protected override SkiaDrawer CreateModalDrawer(SkiaControl content, bool useGestures)
    {
        var drawer = base.CreateModalDrawer(content, useGestures);
        
        // Customize the drawer
        drawer.Direction = DrawerDirection.FromBottom;
        drawer.HeaderSize = 40;
        
        // Add custom styling
        content.BackgroundColor = Colors.White;
        content.CornerRadius = new CornerRadius(20, 20, 0, 0);
        
        return drawer;
    }
}
```

### Handling Page Lifecycle

Implement navigation-aware controls:

```csharp
public class MyPage : SkiaLayout, INavigationAware
{
    public void OnAppearing()
    {
        // Page is becoming visible
        LoadData();
    }
    
    public void OnDisappearing()
    {
        // Page is being hidden
        SaveData();
    }
}
```

## Example: Complete Shell Application

Here's a complete example of a minimal shell-based application:

```xml
<!-- MainShell.xaml -->
<drawn:DrawnUiBasePage
    x:Class="MyApp.MainShell"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:drawn="clr-namespace:DrawnUi.Maui;assembly=DrawnUi.Maui">

    <drawn:Canvas
        x:Name="MainCanvas"
        HardwareAcceleration="Enabled"
        Gestures="Enabled">
        
        <drawn:SkiaLayout
            Tag="ShellLayout"
            BackgroundColor="#F0F0F0"
            HorizontalOptions="Fill"
            VerticalOptions="Fill">
            
            <drawn:SkiaLayout
                Tag="RootLayout"
                HorizontalOptions="Fill"
                VerticalOptions="Fill">
                
                <!-- Navigation content -->
                <drawn:SkiaViewSwitcher
                    Tag="NavigationLayout" 
                    HorizontalOptions="Fill"
                    VerticalOptions="Fill"
                    TransitionType="SlideHorizontal" />
                    
                <!-- Bottom tabs -->
                <drawn:SkiaLayout
                    LayoutType="Row"
                    HeightRequest="60"
                    BackgroundColor="White"
                    VerticalOptions="End"
                    HorizontalOptions="Fill"
                    Spacing="0">
                    
                    <drawn:SkiaHotspot 
                        HorizontalOptions="FillAndExpand"
                        Tapped="OnHomeTabTapped">
                        <drawn:SkiaLabel 
                            Text="Home" 
                            HorizontalOptions="Center"
                            VerticalOptions="Center" />
                    </drawn:SkiaHotspot>
                    
                    <drawn:SkiaHotspot 
                        HorizontalOptions="FillAndExpand"
                        Tapped="OnProfileTabTapped">
                        <drawn:SkiaLabel 
                            Text="Profile" 
                            HorizontalOptions="Center"
                            VerticalOptions="Center" />
                    </drawn:SkiaHotspot>
                    
                    <drawn:SkiaHotspot 
                        HorizontalOptions="FillAndExpand"
                        Tapped="OnSettingsTabTapped">
                        <drawn:SkiaLabel 
                            Text="Settings" 
                            HorizontalOptions="Center"
                            VerticalOptions="Center" />
                    </drawn:SkiaHotspot>
                </drawn:SkiaLayout>
                
            </drawn:SkiaLayout>
        </drawn:SkiaLayout>
    </drawn:Canvas>
</drawn:DrawnUiBasePage>
```

```csharp
// MainShell.xaml.cs
public partial class MainShell : DrawnUiBasePage
{
    public SkiaShell Shell { get; private set; }
    
    public MainShell()
    {
        InitializeComponent();
        
        // Initialize shell
        Shell = new SkiaShell();
        Shell.Initialize(MainCanvas);
        
        // Register routes
        Shell.RegisterRoute("home", typeof(HomePage));
        Shell.RegisterRoute("profile", typeof(ProfilePage));
        Shell.RegisterRoute("settings", typeof(SettingsPage));
        Shell.RegisterRoute("details", typeof(DetailsPage));
        
        // Navigate to initial route
        Shell.GoToAsync("home");
    }
    
    private void OnHomeTabTapped(object sender, EventArgs e)
    {
        Shell.GoToAsync("home");
    }
    
    private void OnProfileTabTapped(object sender, EventArgs e)
    {
        Shell.GoToAsync("profile");
    }
    
    private void OnSettingsTabTapped(object sender, EventArgs e)
    {
        Shell.GoToAsync("settings");
    }
    
    protected override bool OnBackButtonPressed()
    {
        // Let shell handle back button
        return Shell.GoBack(true);
    }
}
```

## SkiaTabsSelector

`SkiaTabsSelector` is a control for creating top and bottom tabs with customizable appearance and behavior.

### Basic Usage

```xml
<draw:SkiaTabsSelector
    x:Name="TabsSelector"
    SelectedIndex="0"
    TabHeight="50"
    TabsPosition="Bottom"
    BackgroundColor="White"
    SelectedTabColor="Blue"
    UnselectedTabColor="Gray"
    SelectionChanged="OnTabSelectionChanged">

    <draw:SkiaTabsSelector.Tabs>
        <draw:SkiaTab Text="Home" Icon="home.png" />
        <draw:SkiaTab Text="Search" Icon="search.png" />
        <draw:SkiaTab Text="Profile" Icon="profile.png" />
        <draw:SkiaTab Text="Settings" Icon="settings.png" />
    </draw:SkiaTabsSelector.Tabs>
</draw:SkiaTabsSelector>
```

### Code-Behind Example

```csharp
var tabsSelector = new SkiaTabsSelector
{
    TabHeight = 60,
    TabsPosition = TabsPosition.Top,
    BackgroundColor = Colors.White,
    SelectedTabColor = Colors.Blue,
    UnselectedTabColor = Colors.Gray
};

// Add tabs
tabsSelector.Tabs.Add(new SkiaTab { Text = "Tab 1", Icon = "icon1.png" });
tabsSelector.Tabs.Add(new SkiaTab { Text = "Tab 2", Icon = "icon2.png" });
tabsSelector.Tabs.Add(new SkiaTab { Text = "Tab 3", Icon = "icon3.png" });

// Handle selection changes
tabsSelector.SelectionChanged += (s, e) => {
    Console.WriteLine($"Selected tab: {e.SelectedIndex}");
};
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `SelectedIndex` | int | Index of the currently selected tab |
| `TabHeight` | double | Height of the tab bar |
| `TabsPosition` | TabsPosition | Position of tabs (Top, Bottom) |
| `SelectedTabColor` | Color | Color of the selected tab |
| `UnselectedTabColor` | Color | Color of unselected tabs |
| `Tabs` | ObservableCollection<SkiaTab> | Collection of tabs |

### Events

- `SelectionChanged`: Raised when the selected tab changes
  - Event signature: `EventHandler<TabSelectionChangedEventArgs>`

## SkiaViewSwitcher

`SkiaViewSwitcher` allows you to switch your views with animations like pop, push, and slide transitions.

### Basic Usage

```xml
<draw:SkiaViewSwitcher
    x:Name="ViewSwitcher"
    TransitionType="SlideHorizontal"
    TransitionDuration="300"
    HorizontalOptions="Fill"
    VerticalOptions="Fill">

    <!-- Views will be added programmatically -->
</draw:SkiaViewSwitcher>
```

### Code-Behind Example

```csharp
var viewSwitcher = new SkiaViewSwitcher
{
    TransitionType = ViewTransitionType.SlideHorizontal,
    TransitionDuration = 300
};

// Switch to a new view
var newView = new MyCustomView();
await viewSwitcher.SwitchToAsync(newView, animated: true);

// Push a view (adds to stack)
await viewSwitcher.PushAsync(newView, animated: true);

// Pop the current view
await viewSwitcher.PopAsync(animated: true);
```

## Performance Considerations

- **Layer Management**: SkiaShell maintains separate navigation stacks for better organization and performance
- **Z-Index Control**: Different types of content (modals, popups, toasts) have different Z-index ranges
- **Animation Control**: Customize animations or disable them for better performance
- **Background Freezing**: Uses screenshots to avoid continuously rendering background content
- **Locking Mechanism**: Uses semaphores to prevent multiple simultaneous navigation operations