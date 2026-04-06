namespace DrawnUi.Draw
{
    public interface IHasBanner
    {
        /// <summary>
        /// Main image
        /// </summary>
        public string Banner { get; set; }

        /// <summary>
        /// Indicates that it's already preloading
        /// </summary>
        public bool BannerPreloadOrdered { get; set; }
    }
}
