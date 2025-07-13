using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using DrawnUi.Infrastructure.Xaml;
using Microsoft.Maui.Graphics;

namespace DrawnUi.Draw
{
    /// <summary>
    /// A versatile visual element that can render various shapes such as rectangles, circles, 
    /// ellipses, polygons, paths, and lines. Supports customizable fills, strokes, shadows, 
    /// and gradients. Can also serve as a container for child elements with content clipping.
    /// </summary>
    /// <remarks>
    /// SkiaShape can be used to create a wide variety of UI elements such as:
    /// - Buttons, cards, and panels with custom shapes and effects
    /// - Visual indicators, gauges, and progress bars
    /// - Custom decorative elements with shadows and gradients
    /// - Clipping containers for complex layouts
    /// - Path-based icons and vector graphics
    /// 
    /// Use properties like Type, CornerRadius, StrokeWidth, and BackgroundColor to customize appearance.
    /// For complex shapes, use PathData or Points properties to define custom geometries.
    /// </remarks>
    public partial class SkiaShape : SkiaLayout
    {
        public override void ApplyBindingContext()
        {
            foreach (var shade in Shadows)
            {
                shade.BindingContext = BindingContext;
            }

            if (Bevel != null)
            {
                Bevel.BindingContext = BindingContext;
            }

            base.ApplyBindingContext();
        }

        /// <summary>
        /// Available size for children content, accounting for padding and stroke
        /// </summary>
        public SKRect MeasuredStrokeAwareChildrenSize { get; protected set; }

        /// <summary>
        /// Area to use for clipping inside stroke
        /// </summary>
        public SKRect MeasuredStrokeAwareClipSize { get; protected set; } //todo!!!

        /// <summary>
        /// Size we must use to draw stroke to fit inside available destination
        /// </summary>
        public SKRect MeasuredStrokeAwareSize { get; protected set; }

        #region PROPERTIES

        public static readonly BindableProperty PathDataProperty = BindableProperty.Create(nameof(PathData),
            typeof(string), typeof(SkiaShape),
            null,
            propertyChanged: NeedSetType);

        private static void NeedSetType(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaShape control)
            {
                control.SetupType();
            }
        }

        /// <summary>
        /// For Type = Path, use the path markup syntax
        /// </summary>
        /// <summary>
        /// Gets or sets the SVG path data string used to define a custom path shape.
        /// </summary>
        /// <remarks>
        /// This property is used when Type is set to ShapeType.Path. The string should follow
        /// standard SVG path syntax, for example:
        /// "M0,0L15.825011,8.0009766 31.650999,15.997986 15.825011,23.998993 0,32 0,15.997986z"
        /// 
        /// Common path commands:
        /// - M: MoveTo - Starts a new sub-path (x,y)
        /// - L: LineTo - Draws a line from current position
        /// - H: Horizontal line - Draws a horizontal line
        /// - V: Vertical line - Draws a vertical line
        /// - C: Curve - Cubic Bezier curve
        /// - Q: Quadratic curve - Quadratic Bezier curve
        /// - Z: Close path - Closes the current sub-path
        /// 
        /// The path will be automatically scaled to fit the control dimensions.
        /// </remarks>
        public string PathData
        {
            get { return (string)GetValue(PathDataProperty); }
            set { SetValue(PathDataProperty, value); }
        }

        public static new readonly BindableProperty TypeProperty = BindableProperty.Create(nameof(Type),
            typeof(ShapeType), typeof(SkiaShape),
            ShapeType.Rectangle,
            propertyChanged: NeedSetType);

