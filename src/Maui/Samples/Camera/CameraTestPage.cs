using System.Diagnostics;
using AppoMobi.Specials;
using DrawnUi.Camera;
using DrawnUi.Views;

namespace CameraTests.Views;

public partial class CameraTestPage : BasePageReloadable, IDisposable
{
    private AppCamera CameraControl;
    private SkiaButton _takePictureButton;
    private SkiaButton _flashButton;
    private SkiaLabel _statusLabel;
    private SkiaButton _videoRecordButton;
    private SkiaButton _cameraSelectButton;
    private SkiaButton _audioSelectButton;
    private SkiaButton _audioCodecButton;
    private SkiaLayer _previewOverlay;
    private SkiaImage _previewImage;
    private SkiaButton _preRecordingToggleButton;
    private SkiaButton _preRecordingDurationButton;
    private SkiaLabel _preRecordingStatusLabel;
    private SkiaButton _modeSwitchButton;
    Canvas Canvas;

    public class DebugStack : SkiaStack
    {
        public override void InvalidateByChild(SkiaControl child)
        {
            base.InvalidateByChild(child);
        }

        protected override ScaledSize MeasureStack(SKRect rectForChildrenPixels, float scale, LayoutStructure layoutStructure,
            bool isTemplated, SkiaControl template, SkiaControl[] nonTemplated)
        {
            return base.MeasureStack(rectForChildrenPixels, scale, layoutStructure, isTemplated, template, nonTemplated);
        }


