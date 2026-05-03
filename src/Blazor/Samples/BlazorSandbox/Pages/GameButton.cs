using System.Diagnostics;
using AppoMobi.Gestures;
using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Infrastructure.Models;
using DrawnUi.Infrastructure.Xaml;
using DrawnUi.Views;

namespace BlazorSandbox.Pages;

public class GameButton : SkiaLayout
{
    public GameButton()
    {
        UseCache = SkiaCacheType.Image;
    }

    protected override void CreateDefaultContent()
    {
        base.CreateDefaultContent();

        if (Views.Count == 0)
        {
            AddSubView(CreateView());
        }
    }

    private void MapProperties()
    {
        if (Control != null)
        {
            DarkColor = TintColor.MakeDarker(25);
            Control.Bevel.ShadowColor = DarkColor;
            Control.FillGradient.Colors = new Color[] { TintColor, DarkColor };
        }
    }

    protected virtual SkiaShape CreateView()
    {
        var startColor = TintColor;
        var endColor = TintColor.MakeDarker(20);

        return new SkiaShape()
        {
            UseCache = SkiaCacheType.Image,
            CornerRadius = 8,
            MinimumWidthRequest = 120,
            BackgroundColor = Colors.Black,
            BevelType = BevelType.Bevel,
            Bevel = new SkiaBevel()
            {
                Depth = 2,
                LightColor = Colors.White,
                ShadowColor = Colors.DarkBlue,
                Opacity = 0.33f,
            },
            Children =
            {
                new SkiaLayout()
                {
                    Type = LayoutType.Row,
                    Margin = new Thickness(16, 8),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Spacing = 6,
                    Children =
                    {
                        new SkiaGif()
                        {
                            VerticalOptions = LayoutOptions.Center,
                            WidthRequest = 40,
                            HeightRequest = 40,
                            Aspect = TransformAspect.AspectFit,
                            AutoPlay = true,
                            Repeat = -1,
                        }.ObserveProperty(this, nameof(LeftImageSource),
                            me =>
                            {
                                me.Source = GetImageSourceUri(LeftImageSource);
                                me.IsVisible = LeftImageSource != null;
                            }),
                        new SkiaRichLabel()
                            {
                                Text = Text,
                                UseCache = SkiaCacheType.Operations,
                                HorizontalTextAlignment = DrawTextAlignment.Center,
                                VerticalOptions = LayoutOptions.Center,
                                FontSize = 16,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Colors.White,
                            }.Assign(out TextLabel)
                            .ObserveProperty(this, nameof(Text),
                                me =>
                                {
                                    me.Text = Text;
                                }),
                    }
                }
            },
            FillGradient = new SkiaGradient()
            {
                StartXRatio = 0,
                EndXRatio = 1,
                StartYRatio = 0,
                EndYRatio = 0.5f,
                Colors = new Color[] { startColor, endColor }
            },
        }.WithGestures((me, args, _) =>
        {
            if (args.Type == TouchActionResult.Tapped)
            {
                Clicked?.Invoke(this, EventArgs.Empty);
            }
            else if (args.Type == TouchActionResult.Down)
            {
                SetButtonPressed(me);
            }
            else if (args.Type == TouchActionResult.Up)
            {
                SetButtonReleased(me);
                return null;
            }

            return me;
        });
    }

    public static void SetButtonPressed(SkiaShape btn)
    {
        btn.Children[0].TranslationX = 1.5;
        btn.Children[0].TranslationY = 1.5;
        btn.BevelType = BevelType.Emboss;
        Debug.WriteLine("Pressed");
    }

    public static void SetButtonReleased(SkiaShape btn)
    {
        btn.Children[0].TranslationX = 0;
        btn.Children[0].TranslationY = 0;
        btn.BevelType = BevelType.Bevel;
        Debug.WriteLine("Released");
    }

    public event EventHandler? Clicked;

    private SkiaLabel? TextLabel;

    protected Color DarkColor = Colors.HotPink;

    private SkiaShape? Control
    {
        get
        {
            if (Views.Count == 0)
            {
                return null;
            }

            return Views[0] as SkiaShape;
        }
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(GameButton),
        string.Empty);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty TintColorProperty = BindableProperty.Create(
        nameof(TintColor),
        typeof(Color),
        typeof(GameButton),
        Colors.HotPink,
        propertyChanged: OnLookChanged);

    public Color TintColor
    {
        get => (Color)GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    public static readonly BindableProperty LeftImageSourceProperty = BindableProperty.Create(
        nameof(LeftImageSource),
        typeof(ImageSource),
        typeof(GameButton),
        null);

    [System.ComponentModel.TypeConverter(typeof(FrameworkImageSourceConverter))]
    public ImageSource LeftImageSource
    {
        get => (ImageSource)GetValue(LeftImageSourceProperty);
        set => SetValue(LeftImageSourceProperty, value);
    }

    private static void OnLookChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GameButton control)
        {
            control.MapProperties();
        }
    }

    private static string? GetImageSourceUri(ImageSource source)
    {
        if (source == null || source.IsEmpty)
        {
            return null;
        }

        if (source is UriImageSource sourceUri)
        {
            return sourceUri.Uri.ToString();
        }

        if (source is FileImageSource sourceFile)
        {
            return sourceFile.File;
        }

        if (source is ImageSourceResourceStream stream)
        {
            return stream.Url;
        }

        return source.ToString();
    }
}