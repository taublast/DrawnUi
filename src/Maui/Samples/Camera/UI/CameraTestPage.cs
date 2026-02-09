using System.Diagnostics;
using AppoMobi.Specials;
using CameraTests.Services;
using CameraTests.Visualizers;
using DrawnUi.Camera;
using DrawnUi.Controls;
using DrawnUi.Views;

namespace CameraTests.Views;

public partial class CameraTestPage : BasePageReloadable, IDisposable
{
    private AppCamera CameraControl;
    private SkiaButton _takePictureButton;
    private SkiaButton _flashButton;
    private SkiaLabel _statusLabel;
    private SkiaButton _videoRecordButton;
    private SkiaButton _speechButton;
    private IRealtimeTranscriptionService _realtimeTranscriptionService;
    private SkiaButton _cameraSelectButton;
    private SkiaButton _audioSelectButton;
    private SkiaButton _audioCodecButton;
    private SkiaLayer _previewOverlay;
    private SkiaImage _previewImage;
    private SkiaButton _preRecordingToggleButton;
    private SkiaButton _preRecordingDurationButton;
    private SkiaLabel _captionsLabel;
    private RealtimeCaptionsEngine _captionsEngine;
    private SkiaDrawer _settingsDrawer;
    AudioVisualizer _audioVisualizer;
    Canvas Canvas;

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            _captionsEngine?.Dispose();
            _captionsEngine = null;
            CameraControl?.Stop();
            CameraControl = null;
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
        Canvas?.Dispose();

        CreateContent();
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

    private void SetupCameraEvents()
    {
        CameraControl.CaptureSuccess += OnCaptureSuccess;
        CameraControl.CaptureFailed += OnCaptureFailed;
        CameraControl.OnError += OnCameraError;
        CameraControl.VideoRecordingSuccess += OnVideoRecordingSuccess;
        CameraControl.VideoRecordingProgress += OnVideoRecordingProgress;

        CameraControl.AudioSampleAvailable += OnAudioCaptured;

        // Monitor recording state changes to start/stop speech recognition
        CameraControl.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CameraControl.IsRecording) ||
                e.PropertyName == nameof(CameraControl.IsPreRecording))
            {
                OnRecordingStateChanged();
            }
        };
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
                CameraState.On => Color.FromArgb("#10B981"),
                CameraState.Off => Color.FromArgb("#9CA3AF"),
                CameraState.Error => Color.FromArgb("#DC2626"),
                _ => Color.FromArgb("#9CA3AF")
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
        if (_videoRecordButton != null && CameraControl.IsRecording)
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
        if (CameraControl.State != CameraState.On || !CameraControl.IsRecording)
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
            _preRecordingToggleButton.Text = CameraControl.EnablePreRecording ? "⏱️ Pre-Record: ON" : "⏱️ Pre-Record: OFF";
            _preRecordingToggleButton.BackgroundColor = CameraControl.EnablePreRecording ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
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
            _preRecordingDurationButton.Text = $"⏰ {CameraControl.PreRecordDuration.TotalSeconds:F0}s";
        }
    }

    private void ToggleSpeech()
    {
        _speechEnabled = !_speechEnabled;

        if (_speechButton != null)
        {
            _speechButton.Text = _speechEnabled ? "🎙️ Speech: ON" : "🎙️ Speech: OFF";
            _speechButton.BackgroundColor = _speechEnabled ? Color.FromArgb("#10B981") : Color.FromArgb("#475569");
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

    private void ToggleSettingsDrawer()
    {
        if (_settingsDrawer != null)
        {
            _settingsDrawer.IsOpen = !_settingsDrawer.IsOpen;
        }
    }

    // Removed old manual video gallery implementation - now using SkiaCamera's built-in MoveVideoToGalleryAsync method

}
