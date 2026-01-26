using System.Runtime.InteropServices;

namespace DrawnUi.Controls;

public partial class SkiaDecoratedGrid : SkiaLayout
{
    public SkiaDecoratedGrid()
    {
        this.Type = LayoutType.Grid;
    }

    public static SkiaGradient HorizontalGradient = new SkiaGradient
    {

        Colors = new List<Color>
        {
            Color.FromArgb("#00E8E3D7"),
            Color.FromArgb("#78E8E3D7"),
            Color.FromArgb("#78E8E3D7"),
            Color.FromArgb("#00E8E3D7"),
        },
        ColorPositions = new double[] { 0.0, 0.1, 0.9, 1.0 },
        StartXRatio = 0,
        StartYRatio = 0,
        EndYRatio = 0,
        EndXRatio = 1,
        Type = GradientType.Linear
    };

    public static SkiaGradient VerticalGradient = new SkiaGradient
    {
        Colors = new List<Color>
        {
            Color.FromArgb("#00E8E3D7"),
            Color.FromArgb("#78E8E3D7"),
            Color.FromArgb("#78E8E3D7"),
            Color.FromArgb("#00E8E3D7"),
        },
        ColorPositions = new double[] { 0.0, 0.1, 0.9, 1.0 },
        StartXRatio = 0,
        StartYRatio = 0,
        EndYRatio = 1,
        EndXRatio = 0,
        Type = GradientType.Linear
    };

    public static readonly BindableProperty HorizontalLineProperty = BindableProperty.Create(nameof(HorizontalLine),
        typeof(SkiaGradient),
        typeof(SkiaDecoratedGrid),
        HorizontalGradient, propertyChanged: OnLinesChanged);

    public SkiaGradient HorizontalLine
    {
        get { return (SkiaGradient)GetValue(HorizontalLineProperty); }
        set { SetValue(HorizontalLineProperty, value); }
    }


    public static readonly BindableProperty VerticalLineProperty = BindableProperty.Create(nameof(VerticalLine),
        typeof(SkiaGradient),
        typeof(SkiaDecoratedGrid),
        VerticalGradient, propertyChanged: OnLinesChanged);
    public SkiaGradient VerticalLine
    {
        get { return (SkiaGradient)GetValue(VerticalLineProperty); }
        set { SetValue(VerticalLineProperty, value); }
    }

    public override void Invalidate()
    {
        ContainerLines?.Dispose();
        ContainerLines = null;
        base.Invalidate();
    }

    private static void OnLinesChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaDecoratedGrid control)
        {
            control.UpdateLines();

        }
    }

    protected SkiaLayout ContainerLines { get; set; }

    protected void UpdateLines()
    {
        CreateLines();
        Update();
    }

    public override void OnDisposing()
    {
        if (ContainerLines != null)
        {
            ContainerLines.Dispose();
            ContainerLines = null;
        }
        base.OnDisposing();
    }

    public virtual void CreateLines()
    {
        if (this.GridStructure == null)
        {
            return;
        }

        var kill = ContainerLines;

        ContainerLines = new()
        {
            ZIndex = -1,
            UseCache = SkiaCacheType.Operations,
            Tag = "hline",
            IsOverlay = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            MeasuredSize = this.MeasuredSize,
            NeedMeasure = false
        };

        kill?.Dispose();

        if (VerticalLine != null)
        {
            var col = 0;
            foreach (var definition in GridStructure.Columns)
            {
                if (col > 0)
                {
                    var offset = GridStructure.LeftEdgeOfColumn(col) - ColumnSpacing;

                    ContainerLines.AddSubView(new SkiaControl()
                    {
                        Tag = "vline",
                        UseCache = SkiaCacheType.Operations,
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Fill,
                        FillGradient = VerticalLine,
                        WidthRequest = ColumnSpacing,
                        TranslationX = (float)offset
                    });

                }

                col++;
            }

        }

        if (HorizontalLine != null)
        {
            var row = 0;
            foreach (var definition in GridStructure.Rows.ToList())
            {
                if (row > 0)
                {
                    var offset = GridStructure.TopEdgeOfRow(row) - RowSpacing;

                    ContainerLines.AddSubView(new SkiaShape()
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Start,
                        FillGradient = HorizontalLine,
                        BackgroundColor = Colors.Black,
                        HeightRequest = RowSpacing,
                        StrokeWidth = 0,
                        TranslationY = (float)offset
                    });
                }
                row++;
            }
        }
    }

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        UpdateLines();
    }

    protected override void PostArrange(SKRect destination, float widthRequest, float heightRequest, float scale)
    {
        base.PostArrange(destination, widthRequest, heightRequest, scale);

        if (ContainerLines == null)
        {
            CreateLines();
        }
    }

    protected override void Draw(DrawingContext context)
    {
        base.Draw(context);

        if (ContainerLines != null)
        {
            ContainerLines.Render(context.WithDestination(GetDrawingRectForChildren(Destination, context.Scale)));
        }

        FinalizeDrawingWithRenderObject(context);
    }
}
