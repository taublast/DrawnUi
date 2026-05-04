using DrawnUi.Draw;

namespace DrawnUi.Draw;

public class SkiaCursor : SkiaShape
{
    private SkiaLabel _label;

    public override void OnParentChanged(IDrawnBase newvalue, IDrawnBase oldvalue)
    {
        base.OnParentChanged(newvalue, oldvalue);

        if (Parent is SkiaEditor editor)
        {
            _label = editor.Label;
            if (_blinkAnimator == null)
            {
                _blinkAnimator = new ToggleAnimator(this)
                {
                    Repeat = -1,
                    Speed = 1000,
                    Ratio = 0.5,
                    OnUpdated = (value) =>
                    {
                        if (!_blinkAnimator.State)
                        {
                            Opacity = 1.0;
                        }
                        else
                        {
                            Opacity = 0.01;
                        }
                    }
                };
            }
        }
    }

    public override void Invalidate()
    {
        base.Invalidate();

        if (_blinkAnimator != null)
        {
            if (CanDraw)
                _blinkAnimator.Start();
            else
                _blinkAnimator.Stop();
        }
    }

    private ToggleAnimator _blinkAnimator;

    //public override void OnBeforeDraw()
    //{
    //	base.OnBeforeDraw();

    //	if (_label != null)
    //	{
    //		this.HeightRequest = _label.LineHeightPixels / RenderingScale;
    //	}

    //}

    public static float BlinkAlpha = 0.0f;

    protected void UpdateColors()
    {
        BackgroundColor = Color;
        //todo add gradients
    }

    public override void OnDisposing()
    {
        _label = null;

        _blinkAnimator.Dispose();

        base.OnDisposing();
    }

    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color),
        typeof(Color),
        typeof(SkiaCursor),
        Colors.Red, propertyChanged: (b, o, n) =>
        {
            if (b is SkiaCursor control)
            {
                control.UpdateColors();
            }
        });

    public Color Color
    {
        get { return (Color)GetValue(ColorProperty); }
        set { SetValue(ColorProperty, value); }
    }

}