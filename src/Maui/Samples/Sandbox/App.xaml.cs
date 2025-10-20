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

            var mask = "MainPage";

            // Get XAML-based pages from XamlResourceIdAttribute
            var xamlResources = this.GetType().Assembly
                .GetCustomAttributes<XamlResourceIdAttribute>();

            var xamlPages = xamlResources
                .Where(x => x.Type.Name.Contains(mask)
                && !x.Type.Name.ToLower().Contains("dev")
                && x.Type.Name != mask)
                .Select(s => new MainPageVariant()
                {
                    Name = s.Type.Name.Replace(mask, string.Empty),
                    Type = s.Type
                });

            // Get code-defined pages from Views folder only (classes that inherit from Page and match the mask)
            var allTypes = this.GetType().Assembly.GetTypes();
            var codePages = allTypes
                .Where(t => t.Name.Contains(mask)
                && !t.Name.ToLower().Contains("dev")
                && t.Name != mask
                && typeof(Page).IsAssignableFrom(t)
                && !t.IsAbstract
                && t.Namespace != null
                && t.Namespace.EndsWith(".Views") // Only include pages from Views folder
                && !xamlPages.Any(xp => xp.Type == t)) // Exclude already found XAML pages
                .Select(t => new MainPageVariant()
                {
                    Name = t.Name.Replace(mask, string.Empty),
                    Type = t
                });

            // Combine both lists and sort alphabetically by name
            MainPages = xamlPages.Concat(codePages)
                .OrderBy(p => p.Name)
                .ToList();
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