        public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            return base.OnMeasuring(widthConstraint, heightConstraint, scale);
        }
    }

    public class DebugGrid : SkiaGrid
    {
        public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            return base.OnMeasuring(widthConstraint, heightConstraint, scale);
        }

        public override ScaledSize MeasureGrid(SKRect rectForChildrenPixels, float scale)
        {
            return base.MeasureGrid(rectForChildrenPixels, scale);
        }
    }

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

    public CameraTestPage()
    {
        Title = "SkiaCamera Test";
    }

    private void CreateContent()
    {
        var mainStack = new DebugGrid
        {
            RowSpacing = 16,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {

                // Camera preview
                new AppCamera()
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        BackgroundColor = Colors.Black,
                        CaptureMode = CaptureModeType.Still,
                        Aspect = TransformAspect.AspectFit
                    }
                    .Assign(out CameraControl)
                    .ObserveSelf((me, prop) =>
                    {
                        if (prop == nameof(BindingContext) || prop == nameof(me.State) ||
                            prop == nameof(me.Facing) || prop == nameof(me.CameraIndex) ||
                            prop == nameof(me.CaptureMode))
                        {
                            UpdateStatusText();
                        }
                    }),

                new DebugStack()
                {
                    UseCache = SkiaCacheType.Operations,
                    Spacing = 16,
                    Children =
                    {
                                    // Status label
                        new SkiaLabel("Camera Status: Off")
                            {
                                FontSize = 14,
                                TextColor = Colors.Gray,
                                HorizontalOptions = LayoutOptions.Center,
                                UseCache = SkiaCacheType.Operations
                            }
                            .Assign(out _statusLabel),

                        // All controls in a single wrap layout
                        new SkiaWrap
                        {
                            Spacing = 8,
                            Margin = new Thickness(0, 0, 0, 50),
                            HorizontalOptions = LayoutOptions.Center,
                            Children =
                            {
                                // Power toggle button
                                new SkiaButton("⚡ Power: OFF")
                                    {
                                        BackgroundColor = Colors.Red,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image,
                                        Padding = new Thickness(20, 8)
                                    }
                                    .OnTapped(me => { CameraControl.IsOn = !CameraControl.IsOn; })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.State), me =>
                                    {
                                        me.Text = CameraControl.State == CameraState.On ? "⚡ Power: ON" : "⚡ Power: OFF";
                                        me.BackgroundColor = CameraControl.State == CameraState.On ? Colors.Green : Colors.Red;
                                    }),

                                // Mode switch button
                                new SkiaButton("📸 Photo Mode")
                                    {
                                        BackgroundColor = Colors.DarkCyan,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image,
                                        Padding = new Thickness(20, 8)
                                    }
                                    .Assign(out _modeSwitchButton)
                                    .OnTapped(me => { ToggleCaptureMode(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.Text = CameraControl.CaptureMode == CaptureModeType.Still
                                            ? "📸 Photo Mode"
                                            : "🎥 Video Mode";
                                        me.BackgroundColor = CameraControl.CaptureMode == CaptureModeType.Still
                                            ? Colors.DarkCyan
                                            : Colors.DarkViolet;
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
                                    .OnTapped(async me => { await SelectCamera(); }),

                                // Audio selection button
                                new SkiaButton("🎤 Audio: Default")
                                    {
                                        BackgroundColor = Colors.DarkGoldenrod,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _audioSelectButton)
                                    .OnTapped(async me => { await SelectAudioSource(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Audio Codec selection button
                                new SkiaButton("🎵 Codec: Default")
                                    {
                                        BackgroundColor = Colors.DarkSlateGray,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _audioCodecButton)
                                    .OnTapped(async me => { await SelectAudioCodec(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Take Picture button (only visible in Still mode)
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
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Still;
                                    }),

                                // Photo Formats button (only visible in Still mode)
                                new SkiaButton("📷 Formats")
                                    {
                                        BackgroundColor = Colors.DarkSlateBlue,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .OnTapped(async me => { await ShowPhotoFormatPicker(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Still;
                                    }),

                                // Record button (only visible in Video mode)
                                new SkiaButton("🎥 Record")
                                    {
                                        BackgroundColor = Colors.Purple,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _videoRecordButton)
                                    .OnTapped(async me => { ToggleVideoRecording(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.IsRecordingVideo), me =>
                                    {
                                        if (CameraControl.IsRecordingVideo)
                                        {
                                            me.Text = "🛑 Stop";
                                            me.BackgroundColor = Colors.Red;
                                        }
                                        else if (CameraControl.IsPreRecording)
                                        {
                                            me.Text = "⏺️ Pre-Record";
                                            me.BackgroundColor = Colors.Orange;
                                        }
                                        else
                                        {
                                            me.Text = "🎥 Record";
                                            me.BackgroundColor = Colors.Purple;
                                        }
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.IsPreRecording), me =>
                                    {
                                        if (CameraControl.IsRecordingVideo)
                                        {
                                            me.Text = "🛑";
                                            me.BackgroundColor = Colors.Red;
                                        }
                                        else if (CameraControl.IsPreRecording)
                                        {
                                            me.Text = "⏺️ Pre-Record";
                                            me.BackgroundColor = Colors.Orange;
                                        }
                                        else
                                        {
                                            me.Text = "🎥 Record";
                                            me.BackgroundColor = Colors.Purple;
                                        }
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Abort Recording button (only visible when recording)
                                new SkiaButton("❌ Abort")
                                    {
                                        BackgroundColor = Colors.DarkRed,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image,
                                        IsVisible = false
                                    }
                                    .OnTapped(async me => { await AbortVideoRecording(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.IsRecordingVideo), me =>
                                    {
                                        me.IsVisible = CameraControl.IsRecordingVideo && CameraControl.CaptureMode == CaptureModeType.Video;
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.IsRecordingVideo && CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Video Formats button (only visible in Video mode)
                                new SkiaButton("📹 Formats")
                                    {
                                        BackgroundColor = Colors.DarkSlateBlue,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .OnTapped(async me => { await ShowVideoFormatPicker(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Pre-Record toggle button (only visible in Video mode)
                                new SkiaButton("Pre-Record: OFF")
                                    {
                                        BackgroundColor = Colors.DarkGray,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _preRecordingToggleButton)
                                    .OnTapped(me => { TogglePreRecording(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Pre-Record Duration button (only visible in Video mode)
                                new SkiaButton($"Duration: {CameraControl.PreRecordDuration.TotalSeconds:F0}s")
                                    {
                                        BackgroundColor = Colors.DarkSlateGray,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _preRecordingDurationButton)
                                    .OnTapped(async me => { await ShowPreRecordingDurationPicker(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Capture Flow toggle button (only visible in Video mode)
                                new SkiaButton("Capture Flow: ON")
                                    {
                                        BackgroundColor = Colors.Green,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .OnTapped(me =>
                                    {
                                        CameraControl.UseRealtimeVideoProcessing = !CameraControl.UseRealtimeVideoProcessing;

                                    })
                                    .ObserveProperty(()=>CameraControl, nameof(CameraControl.UseRealtimeVideoProcessing), me =>
                                    {
                                        me.Text = CameraControl.UseRealtimeVideoProcessing ? "Capture Flow: ON" : "Capture Flow: OFF";
                                        me.BackgroundColor = CameraControl.UseRealtimeVideoProcessing ? Colors.Green : Colors.DarkGray;
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Audio toggle button (only visible in Video mode)
                                new SkiaButton("Audio: OFF")
                                    {
                                        BackgroundColor = Colors.DarkGray,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .OnTapped(me =>
                                    {
                                        CameraControl.RecordAudio = !CameraControl.RecordAudio;
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.RecordAudio), me =>
                                    {
                                        me.Text = CameraControl.RecordAudio ? "Audio: ON" : "Audio: OFF";
                                        me.BackgroundColor = CameraControl.RecordAudio ? Colors.Green : Colors.DarkGray;
                                    })
                            }
                        },

                    }
                }.WithRow(1),

                // Pre-Recording status label (only visible in Video mode)
                new SkiaLabel("Pre-Recording: Disabled")
                    {
                        FontSize = 12,
                        BackgroundColor = Color.Parse("#33000000"),
                        Padding = 4,
                        TextColor = Colors.Orange,
                        VerticalOptions = LayoutOptions.End,
                        HorizontalOptions = LayoutOptions.Center,
                        UseCache = SkiaCacheType.Operations
                    }
                    .Assign(out _preRecordingStatusLabel)
                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                    {
                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                    }).WithRow(0),
            }
        }.WithRowDefinitions("*, Auto");

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
            RenderingMode = RenderingModeType.Accelerated,
            Gestures = GesturesMode.Enabled,
            Content = rootLayer,
        };

        Canvas.WillFirstTimeDraw += (sender, context) =>
        {
            Tasks.StartDelayed(TimeSpan.FromMilliseconds(500), () =>
            {
                CameraControl.IsOn = true;
            });
        };

        Content = new Grid() //due to maui layout specifics we are forced to use a Grid as root wrapper
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { Canvas }
        };

        // Configure camera for capture video flow testing
        CameraControl.UseRealtimeVideoProcessing = false; // Enable capture video flow
        CameraControl.VideoQuality = VideoQuality.Standard;
        CameraControl.RecordAudio = true;

        CameraControl.FrameProcessor = (frame) =>
        {
            CameraControl.DrawOverlay(frame);
        };

        CameraControl.PreviewProcessor = (frame) =>
        {
            //if (CameraControl.IsRecordingVideo || CameraControl.IsPreRecording)
            {
                CameraControl.DrawOverlay(frame);
            }
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

    private void ToggleCaptureMode()
    {
        CameraControl.CaptureMode = CameraControl.CaptureMode == CaptureModeType.Still
            ? CaptureModeType.Video
            : CaptureModeType.Still;
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

    private async void OnVideoRecordingSuccess(object sender, CapturedVideo capturedVideo)
    {
        try
        {
            Debug.WriteLine($"✅ Video recorded at: {capturedVideo.FilePath}");

            // Use SkiaCamera's built-in MoveVideoToGalleryAsync method (consistent with SaveToGalleryAsync for photos)
            var publicPath = await CameraControl.MoveVideoToGalleryAsync(capturedVideo, "FastRepro");

            if (!string.IsNullOrEmpty(publicPath))
            {
                Debug.WriteLine($"✅ Video moved to gallery: {publicPath}");
                ShowAlert("Success", "Video saved to gallery!");
            }
            else
            {
                Debug.WriteLine($"❌ Video not saved, path null");
                ShowAlert("Error", "Failed to save video to gallery");
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Error", $"Video save error: {ex.Message}");
            Debug.WriteLine($"❌ Video save error: {ex}");
        }
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

    private void ToggleVideoRecording()
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

    private async Task AbortVideoRecording()
    {
        if (CameraControl.State != CameraState.On || !CameraControl.IsRecordingVideo)
            return;

        try
        {
            await CameraControl.StopVideoRecording(true);
            Debug.WriteLine("❌ Video recording aborted");
        }
        catch (Exception ex)
        {
            Super.Log(ex);
            ShowAlert("Abort Error", $"Error aborting video: {ex.Message}");
        }
    }

    private async Task ShowPhotoFormatPicker()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var formats = await CameraControl.GetAvailableCaptureFormatsAsync();

                if (formats?.Count > 0)
                {
                    var options = formats.Select((format, index) =>
                        $"[{index}] {format.Description}"
                    ).ToArray();

                    var result = await DisplayActionSheet("Select Photo Format", "Cancel", null, options);

                    if (!string.IsNullOrEmpty(result) && result != "Cancel")
                    {
                        var selectedIndex = Array.FindIndex(options, opt => opt == result);
                        if (selectedIndex >= 0)
                        {
                            CameraControl.PhotoQuality = CaptureQuality.Manual;
                            CameraControl.PhotoFormatIndex = selectedIndex;

                            ShowAlert("Format Selected",
                                $"Selected: {formats[selectedIndex].Description}");
                        }
                    }
                }
                else
                {
                    ShowAlert("No Formats", "No photo formats available");
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error", $"Error getting photo formats: {ex.Message}");
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

    private async Task SelectAudioSource()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var inputDevices = await CameraControl.GetAvailableAudioDevicesAsync();

                if (inputDevices?.Count > 0)
                {
                    // Prefix the list with "System Default" option
                    var options = new string[inputDevices.Count + 1];
                    options[0] = "System Default";
                    for (int i = 0; i < inputDevices.Count; i++)
                    {
                        options[i + 1] = inputDevices[i];
                    }

                    var result = await DisplayActionSheet("Select Audio Source", "Cancel", null, options);

                    if (!string.IsNullOrEmpty(result) && result != "Cancel")
                    {
                        if (result == "System Default")
                        {
                            CameraControl.AudioDeviceIndex = -1;
                            _audioSelectButton.Text = "🎤 Audio: Default";
                        }
                        else
                        {
                            // Find the index in our original list
                            // Careful: option list was shifted by 1
                            int selectedIndex = -1;
                            for (int i = 0; i < inputDevices.Count; i++)
                            {
                                if (inputDevices[i] == result)
                                {
                                    selectedIndex = i;
                                    break;
                                }
                            }

                            if (selectedIndex >= 0)
                            {
                                CameraControl.AudioDeviceIndex = selectedIndex;
                                _audioSelectButton.Text = $"🎤 {result}";
                            }
                        }
                    }
                }
                else
                {
                    ShowAlert("No Input Devices", "No audio input devices found.");
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error", $"Error getting audio devices: {ex.Message}");
            }
        });
    }

    private async Task SelectAudioCodec()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var codecs = await CameraControl.GetAvailableAudioCodecsAsync();

                if (codecs?.Count > 0)
                {
                    // Prefix the list with "System Default" option
                    var options = new string[codecs.Count + 1];
                    options[0] = "System Default";
                    for (int i = 0; i < codecs.Count; i++)
                    {
                        options[i + 1] = codecs[i];
                    }

                    var result = await DisplayActionSheet("Select Audio Codec", "Cancel", null, options);

                    if (!string.IsNullOrEmpty(result) && result != "Cancel")
                    {
                        if (result == "System Default")
                        {
                            CameraControl.AudioCodecIndex = -1;
                            _audioCodecButton.Text = "🎵 Codec: Default";
                        }
                        else
                        {
                            // Find the index in our original list
                            // Careful: option list was shifted by 1
                            int selectedIndex = -1;
                            for (int i = 0; i < codecs.Count; i++)
                            {
                                if (codecs[i] == result)
                                {
                                    selectedIndex = i;
                                    break;
                                }
                            }

                            if (selectedIndex >= 0)
                            {
                                CameraControl.AudioCodecIndex = selectedIndex;
                                _audioCodecButton.Text = $"🎵 {CodecsHelper.GetShortName(result)}";
                            }
                        }
                    }
                }
                else
                {
                    ShowAlert("No Audio Codecs", "No audio codecs available.");
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error", $"Error getting audio codecs: {ex.Message}");
            }
        });
    }

    public static class CodecsHelper
    {
        public static string GetShortName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "";
            if (fullName.Contains("AAC", StringComparison.OrdinalIgnoreCase)) return "AAC";
            if (fullName.Contains("MP3", StringComparison.OrdinalIgnoreCase)) return "MP3";
            if (fullName.Contains("FLAC", StringComparison.OrdinalIgnoreCase)) return "FLAC";
            if (fullName.Contains("PCM", StringComparison.OrdinalIgnoreCase)) return "PCM";
            if (fullName.Contains("WMA", StringComparison.OrdinalIgnoreCase)) return "WMA";
            if (fullName.Contains("LPCM", StringComparison.OrdinalIgnoreCase)) return "LPCM";
            return fullName.Length > 10 ? fullName.Substring(0, 10) + ".." : fullName;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CameraControl?.Stop();
    }

    private void TogglePreRecording()
    {
        CameraControl.EnablePreRecording = !CameraControl.EnablePreRecording;

        if (_preRecordingToggleButton != null)
        {
            _preRecordingToggleButton.Text = CameraControl.EnablePreRecording ? "Pre-Record: ON" : "Pre-Record: OFF";
            _preRecordingToggleButton.BackgroundColor = CameraControl.EnablePreRecording ? Colors.Green : Colors.DarkGray;
        }

        UpdatePreRecordingStatus();

        Debug.WriteLine($"Pre-Recording: {(CameraControl.EnablePreRecording ? "ENABLED" : "DISABLED")}");
    }

    private async Task ShowPreRecordingDurationPicker()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var durations = new[] { "2 seconds", "5 seconds", "10 seconds", "15 seconds" };
                var values = new[] { 2, 5, 10, 15 };

                var result = await DisplayActionSheet("Pre-Recording Duration", "Cancel", null, durations);

                if (!string.IsNullOrEmpty(result) && result != "Cancel")
                {
                    var selectedIndex = Array.IndexOf(durations, result);
                    if (selectedIndex >= 0)
                    {
                        CameraControl.PreRecordDuration = TimeSpan.FromSeconds(values[selectedIndex]);
                        UpdatePreRecordingStatus();

                        Debug.WriteLine($"Pre-Recording Duration set to: {values[selectedIndex]} seconds");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error", $"Error setting pre-recording duration: {ex.Message}");
            }
        });
    }

    private void UpdatePreRecordingStatus()
    {
        if (_preRecordingStatusLabel != null)
        {
            var statusText = CameraControl.EnablePreRecording
                ? $"Pre-Recording: Enabled ({CameraControl.PreRecordDuration.TotalSeconds:F0}s)"
                : "Pre-Recording: Disabled";

            _preRecordingStatusLabel.Text = statusText;
            _preRecordingStatusLabel.TextColor = CameraControl.EnablePreRecording ? Colors.LimeGreen : Colors.Orange;
        }

        if (_preRecordingDurationButton != null)
        {
            _preRecordingDurationButton.Text = $"Duration: {CameraControl.PreRecordDuration.TotalSeconds:F0}s";
        }
    }
    // Removed old manual video gallery implementation - now using SkiaCamera's built-in MoveVideoToGalleryAsync method
}
