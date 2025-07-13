using System.Windows.Input;
using Thickness = Microsoft.Maui.Thickness;

namespace DrawnUi.Draw;

/// <summary>
/// Button-like control, can include any content inside. It's either you use default content (todo templates?..)
/// or can include any content inside, and properties will by applied by convention to a SkiaLabel with Tag `MainLabel`, SkiaShape with Tag `MainFrame`. At the same time you can override ApplyProperties() and apply them to your content yourself.
/// Convention elements tags: BtnText, BtnShape.
/// </summary>
public partial class SkiaButton : SkiaLayout, ISkiaGestureListener
{
    public SkiaButton()
    {

    }

    public SkiaButton(string caption)
    {
        Text = caption;
    }


    #region DEFAULT CONTENT

    public class ButtonLabel : SkiaRichLabel
    {
        public ButtonLabel()
        {
            Margin = new Thickness(10, 0);
            //UseCache = SkiaCacheType.OperationsFull; todo fix, OperationsFull not working for some reason
            Tag = "BtnText";
            HorizontalOptions = LayoutOptions.Center;
            VerticalOptions = LayoutOptions.Center;
        }
    }

    protected override void CreateDefaultContent()
    {
        if (this.Views.Count == 0)
        {
            InitialBackgroundColor = this.BackgroundColor;
            InitialBackground = this.Background;

            switch (UsingControlStyle)
            {
                case PrebuiltControlStyle.Cupertino:
                    CreateCupertinoStyleContent();
                    break;
                case PrebuiltControlStyle.Material:
                    CreateMaterialStyleContent();
                    break;
                case PrebuiltControlStyle.Windows:
                    CreateWindowsStyleContent();
                    break;
                default:
                    CreateDefaultStyleContent();
                    break;
            }

            // Subscribe to property changes and handle gesture more efficiently
            this.Observe(this, OnButtonPropertyChanged);

            // Set up gesture handling with cleaner pattern
            OnGestures = (parameters, info) =>
            {
                if (parameters.Type == TouchActionResult.Down && IsInsideTapRegion(parameters))
                {
                    IsPressed = true;
                    return this;
                }

                IsPressed = false;

                return null;
            };

            ApplyProperties();
        }
        else
        {
            ApplyProperties();
        }
    }

    protected virtual void CreateDefaultStyleContent()
    {
        SetDefaultMinimumContentSize(100, 41);

        this.AddSubView(new SkiaShape
        {
            Tag = "BtnShape",
            BackgroundColor = this.InitialBackgroundColor,
            Background = this.InitialBackground,
            CornerRadius = 8,
            HorizontalOptions = LayoutOptions.Fill,
            IsClippedToBounds = true,
            VerticalOptions = LayoutOptions.Fill,
        });

        this.AddSubView(new ButtonLabel() { Text = "Button", TextColor = this.TextColor, });
    }

    protected virtual void CreateCupertinoStyleContent()
    {
        // iOS buttons have specific dimensions and styling
        SetDefaultMinimumContentSize(100, 36);

        this.BackgroundColor = Color.FromRgba(0, 122, 255, 255); // iOS blue

        // iOS buttons use subtle shadows and rounded corners
        var frame = new SkiaShape
        {
            Tag = "BtnShape",
            CornerRadius = 8, // iOS uses moderate corner radius
            HorizontalOptions = LayoutOptions.Fill,
            IsClippedToBounds = true,
            VerticalOptions = LayoutOptions.Fill,
            // Add subtle shadow for iOS style
            Shadows = new List<SkiaShadow>()
            {
                new SkiaShadow
                {
                    X = 0,
                    Y = 1,
                    Blur = 2,
                    Opacity = 0.2,
                    Color = Colors.Black
                }
            }
        };
        this.AddSubView(frame);

        // iOS button text is typically semibold
        var label = new ButtonLabel()
        {
            Text = "Test",
            TextColor = Colors.White,
            FontSize = 17, // iOS typical size
            FontWeight = FontWeights.SemiBold,
        };
        this.AddSubView(label);

        // Store the initial values
        InitialBackgroundColor = frame.BackgroundColor;
        TextColor = Colors.White;
    }

