namespace PreviewTests.Views
{
    public partial class DrawersPage
    {

        public DrawersPage()
        {
            try
            {
                InitializeComponent();

                //avoid setting context BEFORE initialize component, can bug 
                //having parent context still null when constructing from xaml

                BindingContext = new MainPageViewModel();
            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
                Console.WriteLine(e);
            }
        }

        private void SkiaButton_Tapped(object sender, SkiaGesturesParameters skiaGesturesParameters)
        {

        }
    }
}
