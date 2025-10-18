using DrawnUi.Draw;

namespace Sandbox.Views
{
    public partial class MainPageButtons
    {
        private int tapCount = 0;

        public MainPageButtons()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
                Console.WriteLine(e);
            }
        }

        private void OnButtonClicked(object sender, ControlTappedEventArgs controlTappedEventArgs)
        {
            tapCount++;
            if (sender is SkiaButton button)
            {
                TapCountLabel.Text = $"Total taps: {tapCount} | Last: \"{button.Text}\"";
            }
        }
    }
}
