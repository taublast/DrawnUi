# Carousel Controls

DrawnUi.Maui provides powerful carousel controls for creating interactive, swipeable displays of content. This article covers the carousel components available in the framework.

## SkiaCarousel

SkiaCarousel is a specialized scroll control designed specifically for creating swipeable carousels with automatic snapping to items.

### Basic Usage

```xml
<DrawUi:SkiaCarousel
    WidthRequest="400"
    HeightRequest="200"
    SelectedIndex="0">
    
    <!-- Item 1 -->
    <DrawUi:SkiaLayout BackgroundColor="Red">
        <DrawUi:SkiaLabel 
            Text="Slide 1" 
            FontSize="24" 
            HorizontalOptions="Center" 
            VerticalOptions="Center" />
    </DrawUi:SkiaLayout>
    
    <!-- Item 2 -->
    <DrawUi:SkiaLayout BackgroundColor="Green">
        <DrawUi:SkiaLabel 
            Text="Slide 2" 
            FontSize="24" 
            HorizontalOptions="Center" 
            VerticalOptions="Center" />
    </DrawUi:SkiaLayout>
    
    <!-- Item 3 -->
    <DrawUi:SkiaLayout BackgroundColor="Blue">
        <DrawUi:SkiaLabel 
            Text="Slide 3" 
            FontSize="24" 
            HorizontalOptions="Center" 
            VerticalOptions="Center" />
    </DrawUi:SkiaLayout>
    
</DrawUi:SkiaCarousel>
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `SelectedIndex` | int | Current selected item index |
| `InTransition` | bool | Indicates if carousel is currently transitioning |
| `Spacing` | float | Space between carousel items |
| `SidesOffset` | float | Side padding to create a peek effect |
| `Bounces` | bool | Enables bouncing effect at edges |
| `ItemsSource` | IEnumerable | Data source for dynamically generating items |
| `ItemTemplate` | DataTemplate | Template for items when using ItemsSource |

### Peek Next/Previous Items

You can create a peek effect to show portions of adjacent slides:

```xml
<DrawUi:SkiaCarousel
    WidthRequest="400"
    HeightRequest="200"
    SidesOffset="40"
    SelectedIndex="0">
    
    <!-- Items here -->
    
</DrawUi:SkiaCarousel>
```

With `SidesOffset="40"`, 40 pixels on each side will be reserved to show portions of the previous and next items.

### Data Binding

SkiaCarousel supports data binding through `ItemsSource` and `ItemTemplate`:

```xml
<DrawUi:SkiaCarousel
    WidthRequest="400"
    HeightRequest="200"
    ItemsSource="{Binding CarouselItems}">
    
    <DrawUi:SkiaCarousel.ItemTemplate>
        <DataTemplate>
            <DrawUi:SkiaLayout BackgroundColor="{Binding Color}">
                <DrawUi:SkiaLabel 
                    Text="{Binding Title}" 
                    FontSize="24" 
                    HorizontalOptions="Center" 
                    VerticalOptions="Center" />
            </DrawUi:SkiaLayout>
        </DataTemplate>
    </DrawUi:SkiaCarousel.ItemTemplate>
    
</DrawUi:SkiaCarousel>
```

### Tracking Current Item

You can bind to the current item or monitor transitions:

```xml
<DrawUi:SkiaCarousel
    x:Name="MyCarousel"
    SelectedIndex="{Binding CurrentIndex, Mode=TwoWay}"
    WidthRequest="400"
    HeightRequest="200">
    
    <!-- Items here -->
    
</DrawUi:SkiaCarousel>

<!-- Display current state -->
<DrawUi:SkiaLabel 
    Text="{Binding Source={x:Reference MyCarousel}, Path=SelectedIndex, StringFormat='Current: {0}'}" 
    TextColor="Black" />

<DrawUi:SkiaLabel 
    Text="{Binding Source={x:Reference MyCarousel}, Path=InTransition, StringFormat='In Transition: {0}'}" 
    TextColor="Black" />
