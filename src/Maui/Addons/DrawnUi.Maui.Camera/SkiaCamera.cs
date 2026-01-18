global using DrawnUi.Draw;
global using SkiaSharp;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AppoMobi.Specials;
using DrawnUi.Views;
using Color = Microsoft.Maui.Graphics.Color;

#if WINDOWS
using DrawnUi.Camera.Platforms.Windows; 
#elif IOS || MACCATALYST
// READ SkiaCameraIfApple.cs !!!!!
#endif

namespace DrawnUi.Camera;

/// <summary>
/// SkiaCamera control with support for manual camera selection.
///
/// Basic usage:
/// - Set Facing to Default/Selfie for automatic camera selection
/// - Set Facing to Manual and CameraIndex for manual camera selection
///
/// Example:
/// var camera = new SkiaCamera { Facing = CameraPosition.Manual, CameraIndex = 2 };
/// var cameras = await camera.GetAvailableCamerasAsync();
/// </summary>
public partial class SkiaCamera : SkiaControl
{

#if !IOS && !MACCATALYST


    #region VIDEO RECORDING METHODS

 


    private int _frameInFlight = 0;

    /// <summary>
    /// Enable on-screen diagnostics overlay (effective FPS, dropped frames, last submit ms)
    /// during capture video flow to validate performance.
    /// </summary>

    /// <summary>
    /// Mirror diagnostics toggle used in drawing overlays
    /// </summary>
    public bool VideoDiagnosticsOn
    {
        get => EnableCaptureDiagnostics;
        set => EnableCaptureDiagnostics = value;
    }

    private long _diagDroppedFrames = 0;
    private long _diagSubmittedFrames = 0;
    private double _diagLastSubmitMs = 0;
    private DateTime _diagStartTime;
    private int _diagEncWidth = 0, _diagEncHeight = 0;
    private long _diagBitrate = 0;

    /// <summary>
    /// Current encoder width (for preview scale calculation)
    /// </summary>
    public int EncoderWidth => _diagEncWidth;

    /// <summary>
    /// Current encoder height (for preview scale calculation)
    /// </summary>
    public int EncoderHeight => _diagEncHeight;

    // Rolling average FPS calculation for encoder output (same approach as DrawnView)
    private double _diagFpsAverage;
    private int _diagFpsCount;
    private long _diagLastFrameTimestamp;
    private double _diagReportFps;

    // Camera input FPS tracking (frames arriving from camera)
    private double _diagInputFpsAverage;
    private int _diagInputFpsCount;
    private long _diagLastInputFrameTimestamp;
    private double _diagInputReportFps;

    /// <summary>
    /// Calculates the recording FPS using rolling average over N frames.
    /// Same approach as DrawnView uses for display FPS.
    /// </summary>
    /// <param name="averageAmount">Number of frames over which to average. Default is 10.</param>
    private void CalculateRecordingFps(int averageAmount = 10)
    {
        long currentTimestamp = Super.GetCurrentTimeNanos();

        if (_diagLastFrameTimestamp == 0)
        {
            // First frame - just record timestamp, can't calculate FPS yet
            _diagLastFrameTimestamp = currentTimestamp;
            return;
        }

        // Convert nanoseconds to seconds for elapsed time calculation
        double elapsedSeconds = (currentTimestamp - _diagLastFrameTimestamp) / 1_000_000_000.0;
        _diagLastFrameTimestamp = currentTimestamp;

        // Avoid division by zero or unrealistic values
        if (elapsedSeconds <= 0 || elapsedSeconds > 1.0)
            return;

        double currentFps = 1.0 / elapsedSeconds;

        _diagFpsAverage = ((_diagFpsAverage * _diagFpsCount) + currentFps) / (_diagFpsCount + 1);
        _diagFpsCount++;

        if (_diagFpsCount >= averageAmount)
        {
            _diagReportFps = _diagFpsAverage;
            _diagFpsCount = 0;
            _diagFpsAverage = 0.0;
        }
    }

