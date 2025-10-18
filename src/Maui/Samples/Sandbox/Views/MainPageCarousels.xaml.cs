using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Sandbox.Views
{
    public partial class MainPageCarousels
    {
        public MainPageCarousels()
        {
            try
            {
                InitializeComponent();

                //avoid setting context BEFORE InitializeComponent, can bug
                //having parent BindingContext still null when constructing from xaml
                var viewModel = new MainPageViewModel();

                // Add carousel-specific properties
                viewModel.IsLooped = true;
                viewModel.Bounces = true;
                viewModel.SwipeSpeed = 1.0;

                // Add carousel commands
                viewModel.CommandToggleLoop = new Command(() =>
                {
                    viewModel.IsLooped = !viewModel.IsLooped;
                    MainCarousel.IsLooped = viewModel.IsLooped;
                });

                viewModel.CommandToggleBounces = new Command(() =>
                {
                    viewModel.Bounces = !viewModel.Bounces;
                    MainCarousel.Bounces = viewModel.Bounces;
                });

                viewModel.CommandGoPrev = new Command(() => MainCarousel.GoPrev());
                viewModel.CommandGoNext = new Command(() => MainCarousel.GoNext());

                viewModel.CommandSetSpeed05 = new Command(() =>
                {
                    viewModel.SwipeSpeed = 0.5;
                    MainCarousel.SwipeSpeed = 0.5;
                });

                viewModel.CommandSetSpeed10 = new Command(() =>
                {
                    viewModel.SwipeSpeed = 1.0;
                    MainCarousel.SwipeSpeed = 1.0;
                });

                viewModel.CommandSetSpeed20 = new Command(() =>
                {
                    viewModel.SwipeSpeed = 2.0;
                    MainCarousel.SwipeSpeed = 2.0;
                });

                BindingContext = viewModel;
            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
                Console.WriteLine(e);
            }
        }
    }

    public class BoolToLoopStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isLooped)
            {
                return isLooped ? "Mode: Infinite Loop (wraps around)" : "Mode: Standard (stops at edges)";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
