
using System.Diagnostics;
 
using AppoMobi.Specials;
using DrawnUi.Infrastructure;
using DrawnUi.Internals;
using Sandbox;
using Sandbox.Views;
using Canvas = DrawnUi.Views.Canvas;

namespace MauiNet8;

public class MyCanvas : Canvas
{
    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        Trace.WriteLine($"Measuring Canvas for {widthConstraint} x {heightConstraint}");
        return base.MeasureOverride(widthConstraint, heightConstraint);
    }
}

public partial class SmallButton : SkiaButton
{

    async void AnimatePress(SkiaControl icon)
    {
        await icon.ScaleToAsync(0.9, 0.9, 50, Easing.CubicOut);
        await icon.ScaleToAsync(1.0, 1.0, 50, Easing.SpringOut);
    }

    public override bool OnDown(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        this.ScaleToAsync(0.98, 0.95, 16, Easing.CubicOut);

        return base.OnDown(args, apply);
    }


    public override void OnUp(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        this.ScaleToAsync(1.0, 1.0, 32, Easing.SpringOut);

        base.OnUp(args, apply);
    }



    SkiaLabel MainLabel;
    SkiaShape MainFrame;

    //protected override void OnMeasured()
    //{
    //    base.OnMeasured();

    //    if (MainLabel == null)
    //    {
    //        MainLabel = this.FindView<SkiaLabel>("MainLabel");
    //        MainLabel.Text = Text;
    //    }

    //    if (MainFrame == null)
    //    {
    //        MainFrame = this.FindView<SkiaShape>("MainFrame");
    //        this.TransformView = MainFrame;
    //    }
    //}

    //protected override void OnPropertyChanged(string propertyName = null)
    //{
    //    base.OnPropertyChanged(propertyName);

    //    if (propertyName == nameof(Text))
    //    {
    //        if (MainLabel != null)
    //        {
    //            MainLabel.Text = this.Text;
    //        }
    //    }
    //}
}

public partial class MainPageDev : BasePageCodeBehind
{
    private readonly List<string> _shaders;

    public MainPageDev()
    {
        try
        {

            Items.AddRange(new[]
            {
                "8.jpg","monkey1.jpg","hugrobot2.jpg"
            });

            InitializeComponent();

            _shaders = Files.ListAssets(path);

            BindingContext = this;
        }
        catch (Exception e)
        {
            Super.DisplayException(this, e);
        }
    }

    public ObservableRangeCollection<string> Items { get; } = new();

    private int _SelectedIndex;
    public int SelectedIndex
    {
        get
        {
            return _SelectedIndex;
        }
        set
        {
            if (_SelectedIndex != value)
            {
                _SelectedIndex = value;
                OnPropertyChanged();
            }
        }
    }



    async void SelectFIle()
    {
        if (_shaders.Count > 1)
        {
            var options = _shaders.Select(name => new SelectableAction
            {
                Action = async () =>
                {
                    ShaderFile = name;
                },
                Title = name
            }).ToList();
            var selected = await PresentSelection(options, "Select Shader") as SelectableAction;
            selected?.Action();
        }
    }

    public async Task<SelectableAction> PresentSelection(IEnumerable<SelectableAction> options,
        string title = null, string cancel = null)
    {
        if (string.IsNullOrEmpty(title))
            title = "Select";

        if (string.IsNullOrEmpty(cancel))
            cancel = "Cancel";

        var result = await App.Current.MainPage.DisplayActionSheet(title, cancel,
            null, options.Select(x => x.Title).ToArray()
        );

        if (string.IsNullOrEmpty(result))
        {
            return null; //cancel
        }

        var selected = options.FirstOrDefault(x => x.Title == result);
        return selected;
    }

    private void SkiaButton_OnTapped(object sender, SkiaGesturesParameters e)
    {
        MainThread.BeginInvokeOnMainThread(SelectFIle);
    }

    private string path = @"Shaders\transitions";

    public string FullShaderPath
    {
        get
        {
            return $"{path}\\{ShaderFile}";
        }
        set
        {

        }
    }

    private string _ShaderFile = "dreamy.sksl";
    public string ShaderFile
    {
        get
        {
            return _ShaderFile;
        }
        set
        {
            if (_ShaderFile != value)
            {
                _ShaderFile = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullShaderPath));
            }
        }
    }

    private void SkiaButton_Tapped(object sender, ControlTappedEventArgs e)
    {
        Trace.WriteLine("TAPPED");
    }


    private void Blocked(object sender, ControlTappedEventArgs e)
    {
        Trace.WriteLine("BLOCKED");
    }
}
