# Scroll Controls

DrawnUi.Maui provides powerful scrolling containers that offer high-performance scrolling with advanced features like virtualization, infinite scrolling, and pull-to-refresh. This article covers the scroll controls available in the framework.

## SkiaScroll

SkiaScroll is the core scrolling container in DrawnUi.Maui, providing smooth scrolling capabilities with physics-based animations and gesture handling.

### Basic Usage

```xml
<DrawUi:SkiaScroll 
    Orientation="Vertical"
    WidthRequest="400"
    HeightRequest="600">
    
    <DrawUi:SkiaLayout LayoutType="Column" Spacing="10">
        <DrawUi:SkiaLabel Text="Item 1" />
        <DrawUi:SkiaLabel Text="Item 2" />
        <DrawUi:SkiaLabel Text="Item 3" />
        <!-- More items -->
    </DrawUi:SkiaLayout>
    
</DrawUi:SkiaScroll>
```

### Multi-Directional Scrolling

SkiaScroll supports scrolling in multiple directions:

```xml
<DrawUi:SkiaScroll
    Orientation="Both"
    HorizontalOptions="Fill"
    VerticalOptions="Fill">
    
    <DrawUi:SkiaLayout
        HeightRequest="1500"
        WidthRequest="1500">
        <!-- Content larger than viewport -->
        <DrawUi:SkiaImage Source="large_image.jpg" />
    </DrawUi:SkiaLayout>
    
</DrawUi:SkiaScroll>
```

### Zoomable Content

SkiaScroll supports pinch-to-zoom functionality:

```xml
<DrawUi:SkiaScroll
    Orientation="Both"
    ZoomLocked="False"
    ZoomMin="1"
    ZoomMax="3">
    
    <DrawUi:SkiaLayout>
        <DrawUi:SkiaImage Source="zoomable_image.jpg" />
    </DrawUi:SkiaLayout>
    
</DrawUi:SkiaScroll>
```

The zoom properties control the behavior:
- `ZoomLocked`: When true, prevents zooming
- `ZoomMin`: Minimum zoom level (1.0 = original size)
- `ZoomMax`: Maximum zoom level

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Orientation` | ScrollOrientation | Direction of scrolling (Vertical, Horizontal, Both) |
| `Content` | SkiaControl | The scrollable content |
| `Header` | SkiaControl | Optional header element |
| `Footer` | SkiaControl | Optional footer element |
| `HeaderSticky` | bool | If true, header remains fixed during scrolling |
| `HeaderBehind` | bool | If true, header appears behind scrollable content |
| `ViewportOffsetX` | float | Horizontal scroll position |
| `ViewportOffsetY` | float | Vertical scroll position |
| `UseVirtual` | bool | Enables virtualization for large content |
| `ScrollWidthRequest` | float | Width of the scrollable area |
| `ScrollHeightRequest` | float | Height of the scrollable area |
| `EnableScrolling` | bool | Enables/disables scrolling |

### Scrolling Behavior Properties

| Property | Type | Description |
|----------|------|-------------|
| `EnableMouseWheel` | bool | Controls mouse wheel scrolling |
| `ScrollVelocityThreshold` | float | Velocity threshold for scrolling |
| `ThresholdSwipeOnUp` | float | Minimum velocity for fling animation |
| `SystemAnimationTimeSecs` | float | Duration for system animations |
| `ParallaxOverscrollEnabled` | bool | Enables parallax effect when overscrolling |
| `OverscrollEnabled` | bool | Enables overscroll bouncing effect |
| `HeaderParallaxRatio` | float | Controls parallax effect for header |
| `ScrollPositionParallaxRatio` | float | Controls parallax effect for content |
| `CurrentSmoothScrollY` | float | Current smooth scroll position (vertical) |
| `CurrentSmoothScrollX` | float | Current smooth scroll position (horizontal) |

### Headers and Footers

SkiaScroll supports header and footer elements that can behave in special ways:

```xml
<DrawUi:SkiaScroll HeaderSticky="True" HeaderBehind="False">
    
    <DrawUi:SkiaScroll.Header>
        <DrawUi:SkiaShape Type="Rectangle" BackgroundColor="#3498DB" HeightRequest="80">
            <DrawUi:SkiaLabel 
                Text="Sticky Header" 
                TextColor="White" 
                HorizontalOptions="Center" 
                VerticalOptions="Center"
                FontSize="18" />
        </DrawUi:SkiaShape>
    </DrawUi:SkiaScroll.Header>
    
    <DrawUi:SkiaLayout LayoutType="Column" Spacing="10">
        <!-- Content items -->
    </DrawUi:SkiaLayout>
    
    <DrawUi:SkiaScroll.Footer>
        <DrawUi:SkiaShape Type="Rectangle" BackgroundColor="#2C3E50" HeightRequest="60">
            <DrawUi:SkiaLabel 
                Text="Footer" 
                TextColor="White" 
                HorizontalOptions="Center" 
                VerticalOptions="Center" />
        </DrawUi:SkiaShape>
    </DrawUi:SkiaScroll.Footer>
    
