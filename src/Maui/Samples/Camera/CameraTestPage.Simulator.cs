using System.Diagnostics;
using DrawnUi.Camera;
using DrawnUi.Views;

#if IOS || MACCATALYST

using AVFoundation;

#endif

namespace CameraTests.Views
{
    public partial class CameraTestPage
    {

#if IOS || MACCATALYST

        private SkiaLayout CreateSimulatorTestUI()
        {
            var stack = new SkiaStack
            {
                BackgroundColor = Colors.Gainsboro,
                Spacing = 20,
                Padding = new Thickness(40),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Children =
            {
                new SkiaLabel("iOS Simulator - Gallery Save Test")
                {
                    FontSize = 24,
                    TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                    FontAttributes = FontAttributes.Bold
                },
                new SkiaLabel("Camera not available on simulator")
                {
                    FontSize = 14,
                    TextColor = Colors.Gray,
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                },
                new SkiaButton("Test Photo Save")
                {
                    BackgroundColor = Colors.Green,
                    TextColor = Colors.White,
                    CornerRadius = 8,
                    Padding = new Thickness(40, 15),
                    FontSize = 18,
                    HorizontalOptions = LayoutOptions.Center
                }
                .OnTapped(async me => await TestPhotoGallerySave()),

                new SkiaButton("Test Video Save")
                {
                    BackgroundColor = Colors.Blue,
                    TextColor = Colors.White,
                    CornerRadius = 8,
                    Padding = new Thickness(40, 15),
                    FontSize = 18,
                    HorizontalOptions = LayoutOptions.Center
                }
                .OnTapped(async me => await TestVideoGallerySave()),

                new SkiaLabel("")
                {
                    FontSize = 14,
                    TextColor = Colors.Yellow,
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                }
                .Assign(out _statusLabel)
            }
            };

            return  stack;
        }

        private async Task TestPhotoGallerySave()
        {
            try
            {
                _statusLabel.Text = "Requesting photo library permissions...";
                Debug.WriteLine("[TestPhotoGallerySave] Starting test...");

                // Request permissions with detailed status logging
                var authStatus = await Photos.PHPhotoLibrary.RequestAuthorizationAsync(Photos.PHAccessLevel.AddOnly);
                Debug.WriteLine($"[TestPhotoGallerySave] Authorization status: {authStatus}");
                
                bool granted = authStatus == Photos.PHAuthorizationStatus.Authorized || 
                              authStatus == Photos.PHAuthorizationStatus.Limited;

                if (!granted)
                {
                    _statusLabel.Text = $"❌ Permission denied!\nStatus: {authStatus}";
                    _statusLabel.TextColor = Colors.Red;
                    Debug.WriteLine($"[TestPhotoGallerySave] Permission DENIED: {authStatus}");
                    return;
                }

                _statusLabel.Text = $"✅ Permission: {authStatus}\nCreating test image...";
                _statusLabel.TextColor = Colors.Yellow;
                Debug.WriteLine($"[TestPhotoGallerySave] Permission GRANTED: {authStatus}");
                await Task.Delay(500);

                // Create a red 1000x1000 image
                using var surface = SKSurface.Create(new SKImageInfo(1000, 1000));
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Red);

                // Draw some text to make it unique
                using var paint = new SKPaint
                {
                    Color = SKColors.White,
                    TextSize = 60,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center
                };
                canvas.DrawText($"Test Photo", 500, 450, paint);
                canvas.DrawText(DateTime.Now.ToString("HH:mm:ss"), 500, 550, paint);

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                using var stream = data.AsStream();

                _statusLabel.Text = "Saving to gallery...";
                Debug.WriteLine($"[TestPhotoGallerySave] Image created, size: {data.Size} bytes");

                // Save using NativeCamera method
                var filename = $"test_photo_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                Debug.WriteLine($"[TestPhotoGallerySave] Calling SaveJpgStreamToGallery, filename: {filename}");
                
                var nativeCamera = new DrawnUi.Camera.NativeCamera(null);
                var metadata = new DrawnUi.Camera.Metadata();
                var result = await nativeCamera.SaveJpgStreamToGallery(stream, filename, metadata, "CameraTests");
                
                Debug.WriteLine($"[TestPhotoGallerySave] SaveJpgStreamToGallery returned: {result ?? "NULL"}");

                if (!string.IsNullOrEmpty(result))
                {
                    _statusLabel.Text = $"✅ Photo saved successfully!\nAsset: {result.Substring(0, Math.Min(30, result.Length))}...";
                    _statusLabel.TextColor = Colors.LimeGreen;
                    Debug.WriteLine($"[TestPhotoGallerySave] SUCCESS! Asset: {result}");
                }
                else
                {
                    _statusLabel.Text = "❌ Save failed - no result returned";
                    _statusLabel.TextColor = Colors.Red;
                    Debug.WriteLine("[TestPhotoGallerySave] FAILED - null result from SaveJpgStreamToGallery");
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"❌ Error: {ex.Message}\n{ex.GetType().Name}";
                _statusLabel.TextColor = Colors.Red;
                Debug.WriteLine($"[TestPhotoGallerySave] EXCEPTION: {ex}");
                Debug.WriteLine($"[TestPhotoGallerySave] Stack trace: {ex.StackTrace}");
            }
        }

