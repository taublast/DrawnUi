using System.Diagnostics;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace DrawnUi.Testing;

/// <summary>
/// Headless DrawnUI host for automated testing. Renders a <see cref="Canvas"/> to an off-screen
/// SkiaSharp surface and pumps frames synchronously on a deterministic monotonic clock, so gesture
/// delivery (flushed in <c>ExecuteBeforeDraw</c>) and animations/fling (ticked in <c>ExecuteAnimators</c>
/// off <c>FrameTimeNanos</c>) advance predictably without a window or render loop.
///
/// Pair with <see cref="GestureRobot"/> to simulate taps, panning, flinging and wheel scrolling.
/// </summary>
public sealed class HeadlessCanvasHost : IDisposable
{
    private static readonly object _superLock = new();
    private static bool _superInitialized;

    private readonly SKSurface _surface;
    private readonly HeadlessDrawable _drawable;
    private long _frameNanos;

    /// <param name="width">Surface width in pixels.</param>
    /// <param name="height">Surface height in pixels.</param>
    /// <param name="scale">Rendering scale (density). 1 means points == pixels.</param>
    /// <param name="background">Canvas background color. Defaults to transparent.</param>
    public HeadlessCanvasHost(int width, int height, float scale = 1f, DrawnUi.Color? background = null)
    {
        lock (_superLock)
        {
            if (!_superInitialized)
            {
                Super.Init();
                _superInitialized = true;
            }
        }

        _surface = SKSurface.Create(new SKImageInfo(width, height));
        _drawable = new HeadlessDrawable(_surface);

        Canvas = new Canvas
        {
            WidthRequest = width / scale,
            HeightRequest = height / scale,
            BackgroundColor = background ?? Colors.Transparent
        };

        Canvas.AttachCanvasView(_drawable);
        Canvas.ConnectedHandler();

        if (scale > 0)
            Canvas.RenderingScale = scale;
    }

    /// <summary>The hosted canvas. Set <see cref="Canvas.Content"/> or <c>Children</c> then call <see cref="RenderFrame"/>.</summary>
    public Canvas Canvas { get; }

    /// <summary>Current synthetic frame timestamp in nanoseconds.</summary>
    public long FrameTimeNanos => _frameNanos;

    /// <summary>Rendering scale (density) of the hosted canvas.</summary>
    public float Scale => (float)Canvas.RenderingScale;

    /// <summary>
    /// Advances the synthetic clock by <paramref name="advanceMs"/> and renders one frame.
    /// Flushes any pending gesture delivery and ticks one animation step.
    /// </summary>
    public void RenderFrame(double advanceMs = 16.0)
    {
        if (advanceMs > 0)
            _frameNanos += (long)(advanceMs * 1_000_000.0);

        _drawable.PrepareFrame(_frameNanos);
        Canvas.RenderExternalSurface(
            _surface,
            new SKRect(0, 0, _drawable.CanvasSize.Width, _drawable.CanvasSize.Height),
            _frameNanos);
        // Mark the surface as drawn: DrawnView.OnFinalizeRendering forces IsDirty back to true while
        // the drawable reports HasDrawn == false, which would make NeedsFrame useless.
        _drawable.MarkDrawn();
    }

    /// <summary>
    /// True when something requested a redraw since the last <see cref="RenderFrame"/> (animators,
    /// <c>Update()</c>, async image arrival). False = the next frame would be pixel-identical; a
    /// server/streaming host can skip rendering and encoding it.
    /// </summary>
    public bool NeedsFrame => Canvas.IsDirty;

    /// <summary>Renders <paramref name="count"/> frames, each advancing the clock by <paramref name="advanceMs"/>.</summary>
    public void AdvanceFrames(int count, double advanceMs = 16.0)
    {
        for (var i = 0; i < count; i++)
            RenderFrame(advanceMs);
    }

    /// <summary>
    /// Fraction (0..1) of surface pixels whose colour differs from <paramref name="background"/> beyond
    /// <paramref name="tolerance"/> (per channel, 0..255). Near-zero on a populated scene = a blank/empty
    /// render. Render a frame first. Test-only diagnostic (per-pixel scan).
    /// </summary>
    public double NonBackgroundFraction(DrawnUi.Color background, int tolerance = 8)
    {
        using var image = _surface.Snapshot();
        using var bmp = SKBitmap.FromImage(image);

        int br = (int)Math.Round(background.Red * 255);
        int bg = (int)Math.Round(background.Green * 255);
        int bb = (int)Math.Round(background.Blue * 255);

        long differ = 0;
        long total = (long)bmp.Width * bmp.Height;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var p = bmp.GetPixel(x, y);
            if (Math.Abs(p.Red - br) > tolerance ||
                Math.Abs(p.Green - bg) > tolerance ||
                Math.Abs(p.Blue - bb) > tolerance)
                differ++;
        }

