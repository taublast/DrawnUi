namespace Microsoft.Maui.Controls
{
    public sealed class VisualTreeChangeEventArgs : EventArgs
    {
        public View Parent { get; init; }

        public View Child { get; init; }

        public int ChildIndex { get; init; }
    }
}
