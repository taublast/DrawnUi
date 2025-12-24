# SkiaCamera

Camera control drawn with SkiaSharp, part of DrawnUI for for .NET MAUI.

**For iOS, MacCatalyst, Android and Windows**.


## About


Camera is working in one of the two modes: Still and Video (CaptureMode property). Why? Because still and video formats are different and we must present a preview according to this format. So selecting this mode affects mainly the preview.  
For the still image capture you can select camera, format, flash.   
For video capture, it's same but separate methods/properties for video.   
On top of that the video capture has an optional pre-recording feature. What is it for? Imagine you want to trigger recording a video on some conditions. Like a rabbit appeared within camera range, some tech conditions are met etc. But when you then watch the video it would start at the exact moment the recording was triggered, while the pre-recording feature can for example record 5 seconds preceding that event, making the video wich recording was triggered by tech conditions to look natural.


## Features:

- **Renders on a hardware-accelerated SkiaSharp canvas** with all the power of Skia rendering.
- **Post-process captured bitmap** with SkiaSharp and DrawnUi, apply effects, overlay watermark etc.
- **Live preview frames in a convenient form** to integrate with AI/ML.
- **Manual camera selection** to access ultra-wide, telephoto etc by index or by front/back.
- **Precise capture format control** with manual resolution selection and automatic preview aspect ratio matching.
- **Advanced flash control** with independent preview torch and capture flash modes (Off/Auto/On).
- **Inject custom EXIF**, save GPS locations etc!
- **Cares about going to background** or foreground to automatically stop/resume camera.
- **Developer-first design**, open for customization with overrides/events,

## **Dual-Channel Architecture**

SkiaCamera provides **two independent processing channels** to be processed:

* 📹 Live Preview
* 📸 Captured Photo

> **💡 Key Insight**: Preview effects ≠ Capture effects. You can show a vintage filter in preview while capturing the raw photo, or apply completely different processing to each channel.

## Installation

Install nuget package:

```bash
dotnet add package DrawnUi.Maui.Camera
```

Initialize DrawnUi inside `MauiProgram.cs`:

```csharp
builder.UseDrawnUi();
```

