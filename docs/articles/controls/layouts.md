# Layout Controls

DrawnUi.Maui provides a powerful and flexible layout system for arranging controls with high performance. The system is similar to MAUI's native layout system but optimized for direct rendering with SkiaSharp.

## Core Layout Types

DrawnUi.Maui offers several core layout types:

### SkiaLayout

The base layout control for measurement, arrangement, and rendering of child elements. It supports different layout strategies via the `Type` property:

- Managing child controls
- Performance optimizations (see below)

```xml
<draw:SkiaLayout
    Type="Absolute"
    WidthRequest="400"
    HeightRequest="300">
    <!-- Child controls here -->
</draw:SkiaLayout>
```

## Layout Types

The `Type` property on `SkiaLayout` supports:

- **Absolute**: Free positioning, default. Think of it like a MAUI Grid with a single column and a single row.
- **Grid**: Row and column-based layout, classic MAUI Grid.
- **Column**: Vertical stacking (like MAUI VerticalStackLayout)
- **Row**: Horizontal stacking (like MAUI HorizontalStackLayout)
- **Wrap**: Items wrap to new lines when space runs out (similar to WPF Stackpanel)

```xml
<draw:SkiaLayout Type="Wrap" Spacing="5">
    <draw:SkiaLabel Text="Item 1" />
    <draw:SkiaLabel Text="Item 2" />
    <!-- More items -->
</draw:SkiaLayout>
```

> **Note**: The `Spacing` property takes a single double value that applies to both horizontal and vertical spacing between items.

## Specialized Layout Controls

### ContentLayout

A specialized layout for hosting a single content element is `ContentLayout`, `SkiaShape` is subclassing it to be able to contain a single child inside a `Content` property, instead of using `Children`.

### SnappingLayout

Supports snap points for controlled scrolling, ideal for carousels or paginated interfaces. `SkiaDrawer`, `SkiaCarousel` are deriving from it.

### SkiaDecoratedGrid

`SkiaDecoratedGrid` is a specialized grid that can draw shapes between rows and columns, providing visual separation and decoration.

```xml
<draw:SkiaDecoratedGrid
    ColumnDefinitions="*,*,*"
    RowDefinitions="Auto,Auto,Auto"
    RowSpacing="1"
    ColumnSpacing="1"
    SeparatorColor="Gray"
    SeparatorWidth="1">

    <draw:SkiaLabel Text="Cell 1,1" Column="0" Row="0" />
    <draw:SkiaLabel Text="Cell 1,2" Column="1" Row="0" />
    <draw:SkiaLabel Text="Cell 1,3" Column="2" Row="0" />
    <draw:SkiaLabel Text="Cell 2,1" Column="0" Row="1" />
    <draw:SkiaLabel Text="Cell 2,2" Column="1" Row="1" />
    <draw:SkiaLabel Text="Cell 2,3" Column="2" Row="1" />
</draw:SkiaDecoratedGrid>
```

### SkiaHotspot

`SkiaHotspot` provides a way to handle gestures in a lazy way, creating invisible touch-sensitive areas.

```xml
<draw:SkiaLayout Type="Absolute">
    <draw:SkiaImage Source="background.png" />

    <draw:SkiaHotspot
        X="100" Y="100"
        WidthRequest="50" HeightRequest="50"
        Tapped="OnHotspotTapped" />

    <draw:SkiaHotspot
        X="200" Y="150"
        WidthRequest="80" HeightRequest="30"
        Tapped="OnAnotherHotspotTapped" />
</draw:SkiaLayout>
```

### SkiaBackdrop

`SkiaBackdrop` applies effects to the background below, like blur and other visual effects.

```xml
<draw:SkiaLayout Type="Absolute">
    <draw:SkiaImage Source="background.png" />

    <draw:SkiaBackdrop
        X="50" Y="50"
        WidthRequest="200" HeightRequest="150"
        BlurRadius="10"
        BackgroundColor="#80FFFFFF" />

    <draw:SkiaLabel
        Text="Text over blurred background"
        X="60" Y="100"
        TextColor="Black" />
</draw:SkiaLayout>
```

## Example: Creating a Grid Layout

```xml
<draw:SkiaLayout Type="Grid"
    ColumnDefinitions="Auto,*,100"
    RowDefinitions="Auto,*,50">
    <!-- Header spanning all columns -->
    <draw:SkiaLabel 
        Text="Grid Header" 
        Column="0" 
        ColumnSpan="3"
        Row="0" 
        HorizontalOptions="Center" />
    <!-- Sidebar -->
    <draw:SkiaLayout
        Type="Column"
        Column="0"
        Row="1"
        RowSpan="2"
        BackgroundColor="LightGray"
        Padding="10">
        <draw:SkiaLabel Text="Menu Item 1" />
        <draw:SkiaLabel Text="Menu Item 2" />
        <draw:SkiaLabel Text="Menu Item 3" />
    </draw:SkiaLayout>
    <!-- Main content -->
    <draw:ContentLayout 
        Column="1" 
        Row="1">
        <draw:SkiaLabel 
            Text="Main Content Area" 
            HorizontalOptions="Center" 
            VerticalOptions="Center" />
    </draw:ContentLayout>
    <!-- Right panel -->
    <draw:SkiaLayout
        Type="Column"
        Column="2"
        Row="1"
        BackgroundColor="LightBlue"
        Padding="5">
        <draw:SkiaLabel Text="Panel Info" />
    </draw:SkiaLayout>
    <!-- Footer spanning columns 1-2 -->
    <draw:SkiaLabel 
        Text="Footer" 
        Column="1" 
        ColumnSpan="2"
        Row="2" 
        HorizontalOptions="Center"
        VerticalOptions="Center" />
</draw:SkiaLayout>
```

All grid functionality is handled by SkiaLayout with `Type="Grid"`.

## Under the Hood

The layout system is built on top of the `SkiaControl` base class. Layout controls extend this for child management, measurement, and arrangement. Internally, structures like `LayoutStructure` and `GridStructure` efficiently track and manage layout information.

### Caching

Caching options help balance CPU and memory usage:

- **None**: No caching, recalculated every frame
- **Operations**: Caches drawing commands
- **Image**: Caches as bitmap (more memory, less CPU)
- **GPU**: Uses hardware acceleration where available