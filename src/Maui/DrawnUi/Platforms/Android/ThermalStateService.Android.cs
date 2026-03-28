using Android.Content;
using Android.OS;
using System.Runtime.Versioning;
using DrawnUi.Draw;
using Platform = Microsoft.Maui.ApplicationModel.Platform;

namespace DrawnUi;

public sealed class ThermalStateService : Java.Lang.Object, IThermalStateService
{
    private readonly PowerManager _powerManager;

    public ThermalState CurrentState
    {
        get
        {
            if (_powerManager == null || !OperatingSystem.IsAndroidVersionAtLeast(29))
                return ThermalState.Unknown;

            return MapState(_powerManager.CurrentThermalStatus);
        }
    }

    public event Action<ThermalState>? StateChanged;

    public ThermalStateService()
    {
        _powerManager = Platform.AppContext?.GetSystemService(Context.PowerService) as PowerManager;

        if (_powerManager != null && OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            _powerManager.ThermalStatus += OnThermalStatusChanged;
        }
    }

    [SupportedOSPlatform("android29.0")]
    private void OnThermalStatusChanged(object sender, PowerManager.ThermalStatusChangedEventArgs e)
    {
        StateChanged?.Invoke(MapState(e.Status));
    }

    [SupportedOSPlatform("android29.0")]
    private static ThermalState MapState(Android.OS.ThermalStatus nativeState)
    {
        return nativeState switch
        {
            Android.OS.ThermalStatus.None => ThermalState.Nominal,
            Android.OS.ThermalStatus.Light => ThermalState.Fair,
            Android.OS.ThermalStatus.Moderate => ThermalState.Fair,
            Android.OS.ThermalStatus.Severe => ThermalState.Serious,
            Android.OS.ThermalStatus.Critical => ThermalState.Critical,
            Android.OS.ThermalStatus.Emergency => ThermalState.Critical,
            Android.OS.ThermalStatus.Shutdown => ThermalState.Critical,
            _ => ThermalState.Unknown
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _powerManager != null && OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            _powerManager.ThermalStatus -= OnThermalStatusChanged;
        }

        base.Dispose(disposing);
    }
}