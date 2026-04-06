namespace DrawnUi.Draw
{
    public struct StringReference
    {
        public ReadOnlySpan<char> Spans
        {
            get
            {
                return Source.AsSpan().Slice(StartIndex, Length);
            }
        }

        public string Source { get; set; }

        /// <summary>
        /// Position inside existing string
        /// </summary>
        public int StartIndex { get; set; }

        /// <summary>
        /// Length inside existing string
        /// </summary>
        public int Length { get; set; }
    }
}