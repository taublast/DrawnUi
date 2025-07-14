using System.Diagnostics;
using DrawnUi.Controls;
using Sandbox.Models;

namespace Sandbox.Views;

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

    protected override void OnCacheCreated()
    {
        base.OnCacheCreated();

        if (BindingContext is NewsItem item)
        {
            Debug.WriteLine($"Cache created for ID {item.Id} cell {this.Uid}");
        }
    }

    //protected override void OnCacheDestroyed()
    //{
    //    base.OnCacheDestroyed();

    //    if (BindingContext is NewsItem item)
    //    {
    //        Debug.WriteLine($"Cache KILLED when ID {item.Id} cell {this.Uid}");
    //    }
    //}

    private void ConfigureForContentType(NewsItem news)
    {
        // Reset all content visibility
        HideAllContent();

        // Configure common elements

        DebugId.Text = $"{news.Id}";
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
        ContentImage.Source = news.ImageUrl;
        ContentImage.IsVisible = true;

        if (!string.IsNullOrEmpty(news.Content))
        {
            ContentLabel.Text = news.Content;
            ContentLabel.IsVisible = true;
        }
    }

    private void ConfigureVideoPost(NewsItem news)
    {
        VideoThumbnail.Source = ExtractVideoThumbnail(news.VideoUrl);
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
