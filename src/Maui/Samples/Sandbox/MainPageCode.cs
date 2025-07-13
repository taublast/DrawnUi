using System.Collections.ObjectModel;
using System.Diagnostics;
using AppoMobi.Specials;
using Sandbox.Views;
using Canvas = DrawnUi.Views.Canvas;

namespace Sandbox
{

    public class TrackCell : SkiaShape
    {
        public override ScaledSize OnMeasuring(float widthConstraint, float heightConstraint, float scale)
        {
            var ret = base.OnMeasuring(widthConstraint, heightConstraint, scale);

            Debug.WriteLine($"[CELL] {BindingContext} measured {ret.Pixels}");
            return ret;
        }
    }

    public class MainPageCode : BasePageCodeBehind, IDisposable
    {
        Canvas Canvas;

        public ObservableRangeCollection<string>Source { get; } = new ();

        //case for developing partial cells changes
        //without remeasuring all inside templated stack

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

            Source.Clear();
            Source.AddRange( new [] { "one", "two", "three" });
            BindingContext = this;

            SkiaButton btn;

            Canvas = new Canvas()
            {
                Gestures = GesturesMode.Enabled,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.White,
                Content = new SkiaLayout()
                {
                    Tag = "WrapperStack",
                    Type = LayoutType.Column,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    Children = new List<SkiaControl>()
                    {

                        new SkiaButton()
                        {
                            Tag = "Button",
                            BackgroundColor = Colors.GreenYellow,
                            TextColor = Colors.Black,
                            UseCache = SkiaCacheType.Image,
                            Text = "Add Item",
                            HeightRequest = 40,
                            HorizontalOptions = LayoutOptions.Center,
                            WidthRequest = 200,
                        }.OnTapped((me) =>
                        {
                            me.TranslationX += RndExtensions.CreateRandom(1, 20);
                            Source.Add("new item");
                        }),

                        new SkiaScroll()
                        {
           
                            BackgroundColor = Colors.Red,
                            Tag = "MainScroll",
                            HorizontalOptions = LayoutOptions.Start,
                            VerticalOptions = LayoutOptions.Fill,
                            Content = new SkiaLayout()
                            {
                                Type = LayoutType.Column,
                                Spacing = 8,
                                ItemsSource = Source,
                                Tag = "ScrollContentStack",
                                MeasureItemsStrategy = MeasuringStrategy.MeasureAll,
                                RecyclingTemplate = RecyclingTemplate.Disabled,
                                ItemTemplate = new DataTemplate(() =>
                                {
                                    var cell = new TrackCell()
                                    {
                                        Tag = "ScrollCell",
                                        BackgroundColor = Colors.Bisque,
                                        UseCache = SkiaCacheType.Image,
                                        HorizontalOptions = LayoutOptions.Fill,
                                        HeightRequest = 80,
                                        Margin = 0,
                                        Children = new List<SkiaControl>()
                                        {
                                            new SkiaLabel()
                                            {
                                                UseCache = SkiaCacheType.Operations,
                                            }.Adapt((
                                                label) =>
                                            {
                                                label.SetBinding(SkiaLabel.TextProperty, ".");
                                            })
                                        }
                                    };

                                    cell.Tapped += (sender, args) =>
                                    {
                                        Debug.WriteLine($"TAPPED {(sender as SkiaControl).BindingContext}");
                                    };

                                    return cell;
                                })
                            }
                        }

                    }
                }


             
            };


            this.Content = Canvas;
        }
    }
}
