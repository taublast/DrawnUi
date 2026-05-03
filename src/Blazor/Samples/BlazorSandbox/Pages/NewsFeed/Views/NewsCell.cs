using DrawnUi.Draw;
using DrawnUI.Tutorials.NewsFeed;
using DrawnUi.Views;

namespace BlazorSandbox.Pages.NewsFeed;

public class NewsCell : DrawnListCell
{
    private const string SvgPlay = "<svg fill=\"#000000\" height=\"800px\" width=\"800px\" version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 472.615 472.615\" xml:space=\"preserve\"><g><g><path d=\"M236.308,0C105.799,0,0,105.798,0,236.308c0,130.507,105.799,236.308,236.308,236.308s236.308-105.801,236.308-236.308 C472.615,105.798,366.816,0,236.308,0z M139.346,347.733V124.88l229.37,111.428L139.346,347.733z\"/></g></g></svg>";

    private readonly SkiaLayout _backgroundLayer;
    private readonly BannerImage _avatarImage;
    private readonly SkiaLabel _authorLabel;
    private readonly SkiaLabel _timeLabel;
    private readonly SkiaRichLabel _titleLabel;
    private readonly SkiaRichLabel _contentLabel;
    private readonly SkiaShape _contentImage;
    private readonly BannerImage _contentImg;
    private readonly SkiaSvg _videoLayout;
    private readonly SkiaLayout _articleLayout;
    private readonly BannerImage _articleThumbnail;
    private readonly SkiaLabel _articleTitle;
    private readonly SkiaLabel _articleDescription;
    private readonly SkiaShape _adLayout;
    private readonly BannerImage _adImage;
    private readonly SkiaLabel _adTitle;
    private readonly SkiaRichLabel _likeButton;
    private readonly SkiaRichLabel _commentButton;

