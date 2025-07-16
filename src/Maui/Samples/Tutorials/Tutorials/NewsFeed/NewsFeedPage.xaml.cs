using DrawnUI.Tutorials.NewsFeed.ViewModels;
using DrawnUi.Views;

namespace DrawnUI.Tutorials.NewsFeed;

public partial class NewsFeedPage : DrawnUiBasePage
{

    public NewsFeedPage()
    {
        try
        {
            InitializeComponent();
            BindingContext = new NewsViewModel();
        }
        catch (Exception e)
        {
            Super.DisplayException(this, e);
        }
    }

}
