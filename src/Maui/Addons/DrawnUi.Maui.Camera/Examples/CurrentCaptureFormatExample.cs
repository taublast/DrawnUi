using DrawnUi.Camera;
using System.Diagnostics;

namespace DrawnUi.Camera.Examples;

/// <summary>
/// Example demonstrating how to use the CurrentStillCaptureFormat property
/// to read the currently selected capture format in cross-platform form.
/// </summary>
public class CurrentCaptureFormatExample
{
    private SkiaCamera _camera;

    public CurrentCaptureFormatExample(SkiaCamera camera)
    {
        _camera = camera;
    }

    /// <summary>
    /// Example 1: Reading current format after setting quality presets
    /// </summary>
    public void ExampleQualityPresets()
    {
        Debug.WriteLine("=== Quality Presets Example ===");

        // Set different quality presets and read the resulting format
        var qualities = new[] { CaptureQuality.Max, CaptureQuality.Medium, CaptureQuality.Low, CaptureQuality.Preview };

        foreach (var quality in qualities)
        {
            _camera.CapturePhotoQuality = quality;
            
            // Read the current format (may be null if camera not initialized)
            var currentFormat = _camera.CurrentStillCaptureFormat;
            if (currentFormat != null)
            {
                Debug.WriteLine($"Quality {quality}: {currentFormat.Description}");
                Debug.WriteLine($"  Resolution: {currentFormat.Width}x{currentFormat.Height}");
                Debug.WriteLine($"  Aspect Ratio: {currentFormat.AspectRatioString}");
                Debug.WriteLine($"  Total Pixels: {currentFormat.TotalPixels:N0}");
            }
            else
            {
                Debug.WriteLine($"Quality {quality}: Format not available (camera may not be initialized)");
            }
        }
    }

    /// <summary>
    /// Example 2: Reading current format after manual format selection
    /// </summary>
    public async Task ExampleManualSelection()
    {
        Debug.WriteLine("=== Manual Selection Example ===");

        try
        {
            // Get available formats
            var availableFormats = await _camera.GetAvailableCaptureFormatsAsync();
            Debug.WriteLine($"Available formats: {availableFormats.Count}");

            if (availableFormats.Count > 0)
            {
                // Set to manual mode and select different formats
                _camera.CapturePhotoQuality = CaptureQuality.Manual;

                for (int i = 0; i < Math.Min(3, availableFormats.Count); i++)
                {
                    _camera.CaptureFormatIndex = i;
                    
                    var currentFormat = _camera.CurrentStillCaptureFormat;
                    var expectedFormat = availableFormats[i];
                    
                    if (currentFormat != null)
                    {
                        Debug.WriteLine($"Format Index {i}:");
                        Debug.WriteLine($"  Current: {currentFormat.Description}");
                        Debug.WriteLine($"  Expected: {expectedFormat.Description}");
                        Debug.WriteLine($"  Match: {currentFormat.Width == expectedFormat.Width && currentFormat.Height == expectedFormat.Height}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in manual selection example: {ex.Message}");
        }
    }

    /// <summary>
    /// Example 3: Monitoring format changes
    /// </summary>
    public void ExampleFormatMonitoring()
    {
        Debug.WriteLine("=== Format Monitoring Example ===");

        // Monitor format changes by checking CurrentStillCaptureFormat periodically
        // or after changing quality/format settings

        var initialFormat = _camera.CurrentStillCaptureFormat;
        Debug.WriteLine($"Initial format: {initialFormat?.Description ?? "None"}");

        // Change quality and check format
        _camera.CapturePhotoQuality = CaptureQuality.Max;
        var maxFormat = _camera.CurrentStillCaptureFormat;
        Debug.WriteLine($"Max quality format: {maxFormat?.Description ?? "None"}");

        _camera.CapturePhotoQuality = CaptureQuality.Low;
        var lowFormat = _camera.CurrentStillCaptureFormat;
        Debug.WriteLine($"Low quality format: {lowFormat?.Description ?? "None"}");

        // Compare formats
        if (maxFormat != null && lowFormat != null)
        {
            Debug.WriteLine($"Max vs Low - Resolution difference: {maxFormat.TotalPixels - lowFormat.TotalPixels:N0} pixels");
            Debug.WriteLine($"Max vs Low - Same aspect ratio: {maxFormat.AspectRatioString == lowFormat.AspectRatioString}");
        }
    }

    /// <summary>
    /// Example 4: Using format information for UI updates
    /// </summary>
    public void ExampleUIUpdates()
    {
        Debug.WriteLine("=== UI Updates Example ===");

        var currentFormat = _camera.CurrentStillCaptureFormat;
        if (currentFormat != null)
        {
            // Example: Update UI labels with current format info
            var resolutionText = $"{currentFormat.Width} × {currentFormat.Height}";
            var aspectRatioText = currentFormat.AspectRatioString;
            var megapixelsText = $"{currentFormat.TotalPixels / 1_000_000.0:F1} MP";
            
            Debug.WriteLine($"UI Update - Resolution: {resolutionText}");
            Debug.WriteLine($"UI Update - Aspect Ratio: {aspectRatioText}");
            Debug.WriteLine($"UI Update - Megapixels: {megapixelsText}");

            // Example: Determine if format is suitable for specific use cases
            var isHighRes = currentFormat.TotalPixels >= 8_000_000; // 8MP+
            var isWidescreen = currentFormat.AspectRatio > 1.5;
            var isSquare = Math.Abs(currentFormat.AspectRatio - 1.0) < 0.1;

            Debug.WriteLine($"Format Analysis - High Resolution: {isHighRes}");
            Debug.WriteLine($"Format Analysis - Widescreen: {isWidescreen}");
            Debug.WriteLine($"Format Analysis - Square: {isSquare}");
        }
        else
        {
            Debug.WriteLine("UI Update - No format available");
        }
    }

    /// <summary>
    /// Example 5: Cross-platform format comparison
    /// </summary>
    public void ExampleCrossPlatformComparison()
    {
        Debug.WriteLine("=== Cross-Platform Comparison Example ===");

        var currentFormat = _camera.CurrentStillCaptureFormat;
        if (currentFormat != null)
        {
            Debug.WriteLine($"Platform Format ID: {currentFormat.FormatId}");
            Debug.WriteLine($"Cross-Platform Format: {currentFormat.Description}");
            
            // The CurrentStillCaptureFormat property provides the same CaptureFormat structure
            // across all platforms (Android, iOS, Windows), making it easy to:
            // - Compare formats across devices
            // - Store format preferences in settings
            // - Display consistent format information in UI
            
            Debug.WriteLine("This format information is consistent across:");
            Debug.WriteLine("- Android (using Camera2 API)");
            Debug.WriteLine("- iOS (using AVFoundation)");
            Debug.WriteLine("- Windows (using MediaCapture)");
        }
    }
}
