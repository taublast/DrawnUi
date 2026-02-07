using System.Diagnostics;
using AppoMobi.Specials;
using DrawnUi.Camera;
using DrawnUi.Views;
using Microsoft.Maui.Storage;

namespace PlayerTests.Views;

public partial class PlayerTestPage : BasePageReloadable, IDisposable
{
    private SkiaPlayer _playerControl;
    private SkiaButton _openFileButton;
    private SkiaButton _playPauseButton;
    private SkiaButton _stopButton;
    private SkiaButton _seekBackButton;
    private SkiaButton _seekForwardButton;
    private SkiaButton _volumeButton;
    private SkiaButton _loopButton;
    private SkiaLabel _statusLabel;
    private SkiaLabel _positionLabel;
    private SkiaLabel _durationLabel;
    private SkiaSlider _seekSlider;
    private SkiaSlider _volumeSlider;
    Canvas Canvas;

 

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
            _playerControl?.StopAsync();
            _playerControl = null;
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

    public PlayerTestPage()
    {
        Title = "SkiaPlayer Test";

        CreateContent();

        // Set up event handlers
        if (_seekSlider != null)
        {
            _seekSlider.EndChanged += (sender, args) =>
            {
                if (_playerControl.Duration.TotalSeconds > 0)
                {
                    var newPosition = TimeSpan.FromSeconds(_seekSlider.End / 100.0 * _playerControl.Duration.TotalSeconds);
                    _playerControl.SeekAsync(newPosition);
                }
            };
        }

        if (_volumeSlider != null)
        {
            _volumeSlider.EndChanged += (sender, args) =>
            {
                _playerControl.Volume = _volumeSlider.End;
            };
        }
    }