</DrawUi:SkiaScroll>
```

### Scrolling to Position

```csharp
// Scroll to a specific position
myScroll.ScrollToPosition(0, 500); // Scroll to Y = 500

// Scroll with animation
myScroll.ScrollToPosition(0, 500, true); // Animated scroll

// Scroll to an element
myScroll.ScrollToView(targetElement, true); // Animated scroll to element
```

### Virtualization

For large content sets, you can enable virtualization to improve performance:

```xml
<DrawUi:SkiaScroll UseVirtual="True" Orientation="Vertical">
    <DrawUi:SkiaLayout 
        LayoutType="Column" 
        ItemsSource="{Binding LargeItemCollection}"
        VirtualizationMode="Enabled">
        <DrawUi:SkiaLayout.ItemTemplate>
            <DataTemplate>
                <DrawUi:SkiaLabel Text="{Binding Title}" />
            </DataTemplate>
        </DrawUi:SkiaLayout.ItemTemplate>
    </DrawUi:SkiaLayout>
</DrawUi:SkiaScroll>
```

### Pull-to-Refresh

SkiaScroll supports pull-to-refresh functionality:

```xml
<DrawUi:SkiaScroll x:Name="MyScrollView" Refreshing="OnRefreshing">
    
    <DrawUi:SkiaScroll.RefreshIndicator>
        <DrawUi:RefreshIndicator />
    </DrawUi:SkiaScroll.RefreshIndicator>
    
    <DrawUi:SkiaLayout LayoutType="Column">
        <!-- Content items -->
    </DrawUi:SkiaLayout>
    
</DrawUi:SkiaScroll>
```

In code-behind:
```csharp
private async void OnRefreshing(object sender, EventArgs e)
{
    // Perform refresh operation
    await LoadDataAsync();
    
    // End refreshing state
    ((SkiaScroll)sender).EndRefresh();
}
```

## SkiaScrollLooped

SkiaScrollLooped extends SkiaScroll to provide infinite, looped scrolling capabilities. This is perfect for carousels, banners, and other UI elements that should loop continuously.

### Basic Usage

```xml
<DrawUi:SkiaScrollLooped 
    Orientation="Horizontal"
    WidthRequest="400"
    HeightRequest="200">
    
    <DrawUi:SkiaLayout LayoutType="Row" Spacing="10">
        <DrawUi:SkiaImage Source="image1.png" WidthRequest="400" HeightRequest="200" />
        <DrawUi:SkiaImage Source="image2.png" WidthRequest="400" HeightRequest="200" />
        <DrawUi:SkiaImage Source="image3.png" WidthRequest="400" HeightRequest="200" />
    </DrawUi:SkiaLayout>
    
</DrawUi:SkiaScrollLooped>
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsBanner` | bool | When true, behaves like a scrolling banner |
| `CycleSpace` | float | Space between content cycles in pixels |

### Banner Mode

In banner mode, there's space between the end of one cycle and the beginning of the next:

```xml
<DrawUi:SkiaScrollLooped 
    Orientation="Horizontal"
    IsBanner="True"
    CycleSpace="100"
    WidthRequest="400"
    HeightRequest="200">
    
    <!-- Content that will repeat infinitely -->
    <DrawUi:SkiaLabel 
        Text="Breaking News: DrawnUi.Maui Revolutionizes Cross-Platform UI Development" 
        FontSize="20"
        TextColor="Red" />
    
