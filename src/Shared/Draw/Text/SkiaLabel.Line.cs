namespace DrawnUi.Draw;

public partial class SkiaLabel
{
    public SKFontMetrics FontMetrics { get; protected set; }


    public class TextMetrics
    {
        SKFontMetrics FontMetrics { get; set; }

        public double LineSpacing { get; set; }

        public float LineHeightPixels { get; set; }

        public double ParagraphSpacing { get; set; }

        public double SpaceBetweenParagraphs
        {
            get
            {
                return LineHeightWithSpacing * ParagraphSpacing;
            }
        }
        public double LineHeightWithSpacing
        {
            get
            {
                return LineHeightPixels + SpaceBetweenLines;
            }
        }
        public double SpaceBetweenLines
        {
            get
            {
                if (FontMetrics.Leading > 0)
                {
                    return FontMetrics.Leading * LineSpacing;
                }
                else
                {
                    if (LineSpacing != 1)
                    {
                        double defaultLeading = LineHeightPixels * 0.1;
                        return defaultLeading * LineSpacing;
                    }
                    return 0;
                }
            }
        }
    }

    public class DecomposedText
    {

        public TextLine[] Lines { get; set; }

        public int CountParagraphs { get; set; }

        public bool WasCut { get; set; }
        /// <summary>
        /// pixels
        /// </summary>
        public float HasMoreVerticalSpace { get; set; }
        /// <summary>
        /// pixels
        /// </summary>
        public float HasMoreHorizontalSpace { get; set; }

        public AutoSizeType AutoSize { get; set; }
    }

}