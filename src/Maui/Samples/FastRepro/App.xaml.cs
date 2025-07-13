using System.Reflection;

namespace Sandbox
{
    public partial class App : Application
    {
        public App()
        {
            Super.SetLocale("en");

            InitializeComponent();

            MainPage = new AppShell();
        }



        public void SetMainPage(Page page)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                MainPage = page;
            });
        }

        public static App Instance => App.Current as App;
    }




}
