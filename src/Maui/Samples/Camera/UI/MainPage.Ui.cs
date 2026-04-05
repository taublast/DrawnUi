using System.Text;
using AppoMobi.Specials;
using CameraTests.Services;
using CameraTests.UI;
using DrawnUi.Camera;
using DrawnUi.Controls;
using DrawnUi.Draw;
using DrawnUi.Infrastructure;
using DrawnUi.Views;
using SolTempo.UI;

namespace CameraTests.Views
{
    public partial class MainPage
    {
        private SkiaShape _captureButtonOuter;


        private SkiaShape[] _tabPills;
        private static Color ColorPanel = Color.FromArgb("#F3101825"); //AA101825
        private static Color ColorDrawer = Color.FromArgb("#66D9F6FF"); //AA101825

        private Canvas CreateCanvas()
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


            // Create preview overlay (initially hidden)
            _previewOverlay = CreateTakenPhotoPreviewPopup();

            var canvas = new Canvas
            {
                RenderingMode = RenderingModeType.Accelerated,
                Gestures = GesturesMode.Lock,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.Black,
                Content =

                    //wrapper
                    new SkiaLayer
                    {
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

                            new SkiaLayer()
                            {
                                UseCache = SkiaCacheType.ImageCompositeGPU,
                                VerticalOptions = LayoutOptions.Fill,
                                Children =
                                {
#if WINDOWS || MACCATALYST
                            CreateStageEdgeOverlay(true),
                            CreateStageEdgeOverlay(false),
#endif
                                    CreateHeaderPanel()
                                        .Assign(out _headerPanel),
                                    CreateCameraControlsPanel()
                                        .Assign(out _cameraControlsPanel),
                                    CreateRecordingStopButton(),

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
                                                CornerRadius = new CornerRadius(26, 26, 0, 0),
                                                HorizontalOptions = LayoutOptions.Fill,
                                                VerticalOptions = LayoutOptions.Fill,
                                                BackgroundColor = ColorPanel, // Color.FromArgb("#FE0A101A"),
                                                StrokeWidth = 1,
                                                StrokeColor = Color.FromArgb("#3311C5BF"),
                                                Children =
                                                {
                                                    new SkiaLayout()
                                                    {
                                                        HorizontalOptions = LayoutOptions.Fill,
                                                        VerticalOptions = LayoutOptions.Fill,
                                                        Children =
                                                        {
                                                            CreateDrawerHeader(), CreateDrawerContent()
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        .Assign(out _settingsDrawer),
                                    _previewOverlay,
                                }
                            },

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
                                UseCache = SkiaCacheType.GPU
                            }
#endif
                        }
                    }
            };

            canvas.WillFirstTimeDraw += (sender, context) =>
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

            return canvas;
        }

        private SkiaShape CreateStageEdgeOverlay(bool top)
        {
            return new SkiaShape()
            {
                Type = ShapeType.Rectangle,
                InputTransparent = true,
                UseCache = SkiaCacheType.Image,
                HeightRequest = top ? 220 : 260,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = top ? LayoutOptions.Start : LayoutOptions.End,
                FillGradient = new SkiaGradient()
                {
                    StartXRatio = 0,
                    EndXRatio = 0,
                    StartYRatio = top ? 0 : 1,
                    EndYRatio = top ? 1 : 0,
                    Colors = new Color[]
                    {
                        top ? Color.FromArgb("#EA06131A") : Color.FromArgb("#F20B1220"),
                        Color.FromArgb("#0006131A")
                    }
                }
            };
        }

