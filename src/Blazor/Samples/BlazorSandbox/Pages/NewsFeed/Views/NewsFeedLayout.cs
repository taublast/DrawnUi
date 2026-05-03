using DrawnUi.Draw;
using DrawnUi.Views;
using DrawnUI.Tutorials.NewsFeed;

namespace BlazorSandbox.Pages.NewsFeed.Views
{
    public class NewsFeedLayout : SkiaLayer
    {
        private readonly NewsViewModel _viewModel;

        public NewsFeedLayout()
        {
            try
            {
                _viewModel = new NewsViewModel();
                BindingContext = _viewModel;

                Tag = "Wrapper";
                VerticalOptions = LayoutOptions.Fill;

                Children = new List<SkiaControl>()
                {
                    CreateContent(),

                    new SkiaLabelFps()
                    {
                        UseCache = SkiaCacheType.GPU,
                        Margin = new (0, 0, 4, 24),
                        VerticalOptions = LayoutOptions.End,
                        HorizontalOptions = LayoutOptions.End,
                        Rotation = -45,
                        BackgroundColor = Colors.DarkRed,
                        TextColor = Colors.White,
                        ZIndex = 110,
                    }
                };
            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
            }
        }

        private SkiaControl CreateContent()
        {
            return new SkiaScroll()
            {
                Orientation = ScrollOrientation.Vertical,
                FrictionScrolled = 0.5f,
                ChangeVelocityScrolled = 1.35f,
                RefreshEnabled = true,
                RefreshShowDistance = 100,
                RefreshDistanceLimit = 120,
                RefreshCommand = _viewModel.RefreshCommand,
                LoadMoreCommand = _viewModel.LoadMoreCommand,
                LoadMoreOffset = 500,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                RefreshIndicator = new AppActivityIndicator()
                {
                    UseCache = SkiaCacheType.Operations
                },
                Header = new SkiaLayer()
                {
                    HeightRequest = 40,
                    UseCache = SkiaCacheType.Image,
                    Children =
                {
                    new SkiaRichLabel()
                    {
                        Text = "DrawnUI News Feed Tutorial",
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    }
                }
                },
                Footer = new SkiaLayer()
                {
                    HeightRequest = 50
                },
                Content = new SkiaLayout()
                {
                    Tag = "NewsStack",
                    Type = LayoutType.Column,
                    ItemsSource = _viewModel.NewsItems,
                    RecyclingTemplate = RecyclingTemplate.Enabled,
                    MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
                    ReserveTemplates = 10,
                    VirtualisationInflated = 200,
                    Spacing = 0,
                    ItemTemplateType = typeof(NewsCell),
                    HorizontalOptions = LayoutOptions.Fill,
                }
            }
            .ObserveProperty(_viewModel, nameof(NewsViewModel.IsRefreshing), me =>
            {
                me.IsRefreshing = _viewModel.IsRefreshing;
            });
        }

    }
}
