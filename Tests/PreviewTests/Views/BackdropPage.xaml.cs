namespace PreviewTests.Views
{
    public partial class BackdropPage
    {
        private double _Position;
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

        public BackdropPage()
        {
            try
            {
                InitializeComponent();

                //avoid setting context BEFORE InitializeComponent, can bug 
                //having parent BindingContext still null when constructing from xaml
                BindingContext = new MainPageViewModel();

                //var check1 = MainScroll.GetPositionOnCanvasInPoints();
                //var check2 = Control1.GetPositionOnCanvasInPoints();

            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
                Console.WriteLine(e);
            }
        }


        //private void OnScrolled(object sender, ScaledPoint e)
        //{
        //    Position = Control1.GetPositionOnCanvasInPoints().Y;
        //}
    }
}
