namespace PreviewTests.Views
{
    [Preview<SkiaScroll>]
    public partial class ScrollPage
    {

        public ScrollPage()
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
