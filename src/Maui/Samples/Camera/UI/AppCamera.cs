using CameraTests.UI;
using DrawnUi.Camera;
using DrawnUi.Infrastructure;

namespace CameraTests.Views
{
    public partial class AppCamera : SkiaCamera
    {
        private SkiaShader _effectShader;
        private ShaderEffect _loadedEffect;

        public static readonly BindableProperty VideoEffectProperty = BindableProperty.Create(
            nameof(VideoEffect),
            typeof(ShaderEffect),
            typeof(AppCamera),
            ShaderEffect.None);

        public ShaderEffect VideoEffect
        {
            get => (ShaderEffect)GetValue(VideoEffectProperty);
            set => SetValue(VideoEffectProperty, value);
        }

        public AppCamera()
        {
            //set defaults for this camera
            NeedPermissionsSet = NeedPermissions.Camera | NeedPermissions.Gallery | NeedPermissions.Microphone;

            //GPS metadata
            this.InjectGpsLocation = true;

            //audio 
            this.EnableAudioMonitoring = false;

            this.EnableAudioRecording = true;
            UseRealtimeVideoProcessing = true;

            VideoQuality = VideoQuality.Standard;

            ProcessFrame = OnFrameProcessing;
            ProcessPreview = OnFrameProcessing;

#if DEBUG
            VideoDiagnosticsOn = true;
            //ConstantUpdate = true;
#endif
        }

        public static readonly BindableProperty UseGainProperty = BindableProperty.Create(
            nameof(UseGain),
            typeof(bool),
            typeof(AppCamera),
            true);

        public bool UseGain
        {
            get => (bool)GetValue(UseGainProperty);
            set => SetValue(UseGainProperty, value);
        }


        public static readonly BindableProperty VisualizerNameProperty = BindableProperty.Create(
            nameof(VisualizerName),
            typeof(string),
            typeof(AppCamera),
            "None");

        public string VisualizerName
        {
            get => (string)GetValue(VisualizerNameProperty);
            set => SetValue(VisualizerNameProperty, value);
        }


        /// <summary>
        /// Gain multiplier applied to raw PCM when UseGain is true.
        /// </summary>
        public float GainFactor { get; set; } = 3.0f;

        public void SwitchVisualizer(int index = -1)
        {
            if (VideoDataOverlay is IAppOverlay appOverlay)
            {
                VisualizerName = appOverlay.SwitchVisualizer(index);
            }
        }

        public event Action<AudioSample> OnAudioSample; 

        protected override AudioSample OnAudioSampleAvailable(AudioSample sample)
        {
            if (UseGain && sample.Data != null && sample.Data.Length > 1)
            {
                AmplifyPcm16(sample.Data, GainFactor);
            }

            OnAudioSample?.Invoke(sample);

            if (VideoDataOverlay is IAppOverlay appOverlay)
            {
                appOverlay.AddAudioSample(sample);
            }

            return base.OnAudioSampleAvailable(sample);
        }

        /// <summary>
        /// Amplifies PCM16 audio data in-place. Zero allocations.
        /// </summary>
        private static void AmplifyPcm16(byte[] data, float gain)
        {
            for (int i = 0; i < data.Length - 1; i += 2)
            {
                int sample = (short)(data[i] | (data[i + 1] << 8));
                sample = (int)(sample * gain);

                // Clamp to 16-bit range
                if (sample > 32767) sample = 32767;
                else if (sample < -32768) sample = -32768;

                data[i] = (byte)(sample & 0xFF);
                data[i + 1] = (byte)((sample >> 8) & 0xFF);
            }
        }

        protected override void RenderPreviewForProcessing(SKCanvas canvas, SKImage frame)
        {
            var shader = GetEffectShader();
            if (shader == null)
            {
                base.RenderPreviewForProcessing(canvas, frame);
                return;
            }

            shader.DrawImage(canvas, frame, 0, 0);
        }

        protected override void RenderFrameForRecording(SKCanvas canvas, SKImage frame, SKRect src, SKRect dst)
        {
            var shader = GetEffectShader();
            if (shader == null)
            {
                base.RenderFrameForRecording(canvas, frame, src, dst);
                return;
            }

            shader.DrawRect(canvas, frame, dst);
        }

