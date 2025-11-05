namespace DrawnUi.Draw
{
    public enum TextTransform
    {
        None = 0,

        Lowercase,

        Uppercase,

        /// <summary>
        /// Every word starts with a capital letter, others do not change 
        /// </summary>
        Titlecase,

        /// <summary>
        /// First word starts with a capital letter, others do not change 
        /// </summary>
        Phrasecase
    }
}
