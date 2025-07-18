using System.Collections.Concurrent;

namespace DrawnUi.Draw;

// MAUI

/// <summary>
/// MAUI-specific theme provider implementation
/// </summary>
public class MauiThemeProvider : IThemeProvider
{
    private AppTheme _lastTheme = AppTheme.Unspecified;

    public AppTheme CurrentTheme
    {
        get
        {
            var userTheme = Application.Current?.UserAppTheme ?? AppTheme.Unspecified;
            var systemTheme = Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
            return userTheme != AppTheme.Unspecified ? userTheme : systemTheme;
        }
    }

    public event EventHandler<ThemeChangedEventArgs> ThemeChanged;

    public MauiThemeProvider()
    {
        if (Application.Current != null)
        {
            Application.Current.PropertyChanged += OnApplicationPropertyChanged;
            _lastTheme = CurrentTheme;
        }
    }

    private void OnApplicationPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Application.RequestedTheme) ||
            e.PropertyName == nameof(Application.UserAppTheme))
        {
            var newTheme = CurrentTheme;
            if (newTheme != _lastTheme)
            {
                var oldTheme = _lastTheme;
                _lastTheme = newTheme;
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(oldTheme, newTheme));
            }
        }
    }
}

/// <summary>
/// Theme binding extension for DrawnUI controls
/// Supports MAUI, Blazor, and other platforms through IThemeProvider abstraction
/// </summary>
[ContentProperty(nameof(Default))]
public class ThemeBindingExtension : IMarkupExtension<object>
{
    public object Light { get; set; }
    public object Dark { get; set; }
    public object Default { get; set; }

    public object ProvideValue(IServiceProvider serviceProvider)
    {
        if (serviceProvider == null)
            return GetCurrentThemeValue();

        try
        {
            // Get the target object and property
            var targetProvider = serviceProvider.GetService<IProvideValueTarget>();
            if (targetProvider?.TargetObject == null || targetProvider.TargetProperty == null)
                return GetCurrentThemeValue();

            var targetObject = targetProvider.TargetObject;
            var targetProperty = targetProvider.TargetProperty;

            // Create a theme-aware binding that will update when theme changes
            if (targetObject is BindableObject bindableObject && targetProperty is BindableProperty bindableProperty)
            {
                // Create and register a theme binding
                var themeBinding = ThemeBindingManager.CreateBinding(this, bindableObject, bindableProperty);

                // Return initial value
                return GetCurrentThemeValue();
            }

            return GetCurrentThemeValue();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ThemeBinding error: {ex.Message}");
            return GetCurrentThemeValue(); // Fallback to current value instead of Default
        }
    }

    public object GetCurrentThemeValue()
    {
        var currentTheme = ThemeBindingManager.CurrentTheme;

        return currentTheme switch
        {
            AppTheme.Dark => Dark ?? Default,
            AppTheme.Light => Light ?? Default,
            _ => Default ?? Light // Fallback to Light if Default is null
        };
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
    {
        return ProvideValue(serviceProvider);
    }
}
