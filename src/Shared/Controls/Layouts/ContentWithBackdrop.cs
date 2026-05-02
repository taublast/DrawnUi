namespace DrawnUi.Controls;

public class ContentWithBackdrop : ContentLayout
{
    public SkiaBackdrop Backdrop { get; protected set; }

    public bool NoBackdrop { get; set; }

    protected override void SetContent(SkiaControl view)
    {
        base.SetContent(view);

        if (Backdrop == null && !NoBackdrop)
        {
            Backdrop = new SkiaBackdrop()
            {
                Tag = "backdrop",
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                ZIndex = -1,
            };
            if (Views.All(x => x != Backdrop))
            {
                AddSubView(Backdrop);
            }
        }
    }

    protected override int DrawViews(DrawingContext context)
    {
        if (context.Context.Superview == null || context.Destination.Width <= 0 || context.Destination.Height <= 0)
        {
            return 0;
        }

        var drawViews = GetOrderedSubviews();
        return RenderViewsList(context, drawViews);
    }
}