        return total == 0 ? 0 : (double)differ / total;
    }

    /// <summary>
    /// Per-row content signature (sum of R+G+B per pixel row) for rows [top..bottom). Cross-correlate
    /// signatures of consecutive frames to estimate the VERTICAL PIXEL SHIFT of rendered content —
    /// catches visual wobble/jumps that structure-level probes (HitRect) can't see (e.g. a stale cache
    /// plane blitted at the old position for a frame). Test-only diagnostic.
    /// </summary>
    public long[] RowSignature(int top, int bottom)
    {
        using var image = _surface.Snapshot();
        using var bmp = SKBitmap.FromImage(image);

        top = Math.Clamp(top, 0, bmp.Height);
        bottom = Math.Clamp(bottom, top, bmp.Height);
        var sig = new long[bottom - top];
        for (int y = top; y < bottom; y++)
        {
            long sum = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var p = bmp.GetPixel(x, y);
                sum += p.Red + p.Green + p.Blue;
            }
            sig[y - top] = sum;
        }

        return sig;
    }

    /// <summary>Best vertical shift dy in [-maxShift..maxShift] aligning <paramref name="prev"/> to
    /// <paramref name="cur"/> (minimal mean absolute difference over the overlap). Positive = content
    /// moved DOWN on screen since the previous frame.</summary>
    public static int EstimateVerticalShift(long[] prev, long[] cur, int maxShift = 40)
    {
        int best = 0;
        double bestCost = double.MaxValue;
        for (int dy = -maxShift; dy <= maxShift; dy++)
        {
            double cost = 0;
            int n = 0;
            for (int y = 0; y < cur.Length; y++)
            {
                int py = y - dy;
                if (py < 0 || py >= prev.Length) continue;
                cost += Math.Abs(cur[y] - prev[py]);
                n++;
            }
            if (n == 0) continue;
            cost /= n;
            if (cost < bestCost) { bestCost = cost; best = dy; }
        }

        return best;
    }

    /// <summary>
    /// Tallest fully-background horizontal strip (in px) that sits BETWEEN content rows — i.e. an empty
    /// band/hole in the middle of rendered content (not the empty margins above/below). A row counts as
    /// "background" when fewer than <paramref name="rowFillThreshold"/> of its pixels differ from the
    /// background. Used to catch a stale cache plane blitting cells with a gap.
    /// </summary>
    public int MaxInteriorEmptyBandPx(DrawnUi.Color background, int tolerance = 8, double rowFillThreshold = 0.02)
        => MaxInteriorEmptyBand(background, out _, out _, tolerance, rowFillThreshold);

    /// <summary>As <see cref="MaxInteriorEmptyBandPx"/>, also returning the band's [bandTop..bandBottom] px.</summary>
    public int MaxInteriorEmptyBand(DrawnUi.Color background, out int bandTop, out int bandBottom,
        int tolerance = 8, double rowFillThreshold = 0.02)
    {
        bandTop = bandBottom = -1;
        using var image = _surface.Snapshot();
        using var bmp = SKBitmap.FromImage(image);

        int br = (int)Math.Round(background.Red * 255);
        int bgc = (int)Math.Round(background.Green * 255);
        int bb = (int)Math.Round(background.Blue * 255);

        // mark each row as content (true) or background (false)
        var content = new bool[bmp.Height];
        for (int y = 0; y < bmp.Height; y++)
        {
            int differ = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (Math.Abs(p.Red - br) > tolerance || Math.Abs(p.Green - bgc) > tolerance ||
                    Math.Abs(p.Blue - bb) > tolerance)
                    differ++;
            }
            content[y] = differ > bmp.Width * rowFillThreshold;
        }

        int firstContent = Array.IndexOf(content, true);
        int lastContent = Array.LastIndexOf(content, true);
        if (firstContent < 0 || lastContent <= firstContent)
            return 0;

        int maxRun = 0, run = 0, runStart = 0;
        for (int y = firstContent; y <= lastContent; y++) // only BETWEEN content (ignore outer margins)
        {
            if (!content[y])
            {
                if (run == 0) runStart = y;
                run++;
                if (run > maxRun) { maxRun = run; bandTop = runStart; bandBottom = y; }
            }
            else run = 0;
        }
        return maxRun;
    }

    /// <summary>Saves the current surface contents to a PNG file (useful for visual diffing).</summary>
    /// <summary>Snapshot of the current surface (caller disposes). Encode it yourself for streaming.</summary>
    public SKImage Snapshot() => _surface.Snapshot();

    public void SavePng(string filePath)
    {
        using var image = _surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }

    public void Dispose()
    {
        Canvas.Dispose();
        _drawable.Dispose();
        _surface.Dispose();
    }

    private sealed class HeadlessDrawable : ISkiaDrawable
    {
        public HeadlessDrawable(SKSurface surface)
        {
            Surface = surface;
            CanvasSize = new SKSize(surface.Canvas.LocalClipBounds.Width, surface.Canvas.LocalClipBounds.Height);
            OnDraw = static (_, _) => false;
        }

        public Func<SKSurface, SKRect, bool> OnDraw { get; set; }
        public SKSurface Surface { get; }
        public bool IsHardwareAccelerated => false;
        public double FPS => 60;
        public bool IsDrawing { get; private set; }
        public bool HasDrawn { get; private set; }
        public long FrameTime { get; private set; }
        public Guid Uid { get; } = Guid.NewGuid();
        public SKSize CanvasSize { get; }

        public bool Update(long nanos = 0)
        {
            PrepareFrame(nanos);
            IsDrawing = true;
            try
            {
                HasDrawn = OnDraw?.Invoke(Surface, new SKRect(0, 0, CanvasSize.Width, CanvasSize.Height)) ?? false;
                return HasDrawn;
            }
            finally
            {
                IsDrawing = false;
            }
        }

        public void SignalFrame(long nanoseconds) => FrameTime = nanoseconds;

        /// <summary>External-surface renders bypass <see cref="Update"/>; the host calls this after each frame.</summary>
        public void MarkDrawn() => HasDrawn = true;

        public void PrepareFrame(long nanos = 0) =>
            FrameTime = nanos > 0 ? nanos : GetFrameTimestampNanos();

        public void Dispose() { }

        private static long GetFrameTimestampNanos()
        {
            var timestamp = Stopwatch.GetTimestamp();
            return (long)(1_000_000_000.0 * timestamp / Stopwatch.Frequency);
        }
    }
}