[Read more about DrawnUi initialization](https://taublast.github.io/DrawnUi/articles/getting-started.html).

## Set up permissions

### Windows:

No specific setup needed.

### Apple:

Put this inside the file `Platforms/iOS/Info.plist` and `Platforms/MacCatalyst/Info.plist` inside the `<dict>` tag:

```xml
  <key>NSCameraUsageDescription</key>
  <string>Allow access to the camera</string>	
	<key>NSPhotoLibraryAddUsageDescription</key>
	<string>Allow access to the library to save photos</string>
	<key>NSPhotoLibraryUsageDescription</key>
	<string>Allow access to the library to save photos</string>
```

If you want to geo-tag photos (get and save GPS location metadata) add this:

```xml
	<key>NSLocationWhenInUseUsageDescription</key>
	<string>To be able to geotag photos</string>
```

### Android

Put this inside the file `Platforms/Android/AndroidManifest.xml` inside the `<manifest>` tag:

```xml
    <!--for camera and gallery access-->
    <uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
    <uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" android:maxSdkVersion="32" />
    <uses-permission android:name="android.permission.CAMERA" />
```

If you want to geo-tag photos (get and save GPS location metadata) add this:

```xml
  <!--geotag photos-->
  <uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
```

#### FileProvider Setup (Required for OpenFileInGallery)

To use the `OpenFileInGallery()` method, you must configure a FileProvider. Add this inside the `<application>` tag in `AndroidManifest.xml`:

```xml
<provider
    android:name="androidx.core.content.FileProvider"
    android:authorities="${applicationId}.fileprovider"
    android:exported="false"
    android:grantUriPermissions="true">
    <meta-data
        android:name="android.support.FILE_PROVIDER_PATHS"
        android:resource="@xml/file_paths" />
</provider>
```

Create `Platforms/Android/Resources/xml/file_paths.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<paths xmlns:android="http://schemas.android.com/apk/res/android">
    <external-files-path name="my_images" path="Pictures" />
    <external-files-path name="my_movies" path="Movies" />
    <cache-path name="my_cache" path="." />
</paths>
```

## Usage Guide

### 1. XAML Declaration

```xml
<camera:SkiaCamera
    x:Name="CameraControl"
    BackgroundColor="Black"
    PhotoQuality="Medium"
    Facing="Default"
    HorizontalOptions="Fill"
    VerticalOptions="Fill"
    ZoomLimitMax="10"
    ZoomLimitMin="1" />
```

### 2. Essential Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Facing` | `CameraPosition` | `Default` | Camera selection: `Default` (back), `Selfie` (front), `Manual` |
| `CameraIndex` | `int` | `-1` | Manual camera selection index (when `Facing = Manual`) |
| `IsOn` | `bool` | `false` | Camera power state - use this to start/stop camera |
| `PhotoQuality` | `CaptureQuality` | `Max` | Photo quality: `Max`, `Medium`, `Low`, `Preview`, `Manual` |
| `PhotoFormatIndex` | `int` | `0` | Format index for manual capture (when `PhotoQuality = Manual`) |
| `FlashMode` | `FlashMode` | `Off` | Preview torch mode: `Off`, `On`, `Strobe` |
| `CaptureFlashMode` | `CaptureFlashMode` | `Auto` | Flash mode for capture: `Off`, `Auto`, `On` |
| `IsFlashSupported` | `bool` | - | Whether flash is available (read-only) |
| `IsAutoFlashSupported` | `bool` | - | Whether auto flash is supported (read-only) |
| `Effect` | `SkiaImageEffect` | `None` | Real-time effects: `None`, `Sepia`, `BlackAndWhite`, `Pastel` |
| `Zoom` | `double` | `1.0` | Camera zoom level |
| `ZoomLimitMin/Max` | `double` | `1.0/10.0` | Zoom constraints |
| `State` | `CameraState` | `Off` | Current camera state (read-only) |
| `IsBusy` | `bool` | `false` | Whether camera is processing (read-only) |

### 3. Camera Lifecycle Management

```csharp
// ✅ CORRECT: Use IsOn for lifecycle management
camera.IsOn = true;  // Start camera
camera.IsOn = false; // Stop camera

// ❌ AVOID: Direct Start() calls unless you know what you're doing
// camera.Start(); // Use only in special scenarios
```

**Important**: `IsOn` vs `Start()` difference:
- `IsOn = true`: Proper lifecycle management, handles permissions, app backgrounding
- `Start()`: Direct method call, bypasses safety checks

### 4. Flash Control

SkiaCamera provides comprehensive flash control for both preview torch and still image capture:

#### Preview Torch Control
```csharp
// Property-based approach
camera.FlashMode = FlashMode.Off;   // Disable torch
camera.FlashMode = FlashMode.On;    // Enable torch
camera.FlashMode = FlashMode.Strobe; // Strobe mode (future feature)

// Get current torch mode
var currentMode = camera.GetFlashMode();
```

#### Capture Flash Mode Control
```csharp
// Set flash mode for still image capture
camera.CaptureFlashMode = CaptureFlashMode.Off;   // No flash
camera.CaptureFlashMode = CaptureFlashMode.Auto;  // Auto flash based on lighting
camera.CaptureFlashMode = CaptureFlashMode.On;    // Always flash

// Check flash capabilities
if (camera.IsFlashSupported)
{
    // Flash is available on this camera
    if (camera.IsAutoFlashSupported)
    {
        // Auto flash mode is supported
        camera.CaptureFlashMode = CaptureFlashMode.Auto;
    }
}

// Get current capture flash mode
var currentMode = camera.GetCaptureFlashMode();
```

#### XAML Binding
```xml
<camera:SkiaCamera
    x:Name="CameraControl"
    FlashMode="Off"
    CaptureFlashMode="Auto"
    Facing="Default" />
```

#### Flash Mode Cycling Examples
```csharp
// Preview torch cycling
private void OnTorchButtonClicked()
{
    var currentMode = camera.FlashMode;
    var nextMode = currentMode switch
    {
        FlashMode.Off => FlashMode.On,
        FlashMode.On => FlashMode.Off,
        FlashMode.Strobe => FlashMode.Off, // Future feature
        _ => FlashMode.Off
    };

    camera.FlashMode = nextMode;
    UpdateTorchButtonUI(nextMode);
}

// Capture flash cycling
private void OnCaptureFlashButtonClicked()
{
    var currentMode = camera.CaptureFlashMode;
    var nextMode = currentMode switch
    {
        CaptureFlashMode.Off => CaptureFlashMode.Auto,
        CaptureFlashMode.Auto => CaptureFlashMode.On,
        CaptureFlashMode.On => CaptureFlashMode.Off,
        _ => CaptureFlashMode.Auto
    };

    camera.CaptureFlashMode = nextMode;
    UpdateCaptureFlashButtonUI(nextMode);
}
```

**Important Notes:**
- `FlashMode` property controls preview torch (live view)
- `CaptureFlashMode` controls flash behavior during photo capture
- These are independent - you can have torch off but capture flash on Auto
- Flash capabilities vary by device and camera (front/back)
- Property-based approach provides excellent MVVM support and extensibility

#### Flash Control Architecture

SkiaCamera implements a **dual-channel flash system** that separates preview illumination from capture flash:

**🔦 Preview Torch Channel**
- Controls LED torch for live camera preview
- **Property**: `FlashMode` (Off/On/Strobe)
- Use case: Illumination while composing shots
- Platform: Uses torch/flashlight APIs

**📸 Capture Flash Channel**
- Controls flash behavior during photo capture
- Property: `CaptureFlashMode` (Off/Auto/On)
- Use case: Optimal lighting for still photos
- Platform: Uses camera flash APIs

**Platform Implementation Details:**

| Platform | Preview Torch | Capture Flash | Auto Flash |
|----------|---------------|---------------|------------|
| **Android** | `FlashMode.Torch` | `FlashMode.Single` + `ControlAEMode` | ✅ `OnAutoFlash` |
| **iOS/macOS** | `AVCaptureTorchMode` | `AVCaptureFlashMode` | ✅ `Auto` mode |
| **Windows** | `FlashControl.Enabled` | `FlashControl.Auto` | ✅ Auto detection |

### 5. Capture Format Selection

SkiaCamera provides **precise control over capture resolution and aspect ratio** with automatic preview synchronization:

#### Quality Presets
```csharp
// Quick quality selection
camera.PhotoQuality = CaptureQuality.Max;     // Highest resolution
camera.PhotoQuality = CaptureQuality.Medium;  // Balanced quality/size
camera.PhotoQuality = CaptureQuality.Low;     // Fastest capture
camera.PhotoQuality = CaptureQuality.Preview; // Smallest usable size
```

#### Manual Format Selection
```csharp
// Get available capture formats for current camera
var formats = await camera.GetAvailableCaptureFormatsAsync();

// Display format picker
var options = formats.Select((format, index) =>
    $"[{index}] {format.Width}x{format.Height}, {format.AspectRatioString}"
).ToArray();

var result = await DisplayActionSheet("Select Capture Format", "Cancel", null, options);

if (!string.IsNullOrEmpty(result))
{
    var selectedIndex = Array.IndexOf(options, result);
    if (selectedIndex >= 0)
    {
        // Set manual capture mode with selected format
        camera.PhotoQuality = CaptureQuality.Manual;
        camera.PhotoFormatIndex = selectedIndex;

        // Preview automatically adjusts to match aspect ratio!
        var selectedFormat = formats[selectedIndex];
        Debug.WriteLine($"Selected: {selectedFormat.Description}");
    }
}
```

#### Reading Current Format
```csharp
// Get the currently selected capture format
var currentFormat = camera.CurrentStillCaptureFormat;
if (currentFormat != null)
{
    Debug.WriteLine($"Current capture format: {currentFormat.Description}");
    Debug.WriteLine($"Resolution: {currentFormat.Width}x{currentFormat.Height}");
    Debug.WriteLine($"Aspect ratio: {currentFormat.AspectRatioString}");
    Debug.WriteLine($"Total pixels: {currentFormat.TotalPixels:N0}");
}

// This works regardless of whether PhotoQuality is set to:
// - Max, Medium, Low, Preview (automatic selection)
// - Manual (using PhotoFormatIndex)
```

#### Format Information
```csharp
// CaptureFormat provides detailed information
foreach (var format in formats)
{
    Console.WriteLine($"Resolution: {format.Width}x{format.Height}");
    Console.WriteLine($"Aspect Ratio: {format.AspectRatioString}"); // "16:9", "4:3", etc.
    Console.WriteLine($"Total Pixels: {format.TotalPixels:N0}");
    Console.WriteLine($"Description: {format.Description}");
    Console.WriteLine($"Platform ID: {format.FormatId}");
}
```

#### Automatic Preview Synchronization

**🎯 Key Feature**: When you change capture format, preview automatically adjusts to match the aspect ratio:

```csharp
// Before: Preview might be 16:9, capture might be 4:3 (letterboxing!)
camera.PhotoQuality = CaptureQuality.Manual;
camera.PhotoFormatIndex = 2; // Select 4000x3000 (4:3)

// After: Preview automatically switches to 4:3 aspect ratio
// Result: True WYSIWYG - what you see is what you capture!
```

**Platform Implementation:**
- **Android**: Uses `ChooseOptimalSize()` with aspect ratio matching
- **iOS/macOS**: Single format controls both preview and capture
- **Windows**: Dynamic preview format switching with `ChooseOptimalPreviewFormat()`

#### Format Caching & Performance
```csharp
// Formats are cached when camera initializes
var formats = await camera.GetAvailableCaptureFormatsAsync(); // Fast - uses cache

// Force refresh when camera changes
await camera.RefreshAvailableCaptureFormatsAsync(); // Slower - re-detects

// Cache is automatically cleared when:
// - Camera facing changes (Front ↔ Back)
// - Manual camera index changes
// - Camera restarts
```

#### Example: Professional Camera App
```csharp
private async void OnFormatSelectionClicked()
{
    try
    {
        var formats = await CameraControl.GetAvailableCaptureFormatsAsync();

        // Group by aspect ratio for better UX
        var groupedFormats = formats
            .GroupBy(f => f.AspectRatioString)
            .OrderByDescending(g => g.Key == "16:9") // Prioritize 16:9
            .ThenByDescending(g => g.Key == "4:3")   // Then 4:3
            .ToList();

        var options = new List<string>();
        var formatMap = new Dictionary<string, (int index, CaptureFormat format)>();

        foreach (var group in groupedFormats)
        {
            options.Add($"--- {group.Key} Aspect Ratio ---");

            foreach (var format in group.OrderByDescending(f => f.TotalPixels))
            {
                var index = formats.IndexOf(format);
                var megapixels = format.TotalPixels / 1_000_000.0;
                var option = $"{format.Width}x{format.Height} ({megapixels:F1}MP)";

                options.Add(option);
                formatMap[option] = (index, format);
            }
        }

        var result = await DisplayActionSheet("Select Resolution", "Cancel", null, options.ToArray());

        if (formatMap.TryGetValue(result, out var selection))
        {
            CameraControl.PhotoQuality = CaptureQuality.Manual;
            CameraControl.PhotoFormatIndex = selection.index;

            StatusLabel.Text = $"📸 {selection.format.Description}";
        }
    }
    catch (Exception ex)
    {
        await DisplayAlert("Error", $"Failed to get formats: {ex.Message}", "OK");
    }
}
```

### 6. Opening Files in Gallery

Use `OpenFileInGallery()` to open captured photos in the system gallery app:

```csharp
private async void OnCaptureSuccess(object sender, CapturedImage captured)
{
    try
    {
        // Save the captured image
        var fileName = $"photo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        var filePath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);

        // Save SKImage to file
        using var fileStream = File.Create(filePath);
        using var data = captured.Image.Encode(SKEncodedImageFormat.Jpeg, 90);
        data.SaveTo(fileStream);

        // Open in gallery
        camera.OpenFileInGallery(filePath);
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Error opening file in gallery: {ex.Message}");
    }
}
```

**Platform Requirements:**
- **Android**: Requires FileProvider configuration (see setup section above)
- **iOS/macOS**: Works out of the box
- **Windows**: Opens with default photo viewer

**Real-World Usage Scenarios:**

```csharp
// Scenario 1: Night photography with preview torch
camera.FlashMode = FlashMode.On;                 // Light up preview for composition
camera.CaptureFlashMode = CaptureFlashMode.On;   // Ensure flash fires for photo

// Scenario 2: Daylight with auto flash backup
camera.FlashMode = FlashMode.Off;                // No preview torch needed
camera.CaptureFlashMode = CaptureFlashMode.Auto; // Flash only if needed

// Scenario 3: Silent/stealth mode
camera.FlashMode = FlashMode.Off;                // No preview light
camera.CaptureFlashMode = CaptureFlashMode.Off;  // No capture flash

// Scenario 4: Future strobe mode for special effects
camera.FlashMode = FlashMode.Strobe;             // Blinking torch (future feature)
camera.CaptureFlashMode = CaptureFlashMode.Off;  // No capture flash
```

### 5. Camera Selection

#### Automatic Selection (Default)
```csharp
// Back camera
camera.Facing = CameraPosition.Default;

// Front camera
camera.Facing = CameraPosition.Selfie;

// Switch between front/back
private void SwitchCamera()
{
    if (camera.IsOn)
    {
        camera.Facing = camera.Facing == CameraPosition.Selfie
            ? CameraPosition.Default
            : CameraPosition.Selfie;
    }
}
```

#### Manual Camera Selection
```csharp
// Get available cameras
var cameras = await camera.GetAvailableCamerasAsync();

// List all cameras
foreach (var cam in cameras)
{
    Console.WriteLine($"Camera {cam.Index}: {cam.Name} ({cam.Position}) Flash: {cam.HasFlash}");
}

// Select specific camera (e.g., ultra-wide, telephoto)
camera.Facing = CameraPosition.Manual;
camera.CameraIndex = 2; // Select third camera
camera.IsOn = true;
```

### 5. Camera Information Class

```csharp
public class CameraInfo
{
    public string Id { get; set; }           // Platform-specific camera ID
    public string Name { get; set; }         // Human-readable name ("Back Camera", "Front Camera")
    public CameraPosition Position { get; set; } // Front/Back/Unknown
    public int Index { get; set; }           // Index for manual selection
    public bool HasFlash { get; set; }       // Flash capability
}
```

### 6. Channel 2: Captured Photo Processing

#### Basic Capture with Separate Processing
```csharp
// CHANNEL 2: High-quality still photo processing (independent of preview)
camera.CaptureSuccess += OnCaptureSuccess;
camera.CaptureFailed += OnCaptureFailed;

private async void TakePicture()
{
    if (camera.State == CameraState.On && !camera.IsBusy)
    {
        // Optional: Flash screen effect
        camera.FlashScreen(Color.Parse("#EEFFFFFF"));

        // Capture photo (this is separate from preview processing)
        await camera.TakePicture().ConfigureAwait(false);
    }
}

private void OnCaptureSuccess(object sender, CapturedImage captured)
{
    // CHANNEL 2: Process captured photo independently
    var originalImage = captured.Image; // SKImage - full resolution
    var timestamp = captured.Time;
    var metadata = captured.Metadata;

    // Apply different processing than preview
    ProcessCapturedPhoto(originalImage, metadata);
}

private void ProcessCapturedPhoto(SKImage originalImage, Dictionary<string, object> metadata)
{
    // This processing is SEPARATE from preview effects
    // You can apply completely different effects here

    // Example: Preview shows sketch effect, but save original + watermark
    var processedImage = ApplyWatermarkAndEffects(originalImage);
    SaveToGallery(processedImage);
}

private void OnCaptureFailed(object sender, Exception ex)
{
    Debug.WriteLine($"Capture failed: {ex.Message}");
}
```

#### Command-Based Capture (MVVM)
```csharp
public ICommand CommandCapturePhoto => new Command(async () =>
{
    if (camera.State == CameraState.On && !camera.IsBusy)
    {
        camera.FlashScreen(Color.Parse("#EEFFFFFF"));
        await camera.TakePicture().ConfigureAwait(false);
    }
});
```

### 7. Real-Time Effects & Custom Shaders

#### Built-in Effects
```csharp
// Cycle through built-in effects
private void CycleEffects()
{
    var effects = new[]
    {
        SkiaImageEffect.None,
        SkiaImageEffect.Sepia,
        SkiaImageEffect.BlackAndWhite,
        SkiaImageEffect.Pastel,
        SkiaImageEffect.Custom  // For custom shaders
    };

    var currentIndex = Array.IndexOf(effects, camera.Effect);
    var nextIndex = (currentIndex + 1) % effects.Length;
    camera.Effect = effects[nextIndex];
}
```

#### Custom Shader Effects (Advanced)
```csharp
// Apply custom SKSL shader to camera preview
public class CameraWithEffects : SkiaCamera
{
    private SkiaShaderEffect _shader;

    public void SetCustomShader(string shaderFilename)
    {
        // Remove existing shader
        if (_shader != null && VisualEffects.Contains(_shader))
        {
            VisualEffects.Remove(_shader);
        }

        // Create new shader effect
        _shader = new SkiaShaderEffect()
        {
            ShaderSource = shaderFilename,  // e.g., "Shaders/Camera/retrotv.sksl"
            FilterMode = SKFilterMode.Linear
        };

        // Add to camera's visual effects
        VisualEffects.Add(_shader);
        Effect = SkiaImageEffect.Custom;
    }

    public void ChangeShaderCode(string skslCode)
    {
        if (_shader != null)
        {
            _shader.ShaderCode = skslCode;  // Live shader editing!
        }
    }
}
```

#### Real-World Shader Examples
```csharp
// Professional film looks
SetCustomShader("Shaders/Camera/action.sksl");      // John Wick action grade
SetCustomShader("Shaders/Camera/wes.sksl");         // Wes Anderson pastel
SetCustomShader("Shaders/Camera/bwfineart.sksl");   // Fine art B&W
SetCustomShader("Shaders/Camera/retrotv.sksl");     // CRT/VHS effect
SetCustomShader("Shaders/Camera/sketch.sksl");      // Pencil sketch
SetCustomShader("Shaders/Camera/enigma.sksl");      // Matrix color grade

// Camera lens simulations
SetCustomShader("Shaders/Camera/bwstreet200.sksl"); // 200mm telephoto with shallow DOF
```

#### Shader Preview Grid (Like Instagram Filters)
```xml
<!-- XAML: Shader selection grid -->
<draw:SkiaLayout Type="Row" ItemsSource="{Binding ShaderItems}">
    <draw:SkiaLayout.ItemTemplate>
        <DataTemplate x:DataType="models:ShaderItem">
            <draw:SkiaShape WidthRequest="80" HeightRequest="80">
                <!-- Preview image with shader applied -->
                <draw:SkiaImage
                    ImageBitmap="{Binding Source={x:Reference CameraControl}, Path=DisplayPreview}"
                    Aspect="AspectCover">
                    <draw:SkiaImage.VisualEffects>
                        <draw:SkiaShaderEffect ShaderSource="{Binding ShaderFilename}" />
                    </draw:SkiaImage.VisualEffects>
                </draw:SkiaImage>
            </draw:SkiaShape>
        </DataTemplate>
    </draw:SkiaLayout.ItemTemplate>
</draw:SkiaLayout>
```
```

### 8. Zoom Control

```csharp
// Manual zoom
camera.Zoom = 2.0; // 2x zoom

// Zoom with limits
private void ZoomIn()
{
    camera.Zoom = Math.Min(camera.Zoom + 0.2, camera.ZoomLimitMax);
}

private void ZoomOut()
{
    camera.Zoom = Math.Max(camera.Zoom - 0.2, camera.ZoomLimitMin);
}

// Pinch-to-zoom gesture (XAML)
// <draw:SkiaHotspotZoom ZoomMax="3" ZoomMin="1" Zoomed="OnZoomed" />

private void OnZoomed(object sender, ZoomEventArgs e)
{
    camera.Zoom = e.Value;
}
```

### 9. Camera State Management

```csharp
// Subscribe to state changes
camera.StateChanged += OnCameraStateChanged;

private void OnCameraStateChanged(object sender, CameraState newState)
{
    switch (newState)
    {
        case CameraState.Off:
            // Camera is off
            break;
        case CameraState.On:
            // Camera is running
            break;
        case CameraState.Error:
            // Camera error occurred
            break;
    }
}

// Check camera state before operations
if (camera.State == CameraState.On)
{
    // Safe to perform camera operations
}
```

### 10. Channel 1: Live Preview Processing

```csharp
// CHANNEL 1: Real-time preview frame processing
camera.NewPreviewSet += OnNewPreviewFrame;

private void OnNewPreviewFrame(object sender, LoadedImageSource source)
{
    // This fires for EVERY preview frame (30-60 FPS)
    // Perfect for AI/ML analysis, face detection, object recognition
    Task.Run(() => ProcessPreviewFrameForAI(source));
}

private void ProcessPreviewFrameForAI(LoadedImageSource source)
{
    // AI/ML processing on live preview
    // - Face detection
    // - Object recognition
    // - QR code scanning
    // - Real-time analytics

    // Note: Optimize carefully - this runs continuously!
    var faces = DetectFaces(source.Image);
    var objects = RecognizeObjects(source.Image);

    // Update UI with real-time results
    MainThread.BeginInvokeOnMainThread(() =>
    {
        UpdateOverlayWithDetections(faces, objects);
    });
}

// Apply real-time effects to preview (independent of capture)
private void ApplyPreviewEffects()
{
    // These effects only affect what user sees, not captured photo
    camera.Effect = SkiaImageEffect.Sepia;  // Preview shows sepia

    // Or custom shader for preview
    camera.SetCustomShader("Shaders/Camera/sketch.sksl");  // Preview shows sketch
}
```

### 11. Permission Handling

```csharp
// Check and request permissions before starting camera
SkiaCamera.CheckPermissions(async (granted) =>
{
    if (granted)
    {
        camera.IsOn = true;
    }
    else
    {
        // Handle permission denied
        await DisplayAlert("Permission Required", "Camera access is required", "OK");
    }
});
```

### 12. Complete MVVM Example

#### ViewModel
```csharp
public class CameraViewModel : INotifyPropertyChanged, IDisposable
{
    private SkiaCamera _camera;

    public void AttachCamera(SkiaCamera camera)
    {
        if (_camera == null && camera != null)
        {
            _camera = camera;
            _camera.CaptureSuccess += OnCaptureSuccess;
            _camera.StateChanged += OnCameraStateChanged;
            _camera.NewPreviewSet += OnNewPreviewSet;
        }
    }

    public ICommand CommandCapturePhoto => new Command(async () =>
    {
        if (_camera?.State == CameraState.On && !_camera.IsBusy)
        {
            _camera.FlashScreen(Color.Parse("#EEFFFFFF"));
            await _camera.TakePicture().ConfigureAwait(false);
        }
    });

    public ICommand CommandSwitchCamera => new Command(() =>
    {
        if (_camera?.IsOn == true)
        {
            _camera.Facing = _camera.Facing == CameraPosition.Selfie
                ? CameraPosition.Default
                : CameraPosition.Selfie;
        }
    });

    private void OnCaptureSuccess(object sender, CapturedImage captured)
    {
        // Handle captured image
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Update UI with captured image
        });
    }

    public void Dispose()
    {
        if (_camera != null)
        {
            _camera.CaptureSuccess -= OnCaptureSuccess;
            _camera.StateChanged -= OnCameraStateChanged;
            _camera.NewPreviewSet -= OnNewPreviewSet;
            _camera = null;
        }
    }
}
```

#### Page Code-Behind
```csharp
public partial class CameraPage : ContentPage
{
    private readonly CameraViewModel _viewModel;

