namespace PreviewTests.Views.Controls;

public class SomeGrid : Grid
{
    public override SizeRequest Measure(double widthConstraint, double heightConstraint, MeasureFlags flags = MeasureFlags.None)
    {
        var ret = base.Measure(widthConstraint, heightConstraint, flags);
        return ret;
    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        return base.MeasureOverride(widthConstraint, heightConstraint);
    }

    protected override SizeRequest OnMeasure(double widthConstraint, double heightConstraint)
    {
        var ret = base.OnMeasure(widthConstraint, heightConstraint);
        return ret;
    }

    protected override Size ArrangeOverride(Rect bounds)
    {
        var ret = base.ArrangeOverride(bounds);
        return ret;
    }
}

public partial class SomeContent : ContentView
{
    public SomeContent()
    {
        InitializeComponent();
    }
}
