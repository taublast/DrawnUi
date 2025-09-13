using DrawnUi.Views;

namespace Breakout.Game
{
    public partial class MainPage
    {
#if PREVIEWS
        [AutoGeneratePreview(false)]
#endif
        public class RescalingCanvas : Canvas
        {
            public float GameScale { get; set; } = 1;

            protected override void Draw(DrawingContext context)
            {
                var wantedHeight = Breakout.Game.BreakoutGame.HEIGHT * context.Scale;
                var wantedWidth = Breakout.Game.BreakoutGame.WIDTH * context.Scale;

                var scaleWidth = this.DrawingRect.Width / wantedWidth;
                var scaleHeight = this.DrawingRect.Height / wantedHeight;

                GameScale = Math.Min(scaleWidth, scaleHeight);

                context.Scale *= GameScale;

                base.Draw(context);
            }
        }
    }
}
