using DrawnUi;

namespace DrawnUi.Draw
{
    public partial class SkiaShape : SkiaLayout
    {
        public SKRect MeasuredStrokeAwareChildrenSize { get; protected set; }

        public SKRect MeasuredStrokeAwareClipSize { get; protected set; }

        public SKRect MeasuredStrokeAwareSize { get; protected set; }

        public static readonly BindableProperty PathDataProperty = BindableProperty.Create(
            nameof(PathData),
            typeof(string),
            typeof(SkiaShape),
            null,
            propertyChanged: NeedSetType);

        public static new readonly BindableProperty TypeProperty = BindableProperty.Create(
            nameof(Type),
            typeof(ShapeType),
            typeof(SkiaShape),
            ShapeType.Rectangle,
            propertyChanged: NeedSetType);

        public static readonly BindableProperty StrokeWidthProperty = BindableProperty.Create(
            nameof(StrokeWidth),
            typeof(double),
            typeof(SkiaShape),
            0.0,
            propertyChanged: NeedInvalidateMeasure);

        public static readonly BindableProperty StrokeColorProperty = BindableProperty.Create(
            nameof(StrokeColor),
            typeof(Color),
            typeof(SkiaShape),
            Colors.Gray,
            propertyChanged: NeedDraw);

        public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(SkiaShape),
            default(CornerRadius),
            propertyChanged: NeedInvalidateMeasure);

        public string PathData
        {
            get => (string)GetValue(PathDataProperty);
            set => SetValue(PathDataProperty, value);
        }

        public new ShapeType Type
        {
            get => (ShapeType)GetValue(TypeProperty);
            set => SetValue(TypeProperty, value);
        }

        public double StrokeWidth
        {
            get => (double)GetValue(StrokeWidthProperty);
            set => SetValue(StrokeWidthProperty, value);
        }

