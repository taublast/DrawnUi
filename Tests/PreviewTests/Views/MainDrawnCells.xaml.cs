namespace PreviewTests.Views
{
    public partial class MainDrawnCells
    {
        private double _Position;
        private readonly MockChat2ViewModel _vm;

        public double Position
        {
            get
            {
                return _Position;
            }
            set
            {
                if (_Position != value)
                {
                    _Position = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowPosition));
                }
            }
        }

        public string ShowPosition
        {
            get
            {
                return $"{Position:0.0}";
            }
        }

        public MainDrawnCells()
        {
            try
            {
                InitializeComponent();

                BindingContext = _vm = new MockChat2ViewModel();

            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
                Console.WriteLine(e);
            }
        }


        bool once;

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (!once)
            {
                once = true;
                _vm.LoadData();
            }

        }

        private void OnScrolled(object sender, ScaledPoint e)
        {

        }
    }
}
