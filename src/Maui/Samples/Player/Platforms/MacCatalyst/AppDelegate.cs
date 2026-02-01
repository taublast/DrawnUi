using Foundation;

namespace PlayerTests
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => PlayerTests.MauiProgram.CreateMauiApp();
    }
}