using System.Collections;

namespace DrawnUi.Draw
{
    public partial class SkiaLayout : SkiaControl, ISkiaLayout
    {
        public SkiaLayout()
        {
            ChildrenFactory = new(this);
        }

        public ViewsAdapter ChildrenFactory { get; protected set; }

        public MeasuringStrategy MeasureItemsStrategy { get; set; } = MeasuringStrategy.MeasureAll;

        public int FirstMeasuredIndex { get; protected set; }

        public int LastMeasuredIndex { get; protected set; } = -1;

        public IList? ItemsSource { get; set; }

        public bool IsStack => Type == LayoutType.Column || Type == LayoutType.Row || Type == LayoutType.Wrap;

        public ScaledSize GetEstimatedContentSize(float scale)
        {
            return MeasuredSize;
        }

        public double GetMeasuredContentEnd()
        {
            return Type == LayoutType.Row ? MeasuredSize.Pixels.Width : MeasuredSize.Pixels.Height;
        }

        public int MeasureAdditionalItems(int batchSize, int aheadCount, float scale)
        {
            return 0;
        }

        public virtual ContainsPointResult GetVisibleChildIndexAt(SKPoint point)
        {
            return default;
        }

        public virtual ContainsPointResult GetChildIndexAt(SKPoint point)
        {
            return default;
        }

        public virtual void OnViewportWasChanged(ScaledRect viewport)
        {
        }

        public virtual void OnLoaded()
        {
        }

        public virtual void OnAppearing()
        {
        }

        public virtual void OnAppeared()
        {
        }

        public virtual void OnDisappeared()
        {
        }

        public virtual void OnDisappearing()
        {
        }

        public static readonly BindableProperty TypeProperty = BindableProperty.Create(
            nameof(Type),
            typeof(LayoutType),
            typeof(SkiaLayout),
            LayoutType.Absolute,
            propertyChanged: NeedInvalidateMeasure);

        public LayoutType Type
        {
            get => (LayoutType)GetValue(TypeProperty);
            set => SetValue(TypeProperty, value);
        }

        public override void ApplyMeasureResult()
        {
            ApplyStackMeasureResult();

            base.ApplyMeasureResult();
        }

        public override void DrawDirectInternal(DrawingContext context, SKRect drawingRect)
        {
            var destination = context.Destination;

            var clone = AddPaintArguments(context).WithDestination(drawingRect);
            DrawWithClipAndTransforms(clone, drawingRect, true, true, ctx =>
            {
                PaintWithEffects(ctx);
                DrawLayoutChildren(ctx, drawingRect);

                foreach (var postRenderer in EffectPostRenderers)
                {
                    postRenderer.Render(ctx.WithDestination(destination));
                }
            });
        }

        private void DrawLayoutChildren(DrawingContext context, SKRect drawingRect)
        {
            var children = GetOrderedSubviews();
            if (children.Count == 0)
            {
                SetRenderingTree(new List<SkiaControlWithRect>());
                return;
            }

            var renderTree = new List<SkiaControlWithRect>(children.Count);

            var scale = context.Scale;
            var contentRect = new SKRect(
                drawingRect.Left + (float)(UsePadding.Left * scale),
                drawingRect.Top + (float)(UsePadding.Top * scale),
                drawingRect.Right - (float)(UsePadding.Right * scale),
                drawingRect.Bottom - (float)(UsePadding.Bottom * scale));

            if (contentRect.Width <= 0 || contentRect.Height <= 0)
            {
                SetRenderingTree(renderTree);
                return;
            }

            switch (Type)
            {
                case LayoutType.Column:
                    DrawColumn(context, children, contentRect, scale, renderTree);
                    break;
                case LayoutType.Row:
                    DrawRow(context, children, contentRect, scale, renderTree);
                    break;
                case LayoutType.Wrap:
                    DrawWrap(context, contentRect, renderTree);
                    break;
                default:
                    DrawAbsolute(context, children, contentRect, renderTree);
                    break;
            }

            SetRenderingTree(renderTree);
        }

        private void DrawWrap(DrawingContext context, SKRect contentRect, List<SkiaControlWithRect> renderTree)
        {
            var structure = LatestStackStructure;
            if (structure == null)
                return;

            var index = 0;

            foreach (var cell in structure.GetChildren())
            {
                var child = cell.View;
                if (child == null)
                    continue;

                var renderRect = new SKRect(
                    cell.Destination.Left + contentRect.Left,
                    cell.Destination.Top + contentRect.Top,
                    cell.Destination.Right + contentRect.Left,
                    cell.Destination.Bottom + contentRect.Top);

                DrawAndTrackChild(context, child, renderTree, index++, renderRect);
            }
        }

        private void DrawColumn(DrawingContext context, List<SkiaControl> children, SKRect contentRect, float scale, List<SkiaControlWithRect> renderTree)
        {
            DrawLinearStack(context, children, contentRect, scale, isRow: false, renderTree);
        }

        private void DrawRow(DrawingContext context, List<SkiaControl> children, SKRect contentRect, float scale, List<SkiaControlWithRect> renderTree)
        {
            DrawLinearStack(context, children, contentRect, scale, isRow: true, renderTree);
        }

        private void DrawLinearStack(DrawingContext context, List<SkiaControl> children, SKRect contentRect, float scale, bool isRow, List<SkiaControlWithRect> renderTree)
        {
            var structure = LatestStackStructure;
            if (structure == null)
                return;

            var index = 0;

            foreach (var cell in structure.GetChildren())
            {
                var child = cell.View;
                if (child == null)
                    continue;

                var renderRect = new SKRect(
                    cell.Destination.Left + contentRect.Left,
                    cell.Destination.Top + contentRect.Top,
                    cell.Destination.Right + contentRect.Left,
                    cell.Destination.Bottom + contentRect.Top);

                DrawAndTrackChild(context, child, renderTree, index++, renderRect);
            }
        }

        private void DrawAbsolute(DrawingContext context, List<SkiaControl> children, SKRect contentRect, List<SkiaControlWithRect> renderTree)
        {
            var index = 0;

            foreach (var child in children)
            {
                child.OptionalOnBeforeDrawing();
                if (!child.CanDraw)
                    continue;

                var measured = child.NeedMeasure ? MeasureChild(child, contentRect.Width, contentRect.Height, context.Scale) : child.MeasuredSize;
                child.Arrange(contentRect, measured.Units.Width, measured.Units.Height, context.Scale);
                var renderRect = child.Destination.Width > 0 && child.Destination.Height > 0
                    ? child.Destination
                    : contentRect;
                DrawAndTrackChild(context, child, renderTree, index++, renderRect);
            }
        }

        protected override ScaledSize MeasureInternal(MeasureRequest request)
        {
            if (!CanDraw || request.WidthRequest == 0 || request.HeightRequest == 0)
            {
                InvalidateCacheWithPrevious();
                return SetMeasuredAsEmpty(request.Scale);
            }

            var constraints = GetMeasuringConstraints(request);

            ContentSize = Type switch
            {
                LayoutType.Column or LayoutType.Row => MeasureStackBase(constraints.Content, request.Scale),
                LayoutType.Wrap => MeasureWrapBase(constraints.Content, request.Scale),
                _ => MeasureAbsolute(constraints.Content, request.Scale)
            };

            return SetMeasuredAdaptToContentSize(constraints, request.Scale);
        }
    }
}