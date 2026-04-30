using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;


public class InfiniteLayout : ContentLayout
{

    protected override void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(Orientation))
        {
            Invalidate();
        }
    }



    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(ScrollOrientation), typeof(ContentLayout),
        ScrollOrientation.Vertical,
        propertyChanged: NeedDraw);
    /// <summary>
    /// <summary>Gets or sets the scrolling direction of the ScrollView. This is a bindable property.</summary>
    /// </summary>
    public ScrollOrientation Orientation
    {
        get { return (ScrollOrientation)GetValue(OrientationProperty); }
        set { SetValue(OrientationProperty, value); }
    }

    public static readonly BindableProperty ScrollTypeProperty = BindableProperty.Create(nameof(ViewportScrollType), typeof(ViewportScrollType), typeof(ContentLayout),
        ViewportScrollType.Scrollable,
        propertyChanged: NeedDraw);
    /// <summary>
    /// <summary>Gets or sets the scrolling direction of the ScrollView. This is a bindable property.</summary>
    /// </summary>
    public ViewportScrollType ScrollType
    {
        get { return (ViewportScrollType)GetValue(ScrollTypeProperty); }
        set { SetValue(ScrollTypeProperty, value); }
    }



    protected override SKRect GetContentAvailableRect(SKRect destination)
    {
        var childRect = new SKRect(destination.Left, destination.Top, destination.Right, destination.Bottom);
        if (Orientation == ScrollOrientation.Both)
        {
            childRect.Right = float.PositiveInfinity;
            childRect.Bottom = float.PositiveInfinity;
        }
        else
        if (Orientation == ScrollOrientation.Vertical)
        {
            childRect.Right = destination.Right;
            childRect.Bottom = float.PositiveInfinity;
        }
        if (Orientation == ScrollOrientation.Horizontal)
        {
            childRect.Right = float.PositiveInfinity;
            childRect.Bottom = destination.Bottom;
        }
        return childRect;
    }
}