        public Color StrokeColor
        {
            get => (Color)GetValue(StrokeColorProperty);
            set => SetValue(StrokeColorProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public SKPath DrawPathResized { get; } = new();

        public SKPath DrawPathAligned { get; } = new();

        protected SKPath DrawPath { get; set; } = new();

        protected SKRoundRect DrawRoundedRect { get; set; }

        private static void NeedSetType(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is SkiaShape control)
            {
                control.SetupType();
            }
        }

        public struct ShapePaintArguments
        {
            public SKRect StrokeAwareSize { get; set; }
            public SKRect StrokeAwareChildrenSize { get; set; }
            public SKRect StrokeAwareClipSize { get; set; }
        }

        public virtual void SetupType()
        {
            if (Type == ShapeType.Path)
            {
                var previousPath = DrawPath;

                if (!string.IsNullOrEmpty(PathData))
                {
                    try
                    {
                        DrawPath = SKPath.ParseSvgPathData(PathData);
                    }
                    catch
                    {
                        DrawPath = null;
                    }
                }
                else
                {
                    DrawPath = null;
                }

                if (previousPath != null && !ReferenceEquals(previousPath, DrawPath))
                {
                    previousPath.Dispose();
                }
            }

            Update();
        }

        public virtual bool WillStroke => StrokeColor != TransparentColor && StrokeWidth != 0;

        protected float GetHalfStroke(float scale)
        {
            var pixelsStrokeWidth = StrokeWidth > 0
                ? (float)(StrokeWidth * scale)
                : (float)(-StrokeWidth);

            return pixelsStrokeWidth / 2.0f;
        }

        protected float GetSmallUnderStroke(float scale)
        {
            var pixelsStrokeWidth = StrokeWidth > 0
                ? (float)(StrokeWidth * scale)
                : (float)(-StrokeWidth);

            return pixelsStrokeWidth / 3.0f;
        }

        protected float GetStrokePixels(float scale)
        {
            var pixelsStrokeWidth = StrokeWidth > 0
                ? (float)(StrokeWidth * scale)
                : (float)(-StrokeWidth);

            return pixelsStrokeWidth;
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

            return ContractPixelsRect(destination, scale, UsePadding);
        }

        protected SKRect CalculateClipSizeForStroke(SKRect destination, float scale)
        {
            if (WillStroke)
            {
                var strokeAwareSize = CalculateShapeSizeForStroke(destination, scale);
                return ContractPixelsRect(strokeAwareSize, GetSmallUnderStroke(scale));
            }

            return destination;
        }

        protected SKRect CalculateShapeSizeForStroke(SKRect destination, float scale)
        {
            if (!WillStroke)
            {
                return destination;
            }

            var strokeAwareSize = new SKRect(destination.Left, destination.Top, destination.Right, destination.Bottom);
            var inflate = GetInflationForStroke(GetHalfStroke(scale));
            strokeAwareSize = SKRect.Inflate(strokeAwareSize, inflate, inflate);

            return new SKRect(
                (float)Math.Ceiling(strokeAwareSize.Left),
                (float)Math.Ceiling(strokeAwareSize.Top),
                (float)Math.Floor(strokeAwareSize.Right),
                (float)Math.Floor(strokeAwareSize.Bottom));
        }

        protected void CalculateSizeForStroke(SKRect destination, float scale)
        {
            MeasuredStrokeAwareSize = CalculateShapeSizeForStroke(destination, scale);
            MeasuredStrokeAwareClipSize = CalculateClipSizeForStroke(destination, scale);
            MeasuredStrokeAwareChildrenSize = CalculateContentSizeForStroke(destination, scale);

            if (Type != ShapeType.Path || DrawPath == null)
            {
                return;
            }

            DrawPath.GetTightBounds(out var bounds);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                DrawPathResized.Reset();
                return;
            }

            using var stretched = new SKPath();
            stretched.AddPath(DrawPath);

            var halfStroke = GetHalfStroke(scale);
            var scaleX = MeasuredStrokeAwareSize.Width / (bounds.Width + halfStroke);
            var scaleY = MeasuredStrokeAwareSize.Height / (bounds.Height + halfStroke);
            var translateX = (MeasuredStrokeAwareSize.Width - (bounds.Width + halfStroke) * scaleX) / 2 - bounds.Left * scaleX;
            var translateY = (MeasuredStrokeAwareSize.Height - (bounds.Height + halfStroke) * scaleY) / 2 - bounds.Top * scaleY;

            var matrix = SKMatrix.CreateScale(scaleX, scaleY);
            matrix = SKMatrix.Concat(matrix, SKMatrix.CreateTranslation(translateX / 2, translateY / 2));

            stretched.Transform(matrix);
            stretched.Offset(halfStroke, halfStroke);

            DrawPathResized.Reset();
            DrawPathResized.AddPath(stretched);
        }

        public override void Arrange(SKRect destination, float widthRequest, float heightRequest, float scale)
        {
            base.Arrange(destination, widthRequest, heightRequest, scale);
            CalculateSizeForStroke(DrawingRect, scale);
        }

        protected void DisposeSharedShapeResources()
        {
            DrawPath?.Dispose();
            DrawRoundedRect?.Dispose();
            DrawPathResized?.Dispose();
            DrawPathAligned?.Dispose();
        }

        protected SKPoint[] GetCorrectedBackgroundRadii(SKPoint[] originalRadii, SKRect strokeRect, SKRect backgroundRect)
        {
            var widthContraction = strokeRect.Width - backgroundRect.Width;
            var heightContraction = strokeRect.Height - backgroundRect.Height;
            var horizontalReduction = widthContraction / 2.0f;
            var verticalReduction = heightContraction / 2.0f;
            var radiusReduction = Math.Min(horizontalReduction, verticalReduction);
            var correctedRadii = new SKPoint[4];

            for (var index = 0; index < originalRadii.Length; index++)
            {
                correctedRadii[index] = new SKPoint(
                    Math.Max(0, originalRadii[index].X - radiusReduction),
                    Math.Max(0, originalRadii[index].Y - radiusReduction));
            }

            return correctedRadii;
        }

