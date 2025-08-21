namespace DrawnUi.Draw
{
    public class ControlTappedEventArgs : EventArgs
    {
        public object Control { get; set; }
        public SkiaGesturesParameters Parameters { get; set; }
        public GestureEventProcessingInfo ProcessingInfo { get; set; }

        public ControlTappedEventArgs(object control, SkiaGesturesParameters args, GestureEventProcessingInfo info)
        {
            Control = control;
            Parameters = args;
            ProcessingInfo = info;
        }
    }
}
