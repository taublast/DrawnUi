namespace MusicNotes.UI
{
    /// <summary>
    /// Layout that adapts to camera preview area and device orientation
    /// </summary>
    public class WrappingSmartLayout : SkiaLayout
    {

        public SKRect RectLimits { get; protected set; }

        private DeviceOrientation _orientation;
        private int _videoWidth;
        private int _videoHeight;

        public void Layout(SKRect layout, DeviceOrientation orientation, int videoWidth, int videoHeight)
        {
            _orientation = orientation;
            _videoWidth = videoWidth;
            _videoHeight = videoHeight;

            if (_orientation == DeviceOrientation.LandscapeLeft || _orientation == DeviceOrientation.LandscapeRight)
            {
                RectLimits = new SKRect(
                    layout.Top,
                    layout.Left,
                    layout.Top + layout.Height,
                    layout.Left + layout.Width
                );
            }
            else
            {
                RectLimits = layout;
            }

            InvalidateMeasure();
        }

        protected override ScaledSize MeasureContent(IEnumerable<SkiaControl> children, SKRect rectForChildrenPixels, float scale)
        {
            if (RectLimits != SKRect.Empty)
            {
                return base.MeasureContent(children, RectLimits, scale);
            }

            return base.MeasureContent(children, rectForChildrenPixels, scale);
        }

        public override void Render(DrawingContext context)
        {
            if (RectLimits == SKRect.Empty)
            {
                //base.Render(context);
                return;
            }

            if (_orientation == DeviceOrientation.LandscapeLeft || _orientation == DeviceOrientation.LandscapeRight)
            {
                var rect = context.Destination;

                //pivot. with 0 engine is not mapping gestures correctly, todo fix
                AnchorX = 0.0;
                AnchorY = 0.0;

                if (_orientation == DeviceOrientation.LandscapeLeft)
                {
                    //this is for pivot 0.0  
                    TranslationX = rect.Width / context.Scale - RectLimits.Left / context.Scale;
                    TranslationY = RectLimits.Left / context.Scale; //rotated side offset

                    Rotation = 90;
                }
                else // LandscapeRight
                {
                    //this is for pivot 0.0
                    TranslationX = -RectLimits.Left / context.Scale;
                    TranslationY = rect.Height / context.Scale - RectLimits.Left / context.Scale;

                    Rotation = -90;
                }
            }
            else
            {
                TranslationX = 0;
                TranslationY = 0;
                Rotation = 0;
            }

            base.Render(context.WithDestination(RectLimits));
        }
    }
}