        private SkiaShader GetEffectShader()
        {
            var effect = VideoEffect;
            if (effect == ShaderEffect.None)
            {
                ReleaseEffectShader();
                return null;
            }

            if (_effectShader != null && _loadedEffect == effect)
            {
                return _effectShader;
            }

            ReleaseEffectShader();

            var filename = ShaderEffectHelper.GetFilename(effect);
            if (string.IsNullOrWhiteSpace(filename))
            {
                return null;
            }

            _effectShader = SkiaShader.FromResource(filename);
            _loadedEffect = effect;

            return _effectShader;
        }

        private void ReleaseEffectShader()
        {
            _effectShader?.Dispose();
            _effectShader = null;
            _loadedEffect = ShaderEffect.None;
        }

        public override void OnWillDisposeWithChildren()
        {
            ReleaseEffectShader();

            base.OnWillDisposeWithChildren();

            _paintRec?.Dispose();
            _paintRec = null;
            _paintPreview?.Dispose();
            _paintPreview = null;
        }

        public void DrawOverlay(DrawableFrame frame)
        {
            SKPaint paint;
            var canvas = frame.Canvas;
            var width = frame.Width;
            var height = frame.Height;
            var scale = frame.Scale;

            if (frame.IsPreview)
            {
                if (_paintPreview == null)
                {
                    _paintPreview = new SKPaint
                    {
                        IsAntialias = true,
                    };
                }
                paint = _paintPreview;
            }
            else
            {
                if (_paintRec == null)
                {
                    _paintRec = new SKPaint
                    {
                        IsAntialias = true,
                    };
                }
                paint = _paintRec;
            }

            paint.TextSize = 48 * scale;
            paint.Color = IsPreRecording ? SKColors.White : SKColors.Red;
            paint.Style = SKPaintStyle.Fill;

            if (IsRecording || IsPreRecording)
            {
                // text at top left
                var text = IsPreRecording ? "PRE-RECORDED" : "LIVE";
                canvas.DrawText(text, 50 * scale, 100 * scale, paint);
                canvas.DrawText($"{frame.Time:mm\\:ss}", 50 * scale, 160 * scale, paint);

                // Draw a simple border around the frame
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = 4 * scale;
                canvas.DrawRect(10 * scale, 10 * scale, width - 20 * scale, height - 20 * scale, paint);
            }
            else
            {
                paint.Color = SKColors.White;
                var text = $"PREVIEW {this.CaptureMode}";
                canvas.DrawText(text, 50 * scale, 100 * scale, paint);
            }

            //if (UseRealtimeVideoProcessing && EnableAudioRecording)
            //{
            //    _audioVisualizer?.Render(canvas, width, height, scale);
            //}
        }


        #region DRAWN LAYOUT

        private SKPaint _paintPreview;
        private SKPaint _paintRec;
        private SKPaint _paint;
        private FrameOverlay VideoDataOverlay;
        private SKColor _paintColor;
        private SKColor _paintPreviewColor;
        private float _paintStrokeWidth = -1f;
        private float _paintPreviewStrokeWidth = -1f;
        private float _paintTextSize = -1f;
        private float _paintPreviewTextSize = -1f;
        private SKPaintStyle _paintStyle;       // default(SKPaintStyle) == Fill == 0
        private SKPaintStyle _paintPreviewStyle;

        private DeviceOrientation _orientation;

