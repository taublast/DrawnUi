# Native Integration

DrawnUi.Maui provides seamless integration with native MAUI controls through the `SkiaMauiElement` control. This allows you to embed standard MAUI controls like WebView, MediaElement, and others within your DrawnUI canvas while maintaining hardware acceleration and performance.

## SkiaMauiElement

`SkiaMauiElement` is a wrapper control that enables embedding native MAUI `VisualElement` controls within the DrawnUI rendering pipeline. This is essential for controls that require native platform implementations or when you need to integrate existing MAUI controls into your DrawnUI application.

### Key Features

- **Native Control Embedding**: Wrap any MAUI VisualElement within DrawnUI
- **Platform Optimization**: Automatic platform-specific handling (snapshots on Windows, direct rendering on other platforms)
- **Gesture Coordination**: Proper gesture handling between DrawnUI and native controls
- **Binding Support**: Full data binding support for embedded controls
- **Performance**: Optimized rendering with minimal overhead

### Basic Usage

```xml
<draw:SkiaMauiElement
    HorizontalOptions="Fill"
    VerticalOptions="Fill">
    
    <!-- Any MAUI VisualElement can be embedded -->
    <Entry
        Placeholder="Enter text here"
        Text="{Binding UserInput}" />
        
</draw:SkiaMauiElement>
```

### WebView Integration

One of the most common use cases is embedding a WebView for displaying web content within your DrawnUI application:

```xml
<draw:SkiaLayout
    HorizontalOptions="Fill"
    VerticalOptions="Fill"
    Type="Column">

    <!-- Header -->
    <draw:SkiaLayout
        BackgroundColor="{StaticResource Gray600}"
        HorizontalOptions="Fill"
        HeightRequest="60"
        Type="Row"
        Spacing="16"
        Padding="16,0">

        <draw:SkiaButton
            Text="← Back"
            TextColor="White"
            BackgroundColor="Transparent"
            VerticalOptions="Center" />

        <draw:SkiaLabel
            x:Name="LabelTitle"
            Text="Web Browser"
            TextColor="White"
            FontSize="18"
            VerticalOptions="Center"
            HorizontalOptions="Start" />

    </draw:SkiaLayout>

    <!-- Background -->
    <draw:SkiaControl
        BackgroundColor="{StaticResource Gray600}"
        HorizontalOptions="Fill"
        VerticalOptions="Fill"
        ZIndex="-1" />

    <!-- WebView Content -->
    <draw:SkiaMauiElement
        Margin="1,0"
        HorizontalOptions="Fill"
        VerticalOptions="Fill">

        <WebView
            x:Name="ControlBrowser"
            HorizontalOptions="FillAndExpand"
            VerticalOptions="FillAndExpand" />

    </draw:SkiaMauiElement>

</draw:SkiaLayout>
```

### Code-Behind Implementation

```csharp
public partial class ScreenBrowser
{
    public ScreenBrowser(string title, string source, bool isUrl = true)
    {
        InitializeComponent();

        LabelTitle.Text = title;

        if (isUrl)
        {
            if (string.IsNullOrEmpty(source))
            {
                source = "about:blank";
            }
            var url = new UrlWebViewSource
            {
                Url = source
            };
            ControlBrowser.Source = url;
        }
        else
        {
            if (string.IsNullOrEmpty(source))
            {
                source = "";
            }
            var html = new HtmlWebViewSource
            {
                Html = source
            };
            ControlBrowser.Source = html;
        }
    }
}
```

### Platform-Specific Behavior

`SkiaMauiElement` handles platform differences automatically:

**Windows:**
- Uses bitmap snapshots for rendering native controls within the SkiaSharp canvas
- Automatic snapshot updates when control content changes
- Optimized for performance with caching

**iOS/Android:**
- Direct native view positioning and transformation
- No snapshot overhead - native controls are moved/transformed directly
- Better performance and native feel

### Common Integration Scenarios

#### Media Playback
```xml
<draw:SkiaMauiElement
    HorizontalOptions="Fill"
    HeightRequest="200">
    
    <MediaElement
        Source="video.mp4"
        ShowsPlaybackControls="True"
        AutoPlay="False" />
        
</draw:SkiaMauiElement>
```