```

The `InTransition` property is particularly useful for disabling user interactions during transitions.

### Programmatic Control

You can control the carousel programmatically:

```csharp
// Jump to a specific index
myCarousel.SelectedIndex = 2;

// Animate to a specific index
myCarousel.ScrollTo(2, true);

// Track selection changes
myCarousel.PropertyChanged += (sender, e) => {
    if (e.PropertyName == nameof(SkiaCarousel.SelectedIndex))
    {
        // Handle selection change
        var index = myCarousel.SelectedIndex;
    }
};
```

## Real-World Examples

### Gallery Popup with Zoom

A powerful pattern is using SkiaCarousel inside a popup for image galleries with zoom capabilities:

```xml
<draw:SkiaCarousel
    x:Name="MainCarousel"
    Bounces="True"
    HorizontalOptions="Fill"
    ItemsSource="{Binding GalleryItems}"
    SelectedIndex="{Binding SelectedGalleryIndex}"
    SidesOffset="0"
    Spacing="16"
    VerticalOptions="Fill">

    <draw:SkiaLayout.ItemTemplate>
        <DataTemplate>

            <draw:ZoomContent
                PanningMode="OneFinger"
                UseCache="GPU"
                ZoomMax="2"
                ZoomMin="1">

                <draw:SkiaLayout
                    x:DataType="x:String"
                    BackgroundColor="Transparent"
                    HorizontalOptions="Fill"
                    UseCache="Operations"
                    VerticalOptions="Fill">

                    <!-- Loading indicator -->
                    <draw:SkiaSvg
                        Source="loading.svg"
                        TintColor="White"
                        HorizontalOptions="Center"
                        VerticalOptions="Center" />

                    <!-- Main image -->
                    <draw:SkiaImage
                        Aspect="AspectFit"
                        EraseChangedContent="True"
                        HorizontalOptions="Fill"
                        LoadSourceOnFirstDraw="False"
                        Source="{Binding .}"
                        VerticalOptions="Fill" />

                </draw:SkiaLayout>

            </draw:ZoomContent>

        </DataTemplate>

    </draw:SkiaLayout.ItemTemplate>

</draw:SkiaCarousel>
```

**Key Features:**
- **ZoomContent**: Enables pinch-to-zoom on each image
- **Data Binding**: Uses ItemsSource for dynamic image collection
- **Performance**: GPU caching and optimized image loading
- **UX**: Loading indicators and smooth transitions

### Dedicated Carousel Demo Page

A comprehensive carousel demonstration with spacing and offset effects:

```xml
<draw:SkiaCarousel
    x:Name="MainCarousel"
    BackgroundColor="Transparent"
    Bounces="True"
    HeightRequest="200"
    HorizontalOptions="Fill"
    InTransition="{Binding SomeBoolean}"
    SelectedIndex="{Binding SelectedIndex}"
    SidesOffset="40"
    Spacing="20"
    VerticalOptions="Center">

    <draw:SkiaLayout
        BackgroundColor="Red"
        UseCache="None">

        <draw:SkiaLabel
            Margin="24"
            FontSize="16"
            HorizontalOptions="Center"
            HorizontalTextAlignment="Center"
            Text="Here we have Spacing 20 and SidesOffset 40."
            VerticalOptions="Center" />

    </draw:SkiaLayout>

    <draw:SkiaLayout BackgroundColor="Green">
        <draw:SkiaLabel
            FontSize="40"
            HorizontalOptions="Center"
            Text="2"
            VerticalOptions="Center" />
    </draw:SkiaLayout>

    <draw:SkiaLayout BackgroundColor="Blue">
        <draw:SkiaLabel
            FontSize="40"
            HorizontalOptions="Center"
            Text="3"
            VerticalOptions="Center" />
    </draw:SkiaLayout>

    <draw:SkiaLayout BackgroundColor="Fuchsia">
        <draw:SkiaLabel
            FontSize="40"
            HorizontalOptions="Center"
            Text="4"
            VerticalOptions="Center" />
    </draw:SkiaLayout>

