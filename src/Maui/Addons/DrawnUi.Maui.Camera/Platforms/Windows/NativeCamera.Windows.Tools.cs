using System.ComponentModel;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DrawnUi.Camera;

public partial class NativeCamera : IDisposable, INativeCamera, INotifyPropertyChanged
{
    /// <summary>
    /// Measures actual scene brightness using Windows MediaCapture auto exposure system
    /// </summary>
    public async Task<BrightnessResult> MeasureSceneBrightnessBak(MeteringMode meteringMode)
    {
        try
        {
            if (_mediaCapture == null)
                return new BrightnessResult { Success = false, ErrorMessage = "Camera not initialized" };

            var videoDeviceController = _mediaCapture.VideoDeviceController;

            // Try to get real camera characteristics first
            var cameraCharacteristics = await GetCameraCharacteristics();
            if (cameraCharacteristics == null)
            {
                return new BrightnessResult
                {
                    Success = false,
                    ErrorMessage =
                        "Cannot retrieve camera aperture characteristics - hardware-independent measurement impossible"
                };
            }

            System.Diagnostics.Debug.WriteLine(
                $"[WINDOWS CAMERA] Camera aperture: f/{cameraCharacteristics.Aperture:F1}");

            try
            {
                // Force camera into AUTO mode using extended properties
                await SetAutoExposureMode(videoDeviceController, true);
                await SetAutoISOMode(videoDeviceController, true);

                // Set metering mode if supported
                await SetMeteringMode(meteringMode, videoDeviceController);

                // Wait for camera to measure and adjust
                await Task.Delay(2000); // Longer delay for Windows to stabilize

                // Get the camera's chosen exposure settings using extended properties
                var exposureData = await GetCurrentExposureSettingsExtended(videoDeviceController);
                if (!exposureData.Success)
                {
                    return new BrightnessResult
                    {
                        Success = false,
                        ErrorMessage = $"Cannot read camera exposure settings: {exposureData.ErrorMessage}"
                    };
                }

                // Calculate the EV that the camera chose for "proper" exposure
                var chosenEV = Math.Log2((cameraCharacteristics.Aperture * cameraCharacteristics.Aperture) /
                                         exposureData.ExposureTime)
                               + Math.Log2(exposureData.ISO / 100.0);

                // Convert EV to scene brightness (lux)
                const double K = 12.5;
                var sceneBrightness = K * Math.Pow(2, chosenEV) / (exposureData.ISO / 100.0);

                System.Diagnostics.Debug.WriteLine(
                    $"[WINDOWS CAMERA] Measured: f/{cameraCharacteristics.Aperture:F1}, 1/{(1 / exposureData.ExposureTime):F0}, ISO{exposureData.ISO:F0}");
                System.Diagnostics.Debug.WriteLine(
                    $"[WINDOWS CAMERA] Calculated EV: {chosenEV:F1}, Scene brightness: {sceneBrightness:F0} lux");

                return new BrightnessResult { Success = true, Brightness = sceneBrightness };
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WINDOWS CAMERA ERROR] COMException: {comEx.Message}\n{comEx.StackTrace}");
                return new BrightnessResult { Success = false, ErrorMessage = $"COMException: {comEx.Message}" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA ERROR] Exception: {ex.Message}\n{ex.StackTrace}");
                return new BrightnessResult { Success = false, ErrorMessage = ex.Message };
            }
            finally
            {
                // Restore AUTO modes (usually the default anyway)
                await SetAutoExposureMode(videoDeviceController, true);
                await SetAutoISOMode(videoDeviceController, true);
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA ERROR] {e.Message}\n{e.StackTrace}");
            return new BrightnessResult { Success = false, ErrorMessage = e.Message };
        }
    }

    /// <summary>
    /// Set auto exposure mode using extended device properties
    /// </summary>
    private async Task SetAutoExposureMode(VideoDeviceController controller, bool autoMode)
    {
        try
        {
            if (controller.ExposureControl.Supported)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WINDOWS CAMERA] Exposure auto mode is read-only and cannot be set programmatically.");
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA] SetAutoExposureMode error: {e}");
        }
    }

    /// <summary>
    /// Set auto ISO mode using extended device properties
    /// </summary>
    private async Task SetAutoISOMode(VideoDeviceController controller, bool autoMode)
    {
        try
        {
            if (controller.IsoSpeedControl.Supported)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WINDOWS CAMERA] ISO auto mode is read-only and cannot be set programmatically.");
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA] SetAutoISOMode error: {e}");
        }
    }

    /// <summary>
    /// Get current exposure settings using extended properties
    /// </summary>
    private async Task<ExposureData> GetCurrentExposureSettingsExtended(VideoDeviceController controller)
    {
        try
        {
            double exposureTime = 0;
            double iso = 100;

            // Try to get exposure time
            try
            {
                if (controller.ExposureControl.Supported)
                {
                    var currentExposure = controller.ExposureControl.Value;
                    exposureTime = currentExposure.TotalSeconds;
                    System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA] Read exposure time: {exposureTime:F6}s");
                }
                else
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine(
                        "[WINDOWS CAMERA] ExposureControl is not supported. Using default exposure time.");
                    exposureTime = 1.0 / 60; // Default to 1/60s
#else
                    return new ExposureData
                    {
                        Success = false,
                        ErrorMessage = "ExposureControl not supported"
                    };
#endif
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA] Cannot read exposure time: {ex.Message}");
                exposureTime = 1.0 / 60; // Default to 1/60s
            }

            // Try to get ISO
            try
            {
                if (controller.IsoSpeedControl.Supported)
                {
                    iso = controller.IsoSpeedControl.Value;
                    System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA] Read ISO: {iso}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[WINDOWS CAMERA] IsoSpeedControl is not supported. Using default ISO.");
                    iso = 100; // Default ISO value
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA] Cannot read ISO: {ex.Message}");
                iso = 100; // Default ISO value
            }

            if (exposureTime <= 0)
            {
                return new ExposureData { Success = false, ErrorMessage = "Invalid exposure time" };
            }

            return new ExposureData { Success = true, ExposureTime = exposureTime, ISO = iso };
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[WINDOWS CAMERA ERROR] GetCurrentExposureSettingsExtended error: {e.Message}\n{e.StackTrace}");
            return new ExposureData { Success = false, ErrorMessage = e.Message };
        }
    }

    private async Task<double> GetExposureTimeFromExtendedProperty(VideoDeviceController controller)
    {
        throw new NotSupportedException("Extended property for exposure time is not supported.");
    }

    private async Task<double> GetISOFromExtendedProperty(VideoDeviceController controller)
    {
        throw new NotSupportedException("Extended property for ISO is not supported.");
    }

    /// <summary>
    /// Attempts to get real camera aperture characteristics from hardware
    /// </summary>
    private async Task<CameraCharacteristics> GetCameraCharacteristics()
    {
        try
        {
            // Try multiple approaches to get real aperture value

            // Approach 1: Check if camera exposes aperture control
            if (_mediaCapture.VideoDeviceController.ExposureControl.Supported)
            {
                // Some cameras expose aperture through extended properties
                var aperture = await TryGetApertureFromExtendedProperties();
                if (aperture.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine("[WINDOWS CAMERA] Aperture retrieved from extended properties.");
                    return new CameraCharacteristics { Aperture = aperture.Value };
                }
            }

            // Approach 2: Try to get from device information/metadata
            var apertureFromDevice = await TryGetApertureFromDeviceInfo();
            if (apertureFromDevice.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("[WINDOWS CAMERA] Aperture retrieved from device information.");
                return new CameraCharacteristics { Aperture = apertureFromDevice.Value };
            }

            // Approach 3: Check frame source properties
            var apertureFromFrameSource = TryGetApertureFromFrameSource();
            if (apertureFromFrameSource.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("[WINDOWS CAMERA] Aperture retrieved from frame source properties.");
                return new CameraCharacteristics { Aperture = apertureFromFrameSource.Value };
            }

            // Fallback: Log and return a default aperture value
            System.Diagnostics.Debug.WriteLine(
                "[WINDOWS CAMERA] Unable to retrieve aperture characteristics. Using default value.");
            return new CameraCharacteristics { Aperture = 2.8 }; // Default aperture value
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA] GetCameraCharacteristics error: {e}");
            return null;
        }
    }

    private async Task<double?> TryGetApertureFromExtendedProperties()
    {
        try
        {
            // Try common extended property GUIDs for aperture
            var aperturePropertyId =
                new Guid("{0x9378b8e1, 0x5511, 0x4b5b, {0x9e, 0x5e, 0x5c, 0x80, 0x6b, 0xa9, 0x3e, 0x56}}");

            if (_mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview)
                    .Count > 0)
            {
                // This is a placeholder - the actual implementation would depend on specific camera drivers
                // Different manufacturers use different property GUIDs
                System.Diagnostics.Debug.WriteLine("[WINDOWS CAMERA] Checking extended properties for aperture...");
            }

            return null; // Most consumer webcams don't expose this
        }
        catch
        {
            return null;
        }
    }

    private async Task<double?> TryGetApertureFromDeviceInfo()
    {
        try
        {
            if (_cameraDevice?.Properties != null)
            {
                // Check device properties for aperture information
                foreach (var prop in _cameraDevice.Properties)
                {
                    System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA] Device property: {prop.Key} = {prop.Value}");

                    // Look for aperture-related properties
                    if (prop.Key.Contains("Aperture", StringComparison.OrdinalIgnoreCase) ||
                        prop.Key.Contains("FNumber", StringComparison.OrdinalIgnoreCase))
                    {
                        if (double.TryParse(prop.Value?.ToString(), out double aperture))
                        {
                            return aperture;
                        }
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private double? TryGetApertureFromFrameSource()
    {
        try
        {
            if (_frameSource?.Info?.Properties != null)
            {
                // Check frame source properties
                foreach (var prop in _frameSource.Info.Properties)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[WINDOWS CAMERA] Frame source property: {prop.Key} = {prop.Value}");
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public class CameraCharacteristics
    {
        public double Aperture { get; set; }
    }

    public class ExposureData
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public double ExposureTime { get; set; }
        public double ISO { get; set; }
    }

    private async Task<ExposureData> GetCurrentExposureSettings(VideoDeviceController controller)
    {
        try
        {
            double exposureTime = 0;
            double iso = 100;

            // Get exposure time
            if (controller.ExposureControl.Supported && !controller.ExposureControl.Auto)
            {
                exposureTime = controller.ExposureControl.Value.TotalSeconds;
            }
            else
            {
                return new ExposureData
                {
                    Success = false,
                    ErrorMessage =
                        "Cannot read exposure time - camera doesn't support manual exposure or is in auto mode"
                };
            }

            // Get ISO
            if (controller.IsoSpeedControl.Supported && !controller.IsoSpeedControl.Auto)
            {
                iso = controller.IsoSpeedControl.Value;
            }
            else
            {
                return new ExposureData
                {
                    Success = false,
                    ErrorMessage = "Cannot read ISO - camera doesn't support manual ISO or is in auto mode"
                };
            }

            return new ExposureData { Success = true, ExposureTime = exposureTime, ISO = iso };
        }
        catch (Exception e)
        {
            return new ExposureData { Success = false, ErrorMessage = e.Message };
        }
    }


    private async Task RestoreOriginalSettings(VideoDeviceController controller, bool originalExposureAuto,
        bool originalIsoAuto)
    {
        try
        {
            if (controller.ExposureControl.Supported)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WINDOWS CAMERA] Exposure auto mode is read-only and cannot be restored programmatically.");
            }

            if (controller.IsoSpeedControl.Supported)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WINDOWS CAMERA] ISO auto mode is read-only and cannot be restored programmatically.");
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA] Failed to restore settings: {e}");
        }
    }


    /// <summary>
    /// Detects and returns the list of supported metering modes for the current camera hardware.
    /// </summary>
    public List<MeteringMode> GetSupportedMeteringModes()
    {
        var supportedModes = new List<MeteringMode>();

        try
        {
            if (_mediaCapture == null || _mediaCapture.VideoDeviceController == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[WINDOWS CAMERA] MediaCapture or VideoDeviceController is not initialized.");
                return supportedModes;
            }

            var controller = _mediaCapture.VideoDeviceController;

            // Check if RegionsOfInterestControl is supported and functional
            if (controller.RegionsOfInterestControl != null)
            {
                // Check if auto focus is supported (required for metering modes)
                if (controller.RegionsOfInterestControl.AutoFocusSupported)
                {
                    // Check if the device supports at least one region
                    if (controller.RegionsOfInterestControl.MaxRegions > 0)
                    {
                        // Add supported metering modes
                        supportedModes.Add(MeteringMode.Spot);
                        System.Diagnostics.Debug.WriteLine("[WINDOWS CAMERA] Spot metering mode is supported.");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "[WINDOWS CAMERA] RegionsOfInterestControl supports AutoFocus but MaxRegions is 0.");
                    }

                    // Center-weighted is typically supported if we can clear regions
                    supportedModes.Add(MeteringMode.CenterWeighted);
                    System.Diagnostics.Debug.WriteLine("[WINDOWS CAMERA] Center-weighted metering mode is supported.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[WINDOWS CAMERA] RegionsOfInterestControl is available but AutoFocus is not supported.");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    "[WINDOWS CAMERA] RegionsOfInterestControl is not supported. No advanced metering modes available.");
            }

            System.Diagnostics.Debug.WriteLine(
                $"[WINDOWS CAMERA] Total supported metering modes: {supportedModes.Count}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[WINDOWS CAMERA ERROR] Failed to detect supported metering modes: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA ERROR] StackTrace: {ex.StackTrace}");
        }

        return supportedModes;
    }

    /// <summary>
    /// Gets the current metering mode description for display to user
    /// </summary>
    public string GetCurrentMeteringModeDescription()
    {
        try
        {
            if (_mediaCapture?.VideoDeviceController?.RegionsOfInterestControl == null)
            {
                return "Automatic (camera default)";
            }

            var roiControl = _mediaCapture.VideoDeviceController.RegionsOfInterestControl;

            if (!roiControl.AutoFocusSupported)
            {
                return "Automatic (center-weighted)";
            }

            // If we have control, check if regions are set
            // This would only work if you had previously set a mode
            if (roiControl.MaxRegions > 0)
            {
                return "Manual control available";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA] Error getting metering description: {ex.Message}");
        }

        return "Automatic (unknown pattern)";
    }

    /// <summary>
    /// Gets the current metering capabilities and active mode for UI display
    /// </summary>
    public (MeteringMode currentMode, List<MeteringMode> availableModes, bool canSwitch) GetMeteringInfo()
    {
        var availableModes = new List<MeteringMode>();
        var currentMode = MeteringMode.CenterWeighted; // Most cameras default to this
        var canSwitch = false;

        try
        {
            if (_mediaCapture?.VideoDeviceController?.RegionsOfInterestControl == null)
            {
                // No regions control - show current mode as read-only
                availableModes.Add(MeteringMode.CenterWeighted);
                return (currentMode, availableModes, canSwitch);
            }

            var roiControl = _mediaCapture.VideoDeviceController.RegionsOfInterestControl;

            if (!roiControl.AutoFocusSupported)
            {
                // Case: "Automatic (center-weighted)" - your current situation
                // Show CenterWeighted as current mode, but user can't change it
                currentMode = MeteringMode.CenterWeighted;
                availableModes.Add(MeteringMode.CenterWeighted);
                canSwitch = false;

                System.Diagnostics.Debug.WriteLine("[WINDOWS CAMERA] Showing CenterWeighted (read-only)");
            }
            else if (roiControl.MaxRegions > 0)
            {
                // Case: Full control available - user can switch between modes
                availableModes.Add(MeteringMode.CenterWeighted);
                availableModes.Add(MeteringMode.Spot);
                canSwitch = true;

                // You'd need to track which mode was last set, defaulting to CenterWeighted
                currentMode = _lastSetMeteringMode;

                System.Diagnostics.Debug.WriteLine("[WINDOWS CAMERA] Full metering control available");
            }
            else
            {
                // Edge case: AutoFocus supported but no regions
                currentMode = MeteringMode.CenterWeighted;
                availableModes.Add(MeteringMode.CenterWeighted);
                canSwitch = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA] Error getting metering info: {ex.Message}");
            // Fallback
            availableModes.Add(MeteringMode.CenterWeighted);
        }

        return (currentMode, availableModes, canSwitch);
    }

    /// <summary>
    /// Sets the metering mode for exposure and focus calculations
    /// </summary>
    private async Task SetMeteringMode(MeteringMode meteringMode, VideoDeviceController controller)
    {
        try
        {
            if (controller.RegionsOfInterestControl == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[WINDOWS CAMERA] RegionsOfInterestControl is not supported on this device.");
                return;
            }

            if (!controller.RegionsOfInterestControl.AutoFocusSupported)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[WINDOWS CAMERA] AutoFocus is not supported by RegionsOfInterestControl.");
                return;
            }

            if (controller.RegionsOfInterestControl.MaxRegions == 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[WINDOWS CAMERA] No regions of interest are supported by this device.");
                return;
            }

            switch (meteringMode)
            {
                case MeteringMode.Spot:
                    var spotRegion = new Windows.Foundation.Rect(0.45, 0.45, 0.1, 0.1);
                    var spotRegionOfInterest = new RegionOfInterest
                    {
                        AutoFocusEnabled = true,
                        BoundsNormalized = true,
                        Bounds = spotRegion,
                        Type = RegionOfInterestType.Unknown,
                        Weight = 100
                    };

                    await controller.RegionsOfInterestControl.SetRegionsAsync(
                        new[] { spotRegionOfInterest }, true);

                    // Remember what we set
                    _lastSetMeteringMode = MeteringMode.Spot;
                    System.Diagnostics.Debug.WriteLine("[WINDOWS CAMERA] Spot metering mode set successfully.");
                    break;

                case MeteringMode.CenterWeighted:
                    await controller.RegionsOfInterestControl.ClearRegionsAsync();

                    // Remember what we set  
                    _lastSetMeteringMode = MeteringMode.CenterWeighted;
                    System.Diagnostics.Debug.WriteLine(
                        "[WINDOWS CAMERA] Center-weighted metering mode set successfully.");
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WINDOWS CAMERA ERROR] SetMeteringMode error: {ex.Message}");
        }
    }

    MeteringMode _lastSetMeteringMode = MeteringMode.CenterWeighted;
}