#### Date/Time Pickers
```xml
<draw:SkiaLayout Type="Column" Spacing="16">
    
    <draw:SkiaMauiElement HeightRequest="50">
        <DatePicker
            Date="{Binding SelectedDate}"
            Format="dd/MM/yyyy" />
    </draw:SkiaMauiElement>
    
    <draw:SkiaMauiElement HeightRequest="50">
        <TimePicker
            Time="{Binding SelectedTime}"
            Format="HH:mm" />
    </draw:SkiaMauiElement>
    
</draw:SkiaLayout>
```

#### Native Picker
```xml
<draw:SkiaMauiElement HeightRequest="50">
    <Picker
        Title="Select an option"
        ItemsSource="{Binding Options}"
        SelectedItem="{Binding SelectedOption}" />
</draw:SkiaMauiElement>
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Content` | VisualElement | The native MAUI control to embed |

### Important Notes

- **Content Property**: Use the `Content` property to set the embedded control, not child elements
- **Sizing**: The SkiaMauiElement will size itself based on the embedded control's requirements
- **Gestures**: Native controls handle their own gestures; DrawnUI gestures work outside the embedded area
- **Performance**: Consider the platform-specific rendering approach when designing your layout
- **Binding Context**: The embedded control automatically inherits the binding context

### Limitations

- Cannot have SkiaControl subviews (use Content property instead)
- Platform-specific rendering differences may affect visual consistency
- Some complex native controls may have gesture conflicts

### Best Practices

1. **Use Sparingly**: Only embed native controls when necessary (e.g., WebView, MediaElement)
2. **Size Appropriately**: Set explicit sizes when possible to avoid layout issues
3. **Test on All Platforms**: Verify behavior across iOS, Android, and Windows
4. **Consider Alternatives**: Check if DrawnUI has a native equivalent before embedding
5. **Performance**: Monitor performance impact, especially with multiple embedded controls

## SkiaCamera

SkiaCamera is a specialized control that provides camera functionality directly within the DrawnUI canvas. It allows you to capture photos and video while maintaining the performance and visual consistency of the DrawnUI rendering pipeline.

### Basic Usage

```xml
<draw:SkiaCamera
    IsPreviewEnabled="True"
    CameraFacing="Back"
    WidthRequest="300"
    HeightRequest="400" />
```

### Key Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsPreviewEnabled` | bool | true | Whether to show camera preview |
| `CameraFacing` | CameraFacing | Back | Camera to use (Front/Back) |
| `FlashMode` | FlashMode | Off | Flash mode (Off/On/Auto) |
| `IsRecording` | bool | false | Whether currently recording video |

### Examples

```xml
<!-- Basic camera with controls -->
<draw:SkiaLayout Type="Column" Spacing="10">
    <draw:SkiaCamera
        x:Name="Camera"
        IsPreviewEnabled="True"
        CameraFacing="Back"
        WidthRequest="300"
        HeightRequest="400" />

    <draw:SkiaLayout Type="Row" Spacing="10">
        <draw:SkiaButton
            Text="Capture"
            Clicked="OnCaptureClicked" />
        <draw:SkiaButton
            Text="Switch Camera"
            Clicked="OnSwitchCameraClicked" />
    </draw:SkiaLayout>
</draw:SkiaLayout>
```

### Code-Behind Example

```csharp
private async void OnCaptureClicked(object sender, EventArgs e)
{
    try
    {
        var photo = await Camera.CapturePhotoAsync();
        if (photo != null)
        {
            // Handle captured photo
            await SavePhotoAsync(photo);
        }
    }
    catch (Exception ex)
    {
        // Handle error
        await DisplayAlert("Error", $"Failed to capture photo: {ex.Message}", "OK");
    }
}

private void OnSwitchCameraClicked(object sender, EventArgs e)
{
    Camera.CameraFacing = Camera.CameraFacing == CameraFacing.Back
        ? CameraFacing.Front
        : CameraFacing.Back;
}
```
