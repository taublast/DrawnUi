namespace DrawnUi.Infrastructure;

/// <summary>
/// Standard paper formats
/// </summary>
public enum PaperFormat
{
    Custom,
    A4,
    A5,
    A6,
    Letter,
    Legal
}


public struct PdfPagePosition
{
    /// <summary>
    /// Page index
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Position of the page start
    /// </summary>
    public SKPoint Position { get; set; }

    /// <summary>
    /// This can sometimes be less then the paper size if the content is smaller
    /// </summary>
    public float Height { get; set; }
}

public static class Pdf
{
    /// <summary>
    /// Gets the paper size in pixels for a given paper format and DPI.
    /// </summary>
    /// <param name="format">The paper format.</param>
    /// <param name="dpi">The dots per inch (DPI) value.</param>
    /// <returns>The paper size in pixels as an SKSize.</returns>
    public static SKSize GetPaperSizePixels(PaperFormat format, int dpi)
    {
        var paperSizeInInches = GetPaperSizeInInches(format);
        return GetPaperSizePixels(paperSizeInInches, dpi);
    }

    /// <summary>
    /// Gets the paper size in pixels for a custom paper size in inches and DPI.
    /// </summary>
    /// <param name="paperSizeInInches">The paper size in inches.</param>
    /// <param name="dpi">The dots per inch (DPI) value.</param>
    /// <returns>The paper size in pixels as an SKSize.</returns>
    public static SKSize GetPaperSizePixels(SKSize paperSizeInInches, int dpi)
    {
        float widthPixels = paperSizeInInches.Width * dpi;
        float heightPixels = paperSizeInInches.Height * dpi;
        return new SKSize(widthPixels, heightPixels);
    }

    /// <summary>
    /// Gets the paper size in pixels for a custom paper size in millimeters and DPI.
    /// </summary>
    /// <param name="paperSizeInMillimeters">The paper size in millimeters.</param>
    /// <param name="dpi">The dots per inch (DPI) value.</param>
    /// <returns>The paper size in pixels as an SKSize.</returns>
    public static SKSize GetPaperSizePixelsFromMillimeters(SKSize paperSizeInMillimeters, int dpi)
    {
        var paperSizeInInches = new SKSize(paperSizeInMillimeters.Width / 25.4f, paperSizeInMillimeters.Height / 25.4f);
        return GetPaperSizePixels(paperSizeInInches, dpi);
    }

    /// <summary>
    /// Splits a SkiaStack content into multiple pages based on the provided paper size, considering height only.
    /// </summary>
    /// <param name="content">The size of the content to be split.</param>
    /// <param name="paper">The size of the paper to split the content into.</param>
    /// <returns>A list of PdfPagePosition representing the positions of the pages.</returns>
    public static List<PdfPagePosition> SplitToPages(SKSize content, SKSize paper)
    {
        var positions = new List<PdfPagePosition>();
        int index = 0;

        if (content.Height <= paper.Height)
        {
            // Content fits in a single page
            positions.Add(new PdfPagePosition
            {
                Index = index,
                Position = new SKPoint(0, 0)
            });
            return positions;
        }

        for (float y = 0; y < content.Height; y += paper.Height)
        {
            positions.Add(new PdfPagePosition
            {
                Index = index++,
                Position = new SKPoint(0, y)
            });
        }

        return positions;
    }

    static (SkiaLayout Layout, double AccumulatedOffsetPts) FindVStack(SkiaControl control, bool needTemplated, double ptsOffset)
    {
        //find vstack
        SkiaLayout vstack = null;
        if (control is SkiaLayout maybe && maybe.Type == LayoutType.Column)
        {
            if (needTemplated)
            {
                if (maybe.IsTemplated)
                    return (maybe, ptsOffset);
            }
            else
            {
                return (maybe, ptsOffset);
            }
        }
        //look deeper
        var accumulate = ptsOffset + control.Margin.Top + control.Padding.Top;
        foreach (SkiaControl child in control.Views)
        {
            var childStack = FindVStack(child, needTemplated, accumulate);
            if (childStack.Layout is SkiaLayout maybeChild && maybeChild.Type == LayoutType.Column)
            {
                if (needTemplated)
                {
                    if (maybeChild.IsTemplated)
                        return (maybeChild, childStack.AccumulatedOffsetPts);
                }
                else
                {
                    return (maybeChild, childStack.AccumulatedOffsetPts);
                }


            }
        }
        return (null, ptsOffset);
    }

