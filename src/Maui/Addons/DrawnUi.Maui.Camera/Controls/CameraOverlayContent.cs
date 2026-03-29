namespace DrawnUi.Camera
{
    public class CameraOverlayContent : SkiaLayer
    {
        public SKRect Limits;

        public DeviceOrientation Orientation;

        public CameraOverlayContent()
        {
            VerticalOptions = LayoutOptions.Fill;
        }

        public override void Arrange(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            base.Arrange(
                new SKRect(0, 0, MeasuredSize.Pixels.Width, MeasuredSize.Pixels.Height),
                MeasuredSize.Pixels.Width, MeasuredSize.Pixels.Height, scale);
        }

        public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            if (Orientation == DeviceOrientation.LandscapeLeft || Orientation == DeviceOrientation.LandscapeRight)
            {
                return base.OnMeasuring(heightConstraint, widthConstraint, scale);
            }

            return base.OnMeasuring(widthConstraint, heightConstraint, scale);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context.WithDestination(Limits));
        }
    }



}
