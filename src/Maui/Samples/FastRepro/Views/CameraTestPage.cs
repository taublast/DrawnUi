using DrawnUi.Camera;
using DrawnUi.Views;
using DrawnUi.Controls;
using Sandbox.Views.Controls;
using SkiaSharp;

namespace Sandbox.Views;

public class CameraTestPage : BasePageReloadable, IDisposable
{
    private SkiaCamera CameraControl;
    private SkiaButton _takePictureButton;
    private SkiaButton _flashButton;
    private SkiaLabel _statusLabel;
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
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        BackgroundColor = Colors.Black
                    }
                    .Assign(out CameraControl)
                    .ObserveSelf((me, prop) =>
                    {
                        if (prop == nameof(BindingContext) || prop == nameof(me.State))
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
                    Spacing = 16,
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
                                me => { me.Text = $"Flash: {CameraControl.FlashMode}"; })
                    }
                },

                // Start/Stop camera row
                new SkiaRow
                {
                    Spacing = 16,
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
                }
            }
        };

        // Create preview overlay (initially hidden)
        _previewOverlay = CreatePreviewOverlay();

        // Main layer that contains both the main stack and preview overlay
        var rootLayer = new SkiaLayer { Children = { mainStack, _previewOverlay } };

        Canvas = new Canvas
        {
            RenderingMode = RenderingModeType.Accelerated, Gestures = GesturesMode.Enabled, Content = rootLayer,
        };

        Canvas.WillFirstTimeDraw += (sender, context) => { CameraControl.IsOn = true; };

        Content = Canvas;

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
    }

    private void UpdateStatusText()
    {
        if (_statusLabel != null && CameraControl != null)
        {
            _statusLabel.Text = $"Camera Status: {CameraControl.State}";
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
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await DisplayAlert("Capture Failed", $"Failed to take picture: {e.Message}", "OK");
        });
    }

    private void OnCameraError(object sender, string e)
    {
        MainThread.BeginInvokeOnMainThread(async () => { await DisplayAlert("Camera Error", e, "OK"); });
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
            // Save to gallery
            var path = await CameraControl.SaveToGalleryAsync(_currentCapturedImage, true);
            if (!string.IsNullOrEmpty(path))
            {
                await DisplayAlert("Success", $"Photo saved successfully!\nPath: {path}", "OK");
                HidePreviewOverlay();
            }
            else
            {
                await DisplayAlert("Error", "Failed to save photo", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error saving photo: {ex.Message}", "OK");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CameraControl?.Stop();
    }
}
