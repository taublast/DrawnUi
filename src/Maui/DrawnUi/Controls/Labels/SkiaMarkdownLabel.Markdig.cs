/*

using System.Diagnostics;
using System.Threading;
using System.Windows.Input;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

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
                base.SetTextInternal(); //prepare TextInternal from Text

                var copy = new string(TextInternal);
                if (!string.IsNullOrEmpty(copy))
                {
                    var pipeline = MarkdownPipelinePool.AcquirePipeline();
                    var markdownDocument = Markdown.Parse(copy, pipeline);

                    Spans.Clear();

                    isBold = false;
                    isItalic = false;
                    isHeading1 = false;
                    isHeading2 = false;
                    isCodeBlock = false;
                    hadParagraph = false;
                    isStrikethrough = false;

                    //AddTextSpan("HelloWorld!"); was debugging..

                    if (markdownDocument != null)
                    {
                        foreach (var block in markdownDocument)
                        {
                            RenderBlock(block);
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
 
     
 
    private readonly MarkdownPipeline _pipeline;
    protected void RenderBlock(Block block, Inline prefix = null)
    {
        var wasHeading1 = isHeading1;
        var wasHeading2 = isHeading2;
        var wasCode = isCodeBlock;

        if (block is HeadingBlock heading)
        {
            var level = heading.Level;
            isHeading1 = level == 1;
            if (!isHeading1)
            {
                isHeading2 = true;
            }

            if (heading.Inline != null)
            {
                if (hadParagraph)
                {
                    //start new line
                    RenderInline(new LineBreakInline());
                }

                hadParagraph = true;

                var inline = heading.Inline.FirstChild;
                while (inline != null)
                {
                    RenderInline(inline);
                    inline = inline.NextSibling;
                }
            }
        }
        else if (block is ParagraphBlock paragraphBlock)
        {
            if (hadParagraph)
            {
                //start new line
                RenderInline(new LineBreakInline());
            }

            if (prefix != null)
            {
                RenderInline(prefix);
            }

            hadParagraph = true;

            var inline = paragraphBlock.Inline.FirstChild;
            while (inline != null)
            {
                RenderInline(inline);
                inline = inline.NextSibling;
            }
        }
        else if (block is FencedCodeBlock codeBlock)
        {
            if (hadParagraph)
            {
                //start new line
                RenderInline(new LineBreakInline());
            }

            if (prefix != null)
            {
                RenderInline(prefix);
            }

            hadParagraph = true;

            isCodeBlock = true;

            var codeType = codeBlock.Info; //todo can display header like "csharp" etc
            var lineNb = 0;
            foreach (var line in codeBlock.Lines)
            {
                if (lineNb > 0)
                {
                    RenderInline(new LineBreakInline());
                }

                AddTextSpan(line.ToStringSafe(), (span) =>
                {
                    span.TextColor = CodeTextColor;
                    span.ParagraphColor = ColorCodeBlock;
                });
                lineNb++;
            }
        }
        else if (block is ListBlock listBlock)
        {
            foreach (var child in listBlock)
            {
                if (child is ListItemBlock listItem)
                {
                    if (listBlock.IsOrdered)
                    {
                        RenderOrderedListItem(listItem, listItem.Order);
                    }
                    else
                    {
                        RenderBulletListItem(listItem);
                    }
                }
                else
                {
                    //todo what can it be?..
                }
            }
        }
        else if (block is ListItemBlock listItem)
        {
            foreach (var line in listItem)
            {
                RenderBlock(line);
            }
        }

        isCodeBlock = wasCode;
        isHeading1 = wasHeading1;
        isHeading2 = wasHeading2;
    }



    protected void RenderOrderedListItem(ListItemBlock listItem, int startNumber)
    {
        LiteralInline prefix;
        var firstLine = true;
        foreach (var line in listItem)
        {
            if (firstLine)
            {
                // Add the number before rendering the list item content
                prefix = new LiteralInline(string.Format(PrefixNumbered, startNumber));
                firstLine = false;
            }
            else
            {
                prefix = null;
            }

            RenderBlock(line, prefix);
        }
    }

    protected void RenderBulletListItem(ListItemBlock listItem)
    {
        LiteralInline prefix;
        var firstLine = true;
        foreach (var line in listItem)
        {
            if (firstLine)
            {
                // Add the number before rendering the list item content
                prefix = new LiteralInline(PrefixBullet);
                firstLine = false;
            }
            else
            {
                prefix = null;
            }

            RenderBlock(line, prefix);
        }
    }

    protected void RenderInline(Inline inline)
    {
        bool wasBold = isBold;
        bool wasItalic = isItalic;
        bool wasStrikethrough = isStrikethrough;


        switch (inline)
        {
            case LineBreakInline lineBreak:
                //just add new line to previous span
                var last = Spans.LastOrDefault();
                if (last == null)
                {
                    Spans.Add(new() { Text = "\n" });
                    ;
                }
                else
                {
                    last.Text += "\n";
                }

                break;

            case LiteralInline literal:
                //todo detect available font
                AddTextSpan(literal.Content.ToStringSafe());
                break;

            case CodeInline code:
                AddCodeSpan(code);
                break;

            case LinkInline link:
                AddLinkSpan(link);
                break;

            case EmphasisInline emphasis:
                if (emphasis.DelimiterCount == 3)
                {
                    if (emphasis.DelimiterChar is '_' or '*')
                    {
                        isBold = true;
                        isItalic = true;
                    }
                }
                else if (emphasis.DelimiterCount == 2)
                {
                    if (emphasis.DelimiterChar == '~')
                    {
                        isStrikethrough = true;
                    }
                    else if (emphasis.DelimiterChar is '_' or '*')
                    {
                        isBold = true;
                    }
                }
                else if (emphasis.DelimiterCount == 1)
                {
                    if (emphasis.DelimiterChar is '_' or '*')
                    {
                        isItalic = true;
                    }
                }

                var child = emphasis.FirstChild;
                while (child != null)
                {
                    RenderInline(child);
                    child = child.NextSibling;
                }

                break;
        }

        // Restore the previous state
        isBold = wasBold;
        isItalic = wasItalic;
        isStrikethrough = wasStrikethrough;
    }

    protected virtual void AddCodeSpan(CodeInline code)
       {
           string text = code.Content;

           AddTextSpan(text, (span) =>
           {
               span.TextColor = this.CodeTextColor;
               span.BackgroundColor = CodeBackgroundColor;
           });
       }

    protected virtual void AddLinkSpan(LinkInline link)
       {
           string text = GetLinkLabelText(link);
           if (string.IsNullOrEmpty(text))
               text = link.Url;

           AddTextSpan(text, (span) =>
           {
               span.Tag = link.Url;
               span.TextColor = this.LinkColor;
               span.Underline = this.UnderlineLink;
               span.UnderlineWidth = this.UnderlineWidth;
               span.ForceCaptureInput = true;
           });
       }

    protected virtual string GetLinkLabelText(LinkInline link)
       {
           var child = link.FirstChild;
           string labelText = "";
           while (child != null)
           {
               if (child is LiteralInline literal)
               {
                   labelText += literal.Content;
               }

               child = child.NextSibling;
           }

           return labelText;
       }
     
}

*/
