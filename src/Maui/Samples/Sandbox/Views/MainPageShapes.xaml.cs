using DrawnUi.Views;
using Sandbox.Views;

namespace MauiNet8;

public partial class MainPageShapes : BasePageCodeBehind
{
    int count = 0;

    public MainPageShapes()
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

}

public class DebugCanvas : Canvas
{


    public DebugCanvas()
    {
        RenderingMode = RenderingModeType.Accelerated;
    }

    protected override void Draw(DrawingContext context)
    {
        base.Draw(context);

        /*
        if (Height > 0 && Width > 0)
        {
            var canvas = context.Canvas;
            var res = CreateImage((int)context.Width, (int)context.Height, (float)(context.Width / Width));
            var image = res.Item2;

            //drawn image with rotation to illustrate missing antialiasing
            using var paint = new SKPaint() //max quality
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High,
                IsDither = true
            };

            var count = canvas.Save();

            canvas.RotateDegrees((float)-10, (float)context.Width / 2, (float)context.Height / 2);

            canvas.DrawImage(image, 50, 50, paint); //DrawBitmap gives same edgy not-antialiased effect

            canvas.RestoreToCount(count);

            res.Item1.Dispose();
            res.Item2.Dispose();



        }
        */
    }


    (SKSurface, SKImage) CreateImage(int width, int height, float renderingScale)
    {
        var surfaceInfo = new SKImageInfo(width / 3, height / 3);
        var surface = SKSurface.Create(surfaceInfo);
        surface.Canvas.Clear(SKColors.White);

        // Calculate the margin
        float margin = 12 * renderingScale;

        // Define the rectangle inside the white rectangle
        var redRect = new SKRect(
            margin,
            margin,
            surfaceInfo.Width - margin,
            surfaceInfo.Height - margin
        );

        using (var paint = new SKPaint())
        {
            paint.Color = SKColors.Red;
            surface.Canvas.DrawRect(redRect, paint);
        }

        surface.Flush();
        var image = surface.Snapshot();
        return (surface, image);
    }



}
