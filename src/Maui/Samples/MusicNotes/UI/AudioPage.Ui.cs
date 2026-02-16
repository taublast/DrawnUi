using AppoMobi.Specials;
using DrawnUi.Camera;
using DrawnUi.Controls;
using DrawnUi.Views;
using FastPopups;
using MusicNotes.Audio;
using MusicNotes.Effects;
using MusicNotes.Helpers;
using ShadersCamera.Views;

namespace MusicNotes.UI
{
    public partial class AudioPage
    {
        private SkiaShape _takePictureButton;
        private SkiaLabel _statusLabel;
        private SettingsButton _videoRecordButton;
        private SettingsButton _audioCodecButton;
        private SkiaLayer _previewOverlay;
        private SkiaImage _previewImage;
        private SkiaImage _previewThumbnail;
        private SettingsButton _preRecordingToggleButton;
        private SettingsButton _preRecordingDurationButton;
        private SkiaLabel _captionsLabel;
        private SkiaDrawer _settingsDrawer;
        private SkiaViewSwitcher _settingsTabs;
        private SkiaLabel[] _tabLabels;
        private AudioVisualizer _rhythmDetector;
        private AudioVisualizer _metronome;
        private AudioVisualizer _musicBPMDetector;
        private int _currentMode = 0; // 0=Notes, 1=DrummerBPM, 2=Metronome, 3=MusicBPM
        private SkiaLabel _modeButtonIcon;

        private bool _isLayoutLandscape;
        private SkiaShape _captureButtonOuter;
        private AudioVisualizer _musicNotes;
        private AudioVisualizer _equalizer;