        /// <summary>
        /// Current device orientation used by the frame overlay renderer.
        /// Set this from the page whenever the device orientation changes.
        /// </summary>
        public DeviceOrientation Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation != value)
                {
                    _orientation = value;
                    InvalidateOverlays();
                }
            }
        }
        private float _previewScale;
        private float _renderedScale;
        private SKRect _rectFramePreview;
        private float _overlayScale = 1;
        private float _overlayScaleChanged = -1;
        private VideoFormat _adaptedToFormat;
        private DeviceOrientation _rectOrientation = DeviceOrientation.Unknown;
        private DeviceOrientation _rectOrientationLocked = DeviceOrientation.Unknown;
        public bool LockOrientation { get; set; }

        float GetOverlayBaseDivider(float smallSize)
        {
            var setting = smallSize;
            return setting switch
            {
                0 => 1920f,   // Default 1080p max dimension
                720 => 1280f,   // 1280x720 max
                1080 => 1920f,  // 1920x1080 max
                1440 => 2560f,  // 2560x1440 max
                2160 => 3840f,  // 3840x2160 max
                4320 => 7680f,  // 7680x4320 max
                _ => 1920f
            };
        }

        void AdjustOverlayScale()
        {
            var format = this.CurrentVideoFormat;
            if (format == null)
            {
                _overlayScaleChanged = -1;
                return;
            }
            var (formatWidth, formatHeight) = this.GetRotationCorrectedDimensions(format.Width, format.Height);
            var baseDivider = GetOverlayBaseDivider(Math.Min(format.Width, format.Height));
            _overlayScaleChanged = Math.Max(formatWidth, formatHeight) / baseDivider;
        }

        /// <summary>
        /// ProcessFrame + ProcessPreview events
        /// </summary>
        /// <param name="frame"></param>
        void OnFrameProcessing(DrawableFrame frame)
        {
            bool DrawOverlay(FrameOverlay layout, bool skipRendering)
            {
                /*
                    for 1080:
                    | Format    | Min Dimension | k value |
                    |-----------|---------------|---------|
                    | 1920×1080 | 1080          | 1.0     |
                    | 1280×720  | 720           | 0.667   |
                    | 3840×2160 | 2160          | 2.0     |
                    | 1280×768  | 768           | 0.711   |
                 */

                if (CurrentVideoFormat != _adaptedToFormat)
                {
                    _adaptedToFormat = CurrentVideoFormat;
                    AdjustOverlayScale();
                }

                if (_overlayScale != _overlayScaleChanged)
                {
                    _overlayScale = _overlayScaleChanged;
                    _rectFramePreview = SKRect.Empty;
                }

                if (frame.IsPreview && frame.Scale != _previewScale)
                {
                    _rectFramePreview = SKRect.Empty;
                    _previewScale = frame.Scale;
                }

                var k = _overlayScale;
                var overlayScale = 1.5f * frame.Scale * k; //magic number

                if (_rectOrientation != _orientation && !IsRecording && !IsPreRecording) //lock overlay orientation when recording
                {
                    _rectFramePreview = SKRect.Empty;
                    _rectOrientation = _orientation;
                }

                var orientation = _rectOrientation;

                var frameRect = new SKRect(0, 0, frame.Width, frame.Height);
                ;
                //var rectLimits = frameRect;

                if (frame.IsPreview && _rectFramePreview == SKRect.Empty)
                {
                    _rectFramePreview = frameRect;
                    if (!layout.NeedMeasure)
                    {
                        layout.Invalidate();
                    }
                }

                if (this.Orientation == DeviceOrientation.LandscapeLeft || this.Orientation == DeviceOrientation.LandscapeRight)
                {
                    overlayScale *= 0.8f; //smaller in landscape
                }

                bool wasMeasured = false;

                if (layout.NeedMeasure)
                {
                    layout.SetOrientation(orientation, frameRect, overlayScale);
                    var measured = layout.Measure(frameRect.Width, frameRect.Height, overlayScale);

                    layout.Arrange(
                        new SKRect(0, 0, layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height),
                        layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height, overlayScale);

                    wasMeasured = true;
                }

                var ctx = new SkiaDrawingContext()
                {
                    Canvas = frame.Canvas,
                    Width = frame.Width,
                    Height = frame.Height,
                    Superview = Superview //to enable animations and use of disposal manager
                };

                if (!skipRendering)
                {
                    layout.Render(new DrawingContext(ctx, frameRect, overlayScale));
                    _renderedScale = overlayScale;
                }

                return wasMeasured;
            }

            // Simple text overlay for testing
            if (_paint == null)
            {
                _paint = new SKPaint
                {
                    IsAntialias = true,
                };
            }

            if (_paintPreview == null)
            {
                _paintPreview = new SKPaint
                {
                    IsAntialias = true,
                    Color = SKColors.Fuchsia
                };
            }

            var paint = frame.IsPreview ? _paintPreview : _paint;

            // Ref aliases into the correct shadow fields — one branch, zero per-property duplication
            ref var cachedColor = ref (frame.IsPreview ? ref _paintPreviewColor : ref _paintColor);
            ref var cachedTextSize = ref (frame.IsPreview ? ref _paintPreviewTextSize : ref _paintTextSize);
            ref var cachedStyle = ref (frame.IsPreview ? ref _paintPreviewStyle : ref _paintStyle);
            ref var cachedStrokeWidth = ref (frame.IsPreview ? ref _paintPreviewStrokeWidth : ref _paintStrokeWidth);

            var newTextSize = 32 * frame.Scale;
            if (cachedTextSize != newTextSize)
            {
                cachedTextSize = newTextSize; paint.TextSize = newTextSize;
            }

            if (cachedStyle != SKPaintStyle.Fill)
            {
                cachedStyle = SKPaintStyle.Fill; paint.Style = SKPaintStyle.Fill;
            }

            // text at top left
            var text = string.Empty;
            var text2 = string.Empty;
            SKColor newColor;

            if (IsPreRecording)
            {
                //_labelRec.Text = "PRE";
                text2 = $"{frame.Time:mm\\:ss}";
                newColor = SKColors.White;
            }
            else if (IsRecording)
            {
                //_labelRec.Text = "REC";
                text2 = $"{frame.Time:mm\\:ss}";
                newColor = SKColors.Red;
            }
            else
            {
                //_labelRec.Text = string.Empty;
                newColor = SKColors.Transparent;
                text = $"{CurrentVideoFormat.Width}x{CurrentVideoFormat.Height} ({frame.Width}x{frame.Height}) x{_renderedScale:0.00}";
            }

            if (cachedColor != newColor)
            {
                cachedColor = newColor; paint.Color = newColor;
            }


            //PREVIEW - NOT RECORDING BY DESIGN
            if (frame.IsPreview)
            {
                if (VideoDataOverlay != null)
                {
                    DrawOverlay(VideoDataOverlay, false);
                }
            }
            else
            //RECORDING
            {
                if (VideoDataOverlay != null)// && !Model.IsRecording)
                {
                    DrawOverlay(VideoDataOverlay, false);
                }
            }

            if (VideoDiagnosticsOn && cachedColor != SKColors.Transparent)
            {
                var newStrokeWidth = 2 * frame.Scale;
                if (cachedStyle != SKPaintStyle.Stroke) { cachedStyle = SKPaintStyle.Stroke; paint.Style = SKPaintStyle.Stroke; }
                if (cachedStrokeWidth != newStrokeWidth) { cachedStrokeWidth = newStrokeWidth; paint.StrokeWidth = newStrokeWidth; }
                frame.Canvas.DrawRect(10 * frame.Scale, 10 * frame.Scale, frame.Width - 20 * frame.Scale,
                    frame.Height - 20 * frame.Scale, paint);

                frame.Canvas.DrawText($"{frame.Time:mm\\:ss}", 50 * frame.Scale, 160 * frame.Scale, paint);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                _paint?.Dispose();
                _paintPreview?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        public void InitializeOverlayLayouts(FrameOverlay previewLayout)
        {
            if (previewLayout != null)
            {
                this.VideoDataOverlay = previewLayout;

                previewLayout.UseCache = SkiaCacheType.Operations;
                previewLayout.Tag = "Preview";

                InvalidateOverlays();

                if (previewLayout is IAppOverlay appOverlayInit)
                {
                    VisualizerName = appOverlayInit.Visualizer?.VisualizerName ?? "None";
                }
            }
        }

        /// <summary>
        /// Call this when overlays need remeasuring, like camera format change, orientation change etc..
        /// </summary>
        public void InvalidateOverlays()
        {
            _overlayScaleChanged = -1;
            _rectFramePreview = SKRect.Empty;
        }

        #endregion
    }
}
