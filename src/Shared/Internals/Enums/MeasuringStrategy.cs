namespace DrawnUi.Draw
{
    public enum MeasuringStrategy
    {
        /// <summary>
        /// For different children sizes
        /// </summary>
        MeasureAll,

        /// <summary>
        /// Best for equal item sizes
        /// </summary>
        MeasureFirst,

        /// <summary>
        /// EXPERIMENTAL PREVIEW!!! Acts like MeasureAll but measures by chunks in background.
        /// </summary>
        MeasureVisible,
    }
}