    protected virtual void CreateMaterialStyleContent()
    {
        // Material buttons follow specific design guidelines
        SetDefaultMinimumContentSize(100, 40);

        this.BackgroundColor = Color.FromRgba(33, 150, 243, 255); // Material blue

        // Material uses more pronounced shadows and smaller corner radius
        var frame = new SkiaShape
        {
            Tag = "BtnShape",
            CornerRadius = 4, // Material uses smaller corners
            HorizontalOptions = LayoutOptions.Fill,
            IsClippedToBounds = true,
            VerticalOptions = LayoutOptions.Fill,
            // Material shadows are more pronounced than iOS
            Shadows = new List<SkiaShadow>()
            {
                new SkiaShadow
                {
                    X = 0,
                    Y = 2,
                    Blur = 4,
                    Opacity = 0.3,
                    Color = Colors.Black
                }
            }
        };
        this.AddSubView(frame);

        // Material Design prefers uppercase text
        var label = new ButtonLabel()
        {
            Text = "Test",
            TextColor = Colors.White,
            FontSize = 14, // Material typical size
            TextTransform = TextTransform.Uppercase, // Material uses uppercase
        };
        this.AddSubView(label);

        // Store the initial values
        InitialBackgroundColor = frame.BackgroundColor;
        TextColor = Colors.White;
    }

    protected virtual void CreateWindowsStyleContent()
    {
        // Windows buttons are typically more rectangular
        SetDefaultMinimumContentSize(100, 32);

        BackgroundColor = Color.FromRgba(0, 120, 215, 255); // Windows blue

        var frame = new SkiaShape
        {
            Tag = "BtnShape",
            CornerRadius = 4,
            HorizontalOptions = LayoutOptions.Fill,
            IsClippedToBounds = true,
            VerticalOptions = LayoutOptions.Fill,
            Shadows = new List<SkiaShadow>()
            {
                new SkiaShadow
                {
                    X = 0,
                    Y = 1,
                    Blur = 1,
                    Opacity = 0.2,
                    Color = Colors.Black
                }
            }
        };
        this.AddSubView(frame);

        // Windows text is typically regular weight
        var label = new ButtonLabel()
        {
            Text = "Test", TextColor = Colors.White, FontSize = 15, FontWeight = FontWeights.Medium,
        };
        this.AddSubView(label);

        // Store the initial values
        InitialBackgroundColor = frame.BackgroundColor;
        TextColor = Colors.White;
    }

    protected virtual bool IsInsideTapRegion(SkiaGesturesParameters parameters)
    {
        // Simple logic to determine if movement was minimal enough to count as a tap
        return Math.Abs(parameters.Event.Distance.Delta.X) < 10 &&
               Math.Abs(parameters.Event.Distance.Delta.Y) < 10;
    }

