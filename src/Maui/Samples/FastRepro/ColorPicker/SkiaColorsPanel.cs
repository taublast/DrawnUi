using SkiaSharp.Views.Maui;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using SkiaSharp;

namespace Sandbox.Views.Controls;

/// <summary>
/// User by color picker etc Using shape to be able to clip and stroke etc
/// </summary>
public class SkiaColorsPanel : SkiaShape
{
    protected override void PaintBackground(SkiaDrawingContext ctx, SKRect destination, SKPoint[] radii, float minSize, SKPaint paint)
    {
        SKShader kill = null;

        var colors = PrimaryColors;
        if (PrimaryColors.Count == 0)
        {
            colors = DefaultPrimaryColors;
        }

        if (colors != null && colors.Count > 1)
        {
            kill = paint.Shader;
            var shader = SKShader.CreateLinearGradient(
                   new SKPoint(destination.Left, destination.Top),
                   Direction == ColorFlowDirection.Vertical ?
                       new SKPoint(destination.Right, destination.Top)
                       : new SKPoint(destination.Left, destination.Bottom),
                   colors.Select(x => x.ToSKColor()).ToArray(),
                   null,
                   SKShaderTileMode.Clamp);

            paint.Shader = shader;
        }

        base.PaintBackground(ctx, destination, radii, minSize, paint);

        kill?.Dispose();

        if (SecondaryColors != null && SecondaryColors.Count > 1)
        {
            // Draw secondary gradient color spectrum (Lighness whatever..)
            using (var paint2 = new SKPaint())
            {
                paint2.IsAntialias = true;

                // create the gradient shader between secondary colors
                using (var shader = SKShader.CreateLinearGradient(
                           new SKPoint(destination.Left, destination.Top),
                           Direction == ColorFlowDirection.Vertical ?
                               new SKPoint(destination.Left, destination.Bottom)
                               : new SKPoint(destination.Right, destination.Top),
                           SecondaryColors.Select(s => s.ToSKColor()).ToArray(),
                           null,
                           SKShaderTileMode.Clamp))
                {
                    paint2.Shader = shader;
                    paint2.BlendMode = ColorsBlendMode;
                    ctx.Canvas.DrawRect(destination, paint2);
                }
            }
        }
    }

    void UpdatePrimaryGradient()
    {
        Update();
    }

    public static readonly BindableProperty ColorsBlendModeProperty = BindableProperty.Create(nameof(ColorsBlendMode),
        typeof(SKBlendMode), typeof(SkiaColorsPanel),
        SKBlendMode.SrcOver,
        propertyChanged: (bindable, value, newValue) =>
        {
            ((SkiaColorsPanel)bindable).Update();
        });
    public SKBlendMode ColorsBlendMode
    {
        get { return (SKBlendMode)GetValue(ColorsBlendModeProperty); }
        set { SetValue(ColorsBlendModeProperty, value); }
    }

    #region PRIMARY COLORS

    public static List<Color> DefaultPrimaryColors = new()
    {
        new Color(255, 0, 0), // Red
        new Color(255, 255, 0), // Yellow
        new Color(0, 255, 0), // Green (Lime)
        new Color(0, 255, 255), // Aqua
        new Color(0, 0, 255), // Blue
        new Color(255, 0, 255), // Fuchsia
        new Color(255, 0, 0), // Red
    };

    public static readonly BindableProperty PrimaryColorsProperty = BindableProperty.Create(
        nameof(PrimaryColors),
        typeof(IList<Color>),
        typeof(SkiaColorsPanel),
        defaultValueCreator: (instance) =>
        {
            var created = new ObservableCollection<Color>();
            if (instance is SkiaColorsPanel control)
            {
                created.CollectionChanged += control.OnSkiaPrimaryColorsCollectionChanged;
            }
            return created;
        },
        validateValue: (bo, v) => v is IList<Color>,
        propertyChanged: PrimaryColorsPropertyChanged,
        coerceValue: CoercePrimaryColors);


    public IList<Color> PrimaryColors
    {
        get => (IList<Color>)GetValue(PrimaryColorsProperty);
        set => SetValue(PrimaryColorsProperty, value);
    }

    private static object CoercePrimaryColors(BindableObject bindable, object value)
    {
        if (!(value is ReadOnlyCollection<Color> readonlyCollection))
        {
            return value;
        }

        return new ReadOnlyCollection<Color>(
            readonlyCollection.Select(s => s)
                .ToList());
    }

    private static void PrimaryColorsPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {

        if (bindable is SkiaColorsPanel gradient)
        {
            if (oldvalue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= gradient.OnSkiaPrimaryColorsCollectionChanged;
            }
            if (newvalue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged -= gradient.OnSkiaPrimaryColorsCollectionChanged;
                newCollection.CollectionChanged += gradient.OnSkiaPrimaryColorsCollectionChanged;
            }

            gradient.UpdatePrimaryGradient();
        }

    }

    private void OnSkiaPrimaryColorsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        this.UpdatePrimaryGradient();
    }

    #endregion

    #region SECONDARY COLORS


    public static readonly BindableProperty SecondaryColorsProperty = BindableProperty.Create(
        nameof(SecondaryColors),
        typeof(IList<Color>),
        typeof(SkiaColorsPanel),
        defaultValueCreator: (instance) =>
        {
            var created = new ObservableCollection<Color>();
            if (instance is SkiaColorsPanel control)
            {
                created.CollectionChanged += control.OnSkiaSecondaryColorsCollectionChanged;
            }
            return created;
        },
        validateValue: (bo, v) => v is IList<Color>,
        propertyChanged: SecondaryColorsPropertyChanged,
        coerceValue: CoerceSecondaryColors);


    public IList<Color> SecondaryColors
    {
        get => (IList<Color>)GetValue(SecondaryColorsProperty);
        set => SetValue(SecondaryColorsProperty, value);
    }

    private static object CoerceSecondaryColors(BindableObject bindable, object value)
    {
        if (!(value is ReadOnlyCollection<Color> readonlyCollection))
        {
            return value;
        }

        return new ReadOnlyCollection<Color>(
            readonlyCollection.Select(s => s)
                .ToList());
    }

    private static void SecondaryColorsPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {

        if (bindable is SkiaColorsPanel gradient)
        {
            if (oldvalue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= gradient.OnSkiaSecondaryColorsCollectionChanged;
            }
            if (newvalue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged -= gradient.OnSkiaSecondaryColorsCollectionChanged;
                newCollection.CollectionChanged += gradient.OnSkiaSecondaryColorsCollectionChanged;
            }

            gradient.Parent?.Update();
        }

    }

    private void OnSkiaSecondaryColorsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        this.Update();
    }

    #endregion

    public static readonly BindableProperty DirectionProperty = BindableProperty.Create(nameof(Direction),
    typeof(ColorFlowDirection),
    typeof(SkiaColorsPanel),
    Controls.ColorFlowDirection.Vertical,
    propertyChanged: NeedDraw);
    public ColorFlowDirection Direction
    {
        get { return (ColorFlowDirection)GetValue(DirectionProperty); }
        set { SetValue(DirectionProperty, value); }
    }

    SKShader shaderPrimary;

    public override void OnDisposing()
    {
        base.OnDisposing();

        shaderPrimary?.Dispose();
    }
}