    /// <summary>
    /// Calculates the camera input FPS using rolling average over N frames.
    /// Called when a frame arrives from the camera (before processing).
    /// </summary>
    /// <param name="averageAmount">Number of frames over which to average. Default is 10.</param>
    private void CalculateCameraInputFps(int averageAmount = 10)
    {
        long currentTimestamp = Super.GetCurrentTimeNanos();

        if (_diagLastInputFrameTimestamp == 0)
        {
            // First frame - just record timestamp, can't calculate FPS yet
            _diagLastInputFrameTimestamp = currentTimestamp;
            return;
        }

        // Convert nanoseconds to seconds for elapsed time calculation
        double elapsedSeconds = (currentTimestamp - _diagLastInputFrameTimestamp) / 1_000_000_000.0;
        _diagLastInputFrameTimestamp = currentTimestamp;

        // Avoid division by zero or unrealistic values
        if (elapsedSeconds <= 0 || elapsedSeconds > 1.0)
            return;

        double currentFps = 1.0 / elapsedSeconds;

        _diagInputFpsAverage = ((_diagInputFpsAverage * _diagInputFpsCount) + currentFps) / (_diagInputFpsCount + 1);
        _diagInputFpsCount++;

        if (_diagInputFpsCount >= averageAmount)
        {
            _diagInputReportFps = _diagInputFpsAverage;
            _diagInputFpsCount = 0;
            _diagInputFpsAverage = 0.0;
        }
    }

    /// <summary>
    /// Resets the FPS calculation state. Called when starting a new recording.
    /// </summary>
    private void ResetRecordingFps()
    {
        // Reset encoder output FPS
        _diagFpsAverage = 0;
        _diagFpsCount = 0;
        _diagLastFrameTimestamp = 0;
        _diagReportFps = 0;

        // Reset camera input FPS
        _diagInputFpsAverage = 0;
        _diagInputFpsCount = 0;
        _diagLastInputFrameTimestamp = 0;
        _diagInputReportFps = 0;
    }

    private void DrawDiagnostics(SKCanvas canvas, int width, int height)
    {
        if (!EnableCaptureDiagnostics || canvas == null)
            return;

        var inputFps = _diagInputReportFps;
        var outputFps = _diagReportFps;

        // Get raw camera FPS from native control
        double rawCamFps = 0;
#if WINDOWS
        if (NativeControl is NativeCamera winCam)
        {
            rawCamFps = winCam.RawCameraFps;
        }
#endif

        // Compose text - show both raw and processed FPS
        string line1 = rawCamFps > 0
            ? $"raw: {rawCamFps:F1}  cam: {inputFps:F1}  enc: {outputFps:F1} / {_targetFps}"
            : $"cam: {inputFps:F1}  enc: {outputFps:F1} / {_targetFps}";
        string line2 = $"dropped: {_diagDroppedFrames}  submit: {_diagLastSubmitMs:F1} ms";
        double mbps = _diagBitrate > 0 ? _diagBitrate / 1_000_000.0 : 0.0;
        string line3 = _diagEncWidth > 0 && _diagEncHeight > 0
            ? $"rec: {_diagEncWidth}x{_diagEncHeight}@{_targetFps}  bitrate: {mbps:F1} Mbps"
            : $"bitrate: {mbps:F1} Mbps";

        using var bgPaint = new SKPaint { Color = new SKColor(0, 0, 0, 140), IsAntialias = true };
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            TextSize = Math.Max(14, width / 60f)
        };

        var pad = 8f;
        var y1 = pad + textPaint.TextSize;
        var y2 = y1 + textPaint.TextSize + 4f;
        var y3 = y2 + textPaint.TextSize + 4f;
        var maxTextWidth = Math.Max(textPaint.MeasureText(line1),
            Math.Max(textPaint.MeasureText(line2), textPaint.MeasureText(line3)));
        var rect = new SKRect(pad, pad, pad + maxTextWidth + pad, y3 + pad);

        canvas.Save();
        canvas.DrawRoundRect(rect, 6, 6, bgPaint);
        canvas.DrawText(line1, pad * 1.5f, y1, textPaint);
        canvas.DrawText(line2, pad * 1.5f, y2, textPaint);
        canvas.DrawText(line3, pad * 1.5f, y3, textPaint);
        canvas.Restore();
    }

 



    private DateTime _captureVideoTotalStartTime;

    #endregion

#endif

}
