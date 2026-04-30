// NOTE: Parts of the code below are based on
// https://www.mooict.com/wpf-c-tutorial-create-a-space-battle-shooter-game-in-visual-studio/7/

using AppoMobi.Specials;
using SkiaSharp;

namespace SpaceShooter.Game;

public partial class SpaceShooter
{
    public class EnemySprite : SkiaImage, IWithHitBox, IReusableSprite
    {
        public static float Speed = 50f;

        public static EnemySprite Create()
        {
            var enemySpriteCounter = RndExtensions.CreateRandom(1, 5);

            var newEnemy = new EnemySprite()
            {
                LoadSourceOnFirstDraw = true, //do not load source when it changed but only when first drawing
                Source = $"{SpritesPath}/{enemySpriteCounter}.png", //random image
                SpeedRatio = 0.9f + enemySpriteCounter * 2 / 10f, //random speed
                ColorTint = Color.Parse("#22110022"), //tinted a bit for our game
                ZIndex = 4,
                UseCache = SkiaCacheType.Image,
                WidthRequest = 50,
                HeightRequest = 44,
                AddEffect = SkiaImageEffect.Tint,
                EffectBlendMode = SKBlendMode.SrcATop,
                RescalingQuality = SKFilterQuality.High
            };

            newEnemy.ResetAnimationState();

            return newEnemy;
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

        public bool IsActive { get; set; }

        public float SpeedRatio { get; set; }

        public void ResetAnimationState()
        {
            Opacity = 1;
            Scale = 1;
        }

        public void UpdatePosition(float deltaTime)
        {
            TranslationY += SpeedRatio * Speed * deltaTime; // move the enemy downwards
        }

        public async Task AnimateDisappearing()
        {
            await FadeToAsync(0, 150);
        }
    }
}