        private void CreateContent()
        {
            bool isSimulator = false;
            SkiaLayout mainStack = null;

            if (mainStack == null)
            {
                mainStack = new SkiaLayout
                {
                    BackgroundColor = Colors.HotPink,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaSvg()
                        {
                            UseCache = SkiaCacheType.Operations,
                            Source = @"Svg\54170161_9344048.svg",
                            Aspect = TransformAspect.Fill,
                            TintColor = Colors.Maroon.WithAlpha(0.2f)
                        }.Fill(),
                        // Fullscreen Camera preview
                        new AudioRecorder()
                        {
                            IsVisible=false,
                        }
                        .Assign(out Recorder),

                        new AudioVisualizer(new AudioInstrumentTuner())
                        {
                            Margin = new (16,16,16,0),
                            HorizontalOptions = LayoutOptions.Fill,
                            HeightRequest = 350,
                            BackgroundColor = Color.Parse("#22000000"),
                        }.Assign(out _musicNotes),

                        new AudioVisualizer(new AudioRhythmDetector())
                        {
                            Margin = new (16,16,16,0),
                            HorizontalOptions = LayoutOptions.Fill,
                            HeightRequest = 350,
                            BackgroundColor = Color.Parse("#22000000"),
                            IsVisible = false,
                        }.Assign(out _rhythmDetector),

                        new AudioVisualizer(new AudioMetronome())
                        {
                            Margin = new (16,16,16,0),
                            HorizontalOptions = LayoutOptions.Fill,
                            HeightRequest = 350,
                            BackgroundColor = Color.Parse("#22000000"),
                            IsVisible = false,
                        }.Assign(out _metronome),

                        new AudioVisualizer(new AudioMusicBPMDetector())
                        {
                            Margin = new (16,16,16,0),
                            HorizontalOptions = LayoutOptions.Fill,
                            HeightRequest = 350,
                            BackgroundColor = Color.Parse("#22000000"),
                            IsVisible = false,
                        }.Assign(out _musicBPMDetector),

                        new AudioVisualizer(new AudioSoundBars())
                        {
                            BackgroundColor = Color.Parse("#22000000"),
                            HorizontalOptions = LayoutOptions.Fill,
                            Margin = new (16,380,16,0),
                            HeightRequest = 80,
                        }.Assign(out _equalizer),


                        // Bottom Menu Bar
                        new SkiaShape()
                        {
                            UseCache = SkiaCacheType.Operations,
                            Type = ShapeType.Rectangle,
                            CornerRadius = 32,
                            //BackgroundColor = Color.FromArgb("#DD000000"),
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.End,
                            Margin = new Thickness(0, 0, 0, 16),
                            Children =
                            {
                                new SkiaBackdrop()
                                {
                                    HorizontalOptions = LayoutOptions.Fill,
                                    VerticalOptions = LayoutOptions.Fill,
                                    Blur = 0,
                                    VisualEffects = new List<SkiaEffect>
                                    {
                                        new GlassBackdropEffect()
                                        {
                                            ShaderSource = @"Shaders\glass.sksl",
                                            CornerRadius = 32,  // Match parent SkiaShape
                                            GlassDepth = 1.25f   // 3D emboss intensity (0.0-2.0+)
                                        }
                                    }
                                },
                                new SkiaRow()
                                {
                                    Margin = new Thickness(16, 10),
                                    Spacing = 16,
                                    HorizontalOptions = LayoutOptions.Center,
                                    VerticalOptions = LayoutOptions.Center,
                                    Children =
                                    {
                                        // Mode Button
                                        new SkiaShape()
                                        {
                                            Type = ShapeType.Rectangle,
                                            CornerRadius = 12,
                                            BackgroundColor = Color.FromArgb("#3B82F6"),
                                            WidthRequest = 48,
                                            HeightRequest = 48,
                                            Children =
                                            {
                                                new SkiaLabel()
                                                {
                                                    Text = IconFont.Music,
                                                    TextColor = Colors.WhiteSmoke,
                                                    FontFamily = "FontIcons",
                                                    FontSize = 24,
                                                    HorizontalOptions = LayoutOptions.Center,
                                                    VerticalOptions = LayoutOptions.Center,
                                                }
                                                .Assign(out _modeButtonIcon)
                                            },
                                            UseCache = SkiaCacheType.Image,
                                            FillGradient = new SkiaGradient()
                                            {
                                                Type = GradientType.Linear,
                                                Colors = new List<Color>()
                                                {
                                                    Colors.DarkGrey,
                                                    Colors.CornflowerBlue,
                                                    Colors.Gray
                                                },
                                                ColorPositions = new List<double>()
                                                {
                                                    0.0,
                                                    0.2,
                                                    1.0,
                                                }
                                            }
                                        }
                                        .OnTapped(me =>
                                        {
                                            ToggleVisualizerMode();
                                        }),

                                        // Settings Button
                                        new SkiaShape()
                                        {
                                            UseCache = SkiaCacheType.Image,
                                            Type = ShapeType.Rectangle,
                                            CornerRadius = 12,
                                            BackgroundColor = Color.FromArgb("#6B7280"),
                                            WidthRequest = 48,
                                            HeightRequest = 48,
                                            Children =
                                            {
                                                new SkiaLabel()
                                                {
                                                    Text = IconFont.Cog,
                                                    TextColor = Colors.WhiteSmoke,
                                                    FontFamily = "FontIcons",
                                                    FontSize = 24,
                                                    HorizontalOptions = LayoutOptions.Center,
                                                    VerticalOptions = LayoutOptions.Center,
                                                },
                                            },
                                            FillGradient = new SkiaGradient()
                                            {
                                                Type = GradientType.Linear,
                                                Colors = new List<Color>()
                                                {
                                                    Colors.DarkGrey,
                                                    Colors.DarkCyan,
                                                    Colors.Gray
                                                },
                                                ColorPositions = new List<double>()
                                                {
                                                    0.0,
                                                    0.2,
                                                    1.0,
                                                }
                                            }
                                        }
                                        .OnTapped(me =>
                                        {
                                            MainThread.BeginInvokeOnMainThread(() =>
                                            {
                                                var popup = new AudioPageSettingsPopup(this);
                                                this.ShowPopup(popup);
                                            });
                                            //ToggleSettingsDrawer();
                                        }),

                                        // Help Button
                                        new SkiaShape()
                                        {
                                            Type = ShapeType.Rectangle,
                                            CornerRadius = 12,
                                            BackgroundColor = Color.FromArgb("#6B7280"),
                                            WidthRequest = 48,
                                            HeightRequest = 48,
                                            Children =
                                            {
                                                new SkiaLabel()
                                                {
                                                    Text = IconFont.Help,
                                                    //Text = IconFont.CloudQuestion,
                                                    TextColor = Colors.WhiteSmoke,
                                                    FontFamily = "FontIcons",
                                                    FontSize = 24,
                                                    HorizontalOptions = LayoutOptions.Center,
                                                    VerticalOptions = LayoutOptions.Center,
                                                },
                                            },
                                            UseCache = SkiaCacheType.Image,
                                            FillGradient = new SkiaGradient()
                                            {
                                                Type = GradientType.Linear,
                                                Colors = new List<Color>()
                                                {
                                                    Colors.DarkGrey,
                                                    Colors.Orange,
                                                    Colors.Gray
                                                },
                                                ColorPositions = new List<double>()
                                                {
                                                    0.0,
                                                    0.2,
                                                    1.0,
                                                }
                                            }
                                        }
                                        .OnTapped(me => { /* Help action */ }),

                                        /*
                                        // Profile Button
                                        new SkiaShape()
                                        {
                                            Type = ShapeType.Rectangle,
                                            CornerRadius = 12,
                                            BackgroundColor = Color.FromArgb("#6B7280"),
                                            WidthRequest = 48,
                                            HeightRequest = 48,
                                            Children =
                                            {
                                                new SkiaRichLabel("👤")
                                                {
                                                    FontSize = 24,
                                                    HorizontalOptions = LayoutOptions.Center,
                                                    VerticalOptions = LayoutOptions.Center,
                                                }
                                            },
                                            UseCache = SkiaCacheType.Image,
                                            FillGradient = new SkiaGradient()
                                            {
                                                Type = GradientType.Linear,
                                                Colors = new List<Color>()
                                                {
                                                    Colors.DarkGrey,
                                                    Colors.DarkOliveGreen,
                                                    Colors.Gray
                                                },
                                                ColorPositions = new List<double>()
                                                {
                                                    0.0,
                                                    0.2,
                                                    1.0,
                                                }
                                            }
                                        }
                                        .OnTapped(me => {  }),
                                        */
                                    }
                                }
                            },
                        },

                    }
                };
            }

