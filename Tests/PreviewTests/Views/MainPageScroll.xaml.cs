namespace PreviewTests.Views
{
    public partial class MainPageScroll
    {

        public MainPageScroll()
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
