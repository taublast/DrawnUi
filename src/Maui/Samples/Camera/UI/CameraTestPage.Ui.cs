using AppoMobi.Specials;
using CameraTests.Services;
using DrawnUi.Camera;
using DrawnUi.Controls;
using DrawnUi.Views;

namespace CameraTests.Views
{
    public partial class CameraTestPage
    {
        private SkiaButton _modeSwitchButton;

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
                    CaptureMode = CaptureModeType.Still,
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
                //new SkiaShape()
                //{
                //    Type=ShapeType.Circle,
                //    LockRatio = 1,
                //    WidthRequest = 32,
                //    Margin = 24,
                //    BackgroundColor = Color.FromArgb("#883300FF"),
                //    HorizontalOptions = LayoutOptions.End,
                //    VerticalOptions = LayoutOptions.End,
                //}
                //.ObserveProperty(this, nameof(IsTranscribing), me =>
                //{
                //    me.IsVisible = this.IsTranscribing;
                //}),

                // OVERLAY CONTROLS - Essential buttons on top of camera
                new SkiaRow()
                {
                    Spacing = 12,
                    Margin = new Thickness(16, 0, 16, 100),
                    VerticalOptions = LayoutOptions.End,
                    HorizontalOptions = LayoutOptions.Center,
                    Children =
                    {
                        // Power toggle button
                        new SkiaButton("⚡")
                        {
                            BackgroundColor = Color.FromArgb("#33000000"),
                            TextColor = Colors.White,
                            CornerRadius = 32,
                            UseCache = SkiaCacheType.Image,
                            WidthRequest = 64,
                            HeightRequest = 64,
                            FontSize = 28,
                            StrokeWidth = 2
                        }
                        .OnTapped(me => { CameraControl.IsOn = !CameraControl.IsOn; })
                        .ObserveProperty(CameraControl, nameof(CameraControl.State), me =>
                        {
                            me.BackgroundColor = CameraControl.State == CameraState.On 
                                ? Color.FromArgb("#BB10B981") 
                                : Color.FromArgb("#BBDC2626");
                            me.StrokeColor = CameraControl.State == CameraState.On ? Colors.LightGreen : Colors.LightPink;
                        }),

                        // Mode switch button
                        new SkiaButton("📸")
                        {
                            BackgroundColor = Color.FromArgb("#BB0891B2"),
                            TextColor = Colors.White,
                            CornerRadius = 32,
                            UseCache = SkiaCacheType.Image,
                            WidthRequest = 64,
                            HeightRequest = 64,
                            FontSize = 28
                        }
                        .Assign(out _modeSwitchButton)
                        .OnTapped(me => { ToggleCaptureMode(); })
                        .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                        {
                            me.Text = CameraControl.CaptureMode == CaptureModeType.Still ? "📸" : "🎥";
                            me.BackgroundColor = CameraControl.CaptureMode == CaptureModeType.Still
                                ? Color.FromArgb("#BB0891B2")
                                : Color.FromArgb("#BB7C3AED");
                        }),

                        // Main Capture/Record button (large center button)  
                        new SkiaButton("📷")
                        {
                            BackgroundColor = Color.FromArgb("#DD2563EB"),
                            TextColor = Colors.White,
                            CornerRadius = 45,
                            UseCache = SkiaCacheType.Image,
                            WidthRequest = 90,
                            HeightRequest = 90,
                            FontSize = 38,
                            StrokeWidth = 4,
                            StrokeColor = Colors.White
                        }
                        .Assign(out _takePictureButton)
                        .OnTapped(async me =>
                        {
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
                        .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                        {
                            me.Text = CameraControl.CaptureMode == CaptureModeType.Still ? "📷" : "⏺️";
                        })
                        .ObserveProperty(CameraControl, nameof(CameraControl.IsRecording), me =>
                        {
                            if (CameraControl.CaptureMode == CaptureModeType.Video)
                            {
                                me.Text = CameraControl.IsRecording ? "⏹️" : "⏺️";
                                me.BackgroundColor = CameraControl.IsRecording 
                                    ? Color.FromArgb("#DDDC2626") 
                                    : Color.FromArgb("#DD9333EA");
                            }
                        }),

                        // Flash button
                        new SkiaButton("💡")
                        {
                            BackgroundColor = Color.FromArgb("#BBF59E0B"),
                            TextColor = Colors.White,
                            CornerRadius = 32,
                            UseCache = SkiaCacheType.Image,
                            WidthRequest = 64,
                            HeightRequest = 64,
                            FontSize = 28
                        }
                        .Assign(out _flashButton)
                        .OnTapped(me => { ToggleFlash(); })
                        .ObserveProperty(CameraControl, nameof(CameraControl.FlashMode), me =>
                        {
                            me.BackgroundColor = CameraControl.FlashMode != FlashMode.Off
                                ? Color.FromArgb("#BBFBBF24")
                                : Color.FromArgb("#BB78716C");
                        }),

                        // Camera selection button  
                        new SkiaButton("🔄")
                        {
                            BackgroundColor = Color.FromArgb("#BB0D9488"),
                            TextColor = Colors.White,
                            CornerRadius = 32,
                            UseCache = SkiaCacheType.Image,
                            WidthRequest = 64,
                            HeightRequest = 64,
                            FontSize = 28
                        }
                        .Assign(out _cameraSelectButton)
                        .OnTapped(async me => { await SelectCamera(); }),
                    }
                },

