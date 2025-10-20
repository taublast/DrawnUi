using Sandbox.Views;
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox
{
    public class TestCanvas : Canvas
    {
        protected override void Draw(DrawingContext context)
        {
            base.Draw(context);

            SKCanvas canvas = context.Context.Canvas;

            canvas.Clear(SKColors.Maroon);

            // center the entire drawing
            canvas.Translate(100, 300);

            // the "3D camera"
            var view = new Sk3dView();

            // rotate to a nice 3D view
            view.RotateXDegrees(-25);
            view.RotateYDegrees(45);

            // move the origin of the 3D view
            view.Translate(-50, 50, 50);

            // define the cube face
            var face = SKRect.Create(0, 0, 100, 100);

            // draw the left face
            using (new SKAutoCanvasRestore(canvas, true))
            {
                // get the face in the correct location
                view.Save();
                view.RotateYDegrees(-90);
                view.ApplyToCanvas(canvas);
                view.Restore();

                // draw the face
                var leftFace = new SKPaint { Color = SKColors.LightGray, IsAntialias = true };
                canvas.DrawRect(face, leftFace);
            }

            // draw the right face
            using (new SKAutoCanvasRestore(canvas, true))
            {
                // get the face in the correct location
                view.Save();
                view.TranslateZ(-100);
                view.ApplyToCanvas(canvas);
                view.Restore();

                // draw the face
                var rightFace = new SKPaint { Color = SKColors.Gray, IsAntialias = true };
                canvas.DrawRect(face, rightFace);
            }


            // draw the top face
            /*
            using (new SKAutoCanvasRestore(canvas, true))
            {
                // get the face in the correct location
                view.Save();
                view.RotateXDegrees(90);
                view.ApplyToCanvas(canvas);
                view.Restore();

                // draw the face
                var topFace = new SKPaint
                {
                    Color = SKColors.DarkGray,
                    IsAntialias = true
                };
                canvas.DrawRect(face, topFace);
            }
            */
        }
    }

    public class MainPageCodeTransforms : BasePageCodeBehind, IDisposable
    {
        Canvas Canvas;

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                this.Content = null;
                Canvas?.Dispose();
            }

            base.Dispose(isDisposing);
        }


        public override void Build()
        {
            Canvas?.Dispose();

            Canvas = new Canvas()
            {
                Gestures = GesturesMode.Enabled,
                RenderingMode = RenderingModeType.Accelerated,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.Black,
                Content = new SkiaLayout()
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Children = new List<SkiaControl>()
                    {
                        new SkiaShape()
                        {
                            BackgroundColor = Colors.DodgerBlue,
                            ZIndex = -1,
                            CornerRadius = 16,
                            RotationX = 16,
                            RotationY = 20,
                            RotationZ = -6,
                            TranslationZ = 0,
                            Tag = "Shape",
                            WidthRequest = 150,
                            HeightRequest = 150,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center,
                            Children = new List<SkiaControl>()
                            {
                                new SkiaLabel()
                                {
                                    Tag = "Label",
                                    TextColor = Colors.White,
                                    HorizontalOptions = LayoutOptions.Center,
                                    VerticalOptions = LayoutOptions.Center,
                                    Text = $"Overlay {CountReloads}"
                                }
                            }
                        }
                    }
                }
            };


            this.Content = Canvas;
        }
    }
}
