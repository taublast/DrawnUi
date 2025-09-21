using DrawnUi.Draw;
using GameTemplate.Game;
using SkiaSharp;

namespace GameTemplate.Sprites;

/// <summary>
/// A simple rectangle sprite that can be pooled and reused.
/// </summary>
public class PooledSprite : SkiaShape, IReusableSprite, IWithHitBox
{
    public Guid Uid { get; } = Guid.NewGuid();

    public bool IsActive { get; set; }

    public float VX { get; set; }
    public float VY { get; set; }

    public SKRect HitBox { get; private set; }

    public PooledSprite()
    {
        WidthRequest = 14;
        HeightRequest = 14;
        CornerRadius = 3;
        BackgroundColor = Colors.Orange;
        UseCache = SkiaCacheType.Operations;
        ZIndex = 5;
        Opacity = 0.0; // hidden until activated
    }

    public static PooledSprite Create()
    {
        return new PooledSprite();
    }

    public void UpdatePosition(float dt)
    {
        TranslationX += VX * dt;
        TranslationY += VY * dt;
    }

    public void UpdateState(long time)
    {
        // Compute hitbox in canvas coordinates
        var pos = GetPositionOnCanvasInPoints();
        var w = (float)Width;
        var h = (float)Height;
        HitBox = new SKRect(pos.X, pos.Y, pos.X + w, pos.Y + h);
    }

    public void ResetAnimationState()
    {
        try
        {
            this.CancelDisappearing?.Cancel();
        }
        catch
        {
        }

        Opacity = 1;
        Scale = 1;
    }

    /// <summary>
    /// Cancellation token for sprite removal animations.
    /// </summary>
    public CancellationTokenSource CancelDisappearing { get; set; }

    public async Task AnimateDisappearing()
    {
        this.CancelDisappearing?.Cancel();
        using var cancel = new CancellationTokenSource();
        CancelDisappearing = cancel;
        await FadeToAsync(0, 200, Easing.SpringOut, cancel);
    }
}

