using CameraTests.UI;
using DrawnUi.Camera;

namespace CameraTests.Views
{
    public partial class AppCamera : SkiaCamera
    {
        public AppCamera()
        {
            //set defaults for this camera
            NeedPermissionsSet = NeedPermissions.Camera | NeedPermissions.Gallery | NeedPermissions.Microphone | NeedPermissions.Location;

            //GPS metadata
            this.InjectGpsLocation = true;

            //audio 
            this.EnableAudioMonitoring = true;

            FrameProcessor = OnFrameProcessing;
            PreviewProcessor = OnFrameProcessing;



#if DEBUG
            VideoDiagnosticsOn = true;
#endif
        }

        public static readonly BindableProperty UseGainProperty = BindableProperty.Create(
            nameof(UseGain),
            typeof(bool),
            typeof(AppCamera),
            false);

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
            if (OverlayPreview is IAppOverlay appOverlay)
            {
                VisualizerName = appOverlay.SwitchVisualizer(index);
            }
            if (OverlayRecording is IAppOverlay appOverlayRecording)
            {
                VisualizerName = appOverlayRecording.SwitchVisualizer(index);
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

            if (OverlayPreview is IAppOverlay appOverlay)
            {
                appOverlay.AddAudioSample(sample);
            }
            if (OverlayRecording is IAppOverlay appOverlayRecording)
            {
                appOverlayRecording.AddAudioSample(sample);
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

        public override void OnWillDisposeWithChildren()
        {
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

        private DeviceOrientation _orientation;
        private float _previewScale;
        private float _renderedScale;
        private SKRect _rectFramePreview;
        private SKRect _rectFrameRecording;
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

        void OnFrameProcessing(DrawableFrame frame)
        {
            bool DrawOverlay(SkiaLayout layout, bool skipRendering)
            {

                if (this.CurrentVideoFormat != _adaptedToFormat)
                {
                    _adaptedToFormat = this.CurrentVideoFormat;
                    AdjustOverlayScale();
                }

                if (_overlayScale != _overlayScaleChanged)
                {
                    _overlayScale = _overlayScaleChanged;
                    _rectFramePreview = SKRect.Empty;
                    _rectFrameRecording = SKRect.Empty;
                }

                if (frame.IsPreview && frame.Scale != _previewScale)
                {
                    _rectFramePreview = SKRect.Empty;
                    _previewScale = frame.Scale;
                }

                var k = _overlayScale;
                var overlayScale = 3 * frame.Scale * k;

                if (_rectOrientation != _orientation && !LockOrientation)
                {
                    _rectFramePreview = SKRect.Empty;
                    _rectFrameRecording = SKRect.Empty;
                    _rectOrientation = _orientation;
                }

                var orientation = _rectOrientation;
                if (!LockOrientation)
                {
                    _rectOrientationLocked = DeviceOrientation.Unknown;
                }
                else
                {
                    if (_rectOrientationLocked == DeviceOrientation.Unknown)
                    {
                        //LOCK
                        _rectOrientationLocked = _rectOrientation;
                    }
                    orientation = _rectOrientationLocked;
                }

                var frameRect = new SKRect(0, 0, frame.Width, frame.Height); ;
                var rectLimits = frameRect;

                if (frame.IsPreview && _rectFramePreview == SKRect.Empty)
                {
                    _rectFramePreview = frameRect;
                    if (!layout.NeedMeasure)
                    {
                        layout.Invalidate();
                    }

                }
                else
                    if (!frame.IsPreview && _rectFrameRecording == SKRect.Empty)
                    {
                        _rectFrameRecording = frameRect;
                        if (!layout.NeedMeasure)
                        {
                            layout.Invalidate();
                        }
                    }

                if (orientation == DeviceOrientation.LandscapeLeft || orientation == DeviceOrientation.LandscapeRight)
                {
                    layout.AnchorX = 0;
                    layout.AnchorY = 0;

                    rectLimits = new SKRect(
                        rectLimits.Top,
                        rectLimits.Left,
                        rectLimits.Top + rectLimits.Height,
                        rectLimits.Left + rectLimits.Width
                    );
                }
                else
                {
                    layout.TranslationX = 0;
                    layout.TranslationY = 0;
                    layout.Rotation = 0;
                }

                //tune up a bit
                //overlayScale *= 0.9f;

                bool wasMeasured = false;

                if (layout.NeedMeasure)
                {
                    if (orientation == DeviceOrientation.LandscapeLeft || orientation == DeviceOrientation.LandscapeRight)
                    {
                        if (orientation == DeviceOrientation.LandscapeLeft)
                        {
                            layout.TranslationX = frameRect.Width / overlayScale - rectLimits.Left / overlayScale;
                            layout.TranslationY = rectLimits.Left / overlayScale; //rotated side offset
                            layout.Rotation = 90;
                        }
                        else // LandscapeRight
                        {
                            layout.TranslationX = -rectLimits.Left / overlayScale;
                            layout.TranslationY = frameRect.Height / overlayScale - rectLimits.Left / overlayScale;
                            layout.Rotation = -90;
                        }

                        var measured = layout.Measure(frameRect.Height, frameRect.Width, overlayScale);
                    }
                    else
                    {
                        var measured = layout.Measure(frameRect.Width, frameRect.Height, overlayScale);
                    }
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
                    Superview = this.Superview  //to enable animations and use disposal manager
                };

                if (!skipRendering)
                {
                    layout.Render(new DrawingContext(ctx, rectLimits, overlayScale));
                    _renderedScale = overlayScale;
                }

                return wasMeasured;
            }

            // Simple text overlay for testing
            if (_paintRec == null)
            {
                _paintRec = new SKPaint
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

            var paint = frame.IsPreview ? _paintPreview : _paintRec;
            paint.TextSize = 32 * frame.Scale;
            paint.Style = SKPaintStyle.Fill;

            // text at top left
            var text = string.Empty;
            var text2 = string.Empty;

            if (this.IsPreRecording)
            {
                text = "PRE";
                text2 = $"{frame.Time:mm\\:ss}";
                paint.Color = SKColors.White;
            }
            else
            if (this.IsRecording)
            {
                text = "LIVE";
                text2 = $"{frame.Time:mm\\:ss}";
                paint.Color = SKColors.Red;
            }
            else
            {
                paint.Color = SKColors.Transparent;
                //text = $"{this.CurrentVideoFormat.Width}x{this.CurrentVideoFormat.Height} ({frame.Width}x{frame.Height}) x{_renderedScale:0.00}";
                //text = $"{frame.Width}x{frame.Height}";
            }

            //if (_labelRec != null)
            //{
            //    _labelRec.Text = CameraControl.IsPreRecording ? "PRE" : "REC";
            //}

            if (OverlayPreview != null && frame.IsPreview) //PREVIEW small frame
            {
                DrawOverlay(OverlayPreview, false);
            }
            else
            if (OverlayRecording != null && !frame.IsPreview) //RAW frame being recorded
            {
                DrawOverlay(OverlayRecording, false);
            }

            //if (frame.IsPreview)
            {
                // draw frame indicator
                if (paint.Color != SKColors.Transparent)
                {
                    paint.Style = SKPaintStyle.Stroke;
                    paint.StrokeWidth = 2 * frame.Scale;
                    frame.Canvas.DrawRect(10 * frame.Scale, 10 * frame.Scale, frame.Width - 20 * frame.Scale, frame.Height - 20 * frame.Scale, paint);
                }

                if (!string.IsNullOrEmpty(text))
                {
                    paint.TextSize = 48 * frame.Scale;
                    paint.Color = IsPreRecording ? SKColors.White : SKColors.Red;
                    paint.Style = SKPaintStyle.Fill;

                    if (IsRecording || IsPreRecording)
                    {
                        // text at top left
                        frame.Canvas.DrawText(text, 50 * frame.Scale, 100 * frame.Scale, paint);
                        frame.Canvas.DrawText(text2, 50 * frame.Scale, 160 * frame.Scale, paint);
                    }
                    else
                    {
                        paint.Color = SKColors.White;
                        frame.Canvas.DrawText(text, 50 * frame.Scale, 100 * frame.Scale, paint);
                    }
                }
            }
        }

        protected SkiaLayout OverlayPreview;
        protected SkiaLayout OverlayRecording;

        /// <summary>
        /// Set layouts to be rendered over preview and recording frames.
        /// Different instances are needed to avoid remeasuring when switching between preview and recording.
        /// This must be two copies of *same* layout, if you specify different layouts for preview and recording on some platforms only recording layout will be displayed while recording .
        /// </summary>
        /// <param name="previewLayout"></param>
        /// <param name="recordingLayout"></param>
        public void InitializeOverlayLayouts(SkiaLayout previewLayout, SkiaLayout recordingLayout)
        {
            if (previewLayout != null && recordingLayout != null)
            {
                this.OverlayPreview = previewLayout;
                this.OverlayRecording = recordingLayout;

                previewLayout.UseCache = SkiaCacheType.Operations;
                previewLayout.Tag = "Preview";

                recordingLayout.UseCache = SkiaCacheType.Operations;
                recordingLayout.Tag = "Recording";
 
                InvalidateOverlays();

                if (previewLayout is FrameOverlay overlay)
                {
                    VisualizerName = overlay.Visualizer.VisualizerName;
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
            _rectFrameRecording = SKRect.Empty;
        }

        #endregion
    }
}
