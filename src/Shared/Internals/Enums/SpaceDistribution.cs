namespace DrawnUi.Draw;

public enum SpaceDistribution
{
    None,

    /// <summary>
    /// Distribute space evenly between all items but do not affect empty space remaining at the end of the last line
    /// </summary>
    Auto,

    /// <summary>
    /// Distribute space evenly between all items but do not affect empty space
    /// </summary>
    Full
}