        protected virtual SKPoint[] CreateScaledRadii(float scale)
        {
            return new[]
            {
                new SKPoint((float)(CornerRadius.TopLeft * scale), (float)(CornerRadius.TopLeft * scale)),
                new SKPoint((float)(CornerRadius.TopRight * scale), (float)(CornerRadius.TopRight * scale)),
                new SKPoint((float)(CornerRadius.BottomLeft * scale), (float)(CornerRadius.BottomLeft * scale)),
                new SKPoint((float)(CornerRadius.BottomRight * scale), (float)(CornerRadius.BottomRight * scale))
            };
        }

        protected virtual void AlignResizedPath(SKRect outRect)
        {
            if (Type != ShapeType.Path)
            {
                return;
            }

            DrawPathAligned.Reset();
            DrawPathAligned.AddPath(DrawPathResized);
            DrawPathAligned.Offset(outRect.Left, outRect.Top);
        }

        protected virtual bool TryCreateBasicClip(object arguments, bool usePosition, SKPath path)
        {
            var strokeAwareSize = MeasuredStrokeAwareSize;
            var strokeAwareChildrenSize = MeasuredStrokeAwareClipSize;

            if (arguments is ShapePaintArguments args)
            {
                strokeAwareSize = args.StrokeAwareSize;
                strokeAwareChildrenSize = args.StrokeAwareClipSize;
            }

            if (!usePosition)
            {
                var offsetToZero = new SKPoint(
                    strokeAwareChildrenSize.Left - strokeAwareSize.Left,
                    strokeAwareChildrenSize.Top - strokeAwareSize.Top);
                strokeAwareChildrenSize = new SKRect(
                    offsetToZero.X,
                    offsetToZero.Y,
                    strokeAwareChildrenSize.Width + offsetToZero.X,
                    strokeAwareChildrenSize.Height + offsetToZero.Y);
            }

            switch (Type)
            {
                case ShapeType.Path:
                    ShouldClipAntialiased = true;
                    path.AddPath(DrawPathResized);
                    return true;
                case ShapeType.Circle:
                    ShouldClipAntialiased = true;
                    path.AddCircle(
                        strokeAwareChildrenSize.Left + strokeAwareChildrenSize.Width / 2.0f,
                        strokeAwareChildrenSize.Top + strokeAwareChildrenSize.Height / 2.0f,
                        (float)Math.Floor(Math.Min(strokeAwareChildrenSize.Width, strokeAwareChildrenSize.Height) / 2.0f));
                    return true;
                case ShapeType.Ellipse:
                    ShouldClipAntialiased = true;
                    path.AddOval(strokeAwareChildrenSize);
                    return true;
                case ShapeType.Rectangle:
                    if (CornerRadius != default)
                    {
                        ShouldClipAntialiased = true;
                        using var roundedRect = new SKRoundRect(strokeAwareChildrenSize);
                        roundedRect.SetRectRadii(strokeAwareChildrenSize, CreateScaledRadii(RenderingScale));
                        path.AddRoundRect(roundedRect);
                    }
                    else
                    {
                        ShouldClipAntialiased = false;
                        path.AddRect(strokeAwareChildrenSize);
                    }

                    return true;
                default:
                    return false;
            }
        }

        protected virtual bool TryPaintBasicShape(SKCanvas canvas, SKRect rect, SKPoint[] radii, float minSize, SKPaint paint)
        {
            switch (Type)
            {
                case ShapeType.Circle:
                    canvas.DrawCircle(rect.MidX, rect.MidY, minSize / 2.0f, paint);
                    return true;
                case ShapeType.Ellipse:
                    using (var ellipsePath = new SKPath())
                    {
                        ellipsePath.AddOval(rect);
                        canvas.DrawPath(ellipsePath, paint);
                    }

                    return true;
                case ShapeType.Path:
                    if (DrawPathAligned != null && !DrawPathAligned.IsEmpty)
                    {
                        canvas.DrawPath(DrawPathAligned, paint);
                    }

                    return true;
                case ShapeType.Rectangle:
                    if (CornerRadius != default)
                    {
                        DrawRoundedRect ??= new SKRoundRect();
                        DrawRoundedRect.SetRectRadii(rect, radii);
                        canvas.DrawRoundRect(DrawRoundedRect, paint);
                    }
                    else
                    {
                        canvas.DrawRect(rect, paint);
                    }

                    return true;
                default:
                    return false;
            }
        }
    }
}
