using AppoMobi.Specials;
using DrawnUi.Camera;
using DrawnUi.Controls;
using DrawnUi.Views;
using FastPopups;
using MusicNotes.Audio;
using MusicNotes.Helpers;
using ShadersCamera.Views;

namespace MusicNotes.UI
{
    public partial class AudioPage
    {
        private SkiaLabel _modeSwitchButton;
        private AudioVisualizer _rhythmDetector;
        private AudioVisualizer _metronome;
        private AudioVisualizer _musicBPMDetector;
        private int _currentMode = 0; // 0=Notes, 1=DrummerBPM, 2=Metronome, 3=MusicBPM
        private SkiaRichLabel _modeButtonIcon;


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


                        // Recording Indicator (pulsing red dot)
                        new SkiaShape()
                        {
                            Type = ShapeType.Circle,
                            UseCache = SkiaCacheType.Operations,
                            BackgroundColor = Colors.Red,
                            LockRatio = 1,
                            WidthRequest = 20,
                            HeightRequest = 20,
                            Margin = 24,
                            HorizontalOptions = LayoutOptions.Start,
                            VerticalOptions = LayoutOptions.Start,
                            IsVisible = false
                        }
                        .ObserveProperty(Recorder, nameof(Recorder.IsRecording), me =>
                        {
                            me.IsVisible = Recorder.IsRecording;
                            if (me.IsVisible)
                            {
                                // Add pulsing animation effect
                                me.Animate("pulse", callback: (v) => me.Opacity = v,
                                    start: 1.0, end: 0.3, length: 800, repeat: () => true);
                            }
                            else
                            {
                                me.AbortAnimation("pulse");
                                me.Opacity = 1.0;
                            }
                        }),

