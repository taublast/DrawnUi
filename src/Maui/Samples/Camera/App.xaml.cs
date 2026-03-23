using System.Reflection;
using CameraTests.Services;
using CameraTests.Views;

namespace CameraTests
{
    public partial class App : Application
    {
        public App()
        {
            Super.SetLocale("en");

            InitializeComponent();

#if ANDROID
            Super.SetNavigationBarColor(Colors.Black, Colors.Black, false);
#endif

        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            return new Window(new MainPage(Super.Services.GetService<IRealtimeTranscriptionService>()));
        }


        public static App Instance => App.Current as App;
    }




}
