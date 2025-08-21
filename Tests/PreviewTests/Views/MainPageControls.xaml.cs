namespace PreviewTests.Views
{
    public partial class MainPageControls
    {

        public MainPageControls()
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
