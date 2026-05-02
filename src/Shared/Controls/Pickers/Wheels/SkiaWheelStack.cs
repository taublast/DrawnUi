namespace DrawnUi.Controls
{
    public class SkiaWheelStack : SkiaLayout
    {
        public SkiaWheelStack()
        {
            Type = LayoutType.Column;
            //RecyclingTemplate = RecyclingTemplate.Enabled;
            //MeasureItemsStrategy = MeasuringStrategy.MeasureFirst;
            Spacing = 0;
        }


        public override ScaledRect GetOnScreenVisibleArea(DrawingContext context, Vector2 inflateByPixels = default)
        {
            return ScaledRect.FromPixels(new(0, 0, Single.PositiveInfinity, Single.PositiveInfinity), context.Scale);
        }

 
    }
}
