
namespace DrawnUi.Draw;

/// <summary>
/// Left for compatibility, use SkiaRichLabel
/// </summary>
public class SkiaMarkdownLabel : SkiaRichLabel
{

}

/// <summary>
/// Will internally create spans from markdown.
/// Spans property must not be set directly.
/// </summary>
public partial class SkiaRichLabel : SkiaLabel
{
    public SkiaRichLabel()
    {
    }

    public SkiaRichLabel(string text) : base(text)
    {
    }

    #region PROPERTIES DEFAULTS

    public static Color ColorLink = Colors.CornflowerBlue;
    public static Color ColorCodeBackground = Colors.DimGray;
    public static Color ColorCodeBlock = Color.Parse("#222222");
    public static Color ColorCode = Colors.White;
    public static Color ColorStrikeout = Colors.Red;
    public static string MaskPrefixBullet = "• ";
    public static string MaskPrefixNumbered = "{0}. ";

    #endregion

    protected override void OnFontUpdated()
    {
        base.OnFontUpdated();

        OnTextChanged();
    }

    private object LockParsing = new();

    #region SPANS

    /// <summary>
    /// Creates a text span with passed data.
    /// </summary>
    /// <param name="span">Data for span creation</param>
    /// <param name="fontAttributes">Force setting attributes</param>
    /// <returns></returns>
    protected virtual TextSpan SpanWithAttributes(TextSpan span, FontAttributes fontAttributes)
    {
        span.IsBold = isBold || span.IsBold;
        span.IsItalic = isItalic || span.IsItalic;

        span.Strikeout = isStrikethrough || span.Strikeout;
        if (span.Strikeout)
            span.StrikeoutColor = this.StrikeoutColor;

        if (isHeading1)
        {
            span.IsBold = true;
            span.FontSize += 6;
        }

        if (isHeading2)
        {
            span.IsBold = true;
            span.FontSize += 3;
        }

        span.IsBold |= fontAttributes.HasFlag(FontAttributes.Bold);
        span.IsItalic |= fontAttributes.HasFlag(FontAttributes.Italic);

        return span;
    }

    protected virtual void AddTextSpan(string text, Action<TextSpan> modifySpan = null)
    {
        if (TypeFace == null) //might happen early in cycle when set via Styles
            ReplaceFont();

        if (TypeFace == null)
        {
            Super.Log("TYPEFACE NULL");
            return; //do not crash please
        }

        var originalTypeFace = TypeFace; //cannot be null 
        var currentIndex = 0;
        var spanStart = 0;
        var spanData = new List<(string Text, SKTypeface Typeface, int Symbol, bool shape)>();

        SKTypeface currentTypeFace = originalTypeFace;
        bool needShape = false;

        // First pass: Create raw span data
        while (currentIndex < text.Length)
        {
            int codePoint = char.ConvertToUtf32(text, currentIndex);
            string glyphText = char.ConvertFromUtf32(codePoint);
            bool isStandardSymbol = standardSymbols.Contains(glyphText[0]);

            void BreakSpanAndSwitchTypeface(SKTypeface newTypeface)
            {
                // When we switch typefaces, we add the span up to this point and then change the typeface
                var add = text.Substring(spanStart, currentIndex - spanStart);
                if (!needShape)
                {
                    needShape = SkiaLabel.UnicodeNeedsShaping(codePoint);
                }

                spanData.Add((add, currentTypeFace, codePoint, needShape));
                currentTypeFace = newTypeface;
                spanStart = currentIndex;
                needShape = false;
            }

            var glyph = SkiaLabel.GetGlyphs(glyphText, currentTypeFace).First();

            //switch to fallback font
            if (!isStandardSymbol && !glyph.IsAvailable)
            {
                SKTypeface newTypeFace = SkiaFontManager.Manager.MatchCharacter(codePoint);
                if (newTypeFace != null && newTypeFace != currentTypeFace)
                {
                    BreakSpanAndSwitchTypeface(newTypeFace);
                }
            }
            //maybe switch back to original font if possible
            else if (!isStandardSymbol && currentTypeFace != originalTypeFace)
            {
                var glyphInOriginal = SkiaLabel.GetGlyphs(glyphText, originalTypeFace).First();
                if (glyphInOriginal.IsAvailable)
                {
                    if (currentIndex - spanStart > 1)
                    {
                        //if we it's not the first symbol and we had some data to create a span with previous font..
                        BreakSpanAndSwitchTypeface(originalTypeFace);
                    }
                    else
                    {
                        //otherwise just switch font back to original
                        currentIndex--; //go back in time
                        currentTypeFace = originalTypeFace;
                        spanStart = currentIndex;
                        needShape = false;
                    }
                }
            }

            var advance = 1;

            if (char.IsSurrogatePair(text, currentIndex))
            {
                var sequence = EmojiData.IsEmojiModifierSequence(text, currentIndex);
                if (sequence > 0)
                {
                    needShape = true;
                    advance = sequence;
                }
                else
                    advance = 2;
            }

            currentIndex += advance;

            // When we reach the end, we add the remaining text as a span
            if (currentIndex >= text.Length)
            {
                BreakSpanAndSwitchTypeface(originalTypeFace);
            }
        }

        // Optimize spans data
        ProcessSpanData(ref spanData, originalTypeFace);

        // Create TextSpans from optimized spans data
        foreach (var (Text, Typeface, Symbol, Shape) in spanData)
        {
            var span = new TextSpan { Text = Text, TypeFace = Typeface, FontDetectedWith = Symbol, NeedShape = Shape };
            modifySpan?.Invoke(span);
            Spans.Add(SpanWithAttributes(span, this.FontAttributes));
        }
    }

