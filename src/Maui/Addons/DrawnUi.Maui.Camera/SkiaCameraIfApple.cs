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
using AVFoundation;
using CoreMedia;
using Foundation;
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


#if IOS || MACCATALYST


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

    private void DrawDiagnostics(SKCanvas canvas, int width, int height)
    {
        if (!EnableCaptureDiagnostics || canvas == null)
            return;

        var elapsed = (DateTime.Now - _diagStartTime).TotalSeconds;
        var effFps = elapsed > 0 ? _diagSubmittedFrames / elapsed : 0;

        // Get raw camera FPS from native control
        double rawCamFps = 0;
        if (NativeControl is NativeCamera nativeCam)
        {
            rawCamFps = nativeCam.RawCameraFps;
        }

        // Compose text - show both raw and processed FPS
        string line1 = rawCamFps > 0
            ? $"raw: {rawCamFps:F1}  enc: {effFps:F1} / {_targetFps}  dropped: {_diagDroppedFrames}"
            : $"FPS: {effFps:F1} / {_targetFps}  dropped: {_diagDroppedFrames}";
        string line2 = $"submit: {_diagLastSubmitMs:F1} ms";
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



 

    #endregion

#endif

}
