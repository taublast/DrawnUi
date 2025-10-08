using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Sandbox
{
    public partial class TestCarouselLooped : ContentPage, INotifyPropertyChanged
    {
        public TestCarouselLooped()
        {
            try
            {
                InitializeComponent();

                // Initialize ItemsSource for templated carousels
                CarouselItems = new ObservableCollection<CarouselItemData>
                {
                    new CarouselItemData { Number = "1", Color = "#e94560" },
                    new CarouselItemData { Number = "2", Color = "#0f3460" },
                    new CarouselItemData { Number = "3", Color = "#533483" },
                    new CarouselItemData { Number = "4", Color = "#a8df8e" },
                    new CarouselItemData { Number = "5", Color = "#ffc93c" }
                };

                BindingContext = this;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private bool _isLooped = false;
        private ObservableCollection<CarouselItemData> carouselItems;

        public bool IsLooped
        {
            get => _isLooped;
            set
            {
                if (_isLooped != value)
                {
                    _isLooped = value;
                    OnPropertyChanged();

                    // Update all carousels
                    CarouselManual.IsLooped = value;
                    CarouselTemplated.IsLooped = value;
                }
            }
        }

        public ObservableCollection<CarouselItemData> CarouselItems 
        {
            get => carouselItems;
            set
            {
                if (Equals(value, carouselItems))
                {
                    return;
                }

                carouselItems = value;
                OnPropertyChanged();
            }
        }

        public ICommand CommandToggleLoop => new Command(() =>
        {
            IsLooped = !IsLooped;
        });

        public ICommand CommandGoPrevManual => new Command(() =>
        {
            CarouselManual.GoPrev();
        });

        public ICommand CommandGoNextManual => new Command(() =>
        {
            CarouselManual.GoNext();
        });

        public ICommand CommandGoPrevTemplated => new Command(() =>
        {
            CarouselTemplated.GoPrev();
        });

        public ICommand CommandGoNextTemplated => new Command(() =>
        {
            CarouselTemplated.GoNext();
        });

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CarouselItemData
    {
        public string Number { get; set; }
        public string Color { get; set; }
    }

    public class BoolToLoopTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isLooped)
            {
                return isLooped ? "TOGGLE: IsLooped = TRUE" : "TOGGLE: IsLooped = FALSE";
            }
            return "TOGGLE ISLOOPED";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
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
