using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using static DrawnUi.Draw.SkiaControl;

#if BROWSER || DRAWNUI_NET
    using PropertyChangingEventArgs = System.ComponentModel.PropertyChangingEventArgs;
#else
    using PropertyChangingEventArgs = Microsoft.Maui.Controls.PropertyChangingEventArgs;
#endif

namespace DrawnUi.Draw;

[DebuggerDisplay("{DebugString}")]
public partial class TextSpan : IDisposable
{
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
                    var typeFace = SkiaFontManager.MatchCharacter(glyph.Symbol);
                    if (typeFace != null)
                    {
                        FontDetectedWith = glyph.Symbol;
                        NeedShape = SkiaLabel.UnicodeNeedsShaping(glyph.Symbol);
//#if BROWSER || DRAWNUI_NET
//                        if (SkiaLabel.EmojiData.IsEmoji(glyph.Symbol))
//                        {
//                            NeedShape = false;
//                        }
//#endif
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

    public bool HasSetFont { get; set; }
    public bool HasSetSize { get; set; }

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
    private bool _isItalic;
    private bool _isBold;
    private bool _autoFindFont;
    protected SKTypeface _typeFace;
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