    public CameraPage(CameraViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Attach camera to viewmodel
        var camera = this.FindByName<SkiaCamera>("CameraControl");
        _viewModel.AttachCamera(camera);

        // Start camera with permission check
        SkiaCamera.CheckPermissions(async (granted) =>
        {
            if (granted)
            {
                camera.IsOn = true;
            }
        });
    }
}
```

## Testing & Examples

### Demo Projects

#### Basic Camera Demo
- `ScreenCameraPhoto.xaml` - Complete camera UI with controls
- `ScreenCameraPhoto.xaml.cs` - Event handling and camera operations
- `TakePictureViewModel.cs` - MVVM pattern implementation
- `CameraLayout.cs` - Custom camera container with lifecycle management

#### Advanced Shader Camera Demo (ShaderCamera Project)
- `MainPageCamera.xaml` - Professional camera UI with shader effects
- `CameraWithEffects.cs` - Extended camera with custom shader support
- `CameraViewModel.cs` - Advanced MVVM with shader management
- `ShaderEditorPage.cs` - Live shader code editing
- **Professional Shaders**:
  - `action.sksl` - John Wick action movie color grading
  - `wes.sksl` - Wes Anderson pastel aesthetic
  - `bwfineart.sksl` - Fine art black & white with grain
  - `retrotv.sksl` - Retro CRT/VHS effect with distortion
  - `sketch.sksl` - Pencil sketch artistic effect
  - `enigma.sksl` - Matrix-style color transformation
  - `bwstreet200.sksl` - 200mm telephoto lens simulation with shallow DOF

### Quick Start Examples

#### Basic Camera
```csharp
var camera = new SkiaCamera
{
    Facing = CameraPosition.Default,
    PhotoQuality = CaptureQuality.Medium,
    IsOn = true
};
```

#### Professional Shader Camera
```csharp
var camera = new CameraWithEffects
{
    Facing = CameraPosition.Default,
    ConstantUpdate = true,  // For smooth shader effects
    UseCache = SkiaCacheType.GPU
};

