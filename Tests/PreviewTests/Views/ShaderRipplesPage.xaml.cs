namespace PreviewTests.Views
{
    public partial class ShaderRipplesPage
    {

        public ShaderRipplesPage()
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
