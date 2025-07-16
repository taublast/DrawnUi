using DrawnUI.Tutorials.CustomButton;

namespace DrawnUI.Tutorials;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        
        // Register tutorial routes
        Routing.RegisterRoute("cards", typeof(InteractiveCards.TutorialCards));
        Routing.RegisterRoute("cardscode", typeof(InteractiveCards.TutorialCards));
        Routing.RegisterRoute("newsfeed", typeof(NewsFeed.NewsFeedPage));
        Routing.RegisterRoute("firstapp", typeof(FirstApp.FirstAppPage));
        Routing.RegisterRoute("button", typeof(ButtonPage));
    }
}
