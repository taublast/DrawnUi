using DrawnUi.Draw;
using DrawnUi.Views;

namespace DrawnUI.Tutorials.NewsFeed;

public class AppActivityIndicator : SkiaActivityIndicator, IRefreshIndicator
{
    public AppActivityIndicator()
    {
        HorizontalOptions = LayoutOptions.Fill;
    }
}
