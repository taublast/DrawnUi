namespace DrawnUi.Draw
{
    public struct WindowParameters
    {
        public int Width { get; set; }

        public int Height { get; set; }

        /// <summary>
        /// For desktop: if you set this to true the app window will not be allowed to be resized manually.
        /// </summary>
        public bool IsFixedSize { get; set; }
    }
}
