namespace DrawnUi.Views
{
    public interface IThermalStateService
    {
        ThermalState CurrentState { get; }
        event Action<ThermalState>? StateChanged;
    }
}