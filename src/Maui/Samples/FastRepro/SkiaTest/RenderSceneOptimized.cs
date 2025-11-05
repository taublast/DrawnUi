namespace Sandbox
{
    public class RenderSceneOptimized : SkiaControl
    {
        private readonly MotionMarkSceneOptimized _scene;

        public RenderSceneOptimized()
        {
            _scene = new ();
            _scene.SetComplexity(9);
            HorizontalOptions = LayoutOptions.Fill;
            VerticalOptions = LayoutOptions.Fill;
        }

        protected override void Paint(DrawingContext ctx)
        {
            //base.Paint(ctx);

            _scene.Render(ctx.Context.Canvas, this.DrawingRect.Width, DrawingRect.Height);

            Repaint();
        }
    }
}