// Apply professional look
camera.SetCustomShader("Shaders/Camera/action.sksl");
camera.IsOn = true;
```

## Advanced Patterns & Best Practices

### 1. Camera Selection UI

```csharp
public void ShowCameraPicker()
{
    MainThread.BeginInvokeOnMainThread(async () =>
    {
        var cameras = await CameraControl.GetAvailableCamerasAsync();

        // Create picker with detailed camera info
        var options = cameras.Select(c =>
            $"{c.Name} ({c.Position}){(c.HasFlash ? " 📸" : "")}"
        ).ToArray();

        var result = await App.Current.MainPage.DisplayActionSheet("Select Camera", "Cancel", null, options);
        if (!string.IsNullOrEmpty(result))
        {
            var selectedIndex = options.FindIndex(result);
            if (selectedIndex >= 0)
            {
                CameraControl.Facing = CameraPosition.Manual;
                CameraControl.CameraIndex = selectedIndex;
                CameraControl.IsOn = true;
            }
        }
    });
}
```


### Performance Optimization

```csharp
 
// Efficient preview processing
private readonly SemaphoreSlim _frameProcessingSemaphore = new(1, 1);

private void OnNewPreviewFrame(object sender, LoadedImageSource source)
{
    // skip frame if AI/ML still processing previous one
    if (!_frameProcessingSemaphore.Wait(0))
        return;

    //do not block camera, process in another thread
    Task.Run(async () =>
    {
        try
        {
            await ProcessFrameAsync(source);
        }
        finally
        {
            _frameProcessingSemaphore.Release();
        }
    });
}
```

### Error Handling & Recovery

```csharp
private int _cameraRestartAttempts = 0;
private const int MaxRestartAttempts = 3;

private async void OnCameraError(object sender, string error)
{
    Debug.WriteLine($"Camera error: {error}");

    if (_cameraRestartAttempts < MaxRestartAttempts)
    {
        _cameraRestartAttempts++;

        // Wait before retry
        await Task.Delay(1000);

        // Attempt restart
        camera.IsOn = false;
        await Task.Delay(500);
        camera.IsOn = true;
    }
    else
    {
        // Show user-friendly error
        await DisplayAlert("Camera Error",
            "Camera is not responding. Please restart the app.", "OK");
    }
}

private void OnCameraStateChanged(object sender, CameraState newState)
{
    if (newState == CameraState.On)
    {
        // Reset retry counter on successful start
        _cameraRestartAttempts = 0;
    }
}
```
 

## API Reference

### Core Properties
```csharp
// Camera Control
public bool IsOn { get; set; }                    // Start/stop camera
public CameraPosition Facing { get; set; }        // Camera selection mode
public int CameraIndex { get; set; }              // Manual camera index
public CameraState State { get; }                 // Current state (read-only)
public bool IsBusy { get; }                       // Processing state (read-only)

// Capture Settings
public CaptureQuality PhotoQuality { get; set; } // Photo quality
public int PhotoFormatIndex { get; set; }             // Manual format index
public CaptureFlashMode CaptureFlashMode { get; set; }   // Flash mode for capture