</DrawUi:SkiaScrollLooped>
```

### Current Index Tracking

SkiaScrollLooped can track the current visible index:

```csharp
var scrollLooped = new SkiaScrollLooped
{
    Orientation = ScrollOrientation.Horizontal,
    WidthRequest = 400,
    HeightRequest = 200
};

scrollLooped.CurrentIndexChanged += (s, index) => {
    Console.WriteLine($"Current visible index: {index}");
};
```

## Advanced Usage

### Creating a Card Carousel

```xml
<DrawUi:SkiaScrollLooped 
    x:Name="Carousel"
    Orientation="Horizontal"
    WidthRequest="400"
    HeightRequest="300"
    SnapToChildren="Center">
    
    <DrawUi:SkiaLayout LayoutType="Row" Spacing="20" Padding="20,0">
        <!-- Card 1 -->
        <DrawUi:SkiaShape Type="Rectangle" BackgroundColor="White" CornerRadius="16"
                   WidthRequest="300" HeightRequest="250">
            <DrawUi:SkiaShape.Shadows>
                <DrawUi:SkiaShadow Color="#40000000" BlurRadius="10" Offset="0,4" />
            </DrawUi:SkiaShape.Shadows>
            
            <DrawUi:SkiaLayout LayoutType="Column" Padding="20">
                <DrawUi:SkiaLabel Text="Card 1" FontSize="24" TextColor="#333333" />
                <DrawUi:SkiaLabel Text="Swipe to see more cards" FontSize="16" TextColor="#666666" />
            </DrawUi:SkiaLayout>
        </DrawUi:SkiaShape>
        
        <!-- Card 2 -->
        <DrawUi:SkiaShape Type="Rectangle" BackgroundColor="White" CornerRadius="16"
                   WidthRequest="300" HeightRequest="250">
            <DrawUi:SkiaShape.Shadows>
                <DrawUi:SkiaShadow Color="#40000000" BlurRadius="10" Offset="0,4" />
            </DrawUi:SkiaShape.Shadows>
            
            <DrawUi:SkiaLayout LayoutType="Column" Padding="20">
                <DrawUi:SkiaLabel Text="Card 2" FontSize="24" TextColor="#333333" />
                <DrawUi:SkiaLabel Text="Swipe to see more cards" FontSize="16" TextColor="#666666" />
            </DrawUi:SkiaLayout>
        </DrawUi:SkiaShape>
        
        <!-- Card 3 -->
        <DrawUi:SkiaShape Type="Rectangle" BackgroundColor="White" CornerRadius="16"
                   WidthRequest="300" HeightRequest="250">
            <DrawUi:SkiaShape.Shadows>
                <DrawUi:SkiaShadow Color="#40000000" BlurRadius="10" Offset="0,4" />
            </DrawUi:SkiaShape.Shadows>
            
            <DrawUi:SkiaLayout LayoutType="Column" Padding="20">
                <DrawUi:SkiaLabel Text="Card 3" FontSize="24" TextColor="#333333" />
                <DrawUi:SkiaLabel Text="Swipe to see more cards" FontSize="16" TextColor="#666666" />
            </DrawUi:SkiaLayout>
        </DrawUi:SkiaShape>
    </DrawUi:SkiaLayout>
    
</DrawUi:SkiaScrollLooped>
```

### Infinite Image Gallery

```xml
<DrawUi:SkiaScrollLooped 
    Orientation="Horizontal"
    WidthRequest="400"
    HeightRequest="400"
    SnapToChildren="Center">
    
    <DrawUi:SkiaLayout LayoutType="Row">
        <DrawUi:SkiaImage Source="image1.jpg" WidthRequest="400" HeightRequest="400" />
        <DrawUi:SkiaImage Source="image2.jpg" WidthRequest="400" HeightRequest="400" />
        <DrawUi:SkiaImage Source="image3.jpg" WidthRequest="400" HeightRequest="400" />
        <DrawUi:SkiaImage Source="image4.jpg" WidthRequest="400" HeightRequest="400" />
    </DrawUi:SkiaLayout>
    
