using System.Diagnostics;
using System.Text.RegularExpressions;
using AppoMobi.Specials;
using CameraTests.Services;
using CameraTests.UI;
using CameraTests.Visualizers;
using DrawnUi.Camera;
using DrawnUi.Controls;
using DrawnUi.Infrastructure;
using DrawnUi.Views;
using Microsoft.Maui.Platform;

namespace CameraTests.Views;

public partial class MainPage : BasePageReloadable, IDisposable
{
    private AppCamera CameraControl;
    private SkiaSvg _settingsButtonIcon;
    private SkiaShape _takePictureButton;
    private SkiaSvg _iconButtonFlash;
    private SkiaControl _buttonFlash;
    private SkiaControl _buttonSettings;
    private SkiaLabel _statusLabel;
    private SkiaLayer _headerPanel;
    private SkiaLayout _cameraControlsPanel;
    private SkiaShape _recordingStopButton;
    private SkiaLabel _recordingStopLabel;
    private SettingsButton _videoRecordButton;
    private SettingsButton _speechButton;
    private IRealtimeTranscriptionService _realtimeTranscriptionService;
    private SkiaShape _buttonSelectCamera;
    private SkiaSvg _cameraSelectButtonIcon;

    private SettingsButton _audioCodecButton;
    private SkiaLayer _previewOverlay;
    private SkiaImage _previewImage;
    private SkiaImage _previewThumbnail;
    private SettingsButton _preRecordingButton;
    private SkiaLabel _captionsLabel;
    private SkiaLabel _captionHintLabel;
    private RealtimeCaptionsEngine _captionsEngine;
    private SkiaDrawer _settingsDrawer;
    private SkiaViewSwitcher _settingsTabs;
    private SkiaLabel[] _tabLabels;
    private FrameOverlay _previewFrameOverlay;
    private DeviceOrientation _orientation;
    AudioVisualizer _audioVisualizer;
    Canvas Canvas;
    private int _lastAudioRate;
    private int _lastAudioBits;
    private int _lastAudioChannels;
    private bool _audioMonitoringEnabled;
    private bool _speechEnabled;
    bool _isTranscribing;
    private RealtimeTranscriptionSessionState _transcriptionState;
    private string _transcriptionStatusMessage;
    private bool _hasVisibleCaptions;

    public MainPage(IRealtimeTranscriptionService realtimeTranscriptionService)
    {
        Title = "SkiaCamera";

        //iOS statusbar and bottom insets color
        BackgroundColor = Colors.Black;

        Super.RotationChanged += OnRotationChanged;
        Super.OrientationChanged += OnOrientationChanged;

        _realtimeTranscriptionService = realtimeTranscriptionService;
        if (_realtimeTranscriptionService != null)
        {
            _realtimeTranscriptionService.TranscriptionDelta += OnTranscriptionDelta;
            _realtimeTranscriptionService.TranscriptionCompleted += OnTranscriptionCompleted;
            _realtimeTranscriptionService.SpeechActivityChanged += OnSpeechActivityChanged;
            _realtimeTranscriptionService.SessionStateChanged += OnTranscriptionSessionStateChanged;
            _realtimeTranscriptionService.SessionError += OnTranscriptionSessionError;
        }
    }

    private void OnOrientationChanged(object sender, DeviceOrientation e)
    {
        _orientation = e;
    }

    private void OnRotationChanged(object sender, int rotation)
    {
        UpdateCameraControlsRotation(rotation);
    }


    private void UpdateCameraControlsRotation(int rotation)
    {
        var iconRotation = -NormalizeIconRotation(rotation);
        ApplyRotation(_buttonSettings, iconRotation);
        ApplyRotation(_buttonFlash, iconRotation);
        ApplyRotation(_buttonSelectCamera, iconRotation);
    }

    private static void ApplyRotation(SkiaControl control, double rotation)
    {
        if (control != null)
        {
            control.Rotation = rotation;
        }
    }