                // Enhanced Status Display (top-center compact)
                new SkiaShape()
                {
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

                // Settings Drawer (slides up from bottom)
                new SkiaDrawer()
                {
                    Margin = new Thickness(20, 0, 20, 0),
                    HeaderSize = 40,
                    Direction = DrawerDirection.FromBottom,
                    VerticalOptions = LayoutOptions.End,
                    HorizontalOptions = LayoutOptions.Fill,
                    MaximumHeightRequest = 500,
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
                        BackgroundColor = Color.FromArgb("#F5F5F5"),
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
                                    new SkiaScroll()
                                    {
                                        BackgroundColor = Colors.Transparent,
                                        Margin = new Thickness(0, 40, 0, 0),
                                        Orientation = ScrollOrientation.Vertical,
                                        HorizontalOptions = LayoutOptions.Fill,
                                        VerticalOptions = LayoutOptions.Fill,
                                        Content = CreateDrawerContent()
                                    },
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
            _previewOverlay = CreatePreviewOverlay();

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

                CameraControl.FrameProcessor = (frame) =>
                {
                    CameraControl.DrawOverlay(frame);
                };

                CameraControl.PreviewProcessor = (frame) =>
                {
                    //if (CameraControl.IsRecording || CameraControl.IsPreRecording)
                    {
                        CameraControl.DrawOverlay(frame);
                    }
                };

                // Setup camera event handlers
                SetupCameraEvents();
            }

        }

        private SkiaShape CreateDrawerHeader()
        {
            return new SkiaShape()
            {
                UseCache = SkiaCacheType.Image,
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
            return new SkiaStack()
            {
                Spacing = 16,
                UseCache = SkiaCacheType.Operations,
                Padding = new Thickness(16),
                Children =
                {
                    // Section header
                    new SkiaLabel("Settings")
                    {
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.Black,
                        Margin = new(0, 0, 0, 8),
                        UseCache = SkiaCacheType.Operations
                    },

                    // Photo/Video Settings
                    new SkiaLabel("📸 Capture Settings")
                    {
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#6B7280"),
                        Margin = new(0, 8, 0, 4)
                    },
                    new SkiaWrap
                    {
                        Spacing = 8,
                        Children =
                        {
                            new SkiaButton("📸 Photo Mode")
                            {
                                BackgroundColor = Color.FromArgb("#0891B2"),
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image,
                                Padding = new Thickness(16, 10)
                            }
                            .OnTapped(me => { ToggleCaptureMode(); })
                            .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                            {
                                me.Text = CameraControl.CaptureMode == CaptureModeType.Still
                                    ? "📸 Photo Mode"
                                    : "🎥 Video Mode";
                                me.BackgroundColor = CameraControl.CaptureMode == CaptureModeType.Still
                                    ? Color.FromArgb("#0891B2")
                                    : Color.FromArgb("#7C3AED");
                            }),

                            new SkiaButton("🗂️ Photo Formats")
                            {
                                BackgroundColor = Color.FromArgb("#4F46E5"),
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image,
                                Padding = new Thickness(16, 10)
                            }
                            .OnTapped(async me => { await ShowPhotoFormatPicker(); })
                            .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                            {
                                me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Still;
                            }),

                            new SkiaButton("🗂️ Video Formats")
                            {
                                BackgroundColor = Color.FromArgb("#4F46E5"),
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image,
                                Padding = new Thickness(16, 10)
                            }
                            .OnTapped(async me => { await ShowVideoFormatPicker(); })
                            .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                            {
                                me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                            }),

                            new SkiaButton("❌ Abort Recording")
                            {
                                BackgroundColor = Color.FromArgb("#991B1B"),
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image,
                                Padding = new Thickness(16, 10),
                                IsVisible = false
                            }
                            .Assign(out _videoRecordButton)
                            .OnTapped(async me => { await AbortVideoRecording(); })
                            .ObserveProperty(CameraControl, nameof(CameraControl.IsRecording), me =>
                            {
                                me.IsVisible = CameraControl.IsRecording && CameraControl.CaptureMode == CaptureModeType.Video;
                            })
                            .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                            {
                                me.IsVisible = CameraControl.IsRecording && CameraControl.CaptureMode == CaptureModeType.Video;
                            }),
                        }
                    },

