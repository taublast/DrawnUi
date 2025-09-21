using Breakout.Game;

namespace GameTemplate
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

#if ANDROID
    Super.SetNavigationBarColor(Colors.Black, Colors.Black, false);    
#endif

        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new NavigationPage(new MainPage()));
        }
    }
}

