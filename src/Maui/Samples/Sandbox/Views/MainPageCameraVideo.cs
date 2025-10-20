using System.Diagnostics;
using DrawnUi.Camera;
using DrawnUi.Controls;
using DrawnUi.Views;
using Sandbox.Views.Controls;
using SkiaSharp;
using TerraFX.Interop.Windows;

namespace Sandbox.Views;

public class MainPageCameraVideo : BasePageReloadable, IDisposable
{
    private SkiaCamera CameraControl;
    private SkiaButton _takePictureButton;
    private SkiaButton _flashButton;
    private SkiaLabel _statusLabel;
    private SkiaButton _videoRecordButton;
    private SkiaButton _cameraSelectButton;
    private SkiaLayer _previewOverlay;
    private SkiaImage _previewImage;
    Canvas Canvas;

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            CameraControl?.Stop();
            CameraControl = null;
            this.Content = null;
            Canvas?.Dispose();
        }

        base.Dispose(isDisposing);
    }

    void ShowAlert(string title, string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await DisplayAlert(title, message, "OK");
        });
    }

    /// <summary>
    /// This will be called by HotReload
    /// </summary>
    public override void Build()
    {
        Canvas?.Dispose();

        CreateContent();
    }

    public MainPageCameraVideo()
    {
        Title = "SkiaCamera Test";
    }

    private void CreateContent()
    {
        var mainStack = new SkiaStack
        {
            Spacing = 16,
            Padding = 16,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                // Camera preview
                new SkiaCamera()
                    {
                        VideoDiagnosticsOn=true,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        BackgroundColor = Colors.Black,
                        CaptureMode = CaptureModeType.Video,
                        Aspect = TransformAspect.AspectFit
                    }
                    .Assign(out CameraControl)
                    .ObserveSelf((me, prop) =>
                    {
                        if (prop == nameof(BindingContext) || prop == nameof(me.State) ||
                            prop == nameof(me.Facing) || prop == nameof(me.CameraIndex))
                        {
                            UpdateStatusText();
                        }
                    }),

                // Status label
                new SkiaLabel("Camera Status: Off")
                    {
                        FontSize = 14,
                        TextColor = Colors.Gray,
                        HorizontalOptions = LayoutOptions.Center,
                        UseCache = SkiaCacheType.Operations
                    }
                    .Assign(out _statusLabel),

                // Controls row
                new SkiaRow
                {
                    Spacing = 8,
                    HorizontalOptions = LayoutOptions.Center,
                    Children =
                    {
                        // Take Picture button
                        new SkiaButton("Take Picture")
                            {
                                BackgroundColor = Colors.Blue,
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image
                            }
                            .Assign(out _takePictureButton)
                            .OnTapped(async me => { await TakePictureAsync(); })
                            .ObserveProperty(CameraControl, nameof(CameraControl.State), me =>
                            {
                                me.IsEnabled = CameraControl.State == CameraState.On;
                                me.Opacity = me.IsEnabled ? 1.0 : 0.5;
                            }),

                        // Flash control button
                        new SkiaButton("Flash: Off")
                            {
                                BackgroundColor = Colors.Orange,
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image
                            }
                            .Assign(out _flashButton)
                            .OnTapped(me => { ToggleFlash(); })
                            .ObserveProperty(CameraControl, nameof(CameraControl.FlashMode),
                                me => { me.Text = $"Flash: {CameraControl.FlashMode}"; }),

                        // Camera selection button
                        new SkiaButton("📷 Camera")
                            {
                                BackgroundColor = Colors.Teal,
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image
                            }
                            .Assign(out _cameraSelectButton)
                            .OnTapped(async me => { await SelectCamera(); })
                    }
                },

                // Start/Stop camera row
                new SkiaRow
                {
                    Spacing = 8,
                    HorizontalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new SkiaButton("Start Camera")
                            {
                                BackgroundColor = Colors.Green,
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image
                            }
                            .OnTapped(me => { CameraControl.IsOn = true; }),
                        new SkiaButton("Stop Camera")
                            {
                                BackgroundColor = Colors.Red,
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image
                            }
                            .OnTapped(me => { CameraControl.IsOn = false; })
                    }
                },

                // Video recording row
                new SkiaRow
                {
                    Spacing = 16,
                    HorizontalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new SkiaButton("🎥 Record")
                            {
                                BackgroundColor = Colors.Purple,
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image
                            }
                            .Assign(out _videoRecordButton)
                            .OnTapped(async me => { await ToggleVideoRecording(); })
                            .ObserveProperty(CameraControl, nameof(CameraControl.IsRecordingVideo), me =>
                            {
                                if (CameraControl.IsRecordingVideo)
                                {
                                    me.Text = "🛑 Stop (00:00)";
                                    me.BackgroundColor = Colors.Red;
                                }
                                else
                                {
                                    me.Text = "🎥 Record";
                                    me.BackgroundColor = Colors.Purple;
                                }
                            }),
                        new SkiaButton("📹 Formats")
                            {
                                BackgroundColor = Colors.DarkSlateBlue,
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image
                            }
                            .OnTapped(async me => { await ShowVideoFormatPicker(); })
                    }
                },

                new ButtonToRoot()
            }
        };

        // Create preview overlay (initially hidden)
        _previewOverlay = CreatePreviewOverlay();

        // Main layer that contains both the main stack and preview overlay
        var rootLayer = new SkiaLayer
        {
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                mainStack,
                _previewOverlay,
#if DEBUG
                new SkiaLabelFps()
                {
                    Margin = new(0, 0, 4, 24),
                    VerticalOptions = LayoutOptions.End,
                    HorizontalOptions = LayoutOptions.End,
                    Rotation = -45,
                    BackgroundColor = Colors.DarkRed,
                    TextColor = Colors.White,
                    ZIndex = 110,
                }
#endif
            }
        };

        Canvas = new Canvas
        {
            RenderingMode = RenderingModeType.Accelerated, Gestures = GesturesMode.Enabled, Content = rootLayer,
        };

        Canvas.WillFirstTimeDraw += (sender, context) => { CameraControl.IsOn = true; };

        Content = Canvas;

        // Configure camera for capture video flow testing
        CameraControl.RecordAudio = false; // Test with audio recording
        CameraControl.UseCaptureVideoFlow = true; // Enable capture video flow
        CameraControl.VideoQuality = VideoQuality.High;

        CameraControl.FrameProcessor = (frame) =>
        {
            // Simple text overlay for testing
            using var paint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = 48,
                IsAntialias = true,
            };

            // Draw "LIVE" text at top left
            frame.Canvas.DrawText("LIVE", 50, 100, paint);

            // Draw timestamp at top left (below LIVE)
            frame.Canvas.DrawText($"{frame.Time:mm\\:ss}", 50, 160, paint);

            // Draw a simple border around the frame
            using var borderPaint = new SKPaint
            {
                Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = 4, IsAntialias = true
            };
            frame.Canvas.DrawRect(10, 10, frame.Width - 20, frame.Height - 20, borderPaint);
        };

        // Setup camera event handlers
        SetupCameraEvents();
    }

    private SkiaLayer CreatePreviewOverlay()
    {
        return new SkiaLayer
        {
            IsVisible = false,
            BackgroundColor = Color.FromArgb("#CC000000"), // Semi-transparent black overlay
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            ZIndex = 1000, // Ensure it's on top
            Children =
            {
                // Close button (X) at top-right
                new SkiaButton("✕")
                    {
                        FontSize = 24,
                        TextColor = Colors.White,
                        BackgroundColor = Color.FromArgb("#66000000"),
                        CornerRadius = 20,
                        WidthRequest = 40,
                        HeightRequest = 40,
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.Start,
                        Margin = new Thickness(20),
                        UseCache = SkiaCacheType.Image
                    }
                    .OnTapped(me => { HidePreviewOverlay(); }),

                // Main preview image container
                new SkiaLayout
                {
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Padding = 20,
                    Children =
                    {
                        // White background frame
                        new SkiaShape
                        {
                            Type = ShapeType.Rectangle,
                            BackgroundColor = Colors.White,
                            CornerRadius = 8,
                            Padding = 8,
                            Children =
                            {
                                new SkiaImage()
                                    {
                                        Aspect = TransformAspect.AspectFit,
                                        MaximumWidthRequest = 400,
                                        MaximumHeightRequest = 600,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _previewImage)
                            }
                        }
                    }
                },

                // Action buttons at bottom
                new SkiaRow
                {
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.End,
                    Margin = new Thickness(20, 20, 20, 40),
                    Spacing = 16,
                    Children =
                    {
                        new SkiaButton("Save to Gallery")
                            {
                                BackgroundColor = Colors.Green,
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                Padding = new Thickness(16, 8),
                                UseCache = SkiaCacheType.Image
                            }
                            .OnTapped(async me => { await SaveCurrentImageToGallery(); }),
                        new SkiaButton("Discard")
                            {
                                BackgroundColor = Colors.Red,
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                Padding = new Thickness(16, 8),
                                UseCache = SkiaCacheType.Image
                            }
                            .OnTapped(me => { HidePreviewOverlay(); })
                    }
                }
            }
        };
    }

    private void SetupCameraEvents()
    {
        CameraControl.CaptureSuccess += OnCaptureSuccess;
        CameraControl.CaptureFailed += OnCaptureFailed;
        CameraControl.OnError += OnCameraError;
        CameraControl.VideoRecordingSuccess += OnVideoRecordingSuccess;
        CameraControl.VideoRecordingProgress += OnVideoRecordingProgress;
    }

    private void UpdateStatusText()
    {
        if (_statusLabel != null && CameraControl != null)
        {
            var statusText = $"Camera Status: {CameraControl.State}";
            if (CameraControl.Facing == CameraPosition.Manual)
            {
                statusText += $" | Index: {CameraControl.CameraIndex} | Facing: {CameraControl.Facing}";
            }
            else
            {
                statusText += $" | Facing: {CameraControl.Facing}";
            }

            _statusLabel.Text = statusText;
            _statusLabel.TextColor = CameraControl.State switch
            {
                CameraState.On => Colors.Green,
                CameraState.Off => Colors.Gray,
                CameraState.Error => Colors.Red,
                _ => Colors.Gray
            };
        }
    }

    private async Task TakePictureAsync()
    {
        if (CameraControl.State != CameraState.On)
            return;

        try
        {
            _takePictureButton.IsEnabled = false;
            _takePictureButton.Text = "Taking...";

            await Task.Run(async () => { await CameraControl.TakePicture(); });
        }
        finally
        {
            _takePictureButton.IsEnabled = true;
            _takePictureButton.Text = "Take Picture";
        }
    }

    private void ToggleFlash()
    {
        if (!CameraControl.IsFlashSupported)
        {
            DisplayAlert("Flash", "Flash is not supported on this camera", "OK");
            return;
        }

        CameraControl.FlashMode = CameraControl.FlashMode switch
        {
            FlashMode.Off => FlashMode.On,
            FlashMode.On => FlashMode.Strobe,
            FlashMode.Strobe => FlashMode.Off,
            _ => FlashMode.Off
        };
    }

    private CapturedImage _currentCapturedImage;

    private void OnCaptureSuccess(object sender, CapturedImage e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                // Store the captured image for potential saving later
                _currentCapturedImage = e;

                // Show the image in preview overlay
                ShowPreviewOverlay(e.Image);
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"Error displaying photo: {ex.Message}", "OK");
            }
        });
    }

    private void OnCaptureFailed(object sender, Exception e)
    {
        ShowAlert("Capture Failed", $"Failed to take picture: {e.Message}");
    }

    private void OnCameraError(object sender, string e)
    {
        ShowAlert("Camera Error", e);
    }

    private void OnVideoRecordingSuccess(object sender, CapturedVideo capturedVideo)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                Debug.WriteLine($"✅ Video recorded at: {capturedVideo.FilePath}");

                // Use SkiaCamera's built-in MoveVideoToGalleryAsync method (consistent with SaveToGalleryAsync for photos)
                var publicPath = await CameraControl.MoveVideoToGalleryAsync(capturedVideo, "DrawnUI");

                if (!string.IsNullOrEmpty(publicPath))
                {
                    ShowAlert("Success", $"Video saved to gallery! At: {publicPath}");
                    Debug.WriteLine($"✅ Video moved to gallery: {publicPath}");
                }
                else
                {
                    ShowAlert("Error", "Failed to save video to gallery");
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error", $"Video save error: {ex.Message}");
                Debug.WriteLine($"❌ Video save error: {ex}");
            }
        });
    }

    private void OnVideoRecordingProgress(object sender, TimeSpan duration)
    {
        if (_videoRecordButton != null && CameraControl.IsRecordingVideo)
        {
            // Update button text with timer in MM:SS format
            _videoRecordButton.Text = $"🛑 Stop ({duration:mm\\:ss})";
        }
    }

    private void ShowPreviewOverlay(SkiaSharp.SKImage image)
    {
        if (_previewImage != null && _previewOverlay != null)
        {
            // Set the captured image to the preview image control
            _previewImage.SetImageInternal(image, false);

            // Show the overlay
            _previewOverlay.IsVisible = true;
        }
    }

    private void HidePreviewOverlay()
    {
        if (_previewOverlay != null)
        {
            _previewOverlay.IsVisible = false;
        }

        // Clear the current captured image
        _currentCapturedImage = null;
    }

    private async Task SaveCurrentImageToGallery()
    {
        if (_currentCapturedImage == null)
            return;

        try
        {
            // Save to gallery, note we set reorient to false, because it should be handled by metadata in this case
            var path = await CameraControl.SaveToGalleryAsync(_currentCapturedImage);
            if (!string.IsNullOrEmpty(path))
            {
                ShowAlert("Success", $"Photo saved successfully!\nPath: {path}");
                HidePreviewOverlay();
            }
            else
            {
                ShowAlert("Error", "Failed to save photo");
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Error", $"Error saving photo: {ex.Message}");
        }
    }

    private async Task ToggleVideoRecording()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (CameraControl.State != CameraState.On)
                return;

            try
            {
                if (CameraControl.IsRecordingVideo)
                {
                    await CameraControl.StopVideoRecording();
                }
                else
                {
                    await CameraControl.StartVideoRecording();
                }
            }
            catch (NotImplementedException ex)
            {
                Super.Log(ex);
                ShowAlert("Not Implemented",
                    $"Video recording is not yet implemented for this platform:\n{ex.Message}");
            }
            catch (Exception ex)
            {
                Super.Log(ex);
                ShowAlert("Video Recording Error", $"Error: {ex.Message}");
            }
        });
    }

    private async Task ShowVideoFormatPicker()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var formats = await CameraControl.GetAvailableVideoFormatsAsync();

                if (formats?.Count > 0)
                {
                    var options = formats.Select((format, index) =>
                        $"[{index}] {format.Description}"
                    ).ToArray();

                    var result = await DisplayActionSheet("Select Video Format", "Cancel", null, options);

                    if (!string.IsNullOrEmpty(result) && result != "Cancel")
                    {
                        var selectedIndex = Array.FindIndex(options, opt => opt == result);
                        if (selectedIndex >= 0)
                        {
                            CameraControl.VideoQuality = VideoQuality.Manual;
                            CameraControl.VideoFormatIndex = selectedIndex;

                            ShowAlert("Format Selected",
                                $"Selected: {formats[selectedIndex].Description}");
                        }
                    }
                }
                else
                {
                    ShowAlert("No Formats", "No video formats available");
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error", $"Error getting video formats: {ex.Message}");
            }
        });
    }

    private async Task SelectCamera()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var cameras = await CameraControl.GetAvailableCamerasAsync();

                if (cameras?.Count > 0)
                {
                    var options = cameras.Select((camera, index) =>
                        $"[{index}] {camera.Name} ({camera.Position})"
                    ).ToArray();

                    var result = await DisplayActionSheet("Select Camera", "Cancel", null, options);

                    if (!string.IsNullOrEmpty(result) && result != "Cancel")
                    {
                        var selectedIndex = Array.FindIndex(options, opt => opt == result);
                        if (selectedIndex >= 0)
                        {
                            var selectedCamera = cameras[selectedIndex];

                            // Set camera selection - this will automatically trigger restart if camera is running
                            CameraControl.Facing = CameraPosition.Manual;
                            CameraControl.CameraIndex = selectedCamera.Index;

                            // Update button text
                            _cameraSelectButton.Text = $"📷 {selectedCamera.Position}";

                            Debug.WriteLine(
                                $"Selected: {selectedCamera.Name} ({selectedCamera.Position})\nIndex: {selectedCamera.Index}");
                        }
                    }
                }
                else
                {
                    ShowAlert("No Cameras", "No cameras available");
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error", $"Error getting cameras: {ex.Message}");
            }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CameraControl?.Stop();
    }

    // Removed old manual video gallery implementation - now using SkiaCamera's built-in MoveVideoToGalleryAsync method
}
