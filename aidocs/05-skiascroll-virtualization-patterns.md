<!-- Component: SkiaScroll, Category: Layout/Performance, Complexity: Advanced -->
# SkiaScroll - High-Performance Scrolling with Virtualization

## Scenario
SkiaScroll provides smooth, hardware-accelerated scrolling with advanced features like virtualization, infinite scrolling, zoom, and pull-to-refresh. It's optimized for large datasets and complex content. Use SkiaScroll when you need to display scrollable content, especially with many items or when performance is critical.

## Complete Working Example

### Basic Vertical Scroll
```xml
<draw:Canvas>
    <draw:SkiaScroll 
        Orientation="Vertical"
        HorizontalOptions="Fill"
        VerticalOptions="Fill"
        Bounces="True"
        FrictionScrolled="0.1"
        BackgroundColor="White">
        
        <draw:SkiaLayout 
            Type="Column" 
            Spacing="16" 
            Padding="20">
            
            <draw:SkiaLabel Text="Scroll Content Header" FontSize="24" />
            
            <!-- Repeat content to demonstrate scrolling -->
            <draw:SkiaLabel Text="Item 1" FontSize="16" />
            <draw:SkiaLabel Text="Item 2" FontSize="16" />
            <draw:SkiaLabel Text="Item 3" FontSize="16" />
            <!-- ... more items ... -->
            
            <draw:SkiaButton 
                Text="Bottom Button" 
                WidthRequest="200" 
                HeightRequest="44"
                HorizontalOptions="Center" />
        </draw:SkiaLayout>
    </draw:SkiaScroll>
</draw:Canvas>
```

### Virtualized List with Large Dataset
```xml
<draw:SkiaScroll 
    Orientation="Vertical"
    Virtualisation="Enabled"
    HorizontalOptions="Fill"
    VerticalOptions="Fill"
    BackgroundColor="LightGray">
    
    <draw:SkiaLayout 
        Type="Column"
        Spacing="1"
        ItemsSource="{Binding LargeItemCollection}"
        RecyclingTemplate="Enabled"
        MeasureItemsStrategy="MeasureAll"
        Virtualisation="Enabled"
        VirtualisationInflated="2">
        
        <draw:SkiaLayout.ItemTemplate>
            <DataTemplate x:DataType="local:ListItem">
                <draw:SkiaLayout 
                    Type="Row" 
                    Spacing="12" 
                    Padding="16"
                    BackgroundColor="White"
                    HeightRequest="80">
                    
                    <draw:SkiaShape 
                        Type="Circle"
                        WidthRequest="48"
                        HeightRequest="48"
                        BackgroundColor="{Binding Color}"
                        VerticalOptions="Center" />
                        
                    <draw:SkiaLayout 
                        Type="Column" 
                        Spacing="4"
                        VerticalOptions="Center"
                        HorizontalOptions="Fill">
                        
                        <draw:SkiaLabel 
                            Text="{Binding Title}"
                            FontSize="16"
                            FontWeight="Bold"
                            TextColor="Black" />
                        <draw:SkiaLabel 
                            Text="{Binding Description}"
                            FontSize="14"
                            TextColor="Gray" />
                    </draw:SkiaLayout>
                </draw:SkiaLayout>
            </DataTemplate>
        </draw:SkiaLayout.ItemTemplate>
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

### Code-Behind with Scroll Events
```csharp
using DrawnUi.Draw;
using System.Collections.ObjectModel;

public partial class MainPage : ContentPage
{
    public ObservableCollection<ListItem> LargeItemCollection { get; set; }
    
    public MainPage()
    {
        InitializeComponent();
        
        // Generate large dataset
        LargeItemCollection = new ObservableCollection<ListItem>();
        for (int i = 0; i < 10000; i++)
        {
            LargeItemCollection.Add(new ListItem
            {
                Title = $"Item {i + 1}",
                Description = $"Description for item {i + 1}",
                Color = GetRandomColor()
            });
        }
        
        BindingContext = this;
    }
    
    private void OnScrolled(object sender, ScrolledEventArgs e)
    {
        var scroll = sender as SkiaScroll;
        
        // Handle scroll position changes
        if (e.ScrollY > 1000) // Show back-to-top button
        {
            ShowBackToTopButton();
        }
        
        // Infinite scroll implementation
        if (e.ScrollY >= scroll.ContentSize.Height - scroll.Viewport.Height - 100)
        {
            LoadMoreItems();
        }
    }
    
    private Color GetRandomColor()
    {
        var colors = new[] { Colors.Red, Colors.Blue, Colors.Green, Colors.Orange, Colors.Purple };
        return colors[Random.Shared.Next(colors.Length)];
    }
    
