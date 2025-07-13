using AppoMobi.Specials;
using DrawnUi.Draw;
using System.Diagnostics;
using System.Windows.Input;

namespace DrawnUi.Draw
{
    public partial class SkiaEditor : SkiaLayout, ISkiaGestureListener
    {
        public SkiaEditor()
        {
        }

        public override bool WillClipBounds => true;

        public override void OnWillDisposeWithChildren()
        {
            base.OnWillDisposeWithChildren();

            TextChanged = null;
            FocusChanged = null;
            TextSubmitted = null;
        }

        #region EVENTS

        public event EventHandler<string> TextChanged;

        public event EventHandler<bool> FocusChanged;

        public event EventHandler<string> TextSubmitted;

        #endregion

        #region CHILDREN

        public SkiaLabel CreateLabel()
        {
            var label = new SkiaLabel
            {
                //BackgroundColor = Colors.Red,
                // VerticalOptions = LayoutOptions.Center,
                KeepSpacesOnLineBreaks = true,
                Margin = new Thickness(0, 0, 4, 0), //leave side space for cursor todo 4 as property
            };

            return OnCreatingLabel(label);
        }

        public virtual void UpdateLabel()
        {
            if (Label != null)
            {
                Label.Text = Text;
                Label.FontFamily = FontFamily;
                Label.FontSize = FontSize;
                Label.TextColor = this.TextColor;
                Label.FontWeight = this.FontWeight;
                Label.FillGradient = this.TextGradient;

                Label.HorizontalOptions = this.AlignContentHorizontal;
                Label.VerticalOptions = this.AlignContentVertical;

                Label.HorizontalTextAlignment = this.HorizontalTextAlignment;
                Label.VerticalTextAlignment = this.VerticalTextAlignment;
                Label.LineHeight = this.LineHeight;

                Cursor.Color = this.CursorColor;

                UpdateCursorVisibility();

                Invalidate();
            }
        }

        protected virtual SkiaLabel OnCreatingLabel(SkiaLabel label)
        {
            return label;
        }

        public virtual void CreateControl()
        {
            Label = CreateLabel();

            Cursor = new()
            {
                ZIndex = 1,
                //BackgroundColor = Colors.Red,
                WidthRequest = 1,
                Margin = new Thickness(1),
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Fill,
                IsVisible = false
            };

            AddSubView(Label);
            AddSubView(Cursor);

            OnControlCreated();

            UpdateLabel();
        }

        public SkiaLabel Label { get; protected set; }

        public SkiaCursor Cursor { get; protected set; }

        #endregion

        #region ENGINE

        /// <summary>
        /// This is Done or Enter key, so maybe just split lines in specific case
        /// </summary>
        public void Submit()
        {
            if (IsMultiline)
            {
                Text += Environment.NewLine;
            }
            else
            {
                IsFocused = false;
                TextSubmitted?.Invoke(this, Text);
                CommandOnSubmit?.Execute(Text);
            }
        }

        public bool IsMultiline
        {
            get
            {
                return MaxLines != 1;
            }
        }

        public override bool OnFocusChanged(bool focus)
        {
            //base.OnFocusChanged(focus);

            if (focus)
            {
                SetFocus(true);
            }
            else
            {
                SetFocus(false);
            }

            FocusChanged?.Invoke(this, focus);
            CommandOnFocusChanged?.Execute(focus);

            return true;
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
                Debug.WriteLine($"GetCursorPosition exec fror '{Label.Text}', allowed 0-{Label.Text.Length}");

                var firstPosInLine = 0;
                var line = 0;
                foreach (var labelLine in Label.Lines)
                {
                    var posX = x - labelLine.Bounds.Left;
                    var rect = labelLine.Bounds;
                    // rect.Offset(new SKPoint(0, this.HitBoxAuto.Top));

                    if (y >= rect.Top && y <= rect.Bottom) //inside line
                    {
                        var posInline = 0;
                        var prevX = 0f;
                        foreach (var charX in labelLine.Spans.First().Glyphs)
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
                        return firstPosInLine + posInline;

                    }

                    line++;
                    firstPosInLine += labelLine.Spans.First().Glyphs.Length;
                }

                if (Text != null)
                    return Text.Length - 1;
            }
            return 0;
        }




        public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
        {

            switch (args.Type)
            {
            case TouchActionResult.Up:
            return this;
            break;

            case TouchActionResult.Down:

            var thisOffset = TranslateInputCoords(apply.ChildOffset);

            var x = args.Event.StartingLocation.X + thisOffset.X;
            var y = args.Event.StartingLocation.Y + thisOffset.Y;

            var pos = GetCursorPosition(x, y);
            Debug.WriteLine($"GetCursorPosition detected {pos}");
            CursorPosition = pos;

            Superview.FocusedChild = this;
            return this;
            break;

            default:
            if (IsFocused)
                return this;
            break;
            }

            return base.ProcessGestures(args, apply);
        }



