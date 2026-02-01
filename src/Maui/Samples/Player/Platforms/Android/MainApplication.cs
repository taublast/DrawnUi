using Android.App;
using Android.Runtime;

namespace PlayerTests
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => PlayerTests.MauiProgram.CreateMauiApp();
    }
}