</draw:SkiaCarousel>
```

**Configuration Highlights:**
- `SidesOffset="40"` - Shows 40px of adjacent slides
- `Spacing="20"` - 20px gap between slides
- `Bounces="True"` - Elastic bounce at edges
- `InTransition` binding - Track transition state

### Advanced Tab Carousel with Custom Header

A sophisticated pattern combining SkiaCarousel with a custom tab header that tracks scroll progress:

```xml
<!-- Custom tab header that syncs with carousel -->
<draw:SkiaShape
    CornerRadius="24"
    HorizontalOptions="Fill"
    UseCache="GPU">

    <controls:DrawnTabsHeader
        HeightRequest="46"
        ScrollAmount="{Binding Source={x:Reference Carousel}, Path=ScrollProgress}"
        SelectedIndex="{Binding Source={x:Reference Carousel}, Path=SelectedIndex, Mode=TwoWay}"
        TabsCount="{Binding Source={x:Reference Carousel}, Path=ChildrenCount}"
        TouchEffectColor="White">

        <!-- Tab content definitions -->
        <draw:SkiaLayout BackgroundColor="LightBlue">
            <draw:SkiaLabel Text="Tab 1" HorizontalOptions="Center" VerticalOptions="Center" />
        </draw:SkiaLayout>

        <draw:SkiaLayout BackgroundColor="LightGreen">
            <draw:SkiaLabel Text="Tab 2" HorizontalOptions="Center" VerticalOptions="Center" />
        </draw:SkiaLayout>

        <draw:SkiaLayout BackgroundColor="LightCoral">
            <draw:SkiaLabel Text="Tab 3" HorizontalOptions="Center" VerticalOptions="Center" />
        </draw:SkiaLayout>

    </controls:DrawnTabsHeader>

</draw:SkiaShape>

<!-- Main carousel content -->
<draw:SkiaCarousel
    x:Name="Carousel"
    AutoVelocityMultiplyPts="10"
    HorizontalOptions="Fill"
    VerticalOptions="Fill">

    <!-- Tab content pages -->
    <draw:SkiaScroll IgnoreWrongDirection="True">
        <!-- Tab 1 content with vertical scroll -->
        <draw:SkiaLayout Type="Column" Spacing="16">
            <!-- Content items -->
        </draw:SkiaLayout>
    </draw:SkiaScroll>

    <draw:SkiaLayout>
        <!-- Tab 2 content -->
    </draw:SkiaLayout>

    <draw:SkiaLayout>
        <!-- Tab 3 content -->
    </draw:SkiaLayout>

</draw:SkiaCarousel>
```

**Advanced Features:**
- **ScrollProgress Binding**: Tab header tracks carousel scroll position
- **Two-Way Selection**: Tapping tabs changes carousel, swiping carousel updates tabs
- **Nested Scrolling**: `IgnoreWrongDirection="True"` allows vertical scroll within horizontal carousel
- **Auto Velocity**: `AutoVelocityMultiplyPts` controls swipe sensitivity

### Code-Behind Gallery Implementation

For programmatic control, you can create carousels entirely in code:

```csharp
var galleryCarousel = new SkiaCarousel()
{
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    ItemsSource = Model.GalleryItems,
    Spacing = 16,
    ItemTemplate = new DataTemplate(() =>
    {
        var cell = new SkiaLayer()
        {
            UseCache = SkiaCacheType.Operations,
            Children = new List<SkiaControl>()
            {
                // Image with loading placeholder
                new SkiaImage()
                {
                    Aspect = ImageAspect.AspectFit,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                }.Bind(SkiaImage.SourceProperty, ".")
            }
        };
        return cell;
    })
};
```

## Performance Considerations

- For optimal performance, use `Cache="Operations"` or `Cache="Image"` on complex carousel items
- Avoid placing too many items directly in the carousel; use virtualization through `ItemsSource` for large collections
- Consider using lightweight content for peek items if they'll be partially visible most of the time
- Monitor the performance using `SkiaLabelFps` during development to ensure smooth scrolling