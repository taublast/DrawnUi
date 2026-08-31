using AppoMobi.Specials;
using DrawnUi.Draw;
using System.Diagnostics;
using System.Windows.Input;

namespace DrawnUi.Draw
{
    /// <summary>
    /// Drawn entry, support number of lines and other.
    /// Can customize cursor etc.
    /// Default cache SkiaCacheType.Operations is set automatically in constructor.
    /// </summary>
    public partial class SkiaEditor : SkiaShape, ISkiaGestureListener
    {
        public SkiaEditor()
        {
            UseCache = SkiaCacheType.Operations;
            CanBeFocused = true; // SetFrameworkFocus is overridden below and always accepts anyway
        }

        protected override void CreateDefaultContent()
        {
            CreateControl();
        }

        public override bool WillClipBounds => true;

        /// <summary>
        /// Clip editor content to the FULL drawing rect, never the stroke-inset path — so a
        /// bordered editor clips exactly like a borderless one.
        /// Glyph descenders (g, j, p) can extend BELOW the font's metric line box (how far depends
        /// on the font). Borderless styles clip content to the full rect, so that overflow still
        /// shows; the base contracts the content clip for a stroke, so on bordered styles
        /// (Cupertino/Windows) the same overflow gets cut — a visibly large cut with fonts whose
        /// glyphs overshoot their metrics. The border still draws via the shape path and the
        /// editor's Padding keeps text clear of the frame, so ignoring the stroke inset here is safe.
        /// </summary>
        protected override SKRect CalculateClipSizeForStroke(SKRect destination, float scale) => destination;

        /// <summary>
        /// Lay children out in the FULL padded area, never the stroke-inset one. The base insets the
        /// children rect by the stroke (see <see cref="SkiaShape.CalculateContentSizeForStroke"/>), which
        /// on a single-line editor leaves the placeholder/text label ~2px shorter than one line, so it
        /// clips its OWN descenders (g, j, p) — the visible cut on bordered styles (Cupertino/Windows).
        /// The border still draws via the shape path and Padding keeps text off it, so ignoring the
        /// stroke inset for content layout is safe.
        /// </summary>
        protected override SKRect CalculateContentSizeForStroke(SKRect destination, float scale)
            => ContractPixelsRect(destination, scale, UsePadding);

        public override void OnWillDisposeWithChildren()
        {
            base.OnWillDisposeWithChildren();

            TextChanged = null;
            FocusChanged = null;
            CursorMoved = null;
            TextSubmitted = null;
        }

        #region EVENTS

        /// <summary>
        /// Raised when the caret moves to a new position.
        /// </summary>
        public event EventHandler<string> TextChanged;

        /// <summary>
        /// Raised when editor focus changes.
        /// </summary>
        public event EventHandler<bool> FocusChanged;

        /// <summary>
        /// Raised when the caret moves to a new position.
        /// </summary>
        public event EventHandler CursorMoved;

        /// <summary>
        /// Raised when text is submitted.
        /// </summary>
        public event EventHandler<string> TextSubmitted;

        #endregion

        #region CHILDREN

        protected SkiaScroll _scroll;
        protected SkiaLayout _contentLayer;
        protected SkiaEditorSelection _selectionControl;
        protected SkiaLabel _placeholderLabel;
        protected const char ParagraphBreakChar = '\n';
        protected const char SoftLineBreakChar = '\u2028';
        private float _autoMeasuredLineHeightPx = -1f;
        private float _autoMeasuredLineSpacingPx = -1f;

        private void RecreateLabelIfNeeded()
        {
            if (_contentLayer == null || _selectionControl == null || Cursor == null)
                return;

            var oldLabel = Label;
            var newLabel = CreateLabel();

            if (ReferenceEquals(oldLabel, newLabel))
                return;

            Label = newLabel;
            _selectionControl.SourceLabel = Label;

            _contentLayer.ClearChildren();
            _contentLayer.AddSubView(Label);
            _contentLayer.AddSubView(_selectionControl);
            _contentLayer.AddSubView(Cursor);

            Cursor.Initialize(Label);
        }


        public virtual void UpdateLabel()
        {
            if (Label == null)
                return;

            _autoMeasuredLineHeightPx = -1f;
            _autoMeasuredLineSpacingPx = -1f;

            var displayText = Text;
            if (IsPassword && !string.IsNullOrEmpty(displayText))
            {
                displayText = new string('\u2022', displayText.Length);
            }
            else
            {
                displayText = NormalizeDisplayText(displayText);
            }

            Label.Text = displayText;
            //Debug.WriteLine($"Label Text: {Label.Text}");
            Label.FontFamily = FontFamily;
            Label.FontSize = FontSize;
            Label.TextColor = this.TextColor;
            Label.FontWeight = this.FontWeight;
            Label.FillGradient = this.TextGradient;

            Label.HorizontalTextAlignment = this.HorizontalTextAlignment;
            Label.VerticalTextAlignment = this.VerticalTextAlignment;
            Label.LineHeight = this.LineHeight;

            if (IsMultiline)
            {
                Label.HeightRequest = -1;
                Label.MaxLines = -1;
                if (_scroll != null)
                    _scroll.Orientation = ScrollOrientation.Vertical;
                if (Label != null)
                {
                    Label.HorizontalOptions = LayoutOptions.Fill;
                }
            }
            else
            {
                Label.HeightRequest = GetSingleLineHeightPts();
                Label.MaxLines = 1;
                if (_scroll != null)
                    _scroll.Orientation = ScrollOrientation.Horizontal;  
                if (Label != null)
                {
                    Label.HorizontalOptions = LayoutOptions.Start;
                }
            }

            UpdateCursorStyle();
            UpdateCursorVisibility();

            UpdatePlaceholder();
            UpdateViewportHeight();

            Invalidate();
        }

        protected virtual void UpdateCursorStyle()
        {
            if (Cursor != null)
            {
                Cursor.Color = this.CursorColor;
                Cursor.HeightRequest = GetSingleLineHeightPts();
            }
        }
        protected virtual void UpdatePlaceholder()
        {
            if (_placeholderLabel == null)
                return;

            _placeholderLabel.Text = PlaceholderText;
            _placeholderLabel.TextColor = PlaceholderColor;
            _placeholderLabel.HorizontalTextAlignment = PlaceholderHorizontalAlignment;
            _placeholderLabel.FontFamily = FontFamily;
            _placeholderLabel.FontSize = FontSize;
            _placeholderLabel.FontWeight = FontWeight;
            _placeholderLabel.LineHeight = LineHeight;

            if (IsMultiline)
            {
                _placeholderLabel.HeightRequest = -1;
                _placeholderLabel.MaxLines = -1;
            }
            else
            {
                _placeholderLabel.HeightRequest = GetSingleLineHeightPts();
                _placeholderLabel.MaxLines = 1;
            }

            _placeholderLabel.IsVisible = string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(PlaceholderText);
        }

        protected virtual SkiaLabel OnCreatingLabel(SkiaLabel label)
        {
            return label;
        }

        protected virtual SkiaLabel CreatePlaceholderLabel()
        {
            // Mirror the content label's rendering so a placeholder with emoji/unicode shows too.
            SkiaLabel label;
            if (UseMarkdown)
                label = new SkiaRichLabel();
            else if (UseUnicode)
                label = new SkiaRichLabel { MarkdownEnabled = false };
            else
                label = new SkiaLabel();

            label.UseCache = SkiaCacheType.Operations;
            label.HorizontalOptions = LayoutOptions.Fill;
            label.VerticalOptions = LayoutOptions.Start;
            label.Margin = new Thickness(0, 0, 4, 0);
            label.IsVisible = false;
            return label;
        }

        public virtual SkiaLabel CreateLabel()
        {
            // UseMarkdown: rich label with markdown formatting (+ emoji fallback).
            // UseUnicode: rich label, markdown OFF — emoji/unicode fallback, literal text.
            // neither: plain single-font label (lightest).
            SkiaLabel label;
            if (UseMarkdown)
                label = new SkiaRichLabel();
            else if (UseUnicode)
                label = new SkiaRichLabel { MarkdownEnabled = false };
            else
                label = new SkiaLabel();

            // NOTE: applied to ALL branches — previously the object initializer bound only to the
            // non-markdown branch of the ternary, so rich labels silently missed these settings.
            label.KeepSpacesOnLineBreaks = true;
            label.NeedsGlyphPositions = true;
            label.Margin = new Thickness(0, 0, 4, 0);
            label.TextColor = this.TextColor;
            label.HorizontalOptions = IsMultiline ? LayoutOptions.Fill : LayoutOptions.Start;

            return OnCreatingLabel(label);
        }

        protected virtual SkiaCursor CreateCursor()
        {
            return new()
            {
                UseCache = SkiaCacheType.Operations,
                WidthRequest = 2,
                HeightRequest = GetSingleLineHeightPts(),
                BackgroundColor = Colors.LimeGreen//debug color, will be replaced at initialization anyway
            };
        }

