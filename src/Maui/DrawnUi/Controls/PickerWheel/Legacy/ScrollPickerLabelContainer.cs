using DrawnUi.Draw;

namespace DrawnUi.Controls;

public class ScrollPickerLabelContainer : SkiaLayout, IInsideWheelStack
{
    public ScrollPickerLabelContainer()
    {
        UpdateControl();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
    }

    public static readonly BindableProperty ColorTextSelectedProperty = BindableProperty.Create(nameof(ColorTextSelected),
        typeof(Color),
        typeof(ScrollPickerLabelContainer),
        Colors.Orange, propertyChanged: OnNeedUpdate);

    public Color ColorTextSelected
    {
        get { return (Color)GetValue(ColorTextSelectedProperty); }
        set { SetValue(ColorTextSelectedProperty, value); }
    }

    public static readonly BindableProperty ColorTextProperty = BindableProperty.Create(nameof(ColorText),
        typeof(Color),
        typeof(ScrollPickerLabelContainer),
        Colors.White, propertyChanged: OnNeedUpdate);
    public Color ColorText
    {
        get { return (Color)GetValue(ColorTextProperty); }
        set { SetValue(ColorTextProperty, value); }
    }

    private static void OnNeedUpdate(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is ScrollPickerLabelContainer control)
        {
            control.UpdateControl();
        }
    }

    public void UpdateControl()
    {
        if (Views.FirstOrDefault(x => x is SkiaLabel) is SkiaLabel label)
        {
            var color = IsSelected ? ColorTextSelected : ColorText;
            label.TextColor = color.WithAlpha(TextOpacity);
        }
    }

    protected bool IsSelected { get; set; }
    protected float TextOpacity { get; set; } = 1;
    public void OnPositionChanged(float offsetRatio, bool isSelected)
    {
        var opacity = 1.0f - offsetRatio * 2.5f;
        if (opacity > 1)
            opacity = 1;
        else
        if (opacity < 0)
            opacity = 0;

        TextOpacity = opacity;
        IsSelected = isSelected;
        UpdateControl();
    }
}