    protected static HashSet<char> standardSymbols = new HashSet<char> { ' ', '\n', '\r', '\t' };

    /// <summary>
    /// Do not let spans with non-default typeface end with standart symbols like ' ', move them to span with original typecase
    /// </summary>
    /// <param name="spanData"></param>
    /// <param name="originalTypeFace"></param>
    protected virtual void ProcessSpanData(
        ref List<(string Text, SKTypeface Typeface, int Symbol, bool Shape)> spanData, SKTypeface originalTypeFace)
    {
        for (int i = 0; i < spanData.Count - 1; i++)
        {
            var currentSpan = spanData[i];
            var nextSpan = spanData[i + 1];

            if (currentSpan.Typeface != originalTypeFace && standardSymbols.Contains(currentSpan.Text.Last()))
            {
                int standardSymbolIndex = currentSpan.Text.Length - 1;
                while (standardSymbolIndex >= 0 && standardSymbols.Contains(currentSpan.Text[standardSymbolIndex]))
                {
                    standardSymbolIndex--;
                }

                standardSymbolIndex++; // Adjust to point to the first standard symbol

                if (standardSymbolIndex > 0)
                {
                    spanData[i] = (currentSpan.Text.Substring(0, standardSymbolIndex), currentSpan.Typeface,
                        currentSpan.Symbol, currentSpan.Shape);
                }
                else
                {
                    spanData.RemoveAt(i);
                    i--; // Adjust index to account for removed item
                    continue;
                }

                if (nextSpan.Typeface == originalTypeFace)
                {
                    spanData[i + 1] = (currentSpan.Text.Substring(standardSymbolIndex) + nextSpan.Text,
                        nextSpan.Typeface, nextSpan.Symbol, nextSpan.Shape);
                }
                else
                {
                    // If the next span is not using the original typeface, insert a new span with the original typeface
                    spanData.Insert(i + 1,
                        (currentSpan.Text.Substring(standardSymbolIndex), originalTypeFace, 0, false));
                }
            }
        }
    }

    #endregion

    protected bool hadParagraph;
    protected bool isBold;
    protected bool isItalic;
    protected bool isCodeBlock;
    protected bool isHeading1;
    protected bool isHeading2;
    protected bool isStrikethrough;

    #region LINKS

    /// <summary>
    /// Url will be inside Tag
    /// </summary>
    /// <param name="span"></param>
    /// <returns></returns>
    public override ISkiaGestureListener OnSpanTapped(TextSpan span)
    {
        OnLinkTapped(span.Tag, span.Text);
        return base.OnSpanTapped(span);
    }



    public static readonly BindableProperty CommandLinkTappedProperty = BindableProperty.Create(
        nameof(CommandLinkTapped), typeof(ICommand),
        typeof(SkiaRichLabel),
        null);

    public ICommand CommandLinkTapped
    {
        get { return (ICommand)GetValue(CommandLinkTappedProperty); }
        set { SetValue(CommandLinkTappedProperty, value); }
    }

    public event EventHandler<string> LinkTapped;

