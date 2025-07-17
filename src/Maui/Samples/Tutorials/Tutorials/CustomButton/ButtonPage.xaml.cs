using System.Diagnostics;

namespace DrawnUI.Tutorials.CustomButton;

public partial class ButtonPage : ContentPage
{
    public ButtonPage()
    {
        InitializeComponent();
    }

    private void ClickedPlay(object sender, EventArgs e)
    {
        Debug.WriteLine("PLAY was pressed! 🎉");
    }

    private void ClickedBlue(object sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => { DisplayAlert("Success", "Blue button pressed! 💚", "OK"); });
    }

    private void ClickedGreen(object sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => { DisplayAlert("Success", "Green button pressed! 💚", "OK"); });
    }

    private void ClickedOrange(object sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => { DisplayAlert("Success", "Orange button pressed! 💚", "OK"); });
    }
}
