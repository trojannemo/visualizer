using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Visualizer;

[Activity(Label = "Visualizer", Theme = "@style/Maui.SplashTheme", MainLauncher = true,
          ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation,
          ScreenOrientation = ScreenOrientation.Portrait)]  // Forces portrait mode
public class MainActivity : MauiAppCompatActivity
{
}