        protected void SetFocusInternal(bool value)
        {
            Tasks.StartDelayed(TimeSpan.FromMilliseconds(100), () =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetFocusNative(value);
                    MoveInternalCursor();
                });
            });


        }


        /// <summary>
        /// Sets native contol cursor position to CursorPosition and calls UpdateCursorVisibility
        /// </summary>
        protected void MoveInternalCursor()
        {
            SetCursorPositionNative(CursorPosition);

            UpdateCursorVisibility();
        }

        /// <summary>
        /// Positions cursor control where it should be using translation, and sets its visibility.
        /// </summary>
        public virtual void UpdateCursorVisibility()
        {
            if (Label != null && Cursor != null)
            {

                var cursorIndex = CursorPosition;

                //make cursor fit the line height
                Cursor.HeightRequest = Label.MeasuredLineHeight / RenderingScale;

                if (cursorIndex < 0 || Label.Lines == null)
                {
                    CursorPosition = 0;
                    MoveCursorTo(0, 0);
                    return;
                }

                var index = 0;
                var line = 0;
                var endX = 0f;
                var lastY = 0f;
                foreach (var labelLine in Label.Lines)
                {
                    // Check if we're on the last line and the cursor is at the last position
                    if (line == Label.LinesCount - 1 && cursorIndex == index + labelLine.Spans.First().Glyphs.Length)
                    {
                        var translateX = labelLine.Bounds.Width / RenderingScale;
                        var translateY = (labelLine.Bounds.Top - Label.DrawingRect.Top) / RenderingScale;

                        MoveCursorTo(translateX, translateY);
                        break;
                    }

                    if (cursorIndex < index + labelLine.Spans.First().Glyphs.Length)
                    {

                        if (line > 0 && cursorIndex - index == 0)
                        {
                            MoveCursorTo((endX - Label.DrawingRect.Left) / RenderingScale,
                                (lastY - Label.DrawingRect.Top) / RenderingScale);
                            break;
                        }

                        var x = labelLine.Bounds.Left + labelLine.Spans.First().Glyphs[cursorIndex - index].Position;
                        var y = labelLine.Bounds.Top;

                        MoveCursorTo((x - Label.DrawingRect.Left) / RenderingScale, (y - Label.DrawingRect.Top) / RenderingScale);
                        break;
                    }

                    endX = labelLine.Bounds.Right;
                    lastY = labelLine.Bounds.Top;
                    line++;
                    index += labelLine.Spans.First().Glyphs.Length;
                }

                Cursor.IsVisible = IsFocused && CanShowCursor;
            }

        }

        /// <summary>
        /// Translate cursor from the left top corner, params in pts.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        protected virtual void MoveCursorTo(double x, double y)
        {
            Debug.WriteLine($"MoveCursorTo {x:0} {y:0}");
            Cursor.TranslationX = x;
            Cursor.TranslationY = y;
        }


        public void SetFocus(bool focus)
        {
            if (focus)
            {
                IsFocused = true;
            }
            else
            {
                IsFocused = false;
            }

            UpdateLabel();
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


        public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            RenderingScale = scale;

            if (Label == null)
            {
                CreateControl();
            }

            return base.OnMeasuring(widthConstraint, heightConstraint, scale);
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
                //todo
                //Label.SelectionStart = start;
                //Label.SelectionEnd = end;
                MoveInternalCursor();
            }
        }



        public override void OnDisposing()
        {
            DisposePlatform();

            base.OnDisposing();
        }

        protected override void OnLayoutChanged()
        {
            base.OnLayoutChanged();

            UpdateNativePosition();
        }

        #endregion

        #region PROPERTIES

        public static readonly BindableProperty ReturnTypeProperty = BindableProperty.Create(
            nameof(ReturnType),
            typeof(ReturnType),
            typeof(SkiaEditor),
            ReturnType.Done);

        public ReturnType ReturnType
        {
            get { return (ReturnType)GetValue(ReturnTypeProperty); }
            set { SetValue(ReturnTypeProperty, value); }
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
                control.TextChanged?.Invoke(control, (string)newvalue);
                control.CommandOnTextChanged?.Execute((string)newvalue);
                OnNeedUpdateText(bindable, oldvalue, newvalue);
            }
        }

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
            Colors.GreenYellow,
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


        public static readonly BindableProperty MaxLinesProperty = BindableProperty.Create(nameof(MaxLines),
            typeof(int), typeof(SkiaEditor), 1);
        public int MaxLines
        {
            get { return (int)GetValue(MaxLinesProperty); }
            set { SetValue(MaxLinesProperty, value); }
        }



        #endregion

        #region LOCALIZE

        public static string ActionGo = "Go";
        public static string ActionNext = "Next";
        public static string ActionSend = "Send";
        public static string ActionSearch = "Search";
        public static string ActionDone = "Done";

        #endregion

#if (!ANDROID && !IOS && !MACCATALYST && !WINDOWS && !TIZEN)


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

#endif

    }
}