                        // OVERLAY CONTROLS - Native camera style
                        new SkiaLayout()
                        {
                            IsVisible=false,
                            VerticalOptions = LayoutOptions.Fill,
                            HorizontalOptions = LayoutOptions.Fill,
                            UseCache = SkiaCacheType.GPU,
                            Children =
                            {
                                // Mode Label (PHOTO / VIDEO) - tap to switch
                                new SkiaShape()
                                {
                                    Type = ShapeType.Rectangle,
                                    CornerRadius = 12,
                                    BackgroundColor = Color.FromArgb("#66000000"),
                                    Padding = new Thickness(10, 6),
                                    Margin = new Thickness(0, 0, 0, 122),
                                    HorizontalOptions = LayoutOptions.Center,
                                    VerticalOptions = LayoutOptions.End,
                                    Children =
                                    {
                                        new SkiaLabel()
                                        {
                                            Text = "PHOTO",
                                            TextColor = Colors.White,
                                            FontSize = 12,
                                            UseCache = SkiaCacheType.Operations,
                                            VerticalOptions = LayoutOptions.Center,
                                            HorizontalOptions = LayoutOptions.Center,
                                        }
                                        .Assign(out _modeSwitchButton)
                                    }
                                }
                                .OnTapped(me => { ToggleCaptureMode(); })
                                .ObserveProperty(Recorder, nameof(Recorder.CaptureMode), me =>
                                {
                                    _modeSwitchButton.Text = Recorder.CaptureMode == CaptureModeType.Still ? "PHOTO" : "VIDEO";
                                })
                                .ObserveProperty(Recorder, nameof(Recorder.IsRecording), me =>
                                {
                                    // Hide during recording to prevent mode changes
                                    me.IsVisible = !Recorder.IsRecording;
                                }),

                                // Bottom Control Bar
                                new SkiaShape()
                                {
                                    HorizontalOptions = LayoutOptions.Center,
                                    VerticalOptions = LayoutOptions.End,
                                    Margin = new Thickness(0, 0, 0, 44),
                                    Padding = new Thickness(8, 0),
                                    HeightRequest = 70,
                                    StrokeColor = Colors.Black,
                                    StrokeWidth = -1,
                                    BackgroundColor = Color.FromArgb("#66000000"),
                                    CornerRadius = 32,
                                    Children =
                                    {
                                        new SkiaRow()
                                        {
                                            UseCache = SkiaCacheType.GPU,
                                            Padding = new Thickness(1),
                                            Spacing = 10,
                                            HorizontalOptions = LayoutOptions.Center,
                                            VerticalOptions = LayoutOptions.Center,
                                            Children =
                                            {

                                                // Preview thumbnail (last captured)
                                                new SkiaShape()
                                                {
                                                    VerticalOptions = LayoutOptions.Center,
                                                    StrokeColor = Color.FromArgb("#66CECECE"),
                                                    StrokeWidth = 1,
                                                    Type = ShapeType.Circle,
                                                    HeightRequest = 46,
                                                    LockRatio = 1,
                                                    BackgroundColor = Color.FromArgb("#66000000"),
                                                    IsClippedToBounds = true,
                                                    UseCache = SkiaCacheType.Image,
                                                    Children =
                                                    {
                                                        new SkiaImage()
                                                        {
                                                            RescalingQuality = SKFilterQuality.None,
                                                            Aspect = TransformAspect.AspectCover,
                                                            HorizontalOptions = LayoutOptions.Fill,
                                                            VerticalOptions = LayoutOptions.Fill,
                                                        }
                                                        .Assign(out _previewThumbnail)
                                                    }
                                                }
                                                .OnTapped(me =>
                                                {
                                                    if (Recorder.IsPreRecording || Recorder.IsRecording)
                                                    {
                                                        _ = AbortVideoRecording();
                                                    }
                                                    else
                                                    {
                                                        ShowLastCapturedPreview();
                                                    }
                                                }),

                                                // Settings button
                                                new SkiaShape()
                                                {
                                                    VerticalOptions = LayoutOptions.Center,
                                                    StrokeColor = Color.FromArgb("#66CECECE"),
                                                    StrokeWidth = 1,
                                                    UseCache = SkiaCacheType.Image,
                                                    Type = ShapeType.Circle,
                                                    HeightRequest = 46,
                                                    LockRatio = 1,
                                                    BackgroundColor = Colors.Black,
                                                    Children =
                                                    {
                                                        new SkiaRichLabel("⚙️")
                                                        {
                                                            FontSize = 20,
                                                            VerticalOptions = LayoutOptions.Center,
                                                            HorizontalOptions = LayoutOptions.Center,
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

                                                 
                                                // MORPHING CAPTURE BUTTON
                                                new SkiaShape()
                                                {
                                                    VerticalOptions = LayoutOptions.Center,
                                                    UseCache = SkiaCacheType.Image,
                                                    Type = ShapeType.Circle,
                                                    HeightRequest = 58,
                                                    LockRatio = 1,
                                                    StrokeWidth = 4,
                                                    StrokeColor = Color.FromArgb("#D9D9D9"),
                                                    BackgroundColor = Colors.Transparent,
                                                    Padding = new Thickness(4),
                                                    Children =
                                                    {
                                                        new SkiaShape()
                                                        {
                                                            Type = ShapeType.Circle,
                                                            BackgroundColor = Color.FromArgb("#CECECE"),
                                                            WidthRequest = 60,
                                                            CornerRadius = 30,
                                                            HorizontalOptions = LayoutOptions.Center,
                                                            VerticalOptions = LayoutOptions.Center,
                                                            LockRatio = 1,
                                                        }
                                                        .Assign(out _takePictureButton)
                                                    }
                                                }
                                                .Assign(out _captureButtonOuter)
                                                .OnTapped(async me =>
                                                {
                                                    // Tap feedback animation
                                                    await me.ScaleToAsync(1.1, 1.1, 100);
                                                    await me.ScaleToAsync(1.0, 1.0, 100);

                                                    if (Recorder.CaptureMode == CaptureModeType.Still)
                                                    {
                                                        await TakePictureAsync();
                                                    }
                                                    else
                                                    {
                                                        ToggleVideoRecording();
                                                    }
                                                })
                                                .ObserveProperty(Recorder, nameof(Recorder.State), me =>
                                                {
                                                    me.IsEnabled = Recorder.State == CameraState.On;
                                                    me.Opacity = me.IsEnabled ? 1.0 : 0.5;
                                                })
                                                .ObserveProperties(Recorder, new []{nameof(Recorder.IsRecording), nameof(Recorder.IsPreRecording)}, me =>
                                                {
                                                    UpdateCaptureButtonShape();
                                                })

                                            }
                                        }
                                    }
                                }
                            }
                        },

                        // Bottom Menu Bar
                        new SkiaShape()
                        {
                            Type = ShapeType.Rectangle,
                            BackgroundColor = Color.FromArgb("#DD000000"),
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.End,
                            Margin = new Thickness(0, 0, 0, 16),
                            Padding = new Thickness(12, 8),
                            HeightRequest = 64,
                            CornerRadius = 32,
                            Children =
                            {
                                new SkiaRow()
                                {
                                    Spacing = 20,
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
                                                new SkiaRichLabel("🎵")
                                                {
                                                    FontSize = 24,
                                                    HorizontalOptions = LayoutOptions.Center,
                                                    VerticalOptions = LayoutOptions.Center,
                                                }
                                                .Assign(out _modeButtonIcon)
                                            }
                                        }
                                        .OnTapped(me => { ToggleVisualizerMode(); }),

                                        // Settings Button
                                        new SkiaShape()
                                        {
                                            Type = ShapeType.Rectangle,
                                            CornerRadius = 12,
                                            BackgroundColor = Color.FromArgb("#6B7280"),
                                            WidthRequest = 48,
                                            HeightRequest = 48,
                                            Children =
                                            {
                                                new SkiaRichLabel("⚙️")
                                                {
                                                    FontSize = 24,
                                                    HorizontalOptions = LayoutOptions.Center,
                                                    VerticalOptions = LayoutOptions.Center,
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
                                                new SkiaRichLabel("❓")
                                                {
                                                    FontSize = 24,
                                                    HorizontalOptions = LayoutOptions.Center,
                                                    VerticalOptions = LayoutOptions.Center,
                                                }
                                            }
                                        }
                                        .OnTapped(me => { /* Help action */ }),

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
                                            }
                                        }
                                        .OnTapped(me => { /* Profile action */ }),
                                    }
                                }
                            }
                        },

