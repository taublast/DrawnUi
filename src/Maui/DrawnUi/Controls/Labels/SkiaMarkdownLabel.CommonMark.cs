using System.ComponentModel.Design;
using CommonMark;
using CommonMark.Syntax;

namespace DrawnUi.Draw;

/// <summary>
/// Will internally create spans from markdown.
/// Spans property must not be set directly.
/// </summary>
public partial class SkiaRichLabel : SkiaLabel
{
    protected override void SetTextInternal()
    {
        if (IsDisposed || IsDisposing)
            return;

        lock (LockParsing)
        {
            try
            {
                base.SetTextInternal();

                if (!string.IsNullOrEmpty(TextInternal))
                {
                    var markdownDocument = CommonMarkConverter.Parse(TextInternal);

                    Spans.Clear();

                    isBold = false;
                    isItalic = false;
                    isHeading1 = false;
                    isHeading2 = false;
                    isCodeBlock = false;
                    hadParagraph = false;
                    isStrikethrough = false;

                    if (markdownDocument != null)
                    {
                        var block = markdownDocument.FirstChild;
                        while (block != null)
                        {
                            RenderBlock(block);
                            block = block.NextSibling;
                        }
                    }
                }
                else
                {
                    Spans.Clear();
                }
            }
            finally
            {
            }
        }
    }

