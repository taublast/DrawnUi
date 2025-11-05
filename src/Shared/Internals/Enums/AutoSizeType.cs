namespace DrawnUi.Draw;

public enum AutoSizeType
{
    None,

    /// <summary>
    /// This might be faster than FitHorizontal or FillHorizontal for dynamically changing text
    /// </summary>
    FitFillHorizontal,

    /// <summary>
    /// This might be faster than FitVertical or FillVertical for dynamically changing text
    /// </summary>
    FitFillVertical,

    /// <summary>
    /// todo FIX NOT WORKING!!! If you have dynamically changing text think about using FitFillHorizontal instead
    /// </summary>
    FitHorizontal,

    /// <summary>
    /// If you have dynamically changing text think about using FitFillHorizontal instead
    /// </summary>
    FillHorizontal,

    /// <summary>
    /// If you have dynamically changing text think about using FitFillVertical instead
    /// </summary>
    FitVertical,


    /// <summary>
    /// If you have dynamically changing text think about using FitFillVertical instead
    /// </summary>
    FillVertical,

}