    private static int NormalizeIconRotation(int rotation)
    {
        var normalized = rotation % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        return normalized switch
        {
            270 => -90,
            _ => normalized,
        };
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            Super.RotationChanged -= OnRotationChanged;
            Super.OrientationChanged -= OnOrientationChanged;
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
                _realtimeTranscriptionService.SpeechActivityChanged -= OnSpeechActivityChanged;
                _realtimeTranscriptionService.SessionStateChanged -= OnTranscriptionSessionStateChanged;
                _realtimeTranscriptionService.SessionError -= OnTranscriptionSessionError;
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
        bool hotreload = false;
        if (Canvas != null)
        {
            OnClosed();
            Canvas?.Dispose();
            _previewFrameOverlay?.Dispose();
            hotreload = true;
        }

        if (!hotreload)
        {
            //ONCE per app launch
            Tasks.StartDelayed(TimeSpan.FromMilliseconds(500), () =>
            {
                //for fast use in transitions
                SkSl.Precompile(MauiProgram.ShaderRemoveCaption);
            });
        }

        Canvas = CreateCanvas();

        this.Content =
            new Grid() //maui needs this for safeinsets on ios
            {
                Children = { Canvas }
            };

        _previewFrameOverlay = new FrameOverlay();
        _captionsLabel = _previewFrameOverlay.CaptionsLabel;
        CameraControl.InitializeOverlayLayouts(_previewFrameOverlay); // null recording = reuse preview overlay

        UpdateCameraControlsRotation(Super.DeviceRotation);
        InitializeCaptionsEngine();
        UpdateOverlayCaptionsVisibility();
        UpdateAudioMonitoringVisibility();
        OnRecordingStateChanged();

        OnOpen();
    }

    void OnClosed()
    {
        CameraControl?.Stop();
        AttachHardware(false);
        CameraControl = null;
        _previewFrameOverlay?.Dispose();
        _previewFrameOverlay = null;
    }

    void OnOpen()
    {
#if IOS
        Super.MaxFps = 30;
#elif ANDROID
        Super.SetNavigationBarColor(Colors.Black, Colors.Black, true);
        Super.SetStatusBarColor(Colors.Black.ToPlatform());
#endif

        AttachHardware(true);

        UpdateCameraControlsRotation(Super.DeviceRotation);
    }

    private void InitializeCaptionsEngine()
    {
        if (_captionsEngine != null)
        {
            _captionsEngine.CaptionsChanged -= OnCaptionsChanged;
        }

        _captionsEngine?.Dispose();
        _captionsEngine = null;

        if (_captionsLabel != null)
        {
            _captionsEngine = new RealtimeCaptionsEngine(maxLines: 3, expirySeconds: 4.0);
            _captionsEngine.CaptionsChanged += OnCaptionsChanged;
            UpdateOverlayCaptionsVisibility();
        }
    }

    private void AttachHardware(bool subscribe)
    {
        if (subscribe)
        {
            AttachHardware(false);

            CameraControl.PermissionsResult += OnPermissionsResultChanged;
            CameraControl.StateChanged += OnCameraStateChanged;
            CameraControl.CaptureSuccess += OnCaptureSuccess;
            CameraControl.CaptureFailed += OnCaptureFailed;
            CameraControl.OnError += OnCameraError;
            CameraControl.RecordingSuccess += OnVideoRecordingSuccess;
            CameraControl.RecordingProgress += OnVideoRecordingProgress;
            CameraControl.AudioSampleAvailable += OnAudioCaptured;

            // Monitor recording state changes to start/stop speech recognition
            CameraControl.IsRecordingVideoChanged += OnIsRecordingStateChanged;
            CameraControl.IsPreRecordingVideoChanged += OnIsPreRecordingStateChanged;

            //CameraControl.OnAudioSample += HUD.AddAudioSample;
        }
        else
        {
            if (CameraControl != null)
            {
                CameraControl.PermissionsResult -= OnPermissionsResultChanged;
                CameraControl.StateChanged -= OnCameraStateChanged;
                CameraControl.CaptureSuccess -= OnCaptureSuccess;
                CameraControl.CaptureFailed -= OnCaptureFailed;
                CameraControl.OnError -= OnCameraError;
                CameraControl.RecordingSuccess -= OnVideoRecordingSuccess;
                CameraControl.RecordingProgress -= OnVideoRecordingProgress;
                CameraControl.AudioSampleAvailable -= OnAudioCaptured;
                CameraControl.IsRecordingVideoChanged -= OnIsRecordingStateChanged;
                CameraControl.IsPreRecordingVideoChanged -= OnIsPreRecordingStateChanged;

                //CameraControl.OnAudioSample -= HUD.AddAudioSample;
            }
        }
    }

