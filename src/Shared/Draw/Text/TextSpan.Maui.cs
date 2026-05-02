using System.Runtime.CompilerServices;
using static DrawnUi.Draw.SkiaControl;

namespace DrawnUi.Draw;

#if BROWSER || DRAWNUI_NET
    using PropertyChangingEventArgs = System.ComponentModel.PropertyChangingEventArgs;
#else
using PropertyChangingEventArgs = Microsoft.Maui.Controls.PropertyChangingEventArgs;
#endif
public partial class TextSpan : Element //we subclassed Element to be able to use internal IElementNode..
{

    #region BINDABLE PROPERTIES

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(TextSpan),
        string.Empty);

    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor), typeof(Color), typeof(TextSpan),
        Colors.GreenYellow,
        propertyChanged: (b, o, n) =>
        {
            if (b is TextSpan c)
            {
                c.HasSetColor = true;
                c.ParentControl?.Update();
            }
        });

    public Color TextColor
    {
        get { return (Color)GetValue(TextColorProperty); }
        set { SetValue(TextColorProperty, value); }
    }

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(double),
        typeof(TextSpan),
        12.0,
        propertyChanged: (b, o, n) =>
        {
            if (b is TextSpan c)
            {
                c.HasSetSize = true;
                c.ParentControl?.Invalidate();
                c.OnPropertyChanged(nameof(DebugString));
            }
        });

    public double FontSize
    {
        get { return (double)GetValue(FontSizeProperty); }
        set { SetValue(FontSizeProperty, value); }
    }

    #endregion

    #region COLOR PROPERTIES

    public bool HasSetColor { get; set; }

    private Color _strikeoutColor = Colors.Red;
    public Color StrikeoutColor
    {
        get
        {
            return _strikeoutColor;
        }
        set
        {
            if (_strikeoutColor != value)
            {
                _strikeoutColor = value;
                OnPropertyChanged();
            }
        }
    }

    private Color _backgroundColor = Colors.Transparent;
    public Color BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (Equals(value, _backgroundColor)) return;
            _backgroundColor = value;
            OnPropertyChanged(nameof(BackgroundColor));
        }
    }

    private Color _paragraphColor = Colors.Transparent;
    public Color ParagraphColor
    {
        get => _paragraphColor;
        set
        {
            if (Equals(value, _paragraphColor)) return;
            _paragraphColor = value;
            OnPropertyChanged(nameof(ParagraphColor));
        }
    }

    #endregion

    /// <summary>
    /// Update the paint with current format properties
    /// </summary>
    public SKPaint SetupPaint(double scale, SKPaint defaultPaint)
    {
        RenderingScale = (float)scale;

        if (Paint == null)
        {
            Paint = new()
            {
                IsAntialias = true,
                IsDither = true,
                Typeface = SkiaFontManager.DefaultTypeface
            };
        };

        if (HasSetFont || AutoFindFont || defaultPaint == null)
        {
            if (TypeFace != null)
            {
                Paint.Typeface = TypeFace;
            }
        }
        else
        {
            if (defaultPaint.Typeface != null)
                Paint.Typeface = defaultPaint.Typeface;
        }

        if (defaultPaint != null && defaultPaint.Typeface != null)
        {
            if (HasSetColor)
            {
                if (TextColor == null)
                    Paint.Color = defaultPaint.Color;
                else
                    Paint.Color = TextColor.ToSKColor();
            }
            else
            {
                Paint.Color = defaultPaint.Color;
            }

            if (HasSetSize)
            {
                Paint.TextSize = (float)Math.Round(FontSize * scale);
                Paint.StrokeWidth = 0;
            }
            else
            {
                //Paint.Typeface = defaultPaint.Typeface;
                Paint.TextSize = defaultPaint.TextSize;
                Paint.StrokeWidth = defaultPaint.StrokeWidth;
            }
        }

        //always use our own attributes
        Paint.FakeBoldText = IsBold;
        if (this.IsItalic)
        {
            Paint.TextSkewX = -0.25f;
        }
        else
        {
            Paint.TextSkewX = 0;
        }

        //todo stroke and gradient for spans..

        return Paint;
    }

    protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName.IsEither(nameof(Text)))
        {
            ParentControl?.Invalidate();
        }

        if (propertyName.IsEither(nameof(FontFamily),
                nameof(FontWeight)))
        {
            UpdateFont();
        }

        if (propertyName.IsEither(
                nameof(TypeFace),
                nameof(FontFamily),
                nameof(FontWeight)))
        {
            HasSetFont = true;
            ParentControl?.Invalidate();
        }

        if (propertyName.IsEither(
                nameof(FontSize)))
        {
            HasSetSize = true;
            ParentControl?.Invalidate();
        }

        if (propertyName.IsEither(nameof(Text), nameof(AutoFindFont)))
        {
            _fontAutoSet = false;
        }
    }

    public TextSpan()
    {
        _typeFace = SkiaFontManager.DefaultTypeface;

        Paint = new()
        {
            IsAntialias = true,
            Typeface = _typeFace
        };
    }

}
