<!-- Component: SkiaLayout, Category: Layout, Complexity: Basic -->
# SkiaLayout - Flexible Container System

## Scenario
SkiaLayout is the primary container control for arranging child elements in DrawnUI. It supports multiple layout types (Column, Row, Grid, Wrap, Absolute) and provides advanced features like data binding, virtualization, and templating. Use SkiaLayout when you need to organize multiple controls with specific spacing, alignment, and layout behavior.

## Complete Working Example

### Basic Column Layout
```xml
<draw:Canvas>
    <draw:SkiaLayout 
        Type="Column" 
        Spacing="16" 
        Padding="20"
        HorizontalOptions="Fill"
        VerticalOptions="Fill">
        
        <draw:SkiaLabel Text="Header" FontSize="24" TextColor="Black" />
        <draw:SkiaLabel Text="Content line 1" FontSize="16" />
        <draw:SkiaLabel Text="Content line 2" FontSize="16" />
        
        <draw:SkiaButton 
            Text="Action Button" 
            HorizontalOptions="Center"
            WidthRequest="150" 
            HeightRequest="40" />
    </draw:SkiaLayout>
</draw:Canvas>
```

### Data-Bound Layout with ItemTemplate
```xml
<draw:SkiaLayout 
    Type="Column"
    Spacing="8"
    ItemsSource="{Binding Messages}"
    RecyclingTemplate="Enabled"
    MeasureItemsStrategy="MeasureAll">
    
    <draw:SkiaLayout.ItemTemplate>
        <DataTemplate x:DataType="local:ChatMessage">
            <draw:SkiaLayout Type="Row" Spacing="12" Padding="16">
                <draw:SkiaShape 
                    Type="Circle" 
                    WidthRequest="40" 
                    HeightRequest="40"
                    BackgroundColor="Blue" />
                    
                <draw:SkiaLayout Type="Column" Spacing="4" HorizontalOptions="Fill">
                    <draw:SkiaLabel 
                        Text="{Binding Author}" 
                        FontSize="14" 
                        FontWeight="Bold" />
                    <draw:SkiaLabel 
                        Text="{Binding Text}" 
                        FontSize="12" 
                        TextColor="Gray" />
                </draw:SkiaLayout>
            </draw:SkiaLayout>
        </DataTemplate>
    </draw:SkiaLayout.ItemTemplate>
</draw:SkiaLayout>
```

### Code-Behind Setup
```csharp
using DrawnUi.Draw;
using System.Collections.ObjectModel;

public partial class MainPage : ContentPage
{
    public ObservableCollection<ChatMessage> Messages { get; set; }
    
    public MainPage()
    {
        InitializeComponent();
        
        Messages = new ObservableCollection<ChatMessage>
        {
            new ChatMessage { Author = "John", Text = "Hello everyone!" },
            new ChatMessage { Author = "Jane", Text = "How are you doing?" },
            new ChatMessage { Author = "Bob", Text = "Great weather today!" }
        };
        
        BindingContext = this;
    }
}

public class ChatMessage
{
    public string Author { get; set; }
    public string Text { get; set; }
}
```

## Result
- Flexible layout arrangement with proper spacing
- Data-bound dynamic content with templates
- Efficient rendering with recycling and virtualization
- Responsive design that adapts to content

## Variations

### 1. Grid Layout
```xml
<draw:SkiaLayout Type="Grid" ColumnDefinitions="*,*,*" RowDefinitions="Auto,*">
    <draw:SkiaLabel Text="Header" Grid.ColumnSpan="3" Grid.Row="0" />
    <draw:SkiaButton Text="Button 1" Grid.Column="0" Grid.Row="1" />
    <draw:SkiaButton Text="Button 2" Grid.Column="1" Grid.Row="1" />
    <draw:SkiaButton Text="Button 3" Grid.Column="2" Grid.Row="1" />
</draw:SkiaLayout>
```

### 2. Wrap Layout
```xml
<draw:SkiaLayout 
    Type="Wrap" 
    Split="3" 
    Spacing="8" 
    SplitAlign="True">
    <draw:SkiaButton Text="Item 1" WidthRequest="100" />
    <draw:SkiaButton Text="Item 2" WidthRequest="100" />
    <draw:SkiaButton Text="Item 3" WidthRequest="100" />
    <draw:SkiaButton Text="Item 4" WidthRequest="100" />
</draw:SkiaLayout>
```

### 3. Absolute Positioning
```xml
<draw:SkiaLayout Type="Absolute">
    <draw:SkiaLabel 
        Text="Top Left" 
        AbsoluteLayout.LayoutBounds="0,0,100,30" />
    <draw:SkiaLabel 
        Text="Center" 
        AbsoluteLayout.LayoutBounds="0.5,0.5,100,30" 
        AbsoluteLayout.LayoutFlags="PositionProportional" />
    <draw:SkiaLabel 
        Text="Bottom Right" 
        AbsoluteLayout.LayoutBounds="1,1,100,30" 
        AbsoluteLayout.LayoutFlags="PositionProportional" />
</draw:SkiaLayout>
```

### 4. Programmatic Layout Creation
```csharp
var layout = new SkiaLayout()
{
    Type = LayoutType.Column,
    Spacing = 16,
    Padding = 20,
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    Children = new List<SkiaControl>()
    {
        new SkiaLabel() { Text = "Dynamic Header", FontSize = 24 },
        new SkiaButton() { Text = "Dynamic Button" }
    }
};
```

## Related Components
- **Also see**: SkiaScroll, SkiaGrid, Canvas, SkiaStack
- **Requires**: Canvas as parent container
- **Child controls**: Any SkiaControl-derived components

## Common Mistakes

### ❌ Forgetting to set layout Type
```xml
<!-- Wrong - no layout type specified -->
<draw:SkiaLayout Spacing="16">
    <draw:SkiaLabel Text="Item 1" />
    <draw:SkiaLabel Text="Item 2" />
</draw:SkiaLayout>
```

### ✅ Always specify layout Type
```xml
<!-- Correct - layout type specified -->
<draw:SkiaLayout Type="Column" Spacing="16">
    <draw:SkiaLabel Text="Item 1" />
    <draw:SkiaLabel Text="Item 2" />
</draw:SkiaLayout>
```

### ❌ Inefficient ItemTemplate without recycling
```xml
<!-- Wrong - no recycling for large lists -->
<draw:SkiaLayout ItemsSource="{Binding LargeList}">
    <draw:SkiaLayout.ItemTemplate>
        <!-- Complex template -->
    </draw:SkiaLayout.ItemTemplate>
</draw:SkiaLayout>
```

### ✅ Enable recycling for performance
```xml
<!-- Correct - recycling enabled -->
<draw:SkiaLayout 
    ItemsSource="{Binding LargeList}"
    RecyclingTemplate="Enabled"
    MeasureItemsStrategy="MeasureAll">
    <draw:SkiaLayout.ItemTemplate>
        <!-- Complex template -->
    </draw:SkiaLayout.ItemTemplate>
</draw:SkiaLayout>
```

## Tags
#skialayout #layout #container #column #row #grid #wrap #absolute #databinding #itemtemplate #recycling #virtualization #spacing #padding #intermediate
