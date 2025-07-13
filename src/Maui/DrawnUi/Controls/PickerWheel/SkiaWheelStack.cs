namespace DrawnUi.Controls
{
    public class SkiaWheelStack : SkiaLayout
    {
        public SkiaWheelStack()
        {
            Type = LayoutType.Column;
            RecyclingTemplate = RecyclingTemplate.Disabled;
            Spacing = 0;
        }

        public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            var ret = base.OnMeasuring(widthConstraint, heightConstraint, scale);

            var check = this.DebugString;

            return ret;
        }

        public override ScaledRect GetOnScreenVisibleArea(DrawingContext context, Vector2 inflateByPixels = default)
        {
            return ScaledRect.FromPixels(new(0, 0, Single.PositiveInfinity, Single.PositiveInfinity), context.Scale);
        }

    }
}
