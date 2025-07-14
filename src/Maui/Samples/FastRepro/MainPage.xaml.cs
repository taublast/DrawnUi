using DrawnUi.Views;
using Sandbox;
using Sandbox.ViewModels;

namespace Sandbox;

public partial class MainPage : DrawnUiBasePage
{

    public MainPage()
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
