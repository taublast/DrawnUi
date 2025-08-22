namespace PreviewTests.Views
{
    public partial class ControlsPage
    {

        public ControlsPage()
        {
            try
            {
                InitializeComponent();

                BindingContext = new MainPageViewModel();
            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
            }
        }


    }
}