// Flash Control
public FlashMode FlashMode { get; set; }                 // Preview torch mode
public bool IsFlashSupported { get; }                   // Flash availability
public bool IsAutoFlashSupported { get; }               // Auto flash support
public SkiaImageEffect Effect { get; set; }       // Real-time simple color filters

// Video Recording
public bool IsRecordingVideo { get; }             // Recording state (read-only)
public VideoQuality VideoQuality { get; set; }   // Video quality preset
public int VideoFormatIndex { get; set; }         // Manual format index
public bool UseCaptureVideoFlow { get; set; }     // Enable frame-by-frame capture mode
public Action<DrawableFrame> FrameProcessor { get; set; } // Frame processing callback

// Pre-Recording
public bool EnablePreRecording { get; set; }      // Enable pre-recording buffer
public TimeSpan PreRecordDuration { get; set; }   // Duration of pre-recording buffer (default: 5s)
public bool IsPreRecording { get; }               // Pre-recording state (read-only)
public TimeSpan LiveRecordingDuration { get; }    // Duration of current live recording (excluding buffer)

// Zoom & Limits
public double Zoom { get; set; }                  // Current zoom level
public double ZoomLimitMin { get; set; }          // Minimum zoom
public double ZoomLimitMax { get; set; }          // Maximum zoom

// Audio Recording
public bool RecordAudio { get; set; }             // Include audio in video recordings (default: false)
```

### Core Methods
```csharp
// Camera Management
public async Task<List<CameraInfo>> GetAvailableCamerasAsync()
public async Task<List<CameraInfo>> RefreshAvailableCamerasAsync()
public static void CheckPermissions(Action<bool> callback)

// Capture Format Management
public async Task<List<CaptureFormat>> GetAvailableCaptureFormatsAsync()
public async Task<List<CaptureFormat>> RefreshAvailableCaptureFormatsAsync()
public CaptureFormat CurrentStillCaptureFormat { get; }           // Currently selected format

// Capture Operations
public async Task TakePicture()
public void FlashScreen(Color color, long duration = 250)
public void OpenFileInGallery(string filePath)               // Open file in system gallery

// Video Recording Operations
public async Task StartVideoRecording()                      // Start video recording
public async Task StopVideoRecording()                       // Stop video recording (aborts if < 1s in pre-recording mode)
public bool CanRecordVideo()                                 // Check recording support
public async Task<List<VideoFormat>> GetAvailableVideoFormatsAsync()  // Get video formats
public VideoFormat GetCurrentVideoFormat()                  // Current video format
public async Task<string> MoveVideoToGalleryAsync(CapturedVideo video, string album = null, bool deleteOriginal = true) // Move video to gallery (consistent API)

// Camera Controls
public void SetZoom(double value)

// Flash Control
public void SetFlashMode(FlashMode mode)                 // Preview torch control
public FlashMode GetFlashMode()                          // Get current torch mode
public void SetCaptureFlashMode(CaptureFlashMode mode)   // Capture flash control
public CaptureFlashMode GetCaptureFlashMode()            // Get current capture mode
public bool IsFlashSupported { get; }                   // Flash availability
public bool IsAutoFlashSupported { get; }               // Auto flash support
```

### Events
```csharp
// Photo Capture Events
public event EventHandler<CapturedImage> CaptureSuccess;
public event EventHandler<Exception> CaptureFailed;

// Video Recording Events
public event EventHandler<CapturedVideo> VideoRecordingSuccess;
public event EventHandler<Exception> VideoRecordingFailed;
public event EventHandler<TimeSpan> VideoRecordingProgress;

// Preview & State Events
public event EventHandler<LoadedImageSource> NewPreviewSet;
public event EventHandler<CameraState> StateChanged;
public event EventHandler<string> OnError;
public event EventHandler<double> Zoomed;
```

### Data Classes
```csharp
// Capture format information
public class CaptureFormat
{
    public int Width { get; set; }                    // Width in pixels
    public int Height { get; set; }                   // Height in pixels
    public int TotalPixels => Width * Height;         // Total pixel count
    public double AspectRatio => (double)Width / Height; // Decimal aspect ratio
    public string AspectRatioString { get; }          // Standard notation ("16:9", "4:3")
    public string FormatId { get; set; }              // Platform-specific identifier
    public string Description { get; }               // Human-readable description
}

// Camera information
public class CameraInfo
{
    public string Id { get; set; }                    // Platform camera ID
    public string Name { get; set; }                  // Display name
    public CameraPosition Position { get; set; }      // Camera position
    public int Index { get; set; }                    // Camera index
    public bool HasFlash { get; set; }                // Flash availability
}

// Video format information
public class VideoFormat
{
    public int Width { get; set; }                    // Video width in pixels
    public int Height { get; set; }                   // Video height in pixels
    public double FrameRate { get; set; }             // Frames per second
    public string Codec { get; set; }                 // Video codec (H.264, H.265, etc.)
    public long BitRate { get; set; }                 // Bit rate in bits per second
    public double AspectRatio => (double)Width / Height; // Aspect ratio
    public string Description { get; }               // Human-readable description
}

// Captured video information
public class CapturedVideo
{
    public string FilePath { get; set; }              // Path to recorded video file
    public TimeSpan Duration { get; set; }            // Video duration
    public VideoFormat Format { get; set; }           // Video format used
    public CameraPosition Facing { get; set; }        // Camera that recorded video
    public DateTime Time { get; set; }                // Recording timestamp
    public Dictionary<string, object> Metadata { get; set; } // Additional metadata
    public long FileSizeBytes { get; set; }           // File size in bytes
}
```

### Enums
```csharp
public enum CameraPosition { Default, Selfie, Manual }
public enum CameraState { Off, On, Error }
public enum CaptureQuality { Max, Medium, Low, Preview, Manual }
public enum VideoQuality { Low, Standard, High, Ultra, Manual }
public enum FlashMode { Off, On, Strobe }
public enum CaptureFlashMode { Off, Auto, On }
public enum SkiaImageEffect { None, Sepia, BlackAndWhite, Pastel }
```

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| **Camera not starting** | Missing permissions | Use `SkiaCamera.CheckPermissions()` |
| **Black preview** | Camera enumeration failed | Check device camera availability |
| **Capture failures** | Storage permissions | Verify write permissions |
| **Performance issues** | Unoptimized preview processing | Cache controls, limit frame processing |
| **Manual selection fails** | Invalid `CameraIndex` | Verify index is 0 to `cameras.Count-1` |
| **Flash not working** | Flash not supported or wrong mode | Check `IsFlashSupported` and use correct `CaptureFlashMode` |
| **App crashes on camera switch** | Rapid camera changes | Add delays between camera operations |
| **OpenFileInGallery fails (Android)** | FileProvider not configured | Add FileProvider to AndroidManifest.xml (see setup) |
| **"Failed to find configured root"** | Invalid file_paths.xml | Check file is in declared FileProvider paths |
| **FileUriExposedException** | Missing FileProvider | Configure FileProvider with correct authority |
| **Memory leaks** | Event handlers not removed | Properly dispose and unsubscribe events |

### Debug Tips

```csharp
// Enable debug logging
#if DEBUG
camera.OnError += (s, error) => Debug.WriteLine($"Camera Error: {error}");
camera.StateChanged += (s, state) => Debug.WriteLine($"Camera State: {state}");
#endif

// Check camera availability
var cameras = await camera.GetAvailableCamerasAsync();
Debug.WriteLine($"Available cameras: {cameras.Count}");
foreach (var cam in cameras)
{
    Debug.WriteLine($"  {cam.Index}: {cam.Name} ({cam.Position}) Flash: {cam.HasFlash}");
}

// Monitor performance
var stopwatch = Stopwatch.StartNew();
await camera.TakePicture();
Debug.WriteLine($"Capture took: {stopwatch.ElapsedMilliseconds}ms");
```

### Platform-Specific Notes

#### Android
- Requires Camera2 API (API level 21+)
- Some devices may have camera enumeration delays
- Test on various Android versions and manufacturers

#### iOS/macOS
- AVFoundation framework required
- Camera permissions must be declared in Info.plist
- Some camera types only available on newer devices

#### Windows
- UWP/WinUI MediaCapture APIs
- Desktop apps will ask user for camera permissions

## AI Agent Integration Guide

This section provides specific guidance for AI agents working with SkiaCamera.

### Quick Start for AI Agents

```csharp
// 1. Setup dual-channel camera
var camera = new SkiaCamera
{
    Facing = CameraPosition.Default,
    PhotoQuality = CaptureQuality.Medium,
    IsOn = true
};

