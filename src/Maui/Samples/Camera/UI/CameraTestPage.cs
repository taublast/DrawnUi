using System.ComponentModel;
using System.Diagnostics;
using CameraTests.Services;
using CameraTests.UI;
using CameraTests.Visualizers;
using DrawnUi.Camera;
using DrawnUi.Controls;
using DrawnUi.Views;

namespace CameraTests.Views;

public partial class CameraTestPage : BasePageReloadable, IDisposable
{
    private AppCamera CameraControl;
    private SkiaShape _takePictureButton;
    private SkiaLabel _flashButton;
    private SkiaLabel _statusLabel;
    private SettingsButton _videoRecordButton;
    private SettingsButton _speechButton;
    private IRealtimeTranscriptionService _realtimeTranscriptionService;
    private SkiaShape _cameraSelectButton;
 
    private SettingsButton _audioCodecButton;
    private SkiaLayer _previewOverlay;
    private SkiaImage _previewImage;
    private SkiaImage _previewThumbnail;
    private SettingsButton _preRecordingToggleButton;
    private SettingsButton _preRecordingDurationButton;
    private SkiaLabel _captionsLabel;
    private RealtimeCaptionsEngine _captionsEngine;
    private SkiaDrawer _settingsDrawer;
    private SkiaViewSwitcher _settingsTabs;
    private SkiaLabel[] _tabLabels;
    AudioVisualizer _audioVisualizer;
    Canvas Canvas;



    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            _captionsEngine?.Dispose();
            _captionsEngine = null;
            if (CameraControl != null)
            {
                CameraControl?.Stop();
                AttachHardware(false);
                CameraControl = null;
            }

            this.Content = null;
            Canvas?.Dispose();