    protected virtual void OnButtonPropertyChanged(SkiaButton control, string propertyName)
    {
        // Handle various property changes
        if (propertyName == nameof(IsPressed) || propertyName == nameof(ElevationEnabled))
        {
            // Update elevation effects based on pressed state
            if (MainFrame != null && ElevationEnabled)
            {
                // Handle elevation effect
                var effects = MainFrame.VisualEffects?.ToList() ?? new List<SkiaEffect>();
                var shadowEffect = effects.FirstOrDefault(e => e is DropShadowEffect) as DropShadowEffect;

                if (shadowEffect == null)
                {
                    // Create shadow if it doesn't exist
                    shadowEffect = new DropShadowEffect();
                    effects.Add(shadowEffect);
                }

                // Apply platform-specific shadow adjustments
                switch (UsingControlStyle)
                {
                    case PrebuiltControlStyle.Cupertino:
                        // iOS uses more subtle shadows
                        if (IsPressed)
                        {
                            // Pressed state - smaller, closer shadow
                            shadowEffect.X = 0;
                            shadowEffect.Y = 1;
                            shadowEffect.Blur = 1;
                            shadowEffect.Color = Color.FromRgba(0, 0, 0, 0.15);
                        }
                        else
                        {
                            // Normal state - subtle shadow
                            shadowEffect.X = 0;
                            shadowEffect.Y = 2;
                            shadowEffect.Blur = 3;
                            shadowEffect.Color = Color.FromRgba(0, 0, 0, 0.2);
                        }

                        break;

                    case PrebuiltControlStyle.Material:
                        // Material design has more pronounced shadows
                        if (IsPressed)
                        {
                            // Pressed state - smaller, closer shadow
                            shadowEffect.X = 0;
                            shadowEffect.Y = 1;
                            shadowEffect.Blur = 2;
                            shadowEffect.Color = Color.FromRgba(0, 0, 0, 0.3);
                        }
                        else
                        {
                            // Normal state - larger, more distant shadow
                            shadowEffect.X = 0;
                            shadowEffect.Y = 3;
                            shadowEffect.Blur = 4;
                            shadowEffect.Color = Color.FromRgba(0, 0, 0, 0.4);
                        }

                        break;

                    case PrebuiltControlStyle.Windows:
                        // Windows has very subtle shadows
                        if (IsPressed)
                        {
                            // Pressed state - minimal shadow
                            shadowEffect.X = 0;
                            shadowEffect.Y = 0;
                            shadowEffect.Blur = 1;
                            shadowEffect.Color = Color.FromRgba(0, 0, 0, 0.1);
                        }
                        else
                        {
                            // Normal state - subtle shadow
                            shadowEffect.X = 0;
                            shadowEffect.Y = 1;
                            shadowEffect.Blur = 2;
                            shadowEffect.Color = Color.FromRgba(0, 0, 0, 0.2);
                        }

                        break;

                    default:
                        // Default - standard shadow treatment
                        if (IsPressed)
                        {
                            // Pressed state - smaller, closer shadow
                            shadowEffect.X = 0;
                            shadowEffect.Y = 1;
                            shadowEffect.Blur = 2;
                            shadowEffect.Color = Color.FromRgba(0, 0, 0, 0.2);
                        }
                        else
                        {
                            // Normal state - larger, more distant shadow
                            shadowEffect.X = 0;
                            shadowEffect.Y = 2;
                            shadowEffect.Blur = 4;
                            shadowEffect.Color = Color.FromRgba(0, 0, 0, 0.3);
                        }

                        break;
                }

                // Apply updated effects
                MainFrame.VisualEffects = effects;
            }
        }
    }

    #endregion

    /// <summary>
    /// Clip effects with rounded rect of the frame inside
    /// </summary>
    /// <returns></returns>
    public override SKPath CreateClip(object arguments, bool usePosition, SKPath path = null)
    {
        if (MainFrame != null)
        {
            return MainFrame.CreateClip(arguments, false);
            //var offsetFrame = new SKPoint(MainFrame.DrawingRect.Left - DrawingRect.Left, MainFrame.DrawingRect.Top - DrawingRect.Top);
            //var clip = MainFrame.CreateClip(arguments, usePosition); ;
            //clip.Offset(offsetFrame);
            //return clip;
        }

        return base.CreateClip(arguments, usePosition);
    }

    public SkiaLabel MainLabel;
    public SkiaShape MainFrame;

    public override void OnChildrenChanged()
    {
        base.OnChildrenChanged();

        FindViews();
    }

    public virtual void FindViews()
    {
        if (MainLabel == null)
        {
            MainLabel = FindView<SkiaLabel>("BtnText");
        }

        if (MainFrame == null)
        {
            MainFrame = FindView<SkiaShape>("BtnShape");
        }
    }