    private void CreateContent()
    {
        Canvas = new Canvas
        {
            BackgroundColor = Colors.Black,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Gestures = GesturesMode.Lock,
            RenderingMode = RenderingModeType.Accelerated
        };

        var mainStack = new SkiaGrid
        {
            RowSpacing = 16,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                // Video player area
                new SkiaStack()
                {
                    UseCache = SkiaCacheType.Operations,
                    Spacing = 16,
                    Children =
                    {
                        // Player control
                        new SkiaPlayer()
                        {
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                            BackgroundColor = Colors.Black
                        }
                        .Assign(out _playerControl)
                        .ObserveSelf((me, prop) =>
                        {
                            if (prop == nameof(me.IsVideoLoaded) || prop == nameof(me.IsPlaying) ||
                                prop == nameof(me.Position) || prop == nameof(me.Duration))
                            {
                                UpdateStatusText();
                            }
                        }),

                        // Seek slider
                        new SkiaSlider()
                        {
                            HorizontalOptions = LayoutOptions.Fill,
                            Min = 0,
                            Max = 100,
                            End = 0,
                            IsVisible = false
                        }
                        .Assign(out _seekSlider)
                        .ObserveProperty(_playerControl, nameof(_playerControl.IsVideoLoaded), me =>
                        {
                            me.IsVisible = _playerControl.IsVideoLoaded;
                        })
                        .ObserveProperty(_playerControl, nameof(_playerControl.Position), me =>
                        {
                            if (_playerControl.Duration.TotalSeconds > 0)
                            {
                                me.End = (_playerControl.Position.TotalSeconds / _playerControl.Duration.TotalSeconds) * 100.0;
                            }
                        }),
                    }
                },

                // Controls area
                new SkiaStack()
                {
                    UseCache = SkiaCacheType.Operations,
                    Spacing = 16,
                    Padding = new Thickness(16),
                    BackgroundColor = Colors.White.WithAlpha(0.9f),
                    Children =
                    {
                        // Status labels
                        new SkiaLabel("Player Status: No video loaded")
                        {
                            FontSize = 14,
                            TextColor = Colors.Gray,
                            HorizontalOptions = LayoutOptions.Center,
                            UseCache = SkiaCacheType.Operations
                        }
                        .Assign(out _statusLabel),

                        // Position and duration
                        new SkiaStack()
                        {
                            Type = LayoutType.Row,
                            HorizontalOptions = LayoutOptions.Center,
                            Spacing = 16,
                            Children =
                            {
                                new SkiaLabel("00:00")
                                {
                                    FontSize = 12,
                                    TextColor = Colors.Black,
                                    UseCache = SkiaCacheType.Operations
                                }
                                .Assign(out _positionLabel),

                                new SkiaLabel("/ 00:00")
                                {
                                    FontSize = 12,
                                    TextColor = Colors.Gray,
                                    UseCache = SkiaCacheType.Operations
                                }
                                .Assign(out _durationLabel),
                            }
                        },

                        // Control buttons
                        new SkiaWrap
                        {
                            Spacing = 8,
                            HorizontalOptions = LayoutOptions.Center,
                            Children =
                            {
                                // Open file button
                                new SkiaButton("📁 Open File")
                                {
                                    BackgroundColor = Colors.Blue,
                                    TextColor = Colors.White,
                                    CornerRadius = 8,
                                    UseCache = SkiaCacheType.Image,
                                    Padding = new Thickness(20, 8)
                                }
                                .Assign(out _openFileButton)
                                .OnTapped(async me => { await OpenFileAsync(); }),

                                // Play/Pause button
                                new SkiaButton("▶️ Play")
                                {
                                    BackgroundColor = Colors.Green,
                                    TextColor = Colors.White,
                                    CornerRadius = 8,
                                    UseCache = SkiaCacheType.Image,
                                    Padding = new Thickness(20, 8)
                                }
                                .Assign(out _playPauseButton)
                                .OnTapped(async me => { await TogglePlayPauseAsync(); })
                                .ObserveProperty(_playerControl, nameof(_playerControl.IsPlaying), me =>
                                {
                                    me.Text = _playerControl.IsPlaying ? "⏸️ Pause" : "▶️ Play";
                                    me.BackgroundColor = _playerControl.IsPlaying ? Colors.Orange : Colors.Green;
                                    me.IsEnabled = _playerControl.IsVideoLoaded;
                                    me.Opacity = me.IsEnabled ? 1.0 : 0.5;
                                }),

                                // Stop button
                                new SkiaButton("⏹️ Stop")
                                {
                                    BackgroundColor = Colors.Red,
                                    TextColor = Colors.White,
                                    CornerRadius = 8,
                                    UseCache = SkiaCacheType.Image,
                                    Padding = new Thickness(20, 8)
                                }
                                .Assign(out _stopButton)
                                .OnTapped(async me => { await _playerControl.StopAsync(); })
                                .ObserveProperty(_playerControl, nameof(_playerControl.IsVideoLoaded), me =>
                                {
                                    me.IsEnabled = _playerControl.IsVideoLoaded;
                                    me.Opacity = me.IsEnabled ? 1.0 : 0.5;
                                }),

                                // Seek back button
                                new SkiaButton("⏪ -10s")
                                {
                                    BackgroundColor = Colors.Purple,
                                    TextColor = Colors.White,
                                    CornerRadius = 8,
                                    UseCache = SkiaCacheType.Image,
                                    Padding = new Thickness(20, 8)
                                }
                                .Assign(out _seekBackButton)
                                .OnTapped(async me =>
                                {
                                    var newPosition = _playerControl.Position - TimeSpan.FromSeconds(10);
                                    if (newPosition < TimeSpan.Zero) newPosition = TimeSpan.Zero;
                                    await _playerControl.SeekAsync(newPosition);
                                })
                                .ObserveProperty(_playerControl, nameof(_playerControl.IsVideoLoaded), me =>
                                {
                                    me.IsEnabled = _playerControl.IsVideoLoaded;
                                    me.Opacity = me.IsEnabled ? 1.0 : 0.5;
                                }),

                                // Seek forward button
                                new SkiaButton("⏩ +10s")
                                {
                                    BackgroundColor = Colors.Purple,
                                    TextColor = Colors.White,
                                    CornerRadius = 8,
                                    UseCache = SkiaCacheType.Image,
                                    Padding = new Thickness(20, 8)
                                }
                                .Assign(out _seekForwardButton)
                                .OnTapped(async me =>
                                {
                                    var newPosition = _playerControl.Position + TimeSpan.FromSeconds(10);
                                    if (newPosition > _playerControl.Duration) newPosition = _playerControl.Duration;
                                    await _playerControl.SeekAsync(newPosition);
                                })
                                .ObserveProperty(_playerControl, nameof(_playerControl.IsVideoLoaded), me =>
                                {
                                    me.IsEnabled = _playerControl.IsVideoLoaded;
                                    me.Opacity = me.IsEnabled ? 1.0 : 0.5;
                                }),

                                // Volume button
                                new SkiaButton("🔊 Volume")
                                {
                                    BackgroundColor = Colors.Teal,
                                    TextColor = Colors.White,
                                    CornerRadius = 8,
                                    UseCache = SkiaCacheType.Image,
                                    Padding = new Thickness(20, 8)
                                }
                                .Assign(out _volumeButton)
                                .OnTapped(me => { ToggleVolumeSlider(); }),

                                // Loop button
                                new SkiaButton("🔄 Loop: OFF")
                                {
                                    BackgroundColor = Colors.DarkGray,
                                    TextColor = Colors.White,
                                    CornerRadius = 8,
                                    UseCache = SkiaCacheType.Image,
                                    Padding = new Thickness(20, 8)
                                }
                                .Assign(out _loopButton)
                                .OnTapped(me => { _playerControl.IsLooping = !_playerControl.IsLooping; })
                                .ObserveProperty(_playerControl, nameof(_playerControl.IsLooping), me =>
                                {
                                    me.Text = _playerControl.IsLooping ? "🔄 Loop: ON" : "🔄 Loop: OFF";
                                    me.BackgroundColor = _playerControl.IsLooping ? Colors.DarkGreen : Colors.DarkGray;
                                }),
                            }
                        },

                        // Volume slider (initially hidden)
                        new SkiaSlider()
                        {
                            HorizontalOptions = LayoutOptions.Fill,
                            Min = 0,
                            Max = 1,
                            End = 1,
                            IsVisible = false
                        }
                        .Assign(out _volumeSlider)
                        .ObserveProperty(_playerControl, nameof(_playerControl.Volume), me =>
                        {
                            me.End = _playerControl.Volume;
                        }),
                    }
                }.WithRow(1)
            }
        }.WithRowDefinitions("*, Auto");