                        // Settings Drawer (slides up from bottom)
                        new SkiaDrawer()
                        {
                            IsVisible = false,
                            Margin = new Thickness(2, 0, 2, 96),
                            HeaderSize = 40,
                            Direction = DrawerDirection.FromBottom,
                            VerticalOptions = LayoutOptions.End,
                            HorizontalOptions = LayoutOptions.Fill,
                            MaximumHeightRequest = 300,
                            IsOpen = false,
                            BlockGesturesBelow = true,
                            IgnoreWrongDirection = true,
                            ZIndex = 60,
                            Content = new SkiaShape()
                            {
                                Type = ShapeType.Rectangle,
                                CornerRadius = new CornerRadius(20, 20, 0, 0),
                                HorizontalOptions = LayoutOptions.Fill,
                                VerticalOptions = LayoutOptions.Fill,
                                BackgroundColor = Color.FromArgb("#2B0B98"),
                                StrokeWidth = 1,
                                StrokeColor = Color.FromArgb("#22000000"),
                                Children =
                                {
                                    new SkiaLayout()
                                    {
                                        HorizontalOptions = LayoutOptions.Fill,
                                        VerticalOptions = LayoutOptions.Fill,
                                        Children =
                                        {
                                            CreateDrawerHeader(),
                                            CreateDrawerContent()
                                        }
                                    }
                                }
                            }
                        }
                        .Assign(out _settingsDrawer),

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

        #region DRAWER

