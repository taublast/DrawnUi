using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using DrawnUi;
 
namespace Sandbox
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

        }

        public override bool DispatchKeyEvent(KeyEvent e)
        {
            if (e.Action == KeyEventActions.Down)
            {
                // Process key down event
                var keyCode = e.KeyCode;

                // For example, handle a specific key:
                if (keyCode == Keycode.A)
                {
                    // Do something when the "A" key is pressed
                }

                System.Diagnostics.Debug.WriteLine($"KEY DOWN {keyCode}");
            }

            // Pass the event on to let other components process it
            return base.DispatchKeyEvent(e);
        }

    }
}