    public virtual void ApplyProperties()
    {
        if (MainLabel != null)
        {
            MainLabel.Text = this.Text;

            // Apply text case transformation if specified
            if (TextCase != TextTransform.None)
            {
                switch (TextCase)
                {
                    case TextTransform.Uppercase:
                        MainLabel.Text = Text?.ToUpper();
                        break;
                    case TextTransform.Lowercase:
                        MainLabel.Text = Text?.ToLower();
                        break;
                }
            }

            MainLabel.TextColor = this.TextColor;
            MainLabel.StrokeColor = TextStrokeColor;
            MainLabel.FontFamily = this.FontFamily;
            MainLabel.FontSize = this.FontSize;
        }

        if (MainFrame != null)
        {
            // Apply different styles based on ButtonStyle
            switch (ButtonStyle)
            {
                case ButtonStyleType.Contained:
                    MainFrame.BackgroundColor = this.BackgroundColor;
                    MainFrame.StrokeColor = Colors.Transparent;
                    MainFrame.StrokeWidth = 0;
                    break;

                case ButtonStyleType.Outlined:
                    MainFrame.BackgroundColor = Colors.Transparent;
                    MainFrame.StrokeColor = this.BackgroundColor;
                    MainFrame.StrokeWidth = 1;
                    break;

                case ButtonStyleType.Text:
                    MainFrame.BackgroundColor = Colors.Transparent;
                    MainFrame.StrokeColor = Colors.Transparent;
                    MainFrame.StrokeWidth = 0;
                    break;
            }

            if (StrokeColor != TransparentColor)
            {
                MainFrame.StrokeColor = this.StrokeColor;
            }
            if (StrokeWidth != 0)
            {
                MainFrame.StrokeWidth = this.StrokeWidth;
            }

            MainFrame.Background = this.Background;
            if (CornerRadius != 8)
            {
                MainFrame.CornerRadius = CornerRadius;
            }
            else
            {
                switch (UsingControlStyle)
                {
                    case PrebuiltControlStyle.Cupertino:
                        // iOS uses more rounded corners
                        MainFrame.CornerRadius = 8;
                        break;
                    case PrebuiltControlStyle.Material:
                        // Material uses less rounded corners
                        MainFrame.CornerRadius = 4;
                        break;
                    case PrebuiltControlStyle.Windows:
                        // Windows uses subtle corners
                        MainFrame.CornerRadius = 2;
                        break;
                    default:
                        // Default - use provided corner radius
                        MainFrame.CornerRadius = this.CornerRadius;
                        break;
                }
            }

            // Handle shadows based on ElevationEnabled property and platform style
            if (!ElevationEnabled)
            {
                // Remove any shadow effects if elevation is disabled
                if (MainFrame.VisualEffects != null)
                {
                    var effects = MainFrame.VisualEffects.ToList();
                    var shadowEffect = effects.FirstOrDefault(e => e is DropShadowEffect);

                    if (shadowEffect != null)
                    {
                        effects.Remove(shadowEffect);
                        MainFrame.VisualEffects = effects.Count > 0 ? effects : null;
                    }
                }

                // Remove directly applied shadows
                MainFrame.Shadows = null;
            }
            else
            {
                // Default - Handle via the property changed callback for DropShadowEffect
                OnButtonPropertyChanged(this, nameof(IsPressed));

                // Apply platform-specific shadow adjustments if using native shadows
                if (MainFrame.Shadows != null && MainFrame.Shadows.Count > 0)
                {
                    var shadow = MainFrame.Shadows.FirstOrDefault();
                    if (shadow != null)
                    {
                        switch (UsingControlStyle)
                        {
                            case PrebuiltControlStyle.Cupertino:
                                // iOS uses subtle shadows
                                shadow.X = 0;
                                shadow.Y = IsPressed ? 1 : 2;
                                shadow.Blur = IsPressed ? 1 : 3;
                                shadow.Opacity = IsPressed ? 0.2f : 0.3f;
                                break;
                            case PrebuiltControlStyle.Material:
                                // Material design has more pronounced shadows
                                shadow.X = 0;
                                shadow.Y = IsPressed ? 1 : 3;
                                shadow.Blur = IsPressed ? 2 : 4;
                                shadow.Opacity = IsPressed ? 0.3f : 0.4f;
                                break;
                            case PrebuiltControlStyle.Windows:
                                // Windows has very subtle shadows
                                shadow.X = 0;
                                shadow.Y = IsPressed ? 0 : 1;
                                shadow.Blur = IsPressed ? 1 : 2;
                                shadow.Opacity = IsPressed ? 0.1f : 0.2f;
                                break;
                        }
                    }
                }
            }

            MainFrame.Bevel = this.Bevel;
            MainFrame.BevelType = this.BevelType;
        }
        else
        {
            InitialBackgroundColor = this.BackgroundColor;
            InitialBackground = this.Background;
        }

        // Note: IconPosition handling will be implemented when we add icon support
    }

