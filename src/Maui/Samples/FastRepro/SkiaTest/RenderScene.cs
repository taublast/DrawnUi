namespace Sandbox
{
    public class RenderScene : SkiaControl
    {
        private readonly MotionMarkScene _scene;

        public RenderScene()
        {
            _scene = new MotionMarkScene();
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