    private void LoadMoreItems()
    {
        // Add more items for infinite scroll
        for (int i = 0; i < 50; i++)
        {
            LargeItemCollection.Add(new ListItem
            {
                Title = $"New Item {LargeItemCollection.Count + 1}",
                Description = "Dynamically loaded item",
                Color = GetRandomColor()
            });
        }
    }
}

public class ListItem
{
    public string Title { get; set; }
    public string Description { get; set; }
    public Color Color { get; set; }
}
```

## Result
- Smooth 60fps scrolling with large datasets
- Memory-efficient virtualization
- Hardware-accelerated rendering
- Responsive touch interactions with physics

## Variations

### 1. Horizontal Scroll with Zoom
```xml
<draw:SkiaScroll 
    Orientation="Horizontal"
    ZoomLocked="False"
    ZoomMin="0.5"
    ZoomMax="3.0"
    HorizontalOptions="Fill"
    VerticalOptions="Fill">
    
    <draw:SkiaImage 
        Source="large_map.jpg"
        WidthRequest="2000"
        HeightRequest="1500"
        Aspect="AspectFit" />
</draw:SkiaScroll>
```

### 2. Both-Direction Scroll
```xml
<draw:SkiaScroll 
    Orientation="Both"
    HorizontalOptions="Fill"
    VerticalOptions="Fill">
    
    <draw:SkiaLayout 
        WidthRequest="1500"
        HeightRequest="1200"
        BackgroundColor="LightBlue">
        
        <!-- Large content that scrolls both ways -->
        <draw:SkiaImage 
            Source="large_diagram.png"
            HorizontalOptions="Fill"
            VerticalOptions="Fill" />
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

### 3. Pull-to-Refresh Implementation
```xml
<draw:SkiaScroll 
    x:Name="RefreshScroll"
    Orientation="Vertical"
    RefreshView="True"
    RefreshCommand="{Binding RefreshCommand}"
    IsRefreshing="{Binding IsRefreshing}">
    
    <!-- Refresh indicator -->
    <draw:SkiaScroll.RefreshIndicator>
        <draw:SkiaLottie 
            Source="refresh_animation.json"
            WidthRequest="40"
            HeightRequest="40"
            AutoPlay="True" />
    </draw:SkiaScroll.RefreshIndicator>
    
    <!-- Scrollable content -->
    <draw:SkiaLayout Type="Column" ItemsSource="{Binding Items}">
        <!-- Item template -->
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

### 4. Chat-Style Scroll with Auto-Scroll
```csharp
var chatScroll = new SkiaScroll()
{
    Orientation = ScrollOrientation.Vertical,
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    Content = new SkiaLayout()
    {
        Type = LayoutType.Column,
        ItemsSource = Messages,
        RecyclingTemplate = RecyclingTemplate.Enabled
    }
};

// Auto-scroll to bottom when new message arrives
Messages.CollectionChanged += (s, e) =>
{
    if (e.Action == NotifyCollectionChangedAction.Add)
    {
        Device.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100); // Wait for layout
            chatScroll.ScrollToEnd(true);
        });
    }
};
```

## Related Components
- **Also see**: SkiaLayout, VirtualScroll, SkiaCarousel
- **Requires**: Canvas container
- **Child content**: Any SkiaControl, typically SkiaLayout

## Common Mistakes

### ❌ Not enabling virtualization for large lists
```xml
<!-- Wrong - no virtualization for 1000+ items -->
<draw:SkiaScroll>
    <draw:SkiaLayout ItemsSource="{Binding LargeList}">
        <!-- All items rendered at once -->
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

### ✅ Enable virtualization for performance
```xml
<!-- Correct - virtualization enabled -->
<draw:SkiaScroll Virtualisation="Enabled">
    <draw:SkiaLayout 
        ItemsSource="{Binding LargeList}"
        Virtualisation="Enabled"
        RecyclingTemplate="Enabled">
        <!-- Only visible items rendered -->
    </draw:SkiaLayout>
</draw:SkiaScroll>
```

### ❌ Complex layouts without caching
```xml
<!-- Wrong - no caching for complex items -->
<draw:SkiaLayout.ItemTemplate>
    <DataTemplate>
        <draw:SkiaLayout Type="Column">
            <!-- Complex layout without UseCache -->
        </draw:SkiaLayout>
    </DataTemplate>
</draw:SkiaLayout.ItemTemplate>
```

### ✅ Use caching for complex items
```xml
<!-- Correct - caching enabled -->
<draw:SkiaLayout.ItemTemplate>
    <DataTemplate>
        <draw:SkiaLayout 
            Type="Column"
            UseCache="ImageComposite">
            <!-- Complex layout with caching -->
        </draw:SkiaLayout>
    </DataTemplate>
</draw:SkiaLayout.ItemTemplate>
```

## Tags
#skiascroll #scroll #virtualization #performance #large-datasets #infinite-scroll #pull-to-refresh #zoom #physics #advanced
