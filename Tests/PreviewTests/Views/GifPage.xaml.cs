namespace PreviewTests.Views
{
    public partial class GifPage
    {

        public GifPage()
        {
            try
            {
                InitializeComponent();

                BindingContext = this;
            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
            }
        }

        private bool _Visibility = true;
        public bool Visibility
        {
            get
            {
                return _Visibility;
            }
            set
            {
                if (_Visibility != value)
                {
                    _Visibility = value;
                    OnPropertyChanged();
                }
            }
        }

        private void SkiaButton_OnTapped(object sender, ControlTappedEventArgs controlTappedEventArgs)
        {
            MainThread.BeginInvokeOnMainThread(() => //for maui bindings..
            {
                Visibility = !Visibility;
            });
        }
    }
}
