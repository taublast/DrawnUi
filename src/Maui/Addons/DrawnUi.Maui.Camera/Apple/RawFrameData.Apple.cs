
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AppoMobi.Specials;
using AVFoundation;
using CoreFoundation;
using CoreGraphics;
using CoreImage;
using CoreMedia;
using CoreVideo;
using DrawnUi.Controls;
using Foundation;
using ImageIO;
using Microsoft.Maui.Media;
using Photos;
using SkiaSharp;
using SkiaSharp.Views.iOS;
using UIKit;
using static AVFoundation.AVMetadataIdentifiers;

namespace DrawnUi.Camera;


// Lightweight container for raw frame data - no SKImage creation
public class RawFrameData : IDisposable
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int BytesPerRow { get; set; }
    public DateTime Time { get; set; }
    public Rotation CurrentRotation { get; set; }
    public CameraPosition Facing { get; set; }
    public int Orientation { get; set; }
    public byte[] PixelData { get; set; } // Copy pixel data to avoid CVPixelBuffer lifetime issues

    public void Dispose()
    {
        PixelData = null; // Let GC handle byte array
    }
}
