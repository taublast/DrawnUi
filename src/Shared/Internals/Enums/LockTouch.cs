namespace DrawnUi.Draw;

public enum LockTouch
{
    Disabled,

    /// <summary>
    /// Pass nothing below and mark all gestures as consumed by this control
    /// </summary>
    Enabled,

    /// <summary>
    /// Pass nothing below
    /// </summary>
    PassNone,

    /// <summary>
    /// Pass only Tapped below
    /// </summary>
    PassTap,

    /// <summary>
    /// Pass only Tapped and LongPressing below
    /// </summary>
    PassTapAndLongPress
}