        private SkiaShape CreateDrawerHeader()
        {
            return new SkiaShape()
            {
                UseCache = SkiaCacheType.GPU,
                HorizontalOptions = LayoutOptions.Fill,
                Type = ShapeType.Rectangle,
                BackgroundColor = Colors.Transparent,
                VerticalOptions = LayoutOptions.Start,
                HeightRequest = 40,
                Children =
                {
                    new SkiaLayout()
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        Children =
                        {
                            new SkiaShape()
                            {
                                Type = ShapeType.Rectangle,
                                WidthRequest = 40,
                                HeightRequest = 5,
                                BackgroundColor = Color.FromArgb("#CCCCCC"),
                                CornerRadius = 3,
                                HorizontalOptions = LayoutOptions.Center,
                                VerticalOptions = LayoutOptions.Center
                            }
                        }
                    }
                }
            }
            .OnTapped(me => ToggleSettingsDrawer());
        }

        private SkiaLayout CreateDrawerContent()
        {
            _tabLabels = new SkiaLabel[3];

            var tabBar = new SkiaLayout()
            {
                UseCache = SkiaCacheType.Image,
                Type = LayoutType.Row,
                HorizontalOptions = LayoutOptions.Fill,
                HeightRequest = 36,
                Spacing = 0,
                Children =
                {
                    new SkiaLabel("📸 Capture")
                    {
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White,
                        HorizontalOptions = LayoutOptions.Fill,
                        HorizontalTextAlignment = DrawTextAlignment.Center,
                        VerticalOptions = LayoutOptions.Center,
                        UseCache = SkiaCacheType.Operations,
                    }
                    .Assign(out _tabLabels[0])
                    .OnTapped(me => SelectTab(0)),

                    new SkiaLabel("🎤 Processing")
                    {
                        FontSize = 13,
                        TextColor = Color.FromArgb("#888888"),
                        HorizontalOptions = LayoutOptions.Fill,
                        HorizontalTextAlignment = DrawTextAlignment.Center,
                        VerticalOptions = LayoutOptions.Center,
                        UseCache = SkiaCacheType.Operations,
                    }
                    .Assign(out _tabLabels[1])
                    .OnTapped(me => SelectTab(1)),

                    new SkiaLabel("🎬 Export")
                    {
                        FontSize = 13,
                        TextColor = Color.FromArgb("#888888"),
                        HorizontalOptions = LayoutOptions.Fill,
                        HorizontalTextAlignment = DrawTextAlignment.Center,
                        VerticalOptions = LayoutOptions.Center,
                        UseCache = SkiaCacheType.Operations,
                    }
                    .Assign(out _tabLabels[2])
                    .OnTapped(me => SelectTab(2)),
                }
            };

            // Tab 0: Capture Settings
            var captureSection =

                new SkiaScroll()
                {
                    Bounces = false,
                    BackgroundColor = Colors.Transparent,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaWrap
                    {
                        UseCache = SkiaCacheType.Operations,
                        Spacing = 8,
                        Padding = new Thickness(16),
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Start,
                        Children =
                        {

                            // Mode
                            new SettingsButton("📸", "Mode") { TintColor = Color.FromArgb("#0891B2"), }
                                .OnTapped(me => { ToggleCaptureMode(); })
                                .ObserveProperty(Recorder, nameof(Recorder.CaptureMode), me =>
                                {
                                    me.AccessoryIcon = Recorder.CaptureMode == CaptureModeType.Still ? "📸" : "🎥";
                                    me.Text = Recorder.CaptureMode == CaptureModeType.Still
                                        ? "Mode: Photo"
                                        : "Mode: Video";
                                    me.TintColor = Recorder.CaptureMode == CaptureModeType.Still
                                        ? Color.FromArgb("#0891B2")
                                        : Color.FromArgb("#7C3AED");
                                }),

                            //Video Source
                            new SettingsButton("📷", "Source") { TintColor = Color.FromArgb("#D97706"), }
                                .ObserveProperty(Recorder, nameof(Recorder.CameraIndex), async (me) =>
                                {
                                    var cameras = await Recorder.GetAvailableCamerasAsync();
                                    var index = Recorder.CameraIndex;
                                    if (index < 0)
                                    {
                                        index = 0;
                                    }

                                    var selectedCamera = cameras.First(c => c.Index == index);
                                    me.Text = $"{selectedCamera.Name}";
                                })
                                .OnTapped(async me => { await SelectCamera(); }),


                            new SettingsButton("🎤", "Audio Device") { TintColor = Color.FromArgb("#B45309"), }
                                .ObserveProperty(Recorder, nameof(Recorder.AudioDeviceIndex), async (me) =>
                                {
                                    if (Recorder.AudioDeviceIndex < 0)
                                    {
                                        me.Text = "System Default Audio";
                                    }
                                    else
                                    {
                                        var arrayDevices = await Recorder.GetAvailableAudioDevicesAsync();
                                        if (arrayDevices.Count > 0)
                                        {
                                            var device = arrayDevices[Recorder.AudioDeviceIndex];
                                            me.Text = $"{device}";
                                        }
                                        else
                                        {
                                            me.Text = "Error";
                                        }
                                    }
                                })
                                .OnTapped(async me => { await SelectAudioSource(); }),


                            //Video Formats
                            new SettingsButton("🗂️", "Formats") { TintColor = Color.FromArgb("#4F46E5"), }
                                .OnTapped(async me => { await ShowPhotoFormatPicker(); })
                                .ObserveProperties(Recorder,
                                    new[]
                                    {
                                        nameof(Recorder.PhotoFormatIndex), nameof(Recorder.CaptureMode),
                                        nameof(Recorder.CameraIndex),
                                    }, async (me) =>
                                    {
                                        var formats = await Recorder.GetAvailableCaptureFormatsAsync();
                                        if (formats.Count > 0)
                                        {
                                            var index = Recorder.PhotoFormatIndex;
                                            if (index < 0)
                                            {
                                                index = 0;
                                            }

                                            var format = formats.First(c => c.Index == index);
                                            me.Text = $"{format.Description}";
                                        }
                                    })
                                .ObserveProperty(Recorder, nameof(Recorder.CaptureMode), me =>
                                {
                                    me.IsVisible = Recorder.CaptureMode == CaptureModeType.Still;
                                }),
                            new SettingsButton("🗂️", "Formats") { TintColor = Color.FromArgb("#4F46E5"), }
                                .OnTapped(async me => { await ShowVideoFormatPicker(); })
                                .ObserveProperties(Recorder,
                                    new[]
                                    {
                                        nameof(Recorder.VideoFormatIndex), nameof(Recorder.CaptureMode),
                                        nameof(Recorder.CameraIndex),
                                    }, async (me) =>
                                    {
                                        var formats = await Recorder.GetAvailableVideoFormatsAsync();
                                        if (formats.Count > 0)
                                        {
                                            var index = Recorder.VideoFormatIndex;
                                            if (index < 0)
                                            {
                                                index = 0;
                                            }

                                            var format = formats.First(c => c.Index == index);
                                            me.Text = $"{format.Description}";
                                        }
                                    })
                                .ObserveProperty(Recorder, nameof(Recorder.CaptureMode), me =>
                                {
                                    me.IsVisible = Recorder.CaptureMode == CaptureModeType.Video;
                                }),

                            new SettingsButton("❌", "Abort")
                                {
                                    TintColor = Color.FromArgb("#991B1B"), IsVisible = false
                                }
                                .Assign(out _videoRecordButton)
                                .OnTapped(async me => { await AbortVideoRecording(); })
                                .ObserveProperty(Recorder, nameof(Recorder.IsRecording), me =>
                                {
                                    me.IsVisible = Recorder.IsRecording &&
                                                   Recorder.CaptureMode == CaptureModeType.Video;
                                })
                                .ObserveProperty(Recorder, nameof(Recorder.CaptureMode), me =>
                                {
                                    me.IsVisible = Recorder.IsRecording &&
                                                   Recorder.CaptureMode == CaptureModeType.Video;
                                }),
                        }
                    }
                };

            // Tab Processing
            var audioSection =
                new SkiaScroll()
                {
                    Bounces = false,
                    BackgroundColor = Colors.Transparent,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaWrap
                    {
                        Spacing = 8,
                        UseCache = SkiaCacheType.Operations,
                        Padding = new Thickness(16),
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Start,
                        Children =
                {
                    // Processing
                    new SettingsButton("⚙️", "Processing: ON")
                        {
                            TintColor = Color.FromArgb("#10B981"),
                        }
                        .OnTapped(me =>
                        {
                            Recorder.UseRealtimeVideoProcessing = !Recorder.UseRealtimeVideoProcessing;
                        })
                        .ObserveProperty(() => Recorder, nameof(Recorder.UseRealtimeVideoProcessing), me =>
                        {
                            me.Text = Recorder.UseRealtimeVideoProcessing ? "Processing: ON" : "Processing: OFF";
                            me.TintColor = Recorder.UseRealtimeVideoProcessing ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
                        }),

                    new SettingsButton("🎧", "Audio Monitor: OFF")
                    {
                        TintColor = Color.FromArgb("#6B7280"),
                    }
                    .OnTapped(me =>
                    {
                        Recorder.EnableAudioMonitoring = !Recorder.EnableAudioMonitoring;
                    })
                    .ObserveProperty(Recorder, nameof(Recorder.EnableAudioMonitoring), me =>
                    {
                        me.Text = Recorder.EnableAudioMonitoring ? "Audio Monitor: ON" : "Audio Monitor: OFF";
                        me.TintColor = Recorder.EnableAudioMonitoring ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
                    }),




                    new SettingsButton("📈", "Gain: ON")
                    {
                        TintColor = Color.FromArgb("#10B981"),
                    }
                    .OnTapped(me =>
                    {
                        Recorder.UseGain = !Recorder.UseGain;
                    })
                    .ObserveProperty(Recorder, nameof(Recorder.UseGain), me =>
                    {
                        me.Text = Recorder.UseGain ? "Gain: ON" : "Gain: OFF";
                        me.TintColor = Recorder.UseGain ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
                    }),



                }
                    }
                };

            // Tab 2: Export / Recording Settings
            var feedSection =
                new SkiaScroll()
                {
                    Bounces = false,
                    BackgroundColor = Colors.Transparent,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Content = new SkiaWrap
                    {
                        Spacing = 8,
                        UseCache = SkiaCacheType.Operations,
                        Padding = new Thickness(16),
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Start,
                        Children =
                {
                    new SettingsButton("🔇", "Audio")
                    {
                        TintColor = Color.FromArgb("#6B7280"),
                    }
                    .OnTapped(me =>
                    {
                        Recorder.EnableAudioRecording = !Recorder.EnableAudioRecording;
                    })
                    .ObserveProperty(Recorder, nameof(Recorder.EnableAudioRecording), me =>
                    {
                        me.AccessoryIcon = Recorder.EnableAudioRecording ? "🔊" : "🔇";
                        me.Text = Recorder.EnableAudioRecording ? "Audio: SAVE" : "Audio: SKIP";
                        me.TintColor = Recorder.EnableAudioRecording ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
                    })
                    .ObserveProperty(Recorder, nameof(Recorder.CaptureMode), me =>
                    {
                        me.IsVisible = Recorder.CaptureMode == CaptureModeType.Video;
                    }),

                    new SettingsButton("🎵", "Audio Codec")
                    {
                        TintColor = Color.FromArgb("#475569"),
                    }
                    .Assign(out _audioCodecButton)
                    .OnTapped(async me => { await SelectAudioCodec(); })
                    .ObserveProperty(Recorder, nameof(Recorder.CaptureMode), me =>
                    {
                        me.IsVisible = Recorder.CaptureMode == CaptureModeType.Video;
                    }),

                    new SettingsButton("📹", "Video")
                    {
                        TintColor = Color.FromArgb("#10B981"),
                    }
                    .OnTapped(me =>
                    {
                        Recorder.EnableVideoRecording = !Recorder.EnableVideoRecording;
                    })
                    .ObserveProperty(Recorder, nameof(Recorder.EnableVideoRecording), me =>
                    {
                        me.Text = Recorder.EnableVideoRecording ? "Video: SAVE" : "Video: SKIP";
                        me.TintColor = Recorder.EnableVideoRecording ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
                    })
                    .ObserveProperty(Recorder, nameof(Recorder.CaptureMode), me =>
                    {
                        me.IsVisible = Recorder.CaptureMode == CaptureModeType.Video;
                    }),

                    new SettingsButton("⏱️", "Pre-Record: OFF")
                    {
                        TintColor = Color.FromArgb("#6B7280"),
                    }
                    .Assign(out _preRecordingToggleButton)
                    .OnTapped(me => { TogglePreRecording(); })
                    .ObserveProperty(Recorder, nameof(Recorder.CaptureMode), me =>
                    {
                        me.IsVisible = Recorder.CaptureMode == CaptureModeType.Video;
                    }),

                    new SettingsButton("⏰", $"{Recorder.PreRecordDuration.TotalSeconds:F0}s")
                    {
                        TintColor = Color.FromArgb("#475569"),
                    }
                    .Assign(out _preRecordingDurationButton)
                    .OnTapped(async me => { await ShowPreRecordingDurationPicker(); })
                    .ObserveProperty(Recorder, nameof(Recorder.CaptureMode), me =>
                    {
                        me.IsVisible = Recorder.CaptureMode == CaptureModeType.Video;
                    }),

                   
                }
                    }
                };

            return new SkiaStack()
            {
                Spacing = 4,
                UseCache = SkiaCacheType.Operations,
                Margin = new Thickness(0, 40, 0, 0),
                Children =
                {
                    tabBar,
                    new SkiaViewSwitcher()
                    {
                        VerticalOptions = LayoutOptions.Fill,
                        HorizontalOptions = LayoutOptions.Fill,
                        SelectedIndex = 0,
                        Children =
                        {
                            captureSection,
                            audioSection,
                            feedSection,
                        }
                    }
                    .Assign(out _settingsTabs),
                }
            };
        }

        #endregion

        public void ShowAlert(string title, string message)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert(title, message, "OK");
            });
        }

        private void SelectTab(int index)
        {
            if (_settingsTabs != null)
                _settingsTabs.SelectedIndex = index;

            if (_tabLabels != null)
            {
                for (int i = 0; i < _tabLabels.Length; i++)
                {
                    _tabLabels[i].TextColor = i == index
                        ? Colors.White
                        : Color.FromArgb("#888888");
                    _tabLabels[i].FontAttributes = i == index
                        ? FontAttributes.Bold
                        : FontAttributes.None;
                }
            }
        }

        private void ToggleSettingsDrawer()
        {
            if (_settingsDrawer != null)
            {
                _settingsDrawer.IsOpen = !_settingsDrawer.IsOpen;
            }
        }

        private bool _isLayoutLandscape;
        private SkiaShape _captureButtonOuter;
        private AudioVisualizer _musicNotes;
        private AudioVisualizer _equalizer;

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
                        _modeButtonIcon.Text = "🎵";
                    break;
                case 1: // Drummer BPM
                    if (_rhythmDetector != null)
                        _rhythmDetector.IsVisible = true;
                    if (_modeButtonIcon != null)
                        _modeButtonIcon.Text = "🥁";
                    break;
                case 2: // Metronome
                    if (_metronome != null)
                        _metronome.IsVisible = true;
                    if (_modeButtonIcon != null)
                        _modeButtonIcon.Text = "⏱️";
                    break;
                case 3: // Music BPM
                    if (_musicBPMDetector != null)
                        _musicBPMDetector.IsVisible = true;
                    if (_modeButtonIcon != null)
                        _modeButtonIcon.Text = "🎼";
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
