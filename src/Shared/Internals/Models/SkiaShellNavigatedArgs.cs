namespace DrawnUi.Controls;

public class SkiaShellNavigatedArgs : EventArgs
{
    public SkiaShellNavigatedArgs(SkiaControl view, string route, NavigationSource source)
    {
        View = view;
        Route = route ?? ""; //to avoid crashing if people process this as a non null string
        Source = source;
    }

    /// <summary>
    /// Is never null. 
    /// </summary>
    public string Route { get; }

    /// <summary>
    /// The SkiaControl that went upfront
    /// </summary>
    public SkiaControl View { get; }

    public NavigationSource Source { get; }
}