namespace DrawnUI.Tutorials;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnCardsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("cards");
    }

    private async void OnCardsCodeClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("cardscode");
    }

    private async void OnNewsFeedClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("newsfeed");
    }

    private async void OnFirstAppClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("firstapp");
    }

    private async void OnButtonClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("button");
    }
}