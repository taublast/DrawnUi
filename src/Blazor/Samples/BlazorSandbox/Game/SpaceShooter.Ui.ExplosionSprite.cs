namespace SpaceShooter.Game;

public partial class SpaceShooter
{
    public class ExplosionSprite : SkiaLottie, IReusableSprite
    {
        public bool IsActive { get; set; }

        public static ExplosionSprite Create()
        {
            var explosion = new ExplosionSprite()
            {
                AutoPlay = false,
                ZIndex = 6,
                SpeedRatio = 1.5,
                WidthRequest = 150,
                LockRatio = 1,
                UseCache = SkiaCacheType.ImageDoubleBuffered,
                Source = $"Space/Lottie/explosion.json"
            };
            explosion.ResetAnimationState();
            return explosion;
        }

        public void ResetAnimationState()
        {
            Seek(0);
            Opacity = 0.75;
            Scale = 1;
        }

        public async Task AnimateDisappearing()
        {
            await FadeToAsync(0, 150);
        }
    }
}