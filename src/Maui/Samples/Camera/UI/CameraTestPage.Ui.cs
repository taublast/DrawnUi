using AppoMobi.Specials;
using CameraTests.Services;
using DrawnUi.Camera;
using DrawnUi.Views;

namespace CameraTests.Views
{
    public partial class CameraTestPage
    {
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
                mainStack = new SkiaGrid
                {
                    RowSpacing = 16,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
            {

                //ROW 0

                // Camera preview
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

                new SkiaRichLabel()
                {
                    Margin = new(24, 0, 24, 24),
                    HorizontalTextAlignment = DrawTextAlignment.Center,
                    VerticalOptions = LayoutOptions.End,
                    HorizontalOptions = LayoutOptions.Center,
                }
                .Assign(out _captionsLabel),

                //ROW 1

                new SkiaStack()
                {
                    UseCache = SkiaCacheType.Operations,
                    Spacing = 16,
                    Children =
                    {
                        // Status label
                        new SkiaLabel("Camera Status: Off")
                            {
                                FontSize = 14,
                                TextColor = Colors.Gray,
                                HorizontalOptions = LayoutOptions.Center,
                                UseCache = SkiaCacheType.Operations
                            }
                            .Assign(out _statusLabel),

                        // All controls in a single wrap layout
                        new SkiaWrap
                        {
                            Spacing = 8,
                            Margin = new Thickness(0, 0, 0, 50),
                            HorizontalOptions = LayoutOptions.Center,
                            Children =
                            {
                                // Power toggle button
                                new SkiaButton("⚡ Power: OFF")
                                    {
                                        BackgroundColor = Colors.Red,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image,
                                        Padding = new Thickness(20, 8)
                                    }
                                    .OnTapped(me => { CameraControl.IsOn = !CameraControl.IsOn; })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.State), me =>
                                    {
                                        me.Text = CameraControl.State == CameraState.On ? "⚡ Power: ON" : "⚡ Power: OFF";
                                        me.BackgroundColor = CameraControl.State == CameraState.On ? Colors.Green : Colors.Red;
                                    }),

                                // Mode switch button
                                new SkiaButton("📸 Photo Mode")
                                    {
                                        BackgroundColor = Colors.DarkCyan,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image,
                                        Padding = new Thickness(20, 8)
                                    }
                                    .Assign(out _modeSwitchButton)
                                    .OnTapped(me => { ToggleCaptureMode(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.Text = CameraControl.CaptureMode == CaptureModeType.Still
                                            ? "📸 Photo Mode"
                                            : "🎥 Video Mode";
                                        me.BackgroundColor = CameraControl.CaptureMode == CaptureModeType.Still
                                            ? Colors.DarkCyan
                                            : Colors.DarkViolet;
                                    }),

                                // Flash control button
                                new SkiaButton("Flash: Off")
                                    {
                                        BackgroundColor = Colors.Orange,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _flashButton)
                                    .OnTapped(me => { ToggleFlash(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.FlashMode),
                                        me => { me.Text = $"Flash: {CameraControl.FlashMode}"; }),

                                // Camera selection button
                                new SkiaButton("📷 Camera")
                                    {
                                        BackgroundColor = Colors.Teal,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _cameraSelectButton)
                                    .OnTapped(async me => { await SelectCamera(); }),

                                // Audio selection button
                                new SkiaButton("🎤 Audio: Default")
                                    {
                                        BackgroundColor = Colors.DarkGoldenrod,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _audioSelectButton)
                                    .OnTapped(async me => { await SelectAudioSource(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Audio Codec selection button
                                new SkiaButton("🎵 Codec: Default")
                                    {
                                        BackgroundColor = Colors.DarkSlateGray,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _audioCodecButton)
                                    .OnTapped(async me => { await SelectAudioCodec(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Audio Visualizer switch
                                new SkiaButton("Vis: VU Meter")
                                    {
                                        BackgroundColor = Colors.DarkOliveGreen,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .OnTapped(me =>
                                    {
                                        CameraControl.SwitchVisualizer();
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.VisualizerName), me =>
                                    {
                                        me.Text = $"{CameraControl.VisualizerName}";
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        CameraControl.SwitchVisualizer(0);
                                        //me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Take Picture button (only visible in Still mode)
                                new SkiaButton("Take Picture")
                                    {
                                        BackgroundColor = Colors.Blue,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _takePictureButton)
                                    .OnTapped(async me => { await TakePictureAsync(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.State), me =>
                                    {
                                        me.IsEnabled = CameraControl.State == CameraState.On;
                                        me.Opacity = me.IsEnabled ? 1.0 : 0.5;
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Still;
                                    }),

                                // Photo Formats button (only visible in Still mode)
                                new SkiaButton("📷 Formats")
                                    {
                                        BackgroundColor = Colors.DarkSlateBlue,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .OnTapped(async me => { await ShowPhotoFormatPicker(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Still;
                                    }),

                                // Record button (only visible in Video mode)
                                new SkiaButton("🎥 Record")
                                    {
                                        BackgroundColor = Colors.Purple,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _videoRecordButton)
                                    .OnTapped(async me => { ToggleVideoRecording(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.IsRecordingVideo), me =>
                                    {
                                        if (CameraControl.IsRecordingVideo)
                                        {
                                            me.Text = "🛑 Stop";
                                            me.BackgroundColor = Colors.Red;
                                        }
                                        else if (CameraControl.IsPreRecording)
                                        {
                                            me.Text = "⏺️ Pre-Record";
                                            me.BackgroundColor = Colors.Orange;
                                        }
                                        else
                                        {
                                            me.Text = "🎥 Record";
                                            me.BackgroundColor = Colors.Purple;
                                        }
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.IsPreRecording), me =>
                                    {
                                        if (CameraControl.IsRecordingVideo)
                                        {
                                            me.Text = "🛑";
                                            me.BackgroundColor = Colors.Red;
                                        }
                                        else if (CameraControl.IsPreRecording)
                                        {
                                            me.Text = "⏺️ Pre-Record";
                                            me.BackgroundColor = Colors.Orange;
                                        }
                                        else
                                        {
                                            me.Text = "🎥 Record";
                                            me.BackgroundColor = Colors.Purple;
                                        }
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Abort Recording button (only visible when recording)
                                new SkiaButton("❌ Abort")
                                    {
                                        BackgroundColor = Colors.DarkRed,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image,
                                        IsVisible = false
                                    }
                                    .OnTapped(async me => { await AbortVideoRecording(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.IsRecordingVideo), me =>
                                    {
                                        me.IsVisible = CameraControl.IsRecordingVideo && CameraControl.CaptureMode == CaptureModeType.Video;
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.IsRecordingVideo && CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Video Formats button (only visible in Video mode)
                                new SkiaButton("📹 Formats")
                                    {
                                        BackgroundColor = Colors.DarkSlateBlue,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .OnTapped(async me => { await ShowVideoFormatPicker(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Pre-Record toggle button (only visible in Video mode)
                                new SkiaButton("Pre-Record: OFF")
                                    {
                                        BackgroundColor = Colors.DarkGray,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _preRecordingToggleButton)
                                    .OnTapped(me => { TogglePreRecording(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Pre-Record Duration button (only visible in Video mode)
                                new SkiaButton($"Duration: {CameraControl.PreRecordDuration.TotalSeconds:F0}s")
                                    {
                                        BackgroundColor = Colors.DarkSlateGray,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _preRecordingDurationButton)
                                    .OnTapped(async me => { await ShowPreRecordingDurationPicker(); })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Capture Flow toggle button (only visible in Video mode)
                                new SkiaButton("Processing: ON")
                                    {
                                        BackgroundColor = Colors.Green,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .OnTapped(me =>
                                    {
                                        CameraControl.UseRealtimeVideoProcessing = !CameraControl.UseRealtimeVideoProcessing;

                                    })
                                    .ObserveProperty(()=>CameraControl, nameof(CameraControl.UseRealtimeVideoProcessing), me =>
                                    {
                                        me.Text = CameraControl.UseRealtimeVideoProcessing ? "Processing: ON" : "Processing: OFF";
                                        me.BackgroundColor = CameraControl.UseRealtimeVideoProcessing ? Colors.Green : Colors.DarkGray;
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),

                                // Audio toggle button (only visible in Video mode)
                                new SkiaButton("Audio: OFF")
                                    {
                                        BackgroundColor = Colors.DarkGray,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .OnTapped(me =>
                                    {
                                        CameraControl.RecordAudio = !CameraControl.RecordAudio;
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                    {
                                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                                    })
                                    .ObserveProperty(CameraControl, nameof(CameraControl.RecordAudio), me =>
                                    {
                                        me.Text = CameraControl.RecordAudio ? "Audio: ON" : "Audio: OFF";
                                        me.BackgroundColor = CameraControl.RecordAudio ? Colors.Green : Colors.DarkGray;
                                    }),

                                // Speech Recognition toggle
                                new SkiaButton("Speech: OFF")
                                    {
                                        BackgroundColor = Colors.DarkSlateGray,
                                        TextColor = Colors.White,
                                        CornerRadius = 8,
                                        UseCache = SkiaCacheType.Image
                                    }
                                    .Assign(out _speechButton)
                                    .OnTapped(me =>
                                    {
                                        ToggleSpeech();
                                    })
                            }
                        },

                    }
                }.WithRow(1),

                // Pre-Recording status label (only visible in Video mode)
                new SkiaLabel("Pre-Recording: Disabled")
                    {
                        FontSize = 12,
                        BackgroundColor = Color.Parse("#33000000"),
                        Padding = 4,
                        TextColor = Colors.Orange,
                        VerticalOptions = LayoutOptions.End,
                        HorizontalOptions = LayoutOptions.Center,
                        UseCache = SkiaCacheType.Operations
                    }
                    .Assign(out _preRecordingStatusLabel)
                    .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                    {
                        me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video;
                    }).WithRow(0),
            }
                }.WithRowDefinitions("*, Auto");
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
                CameraControl.RecordAudio = true;

                CameraControl.FrameProcessor = (frame) =>
                {
                    CameraControl.DrawOverlay(frame);
                };

                CameraControl.PreviewProcessor = (frame) =>
                {
                    //if (CameraControl.IsRecordingVideo || CameraControl.IsPreRecording)
                    {
                        CameraControl.DrawOverlay(frame);
                    }
                };

                // Setup camera event handlers
                SetupCameraEvents();
            }

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
