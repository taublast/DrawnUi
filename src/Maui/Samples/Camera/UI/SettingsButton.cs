using AppoMobi.Maui.Gestures;
using AppoMobi.Specials;

namespace CameraTests.UI;

/// <summary>
/// Custom button for settings UI following the SkiaButton pattern.
/// Handles all gestures directly in ProcessGestures (no base delegation).
/// Supports pressed state (shadow + translation), hover state (border glow), and fluent chaining.
/// </summary>
public class SettingsButton : SkiaLayout, ISkiaGestureListener
{
    private SkiaShape _shape;
    private SkiaLabel _label;
    private SkiaLabel _iconLabel;

    // Pre-allocated effect lists to avoid GC during state changes
    private List<SkiaEffect> _normalEffects;
    private List<SkiaEffect> _pressedEffects;
    private List<SkiaEffect> _hoverEffects;

    private Action<SettingsButton> _tappedAction;
    private bool _hadDown;

    public SettingsButton()
    {
        UseCache = SkiaCacheType.Image;
        Padding = new Thickness(4);
    }

    public SettingsButton(string text) : this()
    {
        Text = text;
    }

    public SettingsButton(string accessoryIcon, string text) : this()
    {
        AccessoryIcon = accessoryIcon;
        Text = text;
    }

    protected override void CreateDefaultContent()
    {
        base.CreateDefaultContent();

        if (Views.Count == 0)
        {
            AddSubView(CreateView());
        }
    }

    private void MapProperties()
    {
        if (_shape != null)
        {
            _shape.BackgroundColor = TintColor;
        }
    }

    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName.IsEither(nameof(IsHovered), nameof(BindingContext)) && _shape != null)
        {
            //_shape.VisualEffects = IsHovered ? _hoverEffects : _normalEffects;
            _label.StrokeColor = IsHovered ? Color.FromArgb("#66FFFFFF") : Color.FromArgb("#01ffffff");
            _shape.StrokeColor = IsHovered ? Color.FromArgb("#CCFFFFFF") : TintColor;
            _iconLabel.FontSize = IsHovered ? 16 : 14;
        }
    }

    protected virtual SkiaShape CreateView()
    {
        var shadowHover = new DropShadowEffect { Blur = 2, X = 2, Y = 2, Color = Color.FromArgb("#CCFFFFFF") };
        var shadowNormal = new DropShadowEffect { Blur = 2, X = 2, Y = 2, Color = Color.FromArgb("#44222222") };
        var shadowPressed = new DropShadowEffect { Blur = 2, X = 1, Y = 1, Color = Color.FromArgb("#01222222") };
        _normalEffects = new List<SkiaEffect> { shadowNormal };
        _pressedEffects = new List<SkiaEffect> { shadowPressed };
        _hoverEffects = new List<SkiaEffect> { shadowHover };

        return new SkiaShape()
        {
            BackgroundColor = TintColor,
            CornerRadius = 4,
            StrokeWidth = 2,
            StrokeColor = TintColor,
            VisualEffects = _normalEffects,
            Children =
            {
                new SkiaLayout()
                {
                    Type = LayoutType.Row,
                    Margin = new Thickness(10, 8),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Spacing = 4,
                    Children =
                    {
                        new SkiaLayout()
                        {
                            WidthRequest=24,
                            LockRatio = 1,
                            //BackgroundColor = Colors.YellowGreen,
                            Children =
                            {
                                new SkiaRichLabel()
                                    {
                                        Text = this.AccessoryIcon,
                                        FontSize=14,
                                        IsVisible = !string.IsNullOrEmpty(this.AccessoryIcon),
                                        UseCache = SkiaCacheType.Operations,
                                        VerticalOptions = LayoutOptions.Center,
                                        HorizontalOptions = LayoutOptions.Center,
                                        TextColor = Colors.White,
                                    }
                                    .Assign(out _iconLabel)
                                    .ObserveProperty(this, nameof(AccessoryIcon), me =>
                                    {
                                        me.Text = this.AccessoryIcon;
                                    }),
                            }
                        }
                        .ObserveProperty(this, nameof(AccessoryIcon), me =>
                        {
                            me.IsVisible = !string.IsNullOrEmpty(this.AccessoryIcon);
                        }),

                        new SkiaRichLabel()
                        {
                            Text = this.Text,
                            StrokeColor = Color.FromArgb("#01ffffff"),
                            StrokeWidth = 1,
                            UseCache = SkiaCacheType.Operations,
                            VerticalOptions = LayoutOptions.Center,
                            TextColor = Colors.White,
                        }
                        .Assign(out _label)
                        .ObserveProperty(this, nameof(Text), me => { me.Text = this.Text; })
                    }
                }
            }
        }
        .Assign(out _shape);
    }

    /// <summary>
    /// Handles all gestures directly, matching SkiaButton pattern (no base delegation).
    /// </summary>
    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        if (args.Type == TouchActionResult.Pointer)
        {
            SetHover(true);
        }

        if (args.Type == TouchActionResult.Down)
        {
            _shape.TranslationX = 1;
            _shape.TranslationY = 1;
            _shape.VisualEffects = _pressedEffects;
            _hadDown = true;
            return this;
        }

        if (args.Type == TouchActionResult.Up)
        {
            _shape.TranslationX = 0;
            _shape.TranslationY = 0;
            _shape.VisualEffects = _normalEffects;
            _hadDown = false;
            return null;
        }

        if (args.Type == TouchActionResult.Tapped)
        {
            _tappedAction?.Invoke(this);
            return this;
        }

        return _hadDown ? this : null;
    }



    /// <summary>
    /// Register a tap callback. Instance method to route taps from the inner shape.
    /// </summary>
    public SettingsButton OnTapped(Action<SettingsButton> action)
    {
        _tappedAction = action;
        return this;
    }

    #region PROPS

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(SettingsButton), string.Empty);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty AccessoryIconProperty = BindableProperty.Create(
        nameof(AccessoryIcon), typeof(string), typeof(SettingsButton), string.Empty);

    public string AccessoryIcon
    {
        get => (string)GetValue(AccessoryIconProperty);
        set => SetValue(AccessoryIconProperty, value);
    }

    public static readonly BindableProperty TintColorProperty = BindableProperty.Create(
        nameof(TintColor), typeof(Color), typeof(SettingsButton),
        Color.FromArgb("#6B7280"), propertyChanged: OnLookChanged);

    public Color TintColor
    {
        get => (Color)GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    private static void OnLookChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SettingsButton control)
        {
            control.MapProperties();
        }
    }

    #endregion
}
