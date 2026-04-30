namespace DrawnUi.Draw
{
    public readonly struct SizeRequest
    {
        public SizeRequest(Size request, Size minimum)
        {
            Request = request;
            Minimum = minimum;
        }

        public Size Request { get; }

        public Size Minimum { get; }

        public double Width => Request.Width;

        public double Height => Request.Height;
    }
}