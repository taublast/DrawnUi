using PreviewTests.Views.Controls;

namespace PreviewTests.Views
{
    [Preview<CircularProgress>]
    public partial class ProgressPage
    {

        public ProgressPage()
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
