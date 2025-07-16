namespace DrawnUI.Tutorials.NewsFeed.Models;

public enum NewsType
{
    Text,
    Image, 
    Video,
    Article,
    Ad
}

public class NewsItem
{
    public long Id { get; set; }
    public NewsType Type { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string ImageUrl { get; set; }
    public string VideoUrl { get; set; }
    public string ArticleUrl { get; set; }
    public string AuthorName { get; set; }
    public string AuthorAvatarUrl { get; set; }
    public DateTime PublishedAt { get; set; }
    public int LikesCount { get; set; }
    public int CommentsCount { get; set; }
}