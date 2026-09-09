using DrawnUi.Draw;
using DrawnUi.Views;
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox;

/// <summary>
/// Repro for issue #338: SkiaLabel with MaxLines=1 + LineBreakMode.TailTruncation
/// reports 2 lines instead of 1 when the text has no break opportunity (single long word),
/// so ContentSize is twice the line height and VerticalTextAlignment=Center is skipped.
/// Text containing spaces truncates and centers correctly - that is the difference.
/// </summary>
public class Issue338Page : BasePageReloadable, IDisposable
{
    const string LongWithSpaces =
        "This is a very long navigation bar title used to verify whether an ellipsis is displayed at the end when the width is exceeded";

    const string LongNoSpaces =
        "Averyveryverylongsinglewordthatdoesnotfitatallintothelabelwidthandmustbetruncated";

    private Canvas? _canvas;

    private SkiaLabel _caseNoSpaces = null!;
    private SkiaLabel _caseWithSpaces = null!;
    private SkiaLabel _caseShort = null!;
    private SkiaLabel _report = null!;

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            Content = null;
            _canvas?.Dispose();
        }

        base.Dispose(isDisposing);
    }

    static SkiaLabel Subject(string text) => new SkiaLabel()
    {
        Text = text,
        FontSize = 17,
        MaxLines = 1,
        LineBreakMode = LineBreakMode.TailTruncation,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalTextAlignment = DrawTextAlignment.Center,
        TextColor = Colors.White,
        BackgroundColor = Colors.OrangeRed, // visualises the label bounds
        HeightRequest = 50,
        HorizontalOptions = LayoutOptions.Fill,
    };

    static SkiaLabel Caption(string text) => new SkiaLabel(text)
    {
        FontSize = 13,
        TextColor = Colors.Black,
        HorizontalOptions = LayoutOptions.Fill,
    };

    string Describe(string tag, SkiaLabel label) =>
        $"{tag}: LinesCount={label.LinesCount} ContentHeightPx={label.ContentSize.Pixels.Height:0.##} LineHeightPx={label.MeasuredLineHeight:0.##}";

    void Report()
    {
        var text = string.Join("\n",
            Describe("no spaces  (BUG)", _caseNoSpaces),
            Describe("with spaces (ok)", _caseWithSpaces),
            Describe("short       (ok)", _caseShort));

        _report.Text = text;
        Console.WriteLine($"[Issue338]\n{text}");
    }

    public override void Build()
    {
        _canvas?.Dispose();

        _canvas = new Canvas()
        {
            RenderingMode = RenderingModeType.Accelerated,
            Gestures = GesturesMode.Lock,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = Colors.White,
            Content = new SkiaLayout()
            {
                Type = LayoutType.Column,
                Spacing = 10,
                Padding = new Thickness(20, 40),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children =
                {
                    new SkiaLabel("Issue #338 - MaxLines=1 + TailTruncation")
                    {
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        TextColor = Colors.Black,
                    },

                    Caption("1. Long text WITHOUT spaces - text sticks to the TOP (bug)"),
                    Subject(LongNoSpaces).Assign(out _caseNoSpaces),

                    Caption("2. Long text WITH spaces - truncated and centered (ok)"),
                    Subject(LongWithSpaces).Assign(out _caseWithSpaces),

                    Caption("3. Short text, no truncation - centered (ok)"),
                    Subject("Demo.App").Assign(out _caseShort),

                    new SkiaButton("Report measurement")
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        HeightRequest = 44,
                    }.OnTapped(me => Report()),

                    new SkiaLabel("tap the button")
                    {
                        FontSize = 14,
                        TextColor = Colors.DarkRed,
                        HorizontalOptions = LayoutOptions.Fill,
                    }.Assign(out _report),
                }
            }
            .Initialize(me => { me.LayoutIsReady += (s, e) => Report(); })
        };

        this.Content = _canvas;
    }
}