// 2. CHANNEL 1: Live preview processing for AI/ML
camera.NewPreviewSet += (s, source) => {
    // Real-time AI processing on preview frames
    Task.Run(() => ProcessFrameForAI(source.Image));
};

// 3. CHANNEL 2: Captured photo processing
camera.CaptureSuccess += (s, captured) => {
    // High-quality processing on captured photo
    ProcessCapturedPhoto(captured.Image, captured.Metadata);
};

// 4. Manual camera selection
var cameras = await camera.GetAvailableCamerasAsync();
camera.Facing = CameraPosition.Manual;
camera.CameraIndex = 2; // Select third camera

// 5. Take photo (triggers Channel 2 processing)
await camera.TakePicture();
```

### Key Patterns for AI Agents

1. **Understand dual channels**: Preview processing ≠ Capture processing
2. **Channel 1 (Preview)**: Use `NewPreviewSet` event for real-time AI/ML
3. **Channel 2 (Capture)**: Use `CaptureSuccess` event for high-quality processing
4. **Always check `camera.State == CameraState.On` before operations**
5. **Use `camera.IsOn = true/false` for lifecycle management**
6. **Subscribe to events before starting camera**
7. **Handle permissions with `SkiaCamera.CheckPermissions()`**
8. **Use `ConfigureAwait(false)` for async operations**

### Common AI Agent Mistakes to Avoid

❌ **Don't confuse the channels:**
```csharp
// WRONG: Thinking preview effects affect captured photos
camera.Effect = SkiaImageEffect.Sepia;  // Only affects preview
await camera.TakePicture(); // Photo is NOT sepia unless you process it separately
```

❌ **Don't do this:**
```csharp
camera.Start(); // Direct method call
camera.TakePicture(); // Without checking state
```

✅ **Do this instead:**
```csharp
// CORRECT: Understand dual channels
camera.Effect = SkiaImageEffect.Sepia;  // Preview shows sepia

camera.CaptureSuccess += (s, captured) => {
    // Captured photo is original - apply effects here if needed
    var sepiaPhoto = ApplySepia(captured.Image);
};

camera.IsOn = true; // Proper lifecycle
if (camera.State == CameraState.On && !camera.IsBusy)
    await camera.TakePicture().ConfigureAwait(false);
```

## Features

- [x] **Cross-platform support** (Android, iOS, MacCatalyst, Windows)
- [x] **Hardware-accelerated rendering** with SkiaSharp
- [x] **Manual camera selection** by index with enumeration
- [x] **Automatic camera selection** (front/back)
- [x] **Camera enumeration** with `GetAvailableCamerasAsync()`
- [x] **Capture format management** with `GetAvailableCaptureFormatsAsync()` and `CurrentStillCaptureFormat`
- [x] **Custom resolution selection** for capture with quality presets and manual format selection
- [x] **Real-time preview effects** (Sepia, B&W, Pastel)
- [x] **Photo capture** with metadata and custom rendering applied
- [x] **Zoom control** with configurable limits
- [x] **Advanced flash control** (independent preview torch and capture flash modes)
- [x] **Video recording API** with format selection, quality presets, and event-driven progress monitoring
- [x] **Event-driven architecture** for MVVM patterns
- [x] **Permission handling** with built-in checks
- [x] **State management** with proper lifecycle
- [x] **Performance optimization** with GPU caching and acceleration

### Video Recording

SkiaCamera provides comprehensive video recording capabilities with format selection, quality control, and cross-platform support. Video recording follows the same dual-channel architecture as photo capture - the live preview continues uninterrupted while recording video in the background.

#### Basic Video Recording

```csharp
// Check if video recording is supported
if (camera.CanRecordVideo())
{
    // Start recording
    await camera.StartVideoRecording();
    
    // Stop recording
    await camera.StopVideoRecording();
}

// Subscribe to video recording events
camera.VideoRecordingSuccess += OnVideoRecordingSuccess;
camera.VideoRecordingFailed += OnVideoRecordingFailed;
camera.VideoRecordingProgress += OnVideoRecordingProgress;

private async void OnVideoRecordingSuccess(object sender, CapturedVideo video)
{
    // Video recording completed successfully
    var filePath = video.FilePath;
    var duration = video.Duration;
    var format = video.Format;
    
    // Save to gallery with consistent API (move instead of copy for performance)
    var galleryPath = await camera.MoveVideoToGalleryAsync(video, "MyApp");
}

private void OnVideoRecordingFailed(object sender, Exception ex)
{
    await DisplayAlert("Recording Error", $"Failed to record video: {ex.Message}", "OK");
}

private void OnVideoRecordingProgress(object sender, TimeSpan duration)
{
    // Update UI with recording progress
    RecordingTimeLabel.Text = $"Recording: {duration:mm\\:ss}";
}
```

#### Video Recording Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsRecordingVideo` | `bool` | `false` | Whether video recording is active (read-only) |
| `VideoQuality` | `VideoQuality` | `Standard` | Video quality preset: `Low`, `Standard`, `High`, `Ultra`, `Manual` |
| `VideoFormatIndex` | `int` | `0` | Format index for manual recording (when `VideoQuality = Manual`) |
| `CanRecordVideo()` | `bool` | - | Whether video recording is supported on current camera |

#### Video Quality Presets

```csharp
// Quick quality selection
camera.VideoQuality = VideoQuality.Low;       // 720p, lower bitrate, smaller files
camera.VideoQuality = VideoQuality.Standard;  // 1080p, balanced quality/size
camera.VideoQuality = VideoQuality.High;      // 1080p, higher bitrate
camera.VideoQuality = VideoQuality.Ultra;     // 4K if available, highest quality
```

#### Manual Video Format Selection

```csharp
// Get available video formats for current camera
var formats = await camera.GetAvailableVideoFormatsAsync();

// Display format picker
var options = formats.Select((format, index) =>
    $"[{index}] {format.Description}"
).ToArray();

var result = await DisplayActionSheet("Select Video Format", "Cancel", null, options);

if (!string.IsNullOrEmpty(result) && result != "Cancel")
{
    var selectedIndex = Array.FindIndex(options, opt => opt == result);
    if (selectedIndex >= 0)
    {
        // Set manual video recording mode with selected format
        camera.VideoQuality = VideoQuality.Manual;
        camera.VideoFormatIndex = selectedIndex;
        
        var selectedFormat = formats[selectedIndex];
        await DisplayAlert("Format Selected", 
            $"Selected: {selectedFormat.Description}", "OK");
    }
}
```

#### Video Recording with Audio Control

SkiaCamera provides **granular audio control** for video recordings:

```csharp
// Control audio recording
camera.RecordAudio = false;  // Record silent videos (default)
camera.RecordAudio = true;   // Record videos with audio

// Audio control is cross-platform and applies to all video recordings
```

**Audio Recording Property:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RecordAudio` | `bool` | `false` | Whether to include audio in video recordings |

**XAML Binding:**
```xml
<camera:SkiaCamera
    x:Name="CameraControl"
    RecordAudio="{Binding RecordWithAudio}"
    VideoQuality="High" />
```

**Platform Implementation:**
- **Android**: Conditional MediaRecorder audio source and encoder setup
- **iOS/macOS**: Conditional AVCaptureDeviceInput for audio with proper cleanup
- **Windows**: MediaEncodingProfile audio removal when disabled

#### Capture Video Flow (Advanced)

SkiaCamera provides a **frame-by-frame video recording system** with real-time processing capabilities through `UseCaptureVideoFlow`. This allows you to process each camera frame before encoding, enabling watermarks, overlays, filters, and custom effects applied directly to the video output.

**Key Features:**
- **Real-time frame processing** with GPU acceleration
- **Custom drawing** on each frame via `FrameProcessor` callback
- **Dual recording modes**: Native recording (default) vs. Capture flow (frame-by-frame)
- **Cross-platform support** (Windows, Android, iOS/macOS)
- **Hardware encoding** when available
- **Rotation locking** during recording for consistent output

**Basic Usage:**

```csharp
var camera = new SkiaCamera
{
    // Enable capture video flow
    UseCaptureVideoFlow = true,

    // Standard video properties work the same
    VideoQuality = VideoQuality.High,
    RecordAudio = true,

    // Frame processor callback for custom rendering
    FrameProcessor = (frame) =>
    {
        // Draw watermark on each video frame
        using var paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(128),
            TextSize = 48,
            IsAntialias = true
        };

        frame.Canvas.DrawText("LIVE", 50, 100, paint);
        frame.Canvas.DrawText($"{frame.Time:mm\\:ss}", 50, 160, paint);

        // Draw timestamp
        var timestamp = frame.Time.ToString(@"hh\:mm\:ss");
        frame.Canvas.DrawText(timestamp, 50, 180, paint);
    }
};