    private void OnIsRecordingStateChanged(object sender, bool e)
    {
        OnRecordingStateChanged();
    }

    private void OnIsPreRecordingStateChanged(object sender, bool e)
    {
        OnRecordingStateChanged();
    }

    void RefreshGpsLocationIfNeeded()
    {
        if (CameraControl.InjectGpsLocation)
        {
            MainThread.BeginInvokeOnMainThread(() => { _ = CameraControl.RefreshGpsLocation(); });
        }
    }

    private void OnCameraStateChanged(object sender, HardwareState e)
    {
        if (e == HardwareState.On)
        {
            if (CameraControl.Display != null)
            {
                CameraControl.Display.Blur = 0;
            }
            RefreshGpsLocationIfNeeded();
        }
        else
        {
            if (CameraControl.Display != null)
            {
                CameraControl.Display.Blur = 10;
            }
        }
    }

    private void UpdateAudioMonitoringVisibility()
    {
        if (_previewFrameOverlay != null)
        {
            _previewFrameOverlay.SetAudioMonitoring(IsAudioMonitoringEnabled);
        }
    }

    private void UpdateOverlayCaptionsVisibility()
    {
        if (_previewFrameOverlay != null)
        {
            _previewFrameOverlay.SetCaptionsVisible(IsSpeechEnabled && IsAudioMonitoringEnabled);
        }
    }

    public bool IsAudioMonitoringEnabled
    {
        get => CameraControl?.EnableAudioMonitoring ?? _audioMonitoringEnabled;
        set
        {
            if (_audioMonitoringEnabled != value)
            {
                _audioMonitoringEnabled = value;

                if (CameraControl != null)
                {
                    CameraControl.EnableAudioMonitoring = value;
                }

                UpdateAudioMonitoringVisibility();
                UpdateOverlayCaptionsVisibility();
                OnPropertyChanged();
            }
        }
    }

    public bool IsSpeechEnabled
    {
        get => _speechEnabled;
        set
        {
            if (_speechEnabled != value)
            {
                _speechEnabled = value;
                UpdateOverlayCaptionsVisibility();
                OnPropertyChanged();
            }
        }
    }

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

