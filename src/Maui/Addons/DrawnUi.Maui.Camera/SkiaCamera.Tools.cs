global using DrawnUi.Draw;
global using SkiaSharp;

namespace DrawnUi.Camera;

public partial class SkiaCamera : SkiaControl
{
    /// <summary>
    /// Overlays any SkiaLayout over the captured photo and returns a new bitmap.
    /// Remember to dispose the old bitmap if not needed anymore.
    /// Can modify the created SkiaImage used for rendering by passing a callback `createdImage` to add effects etc.
    /// </summary>
    /// <param name="captured"></param>
    /// <param name="overlay"></param>
    /// <param name="createdImage"></param>
    /// <returns></returns>
    public virtual SKImage RenderCapturedPhoto(CapturedImage captured, SkiaLayout overlay,
        Action<SkiaImage> createdImage = null)
    {
        var scaleOverlay = GetRenderingScaleFor(captured.Image.Width, captured.Image.Height);
        double zoomCapturedPhotoX = TextureScale;
        double zoomCapturedPhotoY = TextureScale;

        var width = captured.Image.Width;
        var height = captured.Image.Height;

        if (captured.Rotation == 90 || captured.Rotation == 270)
        {
            height = captured.Image.Width;
            width = captured.Image.Height;
        }

        var info = new SKImageInfo(width, height);

        using (var surface = SKSurface.Create(info))
        {
            if (surface == null)
            {
                Super.Log($"Cannot create SKSurface {width}x{height}");
                return null;
            }

            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.Black);

            //create offscreen rendering context
            var context = new SkiaDrawingContext()
            {
                Surface = surface, Canvas = surface.Canvas, Width = info.Width, Height = info.Height
            };
            var destination = new SKRect(0, 0, info.Width, info.Height);

            //render image
            var image = new SkiaImage
            {
                Tag = "Render",
                LoadSourceOnFirstDraw = true,
                WidthRequest = info.Width,
                HeightRequest = info.Height,
                VerticalOptions = LayoutOptions.Fill,
                IsClippedToBounds = false, //do not clip sides after rotation if any
                AddEffect = this.Effect,
                Aspect = TransformAspect.None,
                ZoomX = zoomCapturedPhotoX,
                ZoomY = zoomCapturedPhotoY,
                ImageBitmap =
                    new LoadedImageSource(captured
                        .Image) //must not dispose bitmap after that, it's used by preview outside
            };

            if (captured.Rotation != 0)
            {
                var transfromRotation = (float)captured.Rotation;
                if (captured.Facing == CameraPosition.Selfie)
                {
                    transfromRotation = (float)((360 - captured.Rotation) % 360);
                }

                image.Rotation = -transfromRotation;
            }

            createdImage?.Invoke(image);

            var ctx = new DrawingContext(context, destination, 1, null);
            image.Render(ctx);
            overlay.Render(ctx.WithScale(scaleOverlay));

            surface.Canvas.Flush();
            return surface.Snapshot();
        }
    }


    #region Measure Brightness

    /// <summary>
    /// Analyzes pixel luminance in a specific area of the frame (shared across all platforms)
    /// </summary>
    /// <param name="frame">The camera frame to analyze</param>
    /// <param name="meteringMode">Spot (10x10 points) or CenterWeighted (50x50 points)</param>
    /// <param name="renderingScale">Rendering scale to convert points to pixels</param>
    /// <returns>Average luminance value (0-255 scale)</returns>
    public double AnalyzeFrameLuminance(SKImage frame, MeteringMode meteringMode)
    {
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));

        float renderingScale = this.RenderingScale;
        var width = frame.Width;
        var height = frame.Height;

        // Define sampling area based on metering mode - in points, then convert to pixels
        int sampleSizePoints = meteringMode == MeteringMode.Spot ? 60 : 300;
        int sampleSizePixels = (int)(sampleSizePoints * renderingScale);

        int centerX = width / 2;
        int centerY = height / 2;

        int startX = Math.Max(0, centerX - sampleSizePixels / 2);
        int startY = Math.Max(0, centerY - sampleSizePixels / 2);
        int endX = Math.Min(width, centerX + sampleSizePixels / 2);
        int endY = Math.Min(height, centerY + sampleSizePixels / 2);

        System.Diagnostics.Debug.WriteLine($"[SHARED CAMERA] Analyzing frame: {width}x{height}");
        System.Diagnostics.Debug.WriteLine(
            $"[SHARED CAMERA] Sampling: {sampleSizePoints}x{sampleSizePoints} pts * {renderingScale:F1} = {sampleSizePixels}x{sampleSizePixels} px");
        System.Diagnostics.Debug.WriteLine($"[SHARED CAMERA] Sampling area: ({startX},{startY}) to ({endX},{endY})");

        // Sample pixels from the target area
        using var bitmap = SKBitmap.FromImage(frame);

        double totalLuminance = 0;
        int pixelCount = 0;

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                var pixel = bitmap.GetPixel(x, y);

                // Calculate luminance using standard formula: 0.299*R + 0.587*G + 0.114*B
                var luminance = (0.299 * pixel.Red + 0.587 * pixel.Green + 0.114 * pixel.Blue);
                totalLuminance += luminance;
                pixelCount++;
            }
        }

        if (pixelCount == 0)
            throw new InvalidOperationException("No pixels to analyze in the specified area");

        var averageLuminance = totalLuminance / pixelCount;
        System.Diagnostics.Debug.WriteLine(
            $"[SHARED CAMERA] Average luminance: {averageLuminance:F1} (0-255 scale), pixels: {pixelCount}");

        return averageLuminance;
    }

    /// <summary>
    /// Converts normalized luminance to estimated lux value (shared across all platforms)
    /// </summary>
    /// <param name="pixelLuminance">Raw pixel luminance (0-255)</param>
    /// <param name="exposureDuration">Camera exposure duration in seconds</param>
    /// <param name="iso">Camera ISO value</param>
    /// <param name="aperture">Camera aperture (f-number)</param>
    /// <returns>Estimated brightness in lux</returns>
    public static double CalculateBrightnessFromExposure(double pixelLuminance, double exposureDuration, float iso,
        float aperture)
    {
        // Normalize pixel luminance to account for camera exposure settings
        // Formula: Actual_Luminance = Pixel_Luminance * (ISO/100) * (1/exposure_duration) / (aperture^2)
        var normalizedLuminance = pixelLuminance * (iso / 100.0) * (1.0 / exposureDuration) / (aperture * aperture);

        System.Diagnostics.Debug.WriteLine(
            $"[SHARED CAMERA] Exposure compensation: Duration={exposureDuration:F6}s, ISO={iso:F0}, Aperture=f/{aperture:F1}");
        System.Diagnostics.Debug.WriteLine(
            $"[SHARED CAMERA] Raw luminance: {pixelLuminance:F1} → Normalized: {normalizedLuminance:F1}");

        // Convert normalized luminance to lux using calibrated scale
        double estimatedLux;

        if (normalizedLuminance < 1)
        {
            // Very dark: 0.1 - 1 lux (moonlight, deep shadow)
            estimatedLux = 0.1 + normalizedLuminance * 0.9;
        }
        else if (normalizedLuminance < 10)
        {
            // Dark: 1 - 10 lux (candlelight, dim room)
            estimatedLux = 1 + (normalizedLuminance - 1) * 1.0;
        }
        else if (normalizedLuminance < 100)
        {
            // Medium: 10 - 100 lux (living room, restaurant)
            estimatedLux = 10 + (normalizedLuminance - 10) * 1.0;
        }
        else if (normalizedLuminance < 1000)
        {
            // Bright: 100 - 1000 lux (office, bright room)
            estimatedLux = 100 + (normalizedLuminance - 100) * 1.0;
        }
        else if (normalizedLuminance < 10000)
        {
            // Very bright: 1000 - 10000 lux (daylight, bright office)
            estimatedLux = 1000 + (normalizedLuminance - 1000) * 1.0;
        }
        else
        {
            // Extremely bright: 10000+ lux (direct sunlight)
            estimatedLux = 10000 + (normalizedLuminance - 10000) * 10.0;
        }

        System.Diagnostics.Debug.WriteLine($"[SHARED CAMERA] Final estimated brightness: {estimatedLux:F0} lux");
        return estimatedLux;
    }

    /// <summary>
    /// Direct pixel-to-lux mapping for platforms where camera exposure settings are unreliable
    /// </summary>
    /// <param name="pixelLuminance">Raw pixel luminance (0-255)</param>
    /// <returns>Estimated brightness in lux</returns>
    public static double CalculateBrightnessFromPixelsOnly(double pixelLuminance)
    {
        System.Diagnostics.Debug.WriteLine($"[SHARED CAMERA] Direct pixel mapping: {pixelLuminance:F1} (0-255 scale)");

        // Direct pixel luminance to lux mapping with wide dynamic range
        double estimatedLux;

        if (pixelLuminance < 5)
        {
            // Very dark: 0.1 - 5 lux (deep shadow, moonlight)
            estimatedLux = 0.1 + (pixelLuminance / 5.0) * 4.9;
        }
        else if (pixelLuminance < 25)
        {
            // Dark: 5 - 50 lux (dim room, candlelight)
            estimatedLux = 5 + ((pixelLuminance - 5) / 20.0) * 45;
        }
        else if (pixelLuminance < 75)
        {
            // Medium: 50 - 500 lux (living room, restaurant)
            estimatedLux = 50 + ((pixelLuminance - 25) / 50.0) * 450;
        }
        else if (pixelLuminance < 150)
        {
            // Bright: 500 - 2000 lux (office, bright room)
            estimatedLux = 500 + ((pixelLuminance - 75) / 75.0) * 1500;
        }
        else if (pixelLuminance < 200)
        {
            // Very bright: 2000 - 10000 lux (daylight indoors)
            estimatedLux = 2000 + ((pixelLuminance - 150) / 50.0) * 8000;
        }
        else
        {
            // Extremely bright: 10000+ lux (direct sunlight)
            estimatedLux = 10000 + ((pixelLuminance - 200) / 55.0) * 90000;
        }

        System.Diagnostics.Debug.WriteLine($"[SHARED CAMERA] Direct pixel brightness: {estimatedLux:F0} lux");
        return estimatedLux;
    }