// Same video recording API as native mode
await camera.StartVideoRecording();
await Task.Delay(10000); // Record for 10 seconds
await camera.StopVideoRecording();
```

**DrawableFrame Properties:**

The `FrameProcessor` callback receives a `DrawableFrame` object with:

| Property | Type | Description |
|----------|------|-------------|
| `Canvas` | `SKCanvas` | SkiaSharp canvas for drawing on the frame |
| `Width` | `int` | Frame width in pixels |
| `Height` | `int` | Frame height in pixels |
| `Time` | `TimeSpan` | Elapsed time since recording started |

**Capture Video Flow Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `UseCaptureVideoFlow` | `bool` | `false` | Enable frame-by-frame capture mode |
| `FrameProcessor` | `Action<DrawableFrame>` | `null` | Callback for processing each frame |
| `RecordAudio` | `bool` | `false` | Include audio in recording |
| `VideoQuality` | `VideoQuality` | `Standard` | Video quality preset |
| `VideoFormatIndex` | `int` | `0` | Manual format selection (when VideoQuality = Manual) |

**Advanced Example: Multi-Layer Overlay**

```csharp
camera.FrameProcessor = (frame) =>
{
    var canvas = frame.Canvas;
    var width = frame.Width;
    var height = frame.Height;
    var time = frame.Time;

    // Draw semi-transparent overlay rectangle
    using var rectPaint = new SKPaint
    {
        Color = SKColors.Black.WithAlpha(100),
        Style = SKPaintStyle.Fill
    };
    canvas.DrawRect(new SKRect(0, height - 120, width, height), rectPaint);

    // Draw recording indicator
    using var circlePaint = new SKPaint { Color = SKColors.Red };
    canvas.DrawCircle(30, height - 80, 10, circlePaint);

    // Draw timestamp
    using var textPaint = new SKPaint
    {
        Color = SKColors.White,
        TextSize = 36,
        IsAntialias = true,
        Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
    };
    canvas.DrawText($"REC {time:hh\\:mm\\:ss}", 60, height - 65, textPaint);

    // Draw custom logo/watermark (from SKBitmap)
    if (_watermarkBitmap != null)
    {
        var logoRect = new SKRect(width - 200, 20, width - 20, 100);
        canvas.DrawBitmap(_watermarkBitmap, logoRect);
    }
};
```

**Platform Implementation Details:**

| Platform | Encoder | GPU Support | Hardware Encoding |
|----------|---------|-------------|-------------------|
| **Windows** | Media Foundation (H.264/HEVC) | ✅ D3D11 textures | ✅ Hardware MFT |
| **Android** | MediaCodec (H.264/HEVC) | ✅ Surface input | ✅ Hardware codec |
| **iOS/macOS** | AVAssetWriter (H.264/HEVC) | ✅ Metal textures | ✅ Hardware encoder |

**Performance Considerations:**

- **GPU-first rendering**: All frame composition happens on GPU when possible
- **Zero-copy encoding**: Frames passed directly to hardware encoder without CPU readback
- **Frame dropping**: Automatic frame dropping when processing can't keep up with camera FPS
- **Memory efficiency**: Single-frame-in-flight policy prevents memory bloat
- **Rotation locking**: Device rotation is locked during recording for consistent output

**Use Cases:**

1. **Watermarking**: Apply logo/branding to videos
2. **Telemetry overlays**: Display real-time data (speed, location, sensor readings)
3. **Filters/Effects**: Apply custom color grading, vintage effects, artistic filters
4. **Annotations**: Draw shapes, arrows, text annotations
5. **Multi-source composition**: Combine camera with other visual elements
6. **Diagnostics**: Add performance metrics, debug information

**Important Notes:**

- When `UseCaptureVideoFlow = true`, the frame processor MUST be set for recording to work
- Frame processing happens in real-time at the target video FPS (typically 30fps)
- Keep `FrameProcessor` code efficient to avoid frame drops
- The same `StartVideoRecording()` / `StopVideoRecording()` API works for both modes
- All existing video events (`VideoRecordingSuccess`, `VideoRecordingFailed`, `VideoRecordingProgress`) work the same
- Preview continues uninterrupted during recording in both modes

#### Video Recording UI Integration

```csharp
// Complete video recording button implementation with audio control
private SkiaButton _videoRecordButton;
private SkiaButton _audioToggleButton;

var recordButton = new SkiaButton("🎥 Record")
{
    BackgroundColor = Colors.Purple,
    TextColor = Colors.White,
    CornerRadius = 8,
    UseCache = SkiaCacheType.Image
}
.Assign(out _videoRecordButton)
.OnTapped(async me => { await ToggleVideoRecording(); })
.ObserveProperty(CameraControl, nameof(CameraControl.IsRecordingVideo), me =>
{
    me.Text = CameraControl.IsRecordingVideo ? "🛑 Stop" : "🎥 Record";
    me.BackgroundColor = CameraControl.IsRecordingVideo ? Colors.Red : Colors.Purple;
});

// Audio toggle button
var audioButton = new SkiaButton("🔇 Silent")
{
    BackgroundColor = Colors.Gray,
    TextColor = Colors.White,
    CornerRadius = 8
}
.Assign(out _audioToggleButton)
.OnTapped(me => { ToggleAudioRecording(); })
.ObserveProperty(CameraControl, nameof(CameraControl.RecordAudio), me =>
{
    me.Text = CameraControl.RecordAudio ? "🎤 Audio" : "🔇 Silent";
    me.BackgroundColor = CameraControl.RecordAudio ? Colors.Green : Colors.Gray;
});

private void ToggleAudioRecording()
{
    // Only allow changing audio setting when not recording
    if (!CameraControl.IsRecordingVideo)
    {
        CameraControl.RecordAudio = !CameraControl.RecordAudio;
    }
}

private async Task ToggleVideoRecording()
{
    if (CameraControl.State != CameraState.On)
        return;

    try
    {
        if (CameraControl.IsRecordingVideo)
        {
            await CameraControl.StopVideoRecording();
        }
        else
        {
            await CameraControl.StartVideoRecording();
        }
    }
    catch (NotImplementedException ex)
    {
        await DisplayAlert("Not Implemented", 
            $"Video recording is not yet implemented for this platform:\n{ex.Message}", "OK");
    }
    catch (Exception ex)
    {
        await DisplayAlert("Video Recording Error", $"Error: {ex.Message}", "OK");
    }
}
```

#### Video Format Information

```csharp
// VideoFormat provides detailed information about video recording formats
var currentFormat = camera.GetCurrentVideoFormat();
if (currentFormat != null)
{
    Console.WriteLine($"Resolution: {currentFormat.Width}x{currentFormat.Height}");
    Console.WriteLine($"Frame Rate: {currentFormat.FrameRate} fps");
    Console.WriteLine($"Codec: {currentFormat.Codec}");
    Console.WriteLine($"Bit Rate: {currentFormat.BitRate} bps");
    Console.WriteLine($"Aspect Ratio: {currentFormat.AspectRatio:F2}");
    Console.WriteLine($"Description: {currentFormat.Description}");
}

