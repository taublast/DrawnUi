using DrawnUi.Views;

namespace DrawnUI.Tutorials.FirstApp;

public partial class FirstAppPageCode : BasePageReloadable, IDisposable
{
    Canvas Canvas;

    private int clickCount = 0;
    private SkiaButton btnClickMe;

    public FirstAppPageCode()
    {
        Title = "First App Tutorial (Code)";
        BackgroundColor = Color.Parse("#f5f5f5");
    }

    /// <summary>
    /// Will be called by .NET HotReload
    /// </summary>
    public override void Build()
    {
        Canvas?.Dispose();

        Canvas = new Canvas()
        {
            RenderingMode = RenderingModeType.Accelerated,
            Gestures = GesturesMode.Enabled,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            BackgroundColor = Color.Parse("#f5f5f5"),
            Content = CreateMainLayout()
        };

        this.Content = Canvas;
    }

    SkiaLayout CreateMainLayout()
    {
        return new SkiaLayout()
        {
            Type = LayoutType.Column,
            HorizontalOptions = LayoutOptions.Center,
            Padding = new Thickness(40),
            Spacing = 16,
            UseCache = SkiaCacheType.Operations,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new SkiaImage()
                {
                    UseCache = SkiaCacheType.Image,
                    Aspect = TransformAspect.AspectFit,
                    WidthRequest = 200,
                    Source = @"Images\dotnetbotcar.png",
                    HorizontalOptions = LayoutOptions.Center,
                },
                new SkiaLabel("DrawnUI for .NET MAUI")
                {
                    UseCache = SkiaCacheType.Operations,
                    FontSize = 24,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.DarkSlateBlue,
                    HorizontalTextAlignment = DrawTextAlignment.Center,
                    HorizontalOptions = LayoutOptions.Center,
                },
                new SkiaRichLabel("This text is drawn with SkiaSharp ✨")
                {
                    UseCache = SkiaCacheType.Operations,
                    HorizontalTextAlignment = DrawTextAlignment.Center,
                    FontSize = 16,
                    TextColor = Colors.Gray,
                    HorizontalOptions = LayoutOptions.Center,
                },
                new SkiaButton("Click Me!")
                    {
                        UseCache = SkiaCacheType.Image,
                        BackgroundColor = Colors.CornflowerBlue,
                        TextColor = Colors.White,
                        CornerRadius = 8,
                        HorizontalOptions = LayoutOptions.Center,
                    }
                    .Assign(out btnClickMe)
                    .OnTapped(async me =>
                    {
                        clickCount++;
                        me.Text = $"Clicked {clickCount} times! 🎉";
                        await me.ScaleToAsync(1.1, 1.1, 100);
                        await me.ScaleToAsync(1, 1, 100);
                    }),
                new SkiaRichLabel()
                {
                    UseCache = SkiaCacheType.Operations,
                    Text = "👆 Try clicking the button",
                    FontSize = 14,
                    TextColor = Colors.Green,
                    HorizontalOptions = LayoutOptions.Center,
                },
                new SkiaRichLabel()
                    {
                        UseCache = SkiaCacheType.Operations,
                        FontSize = 14,
                        TextColor = Colors.Green,
                        HorizontalOptions = LayoutOptions.Center,
                    }
                    .ObserveProperties(() => btnClickMe, [nameof(SkiaButton.Text), nameof(SkiaButton.IsPressed)],
                        me =>
                        {
                            me.Text =
                                $"Observing button: \"..{btnClickMe.Text.Right(12)}\", is pressed: {btnClickMe.IsPressed}";
                        })
            }
        };
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            this.Content = null;
            Canvas?.Dispose();
        }

        base.Dispose(isDisposing);
    }
}
