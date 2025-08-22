namespace PreviewTests.Views
{
    [Preview<SkiaLabel>]
    public partial class DrawnSpansPage
    {

        public DrawnSpansPage()
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
}
