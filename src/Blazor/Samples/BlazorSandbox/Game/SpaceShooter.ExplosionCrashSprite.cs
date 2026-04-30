namespace SpaceShooter.Game;

public partial class SpaceShooter
{
    public class ExplosionCrashSprite : SkiaLottie, IReusableSprite
    {
        public bool IsActive { get; set; }

        public static ExplosionCrashSprite Create()
        {
            var explosion = new ExplosionCrashSprite()
            {
                AutoPlay = false,
                VerticalOptions = LayoutOptions.End,
                ZIndex = 6,
                WidthRequest = 110,
                LockRatio = 1,
                SpeedRatio = 0.6f,
                Repeat = 0,
                UseCache = SkiaCacheType.ImageDoubleBuffered,
                Source = $"Space/Lottie/crash.json"
            };
            explosion.ResetAnimationState();
            return explosion;
        }

        public void ResetAnimationState()
        {
            Seek(0);
            Opacity = 0.5;
            Scale = 1;
        }

        public async Task AnimateDisappearing()
        {
            await FadeToAsync(0, 100);
        }
    }
}