</DrawUi:SkiaScrollLooped>
```

## RefreshIndicator

`RefreshIndicator` can use Lottie and anything as ActivityIndicator or for your scroll RefreshView. It provides customizable pull-to-refresh functionality.

### Basic Usage

```xml
<DrawUi:SkiaScroll>
    <DrawUi:SkiaScroll.RefreshIndicator>
        <DrawUi:RefreshIndicator
            IsRefreshing="{Binding IsRefreshing}"
            RefreshCommand="{Binding RefreshCommand}" />
    </DrawUi:SkiaScroll.RefreshIndicator>

    <!-- Scroll content -->
</DrawUi:SkiaScroll>
```

### Lottie Animation Indicator

```xml
<DrawUi:RefreshIndicator>
    <DrawUi:SkiaLottie
        Source="Lottie/refresh.json"
        AutoPlay="True"
        Repeat="-1"
        WidthRequest="40"
        HeightRequest="40" />
</DrawUi:RefreshIndicator>
```

### Custom Refresh Indicator

```xml
<DrawUi:RefreshIndicator>
    <DrawUi:SkiaLayout Type="Row" Spacing="10">
        <DrawUi:SkiaShape Type="Circle"
                   BackgroundColor="Blue"
                   WidthRequest="8"
                   HeightRequest="8" />
        <DrawUi:SkiaShape Type="Circle"
                   BackgroundColor="Blue"
                   WidthRequest="8"
                   HeightRequest="8" />
        <DrawUi:SkiaShape Type="Circle"
                   BackgroundColor="Blue"
                   WidthRequest="8"
                   HeightRequest="8" />
    </DrawUi:SkiaLayout>
</DrawUi:RefreshIndicator>
```

### Building a Feed with Pull-to-Refresh

```xml
<DrawUi:SkiaScroll x:Name="FeedScroll" Refreshing="OnRefreshingFeed">

    <DrawUi:SkiaScroll.RefreshIndicator>
        <DrawUi:RefreshIndicator />
    </DrawUi:SkiaScroll.RefreshIndicator>
    
    <DrawUi:SkiaLayout 
        LayoutType="Column" 
        Spacing="12" 
        Padding="16"
        ItemsSource="{Binding FeedItems}">
        <DrawUi:SkiaLayout.ItemTemplate>
            <DataTemplate>
                <DrawUi:SkiaShape Type="Rectangle" BackgroundColor="White" CornerRadius="8">
                    <DrawUi:SkiaShape.Shadows>
                        <DrawUi:SkiaShadow Color="#20000000" BlurRadius="4" Offset="0,2" />
                    </DrawUi:SkiaShape.Shadows>
                    
                    <DrawUi:SkiaLayout LayoutType="Column" Padding="16">
                        <DrawUi:SkiaLabel Text="{Binding Title}" FontSize="18" TextColor="#333333" />
                        <DrawUi:SkiaLabel Text="{Binding Description}" FontSize="14" TextColor="#666666" />
                    </DrawUi:SkiaLayout>
                </DrawUi:SkiaShape>
            </DataTemplate>
        </DrawUi:SkiaLayout.ItemTemplate>
    </DrawUi:SkiaLayout>
    
</DrawUi:SkiaScroll>
```

## Performance Considerations

### Virtualization

For optimal performance with large datasets:
- Enable `UseVirtual="True"` on SkiaScroll
- Use `VirtualizationMode="Enabled"` on inner SkiaLayout
- Consider `RecyclingTemplate="Enabled"` for template reuse

### Content Size

When working with infinite scrolling:
- Monitor memory usage, especially with large images
- Use `Cache="Operations"` for content that changes frequently
- For better performance with large collections, consider using data virtualization alongside UI virtualization

### Gestures

If scroll gesture handling conflicts with other gesture recognizers:
- Adjust `ScrollVelocityThreshold` to control sensitivity
- For nested scrolling scenarios, ensure proper gesture propagation
- Consider using `TouchScrollFriendly` on inner components that need to receive touch events