        /// <summary>
        /// Returns the visual height of one editor text line in points for the current font settings.
        /// </summary>
        public virtual double GetSingleLineHeightPts()
        {
            var scale = RenderingScale > 0 ? RenderingScale : 1f;

            double[] candidates =
            {
                Label?.MeasuredLineHeight > 0 ? Math.Ceiling(Label.MeasuredLineHeight / scale) : 0,
                Label?.LineHeightPixels > 0 ? Math.Ceiling(Label.LineHeightPixels / scale) : 0,
                _placeholderLabel?.MeasuredLineHeight > 0 ? Math.Ceiling(_placeholderLabel.MeasuredLineHeight / scale) : 0,
                _placeholderLabel?.LineHeightPixels > 0 ? Math.Ceiling(_placeholderLabel.LineHeightPixels / scale) : 0,
            };

            foreach (var candidate in candidates)
            {
                if (candidate > 0)
                    return candidate;
            }

            var fontSize = FontSize > 0 ? FontSize : 20;
            using (var font = new SKFont())
            {
                var typeface = SkiaFontManager.Instance.GetFont(FontFamily, FontWeight);
                if (typeface != null)
                    font.Typeface = typeface;

                font.Size = (float)(fontSize * scale);

                var metrics = font.Metrics;
                var measuredPixels = Math.Round((-metrics.Ascent + metrics.Descent) * Math.Max(LineHeight, 1.0));
                if (measuredPixels > 0)
                    return Math.Ceiling(measuredPixels / scale);
            }

            return Math.Ceiling(fontSize * 1.2);
        }

        // Flat DrawnUI default palette, shared with the other default-styled controls.
        private static readonly Color EditorDefaultAccent = Color.FromRgba(220, 20, 60, 255);  // Crimson #DC143C
        private static readonly Color EditorDefaultBg = Color.FromRgba(242, 243, 245, 255);     // #F2F3F5
        private static readonly Color EditorPlaceholder = Color.FromRgba(154, 160, 166, 255);   // #9AA0A6

        /// <summary>
        /// Applies platform-specific visuals (background, corner radius, border, cursor color)
        /// per <see cref="SkiaControl.UsingControlStyle"/>. Each property is applied only when the
        /// app has not set it explicitly, so user overrides always win. Called from
        /// <see cref="CreateControl"/> before the label/cursor are built so colors are picked up.
        /// </summary>
        protected virtual void ApplyControlStyleVisuals()
        {
            void Bg(Color c) { if (!IsSet(BackgroundColorProperty)) BackgroundColor = c; }
            void Corner(double c) { if (!IsSet(CornerRadiusProperty)) CornerRadius = c; }
            void Stroke(Color c, double w)
            {
                if (!IsSet(StrokeColorProperty)) StrokeColor = c;
                if (!IsSet(StrokeWidthProperty)) StrokeWidth = w;
            }
            void Cursor_(Color c) { if (!IsSet(CursorColorProperty)) CursorColor = c; }

            if (!IsSet(PaddingProperty)) Padding = new Thickness(12, 8);
            if (!IsSet(PlaceholderColorProperty)) PlaceholderColor = EditorPlaceholder;

            switch (UsingControlStyle)
            {
                case PrebuiltControlStyle.Cupertino:
                    Bg(Colors.White); Corner(10); Stroke(Color.FromRgba(199, 199, 204, 255), 1);
                    Cursor_(Color.FromRgba(0, 122, 255, 255)); // iOS blue
                    break;
                case PrebuiltControlStyle.Material:
                    Bg(Color.FromRgba(239, 235, 244, 255)); Corner(4); Stroke(Colors.Transparent, 0);
                    Cursor_(Color.FromRgba(33, 150, 243, 255)); // Material blue
                    break;
                case PrebuiltControlStyle.Material3:
                    Bg(Color.FromRgba(230, 224, 233, 255)); Corner(4); Stroke(Colors.Transparent, 0);
                    Cursor_(Color.FromRgba(103, 80, 164, 255)); // M3 primary
                    break;
                case PrebuiltControlStyle.Windows:
                    Bg(Colors.White); Corner(2); Stroke(Color.FromRgba(138, 138, 138, 255), 1);
                    Cursor_(Color.FromRgba(0, 120, 212, 255)); // Fluent accent
                    break;
                default:
                    // Unset / flat DrawnUI default. IsSet gating leaves app-configured
                    // editors untouched, so this only styles editors that set nothing.
                    Bg(EditorDefaultBg); Corner(8); Stroke(Colors.Transparent, 0);
                    Cursor_(EditorDefaultAccent);
                    break;
            }
        }

        public virtual void CreateControl()
        {
            ApplyControlStyleVisuals();

            Label = CreateLabel();

            _placeholderLabel = CreatePlaceholderLabel();

            Cursor = CreateCursor();

            Cursor.ZIndex = 1;
            Cursor.IsVisible = false;

            Children = new List<SkiaControl>()
            {
                // Placeholder lives at the editor level so it receives the correct
                // constrained width from the editor's layout pass, not float.MaxValue
                // from the horizontal SkiaScroll content area.
                _placeholderLabel,

                new SkiaScroll()
                {
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Fill,
                    Content =
                        new SkiaLayer
                        {
                            Children =
                            {
                                Label,

                                new SkiaEditorSelection
                                {
                                    HorizontalOptions = LayoutOptions.Fill,
                                    VerticalOptions = LayoutOptions.Fill,
                                    SourceLabel = Label,
                                }.Assign(out _selectionControl),

                                Cursor
                            }
                        }.Assign(out _contentLayer)
                }.Assign(out _scroll)
            };

            Cursor?.Initialize(Label);

            OnControlCreated();

            UpdateLabel();
        }

        public SkiaLabel Label { get; protected set; }

        public SkiaCursor Cursor { get; protected set; }

        #endregion

        #region ENGINE

        protected override void Paint(DrawingContext ctx)
        {
            // Push the editor's real pixel viewport width to the label before any child draws,
            // so DrawLines can use it for Center/End alignment when the label sits inside the
            // horizontal scroll (which gives it an unconstrained/huge rectDraw.Width).
            if (Label != null && DrawingRect.Width > 0)
            {
                var paddingPx = (float)(Padding.HorizontalThickness * ctx.Scale);
                Label.ViewportConstraintWidth = Math.Max(0f, DrawingRect.Width - paddingPx);
            }

            UpdateViewportHeight();
            base.Paint(ctx);
        }

        private string NormalizeDisplayText(string? value)
        {
            var normalized = NormalizeEditorInput(value);
            if (string.IsNullOrEmpty(normalized))
                return normalized;

            if (IsMultiline && IsTrailingEditorBreak(normalized))
            {
                // Preserve the visible trailing empty line for caret placement without mutating editor state.
                return normalized + "\u200B";
            }

            return normalized;
        }

        private string NormalizeEditorInput(string? value)
        {
            var normalized = NormalizeEditorLineBreaks(value);
            return IsMultiline ? normalized : ReplaceEditorBreaksWithSpaces(normalized);
        }

        protected static string NormalizeEditorLineBreaks(string? value)
        {
            return value?
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace('\u2029', ParagraphBreakChar) ?? string.Empty;
        }

        protected static string GetEditorBreakText(bool splitLine)
            => splitLine ? SoftLineBreakChar.ToString() : ParagraphBreakChar.ToString();

        protected static bool IsEditorBreak(char value)
            => value == ParagraphBreakChar || value == SoftLineBreakChar;

        protected static bool IsTrailingEditorBreak(string value)
            => value.Length > 0 && IsEditorBreak(value[^1]);

        protected static string ReplaceEditorBreaksWithSpaces(string value)
            => value.Replace(ParagraphBreakChar, ' ').Replace(SoftLineBreakChar, ' ');

        private int CountVisibleParagraphBreaks(int visibleLines)
        {
            if (visibleLines <= 1 || string.IsNullOrEmpty(Text))
                return 0;

            var remainingVisibleBreaks = visibleLines - 1;
            var paragraphBreaks = 0;

            foreach (var symbol in Text)
            {
                if (!IsEditorBreak(symbol))
                    continue;

                if (symbol == ParagraphBreakChar)
                    paragraphBreaks++;

                remainingVisibleBreaks--;
                if (remainingVisibleBreaks == 0)
                    break;
            }

            return paragraphBreaks;
        }

        private float GetAutoLineHeightPixels(float scale)
        {
            if (_autoMeasuredLineHeightPx > 0)
                return _autoMeasuredLineHeightPx;

            if (Label?.MeasuredLineHeight > 0)
                return Label.MeasuredLineHeight;

            if (!IsMultiline && Label?.HeightRequest >= 0)
                return (float)(Label.HeightRequest * scale);

            if (Label?.LineHeightPixels > 0)
                return Label.LineHeightPixels;

            var lineHeight = FontSize > 0 ? FontSize * Math.Max(LineHeight, 1.0) : 20.0;
            return (float)(lineHeight * scale);
        }