// Browse all available formats
var formats = await camera.GetAvailableVideoFormatsAsync();
foreach (var format in formats)
{
    Console.WriteLine($"{format.Width}x{format.Height} @ {format.FrameRate}fps");
    Console.WriteLine($"Codec: {format.Codec}, BitRate: {format.BitRate}");
    Console.WriteLine($"Description: {format.Description}");
}
```

#### Video Recording Architecture

SkiaCamera implements **non-blocking video recording** that preserves preview performance:

**🎥 Video Recording Channel**
- Records video using platform-native APIs
- **Independent of preview stream** - no performance impact on live camera feed
- Uses hardware-accelerated encoding when available
- **Property**: `IsRecordingVideo` (read-only status)
- **Events**: Success/Failed/Progress for comprehensive monitoring

**Platform Implementation:**

| Platform | Recording API | Hardware Acceleration | Background Recording |
|----------|---------------|----------------------|---------------------|
| **Android** | `MediaRecorder` | ✅ Hardware encoding | ✅ Continues in background |
| **iOS/macOS** | `AVCaptureMovieFileOutput` | ✅ Hardware encoding | ✅ Continues in background |
| **Windows** | `MediaCapture.StartRecordToStreamAsync` | ✅ Hardware encoding | ✅ Continues in background |

#### Performance Considerations

Video recording is designed to have **zero impact on preview performance**:

- **Separate recording session**: Video recording uses a separate output stream from preview
- **Hardware acceleration**: Platform-native encoders handle compression
- **Asynchronous operations**: All recording operations are non-blocking
- **Memory efficient**: Direct stream-to-file recording, no memory buffering

#### Video Recording Events

```csharp
// Comprehensive event handling for video recording
camera.VideoRecordingSuccess += (sender, video) =>
{
    MainThread.BeginInvokeOnMainThread(async () =>
    {
        // Video recording completed
        var message = $"Video recorded successfully!\n" +
                     $"Duration: {video.Duration:mm\\:ss}\n" +
                     $"File: {Path.GetFileName(video.FilePath)}\n" +
                     $"Size: {video.FileSizeBytes / (1024 * 1024):F1} MB";
        
        await DisplayAlert("Recording Complete", message, "OK");
        
        // Save to gallery using consistent API
        var galleryPath = await camera.MoveVideoToGalleryAsync(video, "MyApp Videos");
        if (!string.IsNullOrEmpty(galleryPath))
        {
            await DisplayAlert("Saved", $"Video saved to gallery:\n{galleryPath}", "OK");
        }
    });
};

camera.VideoRecordingFailed += (sender, exception) =>
{
    MainThread.BeginInvokeOnMainThread(async () =>
    {
        await DisplayAlert("Recording Failed", 
            $"Video recording failed: {exception.Message}", "OK");
    });
};

camera.VideoRecordingProgress += (sender, duration) =>
{
    MainThread.BeginInvokeOnMainThread(() =>
    {
        // Update recording duration display
        RecordingLabel.Text = $"🔴 REC {duration:mm\\:ss}";
        
        // Optional: Update progress bar for timed recordings
        if (MaxRecordingDuration > TimeSpan.Zero)
        {
            var progress = duration.TotalSeconds / MaxRecordingDuration.TotalSeconds;
            RecordingProgressBar.Progress = Math.Min(progress, 1.0);
        }
    });
};
```

#### Video Data Classes

```csharp
// Video format specification
public class VideoFormat
{
    public int Width { get; set; }              // Video width in pixels
    public int Height { get; set; }             // Video height in pixels
    public double FrameRate { get; set; }       // Frames per second
    public string Codec { get; set; }           // Video codec (H.264, H.265, etc.)
    public long BitRate { get; set; }           // Bit rate in bits per second
    public double AspectRatio => (double)Width / Height; // Aspect ratio
    public string Description { get; }          // Human-readable description
}

// Captured video information
public class CapturedVideo
{
    public string FilePath { get; set; }        // Path to recorded video file
    public TimeSpan Duration { get; set; }      // Video duration
    public VideoFormat Format { get; set; }     // Video format used
    public CameraPosition Facing { get; set; }  // Camera that recorded video
    public DateTime Time { get; set; }          // Recording timestamp
    public Dictionary<string, object> Metadata { get; set; } // Additional metadata
    public long FileSizeBytes { get; set; }     // File size in bytes
}

// Video quality presets
public enum VideoQuality
{
    Low,        // 720p, optimized for size and battery
    Standard,   // 1080p, balanced quality
    High,       // 1080p, higher bitrate
    Ultra,      // 4K if available, maximum quality
    Manual      // Use VideoFormatIndex for custom format
}
```

#### Video Recording Methods

```csharp
// Video recording control methods
public async Task StartVideoRecording()             // Start recording video
public async Task StopVideoRecording()              // Stop recording video
public bool CanRecordVideo()                        // Check if recording is supported

// Video format management
public async Task<List<VideoFormat>> GetAvailableVideoFormatsAsync()  // Get available formats
public VideoFormat GetCurrentVideoFormat()         // Get currently selected format

// Video file management
public async Task<string> MoveVideoToGalleryAsync(CapturedVideo video, string album = null, bool deleteOriginal = true)
```

#### Complete Video Recording Example

```csharp
public partial class VideoRecordingPage : ContentPage
{
    private SkiaCamera _camera;
    private SkiaButton _recordButton;
    private SkiaLabel _statusLabel;
    private DateTime _recordingStartTime;

    private void SetupVideoRecording()
    {
        // Setup camera with video recording
        _camera = new SkiaCamera
        {
            Facing = CameraPosition.Default,
            VideoQuality = VideoQuality.High,
            IsOn = true
        };

        // Subscribe to video events
        _camera.VideoRecordingSuccess += OnVideoSuccess;
        _camera.VideoRecordingFailed += OnVideoFailed;
        _camera.VideoRecordingProgress += OnVideoProgress;

        // Create recording button
        _recordButton = new SkiaButton("🎥 Record")
        {
            BackgroundColor = Colors.Red,
            TextColor = Colors.White
        };
        _recordButton.Clicked += OnRecordButtonClicked;

        // Create status label
        _statusLabel = new SkiaLabel("Ready to record")
        {
            TextColor = Colors.White,
            FontSize = 16
        };
    }

    private async void OnRecordButtonClicked(object sender, EventArgs e)
    {
        if (!_camera.CanRecordVideo())
        {
            await DisplayAlert("Not Supported", "Video recording not available", "OK");
            return;
        }

        try
        {
            if (_camera.IsRecordingVideo)
            {
                await _camera.StopVideoRecording();
            }
            else
            {
                _recordingStartTime = DateTime.Now;
                await _camera.StartVideoRecording();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Recording error: {ex.Message}", "OK");
        }
    }

    private async void OnVideoSuccess(object sender, CapturedVideo video)
    {
        var duration = video.Duration;
        var fileSize = video.FileSizeBytes / (1024 * 1024); // MB
        
        var message = $"Video recorded!\n" +
                     $"Duration: {duration:mm\\:ss}\n" +
                     $"Size: {fileSize:F1} MB\n" +
                     $"Format: {video.Format?.Description ?? "Unknown"}";

        await DisplayAlert("Success", message, "Save to Gallery", "OK");
        
        // Save to gallery using consistent API
        var galleryPath = await _camera.MoveVideoToGalleryAsync(video, "My Videos");
        if (!string.IsNullOrEmpty(galleryPath))
        {
            _statusLabel.Text = "Video saved to gallery";
        }
    }

    private async void OnVideoFailed(object sender, Exception ex)
    {
        await DisplayAlert("Recording Failed", ex.Message, "OK");
        _recordButton.Text = "🎥 Record";
        _recordButton.BackgroundColor = Colors.Red;
        _statusLabel.Text = "Recording failed";
    }

    private void OnVideoProgress(object sender, TimeSpan duration)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _recordButton.Text = $"🛑 Stop ({duration:mm\\:ss})";
            _recordButton.BackgroundColor = Colors.DarkRed;
            _statusLabel.Text = $"Recording... {duration:mm\\:ss}";
        });
    }
}
```

### 🚧 ToDo
- [ ] **Manual camera controls** (focus, exposure, ISO, white balance) - partially implemented, need to expose more controls  
- [ ] **Camera capability detection** (zoom ranges, supported formats) - need to combine available cameras list with camera units list and expose
- [ ] **Video recording platform implementations** - API complete, platform-specific recording implementations needed
- [ ] **Preview format customization** - currently auto-selected to match capture aspect ratio


## References

iOS: 
* [Manual Camera Controls in Xamarin.iOS](https://github.com/MicrosoftDocs/xamarin-docs/blob/0506e3bf14b520776fc7d33781f89069bbc57138/docs/ios/user-interface/controls/intro-to-manual-camera-controls.md) by David Britch

