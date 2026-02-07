namespace DrawnUi.Controls;

/// <summary>
/// A decorated Column layout that draws separator lines between rows and columns.
/// Unlike SkiaDecoratedGrid, this is row-aware - vertical separators are only drawn
/// where a specific row has multiple columns (supports DynamicColumns behavior).
/// </summary>
public partial class SkiaDecoratedColumn : SkiaStack
{

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

    public static readonly BindableProperty HorizontalLineProperty = BindableProperty.Create(
        nameof(HorizontalLine),
        typeof(SkiaGradient),
        typeof(SkiaDecoratedColumn),
        HorizontalGradient,
        propertyChanged: OnLinesChanged);

    public SkiaGradient HorizontalLine
    {
        get { return (SkiaGradient)GetValue(HorizontalLineProperty); }
        set { SetValue(HorizontalLineProperty, value); }
    }

    public static readonly BindableProperty VerticalLineProperty = BindableProperty.Create(
        nameof(VerticalLine),
        typeof(SkiaGradient),
        typeof(SkiaDecoratedColumn),
        VerticalGradient,
        propertyChanged: OnLinesChanged);

    public SkiaGradient VerticalLine
    {
        get { return (SkiaGradient)GetValue(VerticalLineProperty); }
        set { SetValue(VerticalLineProperty, value); }
    }

    public override void Invalidate()
    {
        DisposeObject(ContainerLines);
        ContainerLines = null;
        base.Invalidate();
    }

    private static void OnLinesChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaDecoratedColumn control)
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
        var structure = LatestStackStructure;
        if (structure == null || structure.Length == 0)
        {
            return;
        }

        var kill = ContainerLines;

        ContainerLines = new()
        {
            ZIndex = -1,
            UseCache = SkiaCacheType.Operations,
            Tag = "lines",
            IsOverlay = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            MeasuredSize = this.MeasuredSize,
            NeedMeasure = false
        };

        DisposeObject(kill);

        // Draw vertical lines between columns (row-aware, optimized for consecutive rows)
        if (VerticalLine != null)
        {
            int spanStartRow = -1;
            int spanColumnCount = 0;

            for (int row = 0; row <= structure.MaxRows; row++)
            {
                var columnsInRow = row < structure.MaxRows ? structure.GetColumnCountForRow(row) : 0;

                // Check if this row continues the current span
                if (columnsInRow == spanColumnCount && columnsInRow > 1)
                {
                    // Continue the span
                    continue;
                }

                // End the previous span if there was one
                if (spanStartRow >= 0 && spanColumnCount > 1)
                {
                    // Draw vertical lines for the span from spanStartRow to row-1
                    var firstRowCells = structure.GetRow(spanStartRow).ToList();
                    var lastRowCells = structure.GetRow(row - 1).ToList();

                    for (int col = 1; col < spanColumnCount; col++)
                    {
                        var topCell = firstRowCells[col];
                        var bottomCell = lastRowCells[col];
                        if (topCell == null || bottomCell == null)
                            continue;

                        var offsetX = topCell.Destination.Left / RenderingScale - ColumnSpacing;
                        var offsetY = topCell.Destination.Top / RenderingScale;
                        var height = (bottomCell.Destination.Bottom - topCell.Destination.Top) / RenderingScale;

                        ContainerLines.AddSubView(new SkiaControl()
                        {
                            Tag = "vline",
                            UseCache = SkiaCacheType.Operations,
                            HorizontalOptions = LayoutOptions.Start,
                            VerticalOptions = LayoutOptions.Start,
                            FillGradient = VerticalLine,
                            WidthRequest = ColumnSpacing,
                            HeightRequest = height,
                            TranslationX = (float)offsetX,
                            TranslationY = (float)offsetY
                        });
                    }
                }

                // Start a new span if this row has multiple columns
                if (columnsInRow > 1)
                {
                    spanStartRow = row;
                    spanColumnCount = columnsInRow;
                }
                else
                {
                    spanStartRow = -1;
                    spanColumnCount = 0;
                }
            }
        }

        // Draw horizontal lines between rows
        if (HorizontalLine != null)
        {
            for(int row = 1; row < structure.MaxRows; row++)
            {
                var control = structure.Get(0, row);
                var offset = control.Destination.Top / RenderingScale - RowSpacing;

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

        if (ContainerLines == null)
        {
            CreateLines();
        }

        if (ContainerLines != null)
        {
            ContainerLines.Render(context.WithDestination(GetDrawingRectForChildren(Destination, context.Scale)));
        }

        FinalizeDrawingWithRenderObject(context);
    }
}
