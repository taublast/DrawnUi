# Text Controls

DrawnUi.Maui offers text rendering capabilities through its specialized text controls. These controls provide text rendering with advanced formatting options while maintaining consistent appearance across all platforms.

## SkiaLabel

SkiaLabel is the primary text rendering control in DrawnUi.Maui, rendering text directly with SkiaSharp. Unlike traditional MAUI labels, SkiaLabel provides pixel-perfect text rendering with advanced formatting capabilities.

### Basic Usage

```xml
<draw:SkiaLabel
    Text="Hello World"
    TextColor="Black"
    FontSize="18"
    HorizontalTextAlignment="Center"
    VerticalTextAlignment="Center" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Text` | string | The text content to display |
| `TextColor` | Color | Text color |
| `FontFamily` | string | Font family name |
| `FontSize` | float | Font size in logical pixels |
| `FontWeight` | int | Font weight (100-900 scale, 400=normal, 700=bold) |
| `FontAttributes` | FontAttributes | Bold/Italic/None |
| `HorizontalTextAlignment` | DrawTextAlignment | Text horizontal alignment (Start, Center, End, Fill) |
| `VerticalTextAlignment` | DrawTextAlignment | Text vertical alignment (Start, Center, End) |
| `LineBreakMode` | LineBreakMode | How text should wrap or truncate |
| `MaxLines` | int | Maximum number of lines to display |
| `StrokeColor` | Color | Outline color |
| `StrokeWidth` | double | Outline width |
| `DropShadowColor` | Color | Shadow color |
| `DropShadowSize` | double | Shadow blur radius |
| `DropShadowOffsetX`/`DropShadowOffsetY` | double | Shadow offset |
| `AutoSize` | AutoSizeType | Auto-sizing mode |
| `AutoSizeText` | string | Text to use for auto-sizing calculations |
| `LineSpacing` | double | Line spacing multiplier |
| `ParagraphSpacing` | double | Paragraph spacing multiplier |
| `CharacterSpacing` | double | Character spacing multiplier |
| `IsMonospaced` | bool | Enables monospaced text rendering |
| `MonoForDigits` | string | Use mono width for digits (e.g. "8") |

### Rich Text Formatting (Spans)

SkiaLabel supports rich text formatting through its `Spans` collection:

```xml
<draw:SkiaLabel>
    <draw:SkiaLabel.Spans>
        <draw:TextSpan Text="Hello " TextColor="Black" FontSize="18" />
        <draw:TextSpan Text="Beautiful " TextColor="Red" FontSize="20" FontWeight="700" />
        <draw:TextSpan Text="World!" TextColor="Blue" FontSize="18" FontAttributes="Italic" />
    </draw:SkiaLabel.Spans>
</draw:SkiaLabel>
```

#### Interactive Spans

You can make any text span interactive by adding the `Tapped` event handler:

```xml
<draw:SkiaLabel FontSize="15" LineSpacing="1.5" TextColor="Black">
    <draw:TextSpan Text="Regular text " />
    <draw:TextSpan
        Text="tappable link"
        TextColor="Purple"
        Tapped="OnSpanTapped"
        Tag="link-id"
        Underline="True" />
    <draw:TextSpan Text=" more text..." />
</draw:SkiaLabel>
```

In your code-behind:

```csharp
private void OnSpanTapped(object sender, EventArgs e)
{
    var span = sender as TextSpan;
    string tag = span?.Tag?.ToString();
    // Handle the tap event based on the span or its tag
}
```

#### Styling Spans

TextSpan supports various styling options:

```xml
<draw:TextSpan Text="Bold text" FontAttributes="Bold" />
<draw:TextSpan Text="Italic text" FontAttributes="Italic" />
<draw:TextSpan Text="Underlined text" Underline="True" />
<draw:TextSpan Text="Strikethrough text" Strikeout="True" />
<draw:TextSpan Text="Highlighted text" BackgroundColor="Yellow" />
```

#### Emoji Support

For emoji rendering, use the `AutoFont` property:

```xml
<draw:TextSpan Text="Regular text " />
<draw:TextSpan AutoFont="True" Text="🌐🚒🙎🏽👻🤖" />
<draw:TextSpan Text=" more text..." />
```

This ensures proper emoji rendering by finding and using appropriate fonts.

### Text Effects

SkiaLabel supports various text effects:

#### Drop Shadow

Use the following properties for shadow effects:
- `DropShadowColor`: Shadow color
- `DropShadowSize`: Blur radius
- `DropShadowOffsetX`, `DropShadowOffsetY`: Shadow offset

```xml
<draw:SkiaLabel
    Text="Shadowed Text"
    FontSize="24"
    TextColor="White"
    DropShadowColor="#80000000"
    DropShadowSize="3"
    DropShadowOffsetX="1"
    DropShadowOffsetY="1" />
```

#### Outlined Text

```xml
<draw:SkiaLabel
    Text="Outlined Text"
    FontSize="24"
    TextColor="White"
    StrokeColor="Black"
    StrokeWidth="1" />
```

#### Gradient Text