                    // Audio Settings (Video mode only)
                    new SkiaLabel("🎤 Audio Settings")
                    {
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#6B7280"),
                        Margin = new(0, 12, 0, 4)
                    }
                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                    {
                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                    }),
                    new SkiaWrap
                    {
                        Spacing = 8,
                        Children =
                        {
                            new SkiaButton("🔇 Audio: OFF")
                            {
                                BackgroundColor = Color.FromArgb("#6B7280"),
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image,
                                Padding = new Thickness(16, 10)
                            }
                            .OnTapped(me =>
                            {
                                CameraControl.EnableAudioRecording = !CameraControl.EnableAudioRecording;
                            })
                            .ObserveProperty(CameraControl, nameof(CameraControl.EnableAudioRecording), me =>
                            {
                                me.Text = CameraControl.EnableAudioRecording ? "🔊 Audio: ON" : "🔇 Audio: OFF";
                                me.BackgroundColor = CameraControl.EnableAudioRecording ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
                            }),

                            new SkiaButton("🎤 Audio Device")
                            {
                                BackgroundColor = Color.FromArgb("#B45309"),
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image,
                                Padding = new Thickness(16, 10)
                            }
                            .Assign(out _audioSelectButton)
                            .OnTapped(async me => { await SelectAudioSource(); }),

                            new SkiaButton("🎵 Audio Codec")
                            {
                                BackgroundColor = Color.FromArgb("#475569"),
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image,
                                Padding = new Thickness(16, 10)
                            }
                            .Assign(out _audioCodecButton)
                            .OnTapped(async me => { await SelectAudioCodec(); }),
                        }
                    }
                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                    {
                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                    }),

                    // Processing Settings (Video mode only)
                    new SkiaLabel("⚡ Processing")
                    {
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#6B7280"),
                        Margin = new(0, 12, 0, 4)
                    }
                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                    {
                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                    }),
                    new SkiaWrap
                    {
                        Spacing = 8,
                        Children =
                        {
                            new SkiaButton("⚙️ Processing: ON")
                            {
                                BackgroundColor = Color.FromArgb("#10B981"),
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image,
                                Padding = new Thickness(16, 10)
                            }
                            .OnTapped(me =>
                            {
                                CameraControl.UseRealtimeVideoProcessing = !CameraControl.UseRealtimeVideoProcessing;
                            })
                            .ObserveProperty(() => CameraControl, nameof(CameraControl.UseRealtimeVideoProcessing), me =>
                            {
                                me.Text = CameraControl.UseRealtimeVideoProcessing ? "⚙️ Processing: ON" : "⚙️ Processing: OFF";
                                me.BackgroundColor = CameraControl.UseRealtimeVideoProcessing ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
                            }),

                            new SkiaButton("📊 Visualizer")
                            {
                                BackgroundColor = Color.FromArgb("#65A30D"),
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image,
                                Padding = new Thickness(16, 10)
                            }
                            .OnTapped(me =>
                            {
                                CameraControl.SwitchVisualizer();
                            })
                            .ObserveProperty(CameraControl, nameof(CameraControl.VisualizerName), me =>
                            {
                                me.Text = $"📊 {CameraControl.VisualizerName}";
                            }),

                            new SkiaButton("🎙️ Speech: OFF")
                            {
                                BackgroundColor = Color.FromArgb("#475569"),
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image,
                                Padding = new Thickness(16, 10)
                            }
                            .Assign(out _speechButton)
                            .OnTapped(me =>
                            {
                                ToggleSpeech();
                            }),
                        }
                    }
                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                    {
                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                    }),

                    // Prerecording Settings (Video mode only)
                    new SkiaLabel("⏱️ Prerecording")
                    {
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#6B7280"),
                        Margin = new(0, 12, 0, 4)
                    }
                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                    {
                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                    }),
                    new SkiaWrap
                    {
                        Spacing = 8,
                        Children =
                        {
                            new SkiaButton("⏱️ Pre-Record: OFF")
                            {
                                BackgroundColor = Color.FromArgb("#6B7280"),
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image,
                                Padding = new Thickness(16, 10)
                            }
                            .Assign(out _preRecordingToggleButton)
                            .OnTapped(me => { TogglePreRecording(); }),

                            new SkiaButton($"⏰ {CameraControl.PreRecordDuration.TotalSeconds:F0}s")
                            {
                                BackgroundColor = Color.FromArgb("#475569"),
                                TextColor = Colors.White,
                                CornerRadius = 8,
                                UseCache = SkiaCacheType.Image,
                                Padding = new Thickness(16, 10)
                            }
                            .Assign(out _preRecordingDurationButton)
                            .OnTapped(async me => { await ShowPreRecordingDurationPicker(); }),
                        }
                    }
                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                    {
                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                    }),
                }
            };
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


        void ShowAlert(string title, string message)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert(title, message, "OK");
            });
        }
    }
}