    public RealtimeTranscriptionSessionState TranscriptionState
    {
        get => _transcriptionState;
        set
        {
            if (_transcriptionState != value)
            {
                _transcriptionState = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsTranscriptionFailed));
            }
        }
    }

    public bool IsTranscriptionFailed => TranscriptionState == RealtimeTranscriptionSessionState.Failed;

    public string TranscriptionStatusMessage
    {
        get => _transcriptionStatusMessage;
        set
        {
            if (_transcriptionStatusMessage != value)
            {
                _transcriptionStatusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    private void OnCaptionsChanged(IList<string> spans)
    {
        MainThread.BeginInvokeOnMainThread(() => { _previewFrameOverlay.SetCaptions(spans); });
    }

    private void OnTranscriptionSessionStateChanged(RealtimeTranscriptionSessionState state)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TranscriptionState = state;

            switch (state)
            {
                case RealtimeTranscriptionSessionState.Off:
                    IsTranscribing = false;
                    TranscriptionStatusMessage = null;
                    break;
                case RealtimeTranscriptionSessionState.Connecting:
                    IsTranscribing = false;
                    TranscriptionStatusMessage = "Connecting to transcription service...";
                    break;
                case RealtimeTranscriptionSessionState.Ready:
                    TranscriptionStatusMessage = null;
                    break;
                case RealtimeTranscriptionSessionState.Failed:
                    IsTranscribing = false;
                    break;
            }

            UpdateSpeechButtonState();
        });
    }

    private void OnTranscriptionSessionError(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TranscriptionStatusMessage = NormalizeTranscriptionErrorMessage(message);
            TranscriptionState = RealtimeTranscriptionSessionState.Failed;
            IsTranscribing = false;
            _captionsEngine?.Clear();
            UpdateSpeechButtonState();
        });
    }

    private string NormalizeTranscriptionErrorMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Transcription unavailable. Tap SPEECH to retry.";
        }

        if (message.Contains("503", StringComparison.OrdinalIgnoreCase))
        {
            return "Transcription unavailable (503). Tap SPEECH to retry.";
        }

        return "Transcription unavailable. Tap SPEECH to retry.";
    }

    private void UpdateSpeechButtonState()
    {
        if (_speechButton != null)
        {
            if (!IsSpeechEnabled)
            {
                _speechButton.Text = "Speech: OFF";
                _speechButton.TintColor = Color.FromArgb("#475569");
            }
            else if (IsTranscriptionFailed)
            {
                _speechButton.Text = "Speech: RETRY";
                _speechButton.TintColor = Color.FromArgb("#F97316");
            }
            else
            {
                _speechButton.Text = "Speech: ON";
                _speechButton.TintColor = Color.FromArgb("#10B981");
            }
        }
    }

    private void OnSpeechActivityChanged(bool state)
    {
        MainThread.BeginInvokeOnMainThread(() => { IsTranscribing = state; });
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
        if (_realtimeTranscriptionService != null && IsSpeechEnabled)
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
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (CameraControl == null)
                return;

            var isRecordingVideo = CameraControl.CaptureMode == CaptureModeType.Video &&
                                   (CameraControl.IsRecording || CameraControl.IsPreRecording);

            bool showAppUi = !isRecordingVideo;

            if (_settingsDrawer != null)
            {
                if (isRecordingVideo)
                {
                    _settingsDrawer.IsOpen = false;
                }

                _settingsDrawer.IsVisible = showAppUi;
            }

            if (_headerPanel != null)
            {
                _headerPanel.IsVisible = showAppUi;
            }

            if (_cameraControlsPanel != null)
            {
                _cameraControlsPanel.IsVisible = showAppUi;
            }

            if (_recordingStopButton != null)
            {
                _recordingStopButton.IsVisible = isRecordingVideo;
            }

            if (_recordingStopLabel != null && !isRecordingVideo)
            {
                _recordingStopLabel.Text = "Stop";
            }
        });
    }

    private void StartTranscription()
    {
        if (_realtimeTranscriptionService == null)
        {
            TranscriptionState = RealtimeTranscriptionSessionState.Failed;
            TranscriptionStatusMessage = "Transcription service is not available.";
            UpdateSpeechButtonState();
            return;
        }

        IsTranscribing = false;
        TranscriptionStatusMessage = "Connecting to transcription service...";
        TranscriptionState = RealtimeTranscriptionSessionState.Connecting;
        _realtimeTranscriptionService?.Start();
    }

    private void StopTranscription()
    {
        _realtimeTranscriptionService?.Stop();
        _captionsEngine?.Clear();
        UpdateOverlayCaptionsVisibility();
        IsTranscribing = false;
        TranscriptionStatusMessage = null;
        TranscriptionState = RealtimeTranscriptionSessionState.Off;
        UpdateSpeechButtonState();
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
                }, 0, 1, (uint)_morphSpeed, Easing.SinOut);

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
                }, 0, 1, (uint)_morphSpeed, Easing.SinIn);

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
            _takePictureButton.BackgroundColor = Colors.DarkRed;
            await Task.Run(async () => { await CameraControl.TakePicture(); });
        }
        finally
        {
            _takePictureButton.IsEnabled = true;
            _takePictureButton.BackgroundColor = Color.FromArgb("#CECECE");
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
    private string _lastSavedPhotoPath;
    private string _lastSavedVideoPath;
    private bool _lastMediaWasVideo;

    /// <summary>
    /// We have captured a Still hi-res photo 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="captured"></param>
    private async void OnCaptureSuccess(object sender, CapturedImage captured)
    {
        if (CameraControl.UseRealtimeVideoProcessing && CameraControl.VideoEffect != ShaderEffect.None)
        {
            //need process
            var imageWithEffect = await CameraControl.RenderCapturedPhotoAsync(captured, null, image =>
            {
                
                if (CameraControl.VideoEffect != ShaderEffect.None)
                {
                    var shaderEffect = new SkiaShaderEffect()
                    {
                        ShaderSource = ShaderEffectHelper.GetFilename(CameraControl.VideoEffect),
                    };
                    image.VisualEffects.Add(shaderEffect);
                }
            }, true);

            captured.Image.Dispose();
            captured.Image = imageWithEffect;

            //captured.Meta.Vendor = "DrawnUI";
            //captured.Meta.Model = "SkiaCamera";
        }

        SaveFinalPhotoInBackground(captured);
    }

    void SaveFinalPhotoInBackground(CapturedImage captured)
    {
        //can avoid blocking, go to background thread
        Tasks.StartDelayed(TimeSpan.FromMilliseconds(16), async () =>
        {
            try
            {
                _currentCapturedImage = captured;

                // Update preview thumbnail in bottom control bar
                if (_previewThumbnail != null)
                {
                    _previewThumbnail.SetImageInternal(captured.Image, false);
                }

                _lastMediaWasVideo = false;

                // Auto-save and open via OS on thumbnail tap
                var path = await CameraControl.SaveToGalleryAsync(captured, MauiProgram.Album);
                if (!string.IsNullOrEmpty(path))
                {
                    _lastSavedPhotoPath = path;
                }
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"Error saving photo: {ex.Message}", "OK");
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
        // since the display image is on the GPU surface we must access it on the GPU thread
        CameraControl.SafeAction(() => //wil be invoked when rendering canvas on GPU thread
        {
            var clone = CameraControl.Display.LoadedSource.Clone();

            //now can avoid blocking we work on CPU only
            Tasks.StartDelayed(TimeSpan.FromMilliseconds(16), async () =>
            {
                try
                {
                    Debug.WriteLine($"✅ Video recorded at: {capturedVideo.FilePath}");

                    using var previewImage = clone.Image.ToRasterImage();

                    var publicPath = await CameraControl.MoveVideoToGalleryAsync(capturedVideo, MauiProgram.Album);

                    if (!string.IsNullOrEmpty(publicPath))
                    {
                        Debug.WriteLine($"✅ Video moved to gallery: {publicPath}");
                        _lastSavedVideoPath = publicPath;

                        if (previewImage != null)
                        {
                            try
                            {
                                // Convert to bitmap for rotation
                                using var bitmap = SKBitmap.FromImage(previewImage);

                                // Apply rotation using SkiaCamera's Reorient method
                                // This physically rotates the image based on device rotation
                                using var rotatedBitmap = SkiaCamera.Reorient(bitmap, CameraControl.DeviceRotation);

                                if (_previewThumbnail != null)
                                {
                                    var thumbImage = SKImage.FromBitmap(rotatedBitmap);
                                    _previewThumbnail.SetImageInternal(thumbImage, false);

                                    /*
                                    var thumbnailsDir = Path.Combine(FileSystem.AppDataDirectory, "Thumbnails");
                                    Directory.CreateDirectory(thumbnailsDir);
                                    var thumbnailFilename = "debug.jpg";
                                    var thumbnailPath = Path.Combine(thumbnailsDir, thumbnailFilename);
                                    using (var data = rotatedBitmap.Encode(SKEncodedImageFormat.Jpeg, 80))
                                    using (var stream = File.OpenWrite(thumbnailPath))
                                    {
                                        data.SaveTo(stream);
                                    }
                                    */
                                }

                                _lastMediaWasVideo = true;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"❌ Thumbnail error: {ex}");
                            }
                        }
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
                finally
                {
                    clone?.Dispose();
                }
            });
        });
    }

    private void OpenLastSavedPhoto()
    {
        if (string.IsNullOrEmpty(_lastSavedPhotoPath))
            return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                SkiaCamera.OpenFileInGallery(_lastSavedPhotoPath);

//#if WINDOWS
//                await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(_lastSavedPhotoPath) });
//#else
//                SkiaCamera.OpenFileInGallery(_lastSavedPhotoPath);
//#endif
            }
            catch (Exception ex)
            {
                ShowAlert("Error", $"Cannot open photo: {ex.Message}");
                Debug.WriteLine($"[PhotoOpen] {ex}");
            }
        });
    }

    private void OpenLastSavedVideo()
    {
        if (string.IsNullOrEmpty(_lastSavedVideoPath))
            return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
#if WINDOWS
                await Launcher.OpenAsync(new OpenFileRequest { File = new ReadOnlyFile(_lastSavedVideoPath) });
#elif ANDROID
                await SkiaCamera.PlayVideoDirectly(_lastSavedVideoPath);
#else
                SkiaCamera.PlayVideoDirectly(_lastSavedVideoPath);
#endif
            }
            catch (Exception ex)
            {
                ShowAlert("Error", $"Cannot open video: {ex.Message}");
                Debug.WriteLine($"[VideoOpen] {ex}");
            }
        });
    }

    private void OnThumbnailTapped()
    {
        if (CameraControl.IsPreRecording || CameraControl.IsRecording)
        {
            _ = AbortVideoRecording();
        }
        else if (_lastMediaWasVideo)
        {
            OpenLastSavedVideo();
        }
        else
        {
            OpenLastSavedPhoto();
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

        if (_recordingStopLabel != null && CameraControl.IsRecording)
        {
            _recordingStopLabel.Text = $"Stop ({duration:mm\\:ss})";
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
        // _currentCapturedImage is intentionally kept so the thumbnail tap can re-open the preview.
        // It is replaced naturally when the next photo is taken.
    }

    private void ShowLastCapturedPreview()
    {
        if (_currentCapturedImage != null)
        {
            // If we have a captured image, show it in the preview overlay
            ShowPreviewOverlay(_currentCapturedImage.Image);
        }
    }

    /// <summary>
    /// Saved already took image and presented inside popup confirmation
    /// </summary>
    /// <returns></returns>
    private async Task SaveCurrentImageToGallery()
    {
        if (_currentCapturedImage == null)
            return;

        try
        {
            // Save to gallery, note we set reorient to false, because it should be handled by metadata in this case
            var path = await CameraControl.SaveToGalleryAsync(_currentCapturedImage, MauiProgram.Album);
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

    private void ShowIndexedPicker<T>(
        string title,
        Func<Task<List<T>>> getItems,
        Func<T, int, string> describe,
        Action<int, T> onSelected,
        string emptyMessage)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var items = await getItems();
                if (items?.Count > 0)
                {
                    var options = items.Select((item, i) => describe(item, i)).ToArray();
                    var result = await DisplayActionSheet(title, "Cancel", null, options);

                    if (!string.IsNullOrEmpty(result) && result != "Cancel")
                    {
                        var idx = Array.FindIndex(options, o => o == result);
                        if (idx >= 0)
                            onSelected(idx, items[idx]);
                    }
                }
                else
                {
                    ShowAlert(title, emptyMessage);
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error", $"Error: {ex.Message}");
            }
        });
    }

    private void ShowDefaultablePicker(
        string title,
        Func<Task<List<string>>> getItems,
        Action<int, string> onSelected,
        string emptyMessage)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var items = await getItems();
                if (items?.Count > 0)
                {
                    var options = new string[items.Count + 1];
                    options[0] = "System Default";
                    for (int i = 0; i < items.Count; i++)
                        options[i + 1] = items[i];

                    var result = await DisplayActionSheet(title, "Cancel", null, options);

                    if (!string.IsNullOrEmpty(result) && result != "Cancel")
                    {
                        if (result == "System Default")
                        {
                            onSelected(-1, null);
                        }
                        else
                        {
                            for (int i = 0; i < items.Count; i++)
                            {
                                if (items[i] == result)
                                {
                                    onSelected(i, items[i]);
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    ShowAlert(title, emptyMessage);
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error", $"Error: {ex.Message}");
            }
        });
    }

    private Task ShowPhotoFormatPicker()
    {
        ShowIndexedPicker<CaptureFormat>(
            "Select Photo Format",
            () => CameraControl.GetAvailableCaptureFormatsAsync(),
            (f, i) => $"[{i}] {f.Description}",
            (i, f) =>
            {
                CameraControl.PhotoQuality = CaptureQuality.Manual;
                CameraControl.PhotoFormatIndex = i;
                ShowAlert("Format Selected", $"Selected: {f.Description}");
            },
            "No photo formats available");
        return Task.CompletedTask;
    }

    private Task ShowVideoFormatPicker()
    {
        ShowIndexedPicker<VideoFormat>(
            "Select Video Format",
            () => CameraControl.GetAvailableVideoFormatsAsync(),
            (f, i) => $"[{i}] {f.Description}",
            (i, f) =>
            {
                CameraControl.VideoQuality = VideoQuality.Manual;
                CameraControl.VideoFormatIndex = i;
                ShowAlert("Format Selected", $"Selected: {f.Description}");
            },
            "No video formats available");
        return Task.CompletedTask;
    }

    private Task SelectCamera()
    {
        ShowIndexedPicker<CameraInfo>(
            "Select Camera",
            () => CameraControl.GetAvailableCamerasAsync(),
            (c, i) => $"[{i}] {c.Name} ({c.Position})",
            (i, c) =>
            {
                CameraControl.CameraIndex = c.Index;
                CameraControl.Facing = CameraPosition.Manual;
                Debug.WriteLine($"Selected: {c.Name} ({c.Position})\nId: {c.Id} Index: {c.Index}");
            },
            "No cameras available");
        return Task.CompletedTask;
    }

    private Task SelectAudioSource()
    {
        ShowDefaultablePicker(
            "Select Audio Source",
            () => CameraControl.GetAvailableAudioDevicesAsync(),
            (i, _) => CameraControl.AudioDeviceIndex = i,
            "No audio input devices found.");
        return Task.CompletedTask;
    }

    private async Task SelectAudioMode()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var modes = new[]
            {
                (CameraAudioMode.Default, "Default — standard system processing"),
                (CameraAudioMode.VideoRecording, "VideoRecording — native Camera app style"),
                (CameraAudioMode.Voice, "Voice — AGC, echo cancel, noise suppress"),
                (CameraAudioMode.Flat, "Flat — minimal processing, raw signal"),
            };

            var options = modes.Select(m => m.Item2).ToArray();
            var result = await DisplayActionSheet("Select Audio Mode", "Cancel", null, options);

            if (!string.IsNullOrEmpty(result) && result != "Cancel")
            {
                var selected = modes.FirstOrDefault(m => m.Item2 == result);
                CameraControl.AudioMode = selected.Item1;
            }
        });
    }

    private Task SelectAudioCodec()
    {
        ShowDefaultablePicker(
            "Select Audio Codec",
            () => CameraControl.GetAvailableAudioCodecsAsync(),
            (i, name) =>
            {
                CameraControl.AudioCodecIndex = i;
                _audioCodecButton.Text = i < 0 ? "Codec: Default" : CodecsHelper.GetShortName(name);
            },
            "No audio codecs available.");
        return Task.CompletedTask;
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

    private async Task ShowPreRecordPicker()
    {
        var durations = new (string Label, int Seconds)[]
        {
            ("Off", 0),
            ("3 seconds", 3),
            ("5 seconds", 5),
            ("10 seconds", 10),
        };

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var result = await DisplayActionSheet("Pre-Recording", "Cancel", null,
                    durations.Select(d => d.Label).ToArray());

                if (!string.IsNullOrEmpty(result) && result != "Cancel")
                {
                    var selected = durations.First(d => d.Label == result);
                    CameraControl.EnablePreRecording = selected.Seconds > 0;
                    if (selected.Seconds > 0)
                        CameraControl.PreRecordDuration = TimeSpan.FromSeconds(selected.Seconds);

                    UpdatePreRecordingStatus();
                    Debug.WriteLine($"Pre-Recording: {(CameraControl.EnablePreRecording ? $"{CameraControl.PreRecordDuration.TotalSeconds}s" : "OFF")}");
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error", $"Error setting pre-recording: {ex.Message}");
            }
        });
    }

    private void CycleEffect()
    {
        var values = Enum.GetValues<ShaderEffect>();
        var current = CameraControl.VideoEffect;
        var index = Array.IndexOf(values, current);
        var next = values[(index + 1) % values.Length];
        CameraControl.VideoEffect = next;
    }

    private void ToggleSpeech()
    {
        if (IsSpeechEnabled && IsTranscriptionFailed)
        {
            if (!IsAudioMonitoringEnabled)
            {
                IsAudioMonitoringEnabled = true;
            }

            StartTranscription();
            return;
        }

        IsSpeechEnabled = !IsSpeechEnabled;
        UpdateSpeechButtonState();

        if (IsSpeechEnabled)
        {
            if (!IsAudioMonitoringEnabled)
            {
                IsAudioMonitoringEnabled = true;
            }

            StartTranscription();
        }
        else
        {
            StopTranscription();
        }
    }
}
