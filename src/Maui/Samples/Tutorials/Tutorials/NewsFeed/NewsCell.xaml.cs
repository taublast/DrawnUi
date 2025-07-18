using System.ComponentModel;
using DrawnUI.Tutorials.NewsFeed.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace DrawnUI.Tutorials.NewsFeed;

public partial class NewsCell : SkiaDynamicDrawnCell
{
    public NewsCell()
    {
        InitializeComponent();
    }

    protected override void SetContent(object ctx)
    {
        base.SetContent(ctx);

        if (ctx is NewsItem news)
        {
            ConfigureForContentType(news);
        }
    }

    public override void OnWillDisposeWithChildren()
    {
        base.OnWillDisposeWithChildren();

        PaintPlaceholder?.Dispose();
    }

    private SKPaint PaintPlaceholder;

    public override void DrawPlaceholder(DrawingContext context)
    {
        var margins = BackgroundLayer.Padding;
        var area =
                new SKRect((float)(context.Destination.Left + margins.Left * context.Scale),
            (float)(context.Destination.Top + margins.Top * context.Scale),
        (float)(context.Destination.Right - margins.Right * context.Scale),
        (float)(context.Destination.Bottom - margins.Bottom * context.Scale));

        PaintPlaceholder ??= new SKPaint
        {
            Color = SKColor.Parse("#FFFFFF"),
            Style = SKPaintStyle.Fill,
        };

        context.Context.Canvas.DrawRect(area, PaintPlaceholder);
    }

    private void ConfigureForContentType(NewsItem news)
    {
        // Reset all content visibility
        HideAllContent();

        // Configure common elements

        //DebugId.Text = $"{news.Id}"; //for debugging

        AuthorLabel.Text = news.AuthorName;
        TimeLabel.Text = GetRelativeTime(news.PublishedAt);
        AvatarImage.Source = news.AuthorAvatarUrl;
        LikeButton.Text = $"👍 {news.LikesCount}";
        CommentButton.Text = $"💬 {news.CommentsCount}";

        // Configure based on content type
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
        TitleLabel.IsVisible = false;
        ContentLabel.IsVisible = false;
        ContentImage.IsVisible = false;
        VideoLayout.IsVisible = false;
        ArticleLayout.IsVisible = false;
        AdLayout.IsVisible = false;
    }

    private void ConfigureTextPost(NewsItem news)
    {
        if (!string.IsNullOrEmpty(news.Title))
        {
            TitleLabel.Text = news.Title;
            TitleLabel.IsVisible = true;
        }

        ContentLabel.Text = news.Content;
        ContentLabel.IsVisible = true;
    }

    private void ConfigureImagePost(NewsItem news)
    {
        ContentImg.Source = news.ImageUrl;
        ContentImage.IsVisible = true;

        if (!string.IsNullOrEmpty(news.Content))
        {
            ContentLabel.Text = news.Content;
            ContentLabel.IsVisible = true;
        }
    }

    private void ConfigureVideoPost(NewsItem news)
    {
        ContentImg.Source = ExtractVideoThumbnail(news.VideoUrl);
        ContentImage.IsVisible = true;
        VideoLayout.IsVisible = true;

        if (!string.IsNullOrEmpty(news.Content))
        {
            ContentLabel.Text = news.Content;
            ContentLabel.IsVisible = true;
        }
    }

    private void ConfigureArticlePost(NewsItem news)
    {
        ArticleThumbnail.Source = news.ImageUrl;
        ArticleTitle.Text = news.Title;
        ArticleDescription.Text = news.Content;
        ArticleLayout.IsVisible = true;
    }

    private void ConfigureAdPost(NewsItem news)
    {
        AdImage.Source = news.ImageUrl;
        AdTitle.Text = news.Title;
        AdLayout.IsVisible = true;
    }

    private string GetRelativeTime(DateTime publishedAt)
    {
        var delta = DateTime.Now - publishedAt;
        return delta.TotalDays >= 1
            ? publishedAt.ToString("MMM dd")
            : delta.TotalHours >= 1
                ? $"{(int)delta.TotalHours}h"
                : $"{(int)delta.TotalMinutes}m";
    }

    private string ExtractVideoThumbnail(string videoUrl)
    {
        // Extract thumbnail from video URL or use placeholder
        return videoUrl; // For now, just use the same URL as it's from Picsum
    }
}