            // Main layer that contains both the main stack and preview overlay
            var rootLayer = new SkiaLayer
            {
                VerticalOptions = LayoutOptions.Fill,
                Children =
            {
                mainStack,
                _previewOverlay,
#if xDEBUG
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
                if (Recorder != null)
                {
                    Tasks.StartDelayed(TimeSpan.FromMilliseconds(500), () =>
                    {
                        Recorder.IsOn = true;
                        // Speech recognition will auto-start/stop based on recording state
                    });
                }
            };

            Content = new Grid() //due to maui layout specifics we are forced to use a Grid as root wrapper
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children = { Canvas }
            };

            if (Recorder != null)
            {
                // Setup SkiaCamera event handlers and apply user settings to it
                AttachHardware(true);
            }

            ToggleVisualizerMode(UserSettings.Current.Module);
        }

        public void ShowAlert(string title, string message)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert(title, message, "OK");
            });
        }

        private void ToggleVisualizerMode(int index=-1)
        {
            var oldMode = _currentMode;
            if (index >= 0)
            {
                _currentMode = index;
            }
            else
            {
                // Cycle through modes: 0=Notes, 1=DrummerBPM, 2=Metronome, 3=MusicBPM
                _currentMode = (_currentMode + 1) % 4;
            }

            // Hide all visualizers
            if (_musicNotes != null)
                _musicNotes.IsVisible = false;
            if (_rhythmDetector != null)
                _rhythmDetector.IsVisible = false;
            if (_metronome != null)
                _metronome.IsVisible = false;
            if (_musicBPMDetector != null)
                _musicBPMDetector.IsVisible = false;
            
            // Show current mode visualizer
            switch (_currentMode)
            {
                case 0: // Notes
                    if (_musicNotes != null)
                        _musicNotes.IsVisible = true;
                    if (_modeButtonIcon != null)
                        _modeButtonIcon.Text = IconFont.PlaylistMusic;//"🎵";
                    break;
                case 1: // Drummer BPM
                    if (_rhythmDetector != null)
                        _rhythmDetector.IsVisible = true;
                    if (_modeButtonIcon != null)
                        _modeButtonIcon.Text = IconFont.DotsCircle; // IconFont.TimerMusic;// IconFont.Metronome;// "🥁";
                    break;
                case 2: // Metronome
                    if (_metronome != null)
                        _metronome.IsVisible = true;
                    if (_modeButtonIcon != null)
                        _modeButtonIcon.Text = IconFont.AccountMusic;// "⏱️";
                    break;
                case 3: // Music BPM
                    if (_musicBPMDetector != null)
                        _musicBPMDetector.IsVisible = true;
                    if (_modeButtonIcon != null)
                        _modeButtonIcon.Text = IconFont.TimerMusic;//IconFont.Music;//"🎼";
                    break;
            }

            if (oldMode != _currentMode)
            {
                UserSettings.Current.Module = _currentMode;
                UserSettings.Save();
            }
        }

    }
}