            if (_realtimeTranscriptionService != null)
            {
                _realtimeTranscriptionService.TranscriptionDelta -= OnTranscriptionDelta;
                _realtimeTranscriptionService.TranscriptionCompleted -= OnTranscriptionCompleted;
                _realtimeTranscriptionService.SendingData -= OnTranscriptionWorking;
                _realtimeTranscriptionService.Dispose();
                _realtimeTranscriptionService = null;
            }
        }

        base.Dispose(isDisposing);
    }

    /// <summary>
    /// This will be called by HotReload
    /// </summary>
    public override void Build()
    {
        if (CameraControl != null)
        {
            CameraControl?.Stop();
            AttachHardware(false);
            CameraControl = null;
        }

        Canvas?.Dispose();

        CreateContent();

        CameraControl.InitializeOverlayLayouts(new FrameOverlay(), new FrameOverlay());

    }

    public CameraTestPage(IRealtimeTranscriptionService realtimeTranscriptionService)
    {
        Title = "SkiaCamera Test";

        _realtimeTranscriptionService = realtimeTranscriptionService;
        if (_realtimeTranscriptionService != null)
        {
            _realtimeTranscriptionService.TranscriptionDelta += OnTranscriptionDelta;
            _realtimeTranscriptionService.TranscriptionCompleted += OnTranscriptionCompleted;
            _realtimeTranscriptionService.SendingData += OnTranscriptionWorking;
        }
    }
    private void AttachHardware(bool subscribe)
    {
        if (subscribe)
        {
            CameraControl.PermissionsResult += OnPermissionsResultChanged;
            CameraControl.StateChanged += CameraControlOnStateChanged;
            CameraControl.CaptureSuccess += OnCaptureSuccess;
            CameraControl.CaptureFailed += OnCaptureFailed;
            CameraControl.OnError += OnCameraError;
            CameraControl.RecordingSuccess += OnVideoRecordingSuccess;
            CameraControl.RecordingProgress += OnVideoRecordingProgress;
            CameraControl.AudioSampleAvailable += OnAudioCaptured;

            // Monitor recording state changes to start/stop speech recognition
            CameraControl.PropertyChanged += CameraControlOnPropertyChanged;

            //CameraControl.OnAudioSample += HUD.AddAudioSample;

        }
        else
        {
            if (CameraControl != null)
            {
                CameraControl.PermissionsResult -= OnPermissionsResultChanged;
                CameraControl.StateChanged -= CameraControlOnStateChanged;
                CameraControl.CaptureSuccess -= OnCaptureSuccess;
                CameraControl.CaptureFailed -= OnCaptureFailed;
                CameraControl.OnError -= OnCameraError;
                CameraControl.RecordingSuccess -= OnVideoRecordingSuccess;
                CameraControl.RecordingProgress -= OnVideoRecordingProgress;

                CameraControl.AudioSampleAvailable -= OnAudioCaptured;

                CameraControl.PropertyChanged -= CameraControlOnPropertyChanged;

                //CameraControl.OnAudioSample -= HUD.AddAudioSample;
            }
        }
    }

    private void CameraControlOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CameraControl.IsRecording) ||
            e.PropertyName == nameof(CameraControl.IsPreRecording))
        {
            OnRecordingStateChanged();
        }
    }

    void RefreshGpsLocationIfNeeded()
    {
        if (CameraControl.InjectGpsLocation)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _ = CameraControl.RefreshGpsLocation();
            });
        }
    }

    private void CameraControlOnStateChanged(object sender, HardwareState e)
    {
        if (e == HardwareState.On)
        {
            RefreshGpsLocationIfNeeded();
        }
    }

    private int _lastAudioRate;
    private int _lastAudioBits;
    private int _lastAudioChannels;
    private bool _speechEnabled;

    bool _isTranscribing;
    public bool IsTranscribing
    {
        get => _isTranscribing;
        set
        {
            if (_isTranscribing != value)
            {
                _isTranscribing = value;
                OnPropertyChanged();
            }
        }
    }



    private System.Timers.Timer? _delayHideTimer;

    private void OnTranscriptionWorking(bool state)
    {
        if (state)
        {
            _delayHideTimer?.Stop();
            IsTranscribing = true;
        }
        else
        {
            if (_delayHideTimer == null)
            {
                _delayHideTimer = new System.Timers.Timer(500);
                _delayHideTimer.AutoReset = false;
                _delayHideTimer.Elapsed += (s, e) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        IsTranscribing = false;
                    });
                };
            }

            _delayHideTimer.Stop();    // reset if already counting
            _delayHideTimer.Start();
        }
    }

    private void OnTranscriptionDelta(string delta)
    {
        _captionsEngine?.AppendDelta(delta);
    }

    private void OnTranscriptionCompleted(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Debug.WriteLine($"[Captions] Committed: {text}");
            _captionsEngine?.CommitLine(text);
        }
    }

    private void OnAudioCaptured(byte[] data, int rate, int bits, int channels)
    {
        if (_realtimeTranscriptionService != null && _speechEnabled)
        {
            // Update audio format whenever it changes
            if (rate != _lastAudioRate || bits != _lastAudioBits || channels != _lastAudioChannels)
            {
                _lastAudioRate = rate;
                _lastAudioBits = bits;
                _lastAudioChannels = channels;
                _realtimeTranscriptionService.SetAudioFormat(rate, bits, channels);
            }

            _realtimeTranscriptionService.FeedAudio(data);
        }
    }

    private void OnRecordingStateChanged()
    {
        // Recording state changes are handled independently of speech transcription
    }

    private void StartTranscription()
    {
        _realtimeTranscriptionService?.Start();
    }

    private void StopTranscription()
    {
        _realtimeTranscriptionService?.Stop();
        _captionsEngine?.Clear();
    }

    private void UpdateStatusText()
    {
        if (_statusLabel != null && CameraControl != null)
        {
            // Compact status for mobile overlay
            var statusText = $"{CameraControl.State}";

            if (CameraControl.Facing == CameraPosition.Manual)
            {
                statusText += $" • Cam{CameraControl.CameraIndex}";
            }
            else if (CameraControl.Facing != CameraPosition.Default)
            {
                statusText += $" • {CameraControl.Facing}";
            }

            _statusLabel.Text = statusText;
            _statusLabel.TextColor = CameraControl.State switch
            {
                HardwareState.On => Color.FromArgb("#10B981"),
                HardwareState.Off => Color.FromArgb("#9CA3AF"),
                HardwareState.Error => Color.FromArgb("#DC2626"),
                _ => Color.FromArgb("#9CA3AF")
            };
        }
    }

    private bool _btnStateIsRecording;
    private const int _morphSpeed = 250;

    private void UpdateCaptureButtonShape()
    {
        bool isRecording = CameraControl.IsRecording || CameraControl.IsPreRecording;
        if (_takePictureButton == null)
            return;

        if (isRecording == _btnStateIsRecording)
            return;

        _btnStateIsRecording = isRecording;
        
        bool animated = _takePictureButton.DrawingRect != SkiaSharp.SKRect.Empty;
        
        if (animated)
        {
            if (isRecording)
            {
                // Animate to square (recording)
                _ = _takePictureButton.AnimateRangeAsync(value =>
                {
                    _takePictureButton.CornerRadius = 30 - (30 - 4) * value; // 30 to 4
                    _takePictureButton.WidthRequest = 60 - (60 - 42) * value; // 60 to 42
                }, 0, 1, (uint)_morphSpeed, Easing.SinOut, default, true);
                
                // Change color to red
                if (CameraControl.IsPreRecording)
                {
                    _takePictureButton.BackgroundColor = Colors.Orange;
                }
                else
                {
                    _takePictureButton.BackgroundColor = Colors.Red;
                }
            }
            else
            {
                // Animate to circle (idle)
                _ = _takePictureButton.AnimateRangeAsync(value =>
                {
                    _takePictureButton.CornerRadius = 4 + (30 - 4) * value; // 4 to 30
                    _takePictureButton.WidthRequest = 42 + (60 - 42) * value; // 42 to 60
                }, 0, 1, (uint)_morphSpeed, Easing.SinIn, default, true);
                
                // Change color back to light gray
                _takePictureButton.BackgroundColor = Color.FromArgb("#CECECE");
            }
        }
        else
        {
            // Set immediately without animation
            if (isRecording)
            {
                _takePictureButton.CornerRadius = 4;
                _takePictureButton.WidthRequest = 42;
                if (CameraControl.IsPreRecording)
                {
                    _takePictureButton.BackgroundColor = Colors.Orange;
                }
                else
                {
                    _takePictureButton.BackgroundColor = Colors.Red;
                }
            }
            else
            {
                _takePictureButton.CornerRadius = 30;
                _takePictureButton.WidthRequest = 60;
                _takePictureButton.BackgroundColor = Color.FromArgb("#CECECE");
            }
        }
    }

    private async Task TakePictureAsync()
    {
        if (CameraControl.State != HardwareState.On)
            return;

        try
        {
            _takePictureButton.IsEnabled = false;
            _takePictureButton.Opacity = 0.5;

            await Task.Run(async () => { await CameraControl.TakePicture(); });
        }
        finally
        {
            _takePictureButton.IsEnabled = true;
            _takePictureButton.Opacity = 1.0;
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

                // Update preview thumbnail in bottom control bar
                if (_previewThumbnail != null)
                {
                    _previewThumbnail.SetImageInternal(e.Image, false);
                }

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
        if (_videoRecordButton != null && CameraControl.IsRecording)
        {
            // Update button text with timer in MM:SS format
            _videoRecordButton.AccessoryIcon = $"🛑";
            _videoRecordButton.Text = $"Stop ({duration:mm\\:ss})";
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

    private void ShowLastCapturedPreview()
    {
        if (_currentCapturedImage != null)
        {
            // If we have a captured image, show it in the preview overlay
            ShowPreviewOverlay(_currentCapturedImage.Image);
        }
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
            if (CameraControl.State != HardwareState.On)
                return;

            try
            {
                if (CameraControl.IsRecording)
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
        if (CameraControl.State != HardwareState.On || !(CameraControl.IsRecording || CameraControl.IsPreRecording))
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
                            CameraControl.CameraIndex = selectedCamera.Index;
                            CameraControl.Facing = CameraPosition.Manual;

                            Debug.WriteLine(
                                $"Selected: {selectedCamera.Name} ({selectedCamera.Position})\nId: {selectedCamera.Id} Index: {selectedCamera.Index}");
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
                            _audioCodecButton.Text = "Codec: Default";
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
                                _audioCodecButton.Text = $"{CodecsHelper.GetShortName(result)}";
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
            _preRecordingToggleButton.TintColor = CameraControl.EnablePreRecording ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
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
        if (_preRecordingDurationButton != null)
        {
            _preRecordingDurationButton.Text = $"{CameraControl.PreRecordDuration.TotalSeconds:F0}s";
        }
    }

    private void ToggleSpeech()
    {
        _speechEnabled = !_speechEnabled;

        if (_speechButton != null)
        {
            _speechButton.Text = _speechEnabled ? "Speech: ON" : "Speech: OFF";
            _speechButton.TintColor = _speechEnabled ? Color.FromArgb("#10B981") : Color.FromArgb("#475569");
        }

        if (_speechEnabled)
        {
            StartTranscription();
        }
        else
        {
            StopTranscription();
        }
    }

 
}
