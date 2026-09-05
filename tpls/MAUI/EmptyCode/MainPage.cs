
namespace EmptyCode;

/// <summary>
/// The whole app: one page hosting one drawing surface.
///
/// Derives from <see cref="BasePageReloadable"/>, so <see cref="Build"/> is called once the handler
/// is attached and again on every C# HotReload — edit the tree below, save, and the UI refreshes
/// without restarting the app.
/// </summary>
public partial class MainPage : BasePageReloadable, IDisposable
{
    private Canvas _canvas;

    public MainPage()
    {
        Title = "DrawnApp";

        // MAUI page background, visible only in the safe-area insets around the canvas.
        // Keep it equal to the canvas background so the insets do not flash a different color.
        BackgroundColor = Color.Parse("#0B0E14");
    }

    /// <summary>
    /// Builds the page. Called by the base class on handler attach and on every C# HotReload,
    /// so it must build a fresh tree every time and never reuse a cached instance.
    /// </summary>
    public override void Build()
    {
        _canvas?.Dispose();

        BindingContext = this;

        _canvas = new Canvas()
        {
            Gestures = GesturesMode.Lock,
            RenderingMode = RenderingModeType.Accelerated,
            BackgroundColor = Color.Parse("#0B0E14"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = CreateContent()
        };

        // MAUI layout specifics: using Grid to respect safe insets if we don't need fullscreen
        Content = new Grid()
        {
            Children = { _canvas }
        };
    }

    /// <summary>
    /// Builds what the canvas draws: your content plus the debug overlay.
    /// </summary>
    private SkiaControl CreateContent()
    {
        return new SkiaLayer()
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                //YOUR MAIN CONTENT 
                CreateMainContent(),

#if DEBUG
                new SkiaLabelFps()
                {
                    Margin = new Thickness(0, 0, 4, 24),
                    VerticalOptions = LayoutOptions.End,
                    HorizontalOptions = LayoutOptions.End,
                    Rotation = -45,
                    FontSize = 11,
                    BackgroundColor = Colors.DarkRed,
                    TextColor = Colors.White,
                    ZIndex = 110,
                },
#endif
            }
        };
    }

    // <fiddle:content>
    // Everything between these two markers is the exported UI: a method body that returns one
    // SkiaControl. It is the same contract as a DrawnUI Fiddle snippet, so a snippet pastes in
    // whole, and the Fiddle "Export MAUI" button replaces this region verbatim.
    /// <summary>
    /// Your UI starts here. Replace the body with your own tree.
    /// </summary>
    protected SkiaControl CreateMainContent()
    {
        var taps = 0;
        SkiaLabel counter = null;

        return new SkiaStack()
        {
            UseCache = SkiaCacheType.Image,
            Spacing = 16,
            Padding = new Thickness(24),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new SkiaSvg()
                {
                    Source = "drawnui.svg",
                    TintColor = Color.Parse("#4C8DFF"),
                    WidthRequest = 64,
                    HeightRequest = 64,
                    HorizontalOptions = LayoutOptions.Center,
                },

                new SkiaLabel("DrawnUI")
                {
                    FontFamily = "FontTextTitle",
                    FontSize = 28,
                    TextColor = Colors.White,
                    HorizontalOptions = LayoutOptions.Center,
                },

                new SkiaLabel("Everything here is drawn on one canvas.")
                {
                    FontSize = 14,
                    TextColor = Color.Parse("#A9B4C6"),
                    HorizontalTextAlignment = DrawTextAlignment.Center,
                    HorizontalOptions = LayoutOptions.Center,
                },

                new SkiaLabel("Nothing tapped yet")
                    {
                        FontSize = 14,
                        TextColor = Color.Parse("#4C8DFF"),
                        HorizontalOptions = LayoutOptions.Center,
                    }
                    .Assign(out counter),

                new SkiaButton("Tap me")
                    {
                        UseCache = SkiaCacheType.Image,
                        BackgroundColor = Color.Parse("#4C8DFF"),
                        TextColor = Colors.White,
                        CornerRadius = 10,
                        WidthRequest = 200,
                        HeightRequest = 44,
                        HorizontalOptions = LayoutOptions.Center,
                    }
                    .OnTapped(async me =>
                    {
                        taps++;
                        counter.Text = taps == 1 ? "Tapped once" : $"Tapped {taps} times";
                        await me.ScaleToAsync(0.96, 0.96, 60);
                        await me.ScaleToAsync(1, 1, 60);
                    }),
            }
        };
    }
    // </fiddle:content>

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            Content = null;
            _canvas?.Dispose();
            _canvas = null;
        }

        base.Dispose(isDisposing);
    }
}
