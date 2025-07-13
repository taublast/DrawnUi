namespace Sandbox.Views
{



    public partial class MainPageUnevenRows
    {
        private double _Position;
        private readonly MockChatViewModel _vm;

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

        public MainPageUnevenRows()
        {
            try
            {
                InitializeComponent();

                BindingContext = _vm = new MockChatViewModel();

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

        private void SkiaControl_OnTapped(object sender, SkiaControl.ControlTappedEventArgs e)
        {
            //refresh, will not change scroll position
            //StackCells.ApplyItemsSource();

            if (BindingContext is MockChatViewModel vm)
            {
                var items = vm.Items.Skip(2).ToList(); //new "copy"
                StackCells.ItemsSource = items;
            }

        }
    }
}
