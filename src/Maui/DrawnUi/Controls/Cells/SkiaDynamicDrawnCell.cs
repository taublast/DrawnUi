namespace DrawnUi.Controls;

/// <summary>
/// This cell can watch binding context property changing
/// </summary>
public class SkiaDynamicDrawnCell : SkiaDrawnCell
{
    public void UpdateVisibilityChanged()
    {
        if (Parent is SkiaLayout layout)
        {
            layout.ReportChildVisibilityChanged(this.ContextIndex, IsVisible);
        }
    }

    public override void OnVisibilityChanged(bool value)
    {
        UpdateVisibilityChanged();
    }

    protected override void OnMeasured()
    {
        base.OnMeasured();

        if (WasMeasured && MeasuredSize.Pixels != LastMeasuredSizePixels)
        {
            var invalidate = LastMeasuredSizePixels.Height >= 0 && LastMeasuredSizePixels.Width >= 0;
            LastMeasuredSizePixels = MeasuredSize.Pixels;
            if (invalidate)
            {
                InvalidateChildrenTree();
            }
        }
    }

    protected SKSize LastMeasuredSizePixels = new SKSize(-1, -1);

    protected override void FreeContext()
    {
        if (Context != null)
        {
            Context.PropertyChanged -= ContextPropertyChanged;
        }
        base.FreeContext();
    }

    protected override void AttachContext(object ctx)
    {
        base.AttachContext(ctx);

        if (Context != null)
            Context.PropertyChanged += ContextPropertyChanged;
    }

    protected virtual void ContextPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
 
    }
}
