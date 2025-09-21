using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Breakout;

[Activity(Theme = "@style/MainTheme", MainLauncher = true, 
    LaunchMode = LaunchMode.SingleTop, 
    ConfigurationChanges = ConfigChanges.ScreenSize | 
                           ConfigChanges.Orientation
                           | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | 
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Density, 
    ScreenOrientation = ScreenOrientation.SensorPortrait)]

public class MainActivity : MauiAppCompatActivity
{
}
