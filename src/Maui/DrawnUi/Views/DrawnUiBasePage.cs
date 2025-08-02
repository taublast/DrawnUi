namespace DrawnUi.Views;

/// <summary>
///     Actually used to: respond to keyboard resizing on mobile and keyboard key presses on Mac. Other than for that this
///     is not needed at all.
/// </summary>
public class DrawnUiBasePage : ContentPage
{
    private double keyboardSize;

    public DrawnUiBasePage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
    }

    public void KeyboardResized(double keyboardSize)
    {
        Debug.WriteLine($"[DrawnUiBasePage] Keyboard {keyboardSize}");
        KeyboardSize = OnKeyboardResized(keyboardSize);
    }

    public virtual double OnKeyboardResized(double size)
    {
        return size;
    }

    /// <summary>
    /// Will be set to the current keyboard size
    /// </summary>
    public double KeyboardSize  
    {
        get => keyboardSize;
        set
        {
            if (value.Equals(keyboardSize))
            {
                return;
            }

            keyboardSize = value;
            OnPropertyChanged();
        }
    }
}