        private float GetAutoLineSpacingPixels(float lineHeightPixels)
        {
            if (_autoMeasuredLineSpacingPx >= 0 && _autoMeasuredLineHeightPx > 0
                && Math.Abs(_autoMeasuredLineHeightPx - lineHeightPixels) < 0.5f)
                return _autoMeasuredLineSpacingPx;

            if (Label == null || lineHeightPixels <= 0)
                return 0f;

            return (float)Label.GetSpaceBetweenLines(lineHeightPixels);
        }

        private float GetAutoVisibleHeightPixels(float scale)
        {
            var lines = MaxLines > 0 ? MaxLines : 1;
            if (AutoHeight && IsMultiline)
            {
                var actualLines = Label?.LinesCount ?? 0;
                if (actualLines < 1)
                    actualLines = 1;
                lines = MaxLines > 0 ? Math.Min(actualLines, MaxLines) : actualLines;
            }
            var lineHeightPx = GetAutoLineHeightPixels(scale);
            if (lineHeightPx <= 0)
                return 0f;

            var lineSpacingPx = lines > 1 ? GetAutoLineSpacingPixels(lineHeightPx) : 0f;
            var contentHeightPx = lineHeightPx * lines + lineSpacingPx * Math.Max(0, lines - 1);
            if (lines <= 1 || Label == null)
                return contentHeightPx;

            var paragraphSpacingPx = (lineHeightPx + lineSpacingPx) * (float)Label.ParagraphSpacing;

            if (!AutoHeight)
            {
                // Fixed reservation: budget for the worst case (every line break is a paragraph
                // break) so the box never has to grow/shrink as the user types Enter, and never
                // clips text that legitimately needs the paragraph gap at paint time.
                return contentHeightPx + paragraphSpacingPx * (lines - 1);
            }

            var paragraphBreaks = CountVisibleParagraphBreaks(lines);
            if (paragraphBreaks == 0)
                return contentHeightPx;

            return contentHeightPx + paragraphSpacingPx * paragraphBreaks;
        }

        private void UpdateViewportHeight()
        {
            if (_scroll == null)
                return;

            if (HeightRequest >= 0)
            {
                _scroll.HeightRequest = -1;
                return;
            }

            var scale = RenderingScale > 0 ? RenderingScale : 1f;
            var viewportHeightPx = GetAutoVisibleHeightPixels(scale);
            if (viewportHeightPx <= 0)
                return;

            var viewportHeightPoints = viewportHeightPx / scale;
            if (Math.Abs(_scroll.HeightRequest - viewportHeightPoints) < 0.5f)
                return;

            _scroll.HeightRequest = viewportHeightPoints;
            InvalidateMeasure();
        }

        public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            return base.OnMeasuring(widthConstraint, heightConstraint, scale);
        }

        protected override SKSize GetContentSizeForAutosizeInPixels()
        {
            var size = base.GetContentSizeForAutosizeInPixels();
            if (HeightRequest >= 0 || MaxLines <= 0)
                return size;

            var heightPx = GetAutoVisibleHeightPixels(RenderingScale > 0 ? RenderingScale : 1f);
            if (heightPx <= 0)
                return size;

            size.Height = heightPx;
            return size;
        }

        /// <summary>
        /// This is Done or Enter key, so maybe just split lines in specific case
        /// </summary>
        public void Submit()
        {
            if (IsMultiline && !ShouldSubmitOnEnter)
            {
                Text += GetEditorBreakText(false);
            }
            else
            {
                // Multiline Send keeps focus so the user can continue typing the next message.
                ExecuteSubmit(clearFocus: !IsMultiline);
            }
        }

        protected void ExecuteSubmit(bool clearFocus)
        {
            if (clearFocus)
                IsFocused = false;

            TextSubmitted?.Invoke(this, Text);
            CommandOnSubmit?.Execute(Text);
        }

        public bool IsMultiline
        {
            get
            {
                return MaxLines != 1;
            }
        }

        public override bool SetFrameworkFocus(bool focus)
        {
            //base.OnFocusChanged(focus);

            IsFocused = focus;

            return true;
        }

        /// <summary>
        /// Returns the line's glyphs flattened across ALL font-run spans, with line-absolute X
        /// positions and ONE ENTRY PER UTF-16 CODE UNIT. The caret math indexes by code unit
        /// (<see cref="CursorPosition"/> is a code-unit offset into <see cref="Text"/>), so a
        /// multi-unit glyph (surrogate-pair emoji, some CJK) must occupy as many slots as it has
        /// code units — otherwise the line looks shorter than the text and the caret can't reach
        /// past it (e.g. paste "play😉" left the caret stuck at start).
        /// A single-span line whose glyphs are all one code unit (the common ASCII/plain case)
        /// takes a zero-allocation fast path and returns the span's array unchanged.
        /// </summary>
        protected virtual LineGlyph[] GetLineGlyphs(TextLine line)
        {
            if (line?.Spans == null || line.Spans.Count == 0)
                return Array.Empty<LineGlyph>();

            // Fast path: one span, no multi-code-unit glyph → already per-code-unit, line-relative.
            if (line.Spans.Count == 1)
            {
                var only = line.Spans[0].Glyphs ?? Array.Empty<LineGlyph>();
                bool simple = true;
                for (int i = 0; i < only.Length; i++)
                {
                    if (only[i].Length > 1) { simple = false; break; }
                }
                if (simple)
                    return only;
            }

            var result = new List<LineGlyph>();
            float spanOffsetX = 0f;
            foreach (var span in line.Spans)
            {
                var glyphs = span.Glyphs;
                if (glyphs != null)
                {
                    foreach (var g in glyphs)
                    {
                        // make X line-absolute (render adds the span's cumulative offset, not the glyph)
                        var abs = LineGlyph.Move(g, spanOffsetX + g.Position);
                        int units = Math.Max(1, g.Length);
                        for (int u = 0; u < units; u++)
                            result.Add(abs); // one slot per code unit so array index == code-unit offset
                    }
                }
                spanOffsetX += span.Size.Width;
            }

            return result.ToArray();
        }

        /// <summary>
        /// Input in pixels
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        protected int GetCursorPosition(float x, float y)
        {
            //we have all lines position inside Label
            if (Label != null && Label.Lines != null)
            {
                var firstPosInLine = 0;
                foreach (var labelLine in Label.Lines)
                {
                    var posX = x - labelLine.Bounds.Left;
                    var rect = labelLine.Bounds;
                    // rect.Offset(new SKPoint(0, this.HitBoxAuto.Top));

                    var lineGlyphs = GetLineGlyphs(labelLine);

                    if (y >= rect.Top && y <= rect.Bottom) //inside line
                    {
                        if (lineGlyphs.Length == 0)
                            return Text?.Length ?? 0;

                        var posInline = 0;
                        var prevX = 0f;
                        foreach (var charX in lineGlyphs)
                        {
                            //we are checking x vs next char in line, not not current
                            if (prevX <= posX && posX <= charX.Position)
                            {
                                return firstPosInLine + posInline;
                            }
                            prevX = charX.Position;
                            posInline++;
                        }

                        //if we fallen here means we clicked outside the line.
                        var endPos = firstPosInLine + posInline;
                        // If the glyph array included a trailing editor break, step back so cursor lands
                        // at end-of-line rather than at the start of the next line.
                        if (Text != null && endPos > 0 && endPos - 1 < Text.Length && IsEditorBreak(Text[endPos - 1]))
                            endPos--;
                        // clamp — glyph array can have N+1 slots for N chars (end-of-text sentinel)
                        return Math.Min(endPos, Text?.Length ?? endPos);

                    }

                    firstPosInLine = AdvanceLineTextIndex(firstPosInLine, lineGlyphs.Length);
                }

                if (Text != null)
                    return Text.Length;
            }
            return 0;
        }




        private enum SelectionDragMode { None, ExtendFromAnchor, DragHandleStart, DragHandleEnd }
        private SelectionDragMode _selectionDragMode;
        private int _selectionAnchor;

        /// <summary>
        /// Seeds the drawn-head keyboard-selection state (anchor + moving edge) after a pointer-driven
        /// selection so a subsequent Shift+Arrow continues coherently. No-op on native-input heads.
        /// </summary>
        partial void SeedKeyboardSelection(int anchor, int movingEdge);

        // Set by platform code to block spurious out-of-bounds Down events that arrive
        // immediately after keyboard input (e.g. WinUI pointer events after TextChanged).
        protected long _spuriousDownBlockUntilMs;

