using DrawnUi.Views;
using DrawnUi.Draw;
using DrawnUi.Controls;
using DrawnUi.Infrastructure.Enums;
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox
{
    public class MainPageEditors : BasePageReloadable, IDisposable
    {
        Canvas Canvas;

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                Content = null;
                Canvas?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        public override void Build()
        {
            Canvas?.Dispose();

            Canvas = new Canvas()
            {
                RenderingMode = RenderingModeType.Default,
                Gestures = GesturesMode.Enabled,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                Content = new SkiaStack()
                {
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Fill,
                    Padding = new Thickness(16),
                    Spacing = 16,
                    Children =
                    {
                        // new SkiaMauiEntry()
                        // {
                        //     HorizontalOptions = LayoutOptions.Fill,
                        //     HeightRequest = 44,
                        //     PlaceholderText = "SkiaMauiEntry",
                        // },

                        new SkiaEditor()
                        {
                            HorizontalOptions = LayoutOptions.Fill,
                            HeightRequest = 120,
                            BackgroundColor = Color.Parse("#22000000"),
                            Padding = new Thickness(8),
                            FontSize = 16,
                            TextColor = Colors.Black,
                            CursorColor = Colors.DodgerBlue,
                        },
                    }
                }
            };

            Content = new Grid()
            {
                Children = { Canvas }
            };
        }
    }
}
