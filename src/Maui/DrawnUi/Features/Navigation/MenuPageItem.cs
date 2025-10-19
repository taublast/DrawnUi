namespace AppoMobi.Maui.Navigation;

public class MenuPageItem : BindableObject
{

    public LayoutOptions VOptions
    {
        get
        {
            if (Key == "placeholder")
            {
                return LayoutOptions.FillAndExpand;
            }
            return LayoutOptions.StartAndExpand;
        }
    }



    private bool _Separator;
    public bool Separator
    {
        get { return _Separator; }
        set
        {
            if (_Separator != value)
            {
                _Separator = value;
                OnPropertyChanged();
            }
        }
    }


    //public int Counter
    //{
    //    get
    //    {
    //        if (GetCounter != null)
    //            return GetCounter();
    //        return -1;
    //    }
    //}

    private int _Counter;
    public int Counter
    {
        get { return _Counter; }
        set
        {
            if (_Counter != value)
            {
                _Counter = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _ShowCounter;
    public bool ShowCounter
    {
        get { return _ShowCounter; }
        set
        {
            if (_ShowCounter != value)
            {
                _ShowCounter = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _IsOption;
    public bool IsOption
    {
        get { return _IsOption; }
        set
        {
            if (_IsOption != value)
            {
                _IsOption = value;
                OnPropertyChanged();
            }
        }
    }

    public Func<int> GetCounter { get; set; }

    public Func<string> GetTitle { get; set; }



    private string _Title;
    public string Title
    {
        get { return _Title; }
        set
        {
            if (_Title != value)
            {
                _Title = value;
                OnPropertyChanged();
            }
        }
    }



    public string IconSource { get; set; }

    public Color BackColor { get; set; } //{x:Static appoMobi:AppColors.Site_PanelXX}

    //new
    public string IconString { get; set; }

    public Action OnSelected { get; set; }

    public Type TargetType { get; set; }

    public object TypeParameter { get; set; }

    public Type ContentType { get; set; }

    public int Tab { get; set; } = 0;
    public bool NeedTransition { get; set; } = false;

    private bool _selectede = false;
    public bool Selected
    {
        get { return _selectede; }

        set
        {
            if (_selectede != value)
            {
                _selectede = value;
                OnPropertyChanged();
            }
        }
    }
    public bool PseudoTab { get; set; }
    public bool IsDetail { get; set; }
    public bool Modal { get; set; }

    private bool _visible = true;
    public bool Visible
    {
        get { return _visible; }

        set
        {
            if (_visible != value)
            {
                _visible = value;
                OnPropertyChanged();
            }
        }
    }
    public string MyId { get; set; }
    public string Url { get; set; }

    /// <summary>
    /// Use for logging etc
    /// </summary>
    private string _Key;
    public string Key
    {
        get { return _Key; }
        set
        {
            if (_Key != value)
            {
                _Key = value;
                OnPropertyChanged();
            }
            OnPropertyChanged("VOptions");
            OnPropertyChanged("Test");
        }
    }


    /// <summary>
    /// if module is disable the use this key to disable this item
    /// </summary>
    public string Module { get; set; }

    public bool AuthorizedOnly { get; set; }
    public string UserRoles { get; set; }
    public bool UnauthorizedOnly { get; set; }


    public MenuPageItem()
    {
        BackColor = Colors.Transparent;
        MyId = Guid.NewGuid().ToString();
    }

}