 
using Sandbox.Views;
 
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox
{
    public class MainPageCodeStack : BasePageCodeBehind, IDisposable
    {
        Canvas Canvas;

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                this.Content = null;
                Canvas?.Dispose();
            }

            base.Dispose(isDisposing);
        }


        public override void Build()
        {
            Canvas?.Dispose();

            Canvas = new Canvas()
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.LightGray,
                Gestures = GesturesMode.Enabled,
                Children =
                {
                    new SkiaLayout()
                    {
                        //AUTOSIZED COLUMN
                        Type = LayoutType.Column,
                        Spacing = 0,
                        VerticalOptions = LayoutOptions.Fill,
                        Children = new List<SkiaControl>()
                        {

                            new SkiaLayout()
                            {
                                HeightRequest = 100,
                                HorizontalOptions = LayoutOptions.Fill,
                                BackgroundColor = Colors.Red,
                            },

                            //AUTOSIZED ABSOLUTE
                            new SkiaLayout()
                            {
                                //WidthRequest = MessageMaxWidth,
                                BackgroundColor = Colors.Green,
                                Spacing = 0,
                                WidthRequest = 200,
                                VerticalOptions = LayoutOptions.Fill,
                                /*
                                Children = new List<SkiaControl>()
                                {
                                    new SkiaShape() //VLine
                                    {
                                        Margin = new Thickness(8, 8, 0, 8),
                                        CornerRadius = 0,
                                        BackgroundColor = Colors.IndianRed,
                                        HorizontalOptions = LayoutOptions.Start,
                                        WidthRequest = 2,
                                        VerticalOptions = LayoutOptions.Fill,
                                    },
                                      
                                    new SkiaLabel()
                                    {
                                        LineBreakMode = LineBreakMode.TailTruncation,
                                        MaxLines = 1,
                                        Text = $"...",
                                        FontSize = 15,
                                        WidthRequest = 200,
                                        Margin = new Thickness(16, 28, 8, 8), //TODO BUG 2 - looks like absolute bug
                                        TextColor = Colors.Black,
                                    }
                                }
                                */
                            },

                            new SkiaLayout()
                            {
                                HeightRequest = 100,
                                HorizontalOptions = LayoutOptions.Fill,
                                BackgroundColor = Colors.Blue,
                            }

           
                        }
                    }
                }
            };


            this.Content = Canvas;
        }
    }
}
