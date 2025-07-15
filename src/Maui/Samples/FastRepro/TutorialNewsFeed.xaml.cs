using DrawnUi.Views;
using Sandbox;
using Sandbox.ViewModels;

namespace Sandbox;

public partial class TutorialNewsFeed : DrawnUiBasePage
{

    public TutorialNewsFeed()
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
