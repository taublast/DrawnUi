// NOTE: Parts of the code below are based on
// https://www.mooict.com/wpf-c-tutorial-create-a-space-battle-shooter-game-in-visual-studio/7/



namespace SpaceShooter.Game;

public partial class SpaceShooter
{
    public class BulletSprite : SkiaShape, IWithHitBox, IReusableSprite
    {
        public static float Speed = 500f;

        public static BulletSprite Create()
        {
            var newBullet = new BulletSprite()
            {
                HeightRequest = 16,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                CornerRadius = 6,
                WidthRequest = 5,
                StrokeWidth = 1,
                StrokeCap = SKStrokeCap.Round,
                BackgroundColor = Color.Parse("#f0ff3333"),
                StrokeColor = Color.Parse("#eeff0000"),
                UseCache = SkiaCacheType.Operations,
                SpeedRatio = 1
            };
            return newBullet;
        }

        public bool IsActive { get; set; }

        public void ResetAnimationState()
        {

        }

        public async Task AnimateDisappearing()
        {

        }

        public void UpdateState(long time)
        {
            if (_stateUpdated != time)
            {
                var position = GetPositionOnCanvasInPoints();
                var hitBox = new SKRect(position.X, position.Y,
                    (float)(position.X + Width), (float)(position.Y + Height));
                HitBox = hitBox;
                _stateUpdated = time;
            }
        }
        long _stateUpdated;

        public SKRect HitBox { get; set; }

        public float SpeedRatio { get; set; }

        public void UpdatePosition(float deltaTime)
        {
            TranslationY -= SpeedRatio * Speed * deltaTime;
        }
    }
}
