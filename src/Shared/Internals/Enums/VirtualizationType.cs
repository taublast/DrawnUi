namespace DrawnUi.Draw;

public enum VirtualisationType
{
    /// <summary>
    /// Visible parent bounds are not accounted for, children are rendred as usual.
    /// </summary>
    Disabled,

    /// <summary>
    /// Children not withing visible parent bounds are not rendered
    /// </summary>
    Enabled,

    /// <summary>
    /// Only the creation of a cached object is permitted for children not within visible parent bounds
    /// </summary>
    Smart,

    /// <summary>
    /// Parent is responsible for providing visible viewport for this control via GetVisibleViewport,
    /// will not check intersection with this control DrawingRect.
    /// </summary>
    Managed
}
