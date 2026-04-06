using SkiaSharp;
using System.Text;

namespace DrawnUi.Controls;

public class GifAnimation : IDisposable
{
    protected object LockFrames = new();

    protected SKBitmap[] Frames { get; set; }

    /// <summary>
    /// Current frame bitmap, can change with SeekFrame method
    /// </summary>
    public SKBitmap Frame { get; protected set; }

    /// <summary>
    /// Select frame. If you pass a negative value the last frame will be set.
    /// </summary>
    /// <param name="frame"></param>
    public void SeekFrame(int frame)
    {
        if (frame > 0)
        {
            if (frame > TotalFrames - 1)
            {
                frame = TotalFrames - 1;
            }
        }

        if (frame < 0) //we can pass -1 for ordering setting the last frame
        {
            frame = TotalFrames - 1;
        }

        if (frame >= 0 && frame <= TotalFrames - 1)
        {
            Frame = Frames[frame];
        }
    }

    public int GetFrameNumber(double msTime)
    {
        if (FramesPositionsMs.Length > 0)
        {
            if (msTime < 0)
            {
                msTime = DurationMs + msTime;
            }

            msTime %= DurationMs; // Wrap around if msTime is greater than the total duration

            for (int i = 0; i < FramesPositionsMs.Length; i++)
            {
                if (msTime < FramesPositionsMs[i])
                {
                    return i;
                }
            }
        }

        return 0;
    }


    protected int Width { get; set; }

    protected int Height { get; set; }

    public int DurationMs { get; protected set; }

    public int TotalFrames { get; protected set; }

    protected int[] FramesPositionsMs { get; set; }

    public bool IsDisposed { get; set; }

    public virtual void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;

            lock (LockFrames)
            {
                if (Frames != null)
                    foreach (var frame in Frames)
                        frame.Dispose();
                Frames = null;
            }
        }
    }

    public void LoadFromStream(Stream stream)
    {
        SKBitmap[] bitmaps;
        int height, width, frameCount, totalDuration = 0;
        int[] durations;
        int[] accumulatedDurations;

        var data = SKData.Create(stream);
        using var codec = SKCodec.Create(data);

        width = codec.Info.Width;
        height = codec.Info.Height;

        // Get info and allocate source bitmaps
        frameCount = codec.FrameCount;
        bitmaps = new SKBitmap[frameCount];
        durations = new int[frameCount];
        accumulatedDurations = new int[frameCount];

        // Note: There's also a RepetitionCount property of SKCodec not used here

        // Loop through the frames
        for (var frame = 0; frame < frameCount; frame++)
        {
            // From the FrameInfo collection, get the duration of each frame
            durations[frame] = codec.FrameInfo[frame].Duration;

            // Create a full-color bitmap for each frame
            var imageInfo = new SKImageInfo(width, height);
            bitmaps[frame] = new SKBitmap(imageInfo);

            // Get the address of the pixels in that bitmap
            var pointer = bitmaps[frame].GetPixels();

            // Create an SKCodecOptions value to specify the frame
            SKCodecOptions codecOptions = new(frame); // SKCodecOptions(frame, false);

            // Copy pixels from the frame into the bitmap
            codec.GetPixels(imageInfo, pointer, codecOptions);
        }

        // Calculate total duration
        for (var frame = 0; frame < durations.Length; frame++) totalDuration += durations[frame];

        // Calculate the accumulated durations 
        for (var frame = 0; frame < durations.Length; frame++)
            accumulatedDurations[frame] = durations[frame] +
                                          (frame == 0 ? 0 : accumulatedDurations[frame - 1]);

        lock (LockFrames)
        {
            if (!IsDisposed)
            {
                var kill = Frames;
                TotalFrames = frameCount;
                Frames = bitmaps;
                Width = width;
                Height = height;
                DurationMs = totalDuration;
                FramesPositionsMs = accumulatedDurations;
                SeekFrame(0);

                if (kill != Frames && kill != null)
                    Tasks.StartDelayed(TimeSpan.FromSeconds(2), () =>
                    {
                        foreach (var frame in kill) frame.Dispose();
                    });
            }
        }
    }
}