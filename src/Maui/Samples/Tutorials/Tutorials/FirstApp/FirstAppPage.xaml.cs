using DrawnUi.Draw;

namespace DrawnUI.Tutorials.FirstApp;

public partial class FirstAppPage : ContentPage
{
    private int clickCount = 0;

    public FirstAppPage()
    {
        InitializeComponent();
    }

    private async void OnButtonClicked(object sender, ControlTappedEventArgs e)
    {
        clickCount++;
        ClickLabel.Text = $"Button clicked {clickCount} times! 🎉";
        
        // Simple animation
        await MyButton.ScaleToAsync(1.1,1.1, 100);
        await MyButton.ScaleToAsync(1,1, 100);
    }
}