    public override void OnDisposing()
    {
        base.OnDisposing();

        LinkTapped = null;
    }

    public virtual void OnLinkTapped(string url, string text)
    {
        LinkTapped?.Invoke(this, url);
        CommandLinkTapped?.Execute(url);
    }

    #endregion

    #region PROPERTIES

    public static readonly BindableProperty StrikeoutColorProperty = BindableProperty.Create(
        nameof(StrikeoutColor),
        typeof(Color),
        typeof(SkiaLabel),
        ColorStrikeout,
        propertyChanged: NeedUpdateFont);

    public Color StrikeoutColor
    {
        get { return (Color)GetValue(StrikeoutColorProperty); }
        set { SetValue(StrikeoutColorProperty, value); }
    }

    public static readonly BindableProperty LinkColorProperty = BindableProperty.Create(
        nameof(LinkColor),
        typeof(Color),
        typeof(SkiaLabel),
        ColorLink,
        propertyChanged: NeedUpdateFont);

    public Color LinkColor
    {
        get { return (Color)GetValue(LinkColorProperty); }
        set { SetValue(LinkColorProperty, value); }
    }

    public static readonly BindableProperty CodeTextColorProperty = BindableProperty.Create(
        nameof(CodeTextColor),
        typeof(Color),
        typeof(SkiaLabel),
        ColorCode,
        propertyChanged: NeedUpdateFont);

    public Color CodeTextColor
    {
        get { return (Color)GetValue(CodeTextColorProperty); }
        set { SetValue(CodeTextColorProperty, value); }
    }

    public static readonly BindableProperty CodeBlockBackgroundColorProperty = BindableProperty.Create(
        nameof(CodeBlockBackgroundColor),
        typeof(Color),
        typeof(SkiaLabel),
        ColorCodeBlock,
        propertyChanged: NeedUpdateFont);

    public Color CodeBlockBackgroundColor
    {
        get { return (Color)GetValue(CodeBlockBackgroundColorProperty); }
        set { SetValue(CodeBlockBackgroundColorProperty, value); }
    }

    public static readonly BindableProperty CodeBackgroundColorProperty = BindableProperty.Create(
        nameof(CodeBackgroundColor),
        typeof(Color),
        typeof(SkiaLabel),
        ColorCodeBackground,
        propertyChanged: NeedUpdateFont);

    public Color CodeBackgroundColor
    {
        get { return (Color)GetValue(CodeBackgroundColorProperty); }
        set { SetValue(CodeBackgroundColorProperty, value); }
    }

    public static readonly BindableProperty PrefixBulletProperty = BindableProperty.Create(
        nameof(PrefixBullet),
        typeof(string),
        typeof(SkiaLabel),
        MaskPrefixBullet,
        propertyChanged: NeedUpdateFont);

    public string PrefixBullet
    {
        get { return (string)GetValue(PrefixBulletProperty); }
        set { SetValue(PrefixBulletProperty, value); }
    }

    public static readonly BindableProperty PrefixNumberedProperty = BindableProperty.Create(
        nameof(PrefixNumbered),
        typeof(string),
        typeof(SkiaLabel),
        MaskPrefixNumbered,
        propertyChanged: NeedUpdateFont);

    public string PrefixNumbered
    {
        get { return (string)GetValue(PrefixNumberedProperty); }
        set { SetValue(PrefixNumberedProperty, value); }
    }

    public static readonly BindableProperty UnderlineLinkProperty = BindableProperty.Create(
        nameof(UnderlineLink),
        typeof(bool),
        typeof(SkiaLabel),
        true,
        propertyChanged: NeedUpdateFont);

    public bool UnderlineLink
    {
        get { return (bool)GetValue(UnderlineLinkProperty); }
        set { SetValue(UnderlineLinkProperty, value); }
    }

    public static readonly BindableProperty UnderlineWidthProperty = BindableProperty.Create(
        nameof(UnderlineWidth),
        typeof(double),
        typeof(SkiaLabel),
        -1.0,
        propertyChanged: NeedUpdateFont);

    /// <summary>
    /// Used for underlining text, in points. If you set it negative it will be in PIXELS instead of points. Default is -1 (1 pixel).
    /// </summary>
    public double UnderlineWidth
    {
        get { return (double)GetValue(UnderlineWidthProperty); }
        set { SetValue(UnderlineWidthProperty, value); }
    }

    #endregion
}