    /// <summary>
    /// Renders a markdown block element into text spans
    /// </summary>
    protected void RenderBlock(Block block, Inline prefix = null)
    {
        var wasHeading1 = isHeading1;
        var wasHeading2 = isHeading2;
        var wasCode = isCodeBlock;

        if (block.Tag == BlockTag.AtxHeading || block.Tag == BlockTag.SetextHeading)
        {
            var level = block.Heading.Level;
            isHeading1 = level == 1;
            if (!isHeading1)
            {
                isHeading2 = true;
            }

            if (block.InlineContent != null)
            {
                if (hadParagraph)
                {
                    RenderInline(CreateLineBreak());
                }

                hadParagraph = true;

                var inline = block.InlineContent.FirstChild;
                while (inline != null)
                {
                    RenderInline(inline);
                    inline = inline.NextSibling;
                }
            }
        }
        else if (block.Tag == BlockTag.Paragraph)
        {
            if (hadParagraph)
            {
                RenderInline(CreateLineBreak());
            }

            if (prefix != null)
            {
                RenderInline(prefix);
            }

            hadParagraph = true;

            var inline = block.InlineContent?.FirstChild;
            if (inline == null)
            {
                var currentInline = block.InlineContent;
                while (currentInline != null)
                {
                    RenderInline(currentInline);
                    currentInline = currentInline.NextSibling;
                }
            }
            else
            {
                while (inline != null)
                {
                    RenderInline(inline);
                    inline = inline.NextSibling;
                }
            }
        }
        else if (block.Tag == BlockTag.IndentedCode || block.Tag == BlockTag.FencedCode)
        {
            if (hadParagraph)
            {
                RenderInline(CreateLineBreak());
            }

            if (prefix != null)
            {
                RenderInline(prefix);
            }

            hadParagraph = true;
            isCodeBlock = true;

            var content = block.StringContent.ToString();
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    RenderInline(CreateLineBreak());
                }

                AddTextSpan(lines[i], (span) =>
                {
                    span.TextColor = CodeTextColor;
                    span.ParagraphColor = ColorCodeBlock;
                });
            }
        }
        else if (block.Tag == BlockTag.List)
        {
            var itemNumber = 1;
            var listData = block.ListData;
            var listItem = block.FirstChild;
            while (listItem != null)
            {
                if (listData.ListType == ListType.Ordered)
                {
                    RenderOrderedListItem(listItem, itemNumber);
                    itemNumber++;
                }
                else
                {
                    RenderBulletListItem(listItem);
                }

                listItem = listItem.NextSibling;
            }
        }
        else if (block.Tag == BlockTag.ListItem)
        {
            var childBlock = block.FirstChild;
            while (childBlock != null)
            {
                RenderBlock(childBlock);
                childBlock = childBlock.NextSibling;
            }
        }

        isCodeBlock = wasCode;
        isHeading1 = wasHeading1;
        isHeading2 = wasHeading2;
    }

    // Replace RenderOrderedListItem method:
    protected void RenderOrderedListItem(Block listItem, int startNumber)
    {
        Inline prefix;
        var firstLine = true;
        var childBlock = listItem.FirstChild;

        while (childBlock != null)
        {
            if (firstLine)
            {
                prefix = CreateLiteral(string.Format(PrefixNumbered, startNumber));
                firstLine = false;
            }
            else
            {
                prefix = null;
            }

            RenderBlock(childBlock, prefix);
            childBlock = childBlock.NextSibling;
        }
    }

    // Replace RenderBulletListItem method:
    protected void RenderBulletListItem(Block listItem)
    {
        Inline prefix;
        var firstLine = true;
        var childBlock = listItem.FirstChild;

        while (childBlock != null)
        {
            if (firstLine)
            {
                prefix = CreateLiteral(PrefixBullet);
                firstLine = false;
            }
            else
            {
                prefix = null;
            }

            RenderBlock(childBlock, prefix);
            childBlock = childBlock.NextSibling;
        }
    }

    // Replace RenderInline method:
    protected void RenderInline(Inline inline)
    {
        bool wasBold = isBold;
        bool wasItalic = isItalic;
        bool wasStrikethrough = isStrikethrough;

        switch (inline.Tag)
        {
            case InlineTag.LineBreak:
            case InlineTag.SoftBreak:
                var last = Spans.LastOrDefault();
                if (last == null)
                {
                    Spans.Add(new() { Text = "\n" });
                }
                else
                {
                    last.Text += "\n";
                }

                break;

            case InlineTag.String:
                AddTextSpan(inline.LiteralContent);
                break;

            case InlineTag.Code:
                AddCodeSpan(inline);
                break;

            case InlineTag.Link:
                AddLinkSpan(inline);
                break;

            case InlineTag.Strong:
                isBold = true;
                var strongChild = inline.FirstChild;
                while (strongChild != null)
                {
                    RenderInline(strongChild);
                    strongChild = strongChild.NextSibling;
                }

                break;

            case InlineTag.Emphasis:
                isItalic = true;
                var emphasisChild = inline.FirstChild;
                while (emphasisChild != null)
                {
                    RenderInline(emphasisChild);
                    emphasisChild = emphasisChild.NextSibling;
                }

                break;

            case InlineTag.Strikethrough:
                isStrikethrough = true;
                var strikeChild = inline.FirstChild;
                while (strikeChild != null)
                {
                    RenderInline(strikeChild);
                    strikeChild = strikeChild.NextSibling;
                }

                break;
        }

        isBold = wasBold;
        isItalic = wasItalic;
        isStrikethrough = wasStrikethrough;
    }

    protected void RenderBlock(Inline inline)
    {
        bool wasBold = isBold;
        bool wasItalic = isItalic;
        bool wasStrikethrough = isStrikethrough;

        switch (inline.Tag)
        {
            case InlineTag.LineBreak:
            case InlineTag.SoftBreak:
                var last = Spans.LastOrDefault();
                if (last == null)
                {
                    Spans.Add(new() { Text = "\n" });
                }
                else
                {
                    last.Text += "\n";
                }

                break;

            case InlineTag.String:
                AddTextSpan(inline.LiteralContent);
                break;

            case InlineTag.Code:
                AddCodeSpan(inline);
                break;

            case InlineTag.Link:
                AddLinkSpan(inline);
                break;

            case InlineTag.Strong:
                isBold = true;
                var strongChild = inline.FirstChild;
                while (strongChild != null)
                {
                    RenderInline(strongChild);
                    strongChild = strongChild.NextSibling;
                }

                break;

            case InlineTag.Emphasis:
                isItalic = true;
                var emphasisChild = inline.FirstChild;
                while (emphasisChild != null)
                {
                    RenderInline(emphasisChild);
                    emphasisChild = emphasisChild.NextSibling;
                }

                break;

            case InlineTag.Strikethrough:
                isStrikethrough = true;
                var strikeChild = inline.FirstChild;
                while (strikeChild != null)
                {
                    RenderInline(strikeChild);
                    strikeChild = strikeChild.NextSibling;
                }

                break;
        }

        isBold = wasBold;
        isItalic = wasItalic;
        isStrikethrough = wasStrikethrough;
    }

    protected virtual void AddCodeSpan(Inline code)
    {
        string text = code.LiteralContent;

        AddTextSpan(text, (span) =>
        {
            span.TextColor = this.CodeTextColor;
            span.BackgroundColor = CodeBackgroundColor;
        });
    }


    protected virtual void AddLinkSpan(Inline link)
    {
        string text = GetLinkLabelText(link);
        if (string.IsNullOrEmpty(text))
            text = link.TargetUrl;

        AddTextSpan(text, (span) =>
        {
            span.Tag = link.TargetUrl;
            span.TextColor = this.LinkColor;
            span.Underline = this.UnderlineLink;
            span.UnderlineWidth = this.UnderlineWidth;
            span.ForceCaptureInput = true;
        });
    }


    protected virtual string GetLinkLabelText(Inline link)
    {
        var child = link.FirstChild;
        string labelText = "";
        while (child != null)
        {
            if (child.Tag == InlineTag.String)
            {
                labelText += child.LiteralContent;
            }

            child = child.NextSibling;
        }

        return labelText;
    }


    /// <summary>
    /// Creates a line break inline element
    /// </summary>
    protected virtual Inline CreateLineBreak()
    {
        return new Inline(InlineTag.LineBreak);
    }

    /// <summary>
    /// Creates a literal inline element with specified content
    /// </summary>
    protected virtual Inline CreateLiteral(string content)
    {
        return new Inline(InlineTag.String) { LiteralContent = content };
    }
}
