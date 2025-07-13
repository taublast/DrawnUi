using SkiaSharp;

namespace DrawnUi.Camera;


/// <summary>
/// This bitmaps comes zoomed with hardware and NOT zoomed with TextureScale, you nave to do it yourself
/// </summary>
public class CapturedImage : IDisposable
{
    public SKImage Image { get; set; }

    public Metadata Meta { get; set; }

    public int Rotation { get; set; }

    public CameraPosition Facing { get; set; }

    /// <summary>
    /// Device local time will be set
    /// </summary>
    public DateTime Time { get; set; }

    public string Path { get; set; }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;

            Image?.Dispose();
            Image = null;
        }
    }

    public bool IsDisposed
    {
        get; protected set;
    }
}
