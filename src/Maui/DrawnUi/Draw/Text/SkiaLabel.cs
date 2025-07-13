using System.Buffers;
using System.Collections.Concurrent;
using SkiaSharp.HarfBuzz;
using Color = Microsoft.Maui.Graphics.Color;
using Font = Microsoft.Maui.Font;
using PropertyChangingEventArgs = Microsoft.Maui.Controls.PropertyChangingEventArgs;

namespace DrawnUi.Draw
{
    //todo add accesibility features

    //todo
    //public enum UseRotationDirection
    //{
    //    Horizontal,
    //    Vertical,
    //    UsePath
    //}

    /// <summary>
    /// A high-performance text rendering control that provides advanced text formatting,
    /// layout, and styling capabilities using SkiaSharp for rendering.
    /// </summary>
    /// <remarks>
    /// SkiaLabel offers rich text formatting with features including:
    /// - Multi-line text with various alignment options
    /// - Rich text styling with spans for portions of text
    /// - Text shadows and gradient effects
    /// - Font customization including weight, family, and size
    /// - Emoji rendering support
    /// - Text transformation and decoration
    /// - Line height and spacing control
    /// - Text measurement and truncation
    /// 
    /// Performance is optimized through text layout caching, glyph measurement caching,
    /// and intelligent rendering that only processes visible portions of text.
    /// </remarks>
    [ContentProperty("Spans")]
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public partial class SkiaLabel : SkiaControl, ISkiaGestureListener, IText
    {
        private string DebuggerDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Text))
                    return "[Empty]";

                return Text.Length <= 16 ? Text : Text.Substring(0, 16) + "...";
            }
        }

        #region INFRASTRUCTURE

        public override void OnDisposing()
        {
            if (_spans != null)
            {
                lock (LockSetup)
                {
                    _spans.CollectionChanged -= OnCollectionChanged;
                    foreach (var span in _spans)
                    {
                        DisposeObject(span);
                    }

                    _spans.Clear();
                }
            }

            CleanAllocations();

            base.OnDisposing();
        }

        /// <summary>
        /// TODO IText?
        /// </summary>
        public Font Font { get; }

        public static Color DebugColor = Colors.Transparent;
        //public static Color DebugColor = Color.Parse("#22ff0000");

        public static bool DebugSpans = false;

        void Constructor()
        {
            _spans.CollectionChanged += OnCollectionChanged;

            UpdateFont();
        }

        public SkiaLabel() : base()
        {
            Constructor();
        }

        public SkiaLabel(string text) : base()
        {
            Constructor();

            Text = text;
        }

        public override void Invalidate()
        {
            ResetTextCalculations(); //force recalc

            base.Invalidate();

            Update();
        }

        public override void CalculateMargins()
        {
            base.CalculateMargins();

            ResetTextCalculations();
        }


        protected override void OnLayoutReady()
        {
            base.OnLayoutReady();

            if (AutoSize != AutoSizeType.None)
                Invalidate();
        }

        public override void OnScaleChanged()
        {
            UpdateFont();
        }

        public override bool CanDraw
        {
            get
            {
                if (string.IsNullOrEmpty(Text))
                {
                    return DrawWhenEmpty && base.CanDraw;
                }

                return base.CanDraw;
            }
        }

        public override void ApplyBindingContext()
        {
            lock (LockSetup)
            {
                base.ApplyBindingContext();

                for (int i = 0; i < Spans.Count; i++)
                    SetInheritedBindingContext(Spans[i], BindingContext);
            }
        }

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            if (propertyName.IsEither(nameof(Spans)))
            {
                InvalidateMeasure();
            }
        }

        public override string ToString()
        {
            lock (LockSetup)
            {
                if (Spans.Count > 0)
                {
                    // Use pooled StringBuilder to avoid allocation
                    using var pooledSb = PooledStringBuilder.Get();
                    var sb = pooledSb.StringBuilder;
                    foreach (var span in Spans)
                    {
                        sb.Append(span.Text);
                    }

                    return sb.ToString();
                }

                return this.TextInternal;
            }
        }

        #endregion

        #region SPANS

        /// <summary>
        /// Gets the collection of text spans for rich text formatting.
        /// </summary>
        /// <remarks>
        /// Spans allow you to apply different styling to portions of text within the label.
        /// Each TextSpan can have its own:
        /// 
        /// - Text content
        /// - Font attributes (weight, family, size)
        /// - Text color
        /// - Background color
        /// - Text decorations (underline, strikethrough)
        /// - Custom styles
        /// 
        /// Spans are rendered in the order they appear in the collection.
        /// 
        /// Example XAML usage:
        /// <code>
        /// &lt;draw:SkiaLabel&gt;
        ///     &lt;draw:SkiaLabel.Spans&gt;
        ///         &lt;draw:TextSpan Text="This is " /&gt;
        ///         &lt;draw:TextSpan Text="bold" FontAttributes="Bold" TextColor="Red" /&gt;
        ///         &lt;draw:TextSpan Text=" text" /&gt;
        ///     &lt;/draw:SkiaLabel.Spans&gt;
        /// &lt;/draw:SkiaLabel&gt;
        /// </code>
        /// 
        /// When spans are used, the Text property is ignored. To reset to using
        /// the Text property, clear the Spans collection.
        /// </remarks>
        public IList<TextSpan> Spans => _spans;


        void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var newItems = e.NewItems as IEnumerable<TextSpan>;

            if (e.OldItems != null)
            {
                foreach (object item in e.OldItems)
                {
                    var bo = item as TextSpan;
                    if (bo != null)
                    {
                        bo.Parent = null;
                        bo.PropertyChanging -= OnItemPropertyChanging;
                        bo.PropertyChanged -= OnItemPropertyChanged;
                        if (newItems == null || newItems != null && !newItems.Contains(bo))
                        {
                            DisposeObject(bo);
                        }
                    }
                }
            }

            if (e.NewItems != null)
            {
                foreach (object item in e.NewItems)
                {
                    var bo = item as TextSpan;
                    if (bo != null)
                    {
                        bo.Parent = this;
                        bo.PropertyChanging += OnItemPropertyChanging;
                        bo.PropertyChanged += OnItemPropertyChanged;
                    }
                }
            }

            OnPropertyChanged(nameof(Spans));
            SpansCollectionChanged?.Invoke(sender, e);
        }

        void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(Spans));

        void OnItemPropertyChanging(object sender, PropertyChangingEventArgs e) => OnPropertyChanging(nameof(Spans));

        protected readonly SpanCollection _spans = new();

        public event NotifyCollectionChangedEventHandler SpansCollectionChanged;

        public class SpanCollection : ObservableRangeCollection<TextSpan>
        {
            protected override void InsertItem(int index, TextSpan item) =>
                base.InsertItem(index, item ?? throw new ArgumentNullException(nameof(item)));

            protected override void SetItem(int index, TextSpan item) =>
                base.SetItem(index, item ?? throw new ArgumentNullException(nameof(item)));

            protected override void ClearItems()
            {
                var removed = new List<TextSpan>(this);
                base.ClearItems();
                base.OnCollectionChanged(
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed));
            }
        }

        #endregion

        public override void OnWillDisposeWithChildren()
        {
            base.OnWillDisposeWithChildren();

            SpansCollectionChanged = null;
        }

        #region PAINT

        protected override void Paint(DrawingContext ctx)
        {
            lock (LockSetup)
            {
                base.Paint(ctx);

                var scale = ctx.Scale;
                var rectForChildren = ContractPixelsRect(ctx.Destination, scale, Padding);

                if (GliphsInvalidated)
                {
                    //remeasure inside the existing frame
                    Measure(ArrangedDestination.Width, ArrangedDestination.Height, scale);
                    ApplyMeasureResult();
                }

                if (Lines != null)
                    DrawLines(ctx.WithDestination(rectForChildren), PaintDefault, SKPoint.Empty, Lines);
            }
        }

        protected virtual void SetupDefaultPaint(float scale)
        {
            if (PaintDefault == null)
            {
                PaintDefault = new SKPaint { IsAntialias = true, IsDither = true };
            }

            PaintDefault.StrokeWidth = 0;
            //todo refactor obsolete
            PaintDefault.TextSize = (float)Math.Round(FontSize * scale);
            PaintDefault.Typeface = this.TypeFace ?? SkiaFontManager.DefaultTypeface;
            PaintDefault.FakeBoldText = (this.FontAttributes & FontAttributes.Bold) != 0;
            PaintDefault.TextSkewX = (this.FontAttributes & FontAttributes.Italic) != 0 ? -0.25f : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DrawTextInternal(SKCanvas canvas, string text, float x, float y, SKPaint paint, float scale)
        {
            DrawTextInternal(canvas, text.AsSpan(), x, y, paint, scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DrawTextInternal(SKCanvas canvas, ReadOnlySpan<char> characters, float x, float y, SKPaint paint,
            float scale)
        {
            //canvas.DrawText(text, x, y, paint);
            using var font = paint.ToFont();
            font.Edging = Super.FontSubPixelRendering ? SKFontEdging.SubpixelAntialias : SKFontEdging.Antialias;
            font.Subpixel = Super.FontSubPixelRendering;

            //todo instead of setting in skpaint upper
            //font.Embolden = (this.FontAttributes & FontAttributes.Bold) != 0;

            using (var blob = SKTextBlob.Create(characters, font))
            {
                if (blob != null)
                {
                    //canvas.DrawText(blob, x, y, paint);
                    canvas.DrawText(blob, (int)Math.Round(x), (int)Math.Round(y), paint);
                }
            }

            //canvas.DrawText(text, (int)Math.Round(x), (int)Math.Round(y), paint);
        }


        /// <summary>
        /// This is called when CharByChar is enabled
        /// You can override it to apply custom effects to every letter
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="lineIndex"></param>
        /// <param name="letterIndex"></param>
        /// <param name="characters"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="paint"></param>
        /// <param name="paintStroke"></param>
        /// <param name="paintDropShadow"></param>
        /// <param name="destination"></param>
        /// <param name="scale"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void DrawCharacter(SKCanvas canvas,
            int lineIndex, int letterIndex,
            ReadOnlySpan<char> characters, float x, float y, SKPaint paint, SKPaint paintStroke,
            SKPaint paintDropShadow, SKRect destination, float scale)
        {
            DrawText(canvas,
                x, y,
                characters,
                paint, paintStroke, paintDropShadow, scale);
        }


        public void DrawLines(
            DrawingContext ctx,
            SKPaint paintDefault,
            SKPoint startOffset,
            IEnumerable<TextLine> lines)
        {
            if (paintDefault == null || paintDefault.Color == null)
                return;

            lock (LockSetup)
            {
                SKRect rectDraw = ctx.Destination;
                double scale = ctx.Scale;

                const char SpaceChar = ' ';

                paintDefault.Color = TextColor.ToSKColor();
                paintDefault.BlendMode = this.FillBlendMode;

                var canvas = ctx.Context.Canvas;
                SKPaint paintStroke = null;

                if (StrokeColor.Alpha != 0 && StrokeWidth > 0)
                {
                    PaintStroke.TextSkewX = (this.FontAttributes & FontAttributes.Italic) != 0 ? -0.25f : 0;
                    PaintStroke.TextSize = paintDefault.TextSize * _scaleResampleText;
                    PaintStroke.Color = StrokeColor.ToSKColor();
                    PaintStroke.StrokeWidth = (float)(StrokeWidth * 2 * scale);
                    PaintStroke.IsStroke = true;
                    PaintStroke.IsAntialias = paintDefault.IsAntialias;
                    PaintStroke.Typeface = paintDefault.Typeface;

                    paintStroke = PaintStroke;
                }

                SKPaint paintDropShadow = null;

                if (DropShadowColor.Alpha != 0)
                {
                    PaintShadow.TextSkewX = (this.FontAttributes & FontAttributes.Italic) != 0 ? -0.25f : 0;
                    PaintShadow.TextSize = paintDefault.TextSize * _scaleResampleText;
                    PaintShadow.Color = DropShadowColor.ToSKColor();
                    PaintShadow.StrokeWidth = (float)(DropShadowSize * 2 * scale);
                    PaintShadow.IsStroke = true;
                    PaintShadow.IsAntialias = paintDefault.IsAntialias;
                    PaintShadow.Typeface = paintDefault.Typeface;

                    paintDropShadow = PaintShadow;
                }

                if (!GradientByLines)
                {
                    SetupGradient(paintDefault, FillGradient, rectDraw);
                    if (paintStroke != null)
                    {
                        SetupGradient(paintStroke, StrokeGradient, rectDraw);
                    }
                }

                if (DebugColor != Colors.Transparent)
                {
                    PaintDeco.Color = DebugColor.ToSKColor();
                    PaintDeco.Style = SKPaintStyle.StrokeAndFill;
                    PaintDeco.StrokeWidth = 0;
                    canvas.DrawRect(rectDraw, PaintDeco);
                }

                bool baseLineCalculated = false;
                int lineNb = 0;

                TextLine[] processLines = (lines is TextLine[] arr) ? arr : lines.ToArray();

                // Clear rectangles in spans
                for (int i = 0; i < Spans.Count; i++)
                {
                    Spans[i].Rects.Clear();
                }

                float baselineY = 0;
                float moveToBaseline = 0f;
                float useLineHeight = 0f;

                int totalLines = processLines.Length;
                for (int lineIndex = 0; lineIndex < totalLines; lineIndex++)
                {
                    var line = processLines[lineIndex];

                    if (!baseLineCalculated)
                    {
                        float PositionBaseline(float calcBaselineY)
                        {
                            float diff = (float)(rectDraw.Height - ContentSize.Pixels.Height);
                            if (VerticalTextAlignment == TextAlignment.End && diff > 0)
                            {
                                calcBaselineY += diff;
                            }
                            else if (VerticalTextAlignment == TextAlignment.Center && diff > 0)
                            {
                                calcBaselineY += diff / 2f;
                            }

                            return calcBaselineY;
                        }

                        if (!LineHeightUniform)
                        {
                            useLineHeight = line.Height;
                            moveToBaseline = useLineHeight - FontMetrics.Descent;
                            if (lineNb == 0)
                            {
                                baselineY += PositionBaseline(rectDraw.Top + moveToBaseline);
                            }
                            else
                            {
                                baselineY += PositionBaseline(moveToBaseline) + FontMetrics.Descent;
                            }
                        }
                        else
                        {
                            useLineHeight = MeasuredLineHeight;

                            var add = useLineHeight - useLineHeight / LineHeight;
                            var move = useLineHeight - add / 2.0;
                            moveToBaseline = (float)(move - FontMetrics.Descent);
                            baselineY = PositionBaseline(moveToBaseline + rectDraw.Top);
                            baseLineCalculated = true;
                        }
                    }

                    lineNb++;

                    if (line.IsNewParagraph && lineNb > 1)
                    {
                        baselineY += (float)SpaceBetweenParagraphs;
                    }

                    float alignedLineDrawingStartX = rectDraw.Left;
                    if (lineNb == 1)
                    {
                        alignedLineDrawingStartX += startOffset.X;
                    }

                    float enlargeSpaceCharacter = 0.0f;
                    float fillCharactersOffset = 0.0f;

                    if (HorizontalTextAlignment == DrawTextAlignment.Center)
                    {
                        alignedLineDrawingStartX += (rectDraw.Width - line.Width) / 2.0f;
                    }
                    else if (HorizontalTextAlignment == DrawTextAlignment.End)
                    {
                        alignedLineDrawingStartX += rectDraw.Width - line.Width;
                    }
                    else if ((HorizontalTextAlignment == DrawTextAlignment.FillWords
                              || HorizontalTextAlignment == DrawTextAlignment.FillCharacters) && !line.IsLastInParagraph
                             || HorizontalTextAlignment == DrawTextAlignment.FillWordsFull
                             || HorizontalTextAlignment == DrawTextAlignment.FillCharactersFull)
                    {
                        float emptySpace = rectDraw.Width - line.Width;
                        if (lineNb == 1)
                        {
                            emptySpace = rectDraw.Width - (line.Width + startOffset.X);
                        }

                        if (emptySpace > 0)
                        {
                            if (HorizontalTextAlignment == DrawTextAlignment.FillWords
                                || HorizontalTextAlignment == DrawTextAlignment.FillWordsFull)
                            {
                                var valSpan = line.Value.AsSpan();
                                int spaceCount = 0;
                                for (int si = 0; si < valSpan.Length; si++)
                                {
                                    if (valSpan[si] == SpaceChar) spaceCount++;
                                }

                                if (spaceCount > 0)
                                {
                                    enlargeSpaceCharacter = emptySpace / spaceCount;
                                }
                            }
                            else if (HorizontalTextAlignment == DrawTextAlignment.FillCharacters
                                     || HorizontalTextAlignment == DrawTextAlignment.FillCharactersFull)
                            {
                                if (line.Value.Length > 1)
                                {
                                    fillCharactersOffset = emptySpace / (line.Value.Length - 1);
                                }
                            }
                        }
                    }

                    if (alignedLineDrawingStartX < rectDraw.Left)
                        alignedLineDrawingStartX = rectDraw.Left;

                    line.Bounds = new SKRect(
                        alignedLineDrawingStartX,
                        baselineY - moveToBaseline,
                        alignedLineDrawingStartX + line.Width,
                        baselineY - moveToBaseline + useLineHeight);

                    if (GradientByLines)
                    {
                        SetupGradient(paintDefault, FillGradient, line.Bounds);
                        if (paintStroke != null)
                        {
                            SetupGradient(paintStroke, StrokeGradient, line.Bounds);
                        }
                    }

                    float offsetX = 0;
                    int spanCount = line.Spans.Count;

                    for (int spanIndex = 0; spanIndex < spanCount; spanIndex++)
                    {
                        var lineSpan = line.Spans[spanIndex];
                        var paint = paintDefault;
                        SKRect rectPrecalculatedSpanBounds = SKRect.Empty;

                        if (lineSpan.Span != null)
                        {
                            paint = lineSpan.Span.SetupPaint(scale, paintDefault);

                            //first span can initiate painting line background
                            if (spanIndex == 0 && lineSpan.Span.ParagraphColor != Colors.Transparent)
                            {
                                rectPrecalculatedSpanBounds = new SKRect(
                                    alignedLineDrawingStartX,
                                    line.Bounds.Top,
                                    alignedLineDrawingStartX + rectDraw.Width,
                                    line.Bounds.Bottom + (float)SpaceBetweenParagraphs);

                                PaintDeco.Color = lineSpan.Span.ParagraphColor.ToSKColor();
                                PaintDeco.Style = SKPaintStyle.StrokeAndFill;
                                canvas.DrawRect(rectPrecalculatedSpanBounds, PaintDeco);
                            }
                        }

                        float offsetAdjustmentX = 0.0f;

                        if (lineSpan.Span is IDrawnTextSpan drawn)
                        {
                            float drawnX = (float)Math.Round(alignedLineDrawingStartX + offsetX);
                            float drawnY;

                            if (drawn.VerticalAlignement == DrawImageAlignment.Center)
                            {
                                drawnY = (float)Math.Round(line.Bounds.Bottom - lineSpan.Size.Height
                                                                              - (line.Bounds.Height -
                                                                                  lineSpan.Size.Height) / 2f);
                            }
                            else if (drawn.VerticalAlignement == DrawImageAlignment.End)
                            {
                                drawnY = (float)Math.Round(line.Bounds.Bottom - lineSpan.Size.Height);
                            }
                            else
                            {
                                drawnY = (float)Math.Round(line.Bounds.Top);
                            }

                            SKRect drawnDestination = new SKRect(drawnX, drawnY, drawnX + lineSpan.Size.Width,
                                line.Bounds.Bottom);
                            drawn.Render(ctx.WithDestination(drawnDestination));
                        }
                        else if (lineSpan.NeedsShaping)
                        {
                            DrawShapedText(canvas,
                                lineSpan.Text,
                                (float)Math.Round(alignedLineDrawingStartX + offsetX),
                                (float)Math.Round(baselineY),
                                paint);
                        }
                        else if (lineSpan.Glyphs != null)
                        {
                            var glyphs = lineSpan.Glyphs;
                            int glyphCount = glyphs.Length;

                            // Declare charIndex before the local function
                            int charIndex = 0;

                            float MoveOffsetAdjustmentX(float x, ReadOnlySpan<char> p)
                            {
                                if (p.Length == 1)
                                {
                                    // Adjust only if not first char and we have fillCharactersOffset
                                    if (enlargeSpaceCharacter > 0 && p[0] == SpaceChar)
                                    {
                                        x += enlargeSpaceCharacter;
                                    }
                                    else if (fillCharactersOffset > 0 && charIndex > 0)
                                    {
                                        x += fillCharactersOffset;
                                    }
                                }

                                return x;
                            }

                            // If background color is set, precompute final width
                            if (lineSpan.Span != null && lineSpan.Span.BackgroundColor != Colors.Transparent)
                            {
                                float x = offsetAdjustmentX;
                                for (charIndex = 0; charIndex < glyphCount; charIndex++)
                                {
                                    x = MoveOffsetAdjustmentX(x, glyphs[charIndex].GetGlyphText());
                                }

                                // Reset charIndex after precomputation
                                charIndex = 0;

                                rectPrecalculatedSpanBounds = new SKRect(
                                    alignedLineDrawingStartX + offsetX,
                                    line.Bounds.Top,
                                    alignedLineDrawingStartX + offsetX + lineSpan.Size.Width + x,
                                    line.Bounds.Top + lineSpan.Size.Height);

                                PaintDeco.Color = lineSpan.Span.BackgroundColor.ToSKColor();
                                PaintDeco.Style = SKPaintStyle.StrokeAndFill;
                                canvas.DrawRect(rectPrecalculatedSpanBounds, PaintDeco);
                            }

                            // Now draw each glyph
                            charIndex = 0;
                            for (; charIndex < glyphCount; charIndex++)
                            {
                                var glyph = glyphs[charIndex];
                                offsetAdjustmentX = MoveOffsetAdjustmentX(offsetAdjustmentX, glyph.GetGlyphText());

                                float posX = alignedLineDrawingStartX + offsetX + glyph.Position + offsetAdjustmentX;

                                DrawCharacter(canvas,
                                    lineNb - 1,
                                    charIndex,
                                    glyph.GetGlyphText(),
                                    posX,
                                    baselineY,
                                    paint,
                                    paintStroke,
                                    paintDropShadow,
                                    line.Bounds,
                                    (float)scale);
                            }
                        }
                        else
                        {
                            DrawText(canvas,
                                alignedLineDrawingStartX + offsetX,
                                baselineY,
                                line.Value,
                                paintDefault,
                                paintStroke,
                                paintDropShadow,
                                (float)scale);
                        }

                        offsetX += lineSpan.Size.Width + offsetAdjustmentX;

                        if (lineSpan.Span != null)
                        {
                            var lineSpanRect = new SKRect(
                                alignedLineDrawingStartX + offsetX - (lineSpan.Size.Width + offsetAdjustmentX),
                                line.Bounds.Top,
                                alignedLineDrawingStartX + offsetX,
                                line.Bounds.Top + lineSpan.Size.Height);

                            lineSpan.Span.Rects.Add(lineSpanRect);
                            SpanPostDraw(canvas, lineSpan.Span, lineSpanRect, baselineY);
                        }
                    }

                    if (MaxLines > 0 && lineNb == MaxLines)
                    {
                        break;
                    }

                    if (LineHeightUniform)
                        baselineY += (float)(useLineHeight + GetSpaceBetweenLines(useLineHeight));
                    else
                        baselineY += (float)GetSpaceBetweenLines(useLineHeight);
                }
            }
        }

        /// <summary>
        /// If strokePaint==null will not stroke
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="text"></param>
        /// <param name="textPaint"></param>
        /// <param name="strokePaint"></param>
        /// <param name="scale"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawText(SKCanvas canvas, float x, float y, string text,
            SKPaint textPaint,
            SKPaint strokePaint,
            SKPaint paintDropShadow,
            float scale)
        {
            DrawText(canvas, x, y, text.AsSpan(), textPaint, strokePaint, paintDropShadow, scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawText(SKCanvas canvas, float x, float y,
            ReadOnlySpan<char> characters,
            SKPaint textPaint,
            SKPaint strokePaint,
            SKPaint paintDropShadow,
            float scale)
        {
            if (paintDropShadow != null)
            {
                var offsetX = (int)(scale * DropShadowOffsetX);
                var offsetY = (int)(scale * DropShadowOffsetY);
                DrawTextInternal(canvas, characters, x + offsetX, y + offsetY, paintDropShadow, scale);
            }

            if (strokePaint != null)
            {
                DrawTextInternal(canvas, characters, x, y, strokePaint, scale);
            }

            DrawTextInternal(canvas, characters, x, y, textPaint, scale);
        }

        protected virtual void SpanPostDraw(
            SKCanvas canvas,
            TextSpan span,
            SKRect rect,
            float textY)
        {
            if (span.HasDecorations)
            {
                DrawSpanDecorations(canvas,
                    span,
                    rect.Left,
                    rect.Right,
                    textY);
            }

            if (DebugSpans)
            {
                PaintDeco.StrokeWidth = 0;
                PaintDeco.Color = GetRandomColor().WithAlpha(0.5f).ToSKColor();
                PaintDeco.Style = SKPaintStyle.StrokeAndFill;
                canvas.DrawRect(rect, PaintDeco);
            }
        }

        protected void DrawSpanDecorations(
            SKCanvas canvas,
            TextSpan span,
            float xStart, float xEnd, float y)
        {
            PaintDeco.Style = SkiaSharp.SKPaintStyle.Stroke;
            PaintDeco.Color = span.TextColor.ToSKColor();
            if (span.Underline)
            {
                var moveY = span.Paint.FontMetrics.UnderlinePosition.GetValueOrDefault();
                if (moveY == 0)
                {
                    moveY = span.RenderingScale;
                }

                var yLevel = (float)Math.Round(y + moveY);

                float lineWidth = span.UnderlineWidth > 0
                    ? (float)(span.UnderlineWidth * span.RenderingScale)
                    : (float)(-span.UnderlineWidth);

                PaintDeco.StrokeWidth = lineWidth;

                canvas.DrawLine(xStart, yLevel, xEnd, yLevel, PaintDeco);
            }

            if (span.Strikeout)
            {
                var moveY = span.Paint.FontMetrics.StrikeoutPosition.GetValueOrDefault();
                if (moveY == 0)
                {
                    moveY = -span.Paint.FontMetrics.XHeight / 2f;
                }

                var yLevel = (float)Math.Round(y + moveY);
                PaintDeco.StrokeWidth = (float)(span.StrikeoutWidth * span.RenderingScale);
                PaintDeco.Color = span.StrikeoutColor.ToSKColor();
                canvas.DrawLine(xStart, yLevel, xEnd, yLevel, PaintDeco);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DrawShapedText(
            SKCanvas canvas,
            string text,
            float x,
            float y,
            SKPaint paint)
        {
            if (string.IsNullOrEmpty(text) || Shaper == null || paint == null || paint.Typeface == null)
                return;

            SetupShaper(paint.Typeface);
            DrawShapedText(canvas, Shaper, text, x, y, paint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DrawShapedText(
            SKCanvas canvas,
            SKShaper shaper,
            string text,
            float x,
            float y,
            SKPaint paint)
        {
            if (string.IsNullOrEmpty(text) || shaper == null || paint == null || paint.Typeface == null)
                return;

            using var font = paint.ToFont();
            font.Edging = Super.FontSubPixelRendering ? SKFontEdging.SubpixelAntialias : SKFontEdging.Antialias;
            font.Subpixel = Super.FontSubPixelRendering;
            font.Typeface = shaper.Typeface;

            SKShaper.Result result = shaper.Shape(text, x, y, paint);
            using (SKTextBlobBuilder skTextBlobBuilder = new SKTextBlobBuilder())
            {
                SKPositionedRunBuffer positionedRunBuffer =
                    skTextBlobBuilder.AllocatePositionedRun(font, result.Codepoints.Length);
                Span<ushort> glyphSpan = positionedRunBuffer.GetGlyphSpan();
                Span<SKPoint> positionSpan = positionedRunBuffer.GetPositionSpan();
                for (int index = 0; index < result.Codepoints.Length; ++index)
                {
                    glyphSpan[index] = (ushort)result.Codepoints[index];
                    positionSpan[index] = result.Points[index];
                }

                using (SKTextBlob blob = skTextBlobBuilder.Build())
                {
                    if (blob != null)
                        canvas.DrawText(blob, 0, 0, paint);
                }
            }
        }


        /// <summary>
        /// Overriding this to be able to control either control background is drawn (when background color is set) or gradient can be drawn over text 
        /// </summary>
        /// <param name="paint"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        protected override bool SetupBackgroundPaint(SKPaint paint, SKRect destination)
        {
            if (paint == null)
                return false;

            var color = this.BackgroundColor;
            var gradient = FillGradient;

            if (Background != null)
            {
                if (Background is SolidColorBrush solid)
                {
                    if (solid.Color != null)
                        color = solid.Color;
                }
                else if (Background is GradientBrush gradientBrush)
                {
                    gradient = SkiaGradient.FromBrush(gradientBrush);
                    if (color == null)
                        color = Colors.Black;
                }
            }
            else
            {
                if (BackgroundColor != null)
                {
                    color = BackgroundColor;
                }
            }

            //if (gradient != null && color == null)
            //{
            //    color = Colors.Black;
            //}

            if (color == null || color.Alpha <= 0) return false;

            paint.Color = color.ToSKColor();
            paint.Style = SKPaintStyle.StrokeAndFill;
            paint.BlendMode = this.FillBlendMode;

            SetupGradient(paint, gradient, destination);

            return true;
        }

        #endregion

        #region MEASURE

        public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            lock (LockSetup)
            {
                // If measuring in a background context, or control can't draw, or constraints are invalid, return cached
                if (IsMeasuring || !CanDraw || widthConstraint < 0 || heightConstraint < 0)
                {
                    return MeasuredSize;
                }

                IsMeasuring = true;

                try
                {
                    if (NeedSetText)
                    {
                        NeedSetText = false;
                        SetTextInternal();
                    }

                    InitializeDefaultContent();

                    var request = CreateMeasureRequest(widthConstraint, heightConstraint, scale);
                    if (request.IsSame)
                    {
                        return MeasuredSize;
                    }

                    ReplaceFont();
                    if (TypeFace == null)
                        return MeasuredSize; // Unexpected  

                    SetupDefaultPaint(scale);
                    var constraints = GetMeasuringConstraints(request);

                    float textWidthPixels = 0f;
                    float textHeightPixels = 0f;

                    UpdateFontMetrics(PaintDefault);

                    if (Spans.Count == 0)
                    {
                        bool needsShaping = false;
                        string text = null;

                        if (GliphsInvalidated)
                        {
                            Glyphs = GetGlyphs(TextInternal, PaintDefault.Typeface);
                        }

                        if (AutoFont && Glyphs != null && Glyphs.Count > 0)
                        {
                            var first = Glyphs[0].Symbol;
                            var matchedFace = SkiaFontManager.Manager.MatchCharacter(first);
                            if (matchedFace != null)
                            {
                                needsShaping = SkiaLabel.UnicodeNeedsShaping(first);
                                TypeFace = matchedFace;
                                PaintDefault.Typeface = matchedFace;
                            }

                            text = TextInternal;
                        }
                        else if (Glyphs.Count > 0)
                        {
                            // Replace unprintable symbols with fallback using pooled StringBuilder
                            using var pooledSb = PooledStringBuilder.Get();
                            var sb = pooledSb.StringBuilder;
                            sb.EnsureCapacity(Glyphs.Count); // Pre-allocate capacity
                            for (int i = 0; i < Glyphs.Count; i++)
                            {
                                if (Glyphs[i].IsAvailable)
                                {
                                    SpanMeasurement.AppendSpan(sb, Glyphs[i].GetGlyphText());
                                }
                                else
                                {
                                    SpanMeasurement.AppendChar(sb, FallbackCharacter);
                                }
                            }

                            text = sb.ToString();
                        }
                        else
                        {
                            text = TextInternal;
                        }

                        Lines = SplitLines(text,
                            PaintDefault,
                            SKPoint.Empty,
                            (float)constraints.Content.Width,
                            (float)constraints.Content.Height,
                            MaxLines,
                            needsShaping,
                            null, scale);
                    }
                    else
                    {
                        // Measure multiple spans
                        // Use pooled list to avoid allocation
                        using var pooledMergedLines = PooledTextLineList.Get();
                        var mergedLines = pooledMergedLines.List;
                        SKPoint offset = SKPoint.Empty;
                        TextLine previousSpanLastLine = null;

                        // Instead of Spans.ToList(), iterate directly:
                        for (int i = 0; i < Spans.Count; i++)
                        {
                            var span = Spans[i];
                            if (string.IsNullOrEmpty(span.Text))
                                continue;

                            span.DrawingOffset = offset;
                            var paint = span.SetupPaint(scale, PaintDefault);

                            if (!(span is IDrawnTextSpan))
                            {
                                // Only check glyph rendering if not drawn (since drawn might not need shaping)
                                span.CheckGlyphsCanBeRendered();
                            }

                            var lines = SplitLines(span.TextFiltered,
                                paint,
                                offset,
                                constraints.Content.Width,
                                constraints.Content.Height,
                                MaxLines,
                                span.NeedShape,
                                span, scale);

                            if (lines != null && lines.Length > 0)
                            {
                                // Instead of lines.First()/Last(), access directly:
                                var firstLine = lines[0];
                                var lastLine = lines[lines.Length - 1];

                                // merge first one
                                if (previousSpanLastLine != null && mergedLines.Count > 0)
                                {
                                    // Remove last line from merged and merge with firstLine
                                    var lastIndex = mergedLines.Count - 1;
                                    if (mergedLines[lastIndex] == previousSpanLastLine)
                                    {
                                        mergedLines.RemoveAt(lastIndex);
                                    }

                                    MergeSpansForLines(span, firstLine, previousSpanLastLine);
                                }

                                previousSpanLastLine = lastLine;
                                offset = new SKPoint(lastLine.Width, 0);

                                // Add all lines from current span
                                mergedLines.AddRange(lines);
                            }
                            else
                            {
                                previousSpanLastLine = null;
                                offset = SKPoint.Empty;
                            }
                        }

                        // Last sanity pass if we don't keep spaces on line breaks
                        if (!KeepSpacesOnLineBreaks && Spans.Count > 0)
                        {
                            // Avoid LINQ .Count(), use Count property
                            int totalLines = mergedLines.Count;
                            for (int i = 0; i < totalLines - 1; i++) // do not process last line
                            {
                                var line = mergedLines[i];
                                if (line.Value.Length > 0 && line.Value[line.Value.Length - 1] == SpaceChar)
                                {
                                    var span = (line.Spans.Count > 0)
                                        ? line.Spans[line.Spans.Count - 1]
                                        : LineSpan.Default;
                                    if (span.Text != null)
                                    {
                                        // remove last character
                                        span.Text = span.Text.Substring(0, span.Text.Length - 1);
                                        line.Value = line.Value.Substring(0, line.Value.Length - 1);

                                        if (span.Glyphs != null && span.Glyphs.Length > 0)
                                        {
                                            var newArray = span.Glyphs;
                                            if (line.Value.Length > 0)
                                            {
                                                // kill last glyph
                                                float removedPos = span.Glyphs[^1].Position;
                                                line.Width -= (span.Size.Width - removedPos);
                                                Array.Resize(ref newArray, newArray.Length - 1);
                                            }

                                            span.Glyphs = newArray;
                                        }
                                    }
                                }
                            }
                        }

                        if (MaxLines > 0)
                        {
                            Lines = mergedLines.Take(MaxLines).ToArray();
                        }
                        else
                        {
                            Lines = mergedLines.ToArray();
                        }
                    }

                    GliphsInvalidated = false;

                    if (Lines != null && Lines.Length > 0)
                    {
                        LinesCount = Lines.Length;

                        // Instead of multiple LINQ calls, do one pass:
                        int paragraphCount = 0;
                        float maxLineWidth = 0f;
                        float maxLineHeight = 0f;

                        for (int i = 0; i < Lines.Length; i++)
                        {
                            var line = Lines[i];
                            if (line.IsNewParagraph)
                                paragraphCount++;

                            if (line.Width > maxLineWidth)
                                maxLineWidth = line.Width;

                            if (line.Height > maxLineHeight)
                                maxLineHeight = line.Height;
                        }

                        int addParagraphSpacingsCount = paragraphCount - 1;
                        var addParagraphSpacings = addParagraphSpacingsCount * SpaceBetweenParagraphs;

                        textWidthPixels = maxLineWidth;

                        // Ensure LineHeightPixels is the minimum line height
                        if (LineHeightUniform)
                        {
                            float usedLineHeight =
                                (LineHeightPixels > maxLineHeight) ? LineHeightPixels : maxLineHeight;
                            MeasuredLineHeight = usedLineHeight;
                            textHeightPixels = (float)(usedLineHeight * LinesCount
                                                       + (LinesCount - 1) * GetSpaceBetweenLines(usedLineHeight) +
                                                       addParagraphSpacings);
                        }
                        else
                        {
                            MeasuredLineHeight = LineHeightPixels;
                            textHeightPixels = 0f;
                            for (int i = 0; i < LinesCount; i++)
                            {
                                var lineHeight = Lines[i].Height;
                                if (LineHeightPixels > lineHeight)
                                    lineHeight = LineHeightPixels;

                                textHeightPixels += (float)(lineHeight + addParagraphSpacings);
                                if (i < LinesCount - 1)
                                    textHeightPixels += (float)GetSpaceBetweenLines(lineHeight);
                            }
                        }

                        ContentSize = ScaledSize.FromPixels(textWidthPixels, textHeightPixels, scale);
                    }
                    else
                    {
                        // No lines
                        ContentSize = ScaledSize.CreateEmpty(scale);
                        LinesCount = 0;
                    }

                    return SetMeasuredAdaptToContentSize(constraints, scale);
                }
                finally
                {
                    IsMeasuring = false;
                    NeedMeasure = false;
                }
            }
        }

        protected float MeasurePartialTextWidth(SKPaint paint, ReadOnlySpan<char> textSpan,
            bool needsShaping, float scale)
        {
            // Use optimized span-based measurement to avoid string allocation
            return MeasurePartialTextWidthOptimized(paint, textSpan, needsShaping, scale);
        }

        /// <summary>
        /// Optimized version of MeasurePartialTextWidth that uses span-based operations
        /// </summary>
        public float MeasurePartialTextWidthOptimized(SKPaint paint, ReadOnlySpan<char> textSpan,
            bool needsShaping, float scale)
        {
            var paintTypeface = paint.Typeface ?? SkiaFontManager.DefaultTypeface;

            // For simple cases, measure directly with span
            if (!needsShaping && textSpan.Length <= 32) // Small text threshold
            {
                return SpanMeasurement.MeasureTextWidthWithAdvanceSpan(paint, textSpan);
            }

            // For complex cases or cache lookup, we need string conversion
            // This is the controlled fallback to maintain cache compatibility
            string text = SpanMeasurement.SpanToStringForCache(textSpan);

            // Check cache first
            if (GlyphMeasurementCache.TryGetValue(paintTypeface, needsShaping, text, out var cachedResult))
            {
                return cachedResult.Width;
            }

            // Measure using instance method and cache result
            var (width, _) = MeasureLineGlyphs(paint, text, needsShaping, scale);
            return width;
        }

        /// <summary>
        /// Optimized version of LastNonSpaceIndex that works with spans
        /// </summary>
        public static int LastNonSpaceIndexOptimized(ReadOnlySpan<char> textSpan)
        {
            return SpanMeasurement.LastNonSpaceIndexSpan(textSpan);
        }

        protected (float Width, LineGlyph[] Glyphs) MeasureLineGlyphs(SKPaint paint, string text, bool needsShaping,
            float scale)
        {
            if (string.IsNullOrEmpty(text))
                return (0.0f, null);

            var paintTypeface = paint.Typeface ?? SkiaFontManager.DefaultTypeface;

            if (GlyphMeasurementCache.TryGetValue(paintTypeface, needsShaping, text, out var cachedResult))
            {
                return cachedResult;
            }

            var glyphs = GetGlyphs(text, paintTypeface);

            // Use pooled list to avoid allocation
            using var pooledPositions = PooledLineGlyphList.Get();
            var positions = pooledPositions.List;
            float value = 0.0f;
            float offsetX = 0f;

            if (needsShaping)
            {
                SetupShaper(paintTypeface);
                var result = GetShapedText(Shaper, text, 0, 0, paint);
                if (result == null)
                {
                    GlyphMeasurementCache.Add(paintTypeface, needsShaping, text, 0f, null);
                    return (0.0f, null);
                }

                var measured = GetResultSize(result);

                GlyphMeasurementCache.Add(paintTypeface, needsShaping, text, measured.Width, null);
                return (measured.Width, null);
            }

            if (charMonoWidthPixels > 0)
            {
                foreach (var g in glyphs)
                {
                    var print = g.GetGlyphText();
                    var mono = g.IsNumber();
                    var thisWidth = MeasureTextWidthWithAdvance(paint, print);
                    var centerOffset = 0f;

                    if (mono)
                    {
                        centerOffset = (charMonoWidthPixels - thisWidth) / 2.0f;
                    }

                    var valueOffset = offsetX + centerOffset;
                    positions.Add(LineGlyph.FromGlyph(g, valueOffset, thisWidth));

                    if (mono)
                    {
                        offsetX += charMonoWidthPixels;
                        value += charMonoWidthPixels;
                    }
                    else
                    {
                        offsetX += thisWidth;
                        value += thisWidth;
                    }
                }

                var arr = positions.ToArray();
                GlyphMeasurementCache.Add(paintTypeface, needsShaping, text, value, arr);
                return (value, arr);
            }

            // Check if we need character spacing or alignment adjustments
            bool requiresComplexMeasuring =
                Spans.Count > 0 ||
                CharacterSpacing != 1f ||
                HorizontalTextAlignment == DrawTextAlignment.FillWordsFull ||
                HorizontalTextAlignment == DrawTextAlignment.FillCharactersFull ||
                HorizontalTextAlignment == DrawTextAlignment.FillWords ||
                HorizontalTextAlignment == DrawTextAlignment.FillCharacters;

            if (requiresComplexMeasuring)
            {
                var spacingModifier = (float)(scale * (CharacterSpacing - 1));
                var pos = 0;
                var addAtIndex = -1;

                if (paint.TextSkewX != 0)
                {
                    addAtIndex = LastNonSpaceIndexOptimized(text.AsSpan());
                }

                foreach (var g in glyphs)
                {
                    var thisWidth = SpanMeasurement.MeasureTextWidthWithAdvanceSpan(paint, g.GetGlyphText());
                    if (pos == addAtIndex)
                    {
                        var additionalWidth = (int)Math.Round(Math.Abs(paint.TextSkewX) * paint.TextSize / 2f);
                        thisWidth += additionalWidth;
                    }

                    positions.Add(LineGlyph.FromGlyph(g, offsetX, thisWidth));
                    offsetX += thisWidth + spacingModifier;
                    value += thisWidth + spacingModifier;
                    pos++;
                }

                var finalWidth = value - spacingModifier;
                var arr2 = positions.ToArray();
                GlyphMeasurementCache.Add(paintTypeface, needsShaping, text, finalWidth, arr2);
                return (finalWidth, arr2);
            }

            var simpleValue = MeasureTextWidthWithAdvance(paint, text);
            if (paint.TextSkewX != 0)
            {
                float additionalWidth = Math.Abs(paint.TextSkewX) * paint.TextSize;
                simpleValue += additionalWidth;
            }

            GlyphMeasurementCache.Add(paintTypeface, needsShaping, text, simpleValue, null);
            return (simpleValue, null);
        }

        protected DecomposedText DecomposeText(string text, SKPaint paint,
            SKPoint firstLineOffset,
            float maxWidth,
            float maxHeight, //-1
            int maxLines, //-1
            bool needsShaping,
            TextSpan span, float scale)
        {
            var ret = new DecomposedText();
            // Use pooled list to avoid allocation
            using var pooledResult = PooledTextLineList.Get();
            var result = pooledResult.List;

            if (span != null)
            {
                needsShaping = span.NeedShape;

                if (span is IDrawnTextSpan drawn)
                {
                    var drawnMeasured = drawn.Measure(maxWidth, maxHeight, scale);

                    //todo check we fit
                    var fitWidth = maxWidth - firstLineOffset.X;
                    if (drawnMeasured.Pixels.Width > fitWidth)
                    {
                        AddEmptyLine(result, span, drawnMeasured.Pixels.Height,
                            MeasuredLineHeight,
                            firstLineOffset.X == 0, needsShaping);
                    }

                    result.Add(new TextLine()
                    {
                        Width = drawnMeasured.Pixels.Width,
                        Height = drawnMeasured.Pixels.Height,
                        Value = span.TextFiltered,
                        Spans = new()
                        {
                            new LineSpan()
                            {
                                NeedsShaping = needsShaping,
                                Glyphs = Array.Empty<LineGlyph>(),
                                Text = span.TextFiltered,
                                Span = span,
                                Size = drawnMeasured.Pixels
                            }
                        }
                    });

                    ret.Lines = result.ToArray();
                    return ret;
                }
            }

            bool isCut = false;
            float totalHeight = 0;
            var countLines = 0;

            float lineMaxHeight = 0f;

            bool offsetFirstLine = false;
            var limitWidth = maxWidth;

            var paragraphs = text.Split('\n');
            ret.CountParagraphs = paragraphs.Length;

            foreach (var line in paragraphs)
            {
                bool isNewParagraph = firstLineOffset.X == 0; //really first

                countLines++;

                if (!offsetFirstLine)
                {
                    offsetFirstLine = true;
                    limitWidth = maxWidth - firstLineOffset.X;
                }
                else
                {
                    limitWidth = maxWidth;
                }

                if (maxLines > -1 && countLines > maxLines)
                {
                    isCut = true;
                    break;
                }

                var lineIndex = 0;
                var lineResult = "";
                float width = 0;
                var space = ' ';
                bool spanPostponed = false;

                Stack<string> stackWords = new Stack<string>(SplitLineToWords(line, space));

                //returns true if need stop processing: was last allowed line
                bool AddLine(string adding, string full = null)
                {
                    bool assingnIsNewParagraph = isNewParagraph;

                    isNewParagraph = false; //have to set again to true upstairs

                    bool retAdd = true;
                    var wasLastChunk = false;

                    totalHeight += (float)LineHeightWithSpacing;
                    limitWidth = maxWidth; //reset the first line offset

                    if ((maxHeight > -1 && maxHeight < totalHeight + LineHeightWithSpacing)
                        || (maxLines > -1 && maxLines == result.Count + 1))
                    {
                        wasLastChunk = true;
                        retAdd = false;
                    }

                    if (!string.IsNullOrEmpty(adding))
                    {
                        if (wasLastChunk)
                        {
                            if (!string.IsNullOrEmpty(full)) //we didn't fit
                            {
                                if (LineBreakMode == LineBreakMode.TailTruncation)
                                {
                                    var maybeTrail = full + Trail;
                                    var limitText = CutLineToFit(paint, maybeTrail, limitWidth);
                                    if (limitText.Limit > 0)
                                    {
                                        adding = maybeTrail.Left(limitText.Limit).TrimEnd() + Trail;
                                        width = limitText.Width;
                                    }
                                    else
                                    {
                                        adding = maybeTrail;
                                        width = limitText.Width;
                                    }
                                }

                                isCut = true;
                            }
                        }

                        var smartMeasure = MeasureLineGlyphs(paint, adding, needsShaping, scale);

                        var widthBlock = (float)Math.Round(smartMeasure.Width);
                        var heightBlock = LineHeightPixels;

                        var chunk = new LineSpan()
                        {
                            NeedsShaping = needsShaping,
                            Glyphs = smartMeasure.Glyphs,
                            Text = adding.Replace("\n", ""),
                            Span = span,
                            Size = new(widthBlock, heightBlock)
                        };

                        var addLine = new TextLine()
                        {
                            Value = adding,
                            IsNewParagraph = assingnIsNewParagraph,
                            Width = widthBlock,
                            Height = heightBlock,
                            Spans = new() { chunk }
                        };

                        if (result.Count > 0)
                        {
                            result[^1].IsLastInParagraph = addLine.IsNewParagraph;
                        }

                        if (addLine.Height > lineMaxHeight)
                            lineMaxHeight = addLine.Height;
                        result.Add(addLine);

                        if (assingnIsNewParagraph && result.Count > 1)
                        {
                            totalHeight += (float)SpaceBetweenParagraphs;
                        }

                        width = 0;
                        lineResult = "";
                    }

                    return retAdd;
                }

                void PostponeToNextLine(string text)
                {
                    stackWords.Push(text);
                    lineResult = "";
                    width = 0;
                }

                void AddEmptyLineInternal()
                {
                    totalHeight = AddEmptyLine(result, span, totalHeight, MeasuredLineHeight,
                        isNewParagraph, needsShaping);

                    if (MeasuredLineHeight > lineMaxHeight)
                        lineMaxHeight = MeasuredLineHeight;

                    isNewParagraph = false;
                    width = 0;
                    lineResult = "";
                    limitWidth = maxWidth;
                }

                while (stackWords.Count > 0)
                {
                    var word = stackWords.Pop();

                    if (KeepSpacesOnLineBreaks && lineIndex > 0)
                    {
                        word += space;
                    }

                    lineIndex++;

                    var textLine = word;

                    bool severalWords = false;
                    if (width > 0) //got some text from previous pass
                    {
                        if (lineResult.Right(1) == Splitter || word.Left() == Splitter)
                        {
                            textLine = lineResult + word;
                        }
                        else
                        {
                            textLine = lineResult + space + word;
                        }

                        severalWords = true;
                    }

                    var textWidth = MeasureLineGlyphs(paint, textLine, needsShaping, scale).Width;

                    //apply

                    width = textWidth;

                    if (width - limitWidth > 1)
                    {
                        //the whole word is bigger than width,

                        //need break word,
                        if (severalWords && LineBreakMode != LineBreakMode.NoWrap)
                        {
                            //cannot add this word
                            if (!AddLine(lineResult, textLine))
                            {
                                break; //was last allowed line
                            }

                            PostponeToNextLine(word); //push word
                            continue;
                        }

                        if (LineBreakMode == LineBreakMode.WordWrap || LineBreakMode == LineBreakMode.NoWrap)
                        {
                            //silly add
                            AddLine(textLine);
                            continue;
                        }

                        if (result.Count == 0 && !spanPostponed && firstLineOffset.X > 0)
                        {
                            //not fitting new span, just postpone to next line
                            spanPostponed = true;

                            if (lineIndex == maxLines)
                            {
                                AddLine(word, textLine);
                                break;
                            }

                            AddEmptyLineInternal();

                            PostponeToNextLine(word); //push word
                            continue;
                        }

                        var cycle = "";
                        var bounds = new SKRect();
                        var maybeLimit = 0;
                        var savedWidth = 0.0f;
                        int lenInsideWord = 0;
                        int posInsideWord = 0;
                        bool needBreak = false;
                        for (int pos = 0; pos < textLine.Length; pos++)
                        {
                            lenInsideWord++;
                            cycle = textLine.Substring(posInsideWord, lenInsideWord);
                            MeasureText(paint, cycle, ref bounds);

                            if (Math.Round(bounds.Width) > limitWidth)
                            {
                                //remove one last character to maybe fit?
                                var chunk = textLine.Substring(posInsideWord, lenInsideWord - 1);

                                if (string.IsNullOrEmpty(chunk))
                                {
                                    needBreak = true;
                                    break;
                                }

                                width = MeasureLineGlyphs(paint, chunk, needsShaping, scale).Width;

                                var pass = textLine;
                                if (paragraphs.Length > 1)
                                    pass = null;

                                if (maxLines > -1 && maxLines == result.Count + 1) //last allowed line
                                {
                                    isCut = true;
                                    AddLine(chunk, pass);
                                    needBreak = true;
                                    break;
                                }

                                var postpone = AddLine(chunk, pass);

                                if (postpone)
                                {
                                    var cut = textLine.Substring(posInsideWord + lenInsideWord - 1,
                                        textLine.Length - (lenInsideWord - 1));

                                    PostponeToNextLine(cut);
                                }
                                else
                                {
                                    needBreak = true;
                                }

                                break;
                            }
                            else
                            {
                                if (pos == textLine.Length - 1)
                                {
                                    //last character, add everything
                                    AddLine(textLine, null);
                                }
                            }
                        }

                        if (needBreak)
                        {
                            break;
                        }
                    }
                    else
                    {
                        lineResult = textLine;
                    }
                }

                //last line
                if (stackWords.Count == 0) //!string.IsNullOrEmpty(lineResult) &&
                {
                    if (string.IsNullOrEmpty(lineResult) && span != null)
                    {
                        //we can add an empty one because we gonna merge spans later and remove empty lines eventually
                        AddEmptyLineInternal();
                    }
                    else
                        AddLine(lineResult);
                }

                if (isCut) // If the text is cut  break paragraphs loop
                {
                    break;
                }
            }

            //finished iterating paragraphs

            if (result.Count > 0)
            {
                result[^1].IsLastInParagraph = true;
            }

            ret.WasCut = isCut;
            ret.Lines = result.ToArray();

            if (maxHeight > 0 && !isCut)
            {
                ret.HasMoreVerticalSpace = (float)(maxHeight - (totalHeight + LineHeightWithSpacing));
            }

            if (result.Count > 0)
            {
                ret.HasMoreHorizontalSpace =
                    limitWidth - ret.Lines.Max(x => x.Width); // ret.Lines.Max(x => x.Width) < maxWidth + 0.5;
            }

            return ret;
        }

        List<string> SplitLineToWords(string line, char space)
        {
            if (line == space.ToString())
            {
                return new() { line };
            }

            string GetSpaces(string str, bool leading)
            {
                var spaces = leading
                    ? str.TakeWhile(c => c == space).ToArray()
                    : str.Reverse().TakeWhile(c => c == space).ToArray();
                return new string(spaces);
            }

            var leadingSpaces = GetSpaces(line, leading: true);
            var trailingSpaces = GetSpaces(line, leading: false);

            // Now trim the line and split by space, without removing empty entries
            var trimmedLine = line.Trim();
            var words = trimmedLine.Split(new[] { space }, StringSplitOptions.None).Reverse().ToList();

            // words list is inverted!
            if (leadingSpaces.Length > 0) words.Add(leadingSpaces);
            if (trailingSpaces.Length > 0) words.Insert(0, trailingSpaces);

            //if (words.Count > 0 && NeedsRTL(words[0]))
            //{
            //    words.Reverse();
            //}

            return words;
        }

        private TextLine[] SplitLines(string text,
            SKPaint paint,
            SKPoint firstLineOffset,
            float maxWidth,
            float maxHeight,
            int maxLines,
            bool needsShaping,
            TextSpan span, float scale)
        {
            if (string.IsNullOrEmpty(text) || paint.Typeface == null)
            {
                return null;
            }

            if (span != null)
            {
                needsShaping = span.NeedShape;
            }

            bool needCalc = true;
            DecomposedText decomposedText = null;
            var autosize = this.AutoSize;
            var autoSizeFontStep = 0.1f;

            if (UsingFontSize > 0 &&
                (AutoSize == AutoSizeType.FitFillHorizontal || AutoSize == AutoSizeType.FitFillVertical))
            {
                paint.TextSize = (float)UsingFontSize; //use from last time
                UpdateFontMetrics(paint);
            }

            bool calculatingMask = false;
            var measureText = text;

            if (!string.IsNullOrEmpty(AutoSizeText))
            {
                calculatingMask = true;
                measureText = AutoSizeText;
            }

            while (needCalc)
            {
                decomposedText = DecomposeText(measureText, paint, firstLineOffset, maxWidth, maxHeight, maxLines,
                    needsShaping, span, scale);

                if (autosize != AutoSizeType.None && maxWidth > 0 && maxHeight > 0)
                {
                    if ((AutoSize == AutoSizeType.FitHorizontal || AutoSize == AutoSizeType.FitFillHorizontal)
                        && (decomposedText.CountParagraphs != decomposedText.Lines.Length || decomposedText.WasCut))
                    {
                        autosize = AutoSizeType.FitHorizontal;
                    }
                    else if ((AutoSize == AutoSizeType.FitVertical || AutoSize == AutoSizeType.FitFillVertical)
                             && decomposedText.WasCut)
                    {
                        autosize = AutoSizeType.FitVertical;
                    }
                    else if ((AutoSize == AutoSizeType.FillVertical || AutoSize == AutoSizeType.FitFillVertical)
                             && decomposedText.HasMoreVerticalSpace >= 3)
                    {
                        autosize = AutoSizeType.FillVertical;
                    }
                    else if ((AutoSize == AutoSizeType.FillHorizontal || AutoSize == AutoSizeType.FitFillHorizontal)
                             && decomposedText.HasMoreHorizontalSpace >= 3)
                    {
                        autosize = AutoSizeType.FillHorizontal;
                    }
                    else
                    {
                        autosize = AutoSizeType.None;
                    }

                    if (autosize == AutoSizeType.FitVertical || autosize == AutoSizeType.FitHorizontal)
                    {
                        if (paint.TextSize == 0)
                        {
                            //wtf just happened
                            Trace.WriteLine(
                                $"[SkiaLabel] Error couldn't fit text '{this.Text}' inside label width {this.Width}");
                            if (Debugger.IsAttached)
                                Debugger.Break();
                            paint.TextSize = 12;
                            needCalc = false;
                        }

                        paint.TextSize -= autoSizeFontStep;
                        UpdateFontMetrics(PaintDefault);
                    }
                    else if (autosize == AutoSizeType.FillVertical || autosize == AutoSizeType.FillHorizontal)
                    {
                        paint.TextSize += autoSizeFontStep;
                        UpdateFontMetrics(PaintDefault);
                    }
                }
                else
                {
                    needCalc = false;
                    if (calculatingMask)
                    {
                        calculatingMask = false;
                        measureText = text;
                        decomposedText = DecomposeText(measureText, paint, firstLineOffset, maxWidth, maxHeight,
                            maxLines, needsShaping, span, scale);
                    }
                }

                decomposedText.AutoSize = autosize;

                if (_lastDecomposed != null && autosize == AutoSizeType.None) //autosize ended
                {
                    if (_lastDecomposed.AutoSize == AutoSizeType.FillHorizontal)
                    {
                        decomposedText = _lastDecomposed;
                    }
                    else if (_lastDecomposed.AutoSize == AutoSizeType.FitHorizontal)
                    {
                        //var stop = _lastDecomposed.Lines;
                    }
                }

                _lastDecomposed = decomposedText;
            }

            IsCut = decomposedText.WasCut;
            UsingFontSize = paint.TextSize;

            return decomposedText.Lines;
        }


        public void MergeSpansForLines(
                TextSpan span,
                TextLine line,
                TextLine previousSpanLastLine)
            //merge first line with last from previous span
        {
            if (string.IsNullOrEmpty(previousSpanLastLine.Value))
            {
                return;
            }

            var spans = previousSpanLastLine.Spans.ToList();
            if (!string.IsNullOrEmpty(line.Value))
            {
                spans.AddRange(line.Spans);
                line.Width += previousSpanLastLine.Width;
            }
            else
            {
                line.Width = previousSpanLastLine.Width;
            }

            if (previousSpanLastLine.Height > line.Height)
                line.Height = previousSpanLastLine.Height;

            line.Spans = spans;

            line.Value = previousSpanLastLine.Value + line.Value;

            line.IsNewParagraph = previousSpanLastLine.IsNewParagraph;
            // var lastSpan = previousSpanLastLine.ApplySpans.LastOrDefault();

            /*
            if (string.IsNullOrEmpty(line.Value))
            {
                line.Value = previousSpanLastLine.Value;
                line.ApplySpans.AddRange(previousSpanLastLine.ApplySpans);
                line.Glyphs = previousSpanLastLine.Glyphs;
                line.Width = previousSpanLastLine.Width;
            }
            else
            {
                line.Value = previousSpanLastLine.Value + line.Value;
                line.ApplySpans.AddRange(previousSpanLastLine.ApplySpans);
                line.ApplySpans.Add(ApplySpan.Create(span,
                    lastSpan.End + 1,
                    lastSpan.End + line.Glyphs.Length));

                // Use pooled list to avoid allocation
                using var pooledCharacterPositions = PooledLineGlyphList.Get();
                var characterPositions = pooledCharacterPositions.List;
                characterPositions.AddRange(previousSpanLastLine.Glyphs);
                var startAt = previousSpanLastLine.Width;
                foreach (var glyph in line.Glyphs)
                {
                    characterPositions.Add(LineGlyph.Move(glyph, glyph.Position + startAt));
                }
                line.Glyphs = characterPositions.ToArray();

                line.Width += previousSpanLastLine.Width;
            }
            */
        }

        public (int Limit, float Width) CutLineToFit(
            SKPaint paint,
            string textIn, float maxWidth)
        {
            SKRect bounds = new SKRect();
            var cycle = "";
            var limit = 0;
            float resultWidth = 0;

            var tail = string.Empty;
            if (LineBreakMode == LineBreakMode.TailTruncation)
                tail = Trail;

            textIn += tail;

            MeasureText(paint, textIn, ref bounds);

            if (bounds.Width > maxWidth && !string.IsNullOrEmpty(textIn))
            {
                for (int pos = 0; pos < textIn.Length; pos++)
                {
                    cycle = textIn.Left(pos + 1).TrimEnd() + tail;
                    MeasureText(paint, cycle, ref bounds);
                    if (bounds.Width > maxWidth)
                        break;
                    resultWidth = bounds.Width;
                    limit = pos + 1;
                }
            }

            return (limit, resultWidth);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MeasureTextWidthWithAdvance(SKPaint paint, string text)
        {
            var bounds = paint.MeasureText(text);
            return bounds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MeasureTextWidthWithAdvance(SKPaint paint, ReadOnlySpan<char> textSpan)
        {
            var bounds = paint.MeasureText(textSpan);
            return bounds;
        }

        /// <summary>
        /// Returns text taken size in pixels. Accounts paint transforms like skew etc.
        /// </summary>
        /// <param name="paint"></param>
        /// <param name="text"></param>
        /// <param name="bounds"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MeasureText(SKPaint paint, string text, ref SKRect bounds)
        {
            paint.MeasureText(text, ref bounds);

            if (paint.TextSkewX != 0)
            {
                float additionalWidth = Math.Abs(paint.TextSkewX) * paint.TextSize;
                bounds.Right += additionalWidth; //notice passed by ref struct will be modified
            }



            //if (StrokeWidth > 0 && StrokeColor != TransparentColor)
            //{
            //    float additionalWidth = (float)(StrokeWidth * 2 * RenderingScale);
            //    bounds.Right += additionalWidth;
            //    bounds.Left -= additionalWidth;
            //    bounds.Bottom += additionalWidth * 2;
            //}

            //if (DropShadowSize > 0 && DropShadowColor != TransparentColor)
            //{
            //    float additionalWidth = (float)(DropShadowSize * RenderingScale + DropShadowOffsetX * RenderingScale);
            //    bounds.Right += additionalWidth;
            //    float additionalHeight = (float)(DropShadowSize * RenderingScale + DropShadowOffsetY * RenderingScale);
            //    bounds.Bottom += additionalHeight;
            //}


            bounds = new SKRect(
                (float)Math.Floor(bounds.Left),
                (float)Math.Floor(bounds.Top),
                (float)Math.Ceiling(bounds.Right),
                (float)Math.Ceiling(bounds.Bottom)
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LastNonSpaceIndex(string text)
        {
            for (int i = text.Length - 1; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return i;
                }
            }

            return -1;
        }


        public static List<bool> AreAllGlyphsAvailable(string text, SKTypeface typeface)
        {
            var glyphIds = typeface.GetGlyphs(text);
            var results = new List<bool>(glyphIds.Length); // Use the length of glyphIds instead

            int glyphIndex = 0;
            for (int i = 0; i < text.Length; i++)
            {
                //int codePoint = char.ConvertToUtf32(text, i);

                // Check if it's a high surrogate and increment to skip the low surrogate.
                if (char.IsHighSurrogate(text[i]))
                {
                    i++;
                }

                bool glyphExists = glyphIds[glyphIndex] != 0;
                results.Add(glyphExists);

                // Since each code point maps to a single glyph, increment the glyph index separately.
                glyphIndex++;
            }

            return results;
        }

        public static SKSize GetResultSize(SKShaper.Result result)
        {
            if (result == null || result.Points.Length == 0)
                throw new ArgumentNullException(nameof(result));

            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (var i = 0; i < result.Points.Length; i++)
            {
                var point = result.Points[i];

                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }

            float height = maxY - minY;

            return new SKSize(result.Width, height);
        }

        public static SKShaper.Result GetShapedText(SKShaper shaper, string text, float x, float y, SKPaint paint)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            if (shaper == null)
                throw new ArgumentNullException(nameof(shaper));
            if (paint == null)
                throw new ArgumentNullException(nameof(paint));

            var font = paint.ToFont();

            if (font != null && shaper.Typeface != null)
            {
                font.Typeface = shaper.Typeface;
                // shape the text
                var result = shaper.Shape(text, x, y, paint);

                return result;
            }

            return null;
        }

        public static List<UsedGlyph> GetGlyphs(string text, SKTypeface typeface)
        {
            if (typeface == null)
                typeface = SkiaFontManager.DefaultTypeface;

            var glyphIds = typeface.GetGlyphs(text);
            var results = new List<UsedGlyph>(glyphIds.Length);
            int glyphIndex = 0;

            if (!string.IsNullOrEmpty(text))
            {
                ReadOnlySpan<char> textSpan = text.AsSpan();
                int i = 0;

                while (i < textSpan.Length)
                {
                    // Use DecodeFromUtf16 instead of TryCreate
                    OperationStatus status =
                        Rune.DecodeFromUtf16(textSpan.Slice(i), out Rune rune, out int charsConsumed);

                    if (status == OperationStatus.Done)
                    {
                        int codePoint = rune.Value;

                        var usedGlyph = new UsedGlyph
                        {
                            Id = (ushort)(glyphIndex < glyphIds.Length ? glyphIds[glyphIndex] : 0),
                            Symbol = codePoint,
                            IsAvailable =
                                (glyphIndex < glyphIds.Length && glyphIds[glyphIndex] != 0) ||
                                IsGlyphAlwaysAvailable(rune.ToString()),
                            StartIndex = i,
                            Length = charsConsumed,
                            Source = text // Assign the original text
                        };

                        results.Add(usedGlyph);
                        glyphIndex++;
                        i += charsConsumed;
                    }
                    else
                    {
                        // Handle invalid rune, possibly replace with fallback
                        var usedGlyph = new UsedGlyph
                        {
                            Id = 0, // Assuming 0 is the fallback glyph ID
                            Symbol = textSpan[i],
                            IsAvailable = false,
                            StartIndex = i,
                            Length = 1,
                            Source = text // Assign the original text
                        };
                        results.Add(usedGlyph);
                        glyphIndex++;
                        i++;
                    }
                }
            }

            return results;
        }

        public static bool IsGlyphAlwaysAvailable(string glyphText)
        {
            return glyphText == "\n";
        }

        public static bool UnicodeNeedsShaping(int unicodeCharacter)
        {
            if (EmojiData.IsEmoji(unicodeCharacter))
                return true;

            // Emoji skin tone modifiers (Fitzpatrick scale)
            if (unicodeCharacter >= 0x1F3FB && unicodeCharacter <= 0x1F3FF) return true;

            // Arabic Unicode range
            if (unicodeCharacter >= 0x0600 && unicodeCharacter <= 0x06FF) return true;

            // Syriac Unicode range
            if (unicodeCharacter >= 0x0700 && unicodeCharacter <= 0x074F) return true;

            // Thaana Unicode range
            if (unicodeCharacter >= 0x0780 && unicodeCharacter <= 0x07BF) return true;

            // Devanagari Unicode range
            if (unicodeCharacter >= 0x0900 && unicodeCharacter <= 0x097F) return true;

            // Bengali Unicode range
            if (unicodeCharacter >= 0x0980 && unicodeCharacter <= 0x09FF) return true;

            // Gurmukhi Unicode range
            if (unicodeCharacter >= 0x0A00 && unicodeCharacter <= 0x0A7F) return true;

            // Gujarati Unicode range
            if (unicodeCharacter >= 0x0A80 && unicodeCharacter <= 0x0AFF) return true;

            // Oriya Unicode range
            if (unicodeCharacter >= 0x0B00 && unicodeCharacter <= 0x0B7F) return true;

            // Tamil Unicode range
            if (unicodeCharacter >= 0x0B80 && unicodeCharacter <= 0x0BFF) return true;

            // Telugu Unicode range
            if (unicodeCharacter >= 0x0C00 && unicodeCharacter <= 0x0C7F) return true;

            // Kannada Unicode range
            if (unicodeCharacter >= 0x0C80 && unicodeCharacter <= 0x0CFF) return true;

            // Malayalam Unicode range
            if (unicodeCharacter >= 0x0D00 && unicodeCharacter <= 0x0D7F) return true;

            // Sinhala Unicode range
            if (unicodeCharacter >= 0x0D80 && unicodeCharacter <= 0x0DFF) return true;

            // Thai Unicode range
            if (unicodeCharacter >= 0x0E00 && unicodeCharacter <= 0x0E7F) return true;

            // Lao Unicode range
            if (unicodeCharacter >= 0x0E80 && unicodeCharacter <= 0x0EFF) return true;

            // Tibetan Unicode range
            if (unicodeCharacter >= 0x0F00 && unicodeCharacter <= 0x0FFF) return true;

            // Myanmar Unicode range
            if (unicodeCharacter >= 0x1000 && unicodeCharacter <= 0x109F) return true;

            // Georgian Unicode range
            if (unicodeCharacter >= 0x10A0 && unicodeCharacter <= 0x10FF) return true;

            // Hangul Jamo (Korean) Unicode range
            if (unicodeCharacter >= 0x1100 && unicodeCharacter <= 0x11FF) return true;

            // Ethiopic Unicode range
            if (unicodeCharacter >= 0x1200 && unicodeCharacter <= 0x137F) return true;

            // Khmer Unicode range
            if (unicodeCharacter >= 0x1780 && unicodeCharacter <= 0x17FF) return true;

            // Mongolian Unicode range
            if (unicodeCharacter >= 0x1800 && unicodeCharacter <= 0x18AF) return true;


            return false;
        }

        public static bool NeedsRTL(string text)
        {
            // Check if the text is null or empty
            if (string.IsNullOrEmpty(text)) return false;

            // Iterate over each character in the text
            foreach (char c in text)
            {
                int unicodeCharacter = c;

                // Check if the character's script is traditionally RTL
                // Arabic, Hebrew, Syriac, Thaana, etc.
                if ((unicodeCharacter >= 0x0600 && unicodeCharacter <= 0x06FF) || // Arabic
                    (unicodeCharacter >= 0x0590 && unicodeCharacter <= 0x05FF) || // Hebrew
                    (unicodeCharacter >= 0x0700 && unicodeCharacter <= 0x074F) || // Syriac
                    (unicodeCharacter >= 0x0780 && unicodeCharacter <= 0x07BF) || // Thaana
                    (unicodeCharacter >= 0x0800 && unicodeCharacter <= 0x083F)) // Samaritan
                {
                    return true;
                }
            }

            // If no RTL characters found, return false
            return false;
        }

        /// <summary>
        /// Returns new totalHeight
        /// </summary>
        /// <param name="result"></param>
        /// <param name="span"></param>
        /// <param name="totalHeight"></param>
        /// <param name="heightBlock"></param>
        /// <param name="isNewParagraph"></param>
        /// <param name="needsShaping"></param>
        float AddEmptyLine(List<TextLine> result, TextSpan span,
            float totalHeight, float heightBlock, bool isNewParagraph, bool needsShaping)
        {
            bool assingnIsNewParagraph = isNewParagraph;
            var widthBlock = 0;

            var chunk = new LineSpan()
            {
                NeedsShaping = needsShaping,
                Glyphs = Array.Empty<LineGlyph>(),
                Text = "",
                Span = span,
                Size = new(widthBlock, heightBlock)
            };

            var addLine = new TextLine()
            {
                Value = "",
                IsNewParagraph = assingnIsNewParagraph,
                Width = widthBlock,
                Spans = new() { chunk },
                Height = heightBlock
            };

            result.Add(addLine);

            if (assingnIsNewParagraph && result.Count > 1)
            {
                totalHeight += (float)SpaceBetweenParagraphs;
            }

            return totalHeight;
        }

        /// <summary>
        /// todo move this to some font info data block
        /// otherwise we wont be able to have multiple fonts 
        /// </summary>
        public double SpaceBetweenParagraphs
        {
            get { return LineHeightWithSpacing * ParagraphSpacing; }
        }

        public double GetSpaceBetweenLines(float lineHeight)
        {
            if (FontMetrics.Leading > 0)
            {
                return FontMetrics.Leading * LineSpacing;
            }
            else if (LineSpacing != 1)
            {
                double defaultLeading = lineHeight * 0.1;
                return defaultLeading * LineSpacing;
            }

            return 0;
        }


        protected void ResetTextCalculations()
        {
            IsCut = false;
            NeedMeasure = true;
            _lastDecomposed = null;
            RenderLimit = -1;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public double SpaceBetweenLines
        {
            get { return GetSpaceBetweenLines(LineHeightPixels); }
        }

        /// <summary>
        /// todo move this to some font info data block
        /// otherwise we wont be able to have multiple fonts 
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public double LineHeightWithSpacing
        {
            get { return LineHeightPixels + SpaceBetweenLines; }
        }

        #endregion

        #region FONT

        void UpdateFontMetrics(SKPaint paint)
        {
            FontMetrics = paint.FontMetrics;
            LineHeightPixels =
                (float)Math.Round((-FontMetrics.Ascent + FontMetrics.Descent) * LineHeight); //PaintText.FontSpacing;
            fontUnderline = FontMetrics.UnderlinePosition.GetValueOrDefault();

            if (!string.IsNullOrEmpty(this.MonoForDigits))
            {
                charMonoWidthPixels = MeasureTextWidthWithAdvance(paint, this.MonoForDigits);
            }
            else
            {
                charMonoWidthPixels = 0;
            }
        }

        /// <summary>
        /// A new TypeFace was set
        /// </summary>
        protected virtual void OnFontUpdated()
        {
            GliphsInvalidated = true;
            NeedMeasure = true;
        }

        protected bool GliphsInvalidated = true;

        protected static object LockSetup = new();

        protected string _fontFamily;

        protected virtual void UpdateFont()
        {
            if (IsDisposed || IsDisposing)
                return;

            lock (LockSetup)
            {
                if (_fontFamily != FontFamily
                    || _fontWeight != FontWeight
                    || _fontFamily == null
                    || TypeFace == null)
                {
                    _fontFamily = FontFamily;
                    _fontWeight = FontWeight;

                    var replaceFont = SkiaFontManager.Instance.GetFont(_fontFamily, _fontWeight);

                    if (replaceFont == null)
                    {
                        Super.Log($"Failed to load font {_fontFamily} with weight {_fontWeight}. Using default.");
                        _replaceFont = SkiaFontManager.DefaultTypeface;
                    }
                    else
                    {
                        _replaceFont = replaceFont;
                    }
                }

                InvalidateText();
            }
        }


        protected void ReplaceFont()
        {
            if (_replaceFont == TypeFace)
                return;

            var newFont = _replaceFont;
            bool updated = false;
            if (newFont != null) //new legal font
            {
                TypeFace = newFont;
                updated = true;
            }

            if (TypeFace == null) //unacceptable state
            {
                TypeFace = SkiaFontManager.DefaultTypeface;
                updated = true;
            }

            if (updated) //update
            {
                _replaceFont = null;
                OnFontUpdated();
            }
        }

        protected static void NeedUpdateFont(BindableObject bindable, object oldvalue, object newvalue)
        {
            var control = bindable as SkiaLabel;
            {
                if (control != null && !control.IsDisposed)
                {
                    control.UpdateFont();
                }
            }
        }

        #endregion

        #region ALLOCATIONS

        void CleanAllocations()
        {
            if (PaintDefault != null)
            {
                PaintDefault.Typeface = null;
                PaintDefault.Dispose();
                PaintDefault = null;
            }

            PaintStroke?.Dispose();
            PaintStroke = null;
            PaintShadow?.Dispose();
            PaintShadow = null;
            PaintDeco?.Dispose();
            PaintDeco = null;
            Shaper?.Dispose();
            Shaper = null;
        }

        public SKPaint PaintDefault = new SKPaint { IsAntialias = true, IsDither = true };

        public SKPaint PaintStroke = new SKPaint { IsAntialias = true, IsDither = true };

        public SKPaint PaintShadow = new SKPaint { IsAntialias = true, IsDither = true };

        public SKPaint PaintDeco = new SKPaint { };

        protected void SetupShaper(SKTypeface typeface)
        {
            if (Shaper == null || Shaper.Typeface != typeface)
            {
                var kill = Shaper;
                Shaper = new SKShaper(typeface);
                DisposeObject(kill);
            }
        }

        protected SKShaper Shaper;

        #endregion


        [EditorBrowsable(EditorBrowsableState.Never)]
        public int LinesCount { get; protected set; } = 1;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public TextLine[] Lines { get; protected set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public float LineHeightPixels { get; protected set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public List<UsedGlyph> Glyphs { get; protected set; } = new();

        [EditorBrowsable(EditorBrowsableState.Never)]
        public float MeasuredLineHeight { get; protected set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public double UsingFontSize { get; set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsCut { get; protected set; }

        protected float charMonoWidthPixels;
        protected int RenderLimit = -1;
        protected float fontUnderline;

        private DecomposedText _lastDecomposed;

        private int _fontWeight;
        private static float _scaleResampleText = 1.0f;
        private SKTypeface _replaceFont;

        public static string Trail = "..";


        #region STATIC BINDABLE PROPERTIES

        public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes),
            typeof(FontAttributes),
            typeof(SkiaLabel),
            FontAttributes.None,
            propertyChanged: NeedUpdateFont);

        [TypeConverter(typeof(DrawnFontAttributesConverter))]
        public FontAttributes FontAttributes
        {
            get { return (FontAttributes)GetValue(FontAttributesProperty); }
            set { SetValue(FontAttributesProperty, value); }
        }

        public static readonly BindableProperty DrawWhenEmptyProperty = BindableProperty.Create(nameof(Tag),
            typeof(bool),
            typeof(SkiaLabel),
            true, propertyChanged: NeedInvalidateMeasure);

        public bool DrawWhenEmpty
        {
            get { return (bool)GetValue(DrawWhenEmptyProperty); }
            set { SetValue(DrawWhenEmptyProperty, value); }
        }

        public static readonly BindableProperty KeepSpacesOnLineBreaksProperty = BindableProperty.Create(
            nameof(KeepSpacesOnLineBreaks),
            typeof(bool),
            typeof(SkiaLabel),
            false,
            propertyChanged: NeedInvalidateMeasure);

        public bool KeepSpacesOnLineBreaks
        {
            get { return (bool)GetValue(KeepSpacesOnLineBreaksProperty); }
            set { SetValue(KeepSpacesOnLineBreaksProperty, value); }
        }


        public static readonly BindableProperty FontWeightProperty = BindableProperty.Create(
            nameof(FontWeight),
            typeof(int),
            typeof(SkiaLabel),
            0, propertyChanged: NeedUpdateFont);

        /// <summary>
        /// Gets or sets the weight (thickness) of the font.
        /// </summary>
        /// <remarks>
        /// Font weight is specified as an integer value, typically in the range of 100-900:
        /// 
        /// - 100: Thin
        /// - 200: Extra Light (Ultra Light)
        /// - 300: Light
        /// - 400: Normal/Regular (default)
        /// - 500: Medium
        /// - 600: Semi Bold (Demi Bold)
        /// - 700: Bold
        /// - 800: Extra Bold (Ultra Bold)
        /// - 900: Black (Heavy)
        /// 
        /// This property requires that fonts be properly registered with font weight information.
        /// Use the following approach in your MauiProgram.cs:
        /// 
        /// ```csharp
        /// fonts.AddFont("Roboto-Light.ttf", "Roboto", FontWeight.Light);
        /// fonts.AddFont("Roboto-Regular.ttf", "Roboto", FontWeight.Regular);
        /// fonts.AddFont("Roboto-Medium.ttf", "Roboto", FontWeight.Medium);
        /// fonts.AddFont("Roboto-Bold.ttf", "Roboto", FontWeight.Bold);
        /// ```
        /// 
        /// A value of 0 means the default weight will be used.
        /// </remarks>
        public int FontWeight
        {
            get { return (int)GetValue(FontWeightProperty); }
            set { SetValue(FontWeightProperty, value); }
        }

        public static readonly BindableProperty AutoFontProperty = BindableProperty.Create(
            nameof(AutoFont),
            typeof(bool),
            typeof(SkiaLabel),
            false, propertyChanged: NeedUpdateFont);

        /// <summary>
        /// Find and set system font where the first glyph in text is present
        /// </summary>
        public bool AutoFont
        {
            get { return (bool)GetValue(AutoFontProperty); }
            set { SetValue(AutoFontProperty, value); }
        }

        public static readonly BindableProperty TypeFaceProperty = BindableProperty.Create(
            nameof(TypeFace),
            typeof(SKTypeface),
            typeof(SkiaLabel),
            defaultValue: null,
            propertyChanged: NeedUpdateFont);

        public SKTypeface TypeFace
        {
            get { return (SKTypeface)GetValue(TypeFaceProperty); }
            set { SetValue(TypeFaceProperty, value); }
        }

        public static readonly BindableProperty HorizontalTextAlignmentProperty = BindableProperty.Create(
            nameof(HorizontalTextAlignment),
            typeof(DrawTextAlignment),
            typeof(SkiaLabel),
            defaultValue: DrawTextAlignment.Start,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Gets or sets the horizontal alignment of text within the label.
        /// </summary>
        /// <remarks>
        /// Available alignment options:
        /// - Start: Aligns text to the left (or right in RTL languages)
        /// - Center: Centers text horizontally
        /// - End: Aligns text to the right (or left in RTL languages)
        /// - FillWords: Stretches text to fill the width by adjusting word spacing
        /// - FillWordsFull: Similar to FillWords but with more aggressive stretching
        /// - FillCharacters: Stretches text by adjusting character spacing
        /// - FillCharactersFull: Similar to FillCharacters but with more aggressive stretching
        /// 
        /// The Fill options are useful for justified text alignment, creating evenly
        /// distributed text that spans the entire width of the label.
        /// </remarks>
        public DrawTextAlignment HorizontalTextAlignment
        {
            get { return (DrawTextAlignment)GetValue(HorizontalTextAlignmentProperty); }
            set { SetValue(HorizontalTextAlignmentProperty, value); }
        }

        public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create(
            nameof(VerticalTextAlignment),
            typeof(TextAlignment),
            typeof(SkiaLabel),
            defaultValue: TextAlignment.Start,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Gets or sets the vertical alignment of text within the label.
        /// </summary>
        /// <remarks>
        /// Available alignment options:
        /// - Start: Aligns text to the top of the label
        /// - Center: Centers text vertically within the label
        /// - End: Aligns text to the bottom of the label
        /// 
        /// This property is particularly useful when the label height is larger than
        /// the text content height, allowing control over where the text is positioned
        /// vertically within the available space.
        /// </remarks>
        public TextAlignment VerticalTextAlignment
        {
            get { return (TextAlignment)GetValue(VerticalTextAlignmentProperty); }
            set { SetValue(VerticalTextAlignmentProperty, value); }
        }


        public static readonly BindableProperty LineHeightProperty = BindableProperty.Create(
            nameof(LineHeight),
            typeof(double),
            typeof(SkiaLabel),
            1.0,
            propertyChanged: NeedUpdateFont);

        /// <summary>
        /// Gets or sets the line height as a multiple of the font size, multiplier of how much space will be allocated for a line.
        /// Note that this is different from LineSpacing.
        /// </summary>
        public double LineHeight
        {
            get { return (double)GetValue(LineHeightProperty); }
            set { SetValue(LineHeightProperty, value); }
        }

        public static readonly BindableProperty SensorRotationProperty = BindableProperty.Create(
            nameof(SensorRotation),
            typeof(double),
            typeof(SkiaLabel),
            0.0,
            propertyChanged: NeedDraw);

        public double SensorRotation
        {
            get { return (double)GetValue(SensorRotationProperty); }
            set { SetValue(SensorRotationProperty, value); }
        }

        public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
            nameof(FontFamily),
            typeof(string),
            typeof(SkiaLabel),
            defaultValue: string.Empty,
            propertyChanged: NeedUpdateFont);

        /// <summary>
        /// Gets or sets the font family name used for rendering the text.
        /// </summary>
        /// <remarks>
        /// Set this property to use a specific font for rendering text. You can use:
        /// 
        /// - System fonts: "Arial", "Helvetica", "Times New Roman", etc.
        /// - Custom fonts that have been registered with the app
        /// 
        /// For custom fonts, you need to:
        /// 1. Add the font file to your project (typically in Resources/Fonts folder)
        /// 2. Register it in MauiProgram.cs using:
        ///    ```csharp
        ///    fonts.AddFont("FontFileName.ttf", "CustomFontName");
        ///    ```
        /// 3. Reference it using the "CustomFontName" alias
        /// 
        /// When used with FontWeight, you can specify different weights of the same font family.
        /// 
        /// If empty (default), a fallback system font will be used.
        /// </remarks>
        public string FontFamily
        {
            get { return (string)GetValue(FontFamilyProperty); }
            set { SetValue(FontFamilyProperty, value); }
        }

        public static readonly BindableProperty MaxLinesProperty = BindableProperty.Create(nameof(MaxLines),
            typeof(int), typeof(SkiaLabel), -1,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Gets or sets the maximum number of lines to display.
        /// </summary>
        /// <remarks>
        /// This property limits the number of text lines rendered:
        /// 
        /// - -1 (default): No limit, all lines are displayed
        /// - 0: No lines are displayed (text is hidden)
        /// - 1: Single line only (similar to a single-line text field)
        /// - 2+: Specific number of lines maximum
        /// 
        /// When text exceeds the maximum number of lines, the overflow behavior is
        /// determined by the LineBreakMode property. For example, with LineBreakMode.TailTruncation,
        /// excess text will be replaced with an ellipsis (...).
        /// 
        /// This property is useful for creating fixed-height text areas or previews
        /// where you want to show only a limited number of lines.
        /// </remarks>
        public int MaxLines
        {
            get { return (int)GetValue(MaxLinesProperty); }
            set { SetValue(MaxLinesProperty, value); }
        }

        //public static readonly BindableProperty AllowUnicodeProperty = BindableProperty.Create(
        //    nameof(AllowUnicode),
        //    typeof(bool),
        //    typeof(SkiaLabel),
        //    true);

        //public bool AllowUnicode
        //{
        //    get { return (bool)GetValue(AllowUnicodeProperty); }
        //    set { SetValue(AllowUnicodeProperty, value); }
        //}


        public static readonly BindableProperty AutoSizeProperty = BindableProperty.Create(nameof(AutoSize),
            typeof(AutoSizeType), typeof(SkiaLabel),
            AutoSizeType.None,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Gets or sets how the label automatically adjusts font size to fit available space.
        /// </summary>
        /// <remarks>
        /// Available auto-sizing options:
        /// 
        /// - None (default): No auto-sizing, text uses the exact FontSize specified
        /// - TextToWidth: Adjusts font size to fit the width of the label
        /// - TextToHeight: Adjusts font size to fit the height of the label
        /// - TextToView: Adjusts font size to fit both width and height of the label
        /// 
        /// When auto-sizing is enabled, the label will automatically reduce the font size
        /// when necessary to make the text fit within the available space. The minimum
        /// font size is determined by the AutoSizeText property.
        /// 
        /// This is useful for:
        /// - Responsive layouts where available space may vary
        /// - Dynamic text where length may change at runtime
        /// - Ensuring text is fully visible within fixed space constraints
        /// 
        /// Note that auto-sizing can impact performance, especially with frequently 
        /// changing text or container sizes.
        /// </remarks>
        public AutoSizeType AutoSize
        {
            get { return (AutoSizeType)GetValue(AutoSizeProperty); }
            set { SetValue(AutoSizeProperty, value); }
        }

        public static readonly BindableProperty AutoSizeTextProperty = BindableProperty.Create(
            nameof(AutoSizeText),
            typeof(string),
            typeof(SkiaLabel),
            null,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Can use this text for auto-sizing the font instead if the real Text
        /// </summary>
        public string AutoSizeText
        {
            get { return (string)GetValue(AutoSizeTextProperty); }
            set { SetValue(AutoSizeTextProperty, value); }
        }

        public static readonly BindableProperty LineSpacingProperty = BindableProperty.Create(nameof(LineSpacing),
            typeof(double), typeof(SkiaLabel), 1.0,
            propertyChanged: NeedInvalidateMeasure);

        public double LineSpacing
        {
            get { return (double)GetValue(LineSpacingProperty); }
            set { SetValue(LineSpacingProperty, value); }
        }

        public static readonly BindableProperty ParagraphSpacingProperty = BindableProperty.Create(
            nameof(ParagraphSpacing),
            typeof(double), typeof(SkiaLabel), 0.25,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Default is 0.25
        /// </summary>
        public double ParagraphSpacing
        {
            get { return (double)GetValue(ParagraphSpacingProperty); }
            set { SetValue(ParagraphSpacingProperty, value); }
        }

        public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create(
            nameof(CharacterSpacing),
            typeof(double), typeof(SkiaLabel), 1.0,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// This applies ONLY when CharByChar is enabled
        /// </summary>
        public double CharacterSpacing
        {
            get { return (double)GetValue(CharacterSpacingProperty); }
            set { SetValue(CharacterSpacingProperty, value); }
        }

        public static readonly BindableProperty LineBreakModeProperty = BindableProperty.Create(
            nameof(LineBreakMode),
            typeof(LineBreakMode),
            typeof(SkiaLabel),
            LineBreakMode.TailTruncation,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Gets or sets how text is handled when it exceeds the available width.
        /// </summary>
        /// <remarks>
        /// Available modes:
        /// - TailTruncation (default): Truncates at the end with an ellipsis (...)
        /// - HeadTruncation: Truncates at the beginning with an ellipsis
        /// - MiddleTruncation: Truncates in the middle with an ellipsis
        /// - CharacterWrap: Wraps to a new line at any character
        /// - WordWrap: Wraps to a new line at word boundaries
        /// - NoWrap: Does not wrap text; long text may be clipped or extend beyond container
        /// 
        /// This property only affects how text that doesn't fit in the available space is handled.
        /// It works in conjunction with MaxLines to control text overflow behavior.
        /// 
        /// For single-line text fields, TailTruncation is commonly used.
        /// For multi-line text, WordWrap is typically preferred.
        /// </remarks>
        public LineBreakMode LineBreakMode
        {
            get { return (LineBreakMode)GetValue(LineBreakModeProperty); }
            set { SetValue(LineBreakModeProperty, value); }
        }

        public static readonly BindableProperty FormattedTextProperty = BindableProperty.Create(
            nameof(FormattedText),
            typeof(FormattedString),
            typeof(SkiaLabel),
            defaultValue: null,
            propertyChanged: NeedInvalidateMeasure);

        public FormattedString FormattedText
        {
            get { return (FormattedString)GetValue(FormattedTextProperty); }
            set { SetValue(FormattedTextProperty, value); }
        }


        public static readonly BindableProperty FormatProperty = BindableProperty.Create(
            nameof(Format), typeof(string), typeof(SkiaLabel),
            string.Empty,
            propertyChanged: TextWasChanged);

        public string Format
        {
            get { return (string)GetValue(FormatProperty); }
            set { SetValue(FormatProperty, value); }
        }

        public static readonly BindableProperty TextProperty = BindableProperty.Create(
            nameof(Text), typeof(string), typeof(SkiaLabel),
            string.Empty,
            propertyChanged: TextWasChanged);

        protected static void TextWasChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaLabel control)
            {
                control.OnTextChanged();
            }
        }

        /// <summary>
        /// Gets or sets the text content to be displayed by the label.
        /// </summary>
        /// <remarks>
        /// This is the primary property for setting simple text content. For rich text with
        /// multiple styles, use the Spans collection or FormattedText property instead.
        /// 
        /// The text supports:
        /// - Multiline content (use newline characters)
        /// - Text transformations via TextTransform property
        /// - Truncation via LineBreakMode property
        /// - Auto-sizing via AutoSize property
        /// 
        /// When Text is set, any existing FormattedText or Spans will be replaced.
        /// </remarks>
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly BindableProperty TextTransformProperty = BindableProperty.Create(nameof(TextTransform),
            typeof(TextTransform),
            typeof(SkiaLabel),
            TextTransform.None,
            propertyChanged: NeedUpdateFont);

        public TextTransform TextTransform
        {
            get { return (TextTransform)GetValue(TextTransformProperty); }
            set { SetValue(TextTransformProperty, value); }
        }

        protected virtual void OnTextChanged()
        {
            InvalidateText();
        }

        public virtual void InvalidateText()
        {
            if (IsDisposed || IsDisposing)
                return;

            GliphsInvalidated = true;
            NeedSetText = true;
            InvalidateMeasure();
        }

        protected bool NeedSetText { get; set; }

        private const string Splitter = " ";

        const char SpaceChar = ' ';

        /// <summary>
        /// Aplies transforms etc
        /// </summary>
        protected virtual void SetTextInternal()
        {
            if (IsDisposed || IsDisposing)
                return;

            var text = string.Empty;
            if (!string.IsNullOrEmpty(Format))
            {
                text = string.Format(Format, Text);
            }
            else
            {
                text = Text;
            }

            if (text != null)
            {
                switch (TextTransform)
                {
                    case TextTransform.Uppercase:
                        TextInternal = text.ToUpper();
                        break;

                    case TextTransform.Lowercase:
                        TextInternal = text.ToLower();
                        break;

                    case TextTransform.Titlecase:
                        TextInternal = text.ToTitleCase(SpaceChar.ToString(), false);
                        break;

                    case TextTransform.Phrasecase:
                        TextInternal = text.ToPhraseCase(true);
                        break;

                    default:
                        TextInternal = text;
                        break;
                }
            }
            else
            {
                TextInternal = string.Empty;
            }
        }

        protected string TextInternal { get; set; }

        public static readonly BindableProperty FallbackCharacterProperty = BindableProperty.Create(
            nameof(FallbackCharacter),
            typeof(char),
            typeof(SkiaLabel),
            ' ', propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Character to show when glyph is not found in font
        /// </summary>
        public char FallbackCharacter
        {
            get { return (char)GetValue(FallbackCharacterProperty); }
            set { SetValue(FallbackCharacterProperty, value); }
        }

        public static readonly BindableProperty MonoForDigitsProperty = BindableProperty.Create(
            nameof(MonoForDigits), typeof(string), typeof(SkiaLabel),
            string.Empty, propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// The character to be taken for its width when want digits to simulate Mono, for example "8", default is null.
        /// </summary>
        public string MonoForDigits
        {
            get { return (string)GetValue(MonoForDigitsProperty); }
            set { SetValue(MonoForDigitsProperty, value); }
        }

        public static readonly BindableProperty LineHeightUniformProperty = BindableProperty.Create(
            nameof(LineHeightUniform), typeof(bool), typeof(SkiaLabel),
            true, propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Should we draw with the maximum line height when lines have different height.
        /// </summary>
        public bool LineHeightUniform
        {
            get { return (bool)GetValue(LineHeightUniformProperty); }
            set { SetValue(LineHeightUniformProperty, value); }
        }

        public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
            nameof(TextColor), typeof(Color), typeof(SkiaLabel),
            Colors.GreenYellow,
            propertyChanged: NeedDraw);

        /// <summary>
        /// Gets or sets the color used to display the text.
        /// </summary>
        /// <remarks>
        /// This property sets the default color for all text within the label.
        /// Individual spans can override this color for portions of text when using
        /// the Spans collection or FormattedText.
        /// 
        /// The default color is GreenYellow, which is easily visible during development.
        /// You should explicitly set this to your desired text color.
        /// 
        /// For gradient text, use the TextGradient property instead of or in conjunction with this property.
        /// </remarks>
        public Color TextColor
        {
            get { return (Color)GetValue(TextColorProperty); }
            set { SetValue(TextColorProperty, value); }
        }

        public static readonly BindableProperty StrokeColorProperty = BindableProperty.Create(
            nameof(StrokeColor),
            typeof(Color),
            typeof(SkiaLabel),
            Colors.Transparent,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Gets or sets the color of the text outline stroke.
        /// </summary>
        /// <remarks>
        /// When set to a non-transparent color and used with a non-zero StrokeWidth,
        /// this creates an outline effect around the text. This can be used for:
        /// 
        /// - Creating outlined text for better visibility on variable backgrounds
        /// - Stylistic effects like outlined fonts
        /// - Creating text that stands out with contrasting outline
        /// 
        /// The default is Transparent, which means no outline is drawn.
        /// For the outline to be visible, both StrokeColor and StrokeWidth must be set.
        /// </remarks>
        public Color StrokeColor
        {
            get { return (Color)GetValue(StrokeColorProperty); }
            set { SetValue(StrokeColorProperty, value); }
        }

        public static readonly BindableProperty StrokeWidthProperty = BindableProperty.Create(
            nameof(StrokeWidth),
            typeof(double),
            typeof(SkiaLabel),
            1.0,
            propertyChanged: NeedInvalidateMeasure);

        public double StrokeWidth
        {
            get { return (double)GetValue(StrokeWidthProperty); }
            set { SetValue(StrokeWidthProperty, value); }
        }

        #region Drop Shadow

        public static readonly BindableProperty DropShadowColorProperty = BindableProperty.Create(
            nameof(DropShadowColor),
            typeof(Color),
            typeof(SkiaLabel),
            Colors.Transparent,
            propertyChanged: NeedDraw);

        public Color DropShadowColor
        {
            get { return (Color)GetValue(DropShadowColorProperty); }
            set { SetValue(DropShadowColorProperty, value); }
        }

        public static readonly BindableProperty DropShadowSizeProperty = BindableProperty.Create(
            nameof(DropShadowSize),
            typeof(double),
            typeof(SkiaLabel),
            2.0,
            propertyChanged: NeedDraw);

        public double DropShadowSize
        {
            get { return (double)GetValue(DropShadowSizeProperty); }
            set { SetValue(DropShadowSizeProperty, value); }
        }

        public static readonly BindableProperty DropShadowOffsetYProperty = BindableProperty.Create(
            nameof(DropShadowOffsetY),
            typeof(double),
            typeof(SkiaLabel),
            2.0,
            propertyChanged: NeedDraw);


        public double DropShadowOffsetY
        {
            get { return (double)GetValue(DropShadowOffsetYProperty); }
            set { SetValue(DropShadowOffsetYProperty, value); }
        }

        public static readonly BindableProperty DropShadowOffsetXProperty = BindableProperty.Create(
            nameof(DropShadowOffsetX),
            typeof(double),
            typeof(SkiaLabel),
            2.0,
            propertyChanged: NeedDraw);

        /// <summary>
        /// To make DropShadow act like shadow
        /// </summary>
        public double DropShadowOffsetX
        {
            get { return (double)GetValue(DropShadowOffsetXProperty); }
            set { SetValue(DropShadowOffsetXProperty, value); }
        }

        #endregion

        public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
            nameof(FontSize),
            typeof(double),
            typeof(SkiaLabel),
            12.0,
            propertyChanged: NeedUpdateFont);

        /// <summary>
        /// Gets or sets the font size in device-independent units.
        /// </summary>
        /// <remarks>
        /// Font size determines the height of the text in device-independent units:
        /// 
        /// - Default is 12.0 units
        /// - Larger values make text bigger
        /// - Smaller values make text smaller
        /// 
        /// In XAML, you can use named font sizes by setting this property to "Small", "Medium", 
        /// "Large", etc. The FontSizeConverter will convert these to appropriate numeric values.
        /// 
        /// When AutoSize is enabled, this value becomes the minimum or initial font size.
        /// 
        /// For best rendering quality, especially with small text, consider using integer values.
        /// </remarks>
        [System.ComponentModel.TypeConverter(typeof(FontSizeConverter))]
        public double FontSize
        {
            get { return (double)GetValue(FontSizeProperty); }
            set { SetValue(FontSizeProperty, value); }
        }


        //public static readonly BindableProperty RotateLayoutParametersProperty = BindableProperty.Create(
        //    nameof(RotateLayoutParameters),
        //    typeof(bool),
        //    typeof(SkiaLabel),
        //    false,
        //    propertyChanged: NeedInvalidateMeasure);

        //public bool RotateLayoutParameters
        //{
        //    get { return (bool)GetValue(RotateLayoutParametersProperty); }
        //    set { SetValue(RotateLayoutParametersProperty, value); }
        //}


        #region GRADIENT

        public static readonly BindableProperty GradientByLinesProperty = BindableProperty.Create(
            nameof(GradientByLines),
            typeof(bool),
            typeof(SkiaLabel),
            true,
            propertyChanged: NeedDraw);

        public bool GradientByLines
        {
            get { return (bool)GetValue(GradientByLinesProperty); }
            set { SetValue(GradientByLinesProperty, value); }
        }


        public static readonly BindableProperty StrokeGradientProperty = BindableProperty.Create(
            nameof(StrokeGradient),
            typeof(SkiaGradient),
            typeof(SkiaLabel),
            null,
            propertyChanged: StrokeGradientPropertyChanged);

        public SkiaGradient StrokeGradient
        {
            get { return (SkiaGradient)GetValue(StrokeGradientProperty); }
            set { SetValue(StrokeGradientProperty, value); }
        }


        private static void StrokeGradientPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl skiaControl)
            {
                if (oldvalue is SkiaGradient skiaGradientOld)
                {
                    skiaGradientOld.Parent = null;
                    skiaGradientOld.BindingContext = null;
                }

                if (newvalue is SkiaGradient skiaGradient)
                {
                    skiaGradient.Parent = skiaControl;
                    skiaGradient.BindingContext = skiaControl.BindingContext;
                }

                skiaControl.Update();
            }
        }

        #endregion

        #endregion

        #region GESTURES

        /// <summary>
        /// Return null if you wish not to consume
        /// </summary>
        /// <param name="span"></param>
        /// <returns></returns>
        public virtual ISkiaGestureListener OnSpanTapped(TextSpan span)
        {
            span.FireTap();
            return this;
        }

        public virtual bool OnFocusChanged(bool focus)
        {
            return false;
        }


        public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args,
            GestureEventProcessingInfo apply)
        {
            if (args.Type == TouchActionResult.Tapped)
            {
                //apply transfroms
                var thisOffset = TranslateInputCoords(apply.ChildOffset, true);
                //apply touch coords
                var x = args.Event.Location.X + thisOffset.X;
                var y = args.Event.Location.Y + thisOffset.Y;

                foreach (var span in Spans.ToList())
                {
                    if (span.HasTapHandler)
                    {
                        if (span.HitIsInside(x, y))
                        {
                            var ptsInsideControl =
                                GetOffsetInsideControlInPoints(args.Event.Location, apply.ChildOffset);
                            PlayRippleAnimation(TouchEffectColor, ptsInsideControl.X, ptsInsideControl.Y);

                            return OnSpanTapped(span);
                        }
                    }
                }
            }

            return base.ProcessGestures(args, apply);
        }

        #endregion
    }
}