```xml
<draw:SkiaLabel
    Text="Gradient Text"
    FontSize="24"
    FillGradient="{StaticResource MyGradient}" />
```

### Auto-sizing Text

SkiaLabel features powerful automatic font sizing capabilities that can dynamically adjust text to fit your container:

```xml
<draw:SkiaLabel
    Text="This text will resize to fit the available space"
    AutoSize="TextToView"
    FontSize="24"
    MaxLines="1" />
```

- `AutoSize`: Controls auto-sizing mode (None, TextToWidth, TextToHeight, TextToView)
- `AutoSizeText`: Text to use for sizing calculations

### Monospaced Text Rendering

SkiaLabel provides the ability to render text in a monospaced style, regardless of the font used:

```xml
<draw:SkiaLabel
    Text="This text will be monospaced"
    FontSize="18"
    MonoForDigits="8" />
```

- `MonoForDigits`: Use mono width for digits (e.g. "8")

### Performance Considerations

- For static text, set `Cache="Image"` to render once and cache as bitmap
- For frequently updated text, use `Cache="Operations"` for best performance
- Consider setting `MaxLines` when appropriate to avoid unnecessary layout calculations
- For large blocks of text, monitor performance and consider breaking into multiple labels
- Use monospaced features only when needed as it adds some calculation overhead
- For complex shadow effects, consider using `Cache="Image"` to optimize rendering

## SkiaRichLabel

SkiaRichLabel extends SkiaLabel to provide Markdown formatting capabilities. It parses Markdown syntax and renders properly formatted text.

### Basic Usage

```xml
<draw:SkiaRichLabel>
# Markdown Title

This is a paragraph with **bold** and *italic* text.

- List item 1
- List item 2

[Visit Documentation](https://link.example.com)

`Inline code` looks like this.

```csharp
// Code block
var label = new SkiaRichLabel();
```
</draw:SkiaRichLabel>
```

### Supported Markdown Features

- **Headings** (# H1, ## H2)
- **Text formatting** (bold, italic, strikethrough)
- **Lists** (bulleted and numbered)
- **Links** (with customizable styling)
- **Code** (inline and blocks)
- **Paragraphs** (with proper spacing)

### Customizing Markdown Style

```xml
<draw:SkiaRichLabel
    LinkColor="Blue"
    CodeTextColor="DarkGreen"
    CodeBackgroundColor="#EEEEEE"
    StrikeoutColor="Red"
    PrefixBullet="• "
    PrefixNumbered="{0}. "
    UnderlineLink="True"
    UnderlineWidth="1">
# Custom Styled Markdown
This has **custom** styling for [links](https://example.com) and `code blocks`.
</draw:SkiaRichLabel>
```

### Link Handling

SkiaRichLabel provides built-in support for handling link taps:

```xml
<draw:SkiaRichLabel
    LinkTapped="OnLinkTapped"
    CommandLinkTapped="{Binding OpenLinkCommand}">
Check out [this link](https://example.com)!
</draw:SkiaRichLabel>
```

In your code-behind:

```csharp
private void OnLinkTapped(object sender, string url)
{
    // url parameter contains the link URL
    Browser.OpenAsync(url);
}
```

### Implementation Notes

- SkiaRichLabel implements a lightweight Markdown parser optimized for display, not full CommonMark compliance
- The parser focuses on the most commonly used Markdown syntax for mobile applications
- For more complex Markdown rendering needs, consider creating a custom renderer

## Special Labels

### SkiaLabelFps

A specialized label for displaying frames-per-second (FPS) metrics, useful for performance monitoring during development:

```xml
<draw:SkiaLabelFps
    TextColor="Green"
    FontSize="12"
    HorizontalOptions="End"
    VerticalOptions="Start"
    Margin="0,20,20,0" />
```

## Example: Text Card

```xml
<draw:SkiaShape
    Type="Rectangle"
    BackgroundColor="White"
    CornerRadius="8"
    Padding="16"
    WidthRequest="300">

    <draw:SkiaShape.Shadows>
        <draw:SkiaShadow
            Color="#22000000"
            BlurRadius="10"
            Offset="0,2" />
    </draw:SkiaShape.Shadows>

    <draw:SkiaLayout Type="Column" Spacing="8">
        <draw:SkiaLabel
            Text="Article Title"
            FontSize="20"
            FontWeight="700"
            TextColor="#333333" />

        <draw:SkiaLabel
            Text="Published on April 3, 2025"
            FontSize="12"
            TextColor="#666666" />

        <draw:SkiaLabel
            Text="Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam in dui mauris. Vivamus hendrerit arcu sed erat molestie vehicula. Sed auctor neque eu tellus rhoncus ut eleifend nibh porttitor."
            FontSize="14"
            TextColor="#444444"
            LineHeight="1.5" />

        <draw:SkiaLabel>
            <draw:SkiaLabel.Spans>
                <draw:TextSpan Text="Read more " TextColor="#444444" FontSize="14" />
                <draw:TextSpan Text="here" TextColor="Blue" FontSize="14" IsUnderline="True" />
            </draw:SkiaLabel.Spans>
        </draw:SkiaLabel>
    </draw:SkiaLayout>
</draw:SkiaShape>
```