        /// <summary>
        /// Gets or sets the type of shape to render.
        /// </summary>
        /// <remarks>
        /// Available shape types:
        /// - Rectangle: A rectangle with optional corner radius (default)
        /// - Circle: A perfect circle that fits within the bounds
        /// - Ellipse: An oval shape that fills the bounds
        /// - Path: A custom shape defined by the PathData property
        /// - Polygon: A multi-point shape defined by the Points collection
        /// - Line: An open path connecting points in the Points collection
        /// - Arc: A curved segment defined by Value1 (start angle) and Value2 (sweep angle)
        /// 
        /// Changing the Type property may require setting additional properties for proper rendering,
        /// such as PathData for Path type or Points for Polygon/Line types.
        /// </remarks>
        public new ShapeType Type
        {
            get { return (ShapeType)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }

        #region StrokeGradient

        private const string nameStrokeGradient = "StrokeGradient";

        public static readonly BindableProperty StrokeGradientProperty = BindableProperty.Create(
            nameStrokeGradient,
            typeof(SkiaGradient),
            typeof(SkiaShape),
            null,
            propertyChanged: StrokeGradientPropertyChanged);

        /// <summary>
        /// Gets or sets the gradient to be applied to the shape's stroke.
        /// </summary>
        /// <remarks>
        /// When set, this gradient will be used for the stroke color instead of the solid 
        /// StrokeColor. The gradient can be defined with multiple colors, stops, and a gradient 
        /// direction.
        /// 
        /// Example XAML usage:
        /// <code>
        /// &lt;draw:SkiaShape&gt;
        ///   &lt;draw:SkiaShape.StrokeGradient&gt;
        ///     &lt;draw:SkiaGradient StartColor="Blue" EndColor="Green" Type="Linear" /&gt;
        ///   &lt;/draw:SkiaShape.StrokeGradient&gt;
        /// &lt;/draw:SkiaShape&gt;
        /// </code>
        /// 
        /// The StrokeWidth property must be set to a non-zero value for the gradient to be visible.
        /// </remarks>
        public SkiaGradient StrokeGradient
        {
            get { return (SkiaGradient)GetValue(StrokeGradientProperty); }
            set { SetValue(StrokeGradientProperty, value); }
        }

        private static void StrokeGradientPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaControl skiaControl)
            {
                if (oldvalue is SkiaGradient skiaGradientOld)
                {
                    skiaGradientOld.Parent = null;
                    skiaGradientOld.BindingContext = null;
                }

                if (newvalue is SkiaGradient skiaGradient)
                {
                    skiaGradient.Parent = skiaControl;
                    skiaGradient.BindingContext = skiaControl.BindingContext;
                }

                skiaControl.Update();
            }
        }

        #endregion

        public static readonly BindableProperty ClipBackgroundColorProperty = BindableProperty.Create(
            nameof(ClipBackgroundColor),
            typeof(bool),
            typeof(SkiaShape),
            false, propertyChanged: NeedDraw);

        /// <summary>
        /// Gets or sets whether the background color is clipped out, allowing transparent areas with shadows.
        /// </summary>
        /// <remarks>
        /// When set to true, the shape's background color will be clipped out, creating a "hollow" shape
        /// that shows content beneath it while still displaying any shadows applied to the shape.
        /// 
        /// This is particularly useful for:
        /// - Creating floating shadows without a visible shape
        /// - Adding depth effects while preserving transparency
        /// - Creating cutout effects where only the shape's outline and shadow are visible
        /// 
        /// Example use case: A floating card shadow effect where the card itself is transparent.
        /// </remarks>
        public bool ClipBackgroundColor
        {
            get { return (bool)GetValue(ClipBackgroundColorProperty); }
            set { SetValue(ClipBackgroundColorProperty, value); }
        }

        public static readonly BindableProperty StrokeWidthProperty = BindableProperty.Create(
            nameof(StrokeWidth),
            typeof(double),
            typeof(SkiaShape),
            0.0,
            propertyChanged: NeedInvalidateMeasure);

        /// <summary>
        /// Gets or sets the width of the stroke (outline) in device-independent units.
        /// If you set it negative it will be in PIXELS instead of point.
        /// </summary>
        /// <remarks>
        /// - A value of 0 (default) means no stroke will be drawn
        /// - The stroke is drawn centered on the shape's path
        /// - For thin lines (e.g., single-pixel), use values around 1-2
        /// - For thicker borders, use larger values
        /// - Must be combined with a non-transparent StrokeColor to be visible
        /// - Affects the layout calculations to preserve the interior area of the shape
        /// 
        /// The stroke is always drawn on top of the shape's fill and any child elements.
        /// </remarks>
        public double StrokeWidth
        {
            get { return (double)GetValue(StrokeWidthProperty); }
            set { SetValue(StrokeWidthProperty, value); }
        }

        public static readonly BindableProperty StrokeCapProperty = BindableProperty.Create(
            nameof(StrokeCap),
            typeof(SKStrokeCap),
            typeof(SkiaShape),
            SKStrokeCap.Round,
            propertyChanged: NeedDraw);

        /// <summary>
        /// Gets or sets the cap style for stroke line ends and the join style for corners.
        /// </summary>
        /// <remarks>
        /// Available cap styles:
        /// - Round (default): Rounds the ends of lines and smooths the corners
        /// - Butt: Creates flat ends exactly at the end points, without extension
        /// - Square: Creates flat ends that extend beyond the end points by half the stroke width
        /// 
        /// This property affects:
        /// - How line segments are joined at corners (as a StrokeJoin)
        /// - How lines end at termination points
        /// - The appearance of dashed lines when StrokePath is used
        /// 
        /// For Line and Path shapes with sharp angles, Round provides the smoothest appearance.
        /// </remarks>
        public SKStrokeCap StrokeCap
        {
            get { return (SKStrokeCap)GetValue(StrokeCapProperty); }
            set { SetValue(StrokeCapProperty, value); }
        }

        public static readonly BindableProperty LayoutChildrenProperty = BindableProperty.Create(
            nameof(LayoutChildren),
            typeof(LayoutType),
            typeof(SkiaShape),
            LayoutType.Absolute,
            propertyChanged: NeedDraw);

        /// <summary>
        /// Gets or sets how child elements are arranged within the shape.
        /// </summary>
        /// <remarks>
        /// Available layout types:
        /// - Absolute (default): Position children using explicit coordinates and size
        /// - Column: Stack children vertically from top to bottom
        /// - Row: Stack children horizontally from left to right
        /// - Grid: Arrange children in rows and columns
        /// 
        /// The SkiaShape can work as a container with specific layout behavior while
        /// still clipping its content to the shape's boundaries. This allows for creating
        /// complex UI components with custom shapes and internal layouts.
        /// 
        /// Child elements are always clipped to the shape's boundaries regardless of layout type.
        /// </remarks>
        public LayoutType LayoutChildren
        {
            get { return (LayoutType)GetValue(LayoutChildrenProperty); }
            set { SetValue(LayoutChildrenProperty, value); }
        }

        public static readonly BindableProperty StrokeBlendModeProperty = BindableProperty.Create(
            nameof(StrokeBlendMode),
            typeof(SKBlendMode), typeof(SkiaShape),
            SKBlendMode.SrcOver,
            propertyChanged: NeedDraw);

        /// <summary>
        /// Gets or sets the blend mode used when rendering the stroke.
        /// </summary>
        /// <remarks>
        /// Blend modes control how the stroke color combines with the underlying content:
        /// 
        /// - SrcOver (default): Normal alpha blending
        /// - Multiply: Multiplies colors, resulting in darker colors
        /// - Screen: Opposite of multiply, resulting in lighter colors
        /// - Plus: Adds colors together, creating additive blending
        /// - Difference: Subtracts colors, creating inverted effects
        /// - Overlay: Combines multiply and screen, enhancing contrast
        /// - HardLight: Similar to overlay but with stronger effect
        /// - Darken: Selects the darker of the source and destination colors
        /// - Lighten: Selects the lighter of the source and destination colors
        /// - Clear: Makes the destination transparent
        /// 
        /// Creative uses include glow effects, neon strokes, or color inversion at edges.
        /// </remarks>
        public SKBlendMode StrokeBlendMode
        {
            get { return (SKBlendMode)GetValue(StrokeBlendModeProperty); }
            set { SetValue(StrokeBlendModeProperty, value); }
        }

        #endregion

        #region SHAPES

        protected static float[] GetDashArray(double[] input, float scale)
        {
            if (input == null || input.Length == 0)
            {
                return new float[0];
            }

            float[] array = new float[input.Length];

            for (int i = 0; i < input.Length; i++)
            {
                array[i] = Convert.ToSingle(Math.Round(input[i] * scale));
            }

            return array;
        }

        public struct ShapePaintArguments
        {
            public SKRect StrokeAwareSize { get; set; }
            public SKRect StrokeAwareChildrenSize { get; set; }
            public SKRect StrokeAwareClipSize { get; set; }
        }

        #endregion

        #region RENDERiNG

        public virtual void SetupType()
        {
            if (Type == ShapeType.Path)
            {
                var kill = DrawPath;
                if (!string.IsNullOrEmpty(PathData))
                {
                    DrawPath = SKPath.ParseSvgPathData(this.PathData);
                }
                else
                {
                    DrawPath = null;
                }

                if (kill != null)
                {
                    Tasks.StartDelayed(TimeSpan.FromSeconds(3), () => { kill.Dispose(); });
                }
            }

            Update();
        }


        public virtual bool WillStroke
        {
            get { return StrokeColor != TransparentColor && StrokeWidth != 0; }
        }

        protected float GetHalfStroke(float scale)
        {
            var pixelsStrokeWidth = StrokeWidth > 0
                ? (float)(StrokeWidth * scale)
                : (float)(-StrokeWidth);

            return (float)(pixelsStrokeWidth / 2.0f);
        }

        protected float GetSmallUnderStroke(float scale)
        {
            var pixelsStrokeWidth = StrokeWidth > 0
                ? (float)(StrokeWidth * scale)
                : (float)(-StrokeWidth);

            return (float)(pixelsStrokeWidth / 3.0f);
        }

        protected float GetStrokePixels(float scale)
        {
            var pixelsStrokeWidth = StrokeWidth > 0
                ? (float)(StrokeWidth * scale)
                : (float)(-StrokeWidth);

            return (float)(pixelsStrokeWidth);
        }

        protected float GetInflationForStroke(float halfStroke)
        {
            if (halfStroke < 0.5f)
            {
                return -1f;
            }

            return -(float)Math.Round(halfStroke);
        }

        protected SKRect CalculateContentSizeForStroke(SKRect destination, float scale)
        {
            if (WillStroke)
            {
                destination = CalculateShapeSizeForStroke(destination, scale);
            }

            var strokeAwareChildrenSize
                = ContractPixelsRect(destination, scale, Padding);

            return strokeAwareChildrenSize;
        }

        protected SKRect CalculateClipSizeForStroke(SKRect destination, float scale)
        {
            if (WillStroke)
            {
                var strokeAwareSize = CalculateShapeSizeForStroke(destination, scale);

                var contract = GetSmallUnderStroke(scale);
                
                var strokeAwareChildrenSize
                    = ContractPixelsRect(strokeAwareSize, contract);

                return strokeAwareChildrenSize;
            }

            return destination;
        }

        protected SKRect CalculateShapeSizeForStroke(SKRect destination, float scale)
        {
            if (WillStroke)
            {
                float halfStroke = 0;
                float inflate;
                var x = destination.Left;
                var y = destination.Top;

                var strokeAwareSize = new SKRect(x, y,
                    (float)(x + (destination.Width)),
                    (float)(y + (destination.Height)));


                halfStroke = GetHalfStroke(scale);
                inflate = GetInflationForStroke(halfStroke);

                strokeAwareSize =
                    SKRect.Inflate(strokeAwareSize, inflate, inflate);

                strokeAwareSize = new SKRect(
                    (float)Math.Ceiling(strokeAwareSize.Left),
                    (float)Math.Ceiling(strokeAwareSize.Top),
                    (float)Math.Floor(strokeAwareSize.Right),
                    (float)Math.Floor(strokeAwareSize.Bottom));

                return strokeAwareSize;
            }

            return destination;
        }

        protected void CalculateSizeForStroke(SKRect destination, float scale)
        {
            MeasuredStrokeAwareSize = CalculateShapeSizeForStroke(destination, scale);
            MeasuredStrokeAwareClipSize = CalculateClipSizeForStroke(destination, scale);
            MeasuredStrokeAwareChildrenSize = CalculateContentSizeForStroke(destination, scale);

            //rescale the path to match container
            if (Type == ShapeType.Path)
            {
                if (DrawPath != null)
                {
                    DrawPath.GetTightBounds(out var bounds);
                    using SKPath stretched = new();
                    stretched.AddPath(DrawPath);

                    float halfStroke = GetHalfStroke(scale);
                    float scaleX = MeasuredStrokeAwareSize.Width / (bounds.Width + halfStroke);
                    float scaleY = MeasuredStrokeAwareSize.Height / (bounds.Height + halfStroke);

                    float translateX = (MeasuredStrokeAwareSize.Width - (bounds.Width + halfStroke) * scaleX) / 2 -
                                       bounds.Left * scaleX;
                    float translateY = (MeasuredStrokeAwareSize.Height - (bounds.Height + halfStroke) * scaleY) / 2 -
                                       bounds.Top * scaleY;

                    SKMatrix matrix = SKMatrix.CreateScale(scaleX, scaleY);
                    matrix = SKMatrix.Concat(matrix, SKMatrix.CreateTranslation(translateX / 2, translateY / 2));

                    stretched.Transform(matrix);
                    stretched.Offset(halfStroke, halfStroke);

                    DrawPathResized.Reset();
                    DrawPathResized.AddPath(stretched);
                }
            }
        }

        protected SKRect CalculateSizeForStrokeFromContent(SKRect destination, float scale)
        {
            if (WillStroke)
            {
                float halfStroke = GetHalfStroke(scale);
                float inflate = -GetInflationForStroke(halfStroke);

                var strokeAwareSize = new SKRect(
                    destination.Left - inflate,
                    destination.Top - inflate,
                    destination.Right + inflate,
                    destination.Bottom + inflate
                );

                strokeAwareSize = new SKRect(
                    (float)Math.Floor(strokeAwareSize.Left),
                    (float)Math.Floor(strokeAwareSize.Top),
                    (float)Math.Ceiling(strokeAwareSize.Right),
                    (float)Math.Ceiling(strokeAwareSize.Bottom)
                );

                return strokeAwareSize;
            }

            return destination;
        }

        public override void Arrange(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            base.Arrange(destination, widthRequest, heightRequest, scale);

            // need to do it everytime we arrange
            CalculateSizeForStroke(DrawingRect, scale);
        }

        protected override SKSize GetContentSizeForAutosizeInPixels()
        {
            if (WillStroke)
            {
                var rect = CalculateSizeForStrokeFromContent(
                    new(0, 0, ContentSize.Pixels.Width, ContentSize.Pixels.Height), ContentSize.Scale);
                return rect.Size;

                var halfStroke = GetHalfStroke(RenderingScale);
                var inflate = GetInflationForStroke(halfStroke);
                return new(ContentSize.Pixels.Width - inflate * 2, ContentSize.Pixels.Height - inflate * 2);
            }

            return base.GetContentSizeForAutosizeInPixels();
        }

        protected override ScaledSize MeasureContent(IEnumerable<SkiaControl> children, SKRect rectForChildrenPixels,
            float scale)
        {
            //nothing special just overriding for debug

            var measured = base.MeasureContent(children, rectForChildrenPixels, scale);

            return measured;
        }

        public SKPath DrawPathResized { get; } = new();
        public SKPath DrawPathAligned { get; } = new();


        public double BorderWithPixels
        {
            get { return (DrawingRect.Width - MeasuredStrokeAwareChildrenSize.Width); }
        }

        protected SKPaint RenderingPaint { get; set; }

        /// <summary>
        /// Gets or sets the parsed SKPath object created from the PathData property.
        /// </summary>
        /// <remarks>
        /// This field holds the SkiaSharp path object that is created by parsing the
        /// PathData string. It is automatically created when PathData is set and Type
        /// is ShapeType.Path. The path is used for both rendering and hit-testing.
        /// 
        /// The path is stored in its original form and is scaled/transformed during rendering
        /// to fit the shape's bounds.
        /// </remarks>
        protected SKPath DrawPath { get; set; } = new();

        protected SKPath DrawPathShape { get; set; } = new();

        /// <summary>
        /// Gets or sets the rounded rectangle used for rendering when Type is Rectangle with CornerRadius.
        /// </summary>
        /// <remarks>
        /// This object is created and reused during rendering to represent a rectangle with
        /// rounded corners. It is automatically configured with the appropriate dimensions
        /// and corner radii based on the shape's properties.
        /// 
        /// The object is created on demand and may be null until needed for rendering.
        /// </remarks>
        protected SKRoundRect DrawRoundedRect { get; set; }

        SKPath ClipContentPath { get; set; } = new();

        public override void OnDisposing()
        {
            RenderingPaint?.Dispose();
            RenderingPaint = null;

            DrawPath?.Dispose();
            DrawPathResized?.Dispose();
            DrawPathAligned?.Dispose();
            DrawRoundedRect?.Dispose();
            DrawPathShape?.Dispose();
            ClipContentPath?.Dispose();

            base.OnDisposing();
        }

        public override SKPath CreateClip(object arguments, bool usePosition, SKPath path = null)
        {
            path ??= new SKPath();

            var strokeAwareSize = MeasuredStrokeAwareSize;
            var strokeAwareChildrenSize = MeasuredStrokeAwareClipSize;

            if (arguments is ShapePaintArguments args)
            {
                strokeAwareSize = args.StrokeAwareSize;
                strokeAwareChildrenSize = args.StrokeAwareClipSize;
            }

            if (!usePosition)
            {
                var offsetToZero = new SKPoint(strokeAwareChildrenSize.Left - strokeAwareSize.Left,
                    strokeAwareChildrenSize.Top - strokeAwareSize.Top);
                strokeAwareChildrenSize = new(offsetToZero.X, offsetToZero.Y,
                    strokeAwareChildrenSize.Width + offsetToZero.X, strokeAwareChildrenSize.Height + offsetToZero.Y);
            }

            switch (Type)
            {
                case ShapeType.Path:
                    ShouldClipAntialiased = true;
                    path.AddPath(DrawPathResized);
                    break;

                case ShapeType.Circle:
                    ShouldClipAntialiased = true;
                    path.AddCircle(
                        (float)(strokeAwareChildrenSize.Left + strokeAwareChildrenSize.Width / 2.0f),
                        (float)(strokeAwareChildrenSize.Top + strokeAwareChildrenSize.Height / 2.0f),
                        (float)Math.Floor(Math.Min(strokeAwareChildrenSize.Width, strokeAwareChildrenSize.Height) /
                                          2.0f) + 0);
                    break;

                case ShapeType.Ellipse:
                    ShouldClipAntialiased = true;
                    path.AddOval(strokeAwareChildrenSize);
                    break;

                case ShapeType.Polygon:
                    ShouldClipAntialiased = true;
                    if (Points != null && Points.Count > 0)
                    {
                        path.Reset();

                        var scaleX = MeasuredStrokeAwareSize.Width;
                        var scaleY = MeasuredStrokeAwareSize.Height;
                        var offsetX = usePosition ? MeasuredStrokeAwareSize.Left : 0;
                        var offsetY = usePosition ? MeasuredStrokeAwareSize.Top : 0;

                        bool first = true;
                        foreach (var skiaPoint in Points)
                        {
                            var point = new SKPoint(
                                (float)Math.Round(offsetX + skiaPoint.X * scaleX),
                                (float)Math.Round(offsetY + skiaPoint.Y * scaleY));

                            if (first)
                            {
                                path.MoveTo(point);
                                first = false;
                            }
                            else
                            {
                                path.LineTo(point);
                            }
                        }

                        path.Close();
                    }

                    break;

                case ShapeType.Rectangle:
                default:
                    if (CornerRadius != default)
                    {
                        ShouldClipAntialiased = true;

                        var scaledRadiusLeftTop = (float)(CornerRadius.TopLeft * RenderingScale);
                        var scaledRadiusRightTop = (float)(CornerRadius.TopRight * RenderingScale);
                        var scaledRadiusLeftBottom = (float)(CornerRadius.BottomLeft * RenderingScale);
                        var scaledRadiusRightBottom = (float)(CornerRadius.BottomRight * RenderingScale);
                        var rrect = new SKRoundRect(strokeAwareChildrenSize);

                        // Step 3: Calculate the inner rounded rectangle corner radii
                        double maxValue = Math.Max(Math.Max(Padding.Left, Padding.Top),
                            Math.Max(Padding.Right, Padding.Bottom));
                        var strokeWidth = StrokeWidth > 0
                            ? (float)(StrokeWidth * RenderingScale)
                            : (float)(-StrokeWidth);

                        float cornerRadiusDifference = -
                            (float)(strokeWidth + maxValue * RenderingScale) / 2.0f;

                        scaledRadiusLeftTop = (float)(Math.Max(scaledRadiusLeftTop + cornerRadiusDifference, 0));
                        scaledRadiusRightTop = (float)(Math.Max(scaledRadiusRightTop + cornerRadiusDifference, 0));
                        scaledRadiusLeftBottom = (float)(Math.Max(scaledRadiusLeftBottom + cornerRadiusDifference, 0));
                        scaledRadiusRightBottom =
                            (float)(Math.Max(scaledRadiusRightBottom + cornerRadiusDifference, 0));

                        rrect.SetRectRadii(strokeAwareChildrenSize,
                            new[]
                            {
                                new SKPoint(scaledRadiusLeftTop, scaledRadiusLeftTop),
                                new SKPoint(scaledRadiusRightTop, scaledRadiusRightTop),
                                new SKPoint(scaledRadiusLeftBottom, scaledRadiusLeftBottom),
                                new SKPoint(scaledRadiusRightBottom, scaledRadiusRightBottom),
                            });
                        path.AddRoundRect(rrect);


                        //path.AddRoundRect(strokeAwareChildrenSize, innerCornerRadius, innerCornerRadius);
                    }
                    else
                    {
                        ShouldClipAntialiased = false;
                        path.AddRect(strokeAwareChildrenSize);
                    }

                    break;
            }

            return path;
        }

        /// <summary>
        /// Calculates corrected corner radii for the background rectangle to maintain visual consistency with the stroke
        /// </summary>
        /// <param name="originalRadii">The original radii used for the stroke</param>
        /// <param name="strokeRect">The rectangle used for drawing the stroke</param>
        /// <param name="backgroundRect">The contracted rectangle used for drawing the background</param>
        /// <returns>Corrected radii array for the background</returns>
        protected SKPoint[] GetCorrectedBackgroundRadii(SKPoint[] originalRadii, SKRect strokeRect,
            SKRect backgroundRect)
        {
            // Calculate how much the rectangle was contracted
            float widthContraction = strokeRect.Width - backgroundRect.Width;
            float heightContraction = strokeRect.Height - backgroundRect.Height;

            // Calculate the reduction per side
            float horizontalReduction = widthContraction / 2.0f;
            float verticalReduction = heightContraction / 2.0f;

            // Use the smaller reduction to maintain the circular nature of corners
            float radiusReduction = Math.Min(horizontalReduction, verticalReduction);

            var correctedRadii = new SKPoint[4];

            for (int i = 0; i < originalRadii.Length; i++)
            {
                // Reduce each radius component, but never go below 0
                float correctedX = Math.Max(0, originalRadii[i].X - radiusReduction);
                float correctedY = Math.Max(0, originalRadii[i].Y - radiusReduction);

                correctedRadii[i] = new SKPoint(correctedX, correctedY);
            }

            return correctedRadii;
        }

        protected virtual void PaintBackground(SkiaDrawingContext ctx,
            SKRect outRect,
            SKPoint[] radii,
            float minSize,
            SKPaint paint)
        {
            paint.BlendMode = this.FillBlendMode;
            paint.Style = SKPaintStyle.Fill;

            switch (Type)
            {
                case ShapeType.Rectangle:
                    if (CornerRadius != default)
                    {
                        paint.IsAntialias = true;

                        if (DrawRoundedRect == null)
                            DrawRoundedRect = new();

                        DrawRoundedRect.SetRectRadii(outRect, radii);
                        ctx.Canvas.DrawRoundRect(DrawRoundedRect, paint);
                    }
                    else
                        ctx.Canvas.DrawRect(outRect, paint);

                    break;

                case ShapeType.Polygon:

                    if (Points != null && Points.Count > 1)
                    {
                        DrawPathShape.Reset();

                        paint.StrokeJoin = MapStrokeCapToStrokeJoin(this.StrokeCap);

                        if (SmoothPoints > 0)
                        {
                            AddSmoothPath(DrawPathShape, Points, MeasuredStrokeAwareSize, SmoothPoints, true);
                        }
                        else
                        {
                            AddStraightPath(DrawPathShape, Points, MeasuredStrokeAwareSize, true);
                        }

                        ctx.Canvas.DrawPath(DrawPathShape, paint);
                    }

                    break;

                case ShapeType.Circle:
                    if (StrokeWidth == 0 || StrokeColor == TransparentColor)
                    {
                        paint.IsAntialias = true;
                    }

                    ctx.Canvas.DrawCircle(outRect.MidX, outRect.MidY, minSize / 2.0f, RenderingPaint);
                    break;

                case ShapeType.Ellipse:
                    if (StrokeWidth == 0 || StrokeColor == TransparentColor)
                    {
                        paint.IsAntialias = true;
                    }

                    DrawPathShape.Reset();
                    DrawPathShape.AddOval(outRect);
                    ctx.Canvas.DrawPath(DrawPathShape, paint);

                    break;

                case ShapeType.Path:
                    if (StrokeWidth == 0 || StrokeColor == TransparentColor)
                    {
                        paint.IsAntialias = true;
                    }


                    ctx.Canvas.DrawPath(DrawPathAligned, paint);

                    break;

                //case ShapeType.Arc: - has no background
            }
        }

        protected virtual SKStrokeJoin MapStrokeCapToStrokeJoin(SKStrokeCap strokeCap)
        {
            switch (strokeCap)
            {
                case SKStrokeCap.Round:
                    return SKStrokeJoin.Round;
                case SKStrokeCap.Square:
                    return SKStrokeJoin.Bevel;
                case SKStrokeCap.Butt:
                default:
                    return SKStrokeJoin.Miter;
            }
        }

        protected override void PaintWithShadows(DrawingContext ctx, Action render)
        {
            render(); //we will handle shadows by ourselves
        }

        public override DrawingContext AddPaintArguments(DrawingContext ctx)
        {
            return ctx.WithArgument(new(nameof(ContextArguments.ShapePaint),
                new ShapePaintArguments()
                {
                    StrokeAwareSize = MeasuredStrokeAwareSize,
                    StrokeAwareChildrenSize = MeasuredStrokeAwareChildrenSize,
                    StrokeAwareClipSize = MeasuredStrokeAwareClipSize
                }));
        }

        protected override void Paint(DrawingContext ctx)
        {
            var scale = ctx.Scale;
            var strokeAwareSize = MeasuredStrokeAwareSize;
            var strokeAwareChildrenSize = MeasuredStrokeAwareChildrenSize;
            var rectBackground = MeasuredStrokeAwareClipSize;

            ShapePaintArguments? arguments = null;
            if (ctx.GetArgument(nameof(ContextArguments.ShapePaint)) is ShapePaintArguments defined)
            {
                arguments = defined;
                strokeAwareSize = defined.StrokeAwareSize;
                strokeAwareChildrenSize = defined.StrokeAwareChildrenSize;
            }

            //we gonna set stroke On only when drawing the last pass
            //otherwise stroke antialiasing will not work
            var willStroke = StrokeColor != TransparentColor && StrokeWidth != 0;

            float pixelsStrokeWidth = StrokeWidth > 0
                ? (float)(StrokeWidth * scale)
                : (float)(-StrokeWidth);

            RenderingPaint ??= new SKPaint() { IsAntialias = true, };

            RenderingPaint.IsDither = IsDistorted;

            if (BackgroundColor != null)
            {
                RenderingPaint.Color = BackgroundColor.ToSKColor();
            }
            else
            {
                RenderingPaint.Color = SKColors.Transparent;
            }

            RenderingPaint.Style = SKPaintStyle.Fill;

            var minSize = Math.Min(strokeAwareSize.Height, strokeAwareSize.Width);

            var outRect = strokeAwareSize;

            if (Type == ShapeType.Path)
            {
                DrawPathAligned.Reset();
                DrawPathAligned.AddPath(DrawPathResized);
                DrawPathAligned.Offset(outRect.Left, outRect.Top);
            }

            CornerRadius scaledRadius = new(
                (CornerRadius.TopLeft * scale),
                (CornerRadius.TopRight * scale),
                (CornerRadius.BottomLeft * scale),
                (CornerRadius.BottomRight * scale));

            var radii = new SKPoint[]
            {
                new SKPoint((float)scaledRadius.TopLeft, (float)scaledRadius.TopLeft), //LeftTop
                new SKPoint((float)scaledRadius.TopRight, (float)scaledRadius.TopRight), //RightTop
                new SKPoint((float)scaledRadius.BottomLeft, (float)scaledRadius.BottomLeft), //LeftBottom
                new SKPoint((float)scaledRadius.BottomRight, (float)scaledRadius.BottomRight), //RightBottom
            };


            /// <summary>
            /// Paints the stroke with both pixel alignment and width compensation for optimal visual consistency
            /// </summary>
            /// <param name="paint">The paint object to use for stroke rendering</param>
            void PaintStroke(SKPaint paint)
            {
                paint.BlendMode = this.StrokeBlendMode;

                SetupGradient(paint, StrokeGradient, outRect);

                paint.ImageFilter = null;

                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeCap = this.StrokeCap;
                paint.StrokeJoin = MapStrokeCapToStrokeJoin(this.StrokeCap);

                if (this.StrokePath != null && StrokePath.Length > 0)
                {
                    var array = GetDashArray(StrokePath, scale);
                    paint.PathEffect = SKPathEffect.CreateDash(array, 0);
                }
                else
                {
                    paint.PathEffect = null;
                }

                float adjustedStrokeWidth = pixelsStrokeWidth;
                paint.Color = StrokeColor.ToSKColor();
                paint.IsAntialias = true;

                switch (Type)
                {
                    case ShapeType.Rectangle:
                        if (CornerRadius != default)
                        {
                            // Apply width compensation for curves
                            adjustedStrokeWidth = GetCompensatedStrokeWidth(pixelsStrokeWidth, scale);
                            paint.StrokeWidth = adjustedStrokeWidth;

                            // Apply pixel alignment for thin strokes
                            if (pixelsStrokeWidth <= 2.0f * scale)
                            {
                                var pixelAlignedRect = new SKRect(
                                    (float)Math.Round(outRect.Left) + 0.5f,
                                    (float)Math.Round(outRect.Top) + 0.5f,
                                    (float)Math.Round(outRect.Right) - 0.5f,
                                    (float)Math.Round(outRect.Bottom) - 0.5f
                                );

                                if (DrawRoundedRect == null)
                                    DrawRoundedRect = new();
                                DrawRoundedRect.SetRectRadii(pixelAlignedRect, radii);
                                ctx.Context.Canvas.DrawRoundRect(DrawRoundedRect, paint);
                            }
                            else
                            {
                                if (DrawRoundedRect == null)
                                    DrawRoundedRect = new();
                                DrawRoundedRect.SetRectRadii(outRect, radii);
                                ctx.Context.Canvas.DrawRoundRect(DrawRoundedRect, paint);
                            }
                        }
                        else
                        {
                            paint.StrokeWidth = pixelsStrokeWidth;
                            ctx.Context.Canvas.DrawRect(outRect, paint);
                        }

                        break;

                    case ShapeType.Circle:
                        adjustedStrokeWidth = GetCompensatedStrokeWidth(pixelsStrokeWidth, scale);
                        paint.StrokeWidth = adjustedStrokeWidth;
                        ctx.Context.Canvas.DrawCircle(outRect.MidX, outRect.MidY, minSize / 2.0f, paint);
                        break;

                    case ShapeType.Line:
                        paint.StrokeWidth = pixelsStrokeWidth / 2.0f;
                        if (Points != null && Points.Count > 1)
                        {
                            DrawPathShape.Reset();
                            if (SmoothPoints > 0)
                            {
                                AddSmoothPath(DrawPathShape, Points, strokeAwareSize, SmoothPoints, false);
                            }
                            else
                            {
                                AddStraightPath(DrawPathShape, Points, strokeAwareSize, false);
                            }

                            ctx.Context.Canvas.DrawPath(DrawPathShape, paint);
                        }

                        break;

                    case ShapeType.Ellipse:
                        adjustedStrokeWidth = GetCompensatedStrokeWidth(pixelsStrokeWidth, scale);
                        paint.StrokeWidth = adjustedStrokeWidth;
                        DrawPathShape.Reset();
                        DrawPathShape.AddOval(outRect);
                        ctx.Context.Canvas.DrawPath(DrawPathShape, paint);
                        break;

                    case ShapeType.Arc:
                        adjustedStrokeWidth = GetCompensatedStrokeWidth(pixelsStrokeWidth, scale);
                        paint.StrokeWidth = adjustedStrokeWidth;
                        DrawPathShape.Reset();
                        var startAngle = (float)Value1;
                        var sweepAngle = (float)Value2;
                        DrawPathShape.AddArc(outRect, startAngle, sweepAngle);
                        ctx.Context.Canvas.DrawPath(DrawPathShape, paint);
                        break;

                    case ShapeType.Path:
                        adjustedStrokeWidth = GetCompensatedStrokeWidth(pixelsStrokeWidth, scale);
                        paint.StrokeWidth = adjustedStrokeWidth;
                        DrawPathShape.Reset();
                        DrawPathShape.AddPath(DrawPathAligned);
                        ctx.Context.Canvas.DrawPath(DrawPathShape, paint);
                        break;

                    case ShapeType.Polygon:
                        paint.StrokeWidth = pixelsStrokeWidth;
                        if (Points != null && Points.Count > 1)
                        {
                            DrawPathShape.Reset();

                            var path = DrawPathShape;

                            if (SmoothPoints > 0)
                            {
                                adjustedStrokeWidth = GetCompensatedStrokeWidth(pixelsStrokeWidth, scale);
                                paint.StrokeWidth = adjustedStrokeWidth;
                                AddSmoothPath(path, Points, strokeAwareSize, SmoothPoints, true);
                            }
                            else
                            {
                                AddStraightPath(path, Points, strokeAwareSize, true);
                            }

                            ctx.Context.Canvas.DrawPath(path, RenderingPaint);
                        }

                        break;
                }
            }

            void PaintWithShadowsInternal(Action render)
            {
                void RenderShadow(SkiaShadow shadow)
                {
                    SetupShadow(RenderingPaint, shadow, RenderingScale);

                    if (ClipBackgroundColor)
                    {
                        ClipContentPath ??= new();
                        ClipContentPath.Reset();
                        CreateClip(arguments, true, ClipContentPath);

                        var saved = ctx.Context.Canvas.Save();

                        ClipSmart(ctx.Context.Canvas, ClipContentPath, SKClipOperation.Difference);
                        render();

                        ctx.Context.Canvas.RestoreToCount(saved);
                    }
                    else
                    {
                        render();
                    }
                }

                if (PlatformShadow != null)
                {
                    RenderShadow(PlatformShadow);
                }
                else if (Shadows != null && Shadows.Count > 0)
                {
                    for (int index = 0; index < Shadows.Count(); index++)
                    {
                        RenderShadow(Shadows[index]);
                    }
                }
                else
                {
                    render();
                }
            }

            ClipContentPath ??= new();
            ClipContentPath.Reset();

            CreateClip(arguments, true, ClipContentPath);

            //background with shadows pass, no stroke
            PaintWithShadowsInternal(() =>
            {
                if (SetupBackgroundPaint(RenderingPaint, rectBackground))
                {
                    //correct radii for inner rounded rect, must be smaller than stroke!
                    if (WillStroke && StrokeWidth > 1)
                    {
                        var backgroundRadii = GetCorrectedBackgroundRadii(radii, outRect, rectBackground);
                        PaintBackground(ctx.Context, rectBackground, backgroundRadii, minSize, RenderingPaint);
                    }
                    else
                    {
                        PaintBackground(ctx.Context, rectBackground, radii, minSize, RenderingPaint);
                    }
                }
            });

            // Apply bevel or emboss effect if enabled
            if (BevelType != BevelType.None && Bevel != null)
            {
                float pixelsBevelDepth = (float)(Bevel.Depth * scale);
                PaintBevelEffect(ctx.Context, outRect, radii, pixelsBevelDepth);
            }

            //draw children views clipped with shape
            var saved = ctx.Context.Canvas.Save();

            ClipSmart(ctx.Context.Canvas, ClipContentPath);

            var rectForChildren = strokeAwareChildrenSize;

            DrawViews(ctx.WithDestination(rectForChildren));

            ctx.Context.Canvas.RestoreToCount(saved);

            //last pass for stroke over background or children
            if (willStroke)
            {
                PaintStroke(RenderingPaint);
            }
        }

        #endregion

        #region SHADOWS

        private static void ShadowsPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaShape control)
            {
                var enumerableShadows = (IEnumerable<SkiaShadow>)newvalue;

                if (oldvalue != null)
                {
                    if (oldvalue is INotifyCollectionChanged oldCollection)
                    {
                        oldCollection.CollectionChanged -= control.OnShadowCollectionChanged;
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
                    shade.Attach(control);
                }

                if (newvalue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged -= control.OnShadowCollectionChanged;
                    newCollection.CollectionChanged += control.OnShadowCollectionChanged;
                }

                control.Update();
            }
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

        public static readonly BindableProperty ShadowsProperty = BindableProperty.Create(
            nameof(Shadows),
            typeof(IList<SkiaShadow>),
            typeof(SkiaShape),
            defaultValueCreator: (instance) =>
            {
                var created = new SkiaShadowsCollection();
                created.CollectionChanged += ((SkiaShape)instance).OnShadowCollectionChanged;
                return created;
            },
            validateValue: (bo, v) => v is IList<SkiaShadow>,
            propertyChanged: ShadowsPropertyChanged,
            coerceValue: CoerceShadows);

        private static int instanceCount = 0;

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

        #endregion

        #region POINTS

        private static object CoercePoints(BindableObject bindable, object value)
        {
            if (value is ReadOnlyCollection<SkiaPoint> readonlyCollection)
            {
                return new ReadOnlyCollection<SkiaPoint>(readonlyCollection.ToList());
            }

            return value;
        }

        public static readonly BindableProperty PointsProperty = BindableProperty.Create(
            nameof(Points),
            typeof(IList<SkiaPoint>),
            typeof(SkiaShape),
            defaultValueCreator: (instance) =>
            {
                var created = new ObservableCollection<SkiaPoint>();
                created.CollectionChanged += ((SkiaShape)instance).OnPointsCollectionChanged;
                return created;
            },
            validateValue: (bo, v) => v is IList<SkiaPoint>,
            propertyChanged: NeedDraw,
            coerceValue: CoercePoints);

        [TypeConverter(typeof(SkiaPointCollectionConverter))]
        public IList<SkiaPoint> Points
        {
            get => (IList<SkiaPoint>)GetValue(PointsProperty);
            set => SetValue(PointsProperty, value);
        }

        public static List<SkiaPoint> PolygonStar
        {
            get { return CreateStarPoints(5); }
        }

        private static void PointsPropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is SkiaShape control)
            {
                if (oldValue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= control.OnPointsCollectionChanged;
                }

                if (oldValue is IEnumerable<SkiaPoint> oldPoints)
                {
                    foreach (var point in oldPoints)
                    {
                        point.ParentShape = null;
                    }
                }

                if (newValue is IEnumerable<SkiaPoint> newPoints)
                {
                    foreach (var point in newPoints)
                    {
                        point.ParentShape = control;
                    }
                }

                if (newValue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged -= control.OnPointsCollectionChanged;
                    newCollection.CollectionChanged += control.OnPointsCollectionChanged;
                }

                control.Update();
            }
        }

        private void OnPointsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (SkiaPoint oldPoint in e.OldItems)
                {
                    oldPoint.ParentShape = null;
                }
            }

            if (e.NewItems != null)
            {
                foreach (SkiaPoint newPoint in e.NewItems)
                {
                    newPoint.ParentShape = this;
                }
            }

            Update();
        }

        #endregion

        #region SMOOTH

        public static readonly BindableProperty SmoothPointsProperty = BindableProperty.Create(
            nameof(SmoothPoints),
            typeof(float),
            typeof(SkiaShape),
            0f, // Default value is 0 (no smoothing)
            propertyChanged: NeedDraw,
            coerceValue: (bindable, value) =>
            {
                float val = (float)value;
                return Math.Max(0f, Math.Min(1f, val)); // Clamp between 0 and 1
            }
        );

        /// <summary>
        /// Gets or sets the smoothness level for Line and Polygon shapes.
        /// </summary>
        /// <remarks>
        /// This property controls how points are connected when drawing Line and Polygon shapes:
        /// 
        /// - 0.0 (default): No smoothing, points are connected with straight lines
        /// - 0.1 to 0.3: Slight smoothing, good for subtle rounding of corners
        /// - 0.4 to 0.7: Moderate smoothing, creates natural-looking curves
        /// - 0.8 to 1.0: Maximum smoothing, creates very rounded curves
        /// 
        /// When SmoothPoints is greater than 0, quadratic Bezier curves are used to connect
        /// points instead of straight lines. This results in smoother, more organic shapes.
        /// 
        /// The smoothing effect works best with points that form a sequential path and may
        /// not produce expected results with arbitrary point configurations.
        /// </remarks>
        public float SmoothPoints
        {
            get => (float)GetValue(SmoothPointsProperty);
            set => SetValue(SmoothPointsProperty, value);
        }

        private void AddSmoothPath(SKPath path, IList<SkiaPoint> points, SKRect rect, float smoothness, bool isClosed)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }

            var scaledPoints = points.Select(p => ScalePoint(p, rect)).ToList();

            if (isClosed)
            {
                var firstPoint = scaledPoints[0];
                var closingPoint = new SKPoint(
                    (firstPoint.X + scaledPoints[1].X) / 2,
                    (firstPoint.Y + scaledPoints[1].Y) / 2);

                scaledPoints.RemoveAt(0);
                scaledPoints.Insert(0, closingPoint);
                scaledPoints.Add(firstPoint);
            }

            path.MoveTo(scaledPoints[0]);

            int pointCount = scaledPoints.Count;

            if (isClosed)
            {
                for (int i = 0; i < pointCount; i++)
                {
                    SmoothPoint(path, scaledPoints, i, smoothness, isClosed);
                }

                path.Close();
            }
            else
            {
                for (int i = 0; i < pointCount; i++)
                {
                    if (i == 0 || i == pointCount - 1)
                    {
                        path.LineTo(scaledPoints[i]);
                    }
                    else
                    {
                        SmoothPoint(path, scaledPoints, i, smoothness, isClosed);
                    }
                }
            }
        }

        private void SmoothPoint(SKPath path, IList<SKPoint> scaledPoints, int i, float smoothness, bool isClosed)
        {
            int pointCount = scaledPoints.Count;

            var prev = scaledPoints[(i - 1 + pointCount) % pointCount];
            var current = scaledPoints[i];
            var next = scaledPoints[(i + 1) % pointCount];

            if (!isClosed)
            {
                if (i == 0)
                {
                    path.LineTo(current);
                    return;
                }

                if (i == pointCount - 1)
                {
                    path.LineTo(current);
                    return;
                }
            }

            var v1 = new SKPoint(current.X - prev.X, current.Y - prev.Y);
            var v2 = new SKPoint(next.X - current.X, next.Y - current.Y);

            float lengthV1 = (float)Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            float lengthV2 = (float)Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

            if (lengthV1 == 0 || lengthV2 == 0)
            {
                path.LineTo(current);
                return;
            }

            v1 = new SKPoint(v1.X / lengthV1, v1.Y / lengthV1);
            v2 = new SKPoint(v2.X / lengthV2, v2.Y / lengthV2);

            float smoothingRadius = lengthV1 * smoothness * 0.3f;
            smoothingRadius = Math.Min(smoothingRadius, lengthV1 * 0.5f);
            smoothingRadius = Math.Min(smoothingRadius, lengthV2 * 0.5f);

            if (smoothingRadius < 0.001f)
            {
                path.LineTo(current); // No significant smoothing is needed
                return;
            }

            var p1 = new SKPoint(
                current.X - v1.X * smoothingRadius,
                current.Y - v1.Y * smoothingRadius);

            var p2 = new SKPoint(
                current.X + v2.X * smoothingRadius,
                current.Y + v2.Y * smoothingRadius);

            path.LineTo(p1);

            // quadratic Bezier curve to smooth  angle
            path.QuadTo(current, p2);
        }

        private void AddStraightPath(SKPath path, IList<SkiaPoint> points, SKRect rect, bool isClosed)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }

            path.MoveTo(ScalePoint(points[0], rect));

            for (int i = 1; i < points.Count; i++)
            {
                path.LineTo(ScalePoint(points[i], rect));
            }

            if (isClosed)
            {
                path.Close();
            }
        }

        private SKPoint ScalePoint(SkiaPoint point, SKRect rect)
        {
            return new SKPoint(
                (float)Math.Round(rect.Left + point.X * rect.Width),
                (float)Math.Round(rect.Top + point.Y * rect.Height));
        }

        #endregion

        public static List<SkiaPoint> CreateStarPoints(int numberOfPoints, double innerRadiusRatio = 0.5)
        {
            if (numberOfPoints < 2)
                throw new ArgumentException("Number of points must be at least 2.", nameof(numberOfPoints));
            if (innerRadiusRatio <= 0 || innerRadiusRatio >= 1)
                throw new ArgumentException("Inner radius ratio must be between 0 and 1.", nameof(innerRadiusRatio));

            List<SkiaPoint> points = new List<SkiaPoint>();
            double angleStep = Math.PI / numberOfPoints;
            double outerRadius = 1.0;
            double innerRadius = outerRadius * innerRadiusRatio;

            for (int i = 0; i < numberOfPoints * 2; i++)
            {
                double angle = i * angleStep - Math.PI / 2;

                double radius = (i % 2 == 0) ? outerRadius : innerRadius;
                double x = radius * Math.Cos(angle);
                double y = radius * Math.Sin(angle);

                points.Add(new SkiaPoint((float)x, (float)y));
            }

            // Find bounding box
            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);

            var scaleX = 1.0f / (maxX - minX);
            var scaleY = 1.0f / (maxY - minY);

            var scale = Math.Min(scaleX, scaleY);

            var offsetX = (1.0f - (maxX - minX) * scale) / 2.0f - minX * scale;
            var offsetY = (1.0f - (maxY - minY) * scale) / 2.0f - minY * scale;

            for (int i = 0; i < points.Count; i++)
            {
                var x = points[i].X * scale + offsetX;
                var y = points[i].Y * scale + offsetY;

                points[i] = new SkiaPoint(x, y);
            }

            return points;
        }

        public static List<SkiaPoint> CreateStarPointsCrossed(int numberOfPoints)
        {
            if (numberOfPoints < 5 || numberOfPoints % 2 == 0)
                throw new ArgumentException("Number of points must be an odd number greater than or equal to 5.",
                    nameof(numberOfPoints));

            List<SkiaPoint> points = new List<SkiaPoint>();
            double angleStep = 2 * Math.PI / numberOfPoints;
            double radius = 1.0;

            List<SkiaPoint> outerPoints = new List<SkiaPoint>();
            for (int i = 0; i < numberOfPoints; i++)
            {
                double angle = i * angleStep - Math.PI / 2;
                double x = radius * Math.Cos(angle);
                double y = radius * Math.Sin(angle);
                outerPoints.Add(new SkiaPoint((float)x, (float)y));
            }

            int skip = (numberOfPoints - 1) / 2;
            for (int i = 0; i < numberOfPoints; i++)
            {
                points.Add(outerPoints[(i * skip) % numberOfPoints]);
            }

            points.Add(points[0]);

            // Find bounding box
            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);

            var scaleX = 1.0f / (maxX - minX);
            var scaleY = 1.0f / (maxY - minY);

            var scale = Math.Min(scaleX, scaleY);

            var offsetX = (1.0f - (maxX - minX) * scale) / 2.0f - minX * scale;
            var offsetY = (1.0f - (maxY - minY) * scale) / 2.0f - minY * scale;

            for (int i = 0; i < points.Count; i++)
            {
                var x = points[i].X * scale + offsetX;
                var y = points[i].Y * scale + offsetY;

                points[i] = new SkiaPoint(x, y);
            }

            return points;
        }

        /// <summary>
        /// Calculates compensated stroke width for curved shapes to fix antialiasing at rounded corners
        /// </summary>
        /// <param name="originalStrokeWidth">The original stroke width in pixels</param>
        /// <param name="scale">The current rendering scale</param>
        /// <returns>Adjusted stroke width that compensates for antialiasing differences</returns>
        protected float GetCompensatedStrokeWidth(float originalStrokeWidth, float scale)
        {
            if (originalStrokeWidth <= 0)
                return originalStrokeWidth;

            if (originalStrokeWidth <= 1.0f * scale)
            {
                return originalStrokeWidth * 0.55f; // Make curves thinner
            }

            return originalStrokeWidth;
        }

    }
}
