using System.Collections.Concurrent;

namespace DrawnUi.Draw;

// SHARED

/// <summary>
/// Platform-agnostic theme provider interface for cross-platform compatibility
/// </summary>
public interface IThemeProvider
{
    AppTheme CurrentTheme { get; }
    event EventHandler<ThemeChangedEventArgs> ThemeChanged;
}

/// <summary>
/// Theme changed event arguments
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
    public AppTheme NewTheme { get; }
    public AppTheme OldTheme { get; }

    public ThemeChangedEventArgs(AppTheme oldTheme, AppTheme newTheme)
    {
        OldTheme = oldTheme;
        NewTheme = newTheme;
    }
}

/// <summary>
/// Simplified theme binding manager using DrawnUI's disposal mechanism
/// No periodic cleanup needed - controls clean up themselves!
/// </summary>
public static class ThemeBindingManager
{
    private static readonly ConcurrentDictionary<int, WeakReference<ThemeBinding>> _bindings = new();
    private static IThemeProvider _themeProvider;
    private static volatile int _nextId = 0;

    static ThemeBindingManager()
    {
        // Initialize with MAUI theme provider by default
        _themeProvider = new MauiThemeProvider();
    }

    /// <summary>
    /// Current theme from the active theme provider
    /// </summary>
    public static AppTheme CurrentTheme => _themeProvider?.CurrentTheme ?? AppTheme.Light;

    /// <summary>
    /// Sets a custom theme provider (for Blazor, testing, etc.)
    /// </summary>
    public static void SetThemeProvider(IThemeProvider themeProvider)
    {
        _themeProvider = themeProvider ?? throw new ArgumentNullException(nameof(themeProvider));

        // Update all existing bindings with new theme
        UpdateAllBindings();
    }

    /// <summary>
    /// Creates and registers a new theme binding
    /// </summary>
    public static ThemeBinding CreateBinding(ThemeBindingExtension extension, BindableObject target, BindableProperty property)
    {
        var binding = new ThemeBinding(extension, target, property);
        return binding;
    }

    /// <summary>
    /// Registers a binding for tracking (called from ThemeBinding constructor)
    /// </summary>
    internal static void RegisterBinding(ThemeBinding binding)
    {
        var id = Interlocked.Increment(ref _nextId);
        _bindings.TryAdd(id, new WeakReference<ThemeBinding>(binding));

        // Cleanup dead references periodically
        if (_bindings.Count % 50 == 0)
        {
            Cleanup();
        }
    }