        public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
        {

            switch (args.Type)
            {
            case TouchActionResult.Up:
            _selectionDragMode = SelectionDragMode.None;
            return this;
            break;

            case TouchActionResult.LongPressing:
            {
                if (!HitIsInside(args.Event.StartingLocation.X, args.Event.StartingLocation.Y))
                    break;

                var lpOffset = TranslateInputCoords(apply.ChildOffset);
                var lpX = args.Event.StartingLocation.X + lpOffset.X;
                var lpY = args.Event.StartingLocation.Y + lpOffset.Y;
                if (_scroll != null)
                {
                    lpX += (float)(_scroll.ViewportOffsetX * RenderingScale);
                    lpY += (float)(_scroll.ViewportOffsetY * RenderingScale);
                }

                var lpPos = GetCursorPosition(lpX, lpY);
                SelectWord(lpPos);
                _selectionDragMode = SelectionDragMode.ExtendFromAnchor;
                _selectionAnchor = CursorPosition;
                SetFocusInternal(true);
                Superview.FocusedChild = this;
                return this;
            }

            case TouchActionResult.Down:

            var thisOffset = TranslateInputCoords(apply.ChildOffset);
            var x = args.Event.Location.X + thisOffset.X;
            var y = args.Event.Location.Y + thisOffset.Y;

            Debug.WriteLine($"[EditorDown] StartXY=({args.Event.StartingLocation.X:F0},{args.Event.StartingLocation.Y:F0}) HitBox={HitBoxAuto} HitIsInside={HitIsInside(args.Event.StartingLocation.X, args.Event.StartingLocation.Y)} IsFocused={IsFocused}");

            // account for scroll offset so hit-testing is in content space
            if (_scroll != null)
            {
                x += (float)(_scroll.ViewportOffsetX * RenderingScale);
                y += (float)(_scroll.ViewportOffsetY * RenderingScale);
            }

            // Handle hit-test: tapping near a selection handle starts drag mode.
            if (SelectionLength > 0 && _selectionControl != null)
            {
                var lhc = _selectionControl.LeftHandleCenter;
                if (lhc != SKPoint.Empty)
                {
                    var hitR = (float)(22 * RenderingScale);
                    var hitR2 = hitR * hitR;
                    float hdx, hdy;
                    hdx = x - lhc.X; hdy = y - lhc.Y;
                    if (hdx * hdx + hdy * hdy <= hitR2)
                    {
                        _selectionDragMode = SelectionDragMode.DragHandleStart;
                        _selectionAnchor = CursorPosition + SelectionLength;
                        SetFocusInternal(true);
                        Superview.FocusedChild = this;
                        return this;
                    }
                    var rhc = _selectionControl.RightHandleCenter;
                    hdx = x - rhc.X; hdy = y - rhc.Y;
                    if (hdx * hdx + hdy * hdy <= hitR2)
                    {
                        _selectionDragMode = SelectionDragMode.DragHandleEnd;
                        _selectionAnchor = CursorPosition;
                        SetFocusInternal(true);
                        Superview.FocusedChild = this;
                        return this;
                    }
                }
            }

            _selectionDragMode = SelectionDragMode.None;
            var pos = GetCursorPosition(x, y);
            Debug.WriteLine($"[EditorDown] x={x:F0} y={y:F0} pos={pos} Text.Length={Text?.Length ?? 0}");

            // Shift+tap extends the selection to the tapped caret position instead of collapsing.
            // Anchor is the existing selection's far edge (kept fixed), or the current caret when
            // there is no selection. Sets ExtendFromAnchor so a following drag keeps extending.
            if (KeyboardManager.IsShiftPressed && IsFocused)
            {
                int anchor;
                if (SelectionLength > 0)
                {
                    int left = CursorPosition;
                    int right = CursorPosition + SelectionLength;
                    anchor = (pos - left) <= (right - pos) ? right : left;
                }
                else
                {
                    anchor = CursorPosition;
                }

                var shiftStart = Math.Min(anchor, pos);
                var shiftEnd = Math.Max(anchor, pos);
                _selectionAnchor = anchor;
                _selectionDragMode = SelectionDragMode.ExtendFromAnchor;
                CursorPosition = shiftStart;
                SelectionLength = shiftEnd - shiftStart;
                SetCursorPositionNative(shiftStart, shiftEnd);
                SeedKeyboardSelection(anchor, pos);
                SetFocusInternal(true);
                Superview.FocusedChild = this;
                return this;
            }

            CursorPosition = pos;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                SelectionLength = 0;
                if (IsFocused)
                {
                    // Already logically focused: the IsFocused BindableProperty callback won't fire
                    // again, but the native sink may have lost the IME meanwhile (Android BACK
                    // dismisses the keyboard without unfocusing; WinUI shifts keyboard focus to the
                    // canvas on tap) — force the native focus + keyboard re-show path directly.
                    SetFocusInternal(true);
                }
                else
                {
                    IsFocused = true;
                }
                Superview.FocusedChild = this;
            });

            return this;
            break;

            case TouchActionResult.Panning:
            if (_selectionDragMode != SelectionDragMode.None)
            {
                var panOffset = TranslateInputCoords(apply.ChildOffset);
                var px = args.Event.Location.X + panOffset.X;
                var py = args.Event.Location.Y + panOffset.Y;
                if (_scroll != null)
                {
                    px += (float)(_scroll.ViewportOffsetX * RenderingScale);
                    py += (float)(_scroll.ViewportOffsetY * RenderingScale);
                }
                var newPos = GetCursorPosition(px, py);
                var selStart = Math.Min(newPos, _selectionAnchor);
                var selEnd = Math.Max(newPos, _selectionAnchor);
                CursorPosition = selStart;
                SelectionLength = selEnd - selStart;
                SetCursorPositionNative(selStart, selEnd);
                return this;
            }
            goto default;

