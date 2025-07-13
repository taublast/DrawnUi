using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using static DrawnUi.Draw.SkiaControl;

namespace DrawnUi.Draw;

[DebuggerDisplay("{DebugString}")]
public class TextSpan : Element, IDisposable //we subclassed Element to be able to use internal IElementNode..
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

    #endregion

    public static TextSpan Default
    {
        get
        {
            return new TextSpan();
        }
    }

    public string DebugString
    {
        get
        {
            return $"'{this.Text}' size {FontSize}";
        }
    }

    public float RenderingScale { get; set; }

    /// <summary>
    /// Ig can be drawn char by char with char spacing etc we use this
    /// </summary>
    public List<UsedGlyph> Glyphs { get; protected set; } = new();

    /// <summary>
    /// If text can be drawn only shaped we use this
    /// </summary>
    public string Shape { get; protected set; }


    public string TextFiltered { get; protected set; }

    /// <summary>
    /// Parse glyphs, setup typeface, replace unrenderable glyphs with fallback character
    /// </summary>
    public void CheckGlyphsCanBeRendered()
    {
        Glyphs = SkiaLabel.GetGlyphs(Text, TypeFace);

        // Use pooled StringBuilder to avoid allocation
        var sb = SkiaLabel.ObjectPools.GetStringBuilder();
        sb.EnsureCapacity(Text.Length); // Pre-allocate based on input length

        foreach (var glyph in Glyphs)
        {
            if (!glyph.IsAvailable)
            {
                sb.Append(((SkiaLabel)Parent).FallbackCharacter);

                if (AutoFindFont && !_fontAutoSet && TypeFace != null)
                {
                    FontDetectedWith = 0;
                    var typeFace = SkiaFontManager.Manager.MatchCharacter(glyph.Symbol);
                    if (typeFace != null)
                    {
                        FontDetectedWith = glyph.Symbol;
                        NeedShape = SkiaLabel.UnicodeNeedsShaping(glyph.Symbol);
                        _fontAutoSet = true;
                        TypeFace = typeFace;
                        CheckGlyphsCanBeRendered();
                        return;
                    }
                }
            }
            else
            {
                ReadOnlySpan<char> glyphSpan = glyph.GetGlyphText();
                sb.Append(glyphSpan);
            }
        }

        TextFiltered = sb.ToString();
        // Return StringBuilder to pool
        SkiaLabel.ObjectPools.ReturnStringBuilder(sb);
    }



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



    public bool HasSetFont { get; set; }
    public bool HasSetSize { get; set; }
    public bool HasSetColor { get; set; }

    public SKPaint Paint { get; set; }

    public SKTypeface TypeFace
    {
        get => _typeFace;
        set
        {
            if (_typeFace != value)
            {
                _typeFace = value;
                OnPropertyChanged();
            }
        }
    }

    public bool NeedShape
    {
        get => _needShape;
        set
        {
            if (value == _needShape) return;
            _needShape = value;
            OnPropertyChanged();
        }
    }

    public bool HasDecorations
    {
        get
        {
            return (this.Underline && UnderlineWidth != 0) || this.Strikeout;
        }
    }

    private bool _Underline;
    public bool Underline
    {
        get
        {
            return _Underline;
        }
        set
        {
            if (_Underline != value)
            {
                _Underline = value;
                OnPropertyChanged();
            }
        }
    }

    private double _UnderlineWidth = -1.0;
    /// <summary>
    /// In points, if set to negative will be in pixels instead.
    /// </summary>
    public double UnderlineWidth
    {
        get
        {
            return _UnderlineWidth;
        }
        set
        {
            if (_UnderlineWidth != value)
            {
                _UnderlineWidth = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _lineThrough;
    public bool Strikeout
    {
        get
        {
            return _lineThrough;
        }
        set
        {
            if (_lineThrough != value)
            {
                _lineThrough = value;
                OnPropertyChanged();
            }
        }
    }

    private double _lineThroughWidth = 1.0;
    /// <summary>
    /// In points
    /// </summary>
    public double StrikeoutWidth
    {
        get
        {
            return _lineThroughWidth;
        }
        set
        {
            if (_lineThroughWidth != value)
            {
                _lineThroughWidth = value;
                OnPropertyChanged();
            }
        }
    }

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

    /// <summary>
    /// Will listen to gestures
    /// </summary>
    public bool HasTapHandler
    {
        get
        {
            return CommandTapped != null || Tapped != null || ForceCaptureInput;
        }
    }

    /// <summary>
    /// When no tap handler or command are set this forces to listen to taps anyway
    /// </summary>
    public bool ForceCaptureInput { get; set; }

    public virtual bool HitIsInside(float x, float y)
    {
        foreach (var rect in Rects.ToList())
        {
            if (rect.ContainsInclusive(x, y))
                return true;
        }
        return false;
    }

    public virtual void FireTap()
    {
        Tapped?.Invoke(this, new ControlTappedEventArgs(this, SkiaGesturesParameters.Empty, GestureEventProcessingInfo.Empty));
        CommandTapped?.Execute(null);
    }

    public virtual void Dispose()
    {
        Paint?.Dispose();

        CommandTapped = null;
        Tapped = null;

        Parent = null;
    }

    /// <summary>
    /// Rendering offset, set when combining spans. Ofset of the first line.
    /// </summary>
    public SKPoint DrawingOffset { get; set; }

    public string Tag { get; set; }

    /// <summary>
    /// Relative to DrawingRect
    /// </summary>
    public readonly List<SKRect> Rects = new();


    private ICommand _commandTapped;
    public ICommand CommandTapped
    {
        get
        {
            return _commandTapped;
        }
        set
        {
            if (_commandTapped != value)
            {
                _commandTapped = value;
                OnPropertyChanged();
            }
        }
    }

    public event EventHandler<ControlTappedEventArgs> Tapped;

    protected SkiaControl ParentControl
    {
        get
        {
            return Parent as SkiaControl;
        }
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

    protected bool _fontAutoSet;

    protected virtual void UpdateFont()
    {
        LineSpacing = (float)LineHeight;// * 1.2f;

        var font = SkiaFontManager.Instance.GetFont(FontFamily, FontWeight);

        _fontFamily = FontFamily;
        _fontWeight = FontWeight;

        //since we reuse fonts from cached dictionnary never dispose previous font
        TypeFace = font;

        Invalidate();
    }

    void Invalidate()
    {
        _fontAutoSet = false;
        OnPropertyChanged(null);
    }

    private string _fontFamily;
    private float _lineSpacing = 1f;
    private float _lineHeight = 1f;
    private int _fontWeight;
    private double _fontSize = 12.0;
    private string _text;
    private bool _isItalic;
    private bool _isBold;
    private Color _textColor = Colors.GreenYellow;
    private Color _backgroundColor = Colors.Transparent;
    private Color _paragraphColor = Colors.Transparent;
    private bool _autoFindFont;
    private SKTypeface _typeFace = SkiaFontManager.DefaultTypeface;
    private bool _needShape;

    public string FontFamily
    {
        get
        {
            return _fontFamily;
        }
        set
        {
            if (_fontFamily != value)
            {
                _fontFamily = value;
                OnPropertyChanged(nameof(FontFamily));
            }
        }
    }

    public float LineSpacing
    {
        get => _lineSpacing;
        set
        {
            if (value.Equals(_lineSpacing)) return;
            _lineSpacing = value;
            OnPropertyChanged(nameof(LineSpacing));
        }
    }

    public float LineHeight
    {
        get => _lineHeight;
        set
        {
            if (value.Equals(_lineHeight)) return;
            _lineHeight = value;
            OnPropertyChanged(nameof(LineHeight));
        }
    }

    public int FontWeight
    {
        get => _fontWeight;
        set
        {
            if (value == _fontWeight) return;
            _fontWeight = value;
            OnPropertyChanged(nameof(FontWeight));
        }
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

    public bool IsItalic
    {
        get => _isItalic;
        set
        {
            if (value == _isItalic) return;
            _isItalic = value;
            OnPropertyChanged(nameof(IsItalic));
        }
    }

    public bool IsBold
    {
        get => _isBold;
        set
        {
            if (value == _isBold) return;
            _isBold = value;
            OnPropertyChanged(nameof(IsBold));
        }
    }



    //public object Parent { get; set; }

    /// <summary>
    /// If any glyph cannot be rendered with selected font try find system font that supports it and switch to it for the whole span
    /// </summary>
    public bool AutoFindFont
    {
        get => _autoFindFont;
        set
        {
            if (value == _autoFindFont) return;
            _autoFindFont = value;
            OnPropertyChanged();
        }
    }

    public int FontDetectedWith { get; set; }

}
