using Microsoft.Extensions.Logging;

namespace DrawnUi.Draw;

public class DrawnUiStartupSettings
{
    /// <summary>
    /// Will be used by Super.Log calls
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// For desktop: if set will affect the app window at startup.
    /// </summary>
    public WindowParameters? DesktopWindow { get; set; }

    /// <summary>
    /// Avoid safe insets and remove system ui like status bar etc if supported by platform
    /// </summary>
    public bool? MobileIsFullscreen { get; set; }

    /// <summary>
    /// Listen to desktop keyboard keys with KeyboardManager. Windows and Catalyst available.
    /// </summary>
    public bool UseDesktopKeyboard { get; set; }

    /// <summary>
    /// Will be executed after DrawnUI is initialized and MAUI App is created. 
    /// </summary>
    public Action<IServiceProvider> Startup { get; set; }

}

