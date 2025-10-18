using System.Collections.ObjectModel;
using System.Collections.Specialized;
using DrawnUi.Infrastructure.Xaml;

namespace DrawnUi.Draw;

[ContentProperty("Shadows")]
public class ChainDropShadowsEffect : BaseChainedEffect
{
    #region PROPERTIES

    public static readonly BindableProperty ShadowsProperty = BindableProperty.Create(
        nameof(Shadows),
        typeof(IList<SkiaShadow>),
        typeof(ChainDropShadowsEffect),
        defaultValueCreator: (instance) =>
        {
            var created = new SkiaShadowsCollection();
            created.CollectionChanged += ((ChainDropShadowsEffect)instance).OnShadowCollectionChanged;
            return created;
        },
        validateValue: (bo, v) => v is IList<SkiaShadow>,
        propertyChanged: ShadowsPropertyChanged,
        coerceValue: CoerceShadows);

    public IList<SkiaShadow> Shadows
    {
        get => (IList<SkiaShadow>)GetValue(ShadowsProperty);
        set => SetValue(ShadowsProperty, value);
    }

    private static object CoerceShadows(BindableObject bindable, object value)
    {
        if (!(value is ReadOnlyCollection<SkiaShadow> readonlyCollection))
        {
            return value;
        }

        return new ReadOnlyCollection<SkiaShadow>(
            readonlyCollection.ToList());
    }

    private static void ShadowsPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        var effect = (ChainDropShadowsEffect)bindable;

        var enumerableShadows = (IEnumerable<SkiaShadow>)newvalue;

        if (oldvalue != null)
        {
            if (oldvalue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= effect.OnShadowCollectionChanged;
            }

            if (oldvalue is IEnumerable<SkiaShadow> oldList)
            {
                foreach (var shade in oldList)
                {
                    shade.Dettach();
                }
            }
        }

        foreach (var shade in enumerableShadows)
        {
            shade.Attach(effect);
        }

        if (newvalue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged -= effect.OnShadowCollectionChanged;
            newCollection.CollectionChanged += effect.OnShadowCollectionChanged;
        }

        effect.Update();
    }

    private void OnShadowCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (SkiaShadow newSkiaPropertyShadow in e.NewItems)
                {
                    newSkiaPropertyShadow.Attach(this);
                }

                break;

            case NotifyCollectionChangedAction.Reset:
            case NotifyCollectionChangedAction.Remove:
                foreach (SkiaShadow oldSkiaPropertyShadow in e.OldItems ?? new SkiaShadow[0])
                {
                    oldSkiaPropertyShadow.Dettach();
                }

                break;
        }

        Update();
    }

    #endregion

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (Shadows != null)
        {
            foreach (var skiaShadow in Shadows)
            {
                skiaShadow.BindingContext = this.BindingContext;
            }
        }
    }

    public override ChainEffectResult Draw(DrawingContext ctx, Action<DrawingContext> drawControl)
    {
        if (NeedApply)
        {
            if (Paint == null)
            {
                Paint = new();
            }

            var restore = 0;

            //draw every shadow without the controls itsselfs
            if (Shadows != null)
            {
                foreach (var shadow in Shadows)
                {
                    //SkiaControl.AddShadowFilter(paint, shadow, Parent.RenderingScale);

                    Paint.ImageFilter = SKImageFilter.CreateDropShadowOnly(
                        (float)Math.Round(shadow.X * Parent.RenderingScale),
                        (float)Math.Round(shadow.Y * Parent.RenderingScale),
                        (float)shadow.Blur, (float)shadow.Blur,
                        shadow.Color.ToSKColor());

                    var saved = ctx.Context.Canvas.SaveLayer(Paint);

                    drawControl(ctx);

                    ctx.Context.Canvas.RestoreToCount(saved);
                }
            }


            return ChainEffectResult.Create(false);
        }

        return base.Draw(ctx, drawControl);
    }

    public override bool NeedApply
    {
        get { return base.NeedApply && Shadows.Count > 0; }
    }
}
