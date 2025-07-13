using DrawnUi.Camera;
using Sandbox.Views;
using System.Diagnostics;

namespace Sandbox;

public partial class MainPageCamera : BasePageCodeBehind
{
    private bool _flashOn = false;

    public MainPageCamera()
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
            _flashOn = !_flashOn;
            
            if (_flashOn)
            {
                CameraControl.TurnOnFlash();
                FlashButton.Text = "Flash On";
                FlashButton.BackgroundColor = Colors.Orange;
            }
            else
            {
                CameraControl.TurnOffFlash();
                FlashButton.Text = "Flash Off";
                FlashButton.BackgroundColor = Colors.DarkGray;
            }
            
            StatusLabel.Text = $"Camera Status: Flash {(_flashOn ? "enabled" : "disabled")}";
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
