# SkiaCamera

A powerful .NET MAUI Camera control, rendered with DrawnUI for for .NET MAUI.
Use inside any MAUI app just by wrapping with a `Canvas`. Use as Camera or a standalone Audio recorder.
 
* Full featured camera control for iOS, MacCatalyst, Android and Windows.
* Draw camera preview with effects and overlays, position UI how You feel it.
* Process live video preview in realtime, use for AI/ML and other calls.
* Post-process taken photos
* Process frames being recorded in realtime, save encoded video with your effects/overlays applied without post-processing.
* Change and visualise audio sample in realtime before encoding.
* Use [pre-recording mode](PreRecording.md) to capture few seconds before the live recording button was pressed.
* Abort recording if needed without saving anything.
* Inject custom EXIF, save GPS location to both photos and video.

## Sample Apps:

- [CameraTests](https://github.com/taublast/DrawnUi/tree/main/src/Maui/Samples/Camera) - Basic usage of the control.
- [BPM Tempo Master](...) - Audio visualizer and BPM detector using SkiaCamera's audio monitoring capabilities.
- [Filters Camera](https://github.com/taublast/ShadersCamera) - Open source piblished still photo-camera with realtime preview and final photo filters, implemented with SKSL shaders.

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

You need to set up permissions for camera, microphone (if you want to record video with sound) and storage/gallery access.  
Depending on your use-case you can request just camera permission for preview, or require access to gallery, microphone etc.  
When saving feed to gallery, and not just temp folder, you usually must be able to read your files back from gallery to show to user at a later time.

### Windows:

No specific setup needed.

### Apple:

Put this inside the file `Platforms/iOS/Info.plist` and `Platforms/MacCatalyst/Info.plist` inside the `<dict>` tag, remove those you might not need:

```xml
  <key>NSCameraUsageDescription</key>
    <string>This app uses camera so You could take photos</string>	
    <key>NSPhotoLibraryAddUsageDescription</key>
    <string>We need access to the library to save photos</string>
    <key>NSPhotoLibraryUsageDescription</key>
    <string>We need access to the library to save photos</string>
```

If you want to save video with audio:

```xml
  <key>NSCameraUsageDescription</key>
    <key>NSMicrophoneUsageDescription</key>
    <string>In case You want to save videos with sound</string>
```

If you want to geo-tag photos and videos (get and save GPS location metadata) add this:

```xml
	<key>NSLocationWhenInUseUsageDescription</key>
	<string>In case You want to be able to geotag taken photos and videos</string>
```

### Android

Put this inside the file `Platforms/Android/AndroidManifest.xml` inside the `<manifest>` tag:

```xml
    <!--for camera and gallery access-->
    <uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
    <uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" android:maxSdkVersion="32" />
    <uses-permission android:name="android.permission.READ_MEDIA_VIDEO" />
    <uses-permission android:name="android.permission.READ_MEDIA_IMAGES" />
    <uses-permission android:name="android.permission.READ_MEDIA_AUDIO" />
    <uses-permission android:name="android.permission.CAMERA" />
```

If you want to save video with audio:

```xml
    <uses-permission android:name="android.permission.RECORD_AUDIO" />
```

If you want to geo-tag photos and videos (get and save GPS location metadata) add this:

```xml
  <!--geotag photos and videos-->
  <uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
```

#### FileProvider Setup (only if you want to use OpenFileInGallery that uses gallery app)

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

### Request and Check Permissions at run time

SkiaCamera has a powerful built-in permission handling. When you set `IsOn = true`, it automatically checks and requests necessary permissions defined by `NeedPermissionsSet` property flags.  
Depending on your use-case you can request either just Camera permission for preview, or require access to gallery, microphone etc.  For a quick and lazy approach, simply set `NeedPermissionsSet` to your desired permissions before turning on the camera.    

You can use additional methods to building your own permissions flows, pass appropriate `NeedPermissions` flags to them:
* `CheckPermissions` - request missing permissions with callbacks.
* `CheckPermissionsGranted` - check if permissions are already granted without requesting, uses callbacks.
* `RequestPermissionsAsync` - async Task to request missing permissions and get a final `bool` result. 
* `RequestPermissionsGrantedAsync` - async Task to check permissions without reqiesting, returns `true` only if all permissions were granted.


## Usage Guide

## Usage Guide Summary

| # | Section | Description |
|---|---------|-------------|
| 1 | Declaration / Setup | XAML and code-behind setup |
| 2 | Essential Properties | Key properties reference |
| 3 | Camera Lifecycle Management | `IsOn`, `Start()`, global instance management |
| 4 | Flash Control | Preview torch and capture flash modes |
| 5 | Capture Format Selection | Quality presets, manual format, preview sync |
| 5 | Camera Selection | Front/back/manual camera switching |
| 5 | Camera Information Class | `CameraInfo` data class |
| 6 | Channel 2: Captured Photo Processing | `CaptureSuccess` event, `TakePicture()` |
| 6 | Opening Files in Gallery | `OpenFileInGallery()` |
| 7 | Real-Time Effects & Custom Shaders | Built-in effects, SKSL shaders |
| 8 | Zoom Control | Manual zoom, pinch-to-zoom |
| 9 | Camera State Management | `StateChanged` event, `CameraState` |
| 10 | **Live Processing: FrameProcessor & PreviewProcessor** | Drawing overlays on preview and recorded video, `NewPreviewSet` for AI/ML |
| 11 | Permission Handling | `NeedPermissions` flags, `CheckPermissions()`, `CheckPermissionsGranted()`, async helpers |
| 12 | Complete MVVM Example | Full ViewModel + Page example |

Also covered:

| Section | Description |
|---------|-------------|
| **Video Recording** | Basic recording, format selection, audio control, pre-recording, capture video flow, real-time audio processing |
| **GPS & Video Metadata** | GPS in photos (EXIF) and videos (MP4 ©xyz), video metadata injection (author, camera, date via `CapturedVideo.Meta`), `RefreshGpsLocation()`, direct file injection |
| **AudioSampleConverter** | PCM16 audio preprocessing (downmix, resample, silence gate) for speech-to-text APIs |
| **API Reference** | Core properties, methods, events, data classes, enums |
| **Troubleshooting** | Common issues, debug tips, platform-specific notes |

### 1. Declaration / Setup

For installation please see Installation section earlier in this document. Then you would be able to consume camera in your app.

The `Canvas` that would contain `SkiaCamera` *must* have its property `RenderingMode = RenderingModeType.Accelerated`:

#### XAML

```xml
xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw"
xmlns:camera="clr-namespace:DrawnUi.Camera;assembly=DrawnUi.Maui.Camera"
```

```xml
    <draw:Canvas
        HorizontalOptions="Fill"
        VerticalOptions="Fill"
        Gestures="Lock"
        RenderingMode = "Accelerated">

<camera:SkiaCamera
    x:Name="CameraControl"
    BackgroundColor="Black"
    PhotoQuality="Medium"
    Facing="Default"
    HorizontalOptions="Fill"
    VerticalOptions="Fill"
    ZoomLimitMax="10"
    ZoomLimitMin="1" />

    </draw:Canvas>
```

#### Code-Behind

Example for use inside an already created `Canvas`:

```csharp
var camera = new SkiaCamera
{
    BackgroundColor = Colors.Black,
    PhotoQuality = CaptureQuality.Medium,
    Facing = CameraPosition.Default,
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    ZoomLimitMax = 10,
    ZoomLimitMin = 1
};
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

#### Global Instance Management

SkiaCamera maintains a static list of all active instances to prevent resource conflicts, especially on Windows where hardware release can be slow.

```csharp
// Access all tracked camera instances
var activeCameras = SkiaCamera.Instances;

// Stop all active cameras globally
await SkiaCamera.StopAllAsync();
```

**Automatic Conflict Resolution**:
When a new camera starts, it automatically checks `SkiaCamera.Instances` for other active cameras. If another camera is found to be busy or stopping (common during Hot Reload or rapid page navigation), the new instance will wait until the hardware is fully released before attempting to initialize. This prevents "Zombie" camera states and FPS drops on Windows.

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

### 10. Live Processing: FrameProcessor & PreviewProcessor

SkiaCamera provides **two drawing callbacks** for real-time overlay rendering, plus an **event** for read-only frame analysis:

| Callback / Event | Type | When It Fires | Use Case |
|------------------|------|---------------|----------|
| `FrameProcessor` | `Action<DrawableFrame>` | Each frame being **encoded to video** | Watermarks, telemetry, overlays baked into recorded video |
| `PreviewProcessor` | `Action<DrawableFrame>` | Each **preview** frame before display | Show overlays on live preview (e.g., gauges, guides) |
| `NewPreviewSet` | `EventHandler<LoadedImageSource>` | Each preview frame after display | Read-only AI/ML analysis, face detection, QR scanning |

> **Key Insight**: `FrameProcessor` draws on what gets **recorded**. `PreviewProcessor` draws on what the user **sees**. `NewPreviewSet` lets you **read** preview frames without drawing. All three are independent.

#### FrameProcessor (Video Recording Overlay)

Draws on each frame being encoded to the video file. Requires `UseRealtimeVideoProcessing = true`. Scale is always 1.0 (full recording resolution).

```csharp
camera.UseRealtimeVideoProcessing = true;
camera.FrameProcessor = (frame) =>
{
    // This draws on the RECORDED video
    using var paint = new SKPaint
    {
        Color = SKColors.White.WithAlpha(128),
        TextSize = 48 * frame.Scale, // Scale=1.0 for recording frames
        IsAntialias = true
    };
    frame.Canvas.DrawText($"REC {frame.Time:mm\\:ss}", 50, 100, paint);
};
```

#### PreviewProcessor (Live Preview Overlay)

Draws on each preview frame before it is displayed to the user. Uses `PreviewScale` so overlay sizing matches the recording. When `UseRecordingFramesForPreview = true` (default) and recording is active, `PreviewProcessor` is **automatically skipped** because the encoder's processed frames (with `FrameProcessor` overlay already baked in) are used as preview.

```csharp
camera.PreviewProcessor = (frame) =>
{
    // This draws on the LIVE PREVIEW the user sees
    // frame.Scale = PreviewScale (e.g., 0.3 if preview is smaller than recording)
    // frame.IsPreview = true
    using var paint = new SKPaint
    {
        Color = SKColors.LimeGreen,
        TextSize = 48 * frame.Scale, // Scale down to match preview resolution
        IsAntialias = true
    };
    frame.Canvas.DrawText("READY", 50 * frame.Scale, 100 * frame.Scale, paint);
};
```

#### Using Both Together

A common pattern is to write a single drawing method and use `frame.Scale` and `frame.IsPreview` to handle both callbacks:

```csharp
// Single drawing method for both recording and preview
void DrawOverlay(DrawableFrame frame)
{
    var s = frame.Scale; // 1.0 for recording, PreviewScale for preview
    using var paint = new SKPaint
    {
        Color = SKColors.White,
        TextSize = 48 * s,
        IsAntialias = true
    };

    frame.Canvas.DrawText($"{frame.Time:mm\\:ss}", 50 * s, 100 * s, paint);

    if (!frame.IsPreview)
    {
        // Extra detail only in the recorded video (e.g., high-res watermark)
        frame.Canvas.DrawText("WATERMARK", 50 * s, 160 * s, paint);
    }
}

// Assign both callbacks
camera.UseRealtimeVideoProcessing = true;
camera.FrameProcessor = DrawOverlay;
camera.PreviewProcessor = DrawOverlay;
```

#### DrawableFrame Properties

| Property | Type | Description |
|----------|------|-------------|
| `Canvas` | `SKCanvas` | SkiaSharp canvas for drawing on the frame |
| `Width` | `int` | Frame width in pixels |
| `Height` | `int` | Frame height in pixels |
| `Time` | `TimeSpan` | Elapsed time since recording started |
| `IsPreview` | `bool` | `true` for preview frames, `false` for recording frames |
| `Scale` | `float` | 1.0 for recording frames; `PreviewScale` for preview frames (e.g., 0.3 if preview is smaller) |

#### Related Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `UseRealtimeVideoProcessing` | `bool` | `false` | Enable frame-by-frame capture mode (required for `FrameProcessor`) |
| `FrameProcessor` | `Action<DrawableFrame>` | `null` | Callback for processing each recorded video frame |
| `PreviewProcessor` | `Action<DrawableFrame>` | `null` | Callback for processing each preview frame before display |
| `UseRecordingFramesForPreview` | `bool` | `true` | Use encoder output as preview during recording (skips `PreviewProcessor`) |
| `PreviewScale` | `float` | `1.0` | Scale of preview relative to recording resolution (read-only) |

#### NewPreviewSet Event (Read-Only Analysis)

For AI/ML processing where you need to **read** preview frames without drawing on them, use the `NewPreviewSet` event:

```csharp
camera.NewPreviewSet += OnNewPreviewFrame;

private void OnNewPreviewFrame(object sender, LoadedImageSource source)
{
    // Fires for every preview frame (30-60 FPS)
    // Read-only — does not affect what user sees or what gets recorded
    Task.Run(() => ProcessPreviewFrameForAI(source));
}

private void ProcessPreviewFrameForAI(LoadedImageSource source)
{
    var faces = DetectFaces(source.Image);
    var objects = RecognizeObjects(source.Image);

    MainThread.BeginInvokeOnMainThread(() =>
    {
        UpdateOverlayWithDetections(faces, objects);
    });
}
```

### 11. Permission Handling

All permission methods are **static** and operate on the main thread internally, so they can be called from any thread.

#### `NeedPermissions` flags enum

```csharp
[Flags]
public enum NeedPermissions
{
    Camera     = 1,   // Camera access
    Gallery    = 2,   // Photo library / storage read-write
    Microphone = 4,   // Audio recording
    Location   = 8    // GPS location (for geotagging)
}
```

Combine flags with `|`:

```csharp
var flags = NeedPermissions.Camera | NeedPermissions.Gallery | NeedPermissions.Microphone;
```

---

#### `CheckPermissions` — request permissions (shows system dialogs)

Checks the specified permissions and **requests any that are not yet granted**, showing the OS permission dialog to the user.

```csharp
SkiaCamera.CheckPermissions(
    granted:    () => camera.IsOn = true,
    notGranted: () => ShowPermissionsError(),
    request:    NeedPermissions.Camera | NeedPermissions.Gallery | NeedPermissions.Microphone);
```

Async wrapper — awaitable `Task<bool>`:

```csharp
bool ok = await SkiaCamera.RequestPermissionsAsync(
    NeedPermissions.Camera | NeedPermissions.Gallery | NeedPermissions.Microphone);

if (ok)
    camera.IsOn = true;
```

---

#### `CheckPermissionsGranted` — silent status check (no dialogs)

Checks the specified permissions **without prompting the user**. Use this to probe the current grant status silently, e.g. to decide whether to show an onboarding flow.

```csharp
SkiaCamera.CheckPermissionsGranted(
    granted:    () => camera.IsOn = true,
    notGranted: () => ShowOnboardingScreen(),
    request:    NeedPermissions.Camera | NeedPermissions.Gallery | NeedPermissions.Microphone);
```

Async wrapper — awaitable `Task<bool>`:

```csharp
bool alreadyGranted = await SkiaCamera.RequestPermissionsGrantedAsync(
    NeedPermissions.Camera | NeedPermissions.Gallery | NeedPermissions.Microphone);

if (!alreadyGranted)
    ShowOnboardingScreen();
```

---

#### `NeedPermissionsSet` — instance property

Controls which permissions are checked automatically when the camera turns on via `IsOn = true`. Defaults to `Camera | Gallery`.

```csharp
// Also require microphone (e.g. for video with audio)
camera.NeedPermissionsSet = NeedPermissions.Camera | NeedPermissions.Gallery | NeedPermissions.Microphone;
camera.IsOn = true;
```

---

#### Typical onboarding flow

```csharp
// 1. On page appear: silent probe
bool alreadyGranted = await SkiaCamera.RequestPermissionsGrantedAsync(
    NeedPermissions.Camera | NeedPermissions.Gallery | NeedPermissions.Microphone);

if (alreadyGranted)
{
    camera.IsOn = true;
}
else
{
    // 2. Show onboarding UI, then on user action:
    bool granted = await SkiaCamera.RequestPermissionsAsync(
        NeedPermissions.Camera | NeedPermissions.Gallery | NeedPermissions.Microphone);

    if (granted)
        camera.IsOn = true;
    else
        ShowOpenSettingsHint(); // direct user to app system settings
}
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
public bool IsRecording { get; }             // Recording state (read-only)
public VideoQuality VideoQuality { get; set; }   // Video quality preset
public int VideoFormatIndex { get; set; }         // Manual format index
public bool UseRealtimeVideoProcessing { get; set; }     // Enable frame-by-frame capture mode
public Action<DrawableFrame> FrameProcessor { get; set; } // Draw on each recorded video frame
public Action<DrawableFrame> PreviewProcessor { get; set; } // Draw on each preview frame before display
public bool UseRecordingFramesForPreview { get; set; }   // Use encoder output as preview during recording (default: true)
public float PreviewScale { get; }                       // Preview-to-recording scale factor (read-only)

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
public bool EnableAudioRecording { get; set; }             // Include audio in video recordings (default: false)
```

### Core Methods
```csharp
// Camera Management
public async Task<List<CameraInfo>> GetAvailableCamerasAsync()
public async Task<List<CameraInfo>> RefreshAvailableCamerasAsync()

// Permissions — request (shows OS dialogs)
public static void CheckPermissions(Action granted, Action notGranted, NeedPermissions request)
public static Task<bool> RequestPermissionsAsync(NeedPermissions request)

// Permissions — silent check (no dialogs)
public static void CheckPermissionsGranted(Action granted, Action notGranted, NeedPermissions request)
public static Task<bool> RequestPermissionsGrantedAsync(NeedPermissions request)

// Permissions — instance, controls which permissions are required on IsOn = true
public NeedPermissions NeedPermissionsSet { get; set; }  // default: Camera | Gallery

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
public event EventHandler<CapturedVideo> RecordingSuccess;
public event EventHandler<Exception> RecordingFailed;
public event EventHandler<TimeSpan> RecordingProgress;

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
    public Metadata Meta { get; set; }                // Video metadata (GPS, author, camera, date — auto-filled on save)
    public double? Latitude { get; set; }             // GPS latitude (set before save, or auto from InjectGpsLocation)
    public double? Longitude { get; set; }            // GPS longitude (set before save, or auto from InjectGpsLocation)
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
| **Camera not starting** | Missing permissions | Use `SkiaCamera.RequestPermissionsAsync()` or set `NeedPermissionsSet` |
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
7. **Handle permissions with `SkiaCamera.RequestPermissionsAsync()` (request) or `SkiaCamera.RequestPermissionsGrantedAsync()` (silent check)**
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
camera.RecordingSuccess += OnRecordingSuccess;
camera.RecordingFailed += OnRecordingFailed;
camera.RecordingProgress += OnRecordingProgress;

private async void OnRecordingSuccess(object sender, CapturedVideo video)
{
    // Video recording completed successfully
    var filePath = video.FilePath;
    var duration = video.Duration;
    var format = video.Format;
    
    // Save to gallery with consistent API (move instead of copy for performance)
    var galleryPath = await camera.MoveVideoToGalleryAsync(video, "MyApp");
}

private void OnRecordingFailed(object sender, Exception ex)
{
    await DisplayAlert("Recording Error", $"Failed to record video: {ex.Message}", "OK");
}

private void OnRecordingProgress(object sender, TimeSpan duration)
{
    // Update UI with recording progress
    RecordingTimeLabel.Text = $"Recording: {duration:mm\\:ss}";
}
```

#### Video Recording Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsRecording` | `bool` | `false` | Whether video recording is active (read-only) |
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

SkiaCamera provides **granular control over video preview, recording, and audio** through four independent properties:

```csharp
// Core control properties
camera.EnableVideoPreview = true;        // Initialize camera hardware, show video preview (default: true)
camera.EnableVideoRecording = true;               // Record video frames to output file (default: true)
camera.EnableAudioRecording = true;               // Capture audio to output file (default: false)
camera.EnableAudioMonitoring = false;    // Enable live audio preview/feedback (default: false)
```

**Control Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableVideoPreview` | `bool` | `true` | Display video preview UI (camera initializes if EnableVideoRecording=true regardless) |
| `EnableVideoRecording` | `bool` | `true` | Capture and encode video frames to file |
| `EnableAudioRecording` | `bool` | `false` | Capture and encode audio to file |
| `EnableAudioMonitoring` | `bool` | `false` | Enable live audio preview/feedback (e.g., for audio level meters) |

**Usage Scenarios:**

| Scenario | EnableVideoPreview | EnableVideoRecording | EnableAudioMonitoring | EnableAudioRecording | Output | Notes |
|----------|-------------------|-------------|----------------------|-------------|--------|-------|
| **Full video recording** | ✅ true | ✅ true | ✅ true | ✅ true | MP4/MOV with A/V | Standard video recording with preview and audio monitoring |
| **Headless video recording** | ❌ false | ✅ true | 🤷 optional | ✅ true | MP4/MOV with A/V | Record video without showing preview UI (camera still running) |
| **Video with silent monitoring** | ✅ true | ✅ true | ✅ true | ❌ false | MP4/MOV video only | Monitor audio levels without recording |
| **Silent video recording** | ✅ true | ✅ true | ❌ false | ❌ false | MP4/MOV video only | No audio capture or monitoring |
| **Preview only (no recording)** | ✅ true | ❌ false | ❌ false | ❌ false | None | Live camera preview, no recording |
| **Pure audio-only recorder** | ❌ false | ❌ false | 🤷 optional | ✅ true | M4A audio only | Audio recording without camera hardware |
| **Audio monitor (no recording)** | ❌ false | ❌ false | ✅ true | ❌ false | None | Audio level monitoring only |

**Key Features:**
- **Headless video recording** possible: set `EnableVideoPreview=false` to record video without showing UI
- **Audio monitoring** decoupled from recording (useful for audio level meters, live feedback)
- **Pure audio-only mode** when both `EnableVideoPreview=false` AND `EnableVideoRecording=false` (no camera hardware initialized)
- **Backward compatible**: default values match previous behavior
- Each property has a single, clear responsibility

**XAML Binding:**
```csharp
<camera:SkiaCamera
    x:Name="CameraControl"
    EnableVideoPreview="true"
    EnableVideoRecording="true"
    EnableAudioRecording="{Binding RecordWithAudio}"
    EnableAudioMonitoring="{Binding ShowAudioLevels}"
    VideoQuality="High" />
```

**Common Use Cases:**

```csharp
// 1. Standard video recording with audio
camera.EnableVideoPreview = true;
camera.EnableVideoRecording = true;
camera.EnableAudioRecording = true;
await camera.StartVideoRecording();

// 2. Audio-only recording (no camera initialization)
camera.EnableVideoPreview = false;  // Don't show preview
camera.EnableVideoRecording = false;         // Don't initialize camera
camera.EnableAudioRecording = true;
await camera.StartVideoRecording();  // Records M4A audio file

// 3. Headless video recording (no preview UI, but camera running)
camera.EnableVideoPreview = false;  // Hide preview UI
camera.EnableVideoRecording = true;          // Camera still initializes and records
camera.EnableAudioRecording = true;
await camera.StartVideoRecording();  // Records video without showing preview

// 4. Video recording with audio level monitoring (but no audio in output)
camera.EnableVideoPreview = true;
camera.EnableVideoRecording = true;
camera.EnableAudioRecording = false;           // Don't record audio to file
camera.EnableAudioMonitoring = true;  // But show audio levels in UI
await camera.StartVideoRecording();

// 5. Silent video recording
camera.EnableVideoPreview = true;
camera.EnableVideoRecording = true;
camera.EnableAudioRecording = false;
camera.EnableAudioMonitoring = false;
await camera.StartVideoRecording();
```

**Audio-only recording output:**

Audio-only recordings produce `.m4a` files. The recorded file path is delivered via the `RecordingSuccess` callback (same as video recordings), giving you direct access to the file for playback, upload, or any custom handling.

If you use `MoveVideoToGalleryAsync` to save the audio file to a user-accessible location, the destination differs per platform:

| Platform | Destination | Where the user finds it |
|----------|-------------|------------------------|
| **Windows** | `Videos/{album}` folder | **File Explorer** → **Videos** → album folder |
| **Android** | `Music/{album}` via MediaStore | **Files** app or file manager → **Music** → album folder. Saved to `MediaStore.Audio`, not visible in Gallery/Photos (which only shows photos and videos) |
| **iOS** | App's **Documents** folder | **Files** app → **On My iPhone** → app name → album folder. iOS Photos library does not support audio-only assets. Requires Info.plist keys (see below) |

**iOS Info.plist requirement for audio file access:**

For the app's Documents folder to be visible in the iOS Files app, add these keys to `Platforms/iOS/Info.plist` inside the `<dict>` tag:

```xml
<key>UIFileSharingEnabled</key>
<true/>
<key>LSSupportsOpeningDocumentsInPlace</key>
<true/>
```

> **Note:** If you add these keys to an already-installed app, you must **delete the app** from the device and reinstall for the changes to take effect.

**Validation Rules:**
- ⚠️ `EnableVideoRecording=false` AND `EnableAudioRecording=false`: Throws `InvalidOperationException` (nothing to record)
- ⚠️ Pure audio-only mode (no camera): Requires `EnableVideoPreview=false` AND `EnableVideoRecording=false`
- Audio monitoring can be enabled independently in any mode

**Platform Implementation:**
- **Android**: Conditional MediaRecorder audio source and encoder setup
- **iOS/macOS**: Conditional AVCaptureDeviceInput for audio with proper cleanup
- **Windows**: MediaEncodingProfile audio removal when disabled, AudioGraph for monitoring

#### Capture Video Flow (Advanced)

SkiaCamera provides a **frame-by-frame video recording system** with real-time processing capabilities through `UseRealtimeVideoProcessing`. This allows you to process each camera frame before encoding, enabling watermarks, overlays, filters, and custom effects applied directly to the video output.

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
    UseRealtimeVideoProcessing = true,

    // Standard video properties work the same
    VideoQuality = VideoQuality.High,
    EnableAudioRecording = true,

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

The `FrameProcessor` and `PreviewProcessor` callbacks receive a `DrawableFrame` object with:

| Property | Type | Description |
|----------|------|-------------|
| `Canvas` | `SKCanvas` | SkiaSharp canvas for drawing on the frame |
| `Width` | `int` | Frame width in pixels |
| `Height` | `int` | Frame height in pixels |
| `Time` | `TimeSpan` | Elapsed time since recording started |
| `IsPreview` | `bool` | `true` for preview frames (`PreviewProcessor`), `false` for recording frames (`FrameProcessor`) |
| `Scale` | `float` | 1.0 for recording frames; `PreviewScale` for preview frames — multiply your drawing coordinates/sizes by this |

**Capture Video Flow Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `UseRealtimeVideoProcessing` | `bool` | `false` | Enable frame-by-frame capture mode |
| `FrameProcessor` | `Action<DrawableFrame>` | `null` | Callback for processing each frame |
| `EnableAudioRecording` | `bool` | `false` | Include audio in recording |
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

- When `UseRealtimeVideoProcessing = true`, the frame processor MUST be set for recording to work
- Frame processing happens in real-time at the target video FPS (typically 30fps)
- Keep `FrameProcessor` code efficient to avoid frame drops
- The same `StartVideoRecording()` / `StopVideoRecording()` API works for both modes
- All existing video events (`RecordingSuccess`, `RecordingFailed`, `RecordingProgress`) work the same
- Preview continues uninterrupted during recording in both modes

#### Real-Time Audio Processing

SkiaCamera allows you to process audio samples in real-time during video recording by overriding the `WriteAudioSample` method. **This feature requires `UseRealtimeVideoProcessing = true`** to enable frame-by-frame processing mode.

**Key Features:**
- **Requires `UseRealtimeVideoProcessing = true`** - Part of the capture video flow
- **Synchronous processing** on audio capture thread
- **In-place modification** of audio data before encoding
- **Full PCM access** to raw audio samples
- **Cross-platform support** (Android, iOS/macOS)
- **Works with all recording modes** (live and pre-recording)

**Basic Usage:**

```csharp
public class MyCamera : SkiaCamera 
{
    public MyCamera()
    {
        // REQUIRED: Enable realtime video processing
        UseRealtimeVideoProcessing = true;
        EnableAudioRecording = true;
        
        FrameProcessor = (frame) =>
        {
            // Your video frame processing
        };
    }
    
    public override void WriteAudioSample(AudioSample sample)
    {
        // Process audio sample
        // sample.Data is byte[] containing PCM audio data
        // You can read and modify it in-place
        
        // Example: Apply volume adjustment
        AdjustVolume(sample.Data, sample.Channels, sample.BitDepth, 0.8f);
        
        // Example: Generate oscillograph data
        var peaks = CalculateAudioPeaks(sample.Data, sample.Channels, sample.BitDepth);
        UpdateOscillographVisualization(peaks);
        
        // MUST call base to record the modified audio
        base.WriteAudioSample(sample);
    }
}
```

**AudioSample Structure:**

| Property | Type | Description |
|----------|------|-------------|
| `Data` | `byte[]` | Raw PCM audio data (modify in-place for effects) |
| `TimestampNs` | `long` | Sample timestamp in nanoseconds |
| `SampleRate` | `int` | Sample rate (e.g., 44100 Hz) |
| `Channels` | `int` | Number of channels (1=mono, 2=stereo) |
| `BitDepth` | `AudioBitDepth` | Bit depth (Int16, Int24, Float32) |
| `SampleCount` | `int` | Number of samples in this chunk |
| `Timestamp` | `TimeSpan` | Converted timestamp as TimeSpan |

**Advanced Example: Live Audio Oscillograph**

```csharp
public class CameraWithOscillograph : SkiaCamera 
{
    private float[] _audioWaveform = new float[100]; // Visual buffer
    private readonly object _waveformLock = new object();
    
    public CameraWithOscillograph()
    {
        // REQUIRED: Enable realtime processing for audio/video sync
        UseRealtimeVideoProcessing = true;
        EnableAudioRecording = true;
        
        // Video frame processor - draw oscillograph overlay
        FrameProcessor = (frame) =>
        {
            DrawOscillograph(frame.Canvas, 50, frame.Height - 150, 
                           frame.Width - 100, 100);
        };
    }
    
    public override void WriteAudioSample(AudioSample sample)
    {
        // Extract waveform data for visualization (downsampled)
        lock (_waveformLock)
        {
            var stepSize = sample.SampleCount / _audioWaveform.Length;
            for (int i = 0; i < _audioWaveform.Length; i++)
            {
                var sampleIndex = i * stepSize * sample.Channels * 2; // *2 for 16-bit
                if (sampleIndex + 1 < sample.Data.Length)
                {
                    // Read as 16-bit PCM
                    short pcmValue = (short)(sample.Data[sampleIndex] | 
                                            (sample.Data[sampleIndex + 1] << 8));
                    _audioWaveform[i] = pcmValue / 32768f; // Normalize to -1..1
                }
            }
        }
        
        // Record the audio
        base.WriteAudioSample(sample);
    }
    
    // Draw oscillograph on video frames
    private void DrawOscillograph(SKCanvas canvas, float x, float y, 
                                  float width, float height)
    {
        lock (_waveformLock)
        {
            using var paint = new SKPaint
            {
                Color = SKColors.LimeGreen,
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            
            var centerY = y + height / 2;
            var path = new SKPath();
            
            for (int i = 0; i < _audioWaveform.Length; i++)
            {
                var px = x + (i / (float)_audioWaveform.Length) * width;
                var py = centerY + (_audioWaveform[i] * height / 2);
                
                if (i == 0)
                    path.MoveTo(px, py);
                else
                    path.LineTo(px, py);
            }
            
            canvas.DrawPath(path, paint);
            path.Dispose();
        }
    }
}
```

**Performance Considerations:**

- **Requires `UseRealtimeVideoProcessing = true`** - audio processing only works in capture mode
- **Runs on audio capture thread** - arrives every ~23ms at 44.1kHz (1024 sample chunks)
- **Keep processing under 10ms** to avoid buffer overruns and audio glitches
- **Avoid allocations** - reuse buffers, avoid creating objects in hot path
- **Thread safety** - use locks when sharing data with UI thread
- **Lightweight analysis only** - complex DSP should be offloaded to background thread

**Common Use Cases:**

1. **Audio visualization** - Create oscillographs, spectrum analyzers, VU meters overlaid on video
2. **Custom effects** - Apply reverb, echo, distortion, pitch shift to recorded audio
3. **Audio filtering** - Noise reduction, compression, EQ applied during recording
4. **Level monitoring** - Peak detection, silence detection, clipping detection
5. **Format conversion** - Channel mixing (stereo to mono), sample rate effects

**Important Notes:**

- **Only works with `UseRealtimeVideoProcessing = true`** - part of the capture video flow
- You **MUST call `base.WriteAudioSample(sample)`** to record the audio
- Modifications to `sample.Data` are recorded to the video file
- Processing happens **before encoding** - your changes affect the final video
- Works in both **live recording and pre-recording modes**
- Thread-safe access required when sharing audio data with other threads
- `EnableAudioRecording = true` must be set to receive audio samples

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
.ObserveProperty(CameraControl, nameof(CameraControl.IsRecording), me =>
{
    me.Text = CameraControl.IsRecording ? "🛑 Stop" : "🎥 Record";
    me.BackgroundColor = CameraControl.IsRecording ? Colors.Red : Colors.Purple;
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
.ObserveProperty(CameraControl, nameof(CameraControl.EnableAudioRecording), me =>
{
    me.Text = CameraControl.EnableAudioRecording ? "🎤 Audio" : "🔇 Silent";
    me.BackgroundColor = CameraControl.EnableAudioRecording ? Colors.Green : Colors.Gray;
});

private void ToggleAudioRecording()
{
    // Only allow changing audio setting when not recording
    if (!CameraControl.IsRecording)
    {
        CameraControl.EnableAudioRecording = !CameraControl.EnableAudioRecording;
    }
}

private async Task ToggleVideoRecording()
{
    if (CameraControl.State != CameraState.On)
        return;

    try
    {
        if (CameraControl.IsRecording)
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
- **Property**: `IsRecording` (read-only status)
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
camera.RecordingSuccess += (sender, video) =>
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

camera.RecordingFailed += (sender, exception) =>
{
    MainThread.BeginInvokeOnMainThread(async () =>
    {
        await DisplayAlert("Recording Failed", 
            $"Video recording failed: {exception.Message}", "OK");
    });
};

camera.RecordingProgress += (sender, duration) =>
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
    public Metadata Meta { get; set; }          // Video metadata (GPS, author, camera, date — auto-filled on save)
    public double? Latitude { get; set; }       // GPS latitude (set before saving to embed location)
    public double? Longitude { get; set; }      // GPS longitude (set before saving to embed location)
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
        _camera.RecordingSuccess += OnVideoSuccess;
        _camera.RecordingFailed += OnVideoFailed;
        _camera.RecordingProgress += OnVideoProgress;

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
            if (_camera.IsRecording)
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

## GPS & Video Metadata

SkiaCamera supports embedding **GPS coordinates** into both **photos** (EXIF) and **videos** (MP4/MOV `©xyz` atom), plus **rich metadata** for videos (author, camera make/model, software, date — similar to photo EXIF). Gallery apps on all platforms will display the location on a map, and metadata readers will show the embedded info.

### Setup

#### 1. Add platform permissions

**iOS/macOS** — add to `Platforms/iOS/Info.plist` and `Platforms/MacCatalyst/Info.plist`:

```xml
<key>NSLocationWhenInUseUsageDescription</key>
<string>To be able to geotag photos and videos</string>
```

**Android** — add to `Platforms/Android/AndroidManifest.xml`:

```xml
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
```

**Windows** — add the `Location` capability in `Package.appxmanifest`:

```xml
<Capabilities>
    <DeviceCapability Name="location" />
</Capabilities>
```

#### 2. Enable GPS injection and fetch coordinates

```csharp
// Enable GPS injection — save methods will embed LocationLat/LocationLon into files
camera.InjectGpsLocation = true;

// Fetch GPS coordinates (call on main thread — shows permission dialog if needed)
await camera.RefreshGpsLocation();
```

**Important:** You must call `RefreshGpsLocation()` yourself — for example when the camera starts, or when your page appears. The save methods do **not** fetch GPS automatically. They only check whether `LocationLat`/`LocationLon` are already set and `InjectGpsLocation` is `true`.

### How It Works

GPS injection uses a **manual fetch + automatic embed** pattern:

1. **You call `RefreshGpsLocation()`** on the main thread (so the permission dialog can be shown).
2. It requests location permissions, then fetches coordinates and stores them in `LocationLat`/`LocationLon`.
3. When you call any save method (`SaveToGalleryAsync`, `MoveVideoToGalleryAsync`, `SaveVideoToGalleryAsync`), it checks `InjectGpsLocation` and `LocationLat`/`LocationLon` — if all set, GPS is embedded automatically.

```
[Your code] — e.g. when camera starts or page appears
    |
    v
await camera.RefreshGpsLocation()
    |
    |-- 1. Check permission: Permissions.CheckStatusAsync<LocationWhenInUse>()
    |       If not granted -> RequestAsync() -> shows system prompt
    |       If denied -> returns without coordinates
    |
    |-- 2. GetLastKnownLocationAsync()     <-- instant, returns OS-cached location
    |       (last GPS fix from any app)
    |
    |-- 3. If no cached location available:
    |       RefreshLocation(msTimeout)     <-- fresh GPS fix, default 2s timeout
    |       calls Geolocation.GetLocationAsync()
    |
    v
LocationLat / LocationLon populated — ready for save methods
```

**Key points:**
- **You control when permissions are requested** — call `RefreshGpsLocation()` on the main thread at a time that makes sense for your app flow.
- `GetLastKnownLocationAsync()` is **instant** — it returns the platform's cached location from the last GPS fix by any app. In most cases this is all that's needed.
- `RefreshLocation()` is the fallback — it requests a fresh GPS fix with `GeolocationAccuracy.Medium`. Only called if there's no cached location.
- Once retrieved, coordinates are stored in `LocationLat`/`LocationLon` and reused for all subsequent saves without re-fetching.
- If GPS is not supported or not enabled, save methods still work — files are saved without location.
- You can also set `LocationLat`/`LocationLon` directly from your own GPS source (e.g., a hardware device).

**Photos** use `JpegExifInjector` — a cross-platform pure C# EXIF writer that injects GPS data into the JPEG stream before saving.

**Videos** use `Mp4MetadataInjector` — a cross-platform pure C# helper that writes text atoms into the MP4/MOV file's `moov > udta` box. GPS goes into `©xyz` (ISO 6709), plus `©too` (software), `©mak` (make), `©mod` (model), `©day` (date), `©ART` (artist), `©cmt` (comment). The operation is instant (modifies only the file header, no re-encoding). `Mp4LocationInjector` is still available as a backward-compatible wrapper for GPS-only injection.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `InjectGpsLocation` | `bool` | Enable GPS embedding into saved photos and videos |
| `LocationLat` | `double` | Current latitude (set by `RefreshGpsLocation()` or manually) |
| `LocationLon` | `double` | Current longitude (set by `RefreshGpsLocation()` or manually) |
| `GpsBusy` | `bool` | Whether a GPS operation is in progress (read-only) |

#### Methods

| Method | Description |
|--------|-------------|
| `RefreshGpsLocation(int msTimeout = 2000)` | Requests location permissions and fetches GPS coordinates. **Must be called on the main thread.** Results stored in `LocationLat`/`LocationLon`. |
| `RefreshLocation(int msTimeout)` | Low-level GPS fetch without permission handling. Requires permissions already granted. |

### Photo Capture Flow

```
TakePicture()
    |
    v
CaptureSuccess event fires with CapturedImage
    |
    v
[Your callback] — optionally set GPS manually on captured.Meta
    |
    v
SaveToGalleryAsync(capturedImage, album)
    |
    |-- Check InjectGpsLocation && LocationLat/Lon are set
    |   and no GPS already set on Meta
    |   -> apply GPS to Meta
    |
    |-- JpegExifInjector.InjectExifMetadata(stream, meta)  <-- GPS embedded here
    |
    v
Platform saves JPEG with EXIF to gallery — gallery app shows location
```

**Example — automatic GPS with RefreshGpsLocation:**

```csharp
// When camera starts (on main thread)
camera.InjectGpsLocation = true;
await camera.RefreshGpsLocation();

// Later, in your capture callback — GPS is embedded automatically
camera.CaptureSuccess += async (sender, captured) =>
{
    var path = await camera.SaveToGalleryAsync(captured, "MyAlbum");
};
```

**Example — manual GPS coordinates (no RefreshGpsLocation needed):**

```csharp
camera.CaptureSuccess += async (sender, captured) =>
{
    // Set coordinates explicitly on the metadata
    Metadata.ApplyGpsCoordinates(captured.Meta, latitude, longitude);

    // Save — EXIF with GPS is injected automatically
    var path = await camera.SaveToGalleryAsync(captured, "MyAlbum");
};
```

### Video Capture Flow

```
StartVideoRecording()
    |
    v
[Recording in progress...]
    |
    v
StopVideoRecording()
    |
    v
RecordingSuccess event fires with CapturedVideo
    |
    v
[Your callback] — optionally customize video.Meta (GPS, author, etc.)
    |
    v
MoveVideoToGalleryAsync(video, album)  or  SaveVideoToGalleryAsync(video, album)
    |
    |-- AutoFillVideoMetadata(video):
    |     - Creates video.Meta if null
    |     - Fills Software (app name + version)
    |     - Fills Vendor (DeviceInfo.Manufacturer) and Model (DeviceInfo.Model)
    |     - Fills DateTimeOriginal from recording start time
    |     - Fills GPS from LocationLat/Lon (if InjectGpsLocation is true)
    |       or from video.Latitude/Longitude (if set manually)
    |     - Skips any field already set by user
    |
    |-- Mp4MetadataInjector.InjectMetadataAsync(filePath, video.Meta)
    |   (instant, modifies file header only — writes ©xyz, ©too, ©mak, ©mod, ©day, etc.)
    |
    v
Platform saves MP4/MOV to gallery — gallery app shows location + metadata
```

**Example — automatic GPS with RefreshGpsLocation:**

```csharp
// When camera starts (on main thread)
camera.InjectGpsLocation = true;
await camera.RefreshGpsLocation();

// Later, in your video callback — GPS is embedded automatically
camera.RecordingSuccess += async (sender, video) =>
{
    var galleryPath = await camera.MoveVideoToGalleryAsync(video, "MyAlbum");
};
```

**Example — manual GPS coordinates (no RefreshGpsLocation needed):**

```csharp
camera.RecordingSuccess += async (sender, video) =>
{
    // Set coordinates explicitly
    video.Latitude = 34.0522;
    video.Longitude = -118.2437;

    // Save — location is injected into MP4 automatically
    var galleryPath = await camera.MoveVideoToGalleryAsync(video, "MyAlbum");
};
```

**Example — customize video metadata:**

```csharp
camera.RecordingSuccess += async (sender, video) =>
{
    // Pre-fill Meta to override auto-populated fields
    video.Meta = new Metadata
    {
        Software = "MyApp Pro 2.0",
        CameraOwnerName = "John Doe",
        UserComment = "Race lap 3",
        Vendor = "CustomCam",
        Model = "RaceCam X1"
    };

    // GPS will still be auto-filled if InjectGpsLocation is on
    // DateTimeOriginal will still be auto-filled from video.Time
    var galleryPath = await camera.MoveVideoToGalleryAsync(video, "MyAlbum");
};
```

### Saving Without Gallery (Direct File Injection)

If you save files to your own app folder instead of the gallery, the automatic metadata injection in `SaveToGalleryAsync`/`MoveVideoToGalleryAsync` won't run. Use the injectors directly on any file path:

**Video — inject metadata into any MP4/MOV file:**

```csharp
camera.RecordingSuccess += async (sender, video) =>
{
    // Save to your own app folder
    var appPath = Path.Combine(FileSystem.AppDataDirectory, "MyVideos", "recording.mp4");
    File.Copy(video.FilePath, appPath);

    // Inject full metadata (GPS, author, camera, date — modifies file header only)
    var meta = new Metadata
    {
        Software = "MyApp 1.0",
        Vendor = "Apple",
        Model = "iPhone 15 Pro"
    };
    Metadata.ApplyGpsCoordinates(meta, 34.0522, -118.2437);
    meta.DateTimeOriginal = DateTime.Now;
    await Mp4MetadataInjector.InjectMetadataAsync(appPath, meta);

    // Or inject GPS only (backward-compatible helper)
    await Mp4LocationInjector.InjectLocationAsync(appPath, 34.0522, -118.2437);

    // Or inject arbitrary atoms directly
    await Mp4MetadataInjector.InjectAtomsAsync(appPath, new Dictionary<string, string>
    {
        [Mp4MetadataInjector.Atom_Artist] = "John Doe",
        [Mp4MetadataInjector.Atom_Comment] = "Race lap 3"
    });
};
```

**Photo — inject GPS into any JPEG file:**

```csharp
camera.CaptureSuccess += async (sender, captured) =>
{
    // Set GPS on the metadata object
    Metadata.ApplyGpsCoordinates(captured.Meta, 34.0522, -118.2437);

    // Could inject other metadata..
    captured.Meta.Model = "MyCamera";

    // Get the JPEG stream with EXIF injected
    await using var stream = camera.CreateOutputStreamRotated(captured, false);
    using var exifStream = await JpegExifInjector.InjectExifMetadata(stream, captured.Meta);

    // Save to your own app folder
    var appPath = Path.Combine(FileSystem.AppDataDirectory, "MyPhotos", "photo.jpg");
    await using var fileStream = File.Create(appPath);
    await exifStream.CopyToAsync(fileStream);
};
```

**Read/write metadata on any existing video file:**

```csharp
// Read all metadata atoms
var atoms = Mp4MetadataInjector.ReadAtoms("/path/to/video.mp4");
if (atoms != null)
{
    foreach (var (key, value) in atoms)
        Console.WriteLine($"{key}: {value}");
}

// Read GPS location specifically
if (Mp4MetadataInjector.ReadLocation("/path/to/video.mp4", out double lat, out double lon))
{
    Console.WriteLine($"Video location: {lat}, {lon}");
}

// Read atoms into a Metadata object
var meta = new Metadata();
if (atoms != null)
    Mp4MetadataInjector.AtomsToMetadata(atoms, meta);

// Inject or replace location
await Mp4MetadataInjector.InjectLocationAsync("/path/to/video.mp4", 34.0522, -118.2437);

// Photo: inject or merge EXIF metadata (including GPS)
var photoMeta = new Metadata();
Metadata.ApplyGpsCoordinates(photoMeta, 34.0522, -118.2437);
using var jpegStream = File.OpenRead("/path/to/photo.jpg");
using var result = await JpegExifInjector.InjectExifMetadata(jpegStream, photoMeta);
await using var output = File.Create("/path/to/photo_with_gps.jpg");
await result.CopyToAsync(output);
```

### Supported Video Metadata Atoms

| Atom | Constant | Metadata Property | Description |
|------|----------|------------------|-------------|
| `©xyz` | `Atom_Location` | `GpsLatitude`/`GpsLongitude` | GPS location (ISO 6709) |
| `©too` | `Atom_Software` | `Software` | App name/version |
| `©mak` | `Atom_Make` | `Vendor` | Device manufacturer |
| `©mod` | `Atom_Model` | `Model` | Device model |
| `©day` | `Atom_Date` | `DateTimeOriginal` | Recording date/time |
| `©ART` | `Atom_Artist` | `CameraOwnerName` | Author/artist |
| `©cmt` | `Atom_Comment` | `UserComment` | Free-text comment |
| `©nam` | `Atom_Title` | — | Title (raw atom only) |
| `©des` | `Atom_Description` | — | Description (raw atom only) |

### Platform Details

| Feature | Android | iOS/macOS | Windows |
|---------|---------|-----------|---------|
| Photo GPS (EXIF) | `JpegExifInjector` + `ExifInterface` | `JpegExifInjector` + Photos framework | `JpegExifInjector` |
| Video metadata | `Mp4MetadataInjector` | `Mp4MetadataInjector` + `CLLocation` on `PHAssetChangeRequest` | `Mp4MetadataInjector` |
| Gallery shows location | Google Photos | Apple Photos | File properties |
| Format | ISO BMFF udta text atoms | ISO BMFF udta text atoms | ISO BMFF udta text atoms |

Video metadata injection is fully cross-platform — the same pure C# code runs on all platforms with no native dependencies. On iOS, the gallery location is additionally set via `PHAssetChangeRequest.Location` since iOS Photos ignores the `©xyz` atom during import.

## AudioSampleConverter

A lightweight audio preprocessing utility for raw PCM16 audio. Handles stereo-to-mono downmix, sample rate conversion, and optional silence gating — useful when feeding microphone audio to speech-to-text APIs (OpenAI Realtime, Whisper, Azure Speech, etc.).

**Smart passthrough** — each processing step is skipped when not needed:
- Mono input (1 channel): no downmix, zero-copy passthrough.
- Source rate == target rate: no resampling, zero-copy passthrough.
- `silenceRmsThreshold == 0`: silence gating disabled entirely, no RMS calculation.
- When all conditions match: `Process()` returns the original input array with zero allocations.

**Stateful resampling** — maintains interpolation continuity across calls for glitch-free audio.

### Usage

```csharp
using DrawnUi.Camera;

// Create: target 24kHz output (for OpenAI Realtime API), default silence gate
var preprocessor = new AudioSampleConverter(targetSampleRate: 24000);

// Or 16kHz for Whisper, with silence gating disabled:
var preprocessor = new AudioSampleConverter(targetSampleRate: 16000, silenceRmsThreshold: 0);

// Set source format from your audio device (call again if device changes):
preprocessor.SetFormat(sampleRate: 48000, channels: 2);

// Process each audio chunk from the microphone callback:
byte[] result = preprocessor.Process(rawPcm16Data);
if (result != null)
{
    // result is mono PCM16 at target sample rate — send to API, save to file, etc.
}
// result == null means prolonged silence or invalid input, skip sending.

// Reset state when starting a new session:
preprocessor.Reset();
```

### Constructor Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `targetSampleRate` | *(required)* | Output sample rate in Hz (e.g. 24000 for OpenAI, 16000 for Whisper) |
| `silenceRmsThreshold` | 0.003 | RMS level (0..1) below which audio is silence. Set to 0 to disable. |
| `silentChunksBeforeMute` | 100 | Consecutive silent chunks before suppressing output (~1s at 48kHz/480 samples) |

### 🚧 ToDo
- [ ] **Manual camera controls** (focus, exposure, ISO, white balance) - partially implemented, need to expose more controls  
- [ ] **Camera capability detection** (zoom ranges, supported formats) - need to combine available cameras list with camera units list and expose
- [ ] **Video recording platform implementations** - API complete, platform-specific recording implementations needed
- [ ] **Preview format customization** - currently auto-selected to match capture aspect ratio


## References

iOS: 
* [Manual Camera Controls in Xamarin.iOS](https://github.com/MicrosoftDocs/xamarin-docs/blob/0506e3bf14b520776fc7d33781f89069bbc57138/docs/ios/user-interface/controls/intro-to-manual-camera-controls.md) by David Britch

