namespace PreviewTests.Views.Controls
{
    public partial class MainPageProgress
    {

        public MainPageProgress()
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

        private void SkiaButton_Tapped(object sender, AppoMobi.Maui.Gestures.TouchActionEventArgs e)
        {
            //MainCarousel.ChildrenFactory.PrintDebugVisible();
        }
    }
}
