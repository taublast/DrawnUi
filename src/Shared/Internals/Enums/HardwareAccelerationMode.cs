namespace DrawnUi.Draw;

public enum RenderingModeType
{
    /// <summary>
    /// No hardware acceleration, lightweight and fast native renderers creation, best for static content, use cache for top layers.
    /// </summary>
    Default,

    /// <summary>
    /// Will use hardware accelerated renderers. Currently Metal for Apple, GL on Android and Angle on Windows. Windows note: canvas background will be opaque.
    /// </summary>
    Accelerated,

    /// <summary>
    /// Experimental: will retain rendering result across frames, you can draw only changed areas over previous result.
    /// Will use hardware accelerated renderers. Currently Metal for Apple, GL on Android and Angle on Windows. Windows note: canvas background will be opaque.
    /// </summary>
    AcceleratedRetained
}
