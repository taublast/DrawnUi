namespace DrawnUi.Controls;

public class ScrollPickerWheel : SkiaLayout, ILayoutInsideViewport
{
    public ScrollPickerWheel()
    {
        // we turn this off because as we repeat views in loop those
        // visible become hidden at the same time and vice versa
        // se we cant recycle hidden cells at all
        RecyclingTemplate = RecyclingTemplate.Disabled;
        MeasureItemsStrategy = MeasuringStrategy.MeasureFirst;
    }

    public static readonly BindableProperty DistortionAngleProperty = BindableProperty.Create(nameof(DistortionAngle),
        typeof(double),
        typeof(ScrollPickerWheel),
        2.8, propertyChanged: NeedDraw);


    public double DistortionAngle
    {
        get { return (double)GetValue(DistortionAngleProperty); }
        set { SetValue(DistortionAngleProperty, value); }
    }

    protected override void OnLayoutChanged()
    {
        //this one constantly changes size, we avoid invoking onsizechanged on every frame..

        //base.OnLayoutReady();
    }

    public override void OnViewportWasChanged(ScaledRect viewport)
    {
        var realHeight = ComputeViewportHeightFromTransformed(viewport.Pixels.Height, 1);
        WheelHeight = realHeight;
        HalfWheelHeight = WheelHeight / 2.0f;
        WheelCenterY = viewport.Pixels.MidY;
        ViewportHeightK = realHeight / viewport.Pixels.Height;

        base.OnViewportWasChanged(viewport);
    }

    private float ComputeViewportHeightFromTransformed(double height, double radians)
    {
        var ret = height * (Math.PI / 2.0) / Math.Cos(radians);
        return (float)ret;
    }

    protected float ViewportHeightK;
    protected float WheelHeight;
    protected float HalfWheelHeight;
    protected float WheelCenterY;

    protected override int GetTemplatesPoolLimit()
    {
        return ItemsSource.Count * 2;
    }

    protected override bool DrawChild(DrawingContext ctx, ISkiaControl child)
    {
        if (child is SkiaControl control &&
            child.CanDraw &&
            HalfWheelHeight > 0)
        {

            var scroll = (SkiaScroll)Parent;
            var destination = ctx.Destination;

            var dest = new SKRect(destination.Left, destination.Top,
                destination.Left + child.MeasuredSize.Pixels.Width,
                destination.Top + child.MeasuredSize.Pixels.Height);


            var itemCenterY = dest.MidY;

            float relativePosition = (WheelCenterY - itemCenterY) / HalfWheelHeight;
            relativePosition = (float)Math.Min(Math.Max((double)relativePosition, -1), 1);
            relativePosition = (float)Math.Max((double)relativePosition, -1);

            var childViewAngle = (float)RadiansToDegrees(relativePosition) * (float)DistortionAngle;
            var distortionZ = 1.0f - (float)DistortionAngle / 10.0f;
            var zDepth = (float)(HalfWheelHeight * (1 - Math.Cos(relativePosition)))* distortionZ;

            var centerX = dest.Left + dest.Width * (float)AnchorX;
            var centerY = dest.Top + dest.Height * (float)AnchorY;

            var centerViewportY = scroll.Destination.Top + scroll.Viewport.Pixels.MidY;

            if (control.Helper3d == null)
            {
                control.Helper3d = new();
            }

            control.Helper3d.Reset();
            control.Helper3d.RotateXDegrees(childViewAngle);
            control.Helper3d.Translate(0, 0, zDepth);
            var applyMatrix = control.Helper3d.Matrix;

            var saved = ctx.Context.Canvas.Save();

            var DrawingMatrix = SKMatrix.CreateTranslation(-centerX, -centerY);
            DrawingMatrix = DrawingMatrix.PostConcat(applyMatrix);
            DrawingMatrix = DrawingMatrix.PostConcat(SKMatrix.CreateTranslation(centerX, centerY));
            DrawingMatrix = DrawingMatrix.PostConcat(ctx.Context.Canvas.TotalMatrix);

            ctx.Context.Canvas.SetMatrix(DrawingMatrix);

            if (child is IInsideWheelStack cell)
            {
                var offset = 0f;
                if (dest.MidY > centerViewportY)
                {
                    offset = dest.MidY - centerViewportY;
                }
                else
                if (centerViewportY > dest.MidY)
                {
                    offset = centerViewportY - dest.MidY;
                }

                var offsetRatio = offset / HalfWheelHeight;
                var isSelected = offset < dest.Height / 2;

                cell.OnPositionChanged(offsetRatio, isSelected);
            }

            _ = base.DrawChild(ctx.WithDestination(dest), child);

            ctx.Context.Canvas.RestoreToCount(saved);

            return true;
        }

        return base.DrawChild(ctx, child);
    }


}