    public virtual bool OnDown(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        //todo check we are inside mainframe OR inside the rect accounting for margins

        Pressed?.Invoke(this, args);

        if (this.ApplyEffect != SkiaTouchAnimation.None)
        {
            var control = this as SkiaControl;
            if (this.TransformView is SkiaControl other)
            {
                control = other;
            }

            if (ApplyEffect == SkiaTouchAnimation.Ripple)
            {
                var ptsInsideControl = GetOffsetInsideControlInPoints(args.Event.Location, apply.ChildOffset);
                control.PlayRippleAnimation(TouchEffectColor, ptsInsideControl.X, ptsInsideControl.Y);
            }
            else if (ApplyEffect == SkiaTouchAnimation.Shimmer)
            {
                var color = ShimmerEffectColor;
                control.PlayShimmerAnimation(color, ShimmerEffectWidth, ShimmerEffectAngle, ShimmerEffectSpeed);
            }
        }

        return true;
    }

    public virtual void OnUp(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        Released?.Invoke(this, args);
    }

    public virtual bool OnTapped(SkiaGesturesParameters args, SKPoint childOffset)
    {
        var ret = false;

        if (!IsDisabled)
        {
            if (SendTapped(this, args, GestureEventProcessingInfo.Empty, Super.SendTapsOnMainThread))
            {
                ret = true;
            }

            if (Clicked != null)
            {
                ret = true;
                Clicked(this, args);
            }

            if (CommandTapped != null)
            {
                ret = true;
                Tasks.StartDelayedAsync(TimeSpan.FromMilliseconds(DelayCallbackMs),
                    async () =>
                    {
                        await Task.Run(() => { CommandTapped?.Execute(CommandTappedParameter); }).ConfigureAwait(false);
                    });
            }
        }

        return ret;
    }

    bool hadDown;
    public static float PanThreshold = 5;

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        //Debug.WriteLine($"SkiaButton {Text}. {args.Type} {args.Event.Distance.Delta}");

        var point = TranslateInputOffsetToPixels(args.Event.Location, apply.ChildOffset);

        var ret = false;

        void SetUp()
        {
            IsPressed = false;
            //MainThread.BeginInvokeOnMainThread(() =>
            //{
            //    IsPressed = false;
            //});
            hadDown = false; //todo track multifingers
            Up?.Invoke(this, args);
            OnUp(args, apply);
        }

        if (args.Type == TouchActionResult.Down)
        {
            IsPressed = true;
            //MainThread.BeginInvokeOnMainThread(() =>
            //{
            //    IsPressed = true;
            //});
            _lastDownPts = point;
            hadDown = true;
            TotalDown++;
            Down?.Invoke(this, args);
            return OnDown(args, apply) ? this : null;
        }

        if (args.Type == TouchActionResult.Panning)
        {
            if (LockPanning)
            {
                return this; //no panning for you my friend 
            }

            var current = point;
            var panthreshold = PanThreshold * RenderingScale;

            if (Math.Abs(current.X - _lastDownPts.X) > panthreshold
                || Math.Abs(current.Y - _lastDownPts.Y) > panthreshold)
            {
                if (hadDown)
                    SetUp();
                hadDown = false;

                return null;
            }
        }
        else if (args.Type == TouchActionResult.Up)
        {
            //todo track multifingers?
            SetUp();
            //hadDown = false; 
            //Up?.Invoke(this, args);
            //OnUp();
        }
        else if (args.Type == TouchActionResult.Tapped)
        {
            TotalTapped++;
            return OnTapped(args, apply.ChildOffset) ? this : null;
        }

