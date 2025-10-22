namespace DrawnUi.Draw;

public interface IVisibilityAware
{
    /// <summary>
    /// This can sometimes be omitted, 
    /// </summary>
    void OnAppearing();

    /// <summary>
    /// This event can sometimes be called without prior OnAppearing
    /// </summary>
    void OnAppeared();

    void OnDisappeared();

    void OnDisappearing();
}