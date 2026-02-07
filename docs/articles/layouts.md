# Layouts in DrawnUI

DrawnUI provides powerful layout controls based on the `SkiaLayout` class for organizing and positioning UI elements efficiently. The core `SkiaLayout` supports various layout types through its `Type` property, and there are specialized derived classes for convenience.

DrawnUI layouts use familiar layout properties similar to WPF and MAUI, such as `HorizontalOptions`, `VerticalOptions`, `WidthRequest`, `HeightRequest`, and `Margin` for positioning and sizing controls within layouts.

Layouts can auto-size to content or take explicit size according to properties.

## Layout Aliases

For convenience, DrawnUI provides alias classes that inherit from `SkiaLayout` with pre-configured `Type` values, contrary to base controls they usually come with `HorizonalOptions="Fill"`:

- `SkiaStack`: Alias for `SkiaLayout` with `Type="Column"` (vertical stack)
- `SkiaRow`: Alias for `SkiaLayout` with `Type="Row"` (horizontal stack)  
- `SkiaWrap`: Alias for `SkiaLayout` with `Type="Wrap"` (responsive wrapping)
- `SkiaLayer`: Alias for `SkiaLayout` with `Type="Absolute"` (absolute positioning)
- `SkiaGrid`: Alias for `SkiaLayout` with `Type="Grid"` (grid layout)

## Layout Types

### Absolute Layout
This is the default type for `SkiaLayout`. Children are positionned inside like they would inside a single column/row grid: one over anouther inside available size.   
It's sometime better for performance to use this type instead of a grid, for example if you have a 2 columns scenario "icon of known size + text" you can use a simple (absolute) layout, put an icon inside, and put a `SkiaLabel` with the left margin of the icon size+spacing, avoiding redundant calculations.

**Usage:**
```xml
<draw:SkiaLayout HorizonatalOptions="Fill" HeightRequest="50">
    <draw:SkiaLabel Text="Top Left" />
    <draw:SkiaLabel Text="Center Bottom" HorizontalOptions="Center" VerticalOptions="End"/>
</draw:SkiaLayout>
```

**Convenience Class:** `SkiaLayer` (inherits from `SkiaLayout`, described as "Absolute layout like MAUI Grid with just one column and row")

### Column Layout
Vertical stack layout that arranges children from top to bottom. When using `ItemsSource` supports `Split` property for an explicit number of columns.

**Usage:**
```xml
<draw:SkiaLayout Type="Column" Spacing="10" Padding="20">
    <draw:SkiaLabel Text="Item 1" />
    <draw:SkiaLabel Text="Item 2" />
    <draw:SkiaLabel Text="Item 3" />
</draw:SkiaLayout>
```

**Convenience Class:** `SkiaStack` (inherits from `SkiaLayout` with `Type="Column"` and horizontal fill by default)

**Additional Properties:**
- `Split`: Number of columns to split items into (for data-bound content). Items are arranged left to right, top to bottom.
- `UseDynamicColumns`: For `SkiaStack` with `ItemsSource`, allows dynamic column count to avoid empty cells. When enabled, the last row can have fewer columns that expand to fill available width.

### Row Layout
Horizontal stack layout that arranges children from left to right.

**Usage:**
```xml
<draw:SkiaLayout Type="Row" Spacing="15" Padding="10">
    <draw:SkiaButton Text="Button 1" />
    <draw:SkiaButton Text="Button 2" />
</draw:SkiaLayout>
```

**Convenience Class:** `SkiaRow` (inherits from `SkiaLayout` with `Type="Row"`)

### Wrap Layout
Responsive layout that wraps children to new lines when they exceed available width. Useful for a wrap and forget scenario to to take all available space. Supports `Split` property for an explicit number of columns.

**Usage:**
```xml
<draw:SkiaLayout Type="Wrap" Spacing="10" Padding="20">
    <!-- Children will wrap to next line when needed -->
</draw:SkiaLayout>
```

**Convenience Class:** `SkiaWrap` (inherits from `SkiaLayout` with `Type="Wrap"` and horizontal fill by default)

**Additional Properties:**
- `Split`: Number of columns to split items into (for data-bound content). Items are arranged left to right, top to bottom.

### Grid Layout
Two-dimensional grid layout with rows and columns. When using `ItemsSource` supports `Split` property for an explicit number of columns.

**Usage:**
```xml
<draw:SkiaLayout Type="Grid">
    <!-- Define grid structure -->
    <draw:SkiaLayout.ColumnDefinitions>
        <draw:ColumnDefinition Width="*" />
        <draw:ColumnDefinition Width="2*" />
    </draw:SkiaLayout.ColumnDefinitions>
    <draw:SkiaLayout.RowDefinitions>
        <draw:RowDefinition Height="Auto" />
        <draw:RowDefinition Height="*" />
    </draw:SkiaLayout.RowDefinitions>
    
    <!-- Position children in grid -->
    <draw:SkiaLabel Text="Cell 1" draw:SkiaLayout.Column="0" draw:SkiaLayout.Row="0" />
    <draw:SkiaLabel Text="Cell 2" draw:SkiaLayout.Column="1" draw:SkiaLayout.Row="0" />
</draw:SkiaLayout>
```

**Convenience Class:** `GridLayout` (helper class for `SkiaLayout` with `Type="Grid"`) or `SkiaGrid` (MAUI Grid alternative)