        Canvas.Content = mainStack;
        Content = Canvas;

        // Set up event handlers
        _playerControl.PositionChanged += OnPositionChanged;
        _playerControl.PlaybackEnded += OnPlaybackEnded;
        _playerControl.VideoLoaded += OnVideoLoaded;
    }

    private void OnPositionChanged(object sender, TimeSpan position)
    {
        UpdateStatusText();
    }

    private void OnPlaybackEnded(object sender, EventArgs e)
    {
        Debug.WriteLine("[PlayerTestPage] Playback ended");
        UpdateStatusText();
    }

    private void OnVideoLoaded(object sender, EventArgs e)
    {
        Debug.WriteLine($"[PlayerTestPage] Video loaded: {_playerControl.Duration.TotalSeconds:F2}s");
        Debug.WriteLine($"[PlayerTestPage] IsVideoLoaded: {_playerControl.IsVideoLoaded}");
        UpdateStatusText();
        
        // Auto-play the video when it's loaded
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                Debug.WriteLine("[PlayerTestPage] Attempting to auto-play...");
                await _playerControl.PlayAsync();
                Debug.WriteLine($"[PlayerTestPage] Auto-play started. IsPlaying: {_playerControl.IsPlaying}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlayerTestPage] Failed to auto-play: {ex.Message}");
                Debug.WriteLine($"[PlayerTestPage] Exception details: {ex.StackTrace}");
            }
        });
    }

    private void UpdateStatusText()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            string status = "No video loaded";
            if (_playerControl.IsVideoLoaded)
            {
                status = _playerControl.IsPlaying ? "Playing" : "Paused";
                status += $" | {_playerControl.Position:mm\\:ss} / {_playerControl.Duration:mm\\:ss}";
            }

            _statusLabel.Text = $"Player Status: {status}";
            _positionLabel.Text = _playerControl.Position.ToString(@"mm\:ss");
            _durationLabel.Text = $"/ {_playerControl.Duration.ToString(@"mm\:ss")}";
        });
    }

    private async Task OpenFileAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select a video file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.movie" } },
                    { DevicePlatform.Android, new[] { "video/*" } },
                    { DevicePlatform.WinUI, new[] { ".mp4", ".avi", ".mov", ".mkv" } },
                    { DevicePlatform.macOS, new[] { "public.movie" } }
                })
            });

            if (result != null)
            {
                Debug.WriteLine($"[PlayerTestPage] Selected file: {result.FullPath}");
                _playerControl.Source = result.FullPath;
                Debug.WriteLine($"[PlayerTestPage] Set source to: {_playerControl.Source}");
                await _playerControl.LoadAsync();
                Debug.WriteLine($"[PlayerTestPage] LoadAsync completed. IsVideoLoaded: {_playerControl.IsVideoLoaded}");
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Error", $"Failed to open file: {ex.Message}");
        }
    }

    private async Task TogglePlayPauseAsync()
    {
        try
        {
            if (_playerControl.IsPlaying)
            {
                await _playerControl.PauseAsync();
            }
            else
            {
                await _playerControl.PlayAsync();
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Error", $"Playback error: {ex.Message}");
        }
    }

    private void ToggleVolumeSlider()
    {
        _volumeSlider.IsVisible = !_volumeSlider.IsVisible;
    }
}