    /// <summary>
    /// Unregisters a binding (called from ThemeBinding.Dispose)
    /// </summary>
    internal static void UnregisterBinding(ThemeBinding binding)
    {
        var deadKeys = new List<int>();

        foreach (var kvp in _bindings)
        {
            if (!kvp.Value.TryGetTarget(out var target) || target == binding)
            {
                deadKeys.Add(kvp.Key);
            }
        }

        foreach (var key in deadKeys)
        {
            _bindings.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Forces update of all active bindings (useful for manual theme changes)
    /// </summary>
    public static void UpdateAllBindings()
    {
        var deadKeys = new List<int>();

        foreach (var kvp in _bindings)
        {
            if (kvp.Value.TryGetTarget(out var binding))
            {
                if (!binding.UpdateTargetValue())
                {
                    deadKeys.Add(kvp.Key);
                }
            }
            else
            {
                deadKeys.Add(kvp.Key);
            }
        }

        // Remove dead bindings
        foreach (var key in deadKeys)
        {
            _bindings.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Gets the current number of active bindings (for diagnostics)
    /// </summary>
    public static int ActiveBindingCount
    {
        get
        {
            var count = 0;
            foreach (var kvp in _bindings)
            {
                if (kvp.Value.TryGetTarget(out _))
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Manually triggers cleanup of dead references
    /// </summary>
    public static void Cleanup()
    {
        var deadKeys = new List<int>();

        foreach (var kvp in _bindings)
        {
            if (!kvp.Value.TryGetTarget(out _))
            {
                deadKeys.Add(kvp.Key);
            }
        }

        foreach (var key in deadKeys)
        {
            _bindings.TryRemove(key, out _);
        }

        System.Diagnostics.Debug.WriteLine($"ThemeBindingManager: Cleaned up {deadKeys.Count} dead bindings. Active: {ActiveBindingCount}");
    }


}

/// <summary>
/// Represents a theme binding for a specific target object and property
/// Implements proper disposal pattern and weak references to prevent memory leaks
/// </summary>
public class ThemeBinding : IDisposable
{
    private readonly ThemeBindingExtension _extension;
    private readonly WeakReference<BindableObject> _targetObjectRef;
    private readonly BindableProperty _targetProperty;
    private readonly int _hashCode;
    private volatile bool _disposed;

    internal ThemeBinding(ThemeBindingExtension extension, BindableObject targetObject, BindableProperty targetProperty)
    {
        _extension = extension ?? throw new ArgumentNullException(nameof(extension));
        _targetObjectRef = new WeakReference<BindableObject>(targetObject ?? throw new ArgumentNullException(nameof(targetObject)));
        _targetProperty = targetProperty ?? throw new ArgumentNullException(nameof(targetProperty));

        // Pre-calculate hash code for efficient lookups
        _hashCode = HashCode.Combine(targetObject.GetHashCode(), targetProperty.GetHashCode());

        // Subscribe to theme changes directly (original working approach)
        if (Application.Current != null)
        {
            Application.Current.PropertyChanged += OnApplicationPropertyChanged;
        }

        // Use DrawnUI's elegant disposal mechanism - fire and forget!
        if (targetObject is SkiaControl skiaControl)
        {
            var cleanupKey = $"ThemeBinding_{Guid.NewGuid()}";
            skiaControl.ExecuteUponDisposal[cleanupKey] = () =>
            {
                if (Application.Current != null)
                {
                    Application.Current.PropertyChanged -= OnApplicationPropertyChanged;
                }
                _disposed = true;
            };
        }
        else
        {
            // Fallback for non-SkiaControl bindable objects
            ThemeBindingManager.RegisterBinding(this);
        }
    }

    private void OnApplicationPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Application.RequestedTheme) ||
            e.PropertyName == nameof(Application.UserAppTheme))
        {
            UpdateTargetValue();
        }
    }

    /// <summary>
    /// Updates the target property with the current theme value
    /// Returns true if update was successful, false if target is no longer available
    /// </summary>
    public bool UpdateTargetValue()
    {
        if (_disposed)
            return false;

        if (!_targetObjectRef.TryGetTarget(out var targetObject))
        {
            // Target has been garbage collected, mark for cleanup
            return false;
        }

        try
        {
            var newValue = _extension.GetCurrentThemeValue();

            // Only update if value has actually changed to avoid unnecessary work
            var currentValue = targetObject.GetValue(_targetProperty);
            if (!Equals(currentValue, newValue))
            {
                targetObject.SetValue(_targetProperty, newValue);

                // Platform-specific invalidation
                InvalidateTarget(targetObject);
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ThemeBinding update error: {ex.Message}");
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvalidateTarget(BindableObject target)
    {
        // For DrawnUI controls, trigger a repaint
        if (target is SkiaControl skiaControl)
        {
            skiaControl.InvalidateViewport();
        }
        // Add other platform-specific invalidation here (Blazor, etc.)
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // For non-SkiaControl objects, manually unsubscribe
        // SkiaControls handle this automatically via ExecuteUponDisposal
        if (Application.Current != null)
        {
            Application.Current.PropertyChanged -= OnApplicationPropertyChanged;
        }

        // Only needed for fallback non-SkiaControl bindings
        ThemeBindingManager.UnregisterBinding(this);
    }

    public override int GetHashCode() => _hashCode;

    public override bool Equals(object obj) =>
        obj is ThemeBinding other &&
        _hashCode == other._hashCode &&
        _targetProperty == other._targetProperty &&
        _targetObjectRef.TryGetTarget(out var thisTarget) &&
        other._targetObjectRef.TryGetTarget(out var otherTarget) &&
        ReferenceEquals(thisTarget, otherTarget);
}

/// <summary>
/// Helper methods for using theme bindings in code-behind
/// </summary>
public static class ThemeBindings
{
    /// <summary>
    /// Sets up a theme binding in code-behind
    /// </summary>
    public static ThemeBinding SetThemeBinding(BindableObject target, BindableProperty property,
        object lightValue, object darkValue, object defaultValue = null)
    {
        var extension = new ThemeBindingExtension
        {
            Light = lightValue,
            Dark = darkValue,
            Default = defaultValue
        };

        var binding = ThemeBindingManager.CreateBinding(extension, target, property);

        // Set initial value
        var initialValue = extension.GetCurrentThemeValue();
        target.SetValue(property, initialValue);

        return binding;
    }

    /// <summary>
    /// Gets the current theme value without creating a binding
    /// </summary>
    public static T GetThemeValue<T>(T lightValue, T darkValue, T defaultValue = default)
    {
        var currentTheme = ThemeBindingManager.CurrentTheme;
        return currentTheme switch
        {
            AppTheme.Dark => darkValue,
            AppTheme.Light => lightValue,
            _ => defaultValue.Equals(default(T)) ? lightValue : defaultValue
        };
    }

    /// <summary>
    /// Fluent extension method for setting theme bindings
    /// </summary>
    public static TControl WithThemeBinding<TControl>(this TControl control, BindableProperty property,
        object lightValue, object darkValue, object defaultValue = null)
        where TControl : BindableObject
    {
        SetThemeBinding(control, property, lightValue, darkValue, defaultValue);
        return control;
    }
}
