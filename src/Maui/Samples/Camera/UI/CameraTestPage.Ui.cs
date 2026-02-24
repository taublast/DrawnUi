using AppoMobi.Specials;
using CameraTests.Services;
using CameraTests.UI;
using DrawnUi.Camera;
using DrawnUi.Controls;
using DrawnUi.Views;

namespace CameraTests.Views
{
    public partial class CameraTestPage
    {
        private SkiaLabel _modeSwitchButton;
        private SkiaShape _captureButtonOuter;

        private void CreateContent()
        {
            bool isSimulator = false;
            SkiaLayout mainStack = null;

#if IOS || MACCATALYST
        isSimulator = DeviceInfo.DeviceType == DeviceType.Virtual;
        if (isSimulator)
        {
            mainStack = CreateSimulatorTestUI();
        }
#endif

            if (mainStack == null)
            {
                mainStack = new SkiaLayout
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
            {

                // Fullscreen Camera preview
                new AppCamera()
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    BackgroundColor = Colors.Black,
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

                // Captions 
                new SkiaRichLabel()
                    {
                        UseCache = SkiaCacheType.Operations,
                        Margin = new(24, 32, 24, 0),
                        HorizontalTextAlignment = DrawTextAlignment.Center,
                        VerticalOptions = LayoutOptions.Start,
                        HorizontalOptions = LayoutOptions.Center,
                        TextColor = Colors.White,
                    }
                    .Assign(out _captionsLabel),

                // AI Transcription Indicator with background
                new SkiaShape()
                {
                    UseCache = SkiaCacheType.Operations,
                    Type = ShapeType.Circle,
                    BackgroundColor = Color.FromArgb("#CC1E3A8A"),
                    LockRatio = 1,
                    WidthRequest = 48,
                    Margin = 20,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Start,
                    Children =
                    {
                        new SkiaLottie()
                        {
                            Source = @"Lottie\ai.json",
                            AutoPlay = true,
                            Repeat = -1,
                            LockRatio = 1,
                            SpeedRatio = 2,
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                        }
                    }
                }
                .ObserveProperty(this, nameof(IsTranscribing), me =>
                {
                    me.IsVisible = this.IsTranscribing;
                }),

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
                .ObserveProperty(CameraControl, nameof(CameraControl.IsRecording), me =>
                {
                    me.IsVisible = CameraControl.IsRecording;
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
                        .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                        {
                            _modeSwitchButton.Text = CameraControl.CaptureMode == CaptureModeType.Still ? "PHOTO" : "VIDEO";
                        })
                        .ObserveProperty(CameraControl, nameof(CameraControl.IsRecording), me =>
                        {
                            // Hide during recording to prevent mode changes
                            me.IsVisible = !CameraControl.IsRecording;
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
                                            if (CameraControl.IsPreRecording || CameraControl.IsRecording)
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
                                        .OnTapped(me => { ToggleSettingsDrawer(); }),

                                        // Flash button
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
                                                new SkiaRichLabel("⚡")
                                                {
                                                    FontSize = 20,
                                                    VerticalOptions = LayoutOptions.Center,
                                                    HorizontalOptions = LayoutOptions.Center,
                                                }
                                                .Assign(out _flashButton)
                                            }
                                        }
                                        .OnTapped(me => { ToggleFlash(); })
                                        .ObserveProperty(CameraControl, nameof(CameraControl.FlashMode), me =>
                                        {
                                            _flashButton.Text = CameraControl.FlashMode == FlashMode.Off ? "⚡" :
                                                               CameraControl.FlashMode == FlashMode.On ? "💡" : "✨";
                                        }),

                                        // Flip Camera button
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
                                                new SkiaRichLabel("🔄")
                                                {
                                                    FontSize = 20,
                                                    VerticalOptions = LayoutOptions.Center,
                                                    HorizontalOptions = LayoutOptions.Center,
                                                }
                                            }
                                        }
                                        .Assign(out _cameraSelectButton)
                                        .OnTapped(async me => { await SelectCamera(); }),

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

                                            if (CameraControl.CaptureMode == CaptureModeType.Still)
                                            {
                                                await TakePictureAsync();
                                            }
                                            else
                                            {
                                                ToggleVideoRecording();
                                            }
                                        })
                                        .ObserveProperty(CameraControl, nameof(CameraControl.State), me =>
                                        {
                                            me.IsEnabled = CameraControl.State == CameraState.On;
                                            me.Opacity = me.IsEnabled ? 1.0 : 0.5;
                                        })
                                        .ObserveProperties(CameraControl, new []{nameof(CameraControl.IsRecording), nameof(CameraControl.IsPreRecording)}, me =>
                                        {
                                            UpdateCaptureButtonShape();
                                        })

                                    }
                                }
                            }
                        }
                    }
                },

                // Status Display (top-center)
                /*
                new SkiaShape()
                {
                    UseCache = SkiaCacheType.Operations,
                    Type = ShapeType.Rectangle,
                    BackgroundColor = Color.FromArgb("#CC000000"),
                    CornerRadius = 12,
                    Padding = new(12, 6),
                    Margin = new(0, 52, 0, 0),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Start,
                    Children =
                    {
                        new SkiaLabel("Camera: Off")
                        {
                            FontSize = 12,
                            TextColor = Colors.Gray,
                            FontAttributes = FontAttributes.Bold,
                            HorizontalOptions = LayoutOptions.Center,
                            UseCache = SkiaCacheType.Operations
                        }
                        .Assign(out _statusLabel)
                    }
                },
                */

                // Settings Drawer (slides up from bottom)
                new SkiaDrawer()
                {
                    Margin = new Thickness(2, 0, 2, 0),
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


            // Create preview overlay (initially hidden)
            _previewOverlay = CreateTakenPhotoPreviewPopup();

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
                if (CameraControl != null)
                {
                    Tasks.StartDelayed(TimeSpan.FromMilliseconds(500), () =>
                    {
                        CameraControl.IsOn = true;
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

            // Initialize captions engine (disposes previous on hot reload)
            _captionsEngine?.Dispose();
            _captionsEngine = new RealtimeCaptionsEngine(_captionsLabel, fontSize: 16f, maxLines: 3, expirySeconds: 8.0);

            if (CameraControl != null)
            {
                // Configure camera for capture video flow testing
                CameraControl.UseRealtimeVideoProcessing = true;
                CameraControl.VideoQuality = VideoQuality.Standard;
                CameraControl.EnableAudioRecording = true;

                // Setup camera event handlers
                AttachHardware(true);
            }

        }

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
                                .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                {
                                    me.AccessoryIcon = CameraControl.CaptureMode == CaptureModeType.Still ? "📸" : "🎥";
                                    me.Text = CameraControl.CaptureMode == CaptureModeType.Still
                                        ? "Mode: Photo"
                                        : "Mode: Video";
                                    me.TintColor = CameraControl.CaptureMode == CaptureModeType.Still
                                        ? Color.FromArgb("#0891B2")
                                        : Color.FromArgb("#7C3AED");
                                }),

                            //Video Source
                            new SettingsButton("📷", "Source") { TintColor = Color.FromArgb("#D97706"), }
                                .ObserveProperty(CameraControl, nameof(CameraControl.CameraIndex), async (me) =>
                                {
                                    var cameras = await CameraControl.GetAvailableCamerasAsync();
                                    var index = CameraControl.CameraIndex;
                                    if (index < 0)
                                    {
                                        index = 0;
                                    }

                                    var selectedCamera = cameras.First(c => c.Index == index);
                                    me.Text = $"{selectedCamera.Name}";
                                })
                                .OnTapped(async me => { await SelectCamera(); }),
                            new SettingsButton("🎤", "Audio Device") { TintColor = Color.FromArgb("#B45309"), }
                                .ObserveProperty(CameraControl, nameof(CameraControl.AudioDeviceIndex), async (me) =>
                                {
                                    if (CameraControl.AudioDeviceIndex < 0)
                                    {
                                        me.Text = "System Default Audio";
                                    }
                                    else
                                    {
                                        var arrayDevices = await CameraControl.GetAvailableAudioDevicesAsync();
                                        if (arrayDevices.Count > 0)
                                        {
                                            var device = arrayDevices[CameraControl.AudioDeviceIndex];
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
                                .ObserveProperties(CameraControl,
                                    new[]
                                    {
                                        nameof(CameraControl.PhotoFormatIndex), nameof(CameraControl.CaptureMode),
                                        nameof(CameraControl.CameraIndex),
                                    }, async (me) =>
                                    {
                                        var formats = await CameraControl.GetAvailableCaptureFormatsAsync();
                                        if (formats.Count > 0)
                                        {
                                            var index = CameraControl.PhotoFormatIndex;
                                            if (index < 0)
                                            {
                                                index = 0;
                                            }

                                            var format = formats.First(c => c.Index == index);
                                            me.Text = $"{format.Description}";
                                        }
                                    })
                                .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                {
                                    me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Still;
                                }),
                            new SettingsButton("🗂️", "Formats") { TintColor = Color.FromArgb("#4F46E5"), }
                                .OnTapped(async me => { await ShowVideoFormatPicker(); })
                                .ObserveProperties(CameraControl,
                                    new[]
                                    {
                                        nameof(CameraControl.VideoFormatIndex), nameof(CameraControl.CaptureMode),
                                        nameof(CameraControl.CameraIndex),
                                    }, async (me) =>
                                    {
                                        var formats = await CameraControl.GetAvailableVideoFormatsAsync();
                                        if (formats.Count > 0)
                                        {
                                            var index = CameraControl.VideoFormatIndex;
                                            if (index < 0)
                                            {
                                                index = 0;
                                            }

                                            var format = formats.First(c => c.Index == index);
                                            me.Text = $"{format.Description}";
                                        }
                                    })
                                .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                {
                                    me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                }),
                            new SettingsButton("❌", "Abort")
                                {
                                    TintColor = Color.FromArgb("#991B1B"), IsVisible = false
                                }
                                .Assign(out _videoRecordButton)
                                .OnTapped(async me => { await AbortVideoRecording(); })
                                .ObserveProperty(CameraControl, nameof(CameraControl.IsRecording), me =>
                                {
                                    me.IsVisible = CameraControl.IsRecording &&
                                                   CameraControl.CaptureMode == CaptureModeType.Video;
                                })
                                .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                {
                                    me.IsVisible = CameraControl.IsRecording &&
                                                   CameraControl.CaptureMode == CaptureModeType.Video;
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
                            CameraControl.UseRealtimeVideoProcessing = !CameraControl.UseRealtimeVideoProcessing;
                        })
                        .ObserveProperty(() => CameraControl, nameof(CameraControl.UseRealtimeVideoProcessing), me =>
                        {
                            me.Text = CameraControl.UseRealtimeVideoProcessing ? "Processing: ON" : "Processing: OFF";
                            me.TintColor = CameraControl.UseRealtimeVideoProcessing ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
                        }),

                    new SettingsButton("🎧", "Audio Monitor: OFF")
                    {
                        TintColor = Color.FromArgb("#6B7280"),
                    }
                    .OnTapped(me =>
                    {
                        CameraControl.EnableAudioMonitoring = !CameraControl.EnableAudioMonitoring;
                    })
                    .ObserveProperty(CameraControl, nameof(CameraControl.EnableAudioMonitoring), me =>
                    {
                        me.Text = CameraControl.EnableAudioMonitoring ? "Audio Monitor: ON" : "Audio Monitor: OFF";
                        me.TintColor = CameraControl.EnableAudioMonitoring ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
                    }),

                    new SettingsButton("📊", "Visualizer")
                    {
                        TintColor = Color.FromArgb("#65A30D"),
                    }
                    .OnTapped(me =>
                    {
                        CameraControl.SwitchVisualizer();
                    })
                    .ObserveProperty(()=>CameraControl, nameof(CameraControl.VisualizerName), me =>
                    {
                        me.Text = CameraControl.VisualizerName;
                    }),

                    new SettingsButton("📈", "Gain: ON")
                    {
                        TintColor = Color.FromArgb("#10B981"),
                    }
                    .OnTapped(me =>
                    {
                        CameraControl.UseGain = !CameraControl.UseGain;
                    })
                    .ObserveProperty(CameraControl, nameof(CameraControl.UseGain), me =>
                    {
                        me.Text = CameraControl.UseGain ? "Gain: ON" : "Gain: OFF";
                        me.TintColor = CameraControl.UseGain ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
                    }),

                    new SettingsButton("🎙️", "Speech: OFF")
                    {
                        TintColor = Color.FromArgb("#475569"),
                    }
                    .Assign(out _speechButton)
                    .OnTapped(me =>
                    {
                        ToggleSpeech();
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
                        CameraControl.EnableAudioRecording = !CameraControl.EnableAudioRecording;
                    })
                    .ObserveProperty(CameraControl, nameof(CameraControl.EnableAudioRecording), me =>
                    {
                        me.AccessoryIcon = CameraControl.EnableAudioRecording ? "🔊" : "🔇";
                        me.Text = CameraControl.EnableAudioRecording ? "Audio: SAVE" : "Audio: SKIP";
                        me.TintColor = CameraControl.EnableAudioRecording ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
                    })
                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                    {
                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                    }),

                    new SettingsButton("🎵", "Audio Codec")
                    {
                        TintColor = Color.FromArgb("#475569"),
                    }
                    .Assign(out _audioCodecButton)
                    .OnTapped(async me => { await SelectAudioCodec(); })
                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                    {
                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                    }),

                    new SettingsButton("📹", "Video")
                    {
                        TintColor = Color.FromArgb("#10B981"),
                    }
                    .OnTapped(me =>
                    {
                        CameraControl.EnableVideoRecording = !CameraControl.EnableVideoRecording;
                    })
                    .ObserveProperty(CameraControl, nameof(CameraControl.EnableVideoRecording), me =>
                    {
                        me.Text = CameraControl.EnableVideoRecording ? "Video: SAVE" : "Video: SKIP";
                        me.TintColor = CameraControl.EnableVideoRecording ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
                    })
                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                    {
                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                    }),

                    new SettingsButton("⏱️", "Pre-Record: OFF")
                    {
                        TintColor = Color.FromArgb("#6B7280"),
                    }
                    .Assign(out _preRecordingToggleButton)
                    .OnTapped(me => { TogglePreRecording(); })
                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                    {
                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                    }),

                    new SettingsButton("⏰", $"{CameraControl.PreRecordDuration.TotalSeconds:F0}s")
                    {
                        TintColor = Color.FromArgb("#475569"),
                    }
                    .Assign(out _preRecordingDurationButton)
                    .OnTapped(async me => { await ShowPreRecordingDurationPicker(); })
                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                    {
                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                    }),

                    new SettingsButton("📍", "Geotag: OFF")
                    {
                        TintColor = Color.FromArgb("#6B7280"),
                    }
                    .OnTapped(me =>
                    {
                        CameraControl.InjectGpsLocation = !CameraControl.InjectGpsLocation;
                        RefreshGpsLocationIfNeeded();
                    })
                    .ObserveProperty(CameraControl, nameof(CameraControl.InjectGpsLocation), me =>
                    {
                        me.Text = CameraControl.InjectGpsLocation ? "Geotag: ON" : "Geotag: OFF";
                        me.TintColor = CameraControl.InjectGpsLocation ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
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

        private SkiaLayer CreateTakenPhotoPreviewPopup()
        {
            return new SkiaLayer
            {
                IsVisible = false,
                UseCache = SkiaCacheType.Operations,
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
                        UseCache = SkiaCacheType.Operations
                    }
                    .OnTapped(me => { HidePreviewOverlay(); }),

                // Main preview image container
                new SkiaLayout
                {
                    UseCache = SkiaCacheType.Image,
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
                                    }
                                    .Assign(out _previewImage)
                            }
                        }
                    }
                },

                // Action buttons at bottom
                new SkiaRow
                {
                    UseCache = SkiaCacheType.Image,
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

        void ShowAlert(string title, string message)
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
        public bool IsLayoutLandscape
        {
            get => _isLayoutLandscape;
            set
            {
                if (value == _isLayoutLandscape) return;
                _isLayoutLandscape = value;
                OnPropertyChanged();
            }
        }

        private void OnPermissionsResultChanged(object sender, bool e)
        {
            if (!e)
            {
                ShowAlert("Error", "The application does not have the required permissions to access all the camera features.");
            }
        }
    }
}