    public NewsCell()
    {
        DelayIncrementMs = 75;
        TimeAnimateMs = 150;
        TimeWindowMs = 100;

        _avatarImage = new BannerImage()
        {
            LoadSourceOnFirstDraw = true,
            EraseChangedContent = true,
            Aspect = TransformAspect.AspectFill,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        _authorLabel = new SkiaLabel()
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black
        };

        _timeLabel = new SkiaLabel()
        {
            FontSize = 12,
            TextColor = Colors.Gray
        };

        _titleLabel = new SkiaRichLabel()
        {
            UseCache = SkiaCacheType.Operations,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black,
            IsVisible = false
        };

        _contentLabel = new SkiaRichLabel()
        {
            UseCache = SkiaCacheType.Operations,
            FontSize = 14,
            TextColor = Colors.DarkSlateGray,
            LineBreakMode = LineBreakMode.WordWrap,
            IsVisible = false
        };

        _contentImg = new BannerImage()
        {
            LoadSourceOnFirstDraw = true,
            EraseChangedContent = true,
            BackgroundColor = Colors.LightGray,
            Aspect = TransformAspect.AspectCover,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };

        _videoLayout = new SkiaSvg()
        {
            UseCache = SkiaCacheType.Operations,
            SvgString = SvgPlay,
            WidthRequest = 50,
            LockRatio = 1,
            TintColor = Colors.White,
            Opacity = 0.66f,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        _contentImage = new SkiaShape()
        {
            IsVisible = false,
            CornerRadius = new CornerRadius(16, 0, 0, 0),
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = 200,
            Children =
            {
                _contentImg,
                _videoLayout
            }
        };

        _articleThumbnail = new BannerImage()
        {
            LoadSourceOnFirstDraw = true,
            EraseChangedContent = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = Colors.LightGray,
            Aspect = TransformAspect.AspectCover
        };

        _articleTitle = new SkiaLabel()
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Black,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        };

        _articleDescription = new SkiaLabel()
        {
            FontSize = 12,
            TextColor = Colors.Gray,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 3
        };

        _articleLayout = new SkiaLayout()
        {
            HorizontalOptions = LayoutOptions.Fill,
            UseCache = SkiaCacheType.Image,
            Type = LayoutType.Row,
            Spacing = 12,
            IsVisible = false,
            Children =
            {
                new SkiaShape()
                {
                    UseCache = SkiaCacheType.Image,
                    CornerRadius = new CornerRadius(8, 0, 0, 8),
                    WidthRequest = 80,
                    HeightRequest = 80,
                    Children =
                    {
                        _articleThumbnail
                    }
                },
                new SkiaLayout()
                {
                    Type = LayoutType.Column,
                    HorizontalOptions = LayoutOptions.Fill,
                    UseCache = SkiaCacheType.Operations,
                    Children =
                    {
                        _articleTitle,
                        _articleDescription
                    }
                }
            }
        };

        _adImage = new BannerImage()
        {
            LoadSourceOnFirstDraw = true,
            EraseChangedContent = true,
            Margin = new Thickness(0, 16, 0, 32),
            UseCache = SkiaCacheType.Image,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            Aspect = TransformAspect.AspectFill
        };

        _adTitle = new SkiaLabel()
        {
            VerticalOptions = LayoutOptions.End,
            UseCache = SkiaCacheType.Operations,
            FontSize = 14,
            Margin = new Thickness(8),
            FontAttributes = FontAttributes.Bold,
            MaxLines = 1,
            TextColor = Colors.Black
        };

        _adLayout = new SkiaShape()
        {
            HeightRequest = 150,
            BackgroundColor = Colors.LightGray,
            HorizontalOptions = LayoutOptions.Fill,
            UseCache = SkiaCacheType.Image,
            IsVisible = false,
            Children =
            {
                new SkiaLabel()
                {
                    UseCache = SkiaCacheType.Operations,
                    Text = "Sponsored",
                    FontSize = 10,
                    TextColor = Colors.Gray,
                    Margin = new Thickness(4, 0),
                    HorizontalOptions = LayoutOptions.End
                },
                _adImage,
                _adTitle
            }
        };

        _likeButton = new SkiaRichLabel()
        {
            HorizontalOptions = LayoutOptions.Center,
            Text = "👍",
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.Gray,
            FontSize = 14
        };

        _commentButton = new SkiaRichLabel()
        {
            HorizontalOptions = LayoutOptions.Center,
            Text = "💬",
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.Gray,
            FontSize = 14
        };

        _backgroundLayer = new SkiaLayout()
        {
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            UseCache = SkiaCacheType.Image,
            Padding = new Thickness(16, 6, 16, 10),
            Children =
            {
                new SkiaShape()
                {
                    CornerRadius = 0,
                    BackgroundColor = Colors.White,
                    VerticalOptions = LayoutOptions.Fill,
                    HorizontalOptions = LayoutOptions.Fill,
                    VisualEffects =
                    {
                        new DropShadowEffect()
                        {
                            Color = Colors.Black,
                            Blur = 3,
                            X = 3,
                            Y = 3
                        }
                    }
                }
            }
        };

        AddSubView(_backgroundLayer);
        AddSubView(new SkiaLayout()
        {
            Margin = new Thickness(16, 6, 16, 10),
            Padding = new Thickness(16),
            Type = LayoutType.Column,
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                new SkiaLayout()
                {
                    Type = LayoutType.Row,
                    Spacing = 8,
                    UseCache = SkiaCacheType.Image,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaShape()
                        {
                            Type = ShapeType.Circle,
                            WidthRequest = 40,
                            HeightRequest = 40,
                            BackgroundColor = Colors.LightGray,
                            Children =
                            {
                                _avatarImage
                            }
                        },
                        new SkiaLayout()
                        {
                            Type = LayoutType.Column,
                            UseCache = SkiaCacheType.Operations,
                            HorizontalOptions = LayoutOptions.Fill,
                            Children =
                            {
                                _authorLabel,
                                _timeLabel
                            }
                        }
                    }
                },
                _titleLabel,
                _contentLabel,
                _contentImage,
                _articleLayout,
                _adLayout,
                new SkiaLayout()
                {
                    Type = LayoutType.Row,
                    UseCache = SkiaCacheType.Operations,
                    Spacing = 0,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaLayout()
                        {
                            HorizontalOptions = LayoutOptions.Fill,
                            WidthRequest = 90,
                            Children =
                            {
                                _likeButton
                            }
                        },
                        new SkiaLayout()
                        {
                            HorizontalOptions = LayoutOptions.Fill,
                            WidthRequest = 90,
                            Children =
                            {
                                _commentButton
                            }
                        },
                        new SkiaRichLabel()
                        {
                            HorizontalOptions = LayoutOptions.Center,
                            Text = "📤",
                            BackgroundColor = Colors.Transparent,
                            TextColor = Colors.Gray,
                            FontSize = 14
                        }
                    }
                }
            }
        });
    }

    protected override void SetContent(object ctx)
    {
        base.SetContent(ctx);

        if (ctx is NewsItem news)
        {
            ConfigureForContentType(news);
        }
    }

    private void ConfigureForContentType(NewsItem news)
    {
        HideAllContent();

        _authorLabel.Text = news.AuthorName;
        _timeLabel.Text = GetRelativeTime(news.PublishedAt);
        //_avatarImage.Source = news.AuthorAvatarUrl;
        _likeButton.Text = $"👍 {news.LikesCount}";
        _commentButton.Text = $"💬 {news.CommentsCount}";

        switch (news.Type)
        {
            case NewsType.Text:
                ConfigureTextPost(news);
                break;
            case NewsType.Image:
                ConfigureImagePost(news);
                break;
            case NewsType.Video:
                ConfigureVideoPost(news);
                break;
            case NewsType.Article:
                ConfigureArticlePost(news);
                break;
            case NewsType.Ad:
                ConfigureAdPost(news);
                break;
        }
    }

    private void HideAllContent()
    {
        _titleLabel.IsVisible = false;
        _contentLabel.IsVisible = false;
        _contentImage.IsVisible = false;
        _videoLayout.IsVisible = false;
        _articleLayout.IsVisible = false;
        _adLayout.IsVisible = false;
    }

    private void ConfigureTextPost(NewsItem news)
    {
        if (!string.IsNullOrEmpty(news.Title))
        {
            _titleLabel.Text = news.Title;
            _titleLabel.IsVisible = true;
        }

        _contentLabel.Text = news.Content;
        _contentLabel.IsVisible = true;
    }

    private void ConfigureImagePost(NewsItem news)
    {
        _contentImg.Source = news.ImageUrl;
        _contentImage.IsVisible = true;

        if (!string.IsNullOrEmpty(news.Content))
        {
            _contentLabel.Text = news.Content;
            _contentLabel.IsVisible = true;
        }
    }

    private void ConfigureVideoPost(NewsItem news)
    {
        _contentImg.Source = ExtractVideoThumbnail(news.VideoUrl);
        _contentImage.IsVisible = true;
        _videoLayout.IsVisible = true;

        if (!string.IsNullOrEmpty(news.Content))
        {
            _contentLabel.Text = news.Content;
            _contentLabel.IsVisible = true;
        }
    }

    private void ConfigureArticlePost(NewsItem news)
    {
        _articleThumbnail.Source = news.ImageUrl;
        _articleTitle.Text = news.Title;
        _articleDescription.Text = news.Content;
        _articleLayout.IsVisible = true;
    }

    private void ConfigureAdPost(NewsItem news)
    {
        _adImage.Source = news.ImageUrl;
        _adTitle.Text = news.Title;
        _adLayout.IsVisible = true;
    }

    private static string GetRelativeTime(DateTime publishedAt)
    {
        var delta = DateTime.Now - publishedAt;
        return delta.TotalDays >= 1
            ? publishedAt.ToString("MMM dd")
            : delta.TotalHours >= 1
                ? $"{(int)delta.TotalHours}h"
                : $"{(int)delta.TotalMinutes}m";
    }

    private static string ExtractVideoThumbnail(string videoUrl)
    {
        return videoUrl;
    }
}