        private async Task TestVideoGallerySave()
        {
            try
            {
                _statusLabel.Text = "Requesting photo library permissions...";

                // Request permissions first
                var granted = await SkiaCamera.RequestGalleryPermissions();
                if (!granted)
                {
                    _statusLabel.Text = "❌ Permission denied!";
                    _statusLabel.TextColor = Colors.Red;
                    return;
                }

                _statusLabel.Text = "Creating test video...";
                await Task.Delay(100);

                // Create a simple test video file (30 frames at 30fps = 1 second)
                var tempPath = Path.Combine(FileSystem.Current.CacheDirectory, $"test_video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

                // For simulator, we'll create a minimal valid MP4 file using AVFoundation
                await CreateTestVideoFile(tempPath);

                _statusLabel.Text = "Saving video to gallery...";

                // Save using NativeCamera method
                var nativeCamera = new DrawnUi.Camera.NativeCamera(null);
                var result = await nativeCamera.SaveVideoToGallery(tempPath, "CameraTests");

                if (!string.IsNullOrEmpty(result))
                {
                    _statusLabel.Text = $"✅ Video saved successfully!";
                    _statusLabel.TextColor = Colors.LimeGreen;

                    // Clean up temp file
                    try { File.Delete(tempPath); } catch { }
                }
                else
                {
                    _statusLabel.Text = "❌ Save failed - no result returned";
                    _statusLabel.TextColor = Colors.Red;
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"❌ Error: {ex.Message}";
                _statusLabel.TextColor = Colors.Red;
                Debug.WriteLine($"[TestVideoGallerySave] Error: {ex}");
            }
        }

        private async Task CreateTestVideoFile(string outputPath)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Use AVAssetWriter to create a valid MP4 file
                    var url = Foundation.NSUrl.FromFilename(outputPath);

                    // Delete if exists
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);

                    var writer = new AVFoundation.AVAssetWriter(url, "public.mpeg-4", out var error);
                    if (error != null)
                    {
                        Debug.WriteLine($"[CreateTestVideoFile] Error creating writer: {error}");
                        return;
                    }

                    // Video settings: 720x720, H.264
                    var videoSettings = new AVFoundation.AVVideoSettingsCompressed
                    {
                        Codec = AVFoundation.AVVideoCodec.H264,
                        Width = 720,
                        Height = 720
                    };

                    var writerInput = new AVFoundation.AVAssetWriterInput(AVFoundation.AVMediaTypes.Video.GetConstant(), videoSettings);
                    writerInput.ExpectsMediaDataInRealTime = false;

                    var adaptor = AVFoundation.AVAssetWriterInputPixelBufferAdaptor.Create(writerInput, new CoreVideo.CVPixelBufferAttributes
                    {
                        PixelFormatType = CoreVideo.CVPixelFormatType.CV32BGRA,
                        Width = 720,
                        Height = 720
                    });

                    writer.AddInput(writerInput);
                    writer.StartWriting();
                    writer.StartSessionAtSourceTime(CoreMedia.CMTime.Zero);

                    // Create 30 red frames (1 second at 30fps)
                    int frameCount = 30;
                    for (int i = 0; i < frameCount; i++)
                    {
                        while (!writerInput.ReadyForMoreMediaData)
                            System.Threading.Thread.Sleep(10);

                        var presentationTime = CoreMedia.CMTime.FromSeconds(i / 30.0, 600);

                        using var pixelBuffer = CreateRedPixelBuffer();
                        adaptor.AppendPixelBufferWithPresentationTime(pixelBuffer, presentationTime);
                    }

                    writerInput.MarkAsFinished();
                    writer.FinishWriting(() =>
                    {
                        Debug.WriteLine($"[CreateTestVideoFile] Video created: {outputPath}");
                    });

                    // Wait for completion
                    while (writer.Status == AVFoundation.AVAssetWriterStatus.Writing)
                        System.Threading.Thread.Sleep(10);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CreateTestVideoFile] Error: {ex}");
                }
            });
        }

        private CoreVideo.CVPixelBuffer CreateRedPixelBuffer()
        {
            var attributes = new CoreVideo.CVPixelBufferAttributes
            {
                PixelFormatType = CoreVideo.CVPixelFormatType.CV32BGRA,
                Width = 720,
                Height = 720
            };

            var pixelBuffer = new CoreVideo.CVPixelBuffer(720, 720, CoreVideo.CVPixelFormatType.CV32BGRA, attributes);
            pixelBuffer.Lock(CoreVideo.CVPixelBufferLock.None);

            try
            {
                var baseAddress = pixelBuffer.BaseAddress;
                var bytesPerRow = (int)pixelBuffer.BytesPerRow;
                var width = (int)pixelBuffer.Width;
                var height = (int)pixelBuffer.Height;

                // Fill with red (BGRA format)
                unsafe
                {
                    byte* ptr = (byte*)baseAddress.ToPointer();
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int offset = y * bytesPerRow + x * 4;
                            ptr[offset + 0] = 0;     // B
                            ptr[offset + 1] = 0;     // G
                            ptr[offset + 2] = 255;   // R
                            ptr[offset + 3] = 255;   // A
                        }
                    }
                }
            }
            finally
            {
                pixelBuffer.Unlock(CoreVideo.CVPixelBufferLock.None);
            }

            return pixelBuffer;
        }
#endif
    }
}
