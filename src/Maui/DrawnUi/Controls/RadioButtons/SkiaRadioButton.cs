namespace DrawnUi.Controls;

/// <summary>
/// Switch-like control, can include any content inside. It's aither you use default content (todo templates?..)
/// or can include any content inside, and properties will by applied by convention to a SkiaShape with Tag `Frame`, SkiaShape with Tag `Thumb`. At the same time you can override ApplyProperties() and apply them to your content yourself.
/// </summary>
public class SkiaRadioButton : SkiaToggle, ISkiaRadioButton
{
    public SkiaRadioButton()
    {

    }

    public SkiaRadioButton(string text)
    {
        Text = text;
    }

    #region DEFAULT CONTENT

    protected override void CreateDefaultContent()
    {
        if (this.Views.Count == 0)
        {
            switch (UsingControlStyle)
            {
                //case PrebuiltControlStyle.Cupertino:
                //    CreateCupertinoStyleContent();
                //    break;
                //case PrebuiltControlStyle.Material:
                //    CreateMaterialStyleContent();
                //    break;
                //case PrebuiltControlStyle.Windows:
                //    CreateWindowsStyleContent();
                //    break;
                default:
                    CreateDefaultStyleContent();
                    break;
            }

            ApplyProperties();
        }
    }

    protected virtual void CreateDefaultStyleContent()
    {
        //SetDefaultContentSize(-1, 24);

        UseCache = SkiaCacheType.Image;
        MinimumHeightRequest = 24;
        Children = new List<SkiaControl>()
        {
            new SkiaLayout()
            {
                HeightRequest = 16,
                LockRatio = 1,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new SkiaShape()
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        StrokeColor = ColorThumbOff,
                        StrokeWidth = 1.51,
                        Type = ShapeType.Circle
                    },
                    new SkiaShape()
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        BackgroundColor = ColorThumbOn,
                        Margin = 4,
                        Type = ShapeType.Circle,
                        Tag = "On"
                    },
                }
            },
            new SkiaRichLabel()
            {
                Margin = new(24, 0, 0, 0),
                FontSize = 14,
                MaxLines = 2,
                Tag = "Text",
                //FontFamily = "FontTextTitle",
                TextColor = ColorThumbOff,
                VerticalOptions = LayoutOptions.Center
            }
        };
    }

    #endregion

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        ApplyProperties();
    }

    public SkiaControl GroupParent
    {
        get
        {
            return Parent as SkiaControl;
        }
    }

    public virtual void ApplyOff()
    {
        if (ViewOn != null)
        {
            ViewOn.IsVisible = false;
        }
    }

    public virtual void ApplyOn()
    {
        if (ViewOn != null)
        {
            ViewOn.IsVisible = true;
        }
    }

    public SkiaControl ViewOff;
    public SkiaControl ViewOn;
    public SkiaLabel ViewText;

    protected virtual void FindViews()
    {
        ViewOn = FindView<SkiaControl>("On");
        ViewOff = FindView<SkiaControl>("Off");
        ViewText = FindView<SkiaLabel>("Text");
    }

    public override void OnChildrenChanged()
    {
        base.OnChildrenChanged();

        FindViews();
    }


    public override void ApplyProperties()
    {
        if (ViewText != null)
        {
            ViewText.Text = this.Text;
        }

        if (IsToggled)
        {
            ApplyOn();
        }
        else
        {
            ApplyOff();
        }
    }

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        if (args.Type == TouchActionResult.Tapped)
        {
            if (!IsToggled)
            {
                IsToggled = true;
                return this;
            }
        }

        return base.ProcessGestures(args, apply);
    }

    protected override void NotifyWasToggled()
    {
        base.NotifyWasToggled();

        if (!_lockIsToggled)
        {
            Manager.ReportValueChange(this, IsToggled);
        }
    }

    protected virtual void OnGroupChanged()
    {
        UpdateGroup();
    }

    public virtual void UpdateGroup()
    {
        Manager.RemoveFromGroups(this);

        if (!string.IsNullOrEmpty(GroupName))
        {
            Manager.AddToGroup(this, GroupName);
        }
        else
        if (this.Parent is SkiaControl control)
        {
            Manager.AddToGroup(this, control);
        }
    }

    public override void OnParentChanged(IDrawnBase newvalue, IDrawnBase oldvalue)
    {
        base.OnParentChanged(newvalue, oldvalue);

        UpdateGroup();
    }

    RadioButtons Manager => RadioButtons.All;

    bool _lockIsToggled;

    public void SetValueInternal(bool value)
    {
        _lockIsToggled = true;
        IsToggled = value;
        _lockIsToggled = false;
    }

    public bool GetValueInternal()
    {
        return IsToggled;
    }

    public static readonly BindableProperty GroupNameProperty = BindableProperty.Create(nameof(GroupName),
        typeof(string),
        typeof(SkiaRadioButton),
        string.Empty,
        propertyChanged: NeedUpdateGroup);

    private static void NeedUpdateGroup(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaRadioButton control)
        {
            control.OnGroupChanged();
        }
    }

    public string GroupName
    {
        get { return (string)GetValue(GroupNameProperty); }
        set { SetValue(GroupNameProperty, value); }
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(SkiaButton),
        string.Empty, propertyChanged: NeedUpdateProperties);

    /// <summary>
    /// Bind to your own content!
    /// </summary>
    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }
}
