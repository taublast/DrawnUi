using AppoMobi.Maui.Gestures;

namespace Sandbox;

public partial class TutorialCards : ContentPage
{
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
        if (sender is SkiaControl control)
        {
            if (e.Args.Type == TouchActionResult.Tapped)
            {
                e.Consumed = true; //could consume

                Task.Run(async () =>
                {
                    // Color pulse effect
                    if (control is SkiaShape shape && shape.FillGradient is SkiaGradient gradient)
                    {
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
                });

                Task.Run(async () =>
                {
                    // Smooth scale animation with bounce effect
                    control.ScaleToAsync(1.1, 1.1, 150, Easing.CubicOut);
                    await Task.Delay(100);
                    control.ScaleToAsync(1.0, 1.0, 200, Easing.BounceOut);

                    // Rotate animation for fun
                    control.RotateToAsync(control.Rotation + 5, 200, Easing.SpringOut);
                    await Task.Delay(150);
                    control.RotateToAsync(0, 300, Easing.SpringOut);
                });

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
    }
}
