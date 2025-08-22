using AppoMobi.Maui.Gestures;
using AppoMobi.Specials;
using PreviewTests;

namespace PreviewTests.Views.Controls;

public partial class ButtonToRoot
{
    public ButtonToRoot()
    {
        InitializeComponent();
    }

    private void GoToRoot(object sender, ControlTappedEventArgs controlTappedEventArgs)
    {
        if (TouchEffect.CheckLockAndSet())
            return;

        App.Instance.SetMainPage(new MainPage());
    }
}