**Data-Bound Grid with Split:**
When using `ItemsSource`, the `Split` property creates a grid automatically:
- `Split`: Number of columns to create
- `Invert`: Controls fill direction
  - `Invert="false"` (default): Items fill left to right, top to bottom (A B, C D)
  - `Invert="true"`: Items fill top to bottom, left to right (A C, B D)

## Key Properties

All layouts inherit from `SkiaLayout` and support these common properties:

- `Spacing`: Space between child elements
- `Padding`: Internal padding around the layout
- `HorizontalOptions`/`VerticalOptions`: Layout alignment options
- `UseCache`: Performance caching strategy (`Operations`, `Image`, `ImageComposite`)
- `BackgroundColor`: Layout background
- `CornerRadius`: Rounded corners
- `Gestures`: Enable/disable touch gestures
- `Split`: Number of columns for multi-column layouts (Column, Wrap, and Grid with ItemsSource)

## Data Binding and Templating

All DrawnUI layout controls support two ways of defining content:

### Static Children
Add child controls directly in XAML or code:
```xml
<draw:SkiaStack Spacing="10">
    <draw:SkiaLabel Text="Item 1" />
    <draw:SkiaLabel Text="Item 2" />
</draw:SkiaStack>
```

### Data-Bound Content
Use `ItemsSource` with `ItemTemplate` or `ItemTemplateType` for dynamic content. The layout will automatically create subviews for each item in the data source:

```xml
<draw:SkiaStack ItemsSource="{Binding MyItems}" Spacing="10">
    <draw:SkiaStack.ItemTemplate>
        <DataTemplate>
            <draw:SkiaLabel Text="{Binding Name}" />
        </DataTemplate>
    </draw:SkiaStack.ItemTemplate>
</draw:SkiaStack>
```

**Multi-Column Layouts with Split:**
For Column, Wrap, and Grid layouts with `ItemsSource`, use the `Split` property to create multiple columns:

```xml
<draw:SkiaLayout Type="Column" ItemsSource="{Binding MyItems}" Split="2" Spacing="10">
    <draw:SkiaLayout.ItemTemplate>
        <DataTemplate>
            <draw:SkiaLabel Text="{Binding Name}" />
        </DataTemplate>
    </draw:SkiaLayout.ItemTemplate>
</draw:SkiaLayout>
```

For Grid layouts with `ItemsSource` and `Split`, use `Invert` to control fill direction:
- `Invert="false"` (default): Left to right, top to bottom
- `Invert="true"`: Top to bottom, left to right

**Key Properties:**
- `ItemsSource`: The data collection to bind to
- `ItemTemplate`: DataTemplate defining how each item should be rendered
- `ItemTemplateType`: Alternative to ItemTemplate using a type reference
- `RecycleTemplate`: Controls view recycling behavior
- `Split`: Number of columns for multi-column data-bound layouts
- `Invert`: Controls fill direction for Grid layouts with ItemsSource

**RecycleTemplate Behavior:**
- `RecycleTemplate="true"` (default): Reuses views for performance with large lists
- `RecycleTemplate="false"`: Creates a new view for each item, equivalent to .NET MAUI's `BindableLayout`

## Grid-Specific Properties

For grid layouts (`Type="Grid"`):

- `ColumnDefinitions`: Define column widths
- `RowDefinitions`: Define row heights
- `ColumnSpacing`: Space between columns
- `RowSpacing`: Space between rows
- `Split`: Number of columns for auto-generated grids (when using `ItemsSource`)
- `Invert`: Controls fill direction for auto-generated grids (`false` = left-to-right, `true` = top-to-bottom)

Child positioning in grids:
- `SkiaLayout.Column`: Column index (0-based)
- `SkiaLayout.Row`: Row index (0-based)
- `SkiaLayout.ColumnSpan`: Number of columns to span
- `SkiaLayout.RowSpan`: Number of rows to span

### Decorated Layouts

Specialized layout classes that add visual separators:

- `SkiaDecoratedGrid`: Extends `SkiaGrid` with separator lines between columns and rows

**Customization Properties:**
Decorators can be customized through gradient properties:
- `HorizontalLine`: SkiaGradient for horizontal separator lines
- `VerticalLine`: SkiaGradient for vertical separator lines

## Performance Considerations

- Use appropriate `UseCache` values based on content complexity
- If you cache the whole layout cells would be created just once, can be used for small-medium layouts inside scroll. 
- When using `ItemsSource` control whether you need to recycle cells or use RecycleTemplate="Disabled".

## Safe Insets

If you do nothing special MAUI should handle this by itsself. On iOS the root view must be a MAUI view to respect insets, for example a `Grid`, and you can inlcude your `Canvas` inside.  
You can explicitely enable/disable safe insets for a MAUI app by setting a startup option:
```csharp
// MauiProgram.cs
builder.UseDrawnUi(new() { MobileIsFullscreen = true });
```

## See Also

- [First App Tutorial](first-app.md) - Basic layout examples
- [Fluent Extensions](fluent-extensions.md) - Code-based layout creation
- [API Reference](../api/DrawnUi.Draw.SkiaLayout.html) - Complete SkiaLayout API
- [API Reference](../api/DrawnUi.Draw.LayoutType.html) - LayoutType enum values