        private SkiaLayer CreateHeaderPanel()
        {
            SkiaLottie aiAnimation = null;
            var failureEffect = new SaturationEffect() { Value = 1f };
            SkiaLabel modeButtonLabel = null;
            SkiaLabel speechButtonLabel = null;
            float animSize = 50;

            return new SkiaLayer()
            {
                Children =
                {
                    new SkiaShape()
                    {
                        UseCache = SkiaCacheType.Image,
                        Type = ShapeType.Rectangle,
                        CornerRadius = 28,
                        Margin = new Thickness(18, 18, 18, 0),
                        Padding = new Thickness(18, 16, 18, 16),
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Start,
                        BackgroundColor = ColorPanel,
                        StrokeWidth = 1,
                        StrokeColor = Color.FromArgb("#3311C5BF"),
                        Children =
                        {
                            new SkiaLabel("SKIACAMERA")
                            {
                                Margin = new(animSize, 0, 0, 0),
                                FontSize = 12,
                                CharacterSpacing = 2,
                                TextColor = Color.FromArgb("#7DEAE5"),
                                UseCache = SkiaCacheType.Operations,
                                HorizontalOptions = LayoutOptions.Start,
                                VerticalOptions = LayoutOptions.Start,
                            },

                            //Camera Status
                            new SkiaLabel("Off")
                                {
                                    Margin = new Thickness(animSize, 22, 0, 0),
                                    FontSize = 13,
                                    TextColor = Color.FromArgb("#A7B5C6"),
                                    UseCache = SkiaCacheType.Operations,
                                    HorizontalOptions = LayoutOptions.Start,
                                    VerticalOptions = LayoutOptions.Start,
                                }
                                .Assign(out _statusLabel),

                            //AI hint/status
                            new SkiaLabel()
                                {
                                    Margin = new(150, 0, 0, 0),
                                    FontFamily = "FontText",
                                    FontSize = 13,
                                    Opacity = 0.33,
                                    UseCache = SkiaCacheType.Operations,
                                    TextColor = Color.FromArgb("#A7B5C6"),
                                    HorizontalOptions = LayoutOptions.End,
                                    VerticalOptions = LayoutOptions.Start,
                                }
                                .Assign(out _captionHintLabel)
                                .ObserveProperties(this,
                                    new[]
                                    {
                                        nameof(IsSpeechEnabled), nameof(IsAudioMonitoringEnabled),
                                        nameof(IsTranscribing), nameof(TranscriptionState),
                                        //nameof(TranscriptionStatusMessage), nameof(HasVisibleCaptions)
                                    }, me =>
                                    {
                                        //me.IsVisible = !HasVisibleCaptions;
                                        me.Text = !IsSpeechEnabled
                                            ? "Turn on speech recognition with AI"
                                            : TranscriptionState == RealtimeTranscriptionSessionState.Connecting
                                                ? "Connecting..."
                                                : IsTranscriptionFailed
                                                    ? (string.IsNullOrWhiteSpace(TranscriptionStatusMessage)
                                                        ? "Network error. Tap SPEECH to retry."
                                                        : TranscriptionStatusMessage)
                                                    : IsTranscribing
                                                        ? "Listening.."
                                                        : "Speak to generate live captions with AI";
                                        me.TextColor = IsTranscriptionFailed
                                            ? Color.FromArgb("#FDBA74")
                                            : IsTranscribing
                                                ? Color.FromArgb("#D9F6FF")
                                                : Color.FromArgb("#A7B5C6");
                                    }),
                            new SkiaLayout()
                            {
                                Type = LayoutType.Wrap,
                                Spacing = 8,
                                Margin = new Thickness(0, 50, 0, 0),
                                HorizontalOptions = LayoutOptions.Start,
                                VerticalOptions = LayoutOptions.Start,
                                Children =
                                {
                                    new SkiaShape()
                                        {
                                            Type = ShapeType.Rectangle,
                                            CornerRadius = 16,
                                            BackgroundColor = Color.FromArgb("#2D0891B2"),
                                            StrokeColor = Color.FromArgb("#660891B2"),
                                            StrokeWidth = 1,
                                            Padding = new Thickness(12, 8),
                                            Children =
                                            {
                                                new SkiaLabel("PHOTO MODE")
                                                    {
                                                        LineBreakMode = LineBreakMode.NoWrap,
                                                        FontSize = 12,
                                                        FontAttributes = FontAttributes.Bold,
                                                        TextColor = Colors.White,
                                                        UseCache = SkiaCacheType.Operations,
                                                    }
                                                    .Assign(out modeButtonLabel)
                                            }
                                        }
                                        .OnTapped(me => ToggleCaptureMode())
                                        .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode), me =>
                                        {
                                            var isVideo = CameraControl.CaptureMode == CaptureModeType.Video;
                                            me.BackgroundColor =
                                                isVideo ? Color.FromArgb("#2D7C3AED") : Color.FromArgb("#2D0891B2");
                                            me.StrokeColor =
                                                isVideo ? Color.FromArgb("#667C3AED") : Color.FromArgb("#660891B2");
                                            modeButtonLabel.Text = isVideo ? "VIDEO MODE" : "PHOTO MODE";
                                        })
                                        .ObserveProperty(CameraControl, nameof(CameraControl.IsRecording),
                                            me => { me.IsVisible = !CameraControl.IsRecording; }),
                                    new SkiaShape()
                                        {
                                            Type = ShapeType.Rectangle,
                                            CornerRadius = 16,
                                            BackgroundColor = Color.FromArgb("#260F172A"),
                                            StrokeColor = Color.FromArgb("#33233445"),
                                            StrokeWidth = 1,
                                            Padding = new Thickness(12, 8),
                                            Children =
                                            {
                                                new SkiaLabel("SPEECH OFF")
                                                    {
                                                        LineBreakMode = LineBreakMode.NoWrap,
                                                        FontSize = 12,
                                                        FontAttributes = FontAttributes.Bold,
                                                        TextColor = Color.FromArgb("#8FA3B7"),
                                                        UseCache = SkiaCacheType.Operations,
                                                    }
                                                    .Assign(out speechButtonLabel)
                                            }
                                        }
                                        .OnTapped(me => ToggleSpeech())
                                        .ObserveProperties(this,
                                            new[] { nameof(IsSpeechEnabled), nameof(IsTranscriptionFailed) }, me =>
                                            {
                                                me.BackgroundColor = !IsSpeechEnabled
                                                    ? Color.FromArgb("#260F172A")
                                                    : IsTranscriptionFailed
                                                        ? Color.FromArgb("#2DF97316")
                                                        : Color.FromArgb("#2D10B981");
                                                me.StrokeColor = !IsSpeechEnabled
                                                    ? Color.FromArgb("#33233445")
                                                    : IsTranscriptionFailed
                                                        ? Color.FromArgb("#66F97316")
                                                        : Color.FromArgb("#6610B981");
                                                speechButtonLabel.Text = !IsSpeechEnabled ? "SPEECH OFF" :
                                                    IsTranscriptionFailed ? "RETRY" : "SPEECH ON";
                                                speechButtonLabel.TextColor =
                                                    IsSpeechEnabled ? Colors.White : Color.FromArgb("#8FA3B7");
                                            })
                                }
                            }
                        }
                    },
                    new SkiaLottie()
                        {
                            Margin = new Thickness(30, 30, 0, 0),
                            Source = @"Lottie\ai.json",
                            AutoPlay = false,
                            Repeat = -1,
                            StopAtCurrentFrame = true,
                            SpeedRatio = 1.75,
                            WidthRequest = animSize,
                            LockRatio = 1,
                            VisualEffects = new List<SkiaEffect>() { failureEffect }
                        }.Assign(out aiAnimation)
                        .ObserveProperties(this,
                            new[] { nameof(IsSpeechEnabled), nameof(IsTranscribing), nameof(IsTranscriptionFailed) },
                            me =>
                            {
                                var opacity = 1f;
                                if (IsTranscribing)
                                {
                                    if (!me.IsPlaying)
                                    {
                                        me.Start();
                                        opacity = 1;
                                    }
                                }
                                else
                                {
                                    if (me.IsPlaying)
                                    {
                                        me.Stop();
                                    }

                                    opacity = IsTranscriptionFailed ? 0.45f : IsSpeechEnabled ? 0.66f : 0.33f;
                                }

                                failureEffect.Value = IsTranscriptionFailed ? 0.2f : 1f;
                                me.Opacity = opacity;
                            }),
                }
            };
        }

        /// <summary>
        /// Button REC and other quick camera access
        /// </summary>
        /// 
        /// <returns></returns>
        private SkiaShape CreateCameraControlsPanel()
        {
            return new SkiaShape()
            {
                UseCache = SkiaCacheType.Image,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 0, 50),
                Padding = new Thickness(10, 8),
                HeightRequest = 86,
                StrokeColor = Color.FromArgb("#3342D9F6"),
                StrokeWidth = 1,
                BackgroundColor = ColorPanel,
                CornerRadius = 44,
                Children =
                {
                    new SkiaRow()
                    {
                        Padding = new Thickness(2),
                        Spacing = 10,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        Children =
                        {
                            //thumbnail preview (opens last taken photo/video or current recording if video mode)
                            new SkiaShape()
                                {
                                    VerticalOptions = LayoutOptions.Center,
                                    StrokeColor = Color.FromArgb("#3342D9F6"),
                                    StrokeWidth = 1,
                                    Type = ShapeType.Circle,
                                    HeightRequest = 52,
                                    LockRatio = 1,
                                    BackgroundColor = Color.FromArgb("#B30B1220"),
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
                                .OnTapped(me => OnThumbnailTapped()),

                            // Settings button
                            new SkiaShape()
                                {
                                    VerticalOptions = LayoutOptions.Center,
                                    StrokeColor = Color.FromArgb("#3342D9F6"),
                                    StrokeWidth = 1,
                                    UseCache = SkiaCacheType.Operations,
                                    Type = ShapeType.Circle,
                                    HeightRequest = 52,
                                    LockRatio = 1,
                                    BackgroundColor = Color.FromArgb("#B30B1220"),
                                    Children =
                                    {
                                        new SkiaSvg()
                                            {
                                                Source = "Svg/icon_settings.svg",
                                                TintColor = Colors.White.WithAlpha(0.9f),
                                                HeightRequest = 24,
                                                WidthRequest = 24,
                                                VerticalOptions = LayoutOptions.Center,
                                                HorizontalOptions = LayoutOptions.Center,
                                            }
                                            .Assign(out _settingsButtonIcon)
                                    }
                                }
                                .OnTapped(me => { ToggleSettingsDrawer(); }),

                            // Flash button
                            new SkiaShape()
                                {
                                    VerticalOptions = LayoutOptions.Center,
                                    StrokeColor = Color.FromArgb("#3342D9F6"),
                                    StrokeWidth = 1,
                                    UseCache = SkiaCacheType.Operations,
                                    Type = ShapeType.Circle,
                                    HeightRequest = 52,
                                    LockRatio = 1,
                                    BackgroundColor = Color.FromArgb("#B30B1220"),
                                    Children =
                                    {
                                        new SkiaSvg()
                                            {
                                                Source = "Svg/icon_flash_off.svg",
                                                TintColor = Colors.White.WithAlpha(0.85f),
                                                HeightRequest = 24,
                                                WidthRequest = 24,
                                                VerticalOptions = LayoutOptions.Center,
                                                HorizontalOptions = LayoutOptions.Center,
                                            }
                                            .Assign(out _iconButtonFlash)
                                    }
                                }
                                .Assign(out _buttonFlash)
                                .OnTapped(me => { ToggleFlash(); })
                                .ObserveProperty(CameraControl, nameof(CameraControl.FlashMode), me =>
                                {
                                    _iconButtonFlash.Source = CameraControl.FlashMode == FlashMode.Off
                                        ? "Svg/icon_flash_off.svg"
                                        : CameraControl.FlashMode == FlashMode.On
                                            ? "Svg/icon_flash_on.svg"
                                            : "Svg/icon_flash_auto.svg";
                                }),

                            // Capture button 
                            new SkiaShape()
                                {
                                    VerticalOptions = LayoutOptions.Center,
                                    UseCache = SkiaCacheType.Image,
                                    Type = ShapeType.Circle,
                                    HeightRequest = 64,
                                    LockRatio = 1,
                                    StrokeWidth = 4,
                                    StrokeColor = Color.FromArgb("#D9F6FF"),
                                    BackgroundColor = Colors.Transparent,
                                    Padding = new Thickness(4),
                                    Children =
                                    {
                                        new SkiaShape()
                                            {
                                                Type = ShapeType.Circle,
                                                BackgroundColor = Color.FromArgb("#E5F2F7"),
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
                                    await me.ScaleToAsync(1.08, 1.08, 90);
                                    await me.ScaleToAsync(1.0, 1.0, 90);

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
                                    me.IsEnabled = CameraControl.State == HardwareState.On;
                                    me.Opacity = me.IsEnabled ? 1.0 : 0.5;
                                })
                                .ObserveProperties(CameraControl,
                                    new[] { nameof(CameraControl.IsRecording), nameof(CameraControl.IsPreRecording) },
                                    me => { UpdateCaptureButtonShape(); }),

                            // Camera select button
                            new SkiaShape()
                                {
                                    VerticalOptions = LayoutOptions.Center,
                                    StrokeColor = Color.FromArgb("#3342D9F6"),
                                    StrokeWidth = 1,
                                    UseCache = SkiaCacheType.Image,
                                    Type = ShapeType.Circle,
                                    HeightRequest = 52,
                                    LockRatio = 1,
                                    BackgroundColor = Color.FromArgb("#B30B1220"),
                                    Children =
                                    {
                                        new SkiaSvg()
                                            {
                                                Source = "Svg/icon_camera_flip.svg",
                                                TintColor = Colors.White.WithAlpha(0.85f),
                                                HeightRequest = 24,
                                                WidthRequest = 24,
                                                VerticalOptions = LayoutOptions.Center,
                                                HorizontalOptions = LayoutOptions.Center,
                                            }
                                            .Assign(out _cameraSelectButtonIcon)
                                    }
                                }
                                .Assign(out _buttonSelectCamera)
                                .OnTapped(async me => { await SelectCamera(); })
                        }
                    }
                }
            };
        }

        private SkiaShape CreateRecordingStopButton()
        {
            return new SkiaShape()
                {
                    UseCache = SkiaCacheType.Image,
                    IsVisible = false,
                    ZIndex = 70,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.End,
                    Margin = new Thickness(0, 0, 0, 56),
                    Padding = new Thickness(24, 14),
                    StrokeColor = Color.FromArgb("#66FF7A7A"),
                    StrokeWidth = 1,
                    BackgroundColor = Color.FromArgb("#E5162230"),
                    CornerRadius = 26,
                    Children =
                    {
                        new SkiaLayout()
                        {
                            Type = LayoutType.Row,
                            Spacing = 10,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center,
                            Children =
                            {
                                new SkiaShape()
                                {
                                    Type = ShapeType.Rectangle,
                                    WidthRequest = 14,
                                    HeightRequest = 14,
                                    CornerRadius = 3,
                                    BackgroundColor = Color.FromArgb("#FF4D4F"),
                                    VerticalOptions = LayoutOptions.Center,
                                },
                                new SkiaLabel("Stop")
                                    {
                                        FontSize = 16,
                                        FontAttributes = FontAttributes.Bold,
                                        TextColor = Colors.White,
                                        UseCache = SkiaCacheType.Operations,
                                        VerticalOptions = LayoutOptions.Center,
                                    }
                                    .Assign(out _recordingStopLabel)
                            }
                        }
                    }
                }
                .Assign(out _recordingStopButton)
                .OnTapped(async me =>
                {
                    await me.ScaleToAsync(1.04, 1.04, 90);
                    await me.ScaleToAsync(1.0, 1.0, 90);
                    //if (CameraControl.IsPreRecording)
                    //{
                    //    _ = AbortVideoRecording();
                    //}
                    //else
                    {
                        ToggleVideoRecording();
                    }
                })
                .ObserveProperty(CameraControl, nameof(CameraControl.State), me =>
                {
                    me.IsEnabled = CameraControl.State == HardwareState.On;
                    me.Opacity = me.IsEnabled ? 1.0 : 0.5;
                });
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
                                    BackgroundColor = Color.FromArgb("#66D9F6FF"),
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
            _tabPills = new SkiaShape[3];

            var tabBar = new SkiaShape()
            {
                UseCache = SkiaCacheType.Image,
                Type = ShapeType.Rectangle,
                CornerRadius = 22,
                BackgroundColor = Color.FromArgb("#101825"),
                StrokeColor = Color.FromArgb("#33233445"),
                StrokeWidth = 1,
                HorizontalOptions = LayoutOptions.Fill,
                Padding = new Thickness(6),
                HeightRequest = 54,
                Children =
                {
                    new SkiaLayout()
                    {
                        Type = LayoutType.Row,
                        Spacing = 6,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        Children =
                        {
                            CreateDrawerTab(0, "Input"),
                            CreateDrawerTab(1, "Processing"),
                            CreateDrawerTab(2, "Output"),
                        }
                    }
                }
            };

            var sectionInput =
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
                        Padding = new Thickness(16, 16, 16, 16),
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Start,
                        Children =
                        {
                            //CreateDrawerSectionTitle("Capture", "Core camera and input setup"),

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
                            new SettingsButton("📷", "Source") { TintColor = Color.FromArgb("#D97706"), }
                                .ObserveProperty(CameraControl, nameof(CameraControl.CameraIndex), async (me) =>
                                {
                                    try
                                    {
                                        var cameras = await CameraControl.GetAvailableCamerasAsync();
                                        if (cameras.Count > 0)
                                        {
                                            var index = CameraControl.CameraIndex;
                                            if (index < 0)
                                            {
                                                index = 0;
                                            }

                                            var selectedCamera = cameras.First(c => c.Index == index);
                                            me.Text = $"{selectedCamera.Name}";
                                        }
                                        else
                                        {
                                            me.Text = $"No cameras";
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Super.Log(e);
                                        me.Text = $"Error";
                                    }
                                })
                                .OnTapped(async me => { await SelectCamera(); }),
                            new SettingsButton("🎤", "Audio Device") { TintColor = Color.FromArgb("#B45309"), }
                                .ObserveProperty(CameraControl, nameof(CameraControl.AudioDeviceIndex), async (me) =>
                                {
                                    try
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
                                    }
                                    catch (Exception e)
                                    {
                                        Super.Log(e);
                                        me.Text = $"Error";
                                    }
                                })
                                .OnTapped(async me => { await SelectAudioSource(); }),
                            new SettingsButton("🔊", "Audio Mode")
                                {
                                    TintColor = Color.FromArgb("#B45309"),
                                }
                                .ObserveProperty(CameraControl, nameof(CameraControl.AudioMode),
                                    me => { me.Text = CameraControl.AudioMode.ToString(); })
                                .OnTapped(async me => { await SelectAudioMode(); }),

                            //CreateDrawerSectionTitle("Formats", "Choose how the feed is captured"),

                            new SettingsButton("🗂️", "Formats")
                                {
                                    TintColor = Color.FromArgb("#4F46E5"),
                                }
                                .OnTapped(async me =>
                                {
                                    await ShowPhotoFormatPicker();
                                })
                                .ObserveProperties(CameraControl,
                                    new[]
                                    {
                                        nameof(CameraControl.PhotoFormatIndex), nameof(CameraControl.CaptureMode),
                                        nameof(CameraControl.CameraIndex),
                                    }, async (me) =>
                                    {
                                        try
                                        {
                                            if (CameraControl.PhotoQuality == CaptureQuality.Manual)
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
                                            }
                                            else
                                            {
                                                me.Text = $"{CameraControl.PhotoQuality}";
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Super.Log(e);
                                            me.Text = $"Error";
                                        }
                                    })
                                .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode),
                                    me => { me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Still; }),

                            new SettingsButton("🗂️", "Formats") { TintColor = Color.FromArgb("#4F46E5"), }
                                .OnTapped(async me => { await ShowVideoFormatPicker(); })
                                .ObserveProperties(CameraControl,
                                    new[]
                                    {
                                        nameof(CameraControl.VideoFormatIndex), nameof(CameraControl.CaptureMode),
                                        nameof(CameraControl.CameraIndex),
                                    }, async (me) =>
                                    {
                                        try
                                        {
                                            if (CameraControl.VideoQuality == VideoQuality.Manual)
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
                                            }
                                            else
                                            {
                                                me.Text = $"{CameraControl.VideoQuality}";
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Super.Log(e);
                                            me.Text = $"Error";
                                        }
                                    })
                                .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode),
                                    me => { me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video; }),
                            new SettingsButton("❌", "Abort")
                                {
                                    TintColor = Color.FromArgb("#E11D48"), IsVisible = false
                                }
                                .Assign(out _videoRecordButton)
                                .OnTapped(async me => { await AbortVideoRecording(); })
                                .ObserveProperty(CameraControl, nameof(CameraControl.IsRecording),
                                    me =>
                                    {
                                        me.IsVisible = CameraControl.IsRecording &&
                                                       CameraControl.CaptureMode == CaptureModeType.Video;
                                    })
                                .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode),
                                    me =>
                                    {
                                        me.IsVisible = CameraControl.IsRecording &&
                                                       CameraControl.CaptureMode == CaptureModeType.Video;
                                    }),
                        }
                    }
                };

            var sectionProcessing =
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
                        Padding = new Thickness(16, 16, 16, 24),
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Start,
                        Children =
                        {
                            //CreateDrawerSectionTitle("Realtime Processing", "Live pipeline controls for the demo"),

                            new SettingsButton("⚙️", "Processing: ON") { TintColor = Color.FromArgb("#10B981"), }
                                .OnTapped(me =>
                                {
                                    CameraControl.UseRealtimeVideoProcessing =
                                        !CameraControl.UseRealtimeVideoProcessing;
                                })
                                .ObserveProperty(() => CameraControl, nameof(CameraControl.UseRealtimeVideoProcessing),
                                    me =>
                                    {
                                        me.Text = CameraControl.UseRealtimeVideoProcessing
                                            ? "Processing: ON"
                                            : "Processing: OFF";
                                        me.TintColor = CameraControl.UseRealtimeVideoProcessing
                                            ? Color.FromArgb("#10B981")
                                            : Color.FromArgb("#6B7280");
                                    }),
                            new SettingsButton("🎨", $"Effect: {ShaderEffectHelper.GetTitle(ShaderEffect.None)}")
                                {
                                    TintColor = Color.FromArgb("#6B7280"),
                                }
                                .OnTapped(me => { CycleEffect(); })
                                .ObserveProperty(CameraControl, nameof(CameraControl.UseRealtimeVideoProcessing),
                                    me => { me.IsVisible = CameraControl.UseRealtimeVideoProcessing; })
                                .ObserveProperty(CameraControl, nameof(CameraControl.VideoEffect), me =>
                                {
                                    var title = ShaderEffectHelper.GetTitle(CameraControl.VideoEffect);
                                    me.Text = $"Effect: {title}";
                                    me.TintColor = CameraControl.VideoEffect != ShaderEffect.None
                                        ? Color.FromArgb("#10B981")
                                        : Color.FromArgb("#6B7280");
                                }),
                            new SettingsButton("🎧", "Audio Monitor: OFF") { TintColor = Color.FromArgb("#6B7280"), }
                                .OnTapped(me => { IsAudioMonitoringEnabled = !IsAudioMonitoringEnabled; })
                                .ObserveProperty(this, nameof(IsAudioMonitoringEnabled), me =>
                                {
                                    me.Text = IsAudioMonitoringEnabled ? "Audio Monitor: ON" : "Audio Monitor: OFF";
                                    me.TintColor = IsAudioMonitoringEnabled
                                        ? Color.FromArgb("#10B981")
                                        : Color.FromArgb("#6B7280");
                                }),
                            new SettingsButton("📊", "Visualizer") { TintColor = Color.FromArgb("#65A30D"), }
                                .OnTapped(me => { CameraControl.SwitchVisualizer(); })
                                .ObserveProperty(() => CameraControl, nameof(CameraControl.VisualizerName),
                                    me => { me.Text = CameraControl.VisualizerName; }),
                            new SettingsButton("📈", "Gain: ON") { TintColor = Color.FromArgb("#10B981"), }
                                .OnTapped(me => { CameraControl.UseGain = !CameraControl.UseGain; })
                                .ObserveProperty(CameraControl, nameof(CameraControl.UseGain), me =>
                                {
                                    me.Text = CameraControl.UseGain ? "Gain: ON" : "Gain: OFF";
                                    me.TintColor = CameraControl.UseGain
                                        ? Color.FromArgb("#10B981")
                                        : Color.FromArgb("#6B7280");
                                }),
                            new SettingsButton("🎙️", "Speech: OFF") { TintColor = Color.FromArgb("#475569"), }
                                .Assign(out _speechButton)
                                .OnTapped(me => { ToggleSpeech(); }),
                        }
                    }
                };

            var sectionOutput =
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
                        Padding = new Thickness(16, 16, 16, 24),
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Start,
                        Children =
                        {
                            //CreateDrawerSectionTitle("Recording Output", "Decide what the demo saves and exports"),

                            new SettingsButton("🔇", "Audio") { TintColor = Color.FromArgb("#6B7280"), }
                                .OnTapped(me =>
                                {
                                    CameraControl.EnableAudioRecording = !CameraControl.EnableAudioRecording;
                                })
                                .ObserveProperty(CameraControl, nameof(CameraControl.EnableAudioRecording), me =>
                                {
                                    me.AccessoryIcon = CameraControl.EnableAudioRecording ? "🔊" : "🔇";
                                    me.Text = CameraControl.EnableAudioRecording ? "Audio: SAVE" : "Audio: SKIP";
                                    me.TintColor = CameraControl.EnableAudioRecording
                                        ? Color.FromArgb("#10B981")
                                        : Color.FromArgb("#6B7280");
                                })
                                .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode),
                                    me => { me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video; }),
                            new SettingsButton("🎵", "Audio Codec") { TintColor = Color.FromArgb("#475569"), }
                                .Assign(out _audioCodecButton)
                                .OnTapped(async me => { await SelectAudioCodec(); })
                                .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode),
                                    me => { me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video; }),
                            new SettingsButton("📹", "Video") { TintColor = Color.FromArgb("#10B981"), }
                                .OnTapped(me =>
                                {
                                    CameraControl.EnableVideoRecording = !CameraControl.EnableVideoRecording;
                                })
                                .ObserveProperty(CameraControl, nameof(CameraControl.EnableVideoRecording), me =>
                                {
                                    me.Text = CameraControl.EnableVideoRecording ? "Video: SAVE" : "Video: SKIP";
                                    me.TintColor = CameraControl.EnableVideoRecording
                                        ? Color.FromArgb("#10B981")
                                        : Color.FromArgb("#6B7280");
                                })
                                .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode),
                                    me => { me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video; }),
                            new SettingsButton("⏳", "Pre-Record: OFF") { TintColor = Color.FromArgb("#6B7280"), }
                                .Assign(out _preRecordingButton)
                                .OnTapped(async me => { await ShowPreRecordPicker(); })
                                .ObserveProperty(CameraControl, nameof(CameraControl.CaptureMode),
                                    me => { me.IsVisible = CameraControl.CaptureMode == CaptureModeType.Video; }),
                            new SettingsButton("📍", "Geotag: OFF") { TintColor = Color.FromArgb("#6B7280"), }
                                .OnTapped(me =>
                                {
                                    CameraControl.InjectGpsLocation = !CameraControl.InjectGpsLocation;
                                    RefreshGpsLocationIfNeeded();
                                })
                                .ObserveProperty(CameraControl, nameof(CameraControl.InjectGpsLocation), me =>
                                {
                                    me.Text = CameraControl.InjectGpsLocation ? "Geotag: ON" : "Geotag: OFF";
                                    me.TintColor = CameraControl.InjectGpsLocation
                                        ? Color.FromArgb("#10B981")
                                        : Color.FromArgb("#6B7280");
                                }),
                        }
                    }
                };

            return new SkiaStack()
            {
                Spacing = 4,
                Margin = new Thickness(0, 40, 0, 0),
                Children =
                {
                    tabBar,
                    new SkiaViewSwitcher()
                        {
                            VerticalOptions = LayoutOptions.Fill,
                            HorizontalOptions = LayoutOptions.Fill,
                            SelectedIndex = 0,
                            Children = { sectionInput, sectionProcessing, sectionOutput, }
                        }
                        .Assign(out _settingsTabs),
                }
            };
        }

        private SkiaShape CreateDrawerTab(int index, string title)
        {
            return new SkiaShape()
                {
                    Type = ShapeType.Rectangle,
                    CornerRadius = 16,
                    BackgroundColor = index == 0 ? Color.FromArgb("#2D11C5BF") : Color.FromArgb("#00000000"),
                    StrokeColor = index == 0 ? Color.FromArgb("#6611C5BF") : Color.FromArgb("#00000000"),
                    StrokeWidth = 1,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Padding = new Thickness(14, 10),
                    Children =
                    {
                        new SkiaLabel(title)
                            {
                                FontSize = 13,
                                FontAttributes = index == 0 ? FontAttributes.Bold : FontAttributes.None,
                                TextColor = index == 0 ? Colors.White : Color.FromArgb("#8FA3B7"),
                                HorizontalOptions = LayoutOptions.Fill,
                                HorizontalTextAlignment = DrawTextAlignment.Center,
                                VerticalOptions = LayoutOptions.Center,
                                UseCache = SkiaCacheType.Operations,
                            }
                            .Assign(out _tabLabels[index])
                    }
                }
                .Assign(out _tabPills[index])
                .OnTapped(me => SelectTab(index));
        }

        private SkiaLayout CreateDrawerSectionTitle(string title, string subtitle)
        {
            return new SkiaLayout()
            {
                WidthRequest = 320,
                Margin = new Thickness(4, 2, 4, 2),
                Children =
                {
                    new SkiaLabel(title)
                    {
                        FontSize = 15,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White,
                        UseCache = SkiaCacheType.Operations,
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Start,
                    },
                    new SkiaLabel(subtitle)
                    {
                        Margin = new Thickness(0, 20, 0, 0),
                        FontSize = 12,
                        TextColor = Color.FromArgb("#8FA3B7"),
                        UseCache = SkiaCacheType.Operations,
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Start,
                    }
                }
            };
        }

        private SkiaLayer CreateTakenPhotoPreviewPopup()
        {
            return new SkiaLayer
            {
                IsVisible = false,
                UseCache = SkiaCacheType.Image,
                BackgroundColor = Color.FromArgb("#E0060B12"),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                ZIndex = 1000,
                Children =
                {
                    new SkiaShape()
                    {
                        UseCache = SkiaCacheType.Image,
                        Type = ShapeType.Rectangle,
                        CornerRadius = 30,
                        BackgroundColor = Color.FromArgb("#D90A101A"),
                        StrokeColor = Color.FromArgb("#3342D9F6"),
                        StrokeWidth = 1,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        Margin = new Thickness(20, 48, 20, 28),
                        Padding = new Thickness(22, 20, 22, 22),
                        Children =
                        {
                            new SkiaLabel("Captured Photo")
                            {
                                FontSize = 12,
                                CharacterSpacing = 1,
                                TextColor = Color.FromArgb("#7DEAE5"),
                                UseCache = SkiaCacheType.Operations,
                                HorizontalOptions = LayoutOptions.Start,
                                VerticalOptions = LayoutOptions.Start,
                            },
                            new SkiaLabel("Review before saving to gallery")
                            {
                                Margin = new Thickness(0, 18, 0, 0),
                                FontSize = 13,
                                TextColor = Color.FromArgb("#A7B5C6"),
                                UseCache = SkiaCacheType.Operations,
                                HorizontalOptions = LayoutOptions.Start,
                                VerticalOptions = LayoutOptions.Start,
                            },
                            new SkiaShape
                            {
                                Type = ShapeType.Rectangle,
                                StrokeColor = Color.FromArgb("#FF101825"),
                                StrokeWidth = 8,
                                CornerRadius = 14,
                                Margin = new Thickness(0, 64, 0, 74),
                                IsClippedToBounds = true,
                                Children =
                                {
                                    new SkiaImage()
                                        {
                                            Aspect = TransformAspect.AspectFit,
                                            MaximumWidthRequest = 420,
                                            MaximumHeightRequest = 620,
                                        }
                                        .Assign(out _previewImage)
                                }
                            },
                            new SkiaShape()
                                {
                                    UseCache = SkiaCacheType.Operations,
                                    Type = ShapeType.Circle,
                                    WidthRequest = 42,
                                    HeightRequest = 42,
                                    LockRatio = 1,
                                    CornerRadius = 21,
                                    BackgroundColor = Color.FromArgb("#260F172A"),
                                    StrokeColor = Color.FromArgb("#3342D9F6"),
                                    StrokeWidth = 1,
                                    HorizontalOptions = LayoutOptions.End,
                                    VerticalOptions = LayoutOptions.Start,
                                    Children =
                                    {
                                        new SkiaLabel(IconFont.Close)
                                        {
                                            FontSize = 18,
                                            FontFamily = "FontIcons",
                                            TextColor = Colors.White,
                                            HorizontalOptions = LayoutOptions.Center,
                                            VerticalOptions = LayoutOptions.Center,
                                        }
                                    }
                                }
                                .OnTapped(me => { HidePreviewOverlay(); }),
                            new SkiaLayout()
                            {
                                Type = LayoutType.Row,
                                Spacing = 12,
                                HorizontalOptions = LayoutOptions.End,
                                VerticalOptions = LayoutOptions.End,
                                Children =
                                {
                                    new SkiaShape()
                                        {
                                            UseCache = SkiaCacheType.Image,
                                            Type = ShapeType.Rectangle,
                                            CornerRadius = 18,
                                            BackgroundColor = Color.FromArgb("#260F172A"),
                                            StrokeColor = Color.FromArgb("#55FF5A5F"),
                                            StrokeWidth = 1,
                                            Padding = new Thickness(18, 12),
                                            Children =
                                            {
                                                new SkiaLabel("Discard")
                                                {
                                                    FontSize = 14,
                                                    FontAttributes = FontAttributes.Bold,
                                                    TextColor = Color.FromArgb("#FFB4B7"),
                                                    UseCache = SkiaCacheType.Operations,
                                                }
                                            }
                                        }
                                        .OnTapped(me => { HidePreviewOverlay(); }),
                                    new SkiaShape()
                                        {
                                            UseCache = SkiaCacheType.Image,
                                            Type = ShapeType.Rectangle,
                                            CornerRadius = 18,
                                            BackgroundColor = Color.FromArgb("#2D10B981"),
                                            StrokeColor = Color.FromArgb("#6610B981"),
                                            StrokeWidth = 1,
                                            Padding = new Thickness(18, 12),
                                            Children =
                                            {
                                                new SkiaLabel("Save to Gallery")
                                                {
                                                    FontSize = 14,
                                                    FontAttributes = FontAttributes.Bold,
                                                    TextColor = Colors.White,
                                                    UseCache = SkiaCacheType.Operations,
                                                }
                                            }
                                        }
                                        .OnTapped(async me => { await SaveCurrentImageToGallery(); })
                                }
                            }
                        }
                    }
                }
            };
        }

        void ShowAlert(string title, string message)
        {
            MainThread.BeginInvokeOnMainThread(async () => { await DisplayAlert(title, message, "OK"); });
        }

        private void SelectTab(int index)
        {
            if (_settingsTabs != null)
                _settingsTabs.SelectedIndex = index;

            if (_tabLabels != null && _tabPills != null)
            {
                for (int i = 0; i < _tabLabels.Length; i++)
                {
                    _tabLabels[i].TextColor = i == index
                        ? Colors.White
                        : Color.FromArgb("#8FA3B7");
                    _tabLabels[i].FontAttributes = i == index
                        ? FontAttributes.Bold
                        : FontAttributes.None;
                    _tabPills[i].BackgroundColor = i == index
                        ? Color.FromArgb("#2D11C5BF")
                        : Color.FromArgb("#00000000");
                    _tabPills[i].StrokeColor = i == index
                        ? Color.FromArgb("#6611C5BF")
                        : Color.FromArgb("#00000000");
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

        private void UpdatePreRecordingStatus()
        {
            if (_preRecordingButton != null)
            {
                if (CameraControl.EnablePreRecording)
                {
                    _preRecordingButton.Text = $"Pre-Record: {CameraControl.PreRecordDuration.TotalSeconds:F0}s";
                    _preRecordingButton.TintColor = Color.FromArgb("#10B981");
                }
                else
                {
                    _preRecordingButton.Text = "Pre-Record: OFF";
                    _preRecordingButton.TintColor = Color.FromArgb("#6B7280");
                }
            }
        }

        private void OnPermissionsResultChanged(object sender, bool e)
        {
            if (!e)
            {
                ShowAlert("Error",
                    "The application does not have the required permissions to access all the camera features.");
            }
        }
    }
}
