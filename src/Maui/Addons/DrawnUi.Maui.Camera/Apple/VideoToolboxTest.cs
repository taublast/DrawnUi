#if IOS || MACCATALYST
using System;
using System.Threading.Tasks;
using SkiaSharp;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Simple test class to verify VideoToolbox encoding works.
    /// Call this from CameraTestPage to validate the pipeline.
    /// </summary>
    public static class VideoToolboxTest
    {
        /// <summary>
        /// Test: Create a 5-second video with simple graphics
        /// Output: test_output.h264 file (raw H.264 stream)
        ///
        /// To view the output on Mac:
        /// ffmpeg -framerate 30 -i test_output.h264 -c copy test_output.mp4
        ///
        /// Or use VLC player directly (it can play raw H.264)
        /// </summary>
        public static async Task RunBasicTest()
        {
            System.Diagnostics.Debug.WriteLine("[VideoToolboxTest] Starting basic test...");

            var encoder = new AppleVideoToolboxEncoder();

            try
            {
                // Initialize: 1280x720 @ 30fps
                var outputPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "test_output.mp4");

                await encoder.InitializeAsync(outputPath, 1280, 720, 30, false);
                await encoder.StartAsync();

                System.Diagnostics.Debug.WriteLine($"[VideoToolboxTest] Recording to: {outputPath}");

                // Encode 150 frames (5 seconds @ 30fps)
                for (int i = 0; i < 150; i++)
                {
                    var timestamp = TimeSpan.FromSeconds(i / 30.0);

                    // Begin frame composition
                    using (encoder.BeginFrame(timestamp, out var canvas, out var info, 0))
                    {
                        // Draw simple test pattern
                        canvas.Clear(SKColors.Black);

                        // Animated gradient background
                        float hue = (i * 2) % 360;
                        var color = SKColor.FromHsl(hue, 80, 50);
                        using (var paint = new SKPaint
                        {
                            Shader = SKShader.CreateLinearGradient(
                                new SKPoint(0, 0),
                                new SKPoint(info.Width, info.Height),
                                new[] { color, SKColors.Black },
                                SKShaderTileMode.Clamp)
                        })
                        {
                            canvas.DrawRect(0, 0, info.Width, info.Height, paint);
                        }

                        // Draw frame number
                        using (var textPaint = new SKPaint
                        {
                            Color = SKColors.White,
                            TextSize = 72,
                            IsAntialias = true,
                            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                        })
                        {
                            var text = $"Frame {i + 1}/150";
                            var textBounds = new SKRect();
                            textPaint.MeasureText(text, ref textBounds);

                            canvas.DrawText(text,
                                (info.Width - textBounds.Width) / 2,
                                (info.Height + textBounds.Height) / 2,
                                textPaint);
                        }

                        // Draw timestamp
                        using (var timePaint = new SKPaint
                        {
                            Color = SKColors.Yellow,
                            TextSize = 48,
                            IsAntialias = true
                        })
                        {
                            var timeText = $"Time: {timestamp:mm\\:ss\\.ff}";
                            canvas.DrawText(timeText, 50, 100, timePaint);
                        }
                    }

                    // Submit frame to encoder
                    await encoder.SubmitFrameAsync();

                    // Simulate frame rate (30fps = ~33ms per frame)
                    await Task.Delay(33);

                    if (i % 30 == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VideoToolboxTest] Encoded {i + 1}/150 frames...");
                    }
                }

                // Stop and finalize
                var finalPath = await encoder.StopAsync();

                System.Diagnostics.Debug.WriteLine($"[VideoToolboxTest] ✅ Test completed!");
                System.Diagnostics.Debug.WriteLine($"[VideoToolboxTest] Output file: {finalPath}");
                System.Diagnostics.Debug.WriteLine($"[VideoToolboxTest] Frames: {encoder.EncodedFrameCount}");
                System.Diagnostics.Debug.WriteLine($"[VideoToolboxTest] Size: {encoder.EncodedDataSize / 1024}KB");
                System.Diagnostics.Debug.WriteLine("");
                System.Diagnostics.Debug.WriteLine($"To convert to MP4 on Mac:");
                System.Diagnostics.Debug.WriteLine($"  ffmpeg -framerate 30 -i {finalPath} -c copy test_output.mp4");
                System.Diagnostics.Debug.WriteLine("");
                System.Diagnostics.Debug.WriteLine($"Or open directly in VLC player");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoToolboxTest] ❌ Test failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[VideoToolboxTest] Stack: {ex.StackTrace}");
            }
            finally
            {
                encoder.Dispose();
            }
        }
    }
}
#endif