#if ONPLATFORM

    /// <summary>
    /// Measures scene brightness using adaptive exposure bracketing to handle extreme lighting conditions.
    /// For bright outdoor conditions, uses progressively faster shutter speeds and lower ISO until non-clipped data is obtained.
    /// Falls back to histogram analysis of darkest pixels when complete saturation occurs.
    /// </summary>
    /// <param name="meteringMode">Spot (10x10 points) or CenterWeighted (50x50 points)</param>
    /// <returns>Brightness measurement result</returns>
    public async Task<BrightnessResult> MeasureSceneBrightness(MeteringMode meteringMode, SKImage autoFrame)
    {
        try
        {
            if (NativeControl == null)
                return new BrightnessResult { Success = false, ErrorMessage = "Camera not initialized" };

            System.Diagnostics.Debug.WriteLine(
                $"[SHARED CAMERA] Starting adaptive brightness measurement with {meteringMode} mode");

            if (autoFrame == null)
                return new BrightnessResult { Success = false, ErrorMessage = "Could not capture frame for analysis" };

            // Check auto exposure metadata to understand lighting conditions
            var autoISO = CameraDevice.Meta.ISO;
            var autoShutter = CameraDevice.Meta.Shutter;
            var autoAperture = CameraDevice.Meta.Aperture;

            System.Diagnostics.Debug.WriteLine(
                $"[SHARED CAMERA] Auto exposure detected: ISO {autoISO}, Shutter {autoShutter}s, Aperture f/{autoAperture}");


            //#if IOS
            // Get possible exposure ranges
            //            var exposureRange = NativeControl.GetExposureRange();
            //            if (exposureRange.IsManualExposureSupported)
            //            {
            //                // Use adaptive exposure bracketing for accurate measurement
            //                var result = await MeasureWithAdaptiveExposure(meteringMode, autoISO, autoShutter, exposureRange);
            //                return result;
            //            }
            //#endif
            // Fallback to direct pixel analysis when manual exposure is not supported
            System.Diagnostics.Debug.WriteLine(
                "[SHARED CAMERA] Manual exposure not supported - using direct pixel approach");

            {
                var pixelLuminance = AnalyzeFrameLuminance(autoFrame, meteringMode);
                var brightness = CalculateBrightnessFromPixelsOnly(pixelLuminance);

                System.Diagnostics.Debug.WriteLine($"[SHARED CAMERA] Direct pixel brightness: {brightness:F0} lux");

                return new BrightnessResult { Success = true, Brightness = brightness };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SHARED CAMERA ERROR] MeasureSceneBrightness failed: {ex.Message}");
            return new BrightnessResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Measures brightness using adaptive exposure bracketing to handle extreme lighting conditions
    /// </summary>
    private async Task<BrightnessResult> MeasureWithAdaptiveExposure(MeteringMode meteringMode, double autoISO,
        double autoShutter, CameraManualExposureRange exposureRange)
    {
        System.Diagnostics.Debug.WriteLine(
            "[SHARED CAMERA] Starting adaptive exposure measurement (aggressive-to-conservative)");

        // Define exposure bracketing sequence from conservative to aggressive
        var exposureSequence = new List<(float iso, float shutter, string description)>();

        // SMART STARTING POINT: Very bright outdoor start dark, Indoor/low light start moderate
        if (autoISO < 100 && autoShutter > 1 / 200f)
        {
            // Very bright outdoor conditions - start darkest to avoid white screen
            if (exposureRange.MinISO <= 25)
                exposureSequence.Add((25f, 1 / 1000f, "darkest"));
            exposureSequence.Add((50f, 1 / 1000f, "very dark"));
            exposureSequence.Add((50f, 1 / 500f, "dark"));
            exposureSequence.Add((50f, 1 / 250f, "moderate"));
            exposureSequence.Add((100f, 1 / 125f, "bright"));
            exposureSequence.Add((100f, 1 / 60f, "brightest"));
        }
        else if (autoISO > 400 || autoShutter < 1 / 60f)
        {
            // Dark conditions - start brighter to get measurable data
            exposureSequence.Add((400f, 1 / 30f, "brightest"));
            exposureSequence.Add((200f, 1 / 60f, "moderate"));
            exposureSequence.Add((100f, 1 / 125f, "darker"));
        }
        else
        {
            // Indoor/moderate conditions - start moderate, go both ways
            exposureSequence.Add((100f, 1 / 125f, "moderate"));
            exposureSequence.Add((100f, 1 / 60f, "brighter"));
            exposureSequence.Add((200f, 1 / 60f, "brightest"));
            exposureSequence.Add((50f, 1 / 250f, "darker"));
            exposureSequence.Add((50f, 1 / 500f, "darkest"));
        }

        // Try each exposure setting until we get good data (not clipped, not too dark)
        for (int i = 0; i < exposureSequence.Count; i++)
        {
            var (iso, shutter, description) = exposureSequence[i];

            // Constrain to hardware limits
            var constrainedISO = Math.Max(exposureRange.MinISO, Math.Min(exposureRange.MaxISO, iso));
            var constrainedShutter = Math.Max(exposureRange.MinShutterSpeed,
                Math.Min(exposureRange.MaxShutterSpeed, shutter));

            System.Diagnostics.Debug.WriteLine(
                $"[SHARED CAMERA] Attempt {i + 1}: {description} - ISO {constrainedISO}, Shutter {constrainedShutter}s");

            var result = await TryExposureSettings(meteringMode, constrainedISO, constrainedShutter);

            if (result.Success)
            {
                if (result.IsClipped)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SHARED CAMERA] Attempt {i + 1} overexposed (white screen), trying next DARKER setting");
                    continue; // This should never happen since we start dark, but just in case
                }
                else if (result.IsTooDark)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SHARED CAMERA] Attempt {i + 1} too dark, trying next LIGHTER setting");
                    continue; // Move to next setting which should be lighter
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SHARED CAMERA] Successful measurement with {description}: {result.Brightness:F0} lux");
                    return new BrightnessResult { Success = true, Brightness = result.Brightness };
                }
            }
        }

        // If all attempts failed (clipped or too dark), use histogram analysis on the best available data
        System.Diagnostics.Debug.WriteLine("[SHARED CAMERA] All exposure attempts failed - using histogram analysis");
        return await FallbackHistogramAnalysis(meteringMode);
    }

    /// <summary>
    /// Tries a specific exposure setting and returns measurement result with clipping and darkness detection
    /// </summary>
    private async Task<(bool Success, double Brightness, bool IsClipped, bool IsTooDark)> TryExposureSettings(
        MeteringMode meteringMode, float iso, float shutter)
    {
        try
        {
            // Set manual exposure
            bool exposureSet = NativeControl.SetManualExposure(iso, shutter);
            if (!exposureSet)
            {
                return (false, 0, false, false);
            }

            // Wait for camera to apply settings and flush old frames
            await Task.Delay(1500);

            // Flush any old frames from buffer by capturing and discarding several frames
            for (int flushAttempt = 0; flushAttempt < 3; flushAttempt++)
            {
                var flushFrame = NativeControl.GetPreviewImage();
                flushFrame?.Dispose();
                await Task.Delay(200);
            }

            // Now capture frame with validated exposure settings
            SKImage frame = null;
            bool frameValidated = false;

            for (int attempts = 0; attempts < 8; attempts++)
            {
                frame?.Dispose(); // Dispose previous attempt
                frame = NativeControl.GetPreviewImage();

                if (frame != null)
                {
                    // Validate that the frame was captured with the expected exposure settings
                    if (ValidateFrameExposure(iso, shutter))
                    {
                        frameValidated = true;
                        break;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[EXPOSURE VALIDATION] Frame {attempts + 1} has wrong exposure settings, retrying...");
                    }
                }

                await Task.Delay(150);
            }

            if (frame == null || !frameValidated)
            {
                frame?.Dispose();
                System.Diagnostics.Debug.WriteLine(
                    $"[EXPOSURE ERROR] Failed to capture validated frame after 8 attempts");
                return (false, 0, false, false);
            }

            using (frame)
            {
                // Analyze luminance and check for clipping and darkness
                var pixelLuminance = AnalyzeFrameLuminance(frame, meteringMode);
                var clippingInfo = AnalyzeClipping(frame, meteringMode);

                System.Diagnostics.Debug.WriteLine(
                    $"[SHARED CAMERA] Luminance: {pixelLuminance:F1}, Clipped pixels: {clippingInfo.ClippedPercentage:F1}%");

                // Consider it clipped if more than 80% of sampled pixels are near maximum
                bool isClipped = clippingInfo.ClippedPercentage > 80;

                // Consider it too dark if average luminance is very low (< 10 on 0-255 scale)
                bool isTooDark = pixelLuminance < 10;

                if (!isClipped && !isTooDark)
                {
                    // Good exposure - calculate brightness using exposure settings
                    var actualISO = CameraDevice.Meta.ISO;
                    var actualShutter = CameraDevice.Meta.Shutter;
                    var actualAperture = CameraDevice.Meta.Aperture;

                    var brightness =
                        CalculateSceneBrightnessFromPixels(pixelLuminance, actualISO.GetValueOrDefault(), actualAperture.GetValueOrDefault(), actualShutter.GetValueOrDefault());
                    return (true, brightness, false, false);
                }
                else
                {
                    return (true, 0, isClipped, isTooDark);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SHARED CAMERA] TryExposureSettings failed: {ex.Message}");
            return (false, 0, false, false);
        }
        finally
        {
            // Always restore auto exposure
            NativeControl.SetAutoExposure();
        }
    }

    /// <summary>
    /// Validates that the current camera exposure settings match the expected values
    /// </summary>
    /// <param name="expectedISO">Expected ISO value</param>
    /// <param name="expectedShutter">Expected shutter speed in seconds</param>
    /// <returns>True if the frame was captured with the expected exposure settings</returns>
    private bool ValidateFrameExposure(float expectedISO, float expectedShutter)
    {
        try
        {
            var meta = CameraDevice?.Meta;
            if (meta == null)
            {
                System.Diagnostics.Debug.WriteLine("[EXPOSURE VALIDATION] No camera metadata available");
                return false; // If we can't validate, assume it's wrong
            }

            var actualISO = meta.ISO;
            var actualShutter = meta.Shutter;

            // Allow some tolerance for floating point comparison and camera rounding
            var isoTolerance = Math.Max(50, expectedISO * 0.1f); // 10% tolerance or minimum 50
            var shutterTolerance = Math.Max(0.001f, expectedShutter * 0.15f); // 15% tolerance or minimum 1ms

            bool isoMatches = Math.Abs(actualISO.GetValueOrDefault() - expectedISO) <= isoTolerance;
            bool shutterMatches = Math.Abs(actualShutter.GetValueOrDefault() - expectedShutter) <= shutterTolerance;

            if (isoMatches && shutterMatches)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EXPOSURE VALIDATION] ✓ Frame validated - Expected: ISO{expectedISO}, {expectedShutter}s | Actual: ISO{actualISO}, {actualShutter}s");
                return true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EXPOSURE VALIDATION] ✗ Frame mismatch - Expected: ISO{expectedISO}, {expectedShutter}s | Actual: ISO{actualISO}, {actualShutter}s");
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EXPOSURE VALIDATION] Error validating frame: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Analyzes pixel clipping in the metering area
    /// </summary>
    private (double ClippedPercentage, double AverageOfNonClipped) AnalyzeClipping(SKImage frame,
        MeteringMode meteringMode)
    {
        var renderingScale = GetRenderingScaleFor(frame.Width, frame.Height);
        var sampleSizePoints = meteringMode == MeteringMode.Spot ? 10 : 50;
        var sampleSizePixels = (int)(sampleSizePoints * renderingScale);

        var width = frame.Width;
        var height = frame.Height;
        var startX = (width - sampleSizePixels) / 2;
        var startY = (height - sampleSizePixels) / 2;
        var endX = startX + sampleSizePixels;
        var endY = startY + sampleSizePixels;

        using var bitmap = SKBitmap.FromImage(frame);

        int totalPixels = 0;
        int clippedPixels = 0;
        double totalNonClippedLuminance = 0;
        int nonClippedCount = 0;

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                var luminance = (0.299 * pixel.Red + 0.587 * pixel.Green + 0.114 * pixel.Blue);

                totalPixels++;

                // Consider pixels clipped if they're very close to maximum (250+)
                if (luminance >= 250)
                {
                    clippedPixels++;
                }
                else
                {
                    totalNonClippedLuminance += luminance;
                    nonClippedCount++;
                }
            }
        }

        var clippedPercentage = totalPixels > 0 ? (clippedPixels * 100.0 / totalPixels) : 0;
        var averageNonClipped = nonClippedCount > 0 ? (totalNonClippedLuminance / nonClippedCount) : 0;

        return (clippedPercentage, averageNonClipped);
    }

    /// <summary>
    /// Fallback analysis using histogram of darkest available pixels
    /// </summary>
    private async Task<BrightnessResult> FallbackHistogramAnalysis(MeteringMode meteringMode)
    {
        try
        {
            // Use the most aggressive settings we can for final attempt
            var exposureRange = NativeControl.GetExposureRange();
            var minISO = Math.Max(25, exposureRange.MinISO);
            var fastestShutter = Math.Max(1 / 1000f, exposureRange.MinShutterSpeed);

            System.Diagnostics.Debug.WriteLine(
                $"[SHARED CAMERA] Fallback analysis with ISO {minISO}, Shutter {fastestShutter}s");

            bool exposureSet = NativeControl.SetManualExposure(minISO, fastestShutter);
            if (exposureSet)
            {
                await Task.Delay(1000);
            }

            SKImage frame = null;
            for (int attempts = 0; attempts < 5; attempts++)
            {
                frame = NativeControl.GetPreviewImage();
                if (frame != null)
                    break;
                await Task.Delay(100);
            }

            if (frame == null)
            {
                return new BrightnessResult
                {
                    Success = false, ErrorMessage = "Could not capture frame for fallback analysis"
                };
            }

            using (frame)
            {
                var clippingInfo = AnalyzeClipping(frame, meteringMode);

                if (clippingInfo.AverageOfNonClipped > 0)
                {
                    // Use the darkest pixels we could find to estimate brightness
                    var actualISO = CameraDevice.Meta.ISO;
                    var actualShutter = CameraDevice.Meta.Shutter;
                    var actualAperture = CameraDevice.Meta.Aperture;

                    // Extrapolate from non-clipped pixels - assume they represent shadows in very bright scene
                    var estimatedSceneLuminance =
                        clippingInfo.AverageOfNonClipped * 3; // Assume shadows are ~1/3 of scene brightness
                    var brightness = CalculateSceneBrightnessFromPixels(estimatedSceneLuminance, actualISO.GetValueOrDefault(),
                        actualAperture.GetValueOrDefault(), actualShutter.GetValueOrDefault());

                    // For extremely bright conditions, apply a multiplier since we're measuring shadows
                    if (clippingInfo.ClippedPercentage > 95)
                    {
                        brightness *= 5; // Very bright outdoor conditions
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[SHARED CAMERA] Fallback estimate: {brightness:F0} lux (from {clippingInfo.ClippedPercentage:F1}% clipped)");

                    return new BrightnessResult
                    {
                        Success = true, Brightness = Math.Min(brightness, 200000)
                    }; // Cap at reasonable maximum
                }
                else
                {
                    // Everything is clipped - return maximum estimate
                    System.Diagnostics.Debug.WriteLine(
                        "[SHARED CAMERA] Complete saturation detected - returning maximum estimate");
                    return new BrightnessResult { Success = true, Brightness = 100000 }; // Bright daylight estimate
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SHARED CAMERA] Fallback analysis failed: {ex.Message}");
            return new BrightnessResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            NativeControl.SetAutoExposure();
        }
    }

#endif

    /// <summary>
    /// Calculates actual scene brightness using the camera's current settings and pixel analysis
    /// </summary>
    /// <param name="pixelLuminance">Average pixel luminance from AnalyzeFrameLuminance (0-255 scale)</param>
    /// <param name="iso">Current camera ISO</param>
    /// <param name="aperture">Current camera aperture (f-stop)</param>
    /// <param name="shutter">Current camera shutter speed (seconds)</param>
    /// <returns>Estimated scene brightness in lux</returns>
    public static double CalculateSceneBrightnessFromPixels(
        double pixelLuminance,
        double iso,
        double aperture,
        double shutter)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[SHARED CAMERA] CalculateSceneBrightnessFromPixels: pixels {pixelLuminance:F0}, iso {iso:0}, aperture {aperture:0.00}, shutter {shutter:0.0000}");

        // Camera exposure settings tell us what the camera thinks is "proper exposure"
        // This represents the brightness level the camera is targeting (middle gray = 18% reflectance)

        // Calculate the EV that the camera is using for "proper exposure"
        double cameraEV = Math.Log2((aperture * aperture) / shutter) + Math.Log2(iso / 100.0);

        // Convert camera EV to the luminance it's targeting (what it thinks middle gray should be)
        const double K = 12.5; // Standard photometric constant
        double cameraTargetLuminance = K * Math.Pow(2, cameraEV);

        System.Diagnostics.Debug.WriteLine(
            $"[SHARED CAMERA] Camera EV: {cameraEV:F1}, Target luminance: {cameraTargetLuminance:F0}");

        // Now use actual pixel values to determine how bright the scene really is
        // Middle gray (18% reflectance) should appear as ~128 on 0-255 scale
        // If pixels are darker/brighter than 128, the scene is darker/brighter than camera's target
        double actualBrightnessRatio = pixelLuminance / 128.0;

        // Apply gamma correction - camera sensors apply ~2.2 gamma curve
        // This converts from camera's gamma-corrected values back to linear light
        actualBrightnessRatio = Math.Pow(Math.Max(0.001, actualBrightnessRatio), 2.2);

        // Calculate final scene brightness
        // If ratio = 1.0: scene matches camera's expectation
        // If ratio < 1.0: scene is darker than camera expected
        // If ratio > 1.0: scene is brighter than camera expected
        double finalSceneBrightness = cameraTargetLuminance * actualBrightnessRatio;

        System.Diagnostics.Debug.WriteLine(
            $"[SHARED CAMERA] Brightness ratio: {actualBrightnessRatio:F3}, Final: {finalSceneBrightness:F0} lux");

        return finalSceneBrightness;
    }

    #endregion
}
