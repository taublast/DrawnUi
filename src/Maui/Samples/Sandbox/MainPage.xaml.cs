using Sandbox;
using Sandbox.Views;

namespace MauiNet8;

public partial class MainPage : BasePageCodeBehind
{
    int count = 0;

    public List<MainPageVariant> ButtonsList { get; }

    public MainPage()
    {
        try
        {
            ButtonsList = App.MainPages;

            InitializeComponent();

            BindingContext = new MainPageViewModel();
        }
        catch (Exception e)
        {
            Super.DisplayException(this, e);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

#if ANDROID

        //App.TestLink();

#endif
    }

    private void TappedSelectPage(object sender, ControlTappedEventArgs controlTappedEventArgs)
    {
        if (sender is SkiaButton btn)
        {
            var page = App.MainPages.FirstOrDefault(x => x.Name == btn.Text);
            if (page != null)
            {
                var instance = (ContentPage)Activator.CreateInstance(page.Type);

                App.Instance.SetMainPage(instance);

            }
        }
    }




}
