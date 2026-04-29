namespace Microsoft.Maui.Devices
{
    public sealed class DeviceDisplay
    {
        private DeviceDisplay()
        {
        }

        public static DeviceDisplay Current { get; } = new();

        public static event EventHandler<DisplayInfoChangedEventArgs> MainDisplayInfoChanged;

        public DisplayInfo MainDisplayInfo { get; set; } = new(1.0);

        public static void RaiseMainDisplayInfoChanged(DisplayInfo displayInfo)
        {
            MainDisplayInfoChanged?.Invoke(Current, new DisplayInfoChangedEventArgs(displayInfo));
        }
    }
}