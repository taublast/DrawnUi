using AppoMobi.Gestures;

namespace DrawnUI.Tutorials.InteractiveCards;

public partial class TutorialCards : ContentPage
{
    private readonly HashSet<SkiaControl> _activeTapAnimations = new();

    public TutorialCards()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception e)
        {
            Super.DisplayException(this, e);
        }
    }

    private void OnCardGestures(object sender, SkiaGesturesInfo e)
    {
        if (sender is not SkiaControl control)
        {
            return;
        }

        if (e.Args.Type == TouchActionResult.Tapped)
        {
            e.Consumed = true;
            _ = AnimateTapAsync(control);
        }

        if (sender == Pannable)
        {
            // Smooth drag following with momentum
            if (e.Args.Type == TouchActionResult.Panning)
            {
                e.Consumed = true;

                control.TranslationX += e.Args.Event.Distance.Delta.X / control.RenderingScale;
                control.TranslationY += e.Args.Event.Distance.Delta.Y / control.RenderingScale;

                // Add subtle rotation based on pan direction
                var deltaX = e.Args.Event.Distance.Total.X / control.RenderingScale;
                var rotationAmount = deltaX * 0.1;
                control.Rotation = Math.Max(-15, Math.Min(15, rotationAmount));
            }
            else if (e.Args.Type == TouchActionResult.Up)
            {
                // Snap back to original position
                control.TranslateToAsync(0, 0, 100, Easing.SpringOut);
                control.RotateToAsync(0, 75, Easing.SpringOut);
            }
        }
    }

    private async Task AnimateTapAsync(SkiaControl control)
    {
        if (!_activeTapAnimations.Add(control))
        {
            return;
        }

        try
        {
            var gradientTask = AnimateGradientAsync(control);

            await control.ScaleToAsync(1.1, 1.1, 150, Easing.CubicOut);
            await control.ScaleToAsync(1.0, 1.0, 200, Easing.BounceOut);
            await control.RotateToAsync(control.Rotation + 2, 200, Easing.SpringOut);
            await control.RotateToAsync(0, 300, Easing.SpringOut);

            await gradientTask;
        }
        finally
        {
            _activeTapAnimations.Remove(control);
        }
    }

    private static async Task AnimateGradientAsync(SkiaControl control)
    {
        // Color pulse effect
        if (control is not SkiaShape shape || shape.FillGradient is not SkiaGradient gradient)
        {
            return;
        }

        var originalStart = gradient.Colors[0];
        var originalEnd = gradient.Colors[1];
        var lighter = 1.5;

        // Brighten colors
        var gradientStartColor = Color.FromRgba(
            Math.Min(1, originalStart.Red * lighter),
            Math.Min(1, originalStart.Green * lighter),
            Math.Min(1, originalStart.Blue * lighter),
            originalStart.Alpha);

        var gradientEndColor = Color.FromRgba(
            Math.Min(1, originalEnd.Red * lighter),
            Math.Min(1, originalEnd.Green * lighter),
            Math.Min(1, originalEnd.Blue * lighter),
            originalEnd.Alpha);

        gradient.Colors = new List<Color>() { gradientStartColor, gradientEndColor };

        // Restore original colors
        await Task.Delay(200);
        gradient.Colors = new List<Color>() { originalStart, originalEnd };
    }
}
