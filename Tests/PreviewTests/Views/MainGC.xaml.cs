using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace PreviewTests.Views
{

    public partial class MainGC
    {


        public MainGC()
        {


            try
            {
                InitializeComponent();

                //avoid setting context BEFORE InitializeComponent, can bug 
                //having parent BindingContext still null when constructing from xaml
                BindingContext = new MainPageViewModel();


            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
                Console.WriteLine(e);
            }
        }

        private void OnPaintSurface(object sender, SKPaintGLSurfaceEventArgs skPaintGlSurfaceEventArgs)
        {
            if (sender is SKCanvasView skiaView)
            {


                //var canvas = e.Surface.Canvas;
                //canvas.Clear(SKColors.Red);

                //using var save = new SKAutoCanvasRestore(canvas, true);

                //scale = Math.Min(
                //    (float)e.BackendRenderTarget.Width / baseSize.Width,
                //    (float)e.BackendRenderTarget.Height / baseSize.Height);

                //var screenRect = (SKRect)e.BackendRenderTarget.Rect;
                //var centeredRect = screenRect.AspectFit(baseSize);

                //offset = centeredRect.Location;

                //canvas.Translate(offset);
                //canvas.Scale(scale);

                //canvas.ClipRect(SKRect.Create(baseSize));

                //game.Draw(canvas);


                //Device.StartTimer(TimeSpan.FromMilliseconds(8), () =>
                //{
                //    // Update the UI

                //    MainThread.BeginInvokeOnMainThread(() =>
                //    {
                //        skiaView.InvalidateSurface();
                //    });

                //    return false;
                //});
            }



        }
    }
}
