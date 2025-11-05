namespace Sandbox
{
    public class RenderSceneNative : SkiaControl
    {
        private readonly MotionMarkNativeScene _scene;

        public RenderSceneNative()
        {
            _scene = new ();
            _scene.SetComplexity(9);
            HorizontalOptions = LayoutOptions.Fill;
            VerticalOptions = LayoutOptions.Fill;
        }

        protected override void Paint(DrawingContext ctx)
        {
            //base.Paint(ctx);

            IntPtr handle = ctx.Context.Canvas.Handle;
            _scene.Render(handle, this.DrawingRect.Width, DrawingRect.Height);

            Repaint();
        }
    }
}
