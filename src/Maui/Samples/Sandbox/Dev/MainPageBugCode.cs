using DrawnUi.Views;

namespace Sandbox;

public class MainPageBugCode : BasePageReloadable
{
    /*


    layout for stack :

    SkiaLayout:
        MeasureStack, cells measured in 2 passes -> LayoutCell -> cell.Arrange(..)


        SkiaControl:
            Arrange(..) ->
            PostArrange(..) ->
            AdaptCachedLayout(..) -> ArrangedDestination.Offset(destination.Left, destination.Top);

        In case of the bugged child:

        1. PostArrange from LayoutCell with destination = full canvas width
        2. PostArrange from Draw with destination = full canvas width
        3. PostArrange from DrawUsingRenderObject with destination = parent layout
        
     */

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
            Gestures = GesturesMode.Enabled,
            RenderingMode = RenderingModeType.Accelerated,

            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.Fill,
            WidthRequest = 300,
            HeightRequest = 300,
            BackgroundColor = Colors.Gray,

            Content = new SkiaLayout()
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.Bisque,
                Children = new List<SkiaControl>()
                {
                    new SkiaLayout()
                    {
                        HeightRequest = 200,
                        BackgroundColor = Colors.Green,
                        Children = new List<SkiaControl>()
                        {
                            new SkiaLayout()
                            {
                                ColumnSpacing = 0,
                                BackgroundColor = Colors.Yellow,
                                Margin=new Thickness(0),
                                Type = LayoutType.Grid,
                                RowDefinitions = new RowDefinitionCollection()
                                {
                                    new RowDefinition(new GridLength(50,GridUnitType.Absolute)),
                                    new RowDefinition(new GridLength(1,GridUnitType.Star)),
                                },
                                Children = new List<SkiaControl>()
                                {
                                    new SkiaLabel()
                                    {
                                        Text = "Star",
                                        HeightRequest = 40,
                                        BackgroundColor = Colors.Red,
                                        HorizontalOptions = LayoutOptions.Fill,
                                    }.WithColumn(1),
                                    new SkiaLabel()
                                    {
                                        Text = "Auto",
                                        HeightRequest = 40,
                                        BackgroundColor = Colors.Orange,
                                        HorizontalOptions = LayoutOptions.Start,
                                    }.WithColumn(2),
                                    new SkiaLabel()
                                    {
                                        Text = "Ahahahahahahaa",
                                        HeightRequest = 40,
                                        BackgroundColor = Colors.Green,
                                        HorizontalOptions = LayoutOptions.Center,
                                    }.WithRow(1).WithColumnSpan(3),
                                }
                            }.WithColumnDefinitions("10, *, Auto"),


                        }
                    },

                }
            }

            /*
            Content = new SkiaLayout()
            {
                Tag = "Bugged",
                Padding = new Thickness(0),
                Type = LayoutType.Wrap,
                HeightRequest = 200,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0),
                Spacing = 8,
                BackgroundColor = Colors.Bisque,
                Children = new List<SkiaControl>()
                {

                    new SkiaLabel
                    {
                        ZIndex = 1,
                        Tag = "Label",
                        BackgroundColor = Colors.GhostWhite,
                        Text = $"Drawn {_reloads}",
                        HorizontalOptions = LayoutOptions.Center,
                        FontSize = 28
                    },

                    new SkiaShape
                    {
                        Tag = "Shape",
                        VerticalOptions = LayoutOptions.Start,
                        HorizontalOptions = LayoutOptions.Center,
                        WidthRequest = 100,
                        HeightRequest = 100,
                        Type = ShapeType.Circle,
                        BackgroundColor = Colors.Orange
                    }

                }
            }
            */
        };

        this.Content = Canvas;
    }



}
