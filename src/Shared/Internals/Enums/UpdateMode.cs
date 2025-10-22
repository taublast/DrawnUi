namespace DrawnUi.Infrastructure.Enums;

public enum UpdateMode
{
    /// <summary>
    /// Will update when needed. 
    /// </summary>
    Dynamic,

    /// <summary>
    /// Constantly invalidating the canvas after every frame
    /// </summary>
    Constant,

    /// <summary>
    /// Will not update until manually invalidated.
    /// </summary>
    Manual
}