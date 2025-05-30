﻿namespace DrawnUi.Controls;

/// <summary>
/// Switch-like control, can include any content inside. It's aither you use default content (todo templates?..)
/// or can include any content inside, and properties will by applied by convention to a SkiaShape with Tag `Frame`, SkiaShape with Tag `Thumb`. At the same time you can override ApplyProperties() and apply them to your content yourself.
/// </summary>
public class SkiaRadioButton : SkiaToggle, ISkiaRadioButton
{

    #region DEFAULT CONTENT

    protected override void CreateDefaultContent()
    {
        // TODO
        /*
        //todo can make different upon platform!
        if (!DefaultChildrenCreated && this.Views.Count == 0)
        {
            if (CreateChildren == null)
            {
                DefaultChildrenCreated = true;

                if (this.WidthRequest < 0)
                    this.WidthRequest = 50;
                if (this.HeightRequest < 0)
                    this.HeightRequest = 32;

                var shape = new SkiaShape
                {
                    Tag = "Frame",
                    Type = ShapeType.Rectangle,
                    CornerRadius = 20,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                };
                this.AddSubView(shape);

                this.AddSubView(new SkiaShape()
                {
                    UseCache = SkiaCacheType.Operations,
                    Type = ShapeType.Circle,
                    Margin = 2,
                    LockRatio = -1,
                    Tag = "Thumb",
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Fill,
                });

                var hotspot = new SkiaHotspot()
                {
                    TransformView = this.Thumb,
                };
                hotspot.Tapped += (s, e) =>
                {
                    IsToggled = !IsToggled;
                };
                this.AddSubView(hotspot);

                ApplyProperties();
            }

        }
        */
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

    RadioButtonGroupManager Manager => RadioButtonGroupManager.Instance;

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
