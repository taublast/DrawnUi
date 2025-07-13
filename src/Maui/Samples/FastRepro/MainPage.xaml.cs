using DrawnUi.Views;
using Sandbox;

namespace Sandbox;

public partial class MainPage : DrawnUiBasePage
{
    
    public MainPage()
    {
        try
        {

            InitializeComponent();

        }
        catch (Exception e)
        {
            Super.DisplayException(this, e);
        }
    }

}