            default:
            if (IsFocused)
            {
                // route to children (scroll) but keep Canvas focus on this editor
                base.ProcessGestures(args, apply);
                return this;
            }
            break;
            }

            return base.ProcessGestures(args, apply);
        }



        protected void SetFocusInternal(bool value)
        {
            Debug.WriteLine($"[Editor] SetFocusInternal {value}");
            if (!value)
            {
                SelectionLength = 0;
                _selectionDragMode = SelectionDragMode.None;
            }

#if BROWSER
            var focusDelayMs = 50;
#else
        var focusDelayMs = 100;
#endif

            // 100 ms: on Android/iOS the IME needs time to connect before ShowSoftInput +
            // SetSelection fire. On Windows, WinUI shifts keyboard focus to the Canvas on
            // every tap — 100 ms lets that settle before we steal it back with Focus(Programmatic).
            // Stray keystrokes during the window are blocked by PlatformClearFocusNow().
            Tasks.StartDelayed(TimeSpan.FromMilliseconds(focusDelayMs), () =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetFocusNative(value);
                    MoveInternalCursor();
                });
            });

            SyncSuperviewFocus(value);

            FocusChanged?.Invoke(this, value);
            CommandOnFocusChanged?.Execute(value);

            UpdateLabel();
        }


        /// <summary>
        /// Sets native control cursor position to CursorPosition (and selection end if active) and calls UpdateCursorVisibility.
        /// </summary>
        /// <summary>
        /// Set to true to suppress the immediate MoveInternalCursor() triggered
        /// by CursorPosition property change while a deferred post-measure update is pending.
        /// </summary>
        protected bool _suppressImmediateCursorMove;

        protected void MoveInternalCursor()
        {
            if (_suppressImmediateCursorMove)
                return;

            if (SelectionLength > 0)
                SetCursorPositionNative(CursorPosition, CursorPosition + SelectionLength);
            else
                SetCursorPositionNative(CursorPosition);

            UpdateCursorVisibility();
        }

        public void SelectAll()
        {
            if (Text == null) return;
            SelectionLength = Text.Length;
            CursorPosition = 0;
            SetCursorPositionNative(0, Text.Length);
        }

        private object lockCursor = new();

        /// <summary>
        /// Positions cursor control where it should be using translation, and sets its visibility.
        /// </summary>
        public virtual void UpdateCursorVisibility()
        {
            lock (lockCursor)
            {
                if (Label != null && Cursor != null)
                {

                    // Clamp to text length — CursorPosition can be stale (16 ms delay on Windows,
                    // 50 ms on Android) while text has already shrunk via native TextBox events.
                    // Without clamping, the glyph loop exits without calling MoveCursorTo and the
                    // cursor stays at its previous pixel position indefinitely.
                    var textLen = Text?.Length ?? 0;
                    var cursorIndex = SelectionLength > 0
                        ? Math.Min(CursorPosition + SelectionLength, textLen)
                        : Math.Min(CursorPosition, textLen);

                    //make cursor fit the line height
                    var lineH = Label.MeasuredLineHeight / RenderingScale;
                    if (lineH <= 0)
                        lineH = (float)GetSingleLineHeightPts();
                    if (lineH > 0)
                        Cursor.HeightRequest = lineH;

                    if (cursorIndex < 0)
                    {
                        CursorPosition = 0;
                        MoveCursorTo(0, 0);
                        SetCursorVisible(IsFocused && CanShowCursor);
                        return;
                    }

                    if (Label.Lines == null || Label.LinesCount <= 0 || string.IsNullOrEmpty(Text))
                    {
                        if (string.IsNullOrEmpty(Text))
                        {
                            // Empty editor: no lines — place cursor at the text insertion point
                            // for the active horizontal alignment so it aligns with placeholder text.
                            var cursorX = 0.0;
                            // Use ViewportConstraintWidth (set from editor's real pixel width in Paint)
                            // so cursor centers within the visible area, not the infinite scroll content.
                            var viewportW = Label.ViewportConstraintWidth > 0
                                ? Label.ViewportConstraintWidth / RenderingScale
                                : Label.DrawingRect.Width / RenderingScale;
                            if (viewportW > 0 && viewportW < SkiaControl.MaxRealPixelSize)
                            {
                                if (HorizontalTextAlignment == DrawTextAlignment.Center)
                                    cursorX = (viewportW - Cursor.WidthRequest) / 2.0;
                                else if (HorizontalTextAlignment == DrawTextAlignment.End)
                                    cursorX = viewportW - Cursor.WidthRequest;
                            }
                            MoveCursorTo(cursorX, 0);
                            SetCursorVisible(IsFocused && CanShowCursor);
                        }
#if !BROWSER
                        else
                        {
                            // On Blazor, Lines are populated during the render pass which happens
                            // after property-change handlers. Hiding cursor here would cause a
                            // 1-frame invisible flicker every keystroke; defer instead (see DeferVisualCursorUpdate).
                            // Text was just changed and label hasn't re-measured yet — hide
                            // cursor briefly; it will be repositioned by the delayed position update.
                            SetCursorVisible(false);
                        }
#endif
                        return;
                    }

                    var index = 0;
                    var line = 0;
                    var endX = 0f;
                    var lastY = 0f;
                    foreach (var labelLine in Label.Lines)
                    {
                        var lineGlyphs = GetLineGlyphs(labelLine);
                        var nextIndex = AdvanceLineTextIndex(index, lineGlyphs.Length);

                        // Check if we're on the last line and the cursor is at the last position
                        if (line == Label.LinesCount - 1 && cursorIndex == index + lineGlyphs.Length)
                        {
                            var endsWithHardLineBreak = cursorIndex > 0
                                && Text != null
                                && cursorIndex == Text.Length
                                && IsEditorBreak(Text[cursorIndex - 1]);

                            double translateX;
                            double translateY;

                            if (endsWithHardLineBreak)
                            {
                                var startX = labelLine.Bounds.Left + (lineGlyphs.Length > 0 ? lineGlyphs[0].Position : 0);
                                translateX = (startX - Label.DrawingRect.Left) / RenderingScale;
                                translateY = (labelLine.Bounds.Bottom - Label.DrawingRect.Top) / RenderingScale;
                            }
                            else
                            {
                                translateX = (labelLine.Bounds.Right - Label.DrawingRect.Left) / RenderingScale;
                                translateY = (labelLine.Bounds.Top - Label.DrawingRect.Top) / RenderingScale;
                            }

                            MoveCursorTo(translateX, translateY);
                            break;
                        }

                        if (cursorIndex == nextIndex && nextIndex > index + lineGlyphs.Length)
                        {
                            var nextLine = line + 1 < Label.LinesCount ? Label.Lines[line + 1] : null;
                            if (nextLine != null)
                            {
                                var nextGlyphs = GetLineGlyphs(nextLine);
                                var startX = nextLine.Bounds.Left + (nextGlyphs.Length > 0 ? nextGlyphs[0].Position : 0);
                                MoveCursorTo((startX - Label.DrawingRect.Left) / RenderingScale,
                                    (nextLine.Bounds.Top - Label.DrawingRect.Top) / RenderingScale);
                                break;
                            }
                        }

                        if (cursorIndex == nextIndex - 1 && nextIndex > index + lineGlyphs.Length)
                        {
                            MoveCursorTo((labelLine.Bounds.Right - Label.DrawingRect.Left) / RenderingScale,
                                (labelLine.Bounds.Top - Label.DrawingRect.Top) / RenderingScale);
                            break;
                        }

                        if (cursorIndex < index + lineGlyphs.Length)
                        {

                            if (line > 0 && cursorIndex - index == 0)
                            {
                                var isHardLineBreak = cursorIndex > 0
                                    && Text != null
                                    && cursorIndex - 1 < Text.Length
                                    && IsEditorBreak(Text[cursorIndex - 1]);

                                if (isHardLineBreak)
                                {
                                    var startX = labelLine.Bounds.Left + (lineGlyphs.Length > 0 ? lineGlyphs[0].Position : 0);
                                    MoveCursorTo((startX - Label.DrawingRect.Left) / RenderingScale,
                                        (labelLine.Bounds.Top - Label.DrawingRect.Top) / RenderingScale);
                                }
                                else
                                {
                                    MoveCursorTo((endX - Label.DrawingRect.Left) / RenderingScale,
                                        (lastY - Label.DrawingRect.Top) / RenderingScale);
                                }
                                break;
                            }

                            var x = labelLine.Bounds.Left + lineGlyphs[cursorIndex - index].Position;
                            var y = labelLine.Bounds.Top;

                            MoveCursorTo((x - Label.DrawingRect.Left) / RenderingScale, (y - Label.DrawingRect.Top) / RenderingScale);
                            break;
                        }

                        endX = labelLine.Bounds.Right;
                        lastY = labelLine.Bounds.Top;
                        line++;
                        index = nextIndex;
                    }

                    SetCursorVisible(IsFocused && CanShowCursor);

                    if (_selectionControl != null)
                    {
                        _selectionControl.SelectionStart = CursorPosition;
                        _selectionControl.SelectionLength = SelectionLength;
                        _selectionControl.SelectionColor = SelectionColor;
                        _selectionControl.Invalidate();
                    }
                }

            }

        }

        protected void SetCursorVisible(bool show)
        {
            if (show)
            {
                Cursor.Opacity = 1;
                if (!Cursor.IsVisible)
                    Cursor.Invalidate();
                Cursor.IsVisible = true;
            }
            else
            {
                Cursor.IsVisible = false;
            }
        }

        /// <summary>
        /// Translate cursor from the left top corner, params in pts.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        protected virtual void MoveCursorTo(double x, double y)
        {
            var moved = Cursor.Left != x || Cursor.Top != y;

            Cursor.Left = x;
            Cursor.Top = y;
            // BindableProperty skips propertyChanged when value equals the default (0.0).
            // At cursor position 0 both Left and Top are 0, so NeedRepaint never fires.
            // Force a repaint so the cursor always redraws at the new position.
            if (moved)
            {
                Cursor.OnMoved();
                OnCursorMoved();
            }
            else
            {
                Cursor.Repaint();
            }

            // scroll to keep cursor visible
            if (_scroll != null)
            {
                var cursorRight = x + Cursor.WidthRequest;
                var cursorBottom = y + Cursor.HeightRequest;

                if (IsMultiline)
                {
                    // vertical scroll: ensure cursor Y is visible
                    var viewH = _scroll.Height;
                    var offsetY = _scroll.ViewportOffsetY;
                    if (y < -offsetY)
                        _scroll.ScrollTo((float)_scroll.ViewportOffsetX, (float)-y, 0.1f, true);
                    else if (cursorBottom > -offsetY + viewH)
                        _scroll.ScrollTo((float)_scroll.ViewportOffsetX, (float)-(cursorBottom - viewH), 0.1f, true);
                }
                else
                {
                    // horizontal scroll: ensure cursor X is visible
                    var viewW = _scroll.Width;
                    var offsetX = _scroll.ViewportOffsetX;
                    if (x < -offsetX)
                    {
                        // cursor off left edge — scroll right to show it
                        _scroll.ScrollTo((float)-x, (float)_scroll.ViewportOffsetY, 0.1f, true);
                    }
                    else if (cursorRight > -offsetX + viewW)
                    {
                        // cursor off right edge — scroll left to show it
                        _scroll.ScrollTo((float)-(cursorRight - viewW), (float)_scroll.ViewportOffsetY, 0.1f, true);
                    }
                    else if (offsetX < 0)
                    {
                        // cursor is visible but viewport is scrolled right further than needed
                        // (e.g. text was deleted) — scroll back left as far as possible while
                        // keeping cursor in view
                        var idealOffsetX = (float)(cursorRight > viewW ? -(cursorRight - viewW) : 0);
                        if (idealOffsetX > offsetX)
                            _scroll.ScrollTo(idealOffsetX, (float)_scroll.ViewportOffsetY, 0.1f, true);
                    }
                }
            }

            Invalidate();
        }

        /// <summary>
        /// Raises the <see cref="CursorMoved"/> event.
        /// </summary>
        protected virtual void OnCursorMoved()
        {
            CursorMoved?.Invoke(this, EventArgs.Empty);
        }

        private int AdvanceLineTextIndex(int currentIndex, int glyphCount)
        {
            var nextIndex = currentIndex + glyphCount;
            // KeepSpacesOnLineBreaks causes the last glyph to be a synthetic space that
            // covers the editor break separator in the text. When that happens, the separator is
            // already consumed and must NOT be skipped a second time.
            var lastGlyphIsNewline = glyphCount > 0
                && Text != null
                && nextIndex - 1 < Text.Length
                && IsEditorBreak(Text[nextIndex - 1]);
            if (!lastGlyphIsNewline && Text != null && nextIndex < Text.Length && IsEditorBreak(Text[nextIndex]))
            {
                nextIndex++;
            }
            return nextIndex;
        }

        /// <summary>Returns the zero-based line index the cursor is currently on.</summary>
        public int GetCursorLine()
        {
            if (Label?.Lines == null || Label.LinesCount == 0)
                return 0;

            var cursorIndex = CursorPosition;
            var textIndex = 0;
            var lineNum = 0;

            foreach (var labelLine in Label.Lines)
            {
                var lineGlyphs = GetLineGlyphs(labelLine);
                var nextIndex = AdvanceLineTextIndex(textIndex, lineGlyphs.Length);

                if (cursorIndex < nextIndex || lineNum == Label.LinesCount - 1)
                    return lineNum;

                textIndex = nextIndex;
                lineNum++;
            }

            return lineNum;
        }


        // Implemented per platform: release the native control from input focus without
        // closing the soft keyboard (another editor will steal input).
        partial void PlatformClearFocusNow();

        protected virtual void HandleVerticalArrow(bool up)
        {
            if (Label?.Lines == null || Label.LinesCount <= 1) return;

            var curLine = GetCursorLine();
            var targetLine = up ? curLine - 1 : curLine + 1;

            if (targetLine < 0 || targetLine >= Label.LinesCount) return;

            var cursorXPixels = (float)(Cursor.Left * RenderingScale);

            var lineStart = 0;
            for (var i = 0; i < targetLine; i++)
                lineStart = AdvanceLineTextIndex(lineStart, GetLineGlyphs(Label.Lines[i]).Length);

            var glyphs = GetLineGlyphs(Label.Lines[targetLine]);
            if (glyphs.Length == 0)
            {
                CursorPosition = lineStart;
                return;
            }

            var prevX = 0f;
            for (var i = 0; i < glyphs.Length; i++)
            {
                if (prevX <= cursorXPixels && cursorXPixels <= glyphs[i].Position)
                {
                    CursorPosition = lineStart + i;
                    return;
                }
                prevX = glyphs[i].Position;
            }

            var pos = lineStart + glyphs.Length;
            if (Text != null && pos > 0 && pos - 1 < Text.Length && IsEditorBreak(Text[pos - 1]))
                pos--;
            CursorPosition = pos;
        }


        protected virtual void OnControlCreated()
        {

        }

        public static readonly BindableProperty CursorPositionProperty = BindableProperty.Create(
            nameof(CursorPosition),
            typeof(int),
            typeof(SkiaEditor),
            -1, propertyChanged: OnNeedUpdateSelection);

        private static void OnNeedUpdateSelection(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor control)
            {
                control.MoveInternalCursor();
            }
        }

        public int CursorPosition
        {
            get { return (int)GetValue(CursorPositionProperty); }
            set { SetValue(CursorPositionProperty, value); }
        }


 

        protected RestartingTimer<int> TimerUpdateParentCursorPosition;

        /// <summary>
        /// We have to sync with a delay after text was changed otherwise the cursor position is not updated yet.
        /// Using restarting timer, every time this is called the timer is reset if callback wasn't executed yet.
        /// </summary>
        /// <param name="ms"></param>
        /// <param name="position"></param>
        protected void SetCursorPositionWithDelay(int ms, int position)
        {
            if (TimerUpdateParentCursorPosition == null)
            {
                TimerUpdateParentCursorPosition = new(TimeSpan.FromMilliseconds(ms), (arg) =>
                {
                    CursorPosition = arg;
                    //Debug.WriteLine("CursorPosition from native: " + arg);
                });
                TimerUpdateParentCursorPosition.Start(position);
            }
            else
            {
                TimerUpdateParentCursorPosition.Restart(position);
            }
        }

        public virtual void SetSelection(int start, int end)
        {
            if (Label != null)
            {
                MoveInternalCursor();
            }
        }

        protected (int start, int end) GetWordBoundaries(int charIndex)
        {
            var text = Text;
            if (string.IsNullOrEmpty(text) || charIndex < 0 || charIndex >= text.Length)
                return (charIndex, charIndex);

            if (!char.IsLetterOrDigit(text[charIndex]))
                return (charIndex, charIndex);

            var start = charIndex;
            while (start > 0 && char.IsLetterOrDigit(text[start - 1]))
                start--;

            var end = charIndex;
            while (end < text.Length && char.IsLetterOrDigit(text[end]))
                end++;

            return (start, end);
        }

        public virtual void SelectWord(int charIndex)
        {
            var (start, end) = GetWordBoundaries(charIndex);
            if (end > start)
            {
                CursorPosition = start;
                SelectionLength = end - start;
                SetCursorPositionNative(start, end);
            }
        }

        public string GetSelectedText()
        {
            if (IsPassword || SelectionLength <= 0) return null;
            var text = Text ?? string.Empty;
            var start = Math.Clamp(CursorPosition, 0, text.Length);
            var len = Math.Clamp(SelectionLength, 0, text.Length - start);
            return len > 0 ? text.Substring(start, len) : null;
        }

        /// <summary>
        /// Number of UTF-16 code units to remove for <paramref name="count"/> grapheme clusters
        /// ending at <paramref name="caret"/> (backspace). Grapheme-aware so a surrogate-pair emoji
        /// (😉), a ZWJ sequence (👨‍👩‍👧) or a skin-toned emoji (👍🏽) is deleted whole — never split
        /// into an orphaned surrogate that corrupts the string (and the markdown/rich-label parser).
        /// </summary>
        public static int CodeUnitsBeforeCaret(string text, int caret, int count)
        {
            if (string.IsNullOrEmpty(text) || caret <= 0 || count <= 0)
                return 0;

            caret = Math.Min(caret, text.Length);
            var starts = new List<int>();
            var e = System.Globalization.StringInfo.GetTextElementEnumerator(text);
            while (e.MoveNext())
            {
                if (e.ElementIndex >= caret)
                    break;
                starts.Add(e.ElementIndex);
            }

            if (starts.Count == 0)
                return caret; // caret sits inside the first element (mid-surrogate) — remove up to it

            int idx = starts.Count - count;
            int start = idx > 0 ? starts[idx] : 0;
            return caret - start;
        }

        /// <summary>
        /// Number of UTF-16 code units to remove for <paramref name="count"/> grapheme clusters
        /// starting at <paramref name="caret"/> (forward delete). See <see cref="CodeUnitsBeforeCaret"/>.
        /// </summary>
        public static int CodeUnitsAfterCaret(string text, int caret, int count)
        {
            if (string.IsNullOrEmpty(text) || caret >= text.Length || count <= 0)
                return 0;

            caret = Math.Max(caret, 0);
            int pos = caret;
            for (int n = 0; n < count && pos < text.Length; n++)
                pos += System.Globalization.StringInfo.GetNextTextElement(text, pos).Length;

            return pos - caret;
        }

        public void DeleteSelection()
        {
            if (SelectionLength <= 0) return;
            var text = Text ?? string.Empty;
            var start = Math.Clamp(CursorPosition, 0, text.Length);
            var len = Math.Clamp(SelectionLength, 0, text.Length - start);
            if (len == 0) return;
            Text = text.Remove(start, len);
            SelectionLength = 0;
            OnSelectionDeleted();
        }

        public void InsertAtCursor(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            var existing = Text ?? string.Empty;
            var start = Math.Clamp(CursorPosition, 0, existing.Length);
            var len = Math.Clamp(SelectionLength, 0, existing.Length - start);
            var normalized = NormalizeEditorInput(value);
            Text = existing.Remove(start, len).Insert(start, normalized);
            _suppressImmediateCursorMove = true;
            CursorPosition = start + normalized.Length;
            SelectionLength = 0;
            OnTextInsertedAtCursor();
        }

