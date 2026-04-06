namespace DrawnUi.Controls;

public class SkiaShellNavigatingArgs : EventArgs
{
    public SkiaShellNavigatingArgs(SkiaControl view, SkiaControl current, string route, NavigationSource source)
    {
        View = view;
        Route = route ?? "";
        Previous = current;
        Source = source;
    }

    /// <summary>
    /// If you set this to True the navigation will be canceled
    /// </summary>
    public bool Cancel { get; set; }

    /// <summary>
    /// Is never null
    /// </summary>
    public string Route { get; }

    /// <summary>
    /// The SkiaControl that will navigate
    /// </summary>
    public SkiaControl View { get; }

    /// <summary>
    /// The SkiaControl that is upfront now
    /// </summary>
    public SkiaControl Previous { get; }

    public NavigationSource Source { get; }
}