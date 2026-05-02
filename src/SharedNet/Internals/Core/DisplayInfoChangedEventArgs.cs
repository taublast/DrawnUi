namespace DrawnUi.Draw
{
    public sealed class DisplayInfoChangedEventArgs : EventArgs
    {
        public DisplayInfoChangedEventArgs(DisplayInfo displayInfo)
        {
            DisplayInfo = displayInfo;
        }

        public DisplayInfo DisplayInfo { get; }
    }
}