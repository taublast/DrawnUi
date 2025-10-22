namespace DrawnUi.Draw
{

    public enum TransformAspect
    {
        None,

        /// <summary>
        /// Enlarges to fill the viewport without maintaining aspect ratio if smaller, but does not scale down if larger
        /// </summary>
        Fill,

        /// <summary>
        /// Fit without maintaining aspect ratio and without enlarging if smaller
        /// </summary>
        Fit,

        /// <summary>
        /// Fit inside viewport respecting aspect without enlarging if smaller, could result in the image having some blank space around
        /// </summary>
        AspectFit,

        /// <summary>
        /// Covers viewport respecting aspect without scaling down if bigger, could result in the image being cropped
        /// </summary>
        AspectFill,

        /// <summary>
        /// AspectFit + AspectFill. Enlarges to cover the viewport or reduces size to fit inside the viewport both respecting aspect ratio, ensuring the entire image is always visible, potentially leaving some parts of the viewport uncovered.
        /// </summary>
        AspectFitFill,

        /// <summary>
        /// Fit + Fill. Enlarges to cover the viewport or reduces size to fit inside the viewport without respecting aspect ratio, ensuring the entire image is always visible, potentially leaving some parts of the viewport uncovered.
        /// </summary>
        FitFill,

        /// <summary>
        /// Enlarges to cover the viewport if smaller and reduces size if larger, all without respecting aspect ratio. Same as AspectFitFill but will crop the image to fill entire viewport.
        /// </summary>
        Cover,

        /// <summary>
        /// Enlarges to cover the viewport or reduces size to fit inside the viewport both respecting aspect ratio. Always covers the entire viewport, potentially cropping the image if it's larger,
        /// never leaves empty space inside viewport.
        /// </summary>
        AspectCover,

        /// <summary>
        /// TODO very soon!
        /// </summary>
        Tile,
    }


}