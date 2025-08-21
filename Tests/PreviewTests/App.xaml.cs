using System.Reflection;

namespace PreviewTests
{
    public partial class App : Application
    {
        public App()
        {
            Super.SetLocale("en");

            InitializeComponent();

            MainPage = new AppShell();

            var mask = "MainPage";

            var xamlResources = this.GetType().Assembly
                .GetCustomAttributes<XamlResourceIdAttribute>();

            MainPages = xamlResources
                .Where(x => x.Type.Name.Contains(mask)
                && !x.Type.Name.ToLower().Contains("dev")

                && x.Type.Name != mask)
                .Select(s => new MainPageVariant()
                {
                    Name = s.Type.Name.Replace(mask, string.Empty),
                    Type = s.Type
                }).ToList();
        }

        public static List<MainPageVariant> MainPages { get; protected set; }

        public void SetMainPage(Page page)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                MainPage = page;
            });
        }

        public static App Instance => App.Current as App;
    }

    public record MainPageVariant()
    {
        public Type Type { get; set; }
        public string Name { get; set; }
    }



}
