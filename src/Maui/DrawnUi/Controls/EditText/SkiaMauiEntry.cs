using System.Runtime.CompilerServices;
using AppoMobi.Specials;
using DrawnUi.Draw;
using System.Windows.Input;

namespace DrawnUi.Controls;

/// <summary>
/// Used to draw maui element over a skia canvas.
/// Positions elelement using drawnUi layout and sometimes just renders element bitmap snapshot instead of displaying the real element, for example, when scrolling/animating.
/// </summary>
public class SkiaMauiEntry : SkiaMauiElement, ISkiaGestureListener
{
    public SkiaMauiEntry()
    {
    }

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        //Debug.WriteLine($"[SkiaMauiEntry] consuming '{args.Type}'");

        return this;
    }

#if ONPLATFORM

    public override void SetNativeVisibility(bool state)
    {
        base.SetNativeVisibility(state);

        if (state && IsFocused)
        {
            SetFocusInternal(true);
        }
    }

#endif

    #region CAN LOCALIZE

    public static string ActionGo = "Go";
    public static string ActionNext = "Next";
    public static string ActionSend = "Send";
    public static string ActionSearch = "Search";
    public static string ActionDone = "Done";

    #endregion

    #region EVENTS

    public event EventHandler<string> TextChanged;
    public event EventHandler<bool> FocusChanged;
    public event EventHandler<string> TextSubmitted;

    #endregion

    public override bool WillClipBounds => true;

    protected virtual Entry GetOrCreateControl()
    {
        if (Control == null)
        {
            //this.AnimateSnapshot = false;

            Control = new MauiEntry()
            {
                ReturnType = ReturnType.Done,
                IsSpellCheckEnabled = this.IsSpellCheckEnabled,
                BackgroundColor = this.BackgroundColor,
                Background = null//Colors.Transparent,
            };

            MapProps(Control);
            AdaptControlSize();

            SubscribeToControl(true);

            Content = Control;

            if (IsFocused)
                SetFocusInternal(true);
        }

        return Control;
    }

    public MauiEntry Control { get; protected set; }

    //todo make static prop
    public bool IsSpellCheckEnabled { get; set; }

    protected void FocusNative()
    {
        if (!Control.IsFocused)
        {
            MainThread.BeginInvokeOnMainThread(() => { Control.Focus(); });
        }
    }

    protected void UnfocusNative()
    {
        MainThread.BeginInvokeOnMainThread(() => { Control?.Unfocus(); });
    }

    public void SetFocus(bool focus)
    {
        if (Control != null)
        {
            if (focus)
            {
                FocusNative();
            }
            else
            {
                UnfocusNative();
            }

            UpdateControl();
        }
    }

    protected virtual void AdaptControlSize()
    {
        if (Control != null)
        {
            Control.WidthRequest = this.Width;
            Control.HeightRequest = this.Height;
            Debug.WriteLine($"[SkiaMauiEntry] Size native control {Width} by {Height}");
        }
    }

    private bool test1;

    protected virtual void MapProps(MauiEntry control)
    {
        var alias = SkiaFontManager.GetRegisteredAlias(this.FontFamily, this.FontWeight);
        control.FontFamily = alias;
        control.MaxLines = MaxLines;
        control.FontSize = FontSize;
        control.TextColor = this.TextColor;
        control.PlaceholderColor = this.PlaceholderColor;
        control.ReturnType = this.ReturnType;
        control.Keyboard = this.KeyboardType;
        control.BackgroundColor = this.BackgroundColor;

        if (Text != control.Text)
            control.Text = Text;

        //todo customize
        control.Placeholder = this.Placeholder;
    }

    object lockAccess = new();

    public virtual void UpdateControl()
    {
        if (Control != null && Control.Handler != null && Control.Handler.PlatformView != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                lock (lockAccess)
                {
                    MapProps(Control);
                    Update();
                }

                AdaptControlSize();
            });
        }
    }

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        MainThread.BeginInvokeOnMainThread(() => { AdaptControlSize(); });
    }

    protected void SubscribeToControl(bool subscribe)
    {
        if (Control != null)
        {
            if (subscribe)
            {
                Control.Unfocused += OnControlUnfocused;
                Control.Focused += OnControlFocused;
                Control.TextChanged += OnControlTextChanged;
                Control.OnCompleted += OnControlCompleted;
            }
            else
            {
                Control.Unfocused -= OnControlUnfocused;
                Control.Focused -= OnControlFocused;
                Control.TextChanged -= OnControlTextChanged;
                Control.OnCompleted -= OnControlCompleted;
            }
        }
    }

    private void OnControlCompleted(object sender, EventArgs e)
    {
        if (!LockFocus)
        {
            Control.Unfocus();
        }

        TextSubmitted?.Invoke(this, Text);
        CommandOnSubmit?.Execute(Text);
    }

    private void OnControlTextChanged(object sender, TextChangedEventArgs e)
    {
        this.Text = Control.Text;
    }

    static object lockFocus = new();
    private bool internalFocus;

    /// <summary>
    /// Invoked by Maui control
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnControlFocused(object sender, FocusEventArgs e)
    {
        Debug.WriteLine($"[SkiaMauiEntry] Focused by native");
        //IsFocused = true;
        lock (lockFocus)
        {
            internalFocus = true;
            IsFocused = true;
            Superview.ReportFocus(this, this);
        }
    }

    /// <summary>
    /// Invoked by Maui control
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnControlUnfocused(object sender, FocusEventArgs e)
    {
        Debug.WriteLine($"[SkiaMauiEntry] Unfocused by native");
        //IsFocused = false;
        lock (lockFocus)
        {
            internalFocus = true;
            IsFocused = false;
            if (Superview.FocusedChild == this)
            {
                Superview.ReportFocus(null, this);
            }

            ;
        }
    }

    /// <summary>
    /// Called by DrawnUi when the focus changes
    /// </summary>
    /// <param name="focus"></param>
    public new bool OnFocusChanged(bool focus)
    {
        lock (lockFocus)
        {

            Debug.WriteLine($"[SkiaMauiEntry] OnFocusChanged {focus}" );

            if (!IsFocused)
                return false; //reject focus

            if (Control != null)
            {
                if (!focus)
                    UnfocusNative();
                else
                    FocusNative();
            }

            return true;
        }
    }

    public override void OnDisposing()
    {
        TextChanged=null;
        FocusChanged=null;
        TextSubmitted=null;

        if (Control != null)
        {
            SubscribeToControl(false);
            Control.DisposeControlAndChildren();
            Control = null;
        }

        base.OnDisposing();
    }

    public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
    {
        GetOrCreateControl();

        return base.OnMeasuring(widthConstraint, heightConstraint, scale);
    }

    protected virtual void WhenFocusChanged(bool state)
    {
        FocusChanged?.Invoke(this, state);
    }

    protected void SetFocusInternal(bool value)
    {
        lock (lockFocus)
        {
            WhenFocusChanged(value);

            if (internalFocus)
            {
                internalFocus = false;
                return;
            }

            if (Control != null)
            {
                if (!Control.IsFocused && value || Control.IsFocused && !value)
                {
                    Tasks.StartDelayed(TimeSpan.FromMilliseconds(100), () =>
                    {
                        if (value)
                            FocusNative();
                        else
                            UnfocusNative();
                    });
                }
            }
        }
    }

    #region PROPERTIES

    private static void NeedUpdateControl(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaMauiEntry control)
        {
            control.UpdateControl();
        }
    }

    public static readonly BindableProperty MaxLinesProperty = BindableProperty.Create(nameof(MaxLines),
        typeof(int), typeof(SkiaMauiEntry), 1);

    /// <summary>
    /// WIth 1 will behave like an ordinary Entry, with -1 (auto) or explicitly set you get an Editor
    /// </summary>
    public int MaxLines
    {
        get { return (int)GetValue(MaxLinesProperty); }
        set { SetValue(MaxLinesProperty, value); }
    }

    public string OldText { get; set; }

    private static void NeedUpdateText(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaMauiEntry control)
        {
            control.TextChanged?.Invoke(control, (string)newvalue);
            control.CommandOnTextChanged?.Execute((string)newvalue);
            control.UpdateControl();
            control.OldText = (string)newvalue;
        }
    }

    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily),
        typeof(string), typeof(SkiaMauiEntry), string.Empty, propertyChanged: NeedUpdateControl);

    public string FontFamily
    {
        get { return (string)GetValue(FontFamilyProperty); }
        set { SetValue(FontFamilyProperty, value); }
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor), typeof(Color), typeof(SkiaMauiEntry),
        Colors.GreenYellow,
        propertyChanged: NeedUpdateControl);

    public Color TextColor
    {
        get { return (Color)GetValue(TextColorProperty); }
        set { SetValue(TextColorProperty, value); }
    }

    public static readonly BindableProperty PlaceholderColorProperty = BindableProperty.Create(
        nameof(PlaceholderColor), typeof(Color), typeof(SkiaMauiEntry),
        Colors.DarkGray,
        propertyChanged: NeedUpdateControl);
    public Color PlaceholderColor
    {
        get { return (Color)GetValue(PlaceholderColorProperty); }
        set { SetValue(PlaceholderColorProperty, value); }
    }

    public static readonly BindableProperty FontWeightProperty = BindableProperty.Create(
        nameof(FontWeight),
        typeof(int),
        typeof(SkiaMauiEntry),
        0,
        propertyChanged: NeedUpdateControl);

    public int FontWeight
    {
        get { return (int)GetValue(FontWeightProperty); }
        set { SetValue(FontWeightProperty, value); }
    }

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize),
        typeof(double), typeof(SkiaMauiEntry), 12.0,
        propertyChanged: NeedUpdateControl);

    public double FontSize
    {
        get { return (double)GetValue(FontSizeProperty); }
        set { SetValue(FontSizeProperty, value); }
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(SkiaMauiEntry),
        default(string),
        BindingMode.TwoWay,
        propertyChanged: NeedUpdateText);

    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder),
        typeof(string),
        typeof(SkiaMauiEntry),
        default(string),
        propertyChanged: NeedUpdateControl);

    public string Placeholder
    {
        get { return (string)GetValue(PlaceholderProperty); }
        set { SetValue(PlaceholderProperty, value); }
    }

    public static readonly BindableProperty ReturnTypeProperty = BindableProperty.Create(
        nameof(ReturnType),
        typeof(ReturnType),
        typeof(SkiaMauiEntry),
        ReturnType.Done,
        propertyChanged: NeedUpdateControl);

    public ReturnType ReturnType
    {
        get { return (ReturnType)GetValue(ReturnTypeProperty); }
        set { SetValue(ReturnTypeProperty, value); }
    }

    public static readonly BindableProperty KeyboardTypeProperty = BindableProperty.Create(
        nameof(KeyboardType),
        typeof(Keyboard),
        typeof(SkiaMauiEntry),
        Keyboard.Plain,
        propertyChanged: NeedUpdateControl);

    [System.ComponentModel.TypeConverter(typeof(Microsoft.Maui.Converters.KeyboardTypeConverter))]
    public Keyboard KeyboardType
    {
        get { return (Keyboard)GetValue(KeyboardTypeProperty); }
        set { SetValue(KeyboardTypeProperty, value); }
    }

    public static readonly BindableProperty CommandOnSubmitProperty = BindableProperty.Create(
        nameof(CommandOnSubmit),
        typeof(ICommand),
        typeof(SkiaMauiEntry),
        null);

    public ICommand CommandOnSubmit
    {
        get { return (ICommand)GetValue(CommandOnSubmitProperty); }
        set { SetValue(CommandOnSubmitProperty, value); }
    }

    public static readonly BindableProperty CommandOnFocusChangedProperty = BindableProperty.Create(
        nameof(CommandOnFocusChanged),
        typeof(ICommand),
        typeof(SkiaMauiEntry),
        null);

    public ICommand CommandOnFocusChanged
    {
        get { return (ICommand)GetValue(CommandOnFocusChangedProperty); }
        set { SetValue(CommandOnFocusChangedProperty, value); }
    }

    public static readonly BindableProperty CommandOnTextChangedProperty = BindableProperty.Create(
        nameof(CommandOnTextChanged),
        typeof(ICommand),
        typeof(SkiaMauiEntry),
        null);

    public ICommand CommandOnTextChanged
    {
        get { return (ICommand)GetValue(CommandOnTextChangedProperty); }
        set { SetValue(CommandOnTextChangedProperty, value); }
    }

    public new static readonly BindableProperty IsFocusedProperty = BindableProperty.Create(
        nameof(IsFocused),
        typeof(bool),
        typeof(SkiaMauiEntry),
        false,
        BindingMode.TwoWay,
        propertyChanged: OnNeedChangeFocus);

    private static void OnNeedChangeFocus(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaMauiEntry control)
        {
            control.SetFocusInternal((bool)newvalue);
        }
    }

    public new bool IsFocused
    {
        get { return (bool)GetValue(IsFocusedProperty); }
        set { SetValue(IsFocusedProperty, value); }
    }

    #endregion
}
