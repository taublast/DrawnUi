using Microsoft.Maui.HotReload;

namespace DrawnUi.Draw;

[ContentProperty("Content")]
public partial class ContentLayout : SkiaLayout, IVisibilityAware, ISkiaGestureListener, IWithContent
{

    public override void Invalidate()
    {
        base.Invalidate();

        Update();
    }

    protected bool IsContentActive
    {
        get
        {
            return Content != null && Content.IsVisible;
        }
    }

    public ScaledRect Viewport { get; protected set; } = new();

    //public override void Arrange(SKRect destination, float widthRequest, float heightRequest, float scale)
    //{
    //    if (!CanDraw)
    //        return;

    //    if (NeedMeasure)
    //    {
    //        var adjustedDestination = CalculateLayout(destination, SizeRequest.Width, SizeRequest.Height, scale);
    //        Measure(adjustedDestination.Width, adjustedDestination.Height, scale);
    //        IsLayoutDirty = true;
    //    }

    //    base.Arrange(destination, MeasuredSize.Units.Width, MeasuredSize.Units.Height, scale);
    //}



    /// <summary>
    /// In PIXELS
    /// </summary>
    /// <param name="destination"></param>
    /// <returns></returns>
    protected virtual SKRect GetContentAvailableRect(SKRect destination)
    {
        return destination;
    }

    /*
    public override ScaledRect GetOnScreenVisibleArea(float inflateByPixels = 0)
    {
        var inflated = DrawingRect;
        inflated.Inflate(inflateByPixels, inflateByPixels);
        return ScaledRect.FromPixels(inflated, RenderingScale);
    }


    protected override void Paint(SkiaDrawingContext ctx, SKRect destination, float scale, object arguments)
    {
        base.Paint(ctx, destination, scale, arguments);

        DrawViews(ctx, destination, scale);
    }


    protected override int DrawViews(SkiaDrawingContext context, SKRect destination, float scale,
        bool debug = false)
    {
        if (!IsContentActive || destination.Width <= 0 || destination.Height <= 0)
        {
            return 0;
        }
        var drawViews = new List<SkiaControl> { Content };

        return RenderViewsList(drawViews, context, destination, scale);
    }

 public override void SetChildren(IEnumerable<SkiaControl> views)
       {
           //do not use subviews as we are using Content property for this control

           return;
       }



       public override void ApplyBindingContext()
       {
           base.ApplyBindingContext();

           if (Content?.BindingContext == null)
           {
               Content?.SetInheritedBindingContext(BindingContext);
           }
       }

protected virtual void SetContent(SkiaControl view)
       {
           var oldContent = Views.FirstOrDefault(x => x == Content);
           if (view != oldContent)
           {
               if (oldContent != null)
               {
                   RemoveSubView(oldContent);
               }
               if (view != null)
               {
                   AddSubView(view);
               }
           }
       }
        */

    public SKRect ContentAvailableSpace { get; protected set; }

    private SkiaControl _oldContent;

    protected virtual void SetContent(SkiaControl view)
    {
        if (_oldContent == view)
        {
            return;
        }
        if (_oldContent != null)
        {
            RemoveSubView(_oldContent);
        }
        if (view != null)
        {
            AddSubView(view);
        }
        _oldContent = view;
    }


    #region PROPERTIES


    private static void OnReplaceContent(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is ContentLayout control)
        {
            control.SetContent(newvalue as SkiaControl);
        }
    }

    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content),
        typeof(SkiaControl), typeof(ContentLayout),
        null,
        propertyChanged: OnReplaceContent);

    public SkiaControl Content
    {
        get { return (SkiaControl)GetValue(ContentProperty); }
        set { SetValue(ContentProperty, value); }
    }



    #endregion







}
