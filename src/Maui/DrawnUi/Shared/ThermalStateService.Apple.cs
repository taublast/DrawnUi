using System.Runtime.CompilerServices;
using CoreAnimation;
using Foundation;
using Microsoft.Maui.Handlers;
using DrawnUi.Draw;

namespace DrawnUi;

public interface IThermalStateService
{
    ThermalState CurrentState { get; }
    event Action<ThermalState>? StateChanged;
}

public class ThermalStateService : NSObject, IThermalStateService
{
    public ThermalState CurrentState => MapState(NSProcessInfo.ProcessInfo.ThermalState);

    public event Action<ThermalState>? StateChanged;

    public ThermalStateService()
    {
        // Register for changes (important!)
        NSNotificationCenter.DefaultCenter.AddObserver(NSProcessInfo.ThermalStateDidChangeNotification, ThermalStateChanged);
    }

    private void ThermalStateChanged(NSNotification notification)
    {
        var newState = MapState(NSProcessInfo.ProcessInfo.ThermalState);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StateChanged?.Invoke(newState);
        });
    }

    private static ThermalState MapState(NSProcessInfoThermalState nativeState)
    {
        return nativeState switch
        {
            NSProcessInfoThermalState.Nominal => ThermalState.Nominal,
            NSProcessInfoThermalState.Fair => ThermalState.Fair,
            NSProcessInfoThermalState.Serious => ThermalState.Serious,
            NSProcessInfoThermalState.Critical => ThermalState.Critical,
            _ => ThermalState.Unknown
        };
    }

    ~ThermalStateService()
    {
        NSNotificationCenter.DefaultCenter.RemoveObserver(this);
    }
}
