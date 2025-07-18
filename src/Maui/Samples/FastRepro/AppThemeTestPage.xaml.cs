using Microsoft.Maui.Controls;
using DrawnUi.Draw;

namespace Sandbox;

public partial class AppThemeTestPage : ContentPage
{
    private bool _isDarkTheme = false;
    private readonly System.Threading.Timer _diagnosticsTimer;

    public AppThemeTestPage()
    {
        try
        {
            InitializeComponent();
            SetupCodeBehindBindings();
            UpdateCurrentThemeDisplay();

            // Setup diagnostics timer to update binding count
            _diagnosticsTimer = new System.Threading.Timer(UpdateDiagnostics, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }
        catch (Exception e)
        {
            Super.DisplayException(this, e);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateCurrentThemeDisplay();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _diagnosticsTimer?.Dispose();
    }

    private void OnThemeSwitchClicked(object sender, EventArgs e)
    {
        try
        {
            // Toggle theme
            _isDarkTheme = !_isDarkTheme;
            
            // Set the application theme
            Application.Current.UserAppTheme = _isDarkTheme ? AppTheme.Dark : AppTheme.Light;
            
            // Update button text
            ThemeSwitchButton.Text = _isDarkTheme ? "Switch to Light Theme" : "Switch to Dark Theme";
            
            // Update current theme display
            UpdateCurrentThemeDisplay();
            
            // Force a layout update to ensure theme changes are applied
            this.ForceLayout();

            // Force update our custom theme bindings
            ThemeBindingManager.UpdateAllBindings();

            // Optional: Add a small delay and force another update
            Device.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(100);
                UpdateCurrentThemeDisplay();
                this.ForceLayout();

                // Force update again after layout
                ThemeBindingManager.UpdateAllBindings();
            });
        }
        catch (Exception ex)
        {
            Super.DisplayException(this, ex);
        }
    }

    private void UpdateCurrentThemeDisplay()
    {
        try
        {
            var currentTheme = Application.Current.RequestedTheme;
            var userTheme = Application.Current.UserAppTheme;
            
            string themeText = $"Current Theme: {currentTheme}";
            if (userTheme != AppTheme.Unspecified)
            {
                themeText += $" (User: {userTheme})";
            }
            
            CurrentThemeLabel.Text = themeText;
            
            // Update internal state to match actual theme
            _isDarkTheme = (userTheme == AppTheme.Dark) || 
                          (userTheme == AppTheme.Unspecified && currentTheme == AppTheme.Dark);
            
            // Update button text to reflect current state
            ThemeSwitchButton.Text = _isDarkTheme ? "Switch to Light Theme" : "Switch to Dark Theme";
        }
        catch (Exception ex)
        {
            CurrentThemeLabel.Text = $"Error getting theme: {ex.Message}";
        }
    }

    private void SetupCodeBehindBindings()
    {
        try
        {
            // Method 1: Direct binding using helper
            ThemeBindings.SetThemeBinding(
                CodeBehindLabel1, SkiaLabel.TextColorProperty, Colors.Purple, Colors.Yellow);
            ThemeBindings.SetThemeBinding(
                CodeBehindLabel1, SkiaLabel.BackgroundColorProperty, Colors.LightPink, Colors.DarkMagenta);

            // Method 2: Fluent API
            CodeBehindLabel2.WithThemeBinding(SkiaLabel.TextColorProperty, Colors.DarkRed, Colors.LightCoral)
                           .WithThemeBinding(SkiaLabel.BackgroundColorProperty, Colors.Beige, Colors.DarkOliveGreen)
                           .WithThemeBinding(SkiaLabel.FontSizeProperty, 14.0, 20.0);

            // Method 3: Helper method for getting values
            var themeColor = ThemeBindings.GetThemeValue(Colors.Navy, Colors.SkyBlue);
            var themeFontSize = ThemeBindings.GetThemeValue(16.0, 22.0);

            CodeBehindLabel3.TextColor = themeColor;
            CodeBehindLabel3.FontSize = themeFontSize;
            CodeBehindLabel3.BackgroundColor = ThemeBindings.GetThemeValue(Colors.Ivory, Colors.DarkSlateBlue);
        }
        catch (Exception ex)
        {
            Super.DisplayException(this, ex);
        }
    }

    private void OnForceUpdateClicked(object sender, EventArgs e)
    {
        try
        {
            ThemeBindingManager.UpdateAllBindings();
            UpdateCurrentThemeDisplay();

            // Show feedback
            DisplayAlert("Force Update", $"Updated {ThemeBindingManager.ActiveBindingCount} active bindings", "OK");
        }
        catch (Exception ex)
        {
            Super.DisplayException(this, ex);
        }
    }

    private void OnCleanupClicked(object sender, EventArgs e)
    {
        try
        {
            var beforeCount = ThemeBindingManager.ActiveBindingCount;
            ThemeBindingManager.Cleanup();
            var afterCount = ThemeBindingManager.ActiveBindingCount;

            var cleanedUp = beforeCount - afterCount;
            DisplayAlert("Cleanup", $"Cleaned up {cleanedUp} dead bindings.\nActive bindings: {afterCount}", "OK");

            UpdateCurrentThemeDisplay();
        }
        catch (Exception ex)
        {
            Super.DisplayException(this, ex);
        }
    }

    private void UpdateDiagnostics(object state)
    {
        try
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                var activeBindings = ThemeBindingManager.ActiveBindingCount;
                var currentTheme = ThemeBindingManager.CurrentTheme;
                DiagnosticsLabel.Text = $"Active Bindings: {activeBindings} | Current Theme: {currentTheme}";
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Diagnostics update error: {ex.Message}");
        }
    }
}
