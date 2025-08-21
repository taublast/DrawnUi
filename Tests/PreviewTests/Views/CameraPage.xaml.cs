using DrawnUi.Camera;
using System.Diagnostics;

namespace PreviewTests.Views;

public partial class CameraPage : BasePageCodeBehind
{
    private bool _flashOn = false;

    public CameraPage()
    {
        try
        {
            InitializeComponent();
            
            // Initialize camera when page loads
            Loaded += OnPageLoaded;
        }
        catch (Exception e)
        {
            Super.DisplayException(this, e);
        }
    }

    private async void OnPageLoaded(object sender, EventArgs e)
    {
        try
        {
            StatusLabel.Text = "Camera Status: Requesting permissions...";
            CameraControl.Start();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Camera Status: Error - {ex.Message}";
            Debug.WriteLine($"[CameraTestPage] OnPageLoaded error: {ex}");
        }
    }

    private void OnCaptureSuccess(object sender, CapturedImage e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = $"Camera Status: Photo captured at {e.Time:HH:mm:ss}";
            });
            
            Debug.WriteLine($"[CameraTestPage] Photo captured successfully: {e.Image?.Width}x{e.Image?.Height}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CameraTestPage] OnCaptureSuccess error: {ex}");
        }
    }

    private void OnCaptureFailed(object sender, Exception e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = $"Camera Status: Capture failed - {e.Message}";
            });
            
            Debug.WriteLine($"[CameraTestPage] Photo capture failed: {e}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CameraTestPage] OnCaptureFailed error: {ex}");
        }
    }

    private void OnZoomed(object sender, double zoomLevel)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = $"Camera Status: Zoom level {zoomLevel:F1}x";
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CameraTestPage] OnZoomed error: {ex}");
        }
    }

    private void OnFlashClicked(object sender, object e)
    {
        try
        {
            // Cycle through preview flash modes: Off -> On -> Off...
            var currentMode = CameraControl.FlashMode;
            var nextMode = currentMode switch
            {
                FlashMode.Off => FlashMode.On,
                FlashMode.On => FlashMode.Off,
                FlashMode.Strobe => FlashMode.Off, // Future feature
                _ => FlashMode.Off
            };

            CameraControl.FlashMode = nextMode;

            // Update UI based on flash mode
            switch (nextMode)
            {
                case FlashMode.Off:
                    FlashButton.Text = "Torch: Off";
                    FlashButton.BackgroundColor = Colors.DarkGray;
                    break;
                case FlashMode.On:
                    FlashButton.Text = "Torch: On";
                    FlashButton.BackgroundColor = Colors.Orange;
                    break;
                case FlashMode.Strobe:
                    FlashButton.Text = "Torch: Strobe";
                    FlashButton.BackgroundColor = Colors.Purple;
                    break;
            }

            StatusLabel.Text = $"Camera Status: Preview torch mode set to {nextMode}";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Camera Status: Flash error - {ex.Message}";
            Debug.WriteLine($"[CameraTestPage] OnFlashClicked error: {ex}");
        }
    }

    private void OnCaptureClicked(object sender, object e)
    {
        try
        {
            StatusLabel.Text = "Camera Status: Taking photo...";
            CameraControl.TakePicture();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Camera Status: Capture error - {ex.Message}";
            Debug.WriteLine($"[CameraTestPage] OnCaptureClicked error: {ex}");
        }
    }

    private void OnSwitchCameraClicked(object sender, object e)
    {
        try
        {
            StatusLabel.Text = "Camera Status: Switching camera...";
            
            // Switch between front and back camera
            CameraControl.Facing = CameraControl.Facing == CameraPosition.Default 
                ? CameraPosition.Selfie 
                : CameraPosition.Default;
                
            StatusLabel.Text = $"Camera Status: Switched to {CameraControl.Facing} camera";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Camera Status: Switch error - {ex.Message}";
            Debug.WriteLine($"[CameraTestPage] OnSwitchCameraClicked error: {ex}");
        }
    }

    protected override void OnDisappearing()
    {
        try
        {
            CameraControl?.StopInternal();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CameraTestPage] OnDisappearing error: {ex}");
        }
        
        base.OnDisappearing();
    }
}