        return hadDown ? this : null;
    }

    /// <summary>
    /// You might want to pause to show effect before executing command. Default is 0.
    /// </summary>
    public static int DelayCallbackMs = 0;

    public override void OnDisposing()
    {
        base.OnDisposing();

        Up = null;
        Down = null;
        Clicked = null;
        Released = null;
        Pressed = null;
    }

    public event EventHandler<SkiaGesturesParameters> Up;
    public event EventHandler<SkiaGesturesParameters> Down;
    //public event EventHandler<SkiaGesturesParameters> Tapped;

    /// <summary>
    /// Occurs when the button is clicked/tapped (Tapped event).
    /// </summary>
    public Action<SkiaButton, SkiaGesturesParameters> Clicked;

    /// <summary>
    /// Occurs when the button is released (Up event).
    /// </summary>
    public Action<SkiaButton, SkiaGesturesParameters> Released;

    /// <summary>
    /// Occurs when the button is pressed (Down event).
    /// </summary>
    public Action<SkiaButton, SkiaGesturesParameters> Pressed;

    private long _TotalTapped;

    public long TotalTapped
    {
        get { return _TotalTapped; }
        set
        {
            if (_TotalTapped != value)
            {
                _TotalTapped = value;
                OnPropertyChanged();
            }
        }
    }

    private long _TotalDown;

    public long TotalDown
    {
        get { return _TotalDown; }
        set
        {
            if (_TotalDown != value)
            {
                _TotalDown = value;
                OnPropertyChanged();
            }
        }
    }

    #region PROPERTIES

    /// <summary>
    /// Defines the button style variants available for different visual appearances.
    /// </summary>
    public enum ButtonStyleType
    {
        /// <summary>
        /// Standard filled button with background color and text
        /// </summary>
        Contained,

        /// <summary>
        /// Button with outline border and transparent background
        /// </summary>
        Outlined,

        /// <summary>
        /// Button with no background or border, only text
        /// </summary>
        Text
    }

    /// <summary>
    /// Defines the position of an icon relative to the button text
    /// </summary>
    public enum IconPositionType
    {
        /// <summary>
        /// Icon appears before the text
        /// </summary>
        Left,

        /// <summary>
        /// Icon appears after the text
        /// </summary>
        Right
    }

    public static readonly BindableProperty ButtonStyleProperty = BindableProperty.Create(
        nameof(ButtonStyle),
        typeof(ButtonStyleType),
        typeof(SkiaButton),
        ButtonStyleType.Contained,
        propertyChanged: OnLookChanged);

    /// <summary>
    /// Gets or sets the style variant of the button (Contained, Outlined, or Text).
    /// </summary>
    public ButtonStyleType ButtonStyle
    {
        get { return (ButtonStyleType)GetValue(ButtonStyleProperty); }
        set { SetValue(ButtonStyleProperty, value); }
    }

    public static readonly BindableProperty TextCaseProperty = BindableProperty.Create(
        nameof(TextCase),
        typeof(TextTransform),
        typeof(SkiaButton),
        TextTransform.None,
        propertyChanged: OnLookChanged);

    /// <summary>
    /// Gets or sets how the button text case is transformed (None, Uppercase, Lowercase).
    /// </summary>
    public TextTransform TextCase
    {
        get { return (TextTransform)GetValue(TextCaseProperty); }
        set { SetValue(TextCaseProperty, value); }
    }

    public static readonly BindableProperty ElevationEnabledProperty = BindableProperty.Create(
        nameof(ElevationEnabled),
        typeof(bool),
        typeof(SkiaButton),
        false,
        propertyChanged: OnLookChanged);

    /// <summary>
    /// Gets or sets whether the button has elevation (shadow effect).
    /// </summary>
    public bool ElevationEnabled
    {
        get { return (bool)GetValue(ElevationEnabledProperty); }
        set { SetValue(ElevationEnabledProperty, value); }
    }

    public static readonly BindableProperty IconPositionProperty = BindableProperty.Create(
        nameof(IconPosition),
        typeof(IconPositionType),
        typeof(SkiaButton),
        IconPositionType.Left,
        propertyChanged: OnLookChanged);

    /// <summary>
    /// Gets or sets the position of the icon relative to the button text.
    /// </summary>
    public IconPositionType IconPosition
    {
        get { return (IconPositionType)GetValue(IconPositionProperty); }
        set { SetValue(IconPositionProperty, value); }
    }

    private static void OnLookChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaButton control)
        {
            control.ApplyProperties();
        }
    }

    public static readonly BindableProperty LockPanningProperty = BindableProperty.Create(nameof(LockPanning),
        typeof(bool),
        typeof(SkiaButton),
        false);

    public bool LockPanning
    {
        get { return (bool)GetValue(LockPanningProperty); }
        set { SetValue(LockPanningProperty, value); }
    }

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(double),
        typeof(SkiaButton),
        12.0,
        propertyChanged: OnLookChanged);

    public double FontSize
    {
        get { return (double)GetValue(FontSizeProperty); }
        set { SetValue(FontSizeProperty, value); }
    }

    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily),
        typeof(string),
        typeof(SkiaButton),
        defaultValue: string.Empty,
        propertyChanged: OnLookChanged);

    public string FontFamily
    {
        get { return (string)GetValue(FontFamilyProperty); }
        set { SetValue(FontFamilyProperty, value); }
    }

    public static readonly BindableProperty IsDisabledProperty = BindableProperty.Create(
        nameof(IsDisabled),
        typeof(bool),
        typeof(SkiaButton),
        false, propertyChanged: OnLookChanged);

    public bool IsDisabled
    {
        get { return (bool)GetValue(IsDisabledProperty); }
        set { SetValue(IsDisabledProperty, value); }
    }

    public static readonly BindableProperty IsPressedProperty = BindableProperty.Create(
        nameof(IsPressed),
        typeof(bool),
        typeof(SkiaButton),
        false,
        BindingMode.OneWayToSource);

    public bool IsPressed
    {
        get { return (bool)GetValue(IsPressedProperty); }
        set { SetValue(IsPressedProperty, value); }
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(SkiaButton),
        string.Empty, propertyChanged: OnLookChanged);

    /// <summary>
    /// Bind to your own content!
    /// </summary>
    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly BindableProperty ShimmerEffectColorProperty = BindableProperty.Create(
        nameof(ShimmerEffectColor),
        typeof(Color),
        typeof(SkiaButton),
        WhiteColor.WithAlpha(0.33f));

    public Color ShimmerEffectColor
    {
        get { return (Color)GetValue(ShimmerEffectColorProperty); }
        set { SetValue(ShimmerEffectColorProperty, value); }
    }

    public static readonly BindableProperty ShimmerEffectAngleProperty = BindableProperty.Create(
        nameof(ShimmerEffectAngle),
        typeof(float),
        typeof(SkiaButton),
        33.0f);

    public float ShimmerEffectAngle
    {
        get { return (float)GetValue(ShimmerEffectAngleProperty); }
        set { SetValue(ShimmerEffectAngleProperty, value); }
    }

    public static readonly BindableProperty ShimmerEffectWidthProperty = BindableProperty.Create(
        nameof(ShimmerEffectWidth),
        typeof(float),
        typeof(SkiaButton),
        150.0f);

    public float ShimmerEffectWidth
    {
        get { return (float)GetValue(ShimmerEffectWidthProperty); }
        set { SetValue(ShimmerEffectWidthProperty, value); }
    }

    public static readonly BindableProperty ShimmerEffectSpeedProperty = BindableProperty.Create(
        nameof(ShimmerEffectSpeed),
        typeof(int),
        typeof(SkiaButton),
        500);

    public int ShimmerEffectSpeed
    {
        get { return (int)GetValue(ShimmerEffectSpeedProperty); }
        set { SetValue(ShimmerEffectSpeedProperty, value); }
    }


    public static readonly BindableProperty ApplyEffectProperty = BindableProperty.Create(nameof(ApplyEffect),
        typeof(SkiaTouchAnimation),
        typeof(SkiaButton), SkiaTouchAnimation.Ripple);

    public SkiaTouchAnimation ApplyEffect
    {
        get { return (SkiaTouchAnimation)GetValue(ApplyEffectProperty); }
        set { SetValue(ApplyEffectProperty, value); }
    }


    public static readonly BindableProperty CommandTappedProperty = BindableProperty.Create(nameof(CommandTapped),
        typeof(ICommand),
        typeof(SkiaButton),
        null);

    public ICommand CommandTapped
    {
        get { return (ICommand)GetValue(CommandTappedProperty); }
        set { SetValue(CommandTappedProperty, value); }
    }

    public static readonly BindableProperty CommandTappedParameterProperty = BindableProperty.Create(
        nameof(CommandTappedParameter), typeof(object),
        typeof(SkiaButton),
        null);

    public object CommandTappedParameter
    {
        get { return GetValue(CommandTappedParameterProperty); }
        set { SetValue(CommandTappedParameterProperty, value); }
    }

    public static readonly BindableProperty CommandLongPressingProperty = BindableProperty.Create(
        nameof(CommandLongPressing), typeof(ICommand),
        typeof(SkiaButton),
        null);

    public ICommand CommandLongPressing
    {
        get { return (ICommand)GetValue(CommandLongPressingProperty); }
        set { SetValue(CommandLongPressingProperty, value); }
    }

    public static readonly BindableProperty CommandLongPressingParameterProperty = BindableProperty.Create(
        nameof(CommandLongPressingParameter), typeof(object),
        typeof(SkiaButton),
        null);

    public object CommandLongPressingParameter
    {
        get { return GetValue(CommandLongPressingParameterProperty); }
        set { SetValue(CommandLongPressingParameterProperty, value); }
    }

    public static readonly BindableProperty StrokeColorProperty = BindableProperty.Create(
        nameof(StrokeColor),
        typeof(Color),
        typeof(SkiaButton),
        TransparentColor,
        propertyChanged: NeedApplyProperties);

    public Color StrokeColor
    {
        get { return (Color)GetValue(StrokeColorProperty); }
        set { SetValue(StrokeColorProperty, value); }
    }

    public static readonly BindableProperty StrokeWidthProperty = BindableProperty.Create(
        nameof(StrokeWidth),
        typeof(float),
        typeof(SkiaButton),
        0.0f, propertyChanged: NeedApplyProperties);

    public float StrokeWidth
    {
        get { return (float)GetValue(StrokeWidthProperty); }
        set { SetValue(StrokeWidthProperty, value); }
    }

    protected SKPoint _lastDownPts;

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(SkiaButton),
        WhiteColor,
        propertyChanged: NeedApplyProperties);

    public Color TextColor
    {
        get { return (Color)GetValue(TextColorProperty); }
        set { SetValue(TextColorProperty, value); }
    }

    public static readonly BindableProperty TextStrokeColorProperty = BindableProperty.Create(
        nameof(TextStrokeColor),
        typeof(Color),
        typeof(SkiaButton),
        TransparentColor,
        propertyChanged: NeedApplyProperties);

    public Color TextStrokeColor
    {
        get { return (Color)GetValue(TextStrokeColorProperty); }
        set { SetValue(TextStrokeColorProperty, value); }
    }

    private static void NeedApplyProperties(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaButton control)
        {
            control.ApplyProperties();
        }
    }

    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName.IsEither(nameof(Background), nameof(BackgroundColor), nameof(CornerRadius)))
        {
            ApplyProperties();
        }
    }

    #endregion

    /// <summary>
    /// Will never paint background, it's up to the button shape to do it.
    /// </summary>
    /// <param name="paint"></param>
    /// <param name="destination"></param>
    /// <returns></returns>
    protected override bool SetupBackgroundPaint(SKPaint paint, SKRect destination)
    {
        return false;
    }
}
