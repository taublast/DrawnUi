namespace DrawnUi.Views;

/// <summary>
///     Actually used to: respond to keyboard resizing on mobile and keyboard key presses on Mac. Other than for that this
///     is not needed at all.
/// </summary>
public class DrawnUiBasePage : ContentPage
{
    public DrawnUiBasePage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
    }

    public void KeyboardResized(double keyboardSize)
    {
        Debug.WriteLine($"[DrawnUiBasePage] Keyboard {keyboardSize}");
        OnKeyboardResized(keyboardSize);
    }

    public virtual double OnKeyboardResized(double size)
    {
        return size;
    }
}
