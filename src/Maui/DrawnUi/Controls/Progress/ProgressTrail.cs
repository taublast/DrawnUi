using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

/// <summary>
/// Progress trail component for linear progress bars.
/// Similar to SliderTrail but optimized for progress display.
/// </summary>
public class ProgressTrail : SkiaShape
{
    public static readonly BindableProperty XPosProperty = BindableProperty.Create(
        nameof(XPos), typeof(double), typeof(ProgressTrail), 0.0);

    /// <summary>
    /// Starting X position of the progress trail
    /// </summary>
    public double XPos
    {
        get { return (double)GetValue(XPosProperty); }
        set { SetValue(XPosProperty, value); }
    }

    public static readonly BindableProperty XPosEndProperty = BindableProperty.Create(
        nameof(XPosEnd), typeof(double), typeof(ProgressTrail), 0.0);

    /// <summary>
    /// Ending X position of the progress trail
    /// </summary>
    public double XPosEnd
    {
        get { return (double)GetValue(XPosEndProperty); }
        set { SetValue(XPosEndProperty, value); }
    }

    public static readonly BindableProperty SideOffsetProperty = BindableProperty.Create(
        nameof(SideOffset), typeof(double), typeof(ProgressTrail), 0.0);

    /// <summary>
    /// Additional offset applied to both sides
    /// </summary>
    public double SideOffset
    {
        get { return (double)GetValue(SideOffsetProperty); }
        set { SetValue(SideOffsetProperty, value); }
    }

    protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(XPosEnd) ||
            propertyName == nameof(XPos) ||
            propertyName == nameof(SideOffset))
        {
            UpdateLayout();
        }
    }

    private void UpdateLayout()
    {
        var width = Math.Max(0, XPosEnd - XPos);
        this.Margin = new Thickness(XPos + SideOffset, 0, 0, 0);
        this.WidthRequest = width;
    }
}
