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

                new SkiaButton("Test SkiaCamera Flow (New Album)")
                {
                    BackgroundColor = Colors.Orange,
                    TextColor = Colors.White,
                    CornerRadius = 8,
                    Padding = new Thickness(40, 15),
                    FontSize = 18,
                    HorizontalOptions = LayoutOptions.Center
                }
                .OnTapped(async me => await TestSkiaCameraGalleryFlow()),

                new SkiaButton("Test With Transform + 1080p")
                {
                    BackgroundColor = Colors.Purple,
                    TextColor = Colors.White,
                    CornerRadius = 8,
                    Padding = new Thickness(40, 15),
                    FontSize = 18,
                    HorizontalOptions = LayoutOptions.Center
                }
                .OnTapped(async me => await TestVideoWithTransform()),

                new SkiaButton("Test PHAssetCreationRequest")
                {
                    BackgroundColor = Colors.Crimson,
                    TextColor = Colors.White,
                    CornerRadius = 8,
                    Padding = new Thickness(40, 15),
                    FontSize = 18,
                    HorizontalOptions = LayoutOptions.Center
                }
                .OnTapped(async me => await TestWithPHAssetCreationRequest()),

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

        /// <summary>
        /// Tests the SkiaCamera.MoveVideoToGalleryAsync flow with a NEW album name
        /// to force album creation - this is the suspected broken code path on iOS 26
        /// </summary>
        private async Task TestSkiaCameraGalleryFlow()
        {
            try
            {
                _statusLabel.Text = "Requesting permissions...";
                _statusLabel.TextColor = Colors.Yellow;
                Debug.WriteLine("[TestSkiaCameraGalleryFlow] Starting test...");

                // Request permissions first
                var granted = await SkiaCamera.RequestGalleryPermissions();
                if (!granted)
                {
                    _statusLabel.Text = "❌ Permission denied!";
                    _statusLabel.TextColor = Colors.Red;
                    Debug.WriteLine("[TestSkiaCameraGalleryFlow] Permission denied");
                    return;
                }

                _statusLabel.Text = "Creating test video...";
                Debug.WriteLine("[TestSkiaCameraGalleryFlow] Creating test video...");

                // Create a test video file
                var tempPath = Path.Combine(FileSystem.Current.CacheDirectory, $"test_skia_{DateTime.Now:HHmmss}.mp4");
                await CreateTestVideoFile(tempPath);

                if (!File.Exists(tempPath))
                {
                    _statusLabel.Text = "❌ Failed to create test video";
                    _statusLabel.TextColor = Colors.Red;
                    Debug.WriteLine("[TestSkiaCameraGalleryFlow] Test video file not created");
                    return;
                }

                var fileSize = new FileInfo(tempPath).Length;
                Debug.WriteLine($"[TestSkiaCameraGalleryFlow] Test video created: {tempPath}, size: {fileSize} bytes");

                // Use a NEW album name each time to force album creation code path
                var testAlbumName = $"TestAlbum_{DateTime.Now:HHmmss}";
                _statusLabel.Text = $"Saving via SkiaCamera to '{testAlbumName}'...";
                Debug.WriteLine($"[TestSkiaCameraGalleryFlow] Using album name: {testAlbumName}");

                // Create SkiaCamera instance and use MoveVideoToGalleryAsync
                // This is the exact code path used by Racebox that's broken on iOS 26
                var skiaCamera = new SkiaCamera();
                var capturedVideo = new CapturedVideo { FilePath = tempPath };

                Debug.WriteLine("[TestSkiaCameraGalleryFlow] Calling MoveVideoToGalleryAsync...");
                var result = await skiaCamera.MoveVideoToGalleryAsync(capturedVideo, testAlbumName);
                Debug.WriteLine($"[TestSkiaCameraGalleryFlow] Result: {result ?? "NULL"}");

                if (!string.IsNullOrEmpty(result))
                {
                    _statusLabel.Text = $"✅ Saved!\nAlbum: {testAlbumName}\nAsset: {result.Substring(0, Math.Min(20, result.Length))}...";
                    _statusLabel.TextColor = Colors.LimeGreen;
                    Debug.WriteLine($"[TestSkiaCameraGalleryFlow] SUCCESS! Check Photos app for album '{testAlbumName}'");
                }
                else
                {
                    _statusLabel.Text = $"❌ Save failed!\nAlbum: {testAlbumName}";
                    _statusLabel.TextColor = Colors.Red;
                    Debug.WriteLine("[TestSkiaCameraGalleryFlow] FAILED - null result");
                }

                // Clean up temp file
                try { File.Delete(tempPath); } catch { }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"❌ Error: {ex.Message}";
                _statusLabel.TextColor = Colors.Red;
                Debug.WriteLine($"[TestSkiaCameraGalleryFlow] EXCEPTION: {ex}");
                Debug.WriteLine($"[TestSkiaCameraGalleryFlow] Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Tests video creation with transform metadata (like real camera recording)
        /// to see if transforms cause iOS 26 gallery issues
        /// </summary>
        private async Task TestVideoWithTransform()
        {
            try
            {
                _statusLabel.Text = "Creating 1080p video with transform...";
                _statusLabel.TextColor = Colors.Yellow;
                Debug.WriteLine("[TestVideoWithTransform] Starting test...");

                var granted = await SkiaCamera.RequestGalleryPermissions();
                if (!granted)
                {
                    _statusLabel.Text = "❌ Permission denied!";
                    _statusLabel.TextColor = Colors.Red;
                    return;
                }

                var tempPath = Path.Combine(FileSystem.Current.CacheDirectory, $"test_transform_{DateTime.Now:HHmmss}.mp4");
                await CreateTestVideoWithTransform(tempPath);

                if (!File.Exists(tempPath))
                {
                    _statusLabel.Text = "❌ Failed to create test video";
                    _statusLabel.TextColor = Colors.Red;
                    return;
                }

                var fileSize = new FileInfo(tempPath).Length;
                Debug.WriteLine($"[TestVideoWithTransform] Video created: {fileSize} bytes");

                var testAlbumName = $"TransformTest_{DateTime.Now:HHmmss}";
                _statusLabel.Text = $"Saving to '{testAlbumName}'...";

                var skiaCamera = new SkiaCamera();
                var capturedVideo = new CapturedVideo { FilePath = tempPath };
                var result = await skiaCamera.MoveVideoToGalleryAsync(capturedVideo, testAlbumName);

                if (!string.IsNullOrEmpty(result))
                {
                    _statusLabel.Text = $"✅ Saved with transform!\nAlbum: {testAlbumName}";
                    _statusLabel.TextColor = Colors.LimeGreen;
                    Debug.WriteLine($"[TestVideoWithTransform] SUCCESS!");
                }
                else
                {
                    _statusLabel.Text = $"❌ Save failed!";
                    _statusLabel.TextColor = Colors.Red;
                }

                try { File.Delete(tempPath); } catch { }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"❌ Error: {ex.Message}";
                _statusLabel.TextColor = Colors.Red;
                Debug.WriteLine($"[TestVideoWithTransform] EXCEPTION: {ex}");
            }
        }

        /// <summary>
        /// Creates a 1080x1920 video with 90-degree rotation transform (like portrait recording)
        /// </summary>
        private async Task CreateTestVideoWithTransform(string outputPath)
        {
            await Task.Run(() =>
            {
                try
                {
                    var url = Foundation.NSUrl.FromFilename(outputPath);

                    if (File.Exists(outputPath))
                        File.Delete(outputPath);

                    var writer = new AVAssetWriter(url, "public.mpeg-4", out var error);
                    if (error != null)
                    {
                        Debug.WriteLine($"[CreateTestVideoWithTransform] Error: {error}");
                        return;
                    }

                    // 1080p landscape, will be rotated to portrait via transform
                    int width = 1920;
                    int height = 1080;

                    var videoSettings = new AVVideoSettingsCompressed
                    {
                        Codec = AVVideoCodec.H264,
                        Width = width,
                        Height = height
                    };

                    var writerInput = new AVAssetWriterInput(AVMediaTypes.Video.GetConstant(), videoSettings);
                    writerInput.ExpectsMediaDataInRealTime = false;

                    // Apply 90-degree rotation transform (like portrait recording)
                    // This mimics what AppleVideoToolboxEncoder does
                    var transform = CoreGraphics.CGAffineTransform.MakeRotation((float)(Math.PI / 2));
                    transform = CoreGraphics.CGAffineTransform.Translate(transform, 0, -width);
                    writerInput.Transform = transform;

                    Debug.WriteLine($"[CreateTestVideoWithTransform] Applied 90° rotation transform");

                    var adaptor = AVAssetWriterInputPixelBufferAdaptor.Create(writerInput, new CoreVideo.CVPixelBufferAttributes
                    {
                        PixelFormatType = CoreVideo.CVPixelFormatType.CV32BGRA,
                        Width = width,
                        Height = height
                    });

                    writer.AddInput(writerInput);
                    writer.StartWriting();
                    writer.StartSessionAtSourceTime(CoreMedia.CMTime.Zero);

                    // Create 30 frames (1 second at 30fps)
                    int frameCount = 30;
                    for (int i = 0; i < frameCount; i++)
                    {
                        while (!writerInput.ReadyForMoreMediaData)
                            System.Threading.Thread.Sleep(10);

                        var presentationTime = CoreMedia.CMTime.FromSeconds(i / 30.0, 600);

                        using var pixelBuffer = CreateColorPixelBuffer(width, height, i);
                        adaptor.AppendPixelBufferWithPresentationTime(pixelBuffer, presentationTime);
                    }

                    writerInput.MarkAsFinished();
                    writer.FinishWriting(() =>
                    {
                        Debug.WriteLine($"[CreateTestVideoWithTransform] Video created: {outputPath}");
                    });

                    while (writer.Status == AVAssetWriterStatus.Writing)
                        System.Threading.Thread.Sleep(10);

                    Debug.WriteLine($"[CreateTestVideoWithTransform] Writer status: {writer.Status}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CreateTestVideoWithTransform] Error: {ex}");
                }
            });
        }

        /// <summary>
        /// Tests using PHAssetCreationRequest instead of PHAssetChangeRequest.FromVideo
        /// This is the modern API that iOS 26 might prefer
        /// </summary>
        private async Task TestWithPHAssetCreationRequest()
        {
            try
            {
                _statusLabel.Text = "Testing PHAssetCreationRequest...";
                _statusLabel.TextColor = Colors.Yellow;
                Debug.WriteLine("[TestPHAssetCreationRequest] Starting test...");

                var granted = await SkiaCamera.RequestGalleryPermissions();
                if (!granted)
                {
                    _statusLabel.Text = "❌ Permission denied!";
                    _statusLabel.TextColor = Colors.Red;
                    return;
                }

                // Create test video with transform (like real recording)
                var tempPath = Path.Combine(FileSystem.Current.CacheDirectory, $"test_creation_{DateTime.Now:HHmmss}.mp4");
                await CreateTestVideoWithTransform(tempPath);

                if (!File.Exists(tempPath))
                {
                    _statusLabel.Text = "❌ Failed to create test video";
                    _statusLabel.TextColor = Colors.Red;
                    return;
                }

                var fileSize = new FileInfo(tempPath).Length;
                Debug.WriteLine($"[TestPHAssetCreationRequest] Video created: {fileSize} bytes");

                var testAlbumName = $"CreationReqTest_{DateTime.Now:HHmmss}";
                _statusLabel.Text = $"Saving via PHAssetCreationRequest...";

                // Use PHAssetCreationRequest instead of PHAssetChangeRequest.FromVideo
                var result = await SaveVideoWithCreationRequest(tempPath, testAlbumName);

                if (!string.IsNullOrEmpty(result))
                {
                    _statusLabel.Text = $"✅ Saved via CreationRequest!\nAlbum: {testAlbumName}";
                    _statusLabel.TextColor = Colors.LimeGreen;
                    Debug.WriteLine($"[TestPHAssetCreationRequest] SUCCESS!");
                }
                else
                {
                    _statusLabel.Text = $"❌ Save failed!";
                    _statusLabel.TextColor = Colors.Red;
                }

                try { File.Delete(tempPath); } catch { }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"❌ Error: {ex.Message}";
                _statusLabel.TextColor = Colors.Red;
                Debug.WriteLine($"[TestPHAssetCreationRequest] EXCEPTION: {ex}");
            }
        }

        /// <summary>
        /// Saves video using PHAssetCreationRequest (modern API) instead of PHAssetChangeRequest.FromVideo
        /// </summary>
        private async Task<string> SaveVideoWithCreationRequest(string videoPath, string albumName)
        {
            var tcs = new TaskCompletionSource<string>();
            Photos.PHObjectPlaceholder placeholder = null;

            var videoUrl = Foundation.NSUrl.FromFilename(videoPath);

            Photos.PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(() =>
            {
                // Use PHAssetCreationRequest instead of PHAssetChangeRequest.FromVideo
                var creationRequest = Photos.PHAssetCreationRequest.CreationRequestForAsset();

                var options = new Photos.PHAssetResourceCreationOptions
                {
                    OriginalFilename = Path.GetFileName(videoPath),
                    ShouldMoveFile = false  // Copy, don't move
                };

                // Add video resource
                creationRequest.AddResource(Photos.PHAssetResourceType.Video, videoUrl, options);

                placeholder = creationRequest.PlaceholderForCreatedAsset;

                // Find or create album and add to it
                var fetchOptions = new Photos.PHFetchOptions();
                fetchOptions.Predicate = Foundation.NSPredicate.FromFormat($"title = '{albumName}'");
                var albums = Photos.PHAssetCollection.FetchAssetCollections(
                    Photos.PHAssetCollectionType.Album,
                    Photos.PHAssetCollectionSubtype.Any,
                    fetchOptions);

                Photos.PHAssetCollectionChangeRequest albumChangeRequest;
                if (albums.Count > 0)
                {
                    var album = albums.FirstObject as Photos.PHAssetCollection;
                    albumChangeRequest = Photos.PHAssetCollectionChangeRequest.ChangeRequest(album);
                }
                else
                {
                    albumChangeRequest = Photos.PHAssetCollectionChangeRequest.CreateAssetCollection(albumName);
                }

                albumChangeRequest?.AddAssets(new Photos.PHObject[] { placeholder });

                Debug.WriteLine($"[SaveVideoWithCreationRequest] Created request with PHAssetCreationRequest");
            },
            (success, error) =>
            {
                if (success && placeholder != null)
                {
                    Debug.WriteLine($"[SaveVideoWithCreationRequest] Success! LocalId: {placeholder.LocalIdentifier}");
                    tcs.SetResult(placeholder.LocalIdentifier);
                }
                else
                {
                    Debug.WriteLine($"[SaveVideoWithCreationRequest] Failed: {error?.LocalizedDescription}");
                    tcs.SetResult(null);
                }
            });

            return await tcs.Task;
        }

        /// <summary>
        /// Creates a pixel buffer with changing colors to make video visually distinct
        /// </summary>
        private CoreVideo.CVPixelBuffer CreateColorPixelBuffer(int width, int height, int frameIndex)
        {
            var pixelBuffer = new CoreVideo.CVPixelBuffer(width, height, CoreVideo.CVPixelFormatType.CV32BGRA);
            pixelBuffer.Lock(CoreVideo.CVPixelBufferLock.None);

            try
            {
                var baseAddress = pixelBuffer.BaseAddress;
                var bytesPerRow = (int)pixelBuffer.BytesPerRow;

                // Cycle through colors based on frame
                byte r = (byte)((frameIndex * 8) % 256);
                byte g = (byte)((128 + frameIndex * 4) % 256);
                byte b = (byte)((255 - frameIndex * 8) % 256);

                unsafe
                {
                    byte* ptr = (byte*)baseAddress.ToPointer();
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int offset = y * bytesPerRow + x * 4;
                            ptr[offset + 0] = b;     // B
                            ptr[offset + 1] = g;     // G
                            ptr[offset + 2] = r;     // R
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
