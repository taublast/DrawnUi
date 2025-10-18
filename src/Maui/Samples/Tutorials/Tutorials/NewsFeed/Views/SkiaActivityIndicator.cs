namespace DrawnUI.Tutorials.NewsFeed;

/// <summary>
/// Drawn activity indicator uses Lottie animation
/// </summary>
public class SkiaActivityIndicator : LottieRefreshIndicator
{
    private static double MySize;

    protected override void CreateDefaultContent()
    {
        if (this.Views.Count == 0)
        {
            MySize = 50.0;

            SetDefaultMinimumContentSize(MySize, MySize);

            Loader = new()
            {
                Tag = "Loader", 
                AutoPlay = false,
                Repeat = -1,
                ColorTint = App.Current.Resources.Get<Color>("ColorPrimary"),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                LockRatio = 1,
                Source = "Lottie/iosloader.json"
            };

            AddSubView(new SkiaShape()
            {
                BackgroundColor = Colors.White,
                Margin = 5,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Fill,
                LockRatio = -1,
                Type = ShapeType.Circle,
                Padding = 5,
                Children =
                {
                    Loader
                },
                Shadows = new List<SkiaShadow>()
                {
                    new SkiaShadow
                    {
                        X = 2,
                        Y = 2,
                        Blur = 2,
                        Opacity = 0.3,
                        Color = Colors.Black
                    }
                }
            });

            if (IsRunning)
                Loader.Start();
        }
    }
}