#if !BROWSER && !DRAWNUI_NET
        public void CutSelection()
        {
            CopySelection();
            DeleteSelection();
        }
#elif DRAWNUI_NET
        // Net target: no clipboard; stubs satisfy the interface contract.
        public void CutSelection() => DeleteSelection();
        public void CopySelection() { }
        public void PasteFromClipboard() { }
#else
        // BROWSER: CutSelection here; CopySelection/PasteFromClipboard in SkiaEditor.Blazor.cs.
        public void CutSelection()
        {
            CopySelection();
            DeleteSelection();
        }
#endif

#if !BROWSER && !DRAWNUI_NET
        public void CopySelection()
        {
            var text = GetSelectedText();
            if (string.IsNullOrEmpty(text)) return;
            _ = Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.SetTextAsync(text);
        }

        public void PasteFromClipboard()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var text = await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.GetTextAsync();
                    if (!string.IsNullOrEmpty(text))
                    {
                        InsertAtCursor(text);
                        SetFocusNative(true);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[SkiaEditor] PasteFromClipboard: {e}");
                }
            });
        }
#endif

        partial void OnSelectionDeleted();

        partial void OnTextInsertedAtCursor();

        public override void OnDisposing()
        {
            DisposePlatform();

            base.OnDisposing();
        }

        protected override void OnLayoutChanged()
        {
            base.OnLayoutChanged();

            if (DefaultContentCreated)
                UpdateNativePosition();

            if (_pendingSuperviewFocusSync && Superview != null)
            {
                _pendingSuperviewFocusSync = false;
                if (IsFocused)
                {
                    // The focus attempt at property-change time had no Superview/handler:
                    // native focus silently no-oped. Redo both halves now that we're attached.
                    SetFocusInternal(true);
                    SyncSuperviewFocus(true);
                }
            }
        }

        #endregion

        #region PROPERTIES

        public enum SkiaEditorKeyboard
        {
            Default,
            Numeric,
            Decimal,
            Phone,
            Email
        }

        public static readonly BindableProperty ReturnTypeProperty = BindableProperty.Create(
            nameof(ReturnType),
            typeof(ReturnType),
            typeof(SkiaEditor),
            ReturnType.Done);

        /// <summary>
        /// Return key action. On soft keyboards selects the action key label.
        /// On a multiline editor, <see cref="ReturnType.Send"/> changes Enter semantics:
        /// Enter submits, Shift+Enter inserts a line break. All other values keep
        /// Enter inserting a line break.
        /// </summary>
        public ReturnType ReturnType
        {
            get { return (ReturnType)GetValue(ReturnTypeProperty); }
            set { SetValue(ReturnTypeProperty, value); }
        }

        /// <summary>
        /// True when an unmodified Enter should submit instead of inserting a line break:
        /// multiline editor with <see cref="ReturnType.Send"/>.
        /// </summary>
        protected bool ShouldSubmitOnEnter => IsMultiline && ReturnType == ReturnType.Send;

        public static readonly BindableProperty KeyboardTypeProperty = BindableProperty.Create(
            nameof(KeyboardType),
            typeof(SkiaEditorKeyboard),
            typeof(SkiaEditor),
            SkiaEditorKeyboard.Default,
            propertyChanged: (b, o, n) => { if (b is SkiaEditor e && e.IsFocused) e.ApplyKeyboardType(); });

        public SkiaEditorKeyboard KeyboardType
        {
            get { return (SkiaEditorKeyboard)GetValue(KeyboardTypeProperty); }
            set { SetValue(KeyboardTypeProperty, value); }
        }

        public static readonly BindableProperty IsPasswordProperty = BindableProperty.Create(
            nameof(IsPassword),
            typeof(bool),
            typeof(SkiaEditor),
            false,
            propertyChanged: (b, o, n) =>
            {
                if (b is SkiaEditor e)
                {
                    e.UpdateLabel();
                    if (e.IsFocused) e.ApplyKeyboardType();
                }
            });

        public bool IsPassword
        {
            get { return (bool)GetValue(IsPasswordProperty); }
            set { SetValue(IsPasswordProperty, value); }
        }

        public static readonly BindableProperty IsSpellCheckEnabledProperty = BindableProperty.Create(
            nameof(IsSpellCheckEnabled),
            typeof(bool),
            typeof(SkiaEditor),
            true,
            propertyChanged: (b, o, n) => { if (b is SkiaEditor e && e.IsFocused) e.ApplyKeyboardType(); });

        /// <summary>
        /// Whether the platform spell checker may underline misspelled words and offer
        /// correction suggestions while typing. Default is <c>true</c>.
        /// Set to <c>false</c> for fields where suggestions are unwanted (usernames, codes, tags).
        /// Applied to the hidden native input on MAUI targets (Android, iOS/MacCatalyst, Windows);
        /// ignored on targets that have no native text input (Blazor, .NET/OpenTK) since there is
        /// no platform spell checker there. Takes effect on the next focus when changed while unfocused.
        /// </summary>
        public bool IsSpellCheckEnabled
        {
            get { return (bool)GetValue(IsSpellCheckEnabledProperty); }
            set { SetValue(IsSpellCheckEnabledProperty, value); }
        }

        public static readonly BindableProperty CommandOnSubmitProperty = BindableProperty.Create(
            nameof(CommandOnSubmit),
            typeof(ICommand),
            typeof(SkiaEditor),
            null);

        public ICommand CommandOnSubmit
        {
            get { return (ICommand)GetValue(CommandOnSubmitProperty); }
            set { SetValue(CommandOnSubmitProperty, value); }
        }

        public static readonly BindableProperty CommandOnFocusChangedProperty = BindableProperty.Create(
            nameof(CommandOnFocusChanged),
            typeof(ICommand),
            typeof(SkiaEditor),
            null);

        public ICommand CommandOnFocusChanged
        {
            get { return (ICommand)GetValue(CommandOnFocusChangedProperty); }
            set { SetValue(CommandOnFocusChangedProperty, value); }
        }

        public static readonly BindableProperty CommandOnTextChangedProperty = BindableProperty.Create(
            nameof(CommandOnTextChanged),
            typeof(ICommand),
            typeof(SkiaEditor),
            null);

        public ICommand CommandOnTextChanged
        {
            get { return (ICommand)GetValue(CommandOnTextChangedProperty); }
            set { SetValue(CommandOnTextChangedProperty, value); }
        }

        public static readonly BindableProperty UseMarkdownProperty = BindableProperty.Create(
            nameof(UseMarkdown),
            typeof(bool),
            typeof(SkiaEditor),
            false,
            propertyChanged: OnUseMarkdownChanged);

        private static void OnUseMarkdownChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor control)
            {
                control.RecreateLabelIfNeeded();
                control.UpdateLabel();
            }
        }

        public bool UseMarkdown
        {
            get { return (bool)GetValue(UseMarkdownProperty); }
            set { SetValue(UseMarkdownProperty, value); }
        }

        public static readonly BindableProperty UseUnicodeProperty = BindableProperty.Create(
            nameof(UseUnicode),
            typeof(bool),
            typeof(SkiaEditor),
            true,
            propertyChanged: OnUseMarkdownChanged);

        /// <summary>
        /// Renders emoji and other unicode glyphs the main font lacks (via per-glyph font fallback)
        /// WITHOUT parsing markdown — text stays literal. Uses a <see cref="SkiaRichLabel"/> with
        /// MarkdownEnabled=false. Ignored when <see cref="UseMarkdown"/> is true (markdown already
        /// includes the same fallback). Leave both false for the lightest plain single-font label.
        /// </summary>
        public bool UseUnicode
        {
            get { return (bool)GetValue(UseUnicodeProperty); }
            set { SetValue(UseUnicodeProperty, value); }
        }


        public static readonly BindableProperty CanShowCursorProperty = BindableProperty.Create(
            nameof(CanShowCursor),
            typeof(bool),
            typeof(SkiaEditor),
            true);

        public bool CanShowCursor
        {
            get { return (bool)GetValue(CanShowCursorProperty); }
            set { SetValue(CanShowCursorProperty, value); }
        }

        public new static readonly BindableProperty IsFocusedProperty = BindableProperty.Create(
            nameof(IsFocused),
            typeof(bool),
            typeof(SkiaEditor),
            false,
            BindingMode.TwoWay,
            propertyChanged: OnNeedChangeFocus);

        private static void OnNeedChangeFocus(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor control)
            {
                control.SetFocusInternal((bool)newvalue);
            }
        }

        private bool _pendingSuperviewFocusSync;

        /// <summary>
        /// Mirrors programmatic IsFocused changes into Canvas.FocusedChild so code-behind focus
        /// behaves like the tap path. The canvas routes desktop keyboard input through
        /// FocusedChild (Net/OpenTK) and uses it for unfocus-on-outside-tap and keyboard
        /// adaptation — without this, programmatic focus shows a blinking cursor but typing is dead.
        /// </summary>
        private void SyncSuperviewFocus(bool focus)
        {
            var super = Superview;
            if (super == null)
            {
                // Not attached yet (focus set in page constructor) — reapply when layout is ready.
                _pendingSuperviewFocusSync = true;
                return;
            }

            if (focus)
                super.FocusedChild = this;
            else if (super.FocusedChild == this)
                super.FocusedChild = null;
        }

        public new bool IsFocused
        {
            get { return (bool)GetValue(IsFocusedProperty); }
            set { SetValue(IsFocusedProperty, value); }
        }

        private static void OnNeedUpdateText(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor control)
            {
                control.UpdateLabel();
            }
        }

        public static readonly BindableProperty TextProperty = BindableProperty.Create(
            nameof(Text),
            typeof(string),
            typeof(SkiaEditor),
            default(string),
            BindingMode.TwoWay,
            propertyChanged: OnControlTextChanged);


        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }
        private static void OnControlTextChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor control)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    control.TextChanged?.Invoke(control, (string)newvalue);
                    control.CommandOnTextChanged?.Execute((string)newvalue);
                    control.SyncNativeText();
                    OnNeedUpdateText(bindable, oldvalue, newvalue);
                });
            }
        }

        // Push a programmatic Text change into the platform's native input control.
        // Without this the native control keeps the old text while unfocused/focused and
        // resurrects it on the next native text event (e.g. clearing a chat input, then the
        // next keystroke brings the old message back). No-op where the platform guard flag
        // indicates the change originated from the native control itself.
        partial void SyncNativeText();

        public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize),
            typeof(double), typeof(SkiaEditor), 12.0,
            propertyChanged: OnNeedUpdateText);

        public double FontSize
        {
            get { return (double)GetValue(FontSizeProperty); }
            set { SetValue(FontSizeProperty, value); }
        }

        public static readonly BindableProperty HorizontalTextAlignmentProperty = BindableProperty.Create(
            nameof(HorizontalTextAlignment),
            typeof(DrawTextAlignment),
            typeof(SkiaEditor),
            defaultValue: DrawTextAlignment.Start,
            propertyChanged: OnNeedUpdateText);

        public DrawTextAlignment HorizontalTextAlignment
        {
            get { return (DrawTextAlignment)GetValue(HorizontalTextAlignmentProperty); }
            set { SetValue(HorizontalTextAlignmentProperty, value); }
        }

        public static readonly BindableProperty VerticalTextAlignmentProperty = BindableProperty.Create(
            nameof(VerticalTextAlignment),
            typeof(TextAlignment),
            typeof(SkiaEditor),
            defaultValue: TextAlignment.Start,
            propertyChanged: OnNeedUpdateText);

        public TextAlignment VerticalTextAlignment
        {
            get { return (TextAlignment)GetValue(VerticalTextAlignmentProperty); }
            set { SetValue(VerticalTextAlignmentProperty, value); }
        }


        public static readonly BindableProperty LineHeightProperty = BindableProperty.Create(
            nameof(LineHeight),
            typeof(double),
            typeof(SkiaEditor),
            1.0,
            propertyChanged: OnNeedUpdateText);

        public double LineHeight
        {
            get { return (double)GetValue(LineHeightProperty); }
            set { SetValue(LineHeightProperty, value); }
        }

        public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily),
            typeof(string), typeof(SkiaEditor), string.Empty, propertyChanged: OnNeedUpdateText);
        public string FontFamily
        {
            get { return (string)GetValue(FontFamilyProperty); }
            set { SetValue(FontFamilyProperty, value); }
        }

        public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
            nameof(TextColor), typeof(Color), typeof(SkiaEditor),
            Colors.Black,
            propertyChanged: OnNeedUpdateText);
        public Color TextColor
        {
            get { return (Color)GetValue(TextColorProperty); }
            set { SetValue(TextColorProperty, value); }
        }

        public static readonly BindableProperty FontWeightProperty = BindableProperty.Create(
            nameof(FontWeight),
            typeof(int),
            typeof(SkiaEditor),
            0,
            propertyChanged: OnNeedUpdateText);

        public int FontWeight
        {
            get { return (int)GetValue(FontWeightProperty); }
            set { SetValue(FontWeightProperty, value); }
        }


        public static readonly BindableProperty TextGradientProperty = BindableProperty.Create(
            nameof(TextGradient),
            typeof(SkiaGradient),
            typeof(SkiaEditor),
            null,
            propertyChanged: OnNeedUpdateText);

        public SkiaGradient TextGradient
        {
            get { return (SkiaGradient)GetValue(TextGradientProperty); }
            set { SetValue(TextGradientProperty, value); }
        }

        public static readonly BindableProperty CursorColorProperty = BindableProperty.Create(
            nameof(CursorColor),
            typeof(Color),
            typeof(SkiaEditor),
            Colors.Black,
            propertyChanged: OnNeedUpdateText);

        public Color CursorColor
        {
            get { return (Color)GetValue(CursorColorProperty); }
            set { SetValue(CursorColorProperty, value); }
        }


        public static readonly BindableProperty CursorGradientProperty = BindableProperty.Create(
            nameof(CursorGradient),
            typeof(SkiaGradient),
            typeof(SkiaEditor),
            null,
            propertyChanged: OnNeedUpdateText);


        public SkiaGradient CursorGradient
        {
            get { return (SkiaGradient)GetValue(CursorGradientProperty); }
            set { SetValue(CursorGradientProperty, value); }
        }


        public static readonly BindableProperty SelectionLengthProperty = BindableProperty.Create(
            nameof(SelectionLength),
            typeof(int),
            typeof(SkiaEditor),
            0,
            propertyChanged: OnSelectionChanged);

        private static void OnSelectionChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor control)
            {
                control.UpdateCursorVisibility();
            }
        }

        public int SelectionLength
        {
            get { return (int)GetValue(SelectionLengthProperty); }
            set { SetValue(SelectionLengthProperty, value); }
        }

        public static readonly BindableProperty SelectionColorProperty = BindableProperty.Create(
            nameof(SelectionColor),
            typeof(Color),
            typeof(SkiaEditor),
            Color.FromArgb("#5590CFFE"),
            propertyChanged: OnNeedUpdateText);

        public Color SelectionColor
        {
            get { return (Color)GetValue(SelectionColorProperty); }
            set { SetValue(SelectionColorProperty, value); }
        }

        public static readonly BindableProperty PlaceholderTextProperty = BindableProperty.Create(
            nameof(PlaceholderText),
            typeof(string),
            typeof(SkiaEditor),
            null,
            propertyChanged: OnPlaceholderChanged);

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public static readonly BindableProperty PlaceholderColorProperty = BindableProperty.Create(
            nameof(PlaceholderColor),
            typeof(Color),
            typeof(SkiaEditor),
            Colors.Gray,
            propertyChanged: OnPlaceholderChanged);

        public Color PlaceholderColor
        {
            get => (Color)GetValue(PlaceholderColorProperty);
            set => SetValue(PlaceholderColorProperty, value);
        }

        public static readonly BindableProperty PlaceholderHorizontalAlignmentProperty = BindableProperty.Create(
            nameof(PlaceholderHorizontalAlignment),
            typeof(DrawTextAlignment),
            typeof(SkiaEditor),
            DrawTextAlignment.Start,
            propertyChanged: OnPlaceholderChanged);

        public DrawTextAlignment PlaceholderHorizontalAlignment
        {
            get => (DrawTextAlignment)GetValue(PlaceholderHorizontalAlignmentProperty);
            set => SetValue(PlaceholderHorizontalAlignmentProperty, value);
        }

        private static void OnPlaceholderChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor e)
                e.UpdatePlaceholder();
        }

        private static void OnMaxLinesChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaEditor control)
            {
                control.UpdateLabel();
                if (control.HeightRequest < 0)
                    control.InvalidateMeasure();
            }
        }

        public static readonly BindableProperty MaxLinesProperty = BindableProperty.Create(nameof(MaxLines),
            typeof(int), typeof(SkiaEditor), 1, propertyChanged: OnMaxLinesChanged);
        public int MaxLines
        {
            get { return (int)GetValue(MaxLinesProperty); }
            set { SetValue(MaxLinesProperty, value); }
        }

        public static readonly BindableProperty AutoHeightProperty = BindableProperty.Create(nameof(AutoHeight),
            typeof(bool), typeof(SkiaEditor), false, propertyChanged: OnMaxLinesChanged);

        /// <summary>
        /// When true and the editor is multiline, the visible height follows the actual number
        /// of text lines (starting at 1) instead of always reserving MaxLines lines.
        /// Growth stops at MaxLines, then content scrolls. Ignored when HeightRequest is set.
        /// </summary>
        public bool AutoHeight
        {
            get { return (bool)GetValue(AutoHeightProperty); }
            set { SetValue(AutoHeightProperty, value); }
        }



        #endregion

        #region LOCALIZE

        public static string ActionGo = "Go";
        public static string ActionNext = "Next";
        public static string ActionSend = "Send";
        public static string ActionSearch = "Search";
        public static string ActionDone = "Done";

        #endregion

#if (!ANDROID && !IOS && !MACCATALYST && !WINDOWS && !TIZEN && !DRAWNUI_NET && !BROWSER)


        public void SetCursorPositionNative(int position, int stop = -1)
        {
            throw new NotImplementedException();
        }

        public void DisposePlatform()
        {
            throw new NotImplementedException();
        }

        public void SetFocusNative(bool focus)
        {
            throw new NotImplementedException();
        }

        public void UpdateNativePosition()
        {
            throw new NotImplementedException();
        }

        public void ApplyKeyboardType() { }

#endif

    }
}
