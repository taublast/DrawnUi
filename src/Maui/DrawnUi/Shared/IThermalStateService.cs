using DrawnUi.Draw;

namespace DrawnUi;

public interface IThermalStateService
{
    ThermalState CurrentState { get; }
    event Action<ThermalState>? StateChanged;
}