    /// <summary>
    /// Pages will be split upon first found vertical stick children.
    /// Must specify if stack is templated.
    /// If no stack is found will split to pages as usual.
    /// </summary>
    /// <param name="control"></param>
    /// <param name="isTemplated"></param>
    /// <param name="paper"></param>
    /// <param name="scale"></param>
    /// <returns></returns>
    public static List<PdfPagePosition> SplitStackToPages(SkiaControl control, bool isTemplated, SKSize paper, float scale = 1)
    {
        var positions = new List<PdfPagePosition>();

        var found = FindVStack(control, isTemplated, 0);
        var vstack = found.Layout;

        if (found.Layout == null)
        {
            return SplitToPages(control.MeasuredSize.Units, paper);
        }

        if (vstack.RenderTree.Count < 1)
        {
            positions.Add(new PdfPagePosition
            {
                Index = 0,
                Position = new SKPoint(0, 0),
                Height = 0
            });
            return positions;
        }

        int index = 0;
        float offset = 0f;
        int cellIndex = 0;
        float pageHeight = paper.Height;

        while (cellIndex < vstack.RenderTree.Count)
        {
            var cell = vstack.RenderTree[cellIndex];

            var childBottom = cell.Rect.Bottom + found.AccumulatedOffsetPts * scale;
            var maxSplit = offset + pageHeight;

            // If adding this cell would exceed the page boundary, start a new page
            if (childBottom > maxSplit)
            {
                var split = cell.Rect.Top + found.AccumulatedOffsetPts * scale;

                positions.Add(new PdfPagePosition
                {
                    Index = index++,
                    Position = new SKPoint(0, offset),
                    Height = (float)split
                });

                // Move offset to the next page start, aligning to the next cell
                offset = (float)split;
            }

            cellIndex++;
        }

        // Add the last page if it wasn't added yet
        if (positions.Count == 0 || positions.Last().Position.Y < offset)
        {
            positions.Add(new PdfPagePosition
            {
                Index = index,
                Position = new SKPoint(0, offset),
                Height = paper.Height
            });
        }

        return positions;
    }


    /// <summary>
    /// Calculates the paper size in pixels based on the paper size in inches and DPI.
    /// </summary>
    /// <param name="paperSizeInInches">The paper size in inches.</param>
    /// <param name="dpi">The dots per inch (DPI) value.</param>
    /// <returns>The paper size in pixels as an SKSize.</returns>
    public static SKSize GetPaperSizePixels(SKSize paperSizeInInches, float dpi)
    {
        float widthPixels = paperSizeInInches.Width * dpi;
        float heightPixels = paperSizeInInches.Height * dpi;
        return new SKSize(widthPixels, heightPixels);
    }

    /// <summary>
    /// Gets the paper size in inches for a given paper format.
    /// </summary>
    /// <param name="format">The paper format.</param>
    /// <returns>The paper size in inches as an SKSize.</returns>
    public static SKSize GetPaperSizeInInches(PaperFormat format)
    {
        switch (format)
        {
            case PaperFormat.A4:
                return new SKSize(8.27f, 11.69f);
            case PaperFormat.A5:
                return new SKSize(5.83f, 8.27f);
            case PaperFormat.A6:
                return new SKSize(4.13f, 5.83f);
            case PaperFormat.Letter:
                return new SKSize(8.5f, 11f);
            case PaperFormat.Legal:
                return new SKSize(8.5f, 14f);
            case PaperFormat.Custom:
                return new SKSize(4.13f, 2.0f);
                throw new ArgumentException("Custom size must be provided separately.", nameof(format));
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }
}

